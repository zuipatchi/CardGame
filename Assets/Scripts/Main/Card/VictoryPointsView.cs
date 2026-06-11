using UnityEngine.UIElements;

namespace Main.Card
{
    // 緑属性の勝利条件の勝利点表示（MedalIcon + 数字）。
    // 緑属性カードのプレイで Activate され、以後永続表示。カード効果で加点される。
    public sealed class VictoryPointsView : VisualElement
    {
        private readonly Label _label;
        private int _points;

        public bool IsActive { get; private set; }
        public int Points => _points;

        public VictoryPointsView(bool isOpponent)
        {
            AddToClassList("victory-points");
            AddToClassList(isOpponent ? "victory-points--opponent" : "victory-points--player");
            pickingMode = PickingMode.Ignore;
            style.display = DisplayStyle.None;

            VisualElement icon = new VisualElement();
            icon.AddToClassList("victory-points-icon");
            icon.pickingMode = PickingMode.Ignore;
            Add(icon);

            _label = new Label(_points.ToString());
            _label.AddToClassList("victory-points-label");
            _label.pickingMode = PickingMode.Ignore;
            Add(_label);
        }

        // 一度有効化されたら永続（非表示には戻らない）
        public void Activate()
        {
            if (IsActive)
            {
                return;
            }
            IsActive = true;
            style.display = DisplayStyle.Flex;
        }

        // 論理値（勝利判定に使う）だけを更新する。表示の更新は SetDisplayedPoints で行う
        public void AddPoints(int amount)
        {
            _points += amount;
        }

        // 表示中の数字を設定する（カウントアップ演出から呼ぶ）
        public void SetDisplayedPoints(int value)
        {
            _label.text = value.ToString();
        }
    }
}
