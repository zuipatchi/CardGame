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
        public string Id => _id;
        public string CardName => _cardName;
        public int Cost => _cost;
        public Sprite Image => _image;
        public string FlavorText => _flavorText;

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
