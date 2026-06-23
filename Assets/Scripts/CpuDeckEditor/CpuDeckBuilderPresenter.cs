using System.Collections.Generic;
using Common.Cpu;
using Common.Deck;
using Common.SceneManagement;
using Cysharp.Threading.Tasks;
using DeckBuilder;
using Main.Card;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace CpuDeckBuilder
{
    // CPU の対戦相手ロスター（CpuRosterSO）を編集する Editor 専用ビルダー。
    // 画面上部の「相手1〜8」スロットタブで相手を切り替え、選んだスロットのデッキをその場で編集する。
    // カードの追加・削除・並び替えのたびに、そのスロットの CardIds へ自動保存する。
    public sealed class CpuDeckBuilderPresenter : DeckBuilderPresenterBase
    {
        private const string CpuRosterAssetPath = "Assets/Data/CpuRoster.asset";

        private CpuRosterSO _roster;
        private int _selectedSlot;
        private VisualElement _slotTabsContainer;
        private readonly List<Button> _slotTabs = new List<Button>();

        [Inject]
        public void Construct(
            CardStore cardStore,
            CardDatabase cardDatabase,
            SceneTransitioner sceneTransitioner)
        {
            _cardStore = cardStore;
            _cardDatabase = cardDatabase;
            _deckModel = new DeckModel();
            // CPU デッキは Home のトグルに関係なく常に同名3枚制限・デッキ枚数制限を適用する。
            // 共有の DeckRuleModel ではなく独自インスタンスを使うが、Editor 用コンストラクタは
            // EditorPrefs（Home のトグル状態）を読むため、生成直後に明示的に常時 ON へ上書きする。
            _deckRuleModel = new DeckRuleModel
            {
                LimitSameCards = true,
                LimitDeckCount = true,
            };
            _sceneTransitioner = sceneTransitioner;
        }

        protected override void InitializeDeck()
        {
#if UNITY_EDITOR
            _roster = LoadOrCreateRoster();
            _selectedSlot = 0;
            LoadSlotIntoDeck(_selectedSlot);
#endif
        }

        protected override void SaveDeck()
        {
#if UNITY_EDITOR
            if (_roster == null || _selectedSlot < 0 || _selectedSlot >= _roster.Opponents.Count)
            {
                return;
            }
            CpuOpponentData opponent = _roster.Opponents[_selectedSlot];
            opponent.CardIds.Clear();
            opponent.CardIds.AddRange(_deckModel.CardIds);
            UnityEditor.EditorUtility.SetDirty(_roster);
            UnityEditor.AssetDatabase.SaveAssets();
            UpdateSlotTabLabels();
#endif
        }

        protected override void NavigateBack()
        {
            _sceneTransitioner.Transit(Scenes.Title).Forget();
        }

        protected override void OnDeckBuilderReady(VisualElement root)
        {
#if UNITY_EDITOR
            _slotTabsContainer = root.Q<VisualElement>("OpponentSlotTabs");
            if (_slotTabsContainer == null || _roster == null)
            {
                return;
            }
            BuildSlotTabs();
            UpdateSlotTabHighlight();
#endif
        }

#if UNITY_EDITOR
        // ロスターアセットを読み込み（無ければ作成）、最低スロット数を確保して返す。
        private CpuRosterSO LoadOrCreateRoster()
        {
            CpuRosterSO roster = UnityEditor.AssetDatabase.LoadAssetAtPath<CpuRosterSO>(CpuRosterAssetPath);
            if (roster == null)
            {
                roster = ScriptableObject.CreateInstance<CpuRosterSO>();
                UnityEditor.AssetDatabase.CreateAsset(roster, CpuRosterAssetPath);
            }
            // 空スロットも編集できるよう、Home のプレースホルダー数ぶんのスロットを確保する。
            bool changed = false;
            while (roster.Opponents.Count < CpuRosterStore.PlaceholderCount)
            {
                roster.Opponents.Add(new CpuOpponentData());
                changed = true;
            }
            if (changed)
            {
                UnityEditor.EditorUtility.SetDirty(roster);
                UnityEditor.AssetDatabase.SaveAssets();
            }
            return roster;
        }

        // 指定スロットのカードIDを DeckModel に展開する（パネルの再描画は呼び出し側で行う）。
        private void LoadSlotIntoDeck(int slot)
        {
            _deckModel.Clear();
            if (_roster == null || slot < 0 || slot >= _roster.Opponents.Count)
            {
                return;
            }
            foreach (string id in _roster.Opponents[slot].CardIds)
            {
                if (_cardDatabase.TryGet(id, out CardData card))
                {
                    _deckModel.Add(id, card.Cost);
                }
            }
        }

        private void BuildSlotTabs()
        {
            _slotTabsContainer.Clear();
            _slotTabs.Clear();
            for (int i = 0; i < _roster.Opponents.Count; i++)
            {
                int slot = i;
                Button tab = new Button();
                tab.AddToClassList("cpudeck-slot-tab");
                tab.text = SlotLabel(slot);
                tab.clicked += () => OnSlotTabClicked(slot);
                _slotTabsContainer.Add(tab);
                _slotTabs.Add(tab);
            }
        }

        private void OnSlotTabClicked(int slot)
        {
            if (slot == _selectedSlot)
            {
                return;
            }
            _selectedSlot = slot;
            LoadSlotIntoDeck(slot);
            RefreshDeckPanel();
            UpdateSlotTabHighlight();
        }

        private void UpdateSlotTabHighlight()
        {
            for (int i = 0; i < _slotTabs.Count; i++)
            {
                bool selected = i == _selectedSlot;
                _slotTabs[i].EnableInClassList("cpudeck-slot-tab--selected", selected);
                bool filled = i < _roster.Opponents.Count && _roster.Opponents[i].CardIds.Count > 0;
                _slotTabs[i].EnableInClassList("cpudeck-slot-tab--filled", filled);
            }
        }

        private void UpdateSlotTabLabels()
        {
            for (int i = 0; i < _slotTabs.Count; i++)
            {
                _slotTabs[i].text = SlotLabel(i);
            }
            UpdateSlotTabHighlight();
        }

        // タブの表示名。相手名が設定済みならその名前、未設定なら「相手n」。デッキ枚数を併記する。
        private string SlotLabel(int slot)
        {
            CpuOpponentData opponent = _roster.Opponents[slot];
            string name = string.IsNullOrEmpty(opponent.Name) ? $"相手{slot + 1}" : opponent.Name;
            int count = opponent.CardIds.Count;
            return count > 0 ? $"{name} ({count})" : name;
        }
#endif
    }
}
