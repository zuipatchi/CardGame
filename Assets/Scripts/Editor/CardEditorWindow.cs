using System.Collections.Generic;
using Main.Card;
using Main.Card.Effects;
using UnityEditor;
using UnityEngine;
using EventType = Main.Card.EventType;

namespace GameEditor
{
    // 属性別に分割された CharacterCardSO / EventCardSO を横断して、カードの全項目を
    // 一画面で検索・編集・追加・削除できるエディタ専用ウィンドウ。
    // 編集は SerializedObject/SerializedProperty 経由で行うため、Undo に対応し、
    // ID の自動採番（各 SO の OnValidate）も ApplyModifiedProperties で発火する。
    public sealed class CardEditorWindow : EditorWindow
    {
        // 一覧の1行＝1カード。SO とリスト内インデックスで実体を指す（採番でズレるため構造変更後は再構築する）。
        private sealed class CardEntry
        {
            public ScriptableObject So { get; set; }
            public bool IsCharacter { get; set; }
            public int Index { get; set; }
            public string Id { get; set; }
            public string Name { get; set; }
            public CardAttribute Attribute { get; set; }
            public int Cost { get; set; }
            public bool InUse { get; set; }
            // 対戦専用トークン（デッキ構築プールから除外）か。
            public bool IsToken { get; set; }
            // キャラの攻撃力／HP（イベントは 0）。SummonChar の効果テキストで召喚先の数値表示に使う。
            public bool IsCharacterCard { get; set; }
            public int Attack { get; set; }
            public int Hp { get; set; }
        }

        // 一覧の並び順。
        private enum SortMode
        {
            Id,
            Cost,
        }

        // EventType は明示的な整数値（11 欠番）を持つため、enumValueIndex（宣言順）を実際の値へ変換するのに使う。
        private static readonly EventType[] EventTypeValues = (EventType[])System.Enum.GetValues(typeof(EventType));

        private static readonly CardAttribute[] AllAttributes =
        {
            CardAttribute.Red,
            CardAttribute.Blue,
            CardAttribute.Green,
            CardAttribute.Yellow,
            CardAttribute.Black,
            CardAttribute.Purple,
            CardAttribute.White,
        };

        private readonly List<CardEntry> _entries = new List<CardEntry>();
        private CardEntry _selected;
        private SerializedObject _selectedSo;

        private string _search = string.Empty;
        private readonly HashSet<CardAttribute> _attributeFilter = new HashSet<CardAttribute>();
        private bool _showCharacter = true;
        private bool _showEvent = true;
        private SortMode _sortMode = SortMode.Id;

        private Vector2 _listScroll;
        private Vector2 _formScroll;

        private CardAttribute _newAttribute = CardAttribute.Red;
        private bool _newIsCharacter = true;

        // 特徴（キーワード）ドロップダウンの選択肢キャッシュ。[0] は「特徴なし（空文字）」。
        private const string KeywordNoneLabel = "（特徴なし）";
        private string[] _keywordOptions = { KeywordNoneLabel };

        [MenuItem("Card/カードエディタ")]
        public static void Open()
        {
            CardEditorWindow window = GetWindow<CardEditorWindow>("カードエディタ");
            window.minSize = new Vector2(760, 480);
            window.Show();
        }

        private void OnEnable()
        {
            RebuildEntries();
            ReloadKeywordOptions();
        }

        // 特徴マスター SO（CardKeywordSO）から特徴ドロップダウンの選択肢を読み込む。SO が無ければ「特徴なし」のみ。
        private void ReloadKeywordOptions()
        {
            List<string> options = new List<string> { KeywordNoneLabel };
            CardKeywordSO so = FindKeywordSo();
            if (so != null && so.Keywords != null)
            {
                foreach (string keyword in so.Keywords)
                {
                    if (!string.IsNullOrEmpty(keyword) && !options.Contains(keyword))
                    {
                        options.Add(keyword);
                    }
                }
            }
            _keywordOptions = options.ToArray();
        }

