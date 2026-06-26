using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using UnityEngine.UIElements;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // SummonFromDeckByKeyword 効果：発動側のデッキから keyword と同じ特徴を持つキャラを1枚選んで
        // 自フィールドに出す（デッキから消費）。配置時に OnEnter も発動する。
        // 特徴一致キャラがデッキにいない／フィールド満杯なら空振り。
        // 選択はプレイヤー（UI）／CPU（最高コスト）／オンライン相手（デッキ内インデックスを受信）で分岐する。
        // デッキ並び順は両クライアントで同期済みのため、インデックス指定で同じカードを取り除ける（DamageEnemy と同じチャネルを流用）。
        internal async UniTask ApplySummonFromDeckByKeywordAsync(string keyword, bool isLocal, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                return;
            }

            FieldView field = isLocal ? _playerFieldView : _opponentFieldView;
            DeckView deck = isLocal ? _playerDeckView : _opponentDeckView;

            if (field.IsCharactersFull || _isGameOver)
            {
                return;
            }

            // デッキ内から特徴一致のキャラを抽出（インデックスは deck と一致）
            IReadOnlyList<CardData> deckCards = deck.GetCardDataSnapshot();
            List<int> candidates = new List<int>();
            for (int i = 0; i < deckCards.Count; i++)
            {
                if (deckCards[i] is CharacterCardData && deckCards[i].Keyword == keyword)
                {
                    candidates.Add(i);
                }
            }
            if (candidates.Count == 0)
            {
                return;
            }

            int chosenIndex = await ResolveDeckSummonTargetAsync(candidates, deckCards, keyword, isLocal, ct);
            if (chosenIndex < 0 || chosenIndex >= deckCards.Count)
            {
                return;
            }

            // デッキから抜いてフィールドへ（SummonChar と同じ配置・登場演出・OnEnter 経路を使う）
            CardData data = deck.RemoveCardAt(chosenIndex);
            deck.RefreshCount();
            if (data == null)
            {
                return;
            }
            await SummonSingleCharAsync(data, field, isLocal, ct);
        }

        // 召喚するデッキカードのインデックスを決定する。
        // ローカル：候補が1枚なら自動・複数ならピッカーで選択（選んだらオンラインへインデックス送信）。
        // オンライン相手：インデックスを受信。CPU：最高コストの候補を選ぶ。
        private async UniTask<int> ResolveDeckSummonTargetAsync(List<int> candidates, IReadOnlyList<CardData> deckCards, string keyword, bool isLocal, CancellationToken ct)
        {
            if (isLocal)
            {
                int chosenIndex = candidates.Count == 1
                    ? candidates[0]
                    : await WaitForPlayerDeckCardSelectionAsync(candidates, deckCards, keyword, ct);
                if (_isOnline)
                {
                    _networkGameService.SendDamageTargets(new[] { chosenIndex });
                }
                return chosenIndex;
            }

            if (_isOnline)
            {
                int[] indices = await _networkGameService.WaitForOpponentDamageTargetsAsync(ct);
                return (indices != null && indices.Length > 0) ? indices[0] : candidates[0];
            }

            // CPU：最高コストの候補を狙う
            int best = candidates[0];
            foreach (int idx in candidates)
            {
                if (deckCards[idx].Cost > deckCards[best].Cost)
                {
                    best = idx;
                }
            }
            return best;
        }

        // 候補キャラを並べたオーバーレイを表示し、プレイヤーが1枚クリックで選ぶのを待つ。
        // 選んだカードのデッキ内インデックスを返す。
        private async UniTask<int> WaitForPlayerDeckCardSelectionAsync(List<int> candidates, IReadOnlyList<CardData> deckCards, string keyword, CancellationToken ct)
        {
            _deckCardSelectionTcs = new UniTaskCompletionSource<int>();

            VisualElement overlay = new VisualElement();
            overlay.AddToClassList("deck-pick-overlay");

            // パネル本体（縦長のダークパネル。上辺をアクセントカラーで縁取り）
            VisualElement panel = new VisualElement();
            panel.AddToClassList("deck-pick-panel");

            // ヘッダー：タイトル（特徴名）＋サブタイトル
            VisualElement header = new VisualElement();
            header.AddToClassList("deck-pick-header");
            header.pickingMode = PickingMode.Ignore;

            Label title = new Label(string.IsNullOrEmpty(keyword) ? "デッキから召喚" : $"『{keyword}』を召喚");
            title.AddToClassList("deck-pick-title");
            header.Add(title);
            panel.Add(header);

            VisualElement divider = new VisualElement();
            divider.AddToClassList("deck-pick-divider");
            divider.pickingMode = PickingMode.Ignore;
            panel.Add(divider);

            // カードを並べる「ステージ」。縦方向のスペースを確保しつつ候補カードを中央に配置する
            VisualElement stage = new VisualElement();
            stage.AddToClassList("deck-pick-stage");

            ScrollView scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.AddToClassList("deck-pick-scroll");
            scroll.contentContainer.AddToClassList("deck-pick-row");

            foreach (int idx in candidates)
            {
                CardData data = deckCards[idx];
                CardView card = new CardView(_cardStore.CardTemplate, data, _cardStore.CardBack, faceDown: false, isOpponent: false);
                card.AddToClassList("deck-pick-card");
                int captured = idx;
                // クリックは詳細モーダルを開く。決定は詳細パネル下部の「このキャラを召喚」ボタンで行う（誤タップ防止）。
                card.RegisterCallback<ClickEvent>(_ =>
                    _cardDetailModal.Show(data, "このキャラを召喚", _ => _deckCardSelectionTcs?.TrySetResult(captured)));
                scroll.Add(card);
            }
            stage.Add(scroll);
            panel.Add(stage);

            Label hint = new Label("カードをタップして詳細を開き召喚");
            hint.AddToClassList("deck-pick-hint");
            hint.pickingMode = PickingMode.Ignore;
            panel.Add(hint);

            overlay.Add(panel);
            _mainRoot.Add(overlay);

            try
            {
                return await _deckCardSelectionTcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                overlay.RemoveFromHierarchy();
                _deckCardSelectionTcs = null;
            }
        }
    }
}
