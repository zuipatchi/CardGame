using Common.Deck;
using Main.Card;
using UnityEngine.UIElements;

namespace Matching
{
    public partial class MatchingPresenter
    {
        // ─── 使用デッキ選択（オンライン対戦で使うデッキの切り替え） ────────────────────

        // 「使用デッキ」ボタン：対戦に使うデッキを選ぶモーダルを開く。
        private void OnDeckSelectClicked()
        {
            _soundPlayer.PlaySE(_soundStore.EnterSE);
            BuildDeckSelectList();
            _deckSelectOverlay.style.display = DisplayStyle.Flex;
        }

        private void OnDeckSelectCloseClicked()
        {
            _soundPlayer.PlaySE(_soundStore.Cancel1SE);
            _deckSelectOverlay.style.display = DisplayStyle.None;
        }

        // 9 スロットの一覧（名前・枚数・使用中マーク）を組む。行タップで使用デッキを切り替える。
        private void BuildDeckSelectList()
        {
            _deckSelectList.Clear();
            int selected = _deckRepository.SelectedIndex;
            for (int i = 0; i < DeckRepository.SlotCount; i++)
            {
                int slot = i;
                VisualElement row = new VisualElement();
                row.AddToClassList("deck-row");
                if (slot == selected)
                {
                    row.AddToClassList("deck-row--selected");
                }
                row.RegisterCallback<ClickEvent>(_ => OnDeckRowClicked(slot));

                // 左にデッキのシンボル（代表カード全体）を表示する。シンボル未設定はカード裏面で代替する。
                VisualElement thumbnail = new VisualElement();
                thumbnail.AddToClassList("deck-row__symbol");
                thumbnail.pickingMode = PickingMode.Ignore;
                ApplyDeckSymbol(thumbnail, slot);
                row.Add(thumbnail);

                Label nameLabel = new Label(_deckRepository.LoadName(slot));
                nameLabel.AddToClassList("deck-row__name");
                nameLabel.pickingMode = PickingMode.Ignore;
                row.Add(nameLabel);

                Label badge = new Label("使用中");
                badge.AddToClassList("deck-row__badge");
                badge.style.display = slot == selected ? DisplayStyle.Flex : DisplayStyle.None;
                badge.pickingMode = PickingMode.Ignore;
                row.Add(badge);

                int count = _deckRepository.LoadCount(slot);
                Label countLabel = new Label($"{count}/{DeckModel.MaxCards}");
                countLabel.AddToClassList("deck-row__count");
                if (count == DeckModel.MaxCards)
                {
                    countLabel.AddToClassList("deck-row__count--ready");
                }
                countLabel.pickingMode = PickingMode.Ignore;
                row.Add(countLabel);

                _deckSelectList.Add(row);
            }
        }

        // 使用デッキを切り替えて対戦用の DeckModel を差し替える。クイックマッチ/作成ボタンの可否も更新する。
        private void OnDeckRowClicked(int slot)
        {
            _soundPlayer.PlaySE(_soundStore.EnterSE);
            _deckRepository.SelectedIndex = slot;
            _deckRepository.Load(_deckModel, slot);
            UpdateDeckSelectButtonLabel();
            UpdateActionButtons();
            _deckSelectOverlay.style.display = DisplayStyle.None;
        }

        // 使用デッキボタンに、選択中デッキのシンボル画像と名前を表示する。
        // ボタンは Button のテキストではなく、シンボル用 VisualElement + ラベルの2子要素で構成する（初回のみ生成）。
        private void UpdateDeckSelectButtonLabel()
        {
            if (_deckSelectButton == null)
            {
                return;
            }
            if (_deckSelectSymbol == null)
            {
                _deckSelectButton.text = string.Empty;
                _deckSelectSymbol = new VisualElement();
                _deckSelectSymbol.AddToClassList("deck-select-symbol");
                _deckSelectSymbol.pickingMode = PickingMode.Ignore;
                _deckSelectButton.Add(_deckSelectSymbol);

                _deckSelectLabel = new Label();
                _deckSelectLabel.AddToClassList("deck-select-label");
                _deckSelectLabel.pickingMode = PickingMode.Ignore;
                _deckSelectButton.Add(_deckSelectLabel);
            }
            int slot = _deckRepository.SelectedIndex;
            ApplyDeckSymbol(_deckSelectSymbol, slot);
            _deckSelectLabel.text = $"使用デッキ：{_deckRepository.LoadName(slot)}";
        }

        // 指定スロットのシンボル（代表カード）画像を target の背景に設定する。
        // シンボル未設定はカード裏面で代替し、どちらも無ければ背景なし。
        private void ApplyDeckSymbol(VisualElement target, int slot)
        {
            string favoriteId = _deckRepository.LoadFavorite(slot);
            if (_cardDatabase != null && !string.IsNullOrEmpty(favoriteId)
                && _cardDatabase.TryGet(favoriteId, out CardData favorite) && favorite.Image != null)
            {
                target.style.backgroundImage = new StyleBackground(favorite.Image);
            }
            else if (_cardBack != null)
            {
                target.style.backgroundImage = new StyleBackground(_cardBack);
            }
            else
            {
                target.style.backgroundImage = StyleKeyword.None;
            }
        }

        // 部屋を探している間だけデッキ変更とマッチング開始を許可する。
        // マッチング開始ボタンは、有効なデッキ（規定枚数）のときのみ押せる。
        private void UpdateActionButtons()
        {
            bool browsing = _model.State.Value == MatchingState.BrowsingRooms;
            bool playable = IsDeckPlayable();
            _quickMatchButton.SetEnabled(browsing && playable);
            _createButton.SetEnabled(browsing && playable);
            _deckSelectButton.SetEnabled(browsing);
        }

        // 対戦を開始できるデッキかどうか。通常はちょうど DeckModel.MaxCards 枚（IsValid）が必要だが、
        // Editor 再生時に「デッキ枚数制限」トグルを OFF にした場合は 1 枚以上であれば開始できる。
        private bool IsDeckPlayable()
        {
            if (_deckRuleModel != null && !_deckRuleModel.LimitDeckCount)
            {
                return _deckModel.Count > 0;
            }
            return _deckModel.IsValid;
        }
    }
}
