using UnityEngine.UIElements;

namespace Main.Card
{
    // 勝利点表示（MedalIcon + 数字）。ゲーム共通の勝利条件（勝利点 WinRule.VictoryPointsToWin で勝利）の
    // ため、ゲーム開始時から常時表示する。カード効果（VictoryPointBonus / GainVPPerGreenGrave）で加点される。
    public sealed class VictoryPointsView : VisualElement
    {
        private readonly Label _label;
        private int _points;

        public int Points => _points;

        public VictoryPointsView(bool isOpponent)
        {
            AddToClassList("victory-points");
            AddToClassList(isOpponent ? "victory-points--opponent" : "victory-points--player");
            pickingMode = PickingMode.Ignore;

            VisualElement icon = new VisualElement();
            icon.AddToClassList("victory-points-icon");
            icon.pickingMode = PickingMode.Ignore;
            Add(icon);

            _label = new Label(_points.ToString());
            _label.AddToClassList("victory-points-label");
            _label.pickingMode = PickingMode.Ignore;
            Add(_label);
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
