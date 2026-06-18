using UnityEngine.UIElements;

namespace Main.Card
{
    // 自分の手番の残り時間表示（「TIME」キャプション + 残り秒数）。自分のメインフェーズ中のみ表示し、
    // 毎秒 SetRemaining で更新する。残り時間が少なくなったら SetWarning(true) で赤く警告表示にする。
    public sealed class TurnTimerView : VisualElement
    {
        private readonly Label _label;

        public TurnTimerView()
        {
            AddToClassList("turn-timer");
            pickingMode = PickingMode.Ignore;
            style.display = DisplayStyle.None;

            Label caption = new Label("TIME");
            caption.AddToClassList("turn-timer-caption");
            caption.pickingMode = PickingMode.Ignore;
            Add(caption);

            _label = new Label("0");
            _label.AddToClassList("turn-timer-label");
            _label.pickingMode = PickingMode.Ignore;
            Add(_label);
        }

        public void SetRemaining(int seconds)
        {
            _label.text = seconds.ToString();
        }

        public void SetWarning(bool isWarning)
        {
            if (isWarning)
            {
                _label.AddToClassList("turn-timer-label--warning");
            }
            else
            {
                _label.RemoveFromClassList("turn-timer-label--warning");
            }
        }

        public void Show()
        {
            style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            style.display = DisplayStyle.None;
        }
    }
}
