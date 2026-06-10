using UnityEngine;

namespace Main.Card
{
    public abstract class CardData
    {
        [SerializeField] protected string _id;
        [SerializeField] protected string _cardName;
        [SerializeField] protected int _cost;
        [SerializeField] protected Sprite _image;
        public string Id => _id;
        public string CardName => _cardName;
        public int Cost => _cost;
        public Sprite Image => _image;

        public virtual int Attack => 0;
        public virtual int Hp => 0;
        public virtual CardAttribute Attribute => CardAttribute.White;

        // 手札からコストとして支払うときに、このカードが何コスト分として数えられるか（通常は1）。
        // EventType.CostBoost を持つカードは設定されたコスト値を返す。
        public virtual int CostPaymentValue => 1;

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