        private static CardKeywordSO FindKeywordSo()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:CardKeywordSO"))
            {
                CardKeywordSO so = AssetDatabase.LoadAssetAtPath<CardKeywordSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (so != null)
                {
                    return so;
                }
            }
            return null;
        }

        // 特徴マスター SO を選択（無ければ Assets/Data/CardKeywords.asset に新規作成）してインスペクタへ表示する。
        // 特徴の登録・編集はこの SO のインスペクタで行う。
        private void OpenOrCreateKeywordSo()
        {
            CardKeywordSO so = FindKeywordSo();
            if (so == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Data"))
                {
                    AssetDatabase.CreateFolder("Assets", "Data");
                }
                so = ScriptableObject.CreateInstance<CardKeywordSO>();
                AssetDatabase.CreateAsset(so, "Assets/Data/CardKeywords.asset");
                AssetDatabase.SaveAssets();
            }
            ReloadKeywordOptions();
            Selection.activeObject = so;
            EditorGUIUtility.PingObject(so);
        }

        // 全 SO を走査して一覧を作り直す。選択は SO＋インデックスで復元を試みる。
        private void RebuildEntries()
        {
            _entries.Clear();

            foreach (string guid in AssetDatabase.FindAssets("t:CharacterCardSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                CharacterCardSO so = AssetDatabase.LoadAssetAtPath<CharacterCardSO>(path);
                AddEntries(so, so != null ? so.Cards : null, true);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:EventCardSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                EventCardSO so = AssetDatabase.LoadAssetAtPath<EventCardSO>(path);
                AddEntries(so, so != null ? so.Cards : null, false);
            }

            SortEntries();

            RestoreSelection();
        }

        private void AddEntries<T>(ScriptableObject so, IReadOnlyList<T> cards, bool isCharacter) where T : CardData
        {
            if (so == null || cards == null)
            {
                return;
            }

            for (int i = 0; i < cards.Count; i++)
            {
                T card = cards[i];
                if (card == null)
                {
                    continue;
                }

                CardEntry entry = new CardEntry
                {
                    So = so,
                    IsCharacter = isCharacter,
                    Index = i,
                    Id = card.Id,
                    Name = card.CardName,
                    Attribute = card.Attribute,
                    Cost = card.Cost,
                    InUse = card.InUse,
                    IsToken = card.InUse && !card.InDeckPool,
                    IsCharacterCard = isCharacter,
                    Attack = card.Attack,
                    Hp = card.Hp,
                };
                _entries.Add(entry);
            }
        }

        // 全 SO のカード ID を現在の採番ルール（CardIdAutoAssigner）で振り直してディスク保存する。
        // 採番ルールを変えた後に一括反映するために使う。
        private void ReassignAllIds()
        {
            if (!EditorUtility.DisplayDialog("ID再採番",
                "全カードの ID を現在のルールで振り直します。\n"
                + "SummonChar の値や保存済みデッキは旧 ID を参照したままになる点に注意してください。\n続行しますか？",
                "再採番する", "キャンセル"))
            {
                return;
            }

            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:CharacterCardSO"))
            {
                CharacterCardSO so = AssetDatabase.LoadAssetAtPath<CharacterCardSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (so != null && CardIdAutoAssigner.AssignIds(so.Cards, "C"))
                {
                    EditorUtility.SetDirty(so);
                    changed++;
                }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:EventCardSO"))
            {
                EventCardSO so = AssetDatabase.LoadAssetAtPath<EventCardSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (so != null && CardIdAutoAssigner.AssignIds(so.Cards, "E"))
                {
                    EditorUtility.SetDirty(so);
                    changed++;
                }
            }

            AssetDatabase.SaveAssets();
            RebuildEntries();
            Debug.Log($"カード ID を再採番しました（更新した SO: {changed} 種）。");
        }

        // 現在のソートモードで一覧を並べ替える。
        private void SortEntries()
        {
            switch (_sortMode)
            {
                case SortMode.Cost:
                    _entries.Sort(CompareByCost);
                    break;
                default:
                    // ID 昇順で並べる（C{4桁}/E{4桁} の固定桁なので序数比較で数値順になり、C → E の順になる）。
                    _entries.Sort(CompareById);
                    break;
            }
        }

        // ID 昇順の比較。空 ID は末尾に回す。
        private static int CompareById(CardEntry a, CardEntry b)
        {
            bool aEmpty = string.IsNullOrEmpty(a.Id);
            bool bEmpty = string.IsNullOrEmpty(b.Id);
            if (aEmpty || bEmpty)
            {
                return aEmpty == bEmpty ? 0 : (aEmpty ? 1 : -1);
            }
            return string.CompareOrdinal(a.Id, b.Id);
        }

        // コスト昇順の比較。同コストは ID 昇順で安定させる。
        private static int CompareByCost(CardEntry a, CardEntry b)
        {
            int byCost = a.Cost.CompareTo(b.Cost);
            return byCost != 0 ? byCost : CompareById(a, b);
        }

        private void RestoreSelection()
        {
            if (_selected == null)
            {
                return;
            }

            CardEntry match = null;
            foreach (CardEntry entry in _entries)
            {
                if (entry.So == _selected.So && entry.Index == _selected.Index && entry.IsCharacter == _selected.IsCharacter)
                {
                    match = entry;
                    break;
                }
            }

            Select(match);
        }

        private void Select(CardEntry entry)
        {
            _selected = entry;
            _selectedSo = entry != null && entry.So != null ? new SerializedObject(entry.So) : null;
        }

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawList();
            DrawForm();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUIUtility.labelWidth = 36f;
            _search = EditorGUILayout.TextField("検索", _search, EditorStyles.toolbarSearchField, GUILayout.Width(220f));
            EditorGUIUtility.labelWidth = 0f;

            _showCharacter = GUILayout.Toggle(_showCharacter, "キャラ", EditorStyles.toolbarButton, GUILayout.Width(54f));
            _showEvent = GUILayout.Toggle(_showEvent, "イベント", EditorStyles.toolbarButton, GUILayout.Width(64f));

            GUILayout.Space(8f);
            EditorGUIUtility.labelWidth = 36f;
            SortMode nextSort = (SortMode)EditorGUILayout.Popup("並び", (int)_sortMode,
                new[] { "ID順", "コスト順" }, EditorStyles.toolbarPopup, GUILayout.Width(108f));
            EditorGUIUtility.labelWidth = 0f;
            if (nextSort != _sortMode)
            {
                _sortMode = nextSort;
                SortEntries();
            }

            GUILayout.Space(8f);
            foreach (CardAttribute attribute in AllAttributes)
            {
                bool on = _attributeFilter.Contains(attribute);
                Color previous = GUI.backgroundColor;
                if (on)
                {
                    GUI.backgroundColor = AttributeColor(attribute);
                }
                bool next = GUILayout.Toggle(on, CardAttributeNames.Short(attribute), EditorStyles.toolbarButton, GUILayout.Width(28f));
                GUI.backgroundColor = previous;
                if (next != on)
                {
                    if (next)
                    {
                        _attributeFilter.Add(attribute);
                    }
                    else
                    {
                        _attributeFilter.Remove(attribute);
                    }
                }
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("ID再採番", EditorStyles.toolbarButton, GUILayout.Width(72f)))
            {
                ReassignAllIds();
            }
            if (GUILayout.Button("再読込", EditorStyles.toolbarButton, GUILayout.Width(56f)))
            {
                RebuildEntries();
                ReloadKeywordOptions();
            }
            if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(48f)))
            {
                AssetDatabase.SaveAssets();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(280f));
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, "box");

            int shown = 0;
            foreach (CardEntry entry in _entries)
            {
                if (!PassesFilter(entry))
                {
                    continue;
                }
                shown++;

                bool isSelected = _selected != null && _selected.So == entry.So
                    && _selected.Index == entry.Index && _selected.IsCharacter == entry.IsCharacter;

                EditorGUILayout.BeginHorizontal();

                Color previous = GUI.color;
                GUI.color = AttributeColor(entry.Attribute);
                GUILayout.Label("■", GUILayout.Width(16f));
                GUI.color = previous;

                string prefix = _sortMode == SortMode.Cost ? $"[{entry.Cost}]  {entry.Id}" : entry.Id;
                string suffix = !entry.InUse ? "  (未使用)" : entry.IsToken ? "  (トークン)" : string.Empty;
                string label = $"{prefix}  {entry.Name}{suffix}";
                GUIStyle style = isSelected ? EditorStyles.boldLabel : EditorStyles.label;
                Color previousContent = GUI.color;
                if (!entry.InUse)
                {
                    // 未使用カードはグレーアウトして判別する。
                    GUI.color = new Color(previousContent.r, previousContent.g, previousContent.b, 0.5f);
                }
                if (GUILayout.Button(label, style))
                {
                    Select(entry);
                    GUI.FocusControl(null);
                }
                GUI.color = previousContent;

                EditorGUILayout.EndHorizontal();
            }

            if (shown == 0)
            {
                EditorGUILayout.LabelField("該当するカードがありません");
            }

            EditorGUILayout.EndScrollView();

            DrawAddPanel();

            EditorGUILayout.EndVertical();
        }

        private bool PassesFilter(CardEntry entry)
        {
            if (entry.IsCharacter && !_showCharacter)
            {
                return false;
            }
            if (!entry.IsCharacter && !_showEvent)
            {
                return false;
            }
            if (_attributeFilter.Count > 0 && !_attributeFilter.Contains(entry.Attribute))
            {
                return false;
            }
            if (!string.IsNullOrEmpty(_search))
            {
                string query = _search.Trim();
                bool nameMatch = entry.Name != null && entry.Name.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0;
                bool idMatch = entry.Id != null && entry.Id.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!nameMatch && !idMatch)
                {
                    return false;
                }
            }
            return true;
        }

        private void DrawAddPanel()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("新規追加", EditorStyles.boldLabel);

            _newIsCharacter = EditorGUILayout.Popup("種別", _newIsCharacter ? 0 : 1, new[] { "キャラ", "イベント" }) == 0;
            _newAttribute = (CardAttribute)EditorGUILayout.EnumPopup("属性", _newAttribute);

            if (GUILayout.Button("追加"))
            {
                AddCard(_newAttribute, _newIsCharacter);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawForm()
        {
            EditorGUILayout.BeginVertical("box");
            _formScroll = EditorGUILayout.BeginScrollView(_formScroll);

            if (_selected == null || _selectedSo == null)
            {
                EditorGUILayout.HelpBox("左の一覧からカードを選択してください。", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            _selectedSo.Update();

            SerializedProperty cards = _selectedSo.FindProperty("_cards");
            if (cards == null || _selected.Index < 0 || _selected.Index >= cards.arraySize)
            {
                EditorGUILayout.HelpBox("選択中のカードが見つかりません。再読込してください。", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            SerializedProperty element = cards.GetArrayElementAtIndex(_selected.Index);

            DrawHeaderRow(element);
            EditorGUILayout.Space();

            DrawCommonFields(element);
            EditorGUILayout.Space();

            if (_selected.IsCharacter)
            {
                DrawCharacterFields(element);
            }
            else
            {
                DrawEventFields(element);
            }

            EditorGUILayout.Space();
            DrawDeleteButton();

            if (_selectedSo.ApplyModifiedProperties())
            {
                // 採番や名前の変更を一覧へ即反映する。
                _selected.Id = element.FindPropertyRelative("_id").stringValue;
                _selected.Name = element.FindPropertyRelative("_cardName").stringValue;
                _selected.InUse = !element.FindPropertyRelative("_excludeFromGame").boolValue;
                _selected.IsToken = _selected.InUse && element.FindPropertyRelative("_excludeFromDeckBuilder").boolValue;
                EditorUtility.SetDirty(_selected.So);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawHeaderRow(SerializedProperty element)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("ID", element.FindPropertyRelative("_id").stringValue);
                EditorGUILayout.EnumPopup("属性", (CardAttribute)element.FindPropertyRelative("_attribute").enumValueIndex);
            }
            EditorGUILayout.LabelField("種別", _selected.IsCharacter ? "キャラ" : "イベント");

            // ゲームで使用する／しない（保存フィールド _excludeFromGame は反転値）。
            SerializedProperty exclude = element.FindPropertyRelative("_excludeFromGame");
            bool inUse = EditorGUILayout.Toggle("ゲームで使用", !exclude.boolValue);
            exclude.boolValue = !inUse;
            if (!inUse)
            {
                EditorGUILayout.HelpBox("このカードはゲーム（デッキ構築・対戦）から除外されます。", MessageType.Warning);
            }
            else
            {
                // 対戦専用トークン：対戦では効果から ID 召喚できるが、デッキ構築のプールには出さない。
                SerializedProperty excludeFromDeck = element.FindPropertyRelative("_excludeFromDeckBuilder");
                bool isToken = EditorGUILayout.Toggle("対戦専用（トークン）", excludeFromDeck.boolValue);
                excludeFromDeck.boolValue = isToken;
                if (isToken)
                {
                    EditorGUILayout.HelpBox("このカードはデッキ構築では使用できませんが、対戦では使用できます（トークン）。", MessageType.Info);
                }
            }
        }

        private void DrawCommonFields(SerializedProperty element)
        {
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_cardName"), new GUIContent("名前"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_cost"), new GUIContent("コスト"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_image"), new GUIContent("画像"));

            EditorGUILayout.PropertyField(element.FindPropertyRelative("_victoryPointBonus"), new GUIContent("勝利点付帯値"));

            SerializedProperty description = element.FindPropertyRelative("_description");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(description, new GUIContent("効果テキスト"));
            if (GUILayout.Button("自動生成", GUILayout.Width(72f)))
            {
                description.stringValue = BuildDescription(element);
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            // _flavorText は [TextArea] のため PropertyField だと複数行になる。ここでは1行で編集する。
            SerializedProperty flavor = element.FindPropertyRelative("_flavorText");
            flavor.stringValue = EditorGUILayout.TextField("フレーバー", flavor.stringValue);

            DrawKeywordField(element);
        }

        // 特徴（キーワード）をマスター SO の一覧からドロップダウンで選ぶ。「管理」ボタンで SO を作成／表示する。
        // 登録外の値（SO から削除された等）が入っている場合は選択肢の末尾に補って表示する。
        private void DrawKeywordField(SerializedProperty element)
        {
            SerializedProperty keyword = element.FindPropertyRelative("_keyword");
            string current = keyword.stringValue ?? string.Empty;

            List<string> options = new List<string>(_keywordOptions);
            if (!string.IsNullOrEmpty(current) && !options.Contains(current))
            {
                options.Add(current);
            }

            int selected = string.IsNullOrEmpty(current) ? 0 : options.IndexOf(current);
            if (selected < 0)
            {
                selected = 0;
            }

            EditorGUILayout.BeginHorizontal();
            int next = EditorGUILayout.Popup("特徴", selected, options.ToArray());
            if (next != selected)
            {
                keyword.stringValue = next == 0 ? string.Empty : options[next];
            }
            if (GUILayout.Button("管理", GUILayout.Width(48f)))
            {
                OpenOrCreateKeywordSo();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCharacterFields(SerializedProperty element)
        {
            EditorGUILayout.LabelField("ステータス", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_attack"), new GUIContent("Attack"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_hp"), new GUIContent("Hp"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("効果", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_effectTrigger"), new GUIContent("発動タイミング"));

            SerializedProperty effectType = element.FindPropertyRelative("_effectType");
            EditorGUILayout.PropertyField(effectType, new GUIContent("効果種別"));
            DrawValueFields(ToEventType(effectType),
                element.FindPropertyRelative("_effectValue"),
                element.FindPropertyRelative("_effectValue2"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("能力", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_guardian"), new GUIContent("守護"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_haste"), new GUIContent("速攻"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_flying"), new GUIContent("飛行"));
        }

        private void DrawEventFields(SerializedProperty element)
        {
            EditorGUILayout.LabelField("効果", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_eventTrigger"), new GUIContent("発動タイミング"));

            SerializedProperty eventType = element.FindPropertyRelative("_eventType");
            EditorGUILayout.PropertyField(eventType, new GUIContent("効果種別"));
            DrawValueFields(ToEventType(eventType),
                element.FindPropertyRelative("_eventValue"),
                element.FindPropertyRelative("_eventValue2"));

            EditorGUILayout.PropertyField(element.FindPropertyRelative("_triggerOnGrave"), new GUIContent("墓地で発動"));
        }

        // EventType に応じて値1/値2 のラベルを切り替え、意味のヒントを表示する。
        private void DrawValueFields(EventType type, SerializedProperty value1, SerializedProperty value2)
        {
            EffectHandler handler = EffectCatalog.Get(type);
            EffectValueInfo info = handler != null ? handler.Values : default;

            if (!string.IsNullOrEmpty(info.Help))
            {
                EditorGUILayout.HelpBox(info.Help, MessageType.None);
            }

            using (new EditorGUI.DisabledScope(!info.Value1Used))
            {
                EditorGUILayout.PropertyField(value1, new GUIContent(info.Value1Label));
            }
            using (new EditorGUI.DisabledScope(!info.Value2Used))
            {
                EditorGUILayout.PropertyField(value2, new GUIContent(info.Value2Label));
            }
        }

        private void DrawDeleteButton()
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.9f, 0.5f, 0.5f);
            if (GUILayout.Button("このカードを削除"))
            {
                if (EditorUtility.DisplayDialog("カード削除",
                    $"「{_selected.Id} {_selected.Name}」を削除します。よろしいですか？", "削除", "キャンセル"))
                {
                    DeleteSelected();
                }
            }
            GUI.backgroundColor = previous;
        }

        private void AddCard(CardAttribute attribute, bool isCharacter)
        {
            ScriptableObject so = FindSo(attribute, isCharacter);
            if (so == null)
            {
                EditorUtility.DisplayDialog("追加できません",
                    $"{attribute} の{(isCharacter ? "キャラ" : "イベント")} SO が見つかりません。"
                    + "先に該当属性の SO を作成し、CardDatabase に登録してください。", "OK");
                return;
            }

            SerializedObject sobj = new SerializedObject(so);
            SerializedProperty cards = sobj.FindProperty("_cards");
            int newIndex = cards.arraySize;
            cards.InsertArrayElementAtIndex(newIndex);
            ResetNewElement(cards.GetArrayElementAtIndex(newIndex), isCharacter);
            sobj.ApplyModifiedProperties();
            EditorUtility.SetDirty(so);

            RebuildEntries();
            SelectByLocation(so, newIndex, isCharacter);
        }

        // InsertArrayElementAtIndex は直前要素のコピーになるため、各フィールドを初期値へ戻す。
        private void ResetNewElement(SerializedProperty element, bool isCharacter)
        {
            element.FindPropertyRelative("_cardName").stringValue = string.Empty;
            element.FindPropertyRelative("_cost").intValue = 0;
            element.FindPropertyRelative("_image").objectReferenceValue = null;
            element.FindPropertyRelative("_flavorText").stringValue = string.Empty;
            element.FindPropertyRelative("_victoryPointBonus").intValue = 0;
            element.FindPropertyRelative("_excludeFromGame").boolValue = false;
            element.FindPropertyRelative("_excludeFromDeckBuilder").boolValue = false;
            element.FindPropertyRelative("_description").stringValue = string.Empty;
            element.FindPropertyRelative("_keyword").stringValue = string.Empty;

            if (isCharacter)
            {
                element.FindPropertyRelative("_attack").intValue = 0;
                element.FindPropertyRelative("_hp").intValue = 0;
                element.FindPropertyRelative("_effectTrigger").enumValueIndex = (int)CharacterEffectTrigger.None;
                element.FindPropertyRelative("_effectType").enumValueIndex = (int)EventType.None;
                element.FindPropertyRelative("_effectValue").intValue = 0;
                element.FindPropertyRelative("_effectValue2").intValue = 0;
                element.FindPropertyRelative("_guardian").boolValue = false;
                element.FindPropertyRelative("_haste").boolValue = false;
                element.FindPropertyRelative("_flying").boolValue = false;
            }
            else
            {
                element.FindPropertyRelative("_eventTrigger").enumValueIndex = (int)EventCardTrigger.OnPlay;
                element.FindPropertyRelative("_eventType").enumValueIndex = (int)EventType.None;
                element.FindPropertyRelative("_eventValue").intValue = 0;
                element.FindPropertyRelative("_eventValue2").intValue = 0;
                element.FindPropertyRelative("_triggerOnGrave").boolValue = false;
            }
        }

        private void DeleteSelected()
        {
            ScriptableObject so = _selected.So;
            SerializedObject sobj = new SerializedObject(so);
            SerializedProperty cards = sobj.FindProperty("_cards");
            if (_selected.Index >= 0 && _selected.Index < cards.arraySize)
            {
                cards.DeleteArrayElementAtIndex(_selected.Index);
                sobj.ApplyModifiedProperties();
                EditorUtility.SetDirty(so);
            }

            _selected = null;
            _selectedSo = null;
            RebuildEntries();
        }

        private void SelectByLocation(ScriptableObject so, int index, bool isCharacter)
        {
            foreach (CardEntry entry in _entries)
            {
                if (entry.So == so && entry.Index == index && entry.IsCharacter == isCharacter)
                {
                    Select(entry);
                    return;
                }
            }
        }

        private ScriptableObject FindSo(CardAttribute attribute, bool isCharacter)
        {
            string filter = isCharacter ? "t:CharacterCardSO" : "t:EventCardSO";
            foreach (string guid in AssetDatabase.FindAssets(filter))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so == null)
                {
                    continue;
                }
                SerializedObject sobj = new SerializedObject(so);
                SerializedProperty attributeProp = sobj.FindProperty("_attribute");
                if (attributeProp != null && (CardAttribute)attributeProp.enumValueIndex == attribute)
                {
                    return so;
                }
            }
            return null;
        }

        // enumValueIndex（宣言順インデックス）を実際の EventType 値へ変換する（11 欠番への対応）。
        private static EventType ToEventType(SerializedProperty enumProperty)
        {
            int index = enumProperty.enumValueIndex;
            if (index < 0 || index >= EventTypeValues.Length)
            {
                return EventType.None;
            }
            return EventTypeValues[index];
        }

        // 現在の選択内容（発動タイミング・効果種別・値・属性・勝利点）から効果テキストを自動生成する。
        // 守護/速攻/飛行フラグは（アイコン表示があるため）テキストに含めない。
        private string BuildDescription(SerializedProperty element)
        {
            bool isCharacter = _selected.IsCharacter;
            CardAttribute attribute = (CardAttribute)element.FindPropertyRelative("_attribute").enumValueIndex;

            string prefix = isCharacter
                ? CharacterTriggerPrefix((CharacterEffectTrigger)element.FindPropertyRelative("_effectTrigger").enumValueIndex)
                : EventTriggerPrefix((EventCardTrigger)element.FindPropertyRelative("_eventTrigger").enumValueIndex);

            EventType type = ToEventType(element.FindPropertyRelative(isCharacter ? "_effectType" : "_eventType"));
            int value1 = element.FindPropertyRelative(isCharacter ? "_effectValue" : "_eventValue").intValue;
            int value2 = element.FindPropertyRelative(isCharacter ? "_effectValue2" : "_eventValue2").intValue;

            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(prefix))
            {
                parts.Add(prefix);
            }

            string keyword = element.FindPropertyRelative("_keyword").stringValue;
            EffectHandler handler = EffectCatalog.Get(type);
            string body = handler != null
                ? handler.BuildBody(new EffectTextContext(value1, value2, attribute, keyword, ResolveCardName, ResolveCardStats))
                : string.Empty;
            if (!string.IsNullOrEmpty(body))
            {
                parts.Add(body);
            }

            int victoryPoint = element.FindPropertyRelative("_victoryPointBonus").intValue;
            if (victoryPoint > 0)
            {
                parts.Add($"勝利点を{victoryPoint}得る");
            }

            // 効果も勝利点も無いときは接頭辞だけ残さず空にする。
            if (body.Length == 0 && victoryPoint <= 0)
            {
                return string.Empty;
            }

            return string.Join("、", parts);
        }

        private static string CharacterTriggerPrefix(CharacterEffectTrigger trigger)
        {
            switch (trigger)
            {
                case CharacterEffectTrigger.OnEnter:
                    return "場に出た時";
                case CharacterEffectTrigger.OnAttack:
                    return "攻撃した時";
                case CharacterEffectTrigger.OnDestroy:
                    return "破壊された時";
                case CharacterEffectTrigger.OnUsedAsCost:
                    return "コストとして使用した時";
                case CharacterEffectTrigger.OnTurnStart:
                    return "自分のターン開始時";
                case CharacterEffectTrigger.OnAttacked:
                    return "攻撃された時";
                case CharacterEffectTrigger.OnKill:
                    return "相手キャラを撃破した時";
                default:
                    return string.Empty;
            }
        }

        private static string EventTriggerPrefix(EventCardTrigger trigger)
        {
            switch (trigger)
            {
                case EventCardTrigger.OnTurnStart:
                    return "自分のターン開始時に毎ターン";
                default:
                    return string.Empty;
            }
        }

        // SummonChar 用：ID から一覧上のカード名を引く。見つからなければ ID をそのまま返す。
        private string ResolveCardName(string id)
        {
            foreach (CardEntry entry in _entries)
            {
                if (entry.Id == id)
                {
                    return entry.Name;
                }
            }
            return id;
        }

        // SummonChar 用：ID から一覧上のキャラの「ATK/HP」表記を引く。
        // 見つからない／キャラでないときは空文字（呼び出し側は名前のみ表示にフォールバック）。
        private string ResolveCardStats(string id)
        {
            foreach (CardEntry entry in _entries)
            {
                if (entry.Id == id)
                {
                    return entry.IsCharacterCard ? $"{entry.Attack}/{entry.Hp}" : string.Empty;
                }
            }
            return string.Empty;
        }

        private static Color AttributeColor(CardAttribute attribute)
        {
            switch (attribute)
            {
                case CardAttribute.Red:
                    return new Color(0.90f, 0.35f, 0.35f);
                case CardAttribute.Blue:
                    return new Color(0.40f, 0.60f, 0.95f);
                case CardAttribute.Green:
                    return new Color(0.40f, 0.80f, 0.45f);
                case CardAttribute.Yellow:
                    return new Color(0.95f, 0.85f, 0.35f);
                case CardAttribute.Black:
                    return new Color(0.55f, 0.55f, 0.60f);
                case CardAttribute.Purple:
                    return new Color(0.70f, 0.50f, 0.90f);
                case CardAttribute.White:
                    return new Color(0.95f, 0.95f, 0.95f);
                default:
                    return Color.white;
            }
        }
    }
}
