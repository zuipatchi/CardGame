using System.Collections.Generic;
using Main.Card;
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
            public bool InUse { get; set; }
        }

        // EventType ごとの値の意味（フィールドラベル・ヒント表示に使う）。
        private readonly struct ValueInfo
        {
            public bool Value1Used { get; }
            public bool Value2Used { get; }
            public string Value1Label { get; }
            public string Value2Label { get; }
            public string Help { get; }

            public ValueInfo(bool value1Used, string value1Label, bool value2Used, string value2Label, string help)
            {
                Value1Used = value1Used;
                Value1Label = value1Label;
                Value2Used = value2Used;
                Value2Label = value2Label;
                Help = help;
            }
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

            // ID 昇順で並べる（C{4桁}/E{4桁} の固定桁なので序数比較で数値順になり、C → E の順になる）。
            _entries.Sort(CompareById);

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
                    InUse = card.InUse,
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
            foreach (CardAttribute attribute in AllAttributes)
            {
                bool on = _attributeFilter.Contains(attribute);
                Color previous = GUI.backgroundColor;
                if (on)
                {
                    GUI.backgroundColor = AttributeColor(attribute);
                }
                bool next = GUILayout.Toggle(on, AttributeShortName(attribute), EditorStyles.toolbarButton, GUILayout.Width(28f));
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

                string label = entry.InUse ? $"{entry.Id}  {entry.Name}" : $"{entry.Id}  {entry.Name}  (未使用)";
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
        }

        private void DrawCommonFields(SerializedProperty element)
        {
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_cardName"), new GUIContent("名前"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_cost"), new GUIContent("コスト"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_image"), new GUIContent("画像"));

            bool isGreen = (CardAttribute)element.FindPropertyRelative("_attribute").enumValueIndex == CardAttribute.Green;
            using (new EditorGUI.DisabledScope(!isGreen))
            {
                EditorGUILayout.PropertyField(element.FindPropertyRelative("_victoryPointBonus"), new GUIContent("勝利点付帯値"));
            }
            if (!isGreen)
            {
                EditorGUILayout.LabelField(" ", "※ 勝利点付帯値は緑属性カードのみ有効", EditorStyles.miniLabel);
            }

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
            ValueInfo info = GetValueInfo(type);

            using (new EditorGUI.DisabledScope(!info.Value1Used))
            {
                EditorGUILayout.PropertyField(value1, new GUIContent(info.Value1Label));
            }
            using (new EditorGUI.DisabledScope(!info.Value2Used))
            {
                EditorGUILayout.PropertyField(value2, new GUIContent(info.Value2Label));
            }

            if (!string.IsNullOrEmpty(info.Help))
            {
                EditorGUILayout.HelpBox(info.Help, MessageType.None);
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
            string body = EffectBody(type, value1, value2, attribute, keyword);
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

        // EventType と値（n=値1, m=値2）・属性・特徴から効果本体のテキストを作る。効果なし/未実装は空文字。
        private string EffectBody(EventType type, int value1, int value2, CardAttribute attribute, string keyword)
        {
            switch (type)
            {
                case EventType.Draw:
                    return $"カードを{value1}枚引く";
                case EventType.BanishChar:
                    return $"相手のキャラを{(value1 <= 0 ? 1 : value1)}体選んで破壊する";
                case EventType.Recover:
                    return $"墓地から上の{value1}枚をデッキに戻す";
                case EventType.Switch:
                    return "自分のキャラ1体を手札に戻し、手札からキャラを1体配置する";
                case EventType.Evolve:
                    return "自分のキャラ1体を生贄にして、より高コストのキャラをコストなしで配置する";
                case EventType.CostBoost:
                    return $"{AttributeShortName(attribute)}コスト{value1}個分として扱う";
                case EventType.DamageAllEnemies:
                    return $"相手キャラ全体に{value1}ダメージを与える";
                case EventType.GainVPPerGreenGrave:
                    return "墓地にある緑カードの数だけ勝利点を得る";
                case EventType.DamageEnemy:
                    return $"相手キャラ{value1}体に{value2}ダメージを与える";
                case EventType.SummonChar:
                    return $"「{ResolveCardName($"C{value1}")}」を{(value2 <= 0 ? 1 : value2)}体召喚する";
                case EventType.NextCardCostFree:
                    return "次に使うカード1枚のコストを0にする";
                case EventType.Bounce:
                    return $"相手キャラ{value1}体を持ち主の手札に戻す";
                case EventType.BounceAll:
                    return "相手キャラ全体を持ち主の手札に戻す";
                case EventType.ExtraTurn:
                    return "追加でもう1度自分のターンを行う";
                case EventType.HealAllAllies:
                    return value1 <= 0 ? "自分のキャラ全体のHPを全回復する" : $"自分のキャラ全体のHPを{value1}回復する";
                case EventType.DrawSkipNext:
                    return $"カードを{value1}枚引く。次のドローを1回スキップする";
                case EventType.DrawNextTurnStart:
                    return $"次の自分のターン開始時に{value1}枚多く引く";
                case EventType.BuffAttackByKeyword:
                    return BuildKeywordBuffBody(keyword, value1, "攻撃力");
                case EventType.BuffHpByKeyword:
                    return BuildKeywordBuffBody(keyword, value1, "HP");
                case EventType.SummonFromDeckByKeyword:
                    return string.IsNullOrEmpty(keyword)
                        ? "デッキからキャラを1枚選んで場に出す"
                        : $"デッキから『{keyword}』を持つキャラを1枚選んで場に出す";
                case EventType.CopyFieldChar:
                    return $"自分のキャラを1体選び、そのコピーを{(value1 <= 0 ? 1 : value1)}体出す";
                default:
                    return string.Empty;
            }
        }

        // キーワードバフ（BuffAttackByKeyword / BuffHpByKeyword）の効果テキストを作る。
        private static string BuildKeywordBuffBody(string keyword, int value, string statName)
        {
            string subject = string.IsNullOrEmpty(keyword)
                ? "自分以外の味方キャラ"
                : $"自分以外の『{keyword}』を持つ味方キャラ";
            return $"{subject}の{statName}を{value}上げる";
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

        private static ValueInfo GetValueInfo(EventType type)
        {
            switch (type)
            {
                case EventType.Draw:
                    return new ValueInfo(true, "値1（ドロー枚数）", false, "値2（未使用）", "値1=デッキ上から手札に加える枚数。");
                case EventType.Recover:
                    return new ValueInfo(true, "値1（回収枚数）", false, "値2（未使用）", "値1=墓地の上から回収してデッキへ戻す枚数。");
                case EventType.CostBoost:
                    return new ValueInfo(true, "値1（コスト換算値）", false, "値2（未使用）", "値1=コストとして支払うときに数える値（属性一致時のみ）。");
                case EventType.DamageAllEnemies:
                    return new ValueInfo(true, "値1（ダメージ量）", false, "値2（未使用）", "値1=敵フィールド全員へ与えるダメージ。");
                case EventType.DamageEnemy:
                    return new ValueInfo(true, "値1（対象数）", true, "値2（ダメージ量）", "値1=選ぶ対象数 / 値2=各対象へのダメージ。");
                case EventType.SummonChar:
                    return new ValueInfo(true, "値1（召喚キャラID数字）", true, "値2（体数）", "値1=召喚するキャラIDの数字部分（例 1001→C1001）/ 値2=体数（0=1体）。");
                case EventType.Bounce:
                    return new ValueInfo(true, "値1（戻す体数）", false, "値2（未使用）", "値1=相手の手札へ戻す敵キャラの体数。");
                case EventType.HealAllAllies:
                    return new ValueInfo(true, "値1（回復量）", false, "値2（未使用）", "値1=自フィールド全員の回復量（0=最大HPまで全回復）。");
                case EventType.DrawSkipNext:
                    return new ValueInfo(true, "値1（ドロー枚数）", false, "値2（未使用）", "値1=即時ドロー枚数。次のドローフェーズを1回スキップする。");
                case EventType.DrawNextTurnStart:
                    return new ValueInfo(true, "値1（ドロー枚数）", false, "値2（未使用）", "値1=次ターン開始時に追加でドローする枚数。");
                case EventType.BuffAttackByKeyword:
                    return new ValueInfo(true, "値1（攻撃力の上昇量）", false, "値2（未使用）", "値1=同じ特徴を持つ味方キャラ（自分以外）の攻撃力を上げる量。発動キャラに特徴の設定が必要。");
                case EventType.BuffHpByKeyword:
                    return new ValueInfo(true, "値1（HPの上昇量）", false, "値2（未使用）", "値1=同じ特徴を持つ味方キャラ（自分以外）のHP（現在・最大）を上げる量。発動キャラに特徴の設定が必要。");
                case EventType.None:
                    return new ValueInfo(false, "値1（未使用）", false, "値2（未使用）", "効果なし。勝利点付帯値だけ得るカードはこれ＋付帯値で作る。");
                case EventType.BanishChar:
                    return new ValueInfo(true, "値1（対象数）", false, "値2（未使用）", "値1=破壊する敵キャラの体数（0=1体）。対象数が敵の数以上なら全員。");
                case EventType.SummonFromDeckByKeyword:
                    return new ValueInfo(false, "値1（未使用）", false, "値2（未使用）", "デッキから自身の特徴を持つキャラを1枚選んで場に出す。値は未使用。発動カードに特徴の設定が必要。");
                case EventType.CopyFieldChar:
                    return new ValueInfo(true, "値1（コピー体数）", false, "値2（未使用）", "値1=選んだ自分のキャラのコピーを出す体数（0=1体）。バフ・現在HP込みでコピー。");
                case EventType.Switch:
                case EventType.Evolve:
                case EventType.GainVPPerGreenGrave:
                case EventType.NextCardCostFree:
                case EventType.BounceAll:
                case EventType.ExtraTurn:
                    return new ValueInfo(false, "値1（未使用）", false, "値2（未使用）", "この効果は値を使用しません（0 のままで可）。");
                case EventType.AtkBoost:
                case EventType.DefBoost:
                case EventType.Negate:
                    return new ValueInfo(true, "値1", true, "値2", "※ この効果は enum 定義のみで未実装です。");
                default:
                    return new ValueInfo(true, "値1", true, "値2", string.Empty);
            }
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

        private static string AttributeShortName(CardAttribute attribute)
        {
            switch (attribute)
            {
                case CardAttribute.Red:
                    return "赤";
                case CardAttribute.Blue:
                    return "青";
                case CardAttribute.Green:
                    return "緑";
                case CardAttribute.Yellow:
                    return "黄";
                case CardAttribute.Black:
                    return "黒";
                case CardAttribute.Purple:
                    return "紫";
                case CardAttribute.White:
                    return "白";
                default:
                    return "?";
            }
        }
    }
}
