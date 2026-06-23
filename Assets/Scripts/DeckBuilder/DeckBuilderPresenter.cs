using Common.Deck;
using Common.SceneManagement;
using Cysharp.Threading.Tasks;
using Main.Card;
using UnityEngine.UIElements;
using VContainer;

namespace DeckBuilder
{
    // プレイヤーのデッキビルダー。1 シーン内で「スロット一覧（9枠）」と「編集」の 2 画面を
    // フルスクリーンオーバーレイの表示／非表示で切り替える。枠を選ぶとそのスロットの編集画面に入り、
    // 編集内容はそのスロットへ自動保存する。対戦に使うデッキ（SelectedIndex）の選択は、スロット一覧画面
    // 上部の「使用デッキ」ボタン→ Home 風の一覧モーダルでここからも行える（Home と同じ SelectedIndex を更新する）。
    public sealed class DeckBuilderPresenter : DeckBuilderPresenterBase
    {
        private DeckRepository _deckRepository;

        private VisualElement _slotOverlay;
        private VisualElement _slotGrid;
        // 使用デッキ（対戦に使う SelectedIndex）を選ぶ：上部ボタン＋ Home 風の一覧モーダル。
        private Button _deckSelectButton;
        private VisualElement _deckSelectOverlay;
        private ScrollView _deckSelectList;
        // 現在編集中のスロット（対戦に使う SelectedIndex とは独立）。
        private int _editingSlot;
        // カード読み込みが完了したか。完了前に枠を開いた場合、デッキ展開は読み込み後の
        // 基底の InitializeDeck / RefreshFilter に任せる（編集UIが未構築のため）。
        private bool _cardsLoaded;
        // 読み込み中に枠を開いたか。true のときは _editingSlot を読み込み後の初期描画に使う。
        private bool _pendingOpen;

        [Inject]
        public void Construct(
            CardStore cardStore,
            CardDatabase cardDatabase,
            DeckModel deckModel,
            DeckRuleModel deckRuleModel,
            DeckRepository deckRepository,
            SceneTransitioner sceneTransitioner)
        {
            _cardStore = cardStore;
            _cardDatabase = cardDatabase;
            _deckModel = deckModel;
            _deckRuleModel = deckRuleModel;
            _deckRepository = deckRepository;
            _sceneTransitioner = sceneTransitioner;
        }

        protected override void InitializeDeck()
        {
            // 読み込み中に枠を開いていればその枠を、そうでなければ既定として使用中スロットを編集対象にする。
            // （枠を開いていない場合は一覧オーバーレイが編集画面を覆うため _deckModel の内容は表に出ない）
            if (!_pendingOpen)
            {
                _editingSlot = _deckRepository.SelectedIndex;
            }
            _deckRepository.Load(_deckModel, _editingSlot);
        }

        protected override void SaveDeck()
        {
            _deckRepository.Save(_deckModel, _editingSlot);
        }

        // 基底の「もどる」は編集画面でのみ押せる（一覧表示中はオーバーレイが覆う）。
        // 編集画面からはスロット一覧へ戻す。一覧からホームへ戻るのは一覧専用の「もどる」が担う。
        protected override void NavigateBack()
        {
            ShowSlotList();
        }

        // カード詳細モーダルに「デッキのシンボルに設定」ボタンを出す（プレイヤーのデッキビルダーのみ）。
        protected override string CardDetailActionLabel => "★ デッキのシンボルに設定";

        // 詳細を開いたカードを、今編集中のデッキのシンボル（スロットに小さく表示する代表カード）に設定する。
        protected override void OnCardDetailAction(CardData card)
        {
            _deckRepository.SaveFavorite(_editingSlot, card.Id);
            ShowToast("デッキのシンボルに設定しました", success: true);
        }

        // カード読み込み前にデッキ選択画面を組んで表示する（ローディング中も見えるように）。
        protected override void OnBeforeLoad(VisualElement root)
        {
            BuildSlotOverlay(root);
            ShowSlotList();
        }

        // カード読み込み完了。これ以降は枠タップでその場でデッキを展開できる
        //（読み込み中に開いた枠は基底の InitializeDeck / RefreshFilter が既に描画済み）。
        protected override void OnDeckBuilderReady(VisualElement root)
        {
            _cardsLoaded = true;
            // 読み込み中に枠を開いていなければ、まだスロット一覧を表示中。カード裏面は読み込み後に
            // しか得られないため、ここで一覧を組み直してシンボル未設定スロットに裏面を反映する。
            if (!_pendingOpen)
            {
                RebuildSlotGrid();
            }
            _pendingOpen = false;
        }

