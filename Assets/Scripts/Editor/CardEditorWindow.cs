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
            // リリース弾（第N弾。1始まり）。所属 SO から取得する。
            public int Set { get; set; }
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
        // 弾フィルタ。0＝すべての弾を表示、N＝第N弾のみ。
        private int _setFilter;
        private bool _showCharacter = true;
        private bool _showEvent = true;
        private SortMode _sortMode = SortMode.Id;

        private Vector2 _listScroll;
        private Vector2 _formScroll;

        private CardAttribute _newAttribute = CardAttribute.Red;
        private bool _newIsCharacter = true;
        // 新規追加先の弾（第N弾。1始まり）。該当する弾の SO が無ければ追加時に自動生成・登録する。
        private int _newSet = 1;

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

        // カードエディタを開かずに、全カードの効果テキストを一括生成する Card メニュー項目。
        // エディタが既に開いていればそのインスタンスで実行（表示中の効果テキストも更新される）、
        // 開いていなければ一時インスタンスを生成して実行・破棄する。生成ロジックは
        // RegenerateAllDescriptions に集約済みで、ウィンドウのボタンと同じ結果になる。
        [MenuItem("Card/効果テキストを一括生成")]
        public static void RegenerateAllDescriptionsFromMenu()
        {
            if (HasOpenInstances<CardEditorWindow>())
            {
                GetWindow<CardEditorWindow>().RegenerateAllDescriptions();
                return;
            }

            CardEditorWindow window = CreateInstance<CardEditorWindow>();
            try
            {
                window.RegenerateAllDescriptions();
            }
            finally
            {
                DestroyImmediate(window);
            }
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
                    Set = card.Set,
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
                if (so != null && CardIdAutoAssigner.AssignIds(so.Cards, "C", so.EditorSet))
                {
                    EditorUtility.SetDirty(so);
                    changed++;
                }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:EventCardSO"))
            {
                EventCardSO so = AssetDatabase.LoadAssetAtPath<EventCardSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (so != null && CardIdAutoAssigner.AssignIds(so.Cards, "E", so.EditorSet))
                {
                    EditorUtility.SetDirty(so);
                    changed++;
                }
            }

            AssetDatabase.SaveAssets();
            RebuildEntries();
            Debug.Log($"カード ID を再採番しました（更新した SO: {changed} 種）。");
        }

        // 全 SO の全カードの効果テキストを現在の設定（発動タイミング・効果種別・値・勝利点）から
        // 自動生成して上書きする。ResolveCardName/ResolveCardStats が _entries を参照するため、
        // 先に最新の一覧を構築しておく。
        private void RegenerateAllDescriptions()
        {
            if (!EditorUtility.DisplayDialog("効果テキスト一括生成",
                "全カードの効果テキストを現在の設定から自動生成して上書きします。\n"
                + "手動で編集した効果テキストも上書きされます。続行しますか？",
                "生成する", "キャンセル"))
            {
                return;
            }

            RebuildEntries();

            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:CharacterCardSO"))
            {
                CharacterCardSO so = AssetDatabase.LoadAssetAtPath<CharacterCardSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (RegenerateDescriptionsFor(so, true))
                {
                    changed++;
                }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:EventCardSO"))
            {
                EventCardSO so = AssetDatabase.LoadAssetAtPath<EventCardSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (RegenerateDescriptionsFor(so, false))
                {
                    changed++;
                }
            }

            AssetDatabase.SaveAssets();
            if (_selected != null && _selected.So != null)
            {
                _selectedSo = new SerializedObject(_selected.So);
            }
            Debug.Log($"効果テキストを一括生成しました（更新した SO: {changed} 種）。");
        }

        // 1つの SO 内の全カードの効果テキストを生成して上書きする。変更があれば true を返す。
        private bool RegenerateDescriptionsFor(ScriptableObject so, bool isCharacter)
        {
            if (so == null)
            {
                return false;
            }

            SerializedObject sobj = new SerializedObject(so);
            SerializedProperty cards = sobj.FindProperty("_cards");
            if (cards == null)
            {
                return false;
            }

            for (int i = 0; i < cards.arraySize; i++)
            {
                SerializedProperty element = cards.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_description").stringValue = BuildDescription(element, isCharacter);
            }

            if (sobj.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(so);
                return true;
            }
            return false;
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
            DrawSetFilter();

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

            GUILayout.Space(8f);

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

        // 弾フィルタのポップアップ（すべて / 第1弾 / … / 第N弾）。N は現在存在する最大の弾。
        private void DrawSetFilter()
        {
            int maxSet = MaxSet();
            // 選択中の弾が一覧から消えた（最大弾が下がった）場合は「すべて」へ戻す。
            if (_setFilter > maxSet)
            {
                _setFilter = 0;
            }

            string[] labels = new string[maxSet + 1];
            labels[0] = "弾:すべて";
            for (int set = 1; set <= maxSet; set++)
            {
                labels[set] = $"第{set}弾";
            }

            _setFilter = EditorGUILayout.Popup(_setFilter, labels, EditorStyles.toolbarPopup, GUILayout.Width(84f));
        }

        // 現在の一覧に存在する最大の弾番号（最低1）。弾フィルタ／追加パネルの目安に使う。
        private int MaxSet()
        {
            int max = 1;
            foreach (CardEntry entry in _entries)
            {
                if (entry.Set > max)
                {
                    max = entry.Set;
                }
            }
            return max;
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
                // 第2弾以降は弾バッジを付けて見分ける（第1弾は無印）。
                string setBadge = entry.Set > 1 ? $"  〈弾{entry.Set}〉" : string.Empty;
                string suffix = !entry.InUse ? "  (未使用)" : entry.IsToken ? "  (トークン)" : string.Empty;
                string label = $"{prefix}{setBadge}  {entry.Name}{suffix}";
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
            if (_setFilter > 0 && entry.Set != _setFilter)
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
            _newSet = Mathf.Max(1, EditorGUILayout.IntField("弾（第N弾）", _newSet));
            EditorGUILayout.LabelField(" ", $"追加先: Data/Set{_newSet}/{_newAttribute}/{SoAssetName(_newAttribute, _newIsCharacter, _newSet)}", EditorStyles.miniLabel);

            if (GUILayout.Button("追加"))
            {
                AddCard(_newAttribute, _newIsCharacter, _newSet);
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
                // 弾は所属 SO が一括設定するため読み取り専用（属性と同じ扱い）。
                EditorGUILayout.IntField("弾（第N弾）", _selected.Set);
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

            // コスト素材にできない（お邪魔トークン用）：手札からコスト支払いの素材に数えない（CostPaymentValue=0）。
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_cannotBeUsedAsCost"), new GUIContent("このカードはコストの支払いには使えない"));

            // コストの色を無視（お邪魔カード用）：このカード自身のコスト支払いで同属性素材1枚の色制約を免除する。
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_ignoreCostColor"), new GUIContent("このカードを使用するときは色制限を無視できる"));

            EditorGUILayout.PropertyField(element.FindPropertyRelative("_victoryPointBonus"), new GUIContent("勝利点付帯値"));

            EditorGUILayout.PropertyField(element.FindPropertyRelative("_description"), new GUIContent("効果テキスト"));

            // _flavorText は [TextArea] のため PropertyField だと複数行になる。ここでは1行で編集する。
            SerializedProperty flavor = element.FindPropertyRelative("_flavorText");
            flavor.stringValue = EditorGUILayout.TextField("フレーバー", flavor.stringValue);

            DrawVoiceSpeakerField(element);

            DrawKeywordField(element);
        }

        // フレーバー読み上げ音声の話者（VOICEVOX speaker）をドロップダウンで選ぶ。
        // 「（共通設定を使う）」＝ID 0 のときは、フレーバー音声生成ツールの既定話者で生成される。
        private void DrawVoiceSpeakerField(SerializedProperty element)
        {
            SerializedProperty speaker = element.FindPropertyRelative("_voiceSpeaker");
            int currentIndex = VoiceSpeakerCatalog.IndexOf(speaker.intValue);
            int nextIndex = EditorGUILayout.Popup("読み上げ話者", currentIndex, VoiceSpeakerCatalog.Labels);
            if (nextIndex != currentIndex)
            {
                speaker.intValue = VoiceSpeakerCatalog.IdAt(nextIndex);
            }
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
                element.FindPropertyRelative("_effectValue2"),
                element.FindPropertyRelative("_effectParam"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("能力", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_guardian"), new GUIContent("守護"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_haste"), new GUIContent("速攻"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_flying"), new GUIContent("飛行"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_sakimori"), new GUIContent("防人"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_assault"), new GUIContent("強襲"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_noDeckAttack"), new GUIContent("デッキ攻撃×"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_deadly"), new GUIContent("必殺"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_triggerOnGrave"), new GUIContent("ダメージトリガー"));
        }

        private void DrawEventFields(SerializedProperty element)
        {
            EditorGUILayout.LabelField("効果", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(element.FindPropertyRelative("_eventTrigger"), new GUIContent("発動タイミング"));

            SerializedProperty eventType = element.FindPropertyRelative("_eventType");
            EditorGUILayout.PropertyField(eventType, new GUIContent("効果種別"));
            DrawValueFields(ToEventType(eventType),
                element.FindPropertyRelative("_eventValue"),
                element.FindPropertyRelative("_eventValue2"),
                element.FindPropertyRelative("_effectParam"));

            EditorGUILayout.PropertyField(element.FindPropertyRelative("_triggerOnGrave"), new GUIContent("ダメージトリガー"));
        }

        // EventType に応じて値1/値2 のラベルを切り替え、意味のヒントを表示する。
        // 値1が文字列の効果（EffectValueInfo.Value1IsText）では、int の value1 ではなく
        // 文字列の param（_effectParam）をテキスト入力欄として描画する。
        private void DrawValueFields(EventType type, SerializedProperty value1, SerializedProperty value2, SerializedProperty param)
        {
            EffectHandler handler = EffectCatalog.Get(type);
            EffectValueInfo info = handler != null ? handler.Values : default;

            if (!string.IsNullOrEmpty(info.Help))
            {
                EditorGUILayout.HelpBox(info.Help, MessageType.None);
            }

            if (info.Value1IsText)
            {
                EditorGUILayout.PropertyField(param, new GUIContent(info.Value1Label));
            }
            else
            {
                using (new EditorGUI.DisabledScope(!info.Value1Used))
                {
                    EditorGUILayout.PropertyField(value1, new GUIContent(info.Value1Label));
                }
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

        private void AddCard(CardAttribute attribute, bool isCharacter, int set)
        {
            set = Mathf.Max(1, set);
            ScriptableObject so = FindSo(attribute, isCharacter, set);
            if (so == null)
            {
                // 該当する（属性×弾）の SO がまだ無ければ自動生成し、CardDatabase に登録する。
                so = CreateAndRegisterSo(attribute, isCharacter, set);
            }
            if (so == null)
            {
                EditorUtility.DisplayDialog("追加できません",
                    $"{attribute} 第{set}弾の{(isCharacter ? "キャラ" : "イベント")} SO を生成・登録できませんでした。"
                    + "CardDatabase アセットが見つかるか確認してください。", "OK");
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
            element.FindPropertyRelative("_effectParam").stringValue = string.Empty;
            element.FindPropertyRelative("_triggerOnGrave").boolValue = false;
            element.FindPropertyRelative("_cannotBeUsedAsCost").boolValue = false;
            element.FindPropertyRelative("_ignoreCostColor").boolValue = false;

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
                element.FindPropertyRelative("_sakimori").boolValue = false;
                element.FindPropertyRelative("_assault").boolValue = false;
                element.FindPropertyRelative("_noDeckAttack").boolValue = false;
                element.FindPropertyRelative("_deadly").boolValue = false;
            }
            else
            {
                element.FindPropertyRelative("_eventTrigger").enumValueIndex = (int)EventCardTrigger.OnPlay;
                element.FindPropertyRelative("_eventType").enumValueIndex = (int)EventType.None;
                element.FindPropertyRelative("_eventValue").intValue = 0;
                element.FindPropertyRelative("_eventValue2").intValue = 0;
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

        // 指定した（属性 × 弾）の SO を探す。弾は未設定（_set=0）を第1弾とみなして比較する。
        private ScriptableObject FindSo(CardAttribute attribute, bool isCharacter, int set)
        {
            int wantedSet = Mathf.Max(1, set);
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
                SerializedProperty setProp = sobj.FindProperty("_set");
                int soSet = setProp != null ? Mathf.Max(1, setProp.intValue) : 1;
                if (attributeProp != null && (CardAttribute)attributeProp.enumValueIndex == attribute && soSet == wantedSet)
                {
                    return so;
                }
            }
            return null;
        }

        // 自動生成する SO アセットのファイル名。第1弾は無印（既存命名と互換）、第2弾以降は _Set{N} を付ける。
        private static string SoAssetName(CardAttribute attribute, bool isCharacter, int set)
        {
            string baseName = isCharacter ? "CharacterCards" : "EventCards";
            string suffix = set > 1 ? $"_Set{set}" : string.Empty;
            return $"{baseName}_{attribute}{suffix}.asset";
        }

        // 該当する（属性×弾）の SO を新規生成し、属性・弾を設定したうえで CardDatabase に登録する。
        // 配置先は弾ごとに分けたフォルダ Assets/Data/Set{弾}/{属性}/。
        private ScriptableObject CreateAndRegisterSo(CardAttribute attribute, bool isCharacter, int set)
        {
            string folder = SoFolder(attribute, set);
            if (!AssetDatabase.IsValidFolder(folder))
            {
                CreateFolderRecursive(folder);
            }

            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{SoAssetName(attribute, isCharacter, set)}");

            ScriptableObject so;
            if (isCharacter)
            {
                CharacterCardSO charSo = ScriptableObject.CreateInstance<CharacterCardSO>();
                charSo.EditorSetCards(attribute, new List<CharacterCardData>(), set);
                so = charSo;
            }
            else
            {
                EventCardSO eventSo = ScriptableObject.CreateInstance<EventCardSO>();
                eventSo.EditorSetCards(attribute, new List<EventCardData>(), set);
                so = eventSo;
            }

            AssetDatabase.CreateAsset(so, path);
            AssetDatabase.SaveAssets();

            if (!RegisterInDatabase(so, isCharacter))
            {
                Debug.LogWarning($"CardDatabase が見つからず、{path} を自動登録できませんでした。手動で CardDatabase に登録してください。");
            }
            return so;
        }

        // 弾×属性で分けた SO の配置フォルダ。Assets/Data/Set{弾}/{属性}/。
        private static string SoFolder(CardAttribute attribute, int set)
        {
            return $"Assets/Data/Set{Mathf.Max(1, set)}/{attribute}";
        }

        private static void CreateFolderRecursive(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        // 生成した SO を CardDatabase の対応する配列（_characterCardSets / _eventCardSets）の末尾に追加する。
        private static bool RegisterInDatabase(ScriptableObject so, bool isCharacter)
        {
            string[] dbGuids = AssetDatabase.FindAssets("t:CardDatabase");
            if (dbGuids.Length == 0)
            {
                return false;
            }

            CardDatabase db = AssetDatabase.LoadAssetAtPath<CardDatabase>(AssetDatabase.GUIDToAssetPath(dbGuids[0]));
            if (db == null)
            {
                return false;
            }

            SerializedObject dbSo = new SerializedObject(db);
            SerializedProperty array = dbSo.FindProperty(isCharacter ? "_characterCardSets" : "_eventCardSets");
            if (array == null)
            {
                return false;
            }

            int index = array.arraySize;
            array.InsertArrayElementAtIndex(index);
            array.GetArrayElementAtIndex(index).objectReferenceValue = so;
            dbSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            return true;
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

        // 指定カードの設定（発動タイミング・効果種別・値・属性・勝利点）から効果テキストを自動生成する。
        // 守護/速攻/飛行フラグは（アイコン表示があるため）テキストに含めない。
        private string BuildDescription(SerializedProperty element, bool isCharacter)
        {
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
            string param = element.FindPropertyRelative("_effectParam").stringValue;
            EffectHandler handler = EffectCatalog.Get(type);
            string body = handler != null
                ? handler.BuildBody(new EffectTextContext(value1, value2, attribute, keyword, param, ResolveCardName, ResolveCardStats))
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

            // コスト素材にできない（お邪魔トークン用）のフラグはテキストに明記する。
            // 発動タイミングを持たない常在の特性なので、接頭辞・効果本文とは独立した一文として末尾に足す。
            bool cannotBeUsedAsCost = element.FindPropertyRelative("_cannotBeUsedAsCost").boolValue;
            if (cannotBeUsedAsCost)
            {
                parts.Add("このカードはコストの支払いには使えない");
            }

            // コストの色を無視（お邪魔カード用）のフラグもテキストに明記する。常在の特性なので末尾に一文として足す。
            bool ignoreCostColor = element.FindPropertyRelative("_ignoreCostColor").boolValue;
            if (ignoreCostColor)
            {
                parts.Add("このカードを使用するときは色制限を無視できる");
            }

            // 効果も勝利点もコスト素材制限も色無視も無いときは接頭辞だけ残さず空にする。
            if (body.Length == 0 && victoryPoint <= 0 && !cannotBeUsedAsCost && !ignoreCostColor)
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
                case CharacterEffectTrigger.OnDealPlayerDamage:
                    return "相手プレイヤーにダメージを与えた時";
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
