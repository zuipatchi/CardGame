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
        public string Id => _id;
        public string CardName => _cardName;
        public int Cost => _cost;
        public Sprite Image => _image;
        public string FlavorText => _flavorText;
        // 特徴（キーワード）。空＝特徴なし。BuffAttackByKeyword / BuffHpByKeyword の対象判定（同じ特徴か）に使う。
        public string Keyword => _keyword;
        // 勝利点の加点は緑属性カードで設定する設計のため、緑以外のカードでは付帯値を常に 0 とみなす（誤設定しても加点されない）。
        public int VictoryPointBonus => Attribute == CardAttribute.Green ? _victoryPointBonus : 0;
        // ゲームで使用するか（デッキ構築のプール・対戦への供給対象か）。
        public bool InUse => !_excludeFromGame;

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

        // エディタ専用：緑属性以外のカードでは勝利点付帯値を 0 に固定する（SO の OnValidate から呼ばれる）。
        // 変更があったら true を返す。
        public bool EditorClampVictoryPointBonusToAttribute()
        {
            if (Attribute != CardAttribute.Green && _victoryPointBonus != 0)
            {
                _victoryPointBonus = 0;
                return true;
            }
            return false;
        }
#endif
    }
}
