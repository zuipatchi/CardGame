using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // ─── マリガン ────────────────────────────────────────────────────

        private async UniTask RunOpponentMulliganAnimationAsync(CardData placeholder, int handSize, CardData[] newDeck, CancellationToken ct)
        {
            await PlayReturnHandToDeckAsync(_opponentHandView, _opponentDeckView, ct);

            // 配る手札分（プレースホルダ）を上に積んで満杯から始め、配牌で減らす。引き切ると残りは newDeck と一致する。
            _opponentDeckView.Rebuild(newDeck.Concat(Enumerable.Repeat(placeholder, handSize)).ToArray());

            await UniTask.NextFrame(ct);

            Rect deckRect = _opponentDeckView.worldBound;
            UniTask[] drawTasks = new UniTask[handSize];
            for (int i = 0; i < handSize; i++)
            {
                drawTasks[i] = DealCardFromDeckAsync(_opponentHandView, _opponentDeckView, placeholder, deckRect, i * DrawStagger, ct);
            }
            await UniTask.WhenAll(drawTasks);
        }

        private async UniTask<bool> RunPlayerMulliganAsync(
            CardData[] fullDeck, HandView hand, DeckView deck, int handSize, CancellationToken ct)
        {
            bool chose = await WaitForMulliganChoiceAsync(ct);
            if (!chose)
            {
                return false;
            }

            await PlayAnnouncementAsync("マリガン", "turn-announcement-label--mulligan", ct);
            await PlayReturnHandToDeckAsync(hand, deck, ct);

            CardData[] reshuffled = CardArrayUtils.Shuffle((CardData[])fullDeck.Clone());
            CardData[] newHandCards = reshuffled.Take(handSize).ToArray();
            CardData[] newDeckCards = reshuffled.Skip(handSize).ToArray();

            // 配る手札分を上に積んで満杯から始め、配牌で減らす。引き切ると残りは newDeckCards と一致する（オンライン同期の並びも保持）。
            deck.Rebuild(newDeckCards.Concat(newHandCards).ToArray());

            await UniTask.NextFrame(ct);

            Rect deckRect = deck.worldBound;
            UniTask[] drawTasks = new UniTask[handSize];
            for (int i = 0; i < handSize; i++)
            {
                drawTasks[i] = DealCardFromDeckAsync(hand, deck, newHandCards[i], deckRect, i * DrawStagger, ct);
            }
            await UniTask.WhenAll(drawTasks);
            return true;
        }

        private async UniTask<bool> WaitForMulliganChoiceAsync(CancellationToken ct)
        {
            _mulliganChoicePending = true;

            VisualElement overlay = new VisualElement();
            overlay.AddToClassList("mulligan-overlay");
            // 背景部分のクリックは下の手札カードへ透過させ、確認中もカード詳細を開けるようにする
            // （Yes/No ボタンは子要素として通常どおりクリックを受け取る）
            overlay.pickingMode = PickingMode.Ignore;

            Label label = new Label("マリガンしますか？");
            label.AddToClassList("mulligan-label");
            label.pickingMode = PickingMode.Ignore;
            overlay.Add(label);

            VisualElement buttonRow = new VisualElement();
            buttonRow.AddToClassList("mulligan-button-row");

            Button yesButton = new Button();
            yesButton.AddToClassList("mulligan-button");
            yesButton.style.backgroundImage = new StyleBackground(_cardStore.YesButtonImage);

            Button noButton = new Button();
            noButton.AddToClassList("mulligan-button");
            noButton.style.backgroundImage = new StyleBackground(_cardStore.NoButtonImage);

            buttonRow.Add(yesButton);
            buttonRow.Add(noButton);
            overlay.Add(buttonRow);
            _mainRoot.Add(overlay);
            _mulliganOverlay = overlay;

            UniTaskCompletionSource<bool> tcs = new UniTaskCompletionSource<bool>();
            yesButton.clicked += () =>
            {
                _soundPlayer.PlaySE(_soundStore.Enter3SE);
                tcs.TrySetResult(true);
            };
            noButton.clicked += () =>
            {
                _soundPlayer.PlaySE(_soundStore.Enter3SE);
                tcs.TrySetResult(false);
            };
            ct.Register(() => tcs.TrySetCanceled());

            bool result;
            try
            {
                result = await tcs.Task;
            }
            catch (OperationCanceledException)
            {
                overlay.RemoveFromHierarchy();
                _mulliganOverlay = null;
                _mulliganChoicePending = false;
                return false;
            }

            overlay.RemoveFromHierarchy();
            _mulliganOverlay = null;
            _mulliganChoicePending = false;
            return result;
        }

        private async UniTask RunCpuMulliganIfNeededAsync(
            CardData[] fullDeck, HandView hand, DeckView deck, int handSize, CancellationToken ct)
        {
            IReadOnlyList<CardView> cards = hand.Cards;
            bool hasCharacter = false;
            foreach (CardView card in cards)
            {
                if (card.Data is CharacterCardData)
                {
                    hasCharacter = true;
                    break;
                }
            }

            if (hasCharacter)
            {
                return;
            }

            await PlayReturnHandToDeckAsync(hand, deck, ct);

            CardData[] reshuffled = CardArrayUtils.Shuffle((CardData[])fullDeck.Clone());
            CardData[] newHandCards = reshuffled.Take(handSize).ToArray();
            CardData[] newDeckCards = reshuffled.Skip(handSize).ToArray();

            // 配る手札分を上に積んで満杯から始め、配牌で減らす。引き切ると残りは newDeckCards と一致する（オンライン同期の並びも保持）。
            deck.Rebuild(newDeckCards.Concat(newHandCards).ToArray());

            await UniTask.NextFrame(ct);

            Rect deckRect = deck.worldBound;
            UniTask[] drawTasks = new UniTask[handSize];
            for (int i = 0; i < handSize; i++)
            {
                drawTasks[i] = DealCardFromDeckAsync(hand, deck, newHandCards[i], deckRect, i * DrawStagger, ct);
            }
            await UniTask.WhenAll(drawTasks);
        }
    }
}
