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
        // 勝利点付帯値（緑属性の勝利条件）。EventType の効果とは独立して、効果解決時に発動側へ加算する。
        // キャラはトリガー（OnEnter 等）のタイミング、イベントはプレイ／OnTurnStart のタイミングで加算。
        // 0 のときは加算なし（演出も出ない）。「勝利点を得るだけ」のカードは EventType=None + この値で表現する。
        [SerializeField] protected int _victoryPointBonus;
        public string Id => _id;
        public string CardName => _cardName;
        public int Cost => _cost;
        public Sprite Image => _image;
        public string FlavorText => _flavorText;
        // 勝利点は緑属性の勝利条件なので、緑以外のカードでは付帯値を常に 0 とみなす（誤設定しても加点されない）。
        public int VictoryPointBonus => Attribute == CardAttribute.Green ? _victoryPointBonus : 0;

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
