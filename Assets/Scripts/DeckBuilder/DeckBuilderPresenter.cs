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
    // 編集内容はそのスロットへ自動保存する。対戦に使うデッキ（SelectedIndex）の選択は Home 側で行う。
    public sealed class DeckBuilderPresenter : DeckBuilderPresenterBase
    {
        private DeckRepository _deckRepository;

        private VisualElement _slotOverlay;
        private VisualElement _slotGrid;
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

            _slotOverlay.Add(header);

            _slotGrid = new VisualElement();
            _slotGrid.AddToClassList("deckbuilder-slot-grid");
            _slotOverlay.Add(_slotGrid);

            root.Add(_slotOverlay);
        }

        private void ShowSlotList()
        {
            RebuildSlotGrid();
            _slotOverlay.style.display = DisplayStyle.Flex;
        }

        private void RebuildSlotGrid()
        {
            _slotGrid.Clear();

            for (int i = 0; i < DeckRepository.SlotCount; i++)
            {
                int slot = i;
                VisualElement card = new VisualElement();
                card.AddToClassList("deckbuilder-slot-card");
                card.RegisterCallback<ClickEvent>(_ => OpenSlot(slot));

                // お気に入りカードが設定されていれば、左に小さくカード全体を表示する。
                string favoriteId = _deckRepository.LoadFavorite(slot);
                if (!string.IsNullOrEmpty(favoriteId)
                    && _cardDatabase.TryGet(favoriteId, out CardData favorite) && favorite.Image != null)
                {
                    VisualElement thumbnail = new VisualElement();
                    thumbnail.AddToClassList("deckbuilder-slot-favorite");
                    thumbnail.style.backgroundImage = new StyleBackground(favorite.Image);
                    thumbnail.pickingMode = PickingMode.Ignore;
                    card.Add(thumbnail);
                }

                VisualElement info = new VisualElement();
                info.AddToClassList("deckbuilder-slot-info");

                Label nameLabel = new Label(_deckRepository.LoadName(slot));
                nameLabel.AddToClassList("deckbuilder-slot-name");
                info.Add(nameLabel);

                Button renameButton = new Button();
                renameButton.text = "✎ 名前を変更";
                renameButton.AddToClassList("deckbuilder-slot-rename");
                // カードのクリック（編集を開く）に伝播させないよう ClickEvent を止める。
                renameButton.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    _soundPlayer.PlaySE(_soundStore.EnterSE);
                    BeginRename(slot);
                });
                info.Add(renameButton);

                int count = _deckRepository.LoadCount(slot);
                Label countLabel = new Label($"{count}/{DeckModel.MaxCards}");
                countLabel.AddToClassList("deckbuilder-slot-count");
                if (count == DeckModel.MaxCards)
                {
                    countLabel.AddToClassList("deckbuilder-slot-count--ready");
                }
                info.Add(countLabel);

                card.Add(info);

                _slotGrid.Add(card);
            }
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