        private void BuildSlotOverlay(VisualElement root)
        {
            _slotOverlay = new VisualElement();
            _slotOverlay.AddToClassList("deckbuilder-slot-overlay");

            VisualElement header = new VisualElement();
            header.AddToClassList("deckbuilder-slot-header");

            Button backButton = new Button();
            backButton.text = "もどる";
            backButton.AddToClassList("deckbuilder-button");
            backButton.AddToClassList("deckbuilder-button--back");
            backButton.clicked += () =>
            {
                _soundPlayer.PlaySE(_soundStore.Cancel1SE);
                _sceneTransitioner.Transit(Scenes.Home).Forget();
            };
            header.Add(backButton);

            Label title = new Label("デッキ編集");
            title.AddToClassList("deckbuilder-slot-title");
            header.Add(title);

            // 使用デッキ（対戦に使う SelectedIndex）を選ぶボタンを画面上部中央に置く。
            // 押すと Home と同じ一覧モーダルを開く（Home 側の使用デッキ選択と同じ SelectedIndex を更新する）。
            VisualElement deckSelect = new VisualElement();
            deckSelect.AddToClassList("deckbuilder-deck-select");
            // ヘッダ全幅に絶対配置して中央寄せするため、空き領域が「もどる」ボタンのクリックを
            // 邪魔しないよう自身はピック対象外にする（子のボタンは操作可能）。
            deckSelect.pickingMode = PickingMode.Ignore;

            _deckSelectButton = new Button();
            _deckSelectButton.AddToClassList("deckbuilder-deck-select-button");
            _deckSelectButton.clicked += OpenDeckSelectModal;
            deckSelect.Add(_deckSelectButton);
            header.Add(deckSelect);

            UpdateDeckSelectButtonLabel();

            _slotOverlay.Add(header);

            _slotGrid = new VisualElement();
            _slotGrid.AddToClassList("deckbuilder-slot-grid");
            _slotOverlay.Add(_slotGrid);

            BuildDeckSelectModal();

            root.Add(_slotOverlay);
        }

        // 使用デッキ一覧モーダル（Home の使用デッキ選択と同じ見た目）を組む。スロット一覧の上に重ねる。
        private void BuildDeckSelectModal()
        {
            _deckSelectOverlay = new VisualElement();
            _deckSelectOverlay.AddToClassList("deckbuilder-deck-modal-overlay");
            _deckSelectOverlay.style.display = DisplayStyle.None;
            // 暗幕クリックで閉じる。
            _deckSelectOverlay.RegisterCallback<ClickEvent>(_ => CloseDeckSelectModal());

            VisualElement panel = new VisualElement();
            panel.AddToClassList("deckbuilder-deck-modal-panel");
            // パネル内クリックは暗幕に伝播させない（閉じない）。
            panel.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            Label title = new Label("使用デッキをえらぶ");
            title.AddToClassList("deckbuilder-deck-modal-title");
            panel.Add(title);

            _deckSelectList = new ScrollView();
            _deckSelectList.AddToClassList("deckbuilder-deck-modal-list");
            panel.Add(_deckSelectList);

            Button closeButton = new Button();
            closeButton.text = "✕";
            closeButton.AddToClassList("deckbuilder-deck-modal-close");
            closeButton.clicked += () =>
            {
                _soundPlayer.PlaySE(_soundStore.Cancel1SE);
                CloseDeckSelectModal();
            };
            panel.Add(closeButton);

            _deckSelectOverlay.Add(panel);
            _slotOverlay.Add(_deckSelectOverlay);
        }

        private void ShowSlotList()
        {
            RebuildSlotGrid();
            _slotOverlay.style.display = DisplayStyle.Flex;
        }

