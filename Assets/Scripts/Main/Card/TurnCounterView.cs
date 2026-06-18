using UnityEngine.UIElements;

namespace Main.Card
{
    // 経過ターン表示（「TURN」キャプション + 通算ターン番号）。ゲーム開始から常時表示し、
    // 各ターン開始時に SetTurn で更新する。数字は GameModel.TurnNumber（通算ターン・1始まり）。
    public sealed class TurnCounterView : VisualElement
    {
        private readonly Label _label;

        public TurnCounterView()
        {
            AddToClassList("turn-counter");
            pickingMode = PickingMode.Ignore;

            Label caption = new Label("TURN");
            caption.AddToClassList("turn-counter-caption");
            caption.pickingMode = PickingMode.Ignore;
            Add(caption);

            _label = new Label("1");
            _label.AddToClassList("turn-counter-label");
            _label.pickingMode = PickingMode.Ignore;
            Add(_label);
        }

        public void SetTurn(int turnNumber)
        {
            _label.text = turnNumber.ToString();
        }
    }
}
