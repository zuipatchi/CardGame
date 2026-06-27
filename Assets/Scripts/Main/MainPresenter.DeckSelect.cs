using Common.Deck;
using Main.Card;
using UnityEngine.UIElements;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // ─── リザルト画面の使用デッキ変更 ────────────────────────────────
        // ゲーム終了オーバーレイの「使用デッキ」ボタンから、次の再戦で使うデッキを切り替える。
        // 切替は Common 常駐の DeckModel/DeckRepository を書き換えるため、再戦（Main 再ロード）後の
        // 対戦に反映される（CPU・オンラインとも BuildAsync が _deckModel を読み直す）。

        private void OpenDeckSelectModal()
        {
            EnsureDeckSelectModal();
            if (_deckSelectOverlay == null)
            {
                return;
            }
            BuildDeckSelectList();
            _deckSelectOverlay.style.display = DisplayStyle.Flex;
        }

        private void CloseDeckSelectModal()
        {
            if (_deckSelectOverlay != null)
            {
                _deckSelectOverlay.style.display = DisplayStyle.None;
            }
        }

        // モーダルは初回オープン時に一度だけ生成し、ゲーム終了オーバーレイと同じ親（MainRoot）へ重ねる。
        private void EnsureDeckSelectModal()
        {
            if (_deckSelectOverlay != null)
            {
                return;
            }

            VisualElement root = _gameEndOverlay != null ? _gameEndOverlay.parent : null;
            if (root == null)
            {
                return;
            }

            _deckSelectOverlay = new VisualElement();
            _deckSelectOverlay.AddToClassList("deck-select-overlay");
            _deckSelectOverlay.style.display = DisplayStyle.None;

            VisualElement panel = new VisualElement();
            panel.AddToClassList("deck-select-panel");
            _deckSelectOverlay.Add(panel);

            Label title = new Label("使用デッキをえらぶ");
            title.AddToClassList("deck-select-title");
            title.pickingMode = PickingMode.Ignore;
            panel.Add(title);

            _deckSelectList = new ScrollView();
            _deckSelectList.AddToClassList("deck-select-scroll");
            panel.Add(_deckSelectList);

            Button closeButton = new Button(() =>
            {
                _soundPlayer.PlaySE(_soundStore.Cancel1SE);
                CloseDeckSelectModal();
            });
            closeButton.text = "閉じる";
            closeButton.AddToClassList("deck-select-close");
            panel.Add(closeButton);

            root.Add(_deckSelectOverlay);
        }

        // 9 スロットの一覧（シンボル・名前・枚数・使用中マーク）を組む。行タップで使用デッキを切り替える。
        private void BuildDeckSelectList()
        {
            if (_deckSelectList == null)
            {
                return;
            }
            _deckSelectList.Clear();
            int selected = _deckRepository.SelectedIndex;
            for (int i = 0; i < DeckRepository.SlotCount; i++)
            {
                int slot = i;
                VisualElement row = new VisualElement();
                row.AddToClassList("deck-select-row");
                if (slot == selected)
                {
                    row.AddToClassList("deck-select-row--selected");
                }
                row.RegisterCallback<ClickEvent>(_ => OnDeckRowClicked(slot));

                VisualElement symbol = new VisualElement();
                symbol.AddToClassList("deck-select-row__symbol");
                symbol.pickingMode = PickingMode.Ignore;
                ApplyDeckSymbol(symbol, slot);
                row.Add(symbol);

                Label nameLabel = new Label(_deckRepository.LoadName(slot));
                nameLabel.AddToClassList("deck-select-row__name");
                nameLabel.pickingMode = PickingMode.Ignore;
                row.Add(nameLabel);

                Label badge = new Label("使用中");
                badge.AddToClassList("deck-select-row__badge");
                badge.style.display = slot == selected ? DisplayStyle.Flex : DisplayStyle.None;
                badge.pickingMode = PickingMode.Ignore;
                row.Add(badge);

                int count = _deckRepository.LoadCount(slot);
                Label countLabel = new Label($"{count}/{DeckModel.MaxCards}");
                countLabel.AddToClassList("deck-select-row__count");
                if (count == DeckModel.MaxCards)
                {
                    countLabel.AddToClassList("deck-select-row__count--ready");
                }
                countLabel.pickingMode = PickingMode.Ignore;
                row.Add(countLabel);

                _deckSelectList.Add(row);
            }
        }

        // 使用デッキを切り替えて次回対戦用の DeckModel を差し替える。再戦ボタンの可否も更新する。
        private void OnDeckRowClicked(int slot)
        {
            _soundPlayer.PlaySE(_soundStore.Enter3SE);
            _deckRepository.SelectedIndex = slot;
            _deckRepository.Load(_deckModel, slot);
            UpdateGameEndDeckButtonLabel();
            UpdateRematchAvailability();
            CloseDeckSelectModal();
        }

        // リザルトの「使用デッキ」ボタンに、選択中デッキのシンボル画像と名前を表示する。
        private void UpdateGameEndDeckButtonLabel()
        {
            if (_deckSelectButtonLabel == null)
            {
                return;
            }
            int slot = _deckRepository.SelectedIndex;
            ApplyDeckSymbol(_deckSelectButtonSymbol, slot);
            _deckSelectButtonLabel.text = $"使用デッキ：{_deckRepository.LoadName(slot)}";
        }

        // 指定スロットのシンボル（代表カード）画像を target の背景に設定する。
        // シンボル未設定はカード裏面（CardStore.CardBack）で代替し、どちらも無ければ背景なし。
        private void ApplyDeckSymbol(VisualElement target, int slot)
        {
            if (target == null)
            {
                return;
            }
            string favoriteId = _deckRepository.LoadFavorite(slot);
            if (_cardDatabase != null && !string.IsNullOrEmpty(favoriteId)
                && _cardDatabase.TryGet(favoriteId, out CardData favorite) && favorite.Image != null)
            {
                target.style.backgroundImage = new StyleBackground(favorite.Image);
            }
            else if (_cardStore != null && _cardStore.CardBack != null)
            {
                target.style.backgroundImage = new StyleBackground(_cardStore.CardBack);
            }
            else
            {
                target.style.backgroundImage = StyleKeyword.None;
            }
        }

        // 有効な枚数のデッキ（規定枚数。Editor 再生時に枚数制限 OFF なら 1 枚以上）でなければ再戦を押せないようにする。
        // 空・未完成のスロットに切り替えて不正なデッキで再戦が始まるのを防ぐ。
        private void UpdateRematchAvailability()
        {
            if (_gameEndRematchButton == null)
            {
                return;
            }
            _gameEndRematchButton.SetEnabled(IsDeckPlayable());
        }

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