        private void RebuildSlotGrid()
        {
            _slotGrid.Clear();

            int selected = _deckRepository.SelectedIndex;
            for (int i = 0; i < DeckRepository.SlotCount; i++)
            {
                int slot = i;
                VisualElement card = new VisualElement();
                card.AddToClassList("deckbuilder-slot-card");
                if (slot == selected)
                {
                    card.AddToClassList("deckbuilder-slot-card--selected");
                }
                card.RegisterCallback<ClickEvent>(_ => OpenSlot(slot));

                // 左にデッキのシンボル（代表カード全体）を表示する。シンボル未設定のスロットは
                // カードの裏面を代わりに表示する（裏面はカード読み込み後にしか無いので、その時は何も出さない）。
                VisualElement thumbnail = new VisualElement();
                thumbnail.AddToClassList("deckbuilder-slot-favorite");
                thumbnail.pickingMode = PickingMode.Ignore;
                string favoriteId = _deckRepository.LoadFavorite(slot);
                if (!string.IsNullOrEmpty(favoriteId)
                    && _cardDatabase.TryGet(favoriteId, out CardData favorite) && favorite.Image != null)
                {
                    thumbnail.style.backgroundImage = new StyleBackground(favorite.Image);
                }
                else if (_cardStore != null && _cardStore.CardBack != null)
                {
                    thumbnail.style.backgroundImage = new StyleBackground(_cardStore.CardBack);
                }
                card.Add(thumbnail);

                VisualElement info = new VisualElement();
                info.AddToClassList("deckbuilder-slot-info");

                // デッキ名（左・伸縮）と名前変更ボタン（右）を横並びにする。
                VisualElement nameRow = new VisualElement();
                nameRow.AddToClassList("deckbuilder-slot-name-row");

                Label nameLabel = new Label(_deckRepository.LoadName(slot));
                nameLabel.AddToClassList("deckbuilder-slot-name");
                nameRow.Add(nameLabel);

                Button renameButton = new Button();
                renameButton.text = "✎";
                renameButton.AddToClassList("deckbuilder-slot-rename");
                // カードのクリック（編集を開く）に伝播させないよう ClickEvent を止める。
                renameButton.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    _soundPlayer.PlaySE(_soundStore.EnterSE);
                    BeginRename(slot);
                });
                nameRow.Add(renameButton);

                info.Add(nameRow);

                // 下段：枚数（左）と、使用中なら「★使用中」バッジ（その右）を横並びにする。
                VisualElement countRow = new VisualElement();
                countRow.AddToClassList("deckbuilder-slot-count-row");

                int count = _deckRepository.LoadCount(slot);
                Label countLabel = new Label($"{count}/{DeckModel.MaxCards}");
                countLabel.AddToClassList("deckbuilder-slot-count");
                if (count == DeckModel.MaxCards)
                {
                    countLabel.AddToClassList("deckbuilder-slot-count--ready");
                }
                countRow.Add(countLabel);

                // 使用中（対戦に使う）デッキのスロットには枚数の右に小さくバッジを出す。
                if (slot == selected)
                {
                    Label useBadge = new Label("★使用中");
                    useBadge.AddToClassList("deckbuilder-slot-use-badge");
                    countRow.Add(useBadge);
                }

                info.Add(countRow);

                card.Add(info);

