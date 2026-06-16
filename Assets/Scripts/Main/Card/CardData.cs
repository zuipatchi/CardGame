using UnityEngine;

namespace Main.Card
{
    public abstract class CardData
    {
        [SerializeField] protected string _id;
        [SerializeField] protected string _cardName;
        [SerializeField] protected int _cost;
        [SerializeField] protected Sprite _image;
        // フレーバーテキスト（世界観・雰囲気用。ゲーム効果には影響せず、カード詳細表示の最下部に表示される）
        [SerializeField, TextArea] protected string _flavorText;
        // 特徴（キーワード）。種族シナジー等の判定に使う任意の文字列（空＝特徴なし）。
        // 登録候補は CardKeywordSO（マスターリスト）で管理し、カードエディタのドロップダウンから選ぶ。
        // マッチング（同じ特徴か）は文字列一致で行うため、実行時に CardKeywordSO をロードする必要はない。
        [SerializeField] protected string _keyword;
        // 勝利点付帯値（勝利点の勝利条件への加点）。EventType の効果とは独立して、効果解決時に発動側へ加算する。
        // キャラはトリガー（OnEnter 等）のタイミング、イベントはプレイ／OnTurnStart のタイミングで加算。
        // 0 のときは加算なし（演出も出ない）。「勝利点を得るだけ」のカードは EventType=None + この値で表現する。
        [SerializeField] protected int _victoryPointBonus;
        // ゲームで使用しないカード（調整中・未完成など）の除外フラグ。
        // 既定値 false＝ゲームで使用する（既存アセットは未設定でも false になるため移行不要）。
        // true のカードは CardDatabase の集計（プール・対戦）から完全に除外される。
        [SerializeField] protected bool _excludeFromGame;
        // デッキ構築のプールからのみ除外するフラグ（トークンカード用）。
        // true でも対戦の参照テーブル（CardDatabase._dict）には登録されるため、効果から ID で召喚・参照できる。
        // 既定値 false＝デッキ構築でも使用する（既存アセットは未設定でも false になるため移行不要）。
        [SerializeField] protected bool _excludeFromDeckBuilder;
        public string Id => _id;
        public string CardName => _cardName;
        public int Cost => _cost;
        public Sprite Image => _image;
        public string FlavorText => _flavorText;
        // 特徴（キーワード）。空＝特徴なし。BuffAttackByKeyword / BuffHpByKeyword の対象判定（同じ特徴か）に使う。
        public string Keyword => _keyword;
        // 勝利点付帯値は全属性のカードで設定できる（属性による制限なし）。
        public int VictoryPointBonus => _victoryPointBonus;
        // 対戦で使用するか（対戦の ID 参照テーブルへの登録対象か）。トークンも含む。
        public bool InUse => !_excludeFromGame;
        // デッキ構築のプールに表示するか（対戦は可だがデッキに入れられないトークンは除外）。
        public bool InDeckPool => InUse && !_excludeFromDeckBuilder;

        public virtual int Attack => 0;
        public virtual int Hp => 0;
        public virtual CardAttribute Attribute => CardAttribute.White;

        // 手札からコストとして支払うときに、このカードが何コスト分として数えられるか（通常は1）。
        // payingForAttribute = 支払い対象（プレイするカード）の属性。
        // EventType.CostBoost を持つカードは、自分の属性が支払い対象属性と一致するときのみ設定値を返す（白も一般属性扱い）。
        public virtual int CostPaymentValue(CardAttribute payingForAttribute) => 1;

        protected CardData() { }

        protected CardData(string id, string name, int cost)
        {
            _id = id;
            _cardName = name;
            _cost = cost;
        }

#if UNITY_EDITOR
        // エディタ専用：ID 自動採番用（CharacterCardSO / EventCardSO の OnValidate から呼ばれる）
        public void EditorSetId(string id)
        {
            _id = id;
        }
#endif
    }
}