                _slotGrid.Add(card);
            }
        }

        // 上部ボタンのラベルを現在の使用デッキ名に合わせる。
        private void UpdateDeckSelectButtonLabel()
        {
            if (_deckSelectButton == null)
            {
                return;
            }
            _deckSelectButton.text = $"使用デッキ：{_deckRepository.LoadName(_deckRepository.SelectedIndex)} ▾";
        }

        private void OpenDeckSelectModal()
        {
            _soundPlayer.PlaySE(_soundStore.EnterSE);
            BuildDeckSelectList();
            _deckSelectOverlay.style.display = DisplayStyle.Flex;
        }

        private void CloseDeckSelectModal()
        {
            _deckSelectOverlay.style.display = DisplayStyle.None;
        }

        // 9 スロットの一覧（サムネ・名前・使用中バッジ・枚数・完成状態）を組む。行タップで使用デッキを切り替える。
        private void BuildDeckSelectList()
        {
            _deckSelectList.Clear();
            int selected = _deckRepository.SelectedIndex;
            for (int i = 0; i < DeckRepository.SlotCount; i++)
            {
                int slot = i;
                VisualElement row = new VisualElement();
                row.AddToClassList("deckbuilder-deckselect-row");
                if (slot == selected)
                {
                    row.AddToClassList("deckbuilder-deckselect-row--selected");
                }
                row.RegisterCallback<ClickEvent>(_ => OnDeckSelectRowClicked(slot));

                // 左にデッキのシンボル（代表カード全体）を表示する。シンボル未設定はカード裏面で代替する。
                VisualElement thumbnail = new VisualElement();
                thumbnail.AddToClassList("deckbuilder-deckselect-favorite");
                thumbnail.pickingMode = PickingMode.Ignore;
                string favoriteId = _deckRepository.LoadFavorite(slot);
                if (!string.IsNullOrEmpty(favoriteId)
                    && _cardDatabase.TryGet(favoriteId, out CardData favorite) && favorite.Image != null)
                {
                    thumbnail.style.backgroundImage = new StyleBackground(favorite.Image);
                }
                else if (_cardStore != null && _cardStore.CardBack != null)
                {
                    thumbnail.style.backgroundImage = new StyleBackground(_cardStore.CardBack);
                }
                row.Add(thumbnail);

                Label nameLabel = new Label(_deckRepository.LoadName(slot));
                nameLabel.AddToClassList("deckbuilder-deckselect-name");
                nameLabel.pickingMode = PickingMode.Ignore;
                row.Add(nameLabel);

                Label badge = new Label("使用中");
                badge.AddToClassList("deckbuilder-deckselect-badge");
                badge.style.display = slot == selected ? DisplayStyle.Flex : DisplayStyle.None;
                badge.pickingMode = PickingMode.Ignore;
                row.Add(badge);

                int count = _deckRepository.LoadCount(slot);
                Label countLabel = new Label($"{count}/{DeckModel.MaxCards}");
                countLabel.AddToClassList("deckbuilder-deckselect-count");
                if (count == DeckModel.MaxCards)
                {
                    countLabel.AddToClassList("deckbuilder-deckselect-count--ready");
                }
                countLabel.pickingMode = PickingMode.Ignore;
                row.Add(countLabel);

                _deckSelectList.Add(row);
            }
        }

        // 使用デッキ（SelectedIndex）を切り替える。編集中デッキ（_deckModel）は触らずグリッド表示だけ更新する。
        private void OnDeckSelectRowClicked(int slot)
        {
            _soundPlayer.PlaySE(_soundStore.EnterSE);
            _deckRepository.SelectedIndex = slot;
            UpdateDeckSelectButtonLabel();
            CloseDeckSelectModal();
            RebuildSlotGrid();
        }

        // 枠タップ＝そのデッキの「編集」。対戦に使うデッキ（SelectedIndex）は変えない。
        private void OpenSlot(int slot)
        {
            _soundPlayer.PlaySE(_soundStore.EnterSE);
            _editingSlot = slot;
            // スロット一覧を閉じて編集画面（読み込み中はその「読み込み中...」表示）を見せる。
            _slotOverlay.style.display = DisplayStyle.None;

            if (_cardsLoaded)
            {
                _deckRepository.Load(_deckModel, slot);
                RefreshDeckPanel();
            }
            else
            {
                // 読み込み中に開いた場合は、読み込み後に基底の InitializeDeck（_editingSlot を展開）と
                // RefreshFilter が編集画面を描画する。
                _pendingOpen = true;
            }
        }

        private void BeginRename(int slot)
        {
            VisualElement overlay = new VisualElement();
            overlay.AddToClassList("deckbuilder-confirm-overlay");
            overlay.RegisterCallback<ClickEvent>(_ => overlay.RemoveFromHierarchy());

            VisualElement panel = new VisualElement();
            panel.AddToClassList("deckbuilder-confirm-panel");
            panel.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            Label message = new Label("デッキ名を入力");
            message.AddToClassList("deckbuilder-confirm-message");

            TextField field = new TextField();
            field.maxLength = 16;
            field.value = _deckRepository.LoadName(slot);
            field.AddToClassList("deckbuilder-rename-field");

            VisualElement buttons = new VisualElement();
            buttons.AddToClassList("deckbuilder-confirm-buttons");

            Button cancelButton = new Button();
            cancelButton.text = "キャンセル";
            cancelButton.AddToClassList("deckbuilder-confirm-button--no");
            cancelButton.clicked += () =>
            {
                _soundPlayer.PlaySE(_soundStore.Cancel1SE);
                overlay.RemoveFromHierarchy();
            };

            Button okButton = new Button();
            okButton.text = "決定";
            okButton.AddToClassList("deckbuilder-confirm-button--yes");
            okButton.clicked += () =>
            {
                _soundPlayer.PlaySE(_soundStore.EnterSE);
                string input = field.value == null ? string.Empty : field.value.Trim();
                string name = string.IsNullOrEmpty(input) ? DeckRepository.DefaultName(slot) : input;
                _deckRepository.SaveName(slot, name);
                overlay.RemoveFromHierarchy();
                RebuildSlotGrid();
                // 使用中スロットの名前を変えた場合は上部ボタンのラベルも更新する。
                UpdateDeckSelectButtonLabel();
            };

            buttons.Add(cancelButton);
            buttons.Add(okButton);
            panel.Add(message);
            panel.Add(field);
            panel.Add(buttons);
            overlay.Add(panel);
            _slotOverlay.Add(overlay);

            field.Focus();
        }
    }
}
