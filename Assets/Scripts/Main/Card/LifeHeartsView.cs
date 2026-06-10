using System.Collections.Generic;
using Main.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    // ハート勝利条件のライフ表示（ハート3個）。
    // 赤属性カードのプレイで Activate され、以後永続表示。攻撃を受けるたびに1個ずつ削除される。
    public sealed class LifeHeartsView : VisualElement
    {
        private readonly List<VisualElement> _hearts = new List<VisualElement>();

        public bool IsActive { get; private set; }
        public int Remaining => _hearts.Count;
        public bool CanBeAttacked => IsActive && _hearts.Count > 0;

        public LifeHeartsView(bool isOpponent)
        {
            AddToClassList("life-hearts");
            AddToClassList(isOpponent ? "life-hearts--opponent" : "life-hearts--player");
            pickingMode = PickingMode.Ignore;
            style.display = DisplayStyle.None;

            for (int i = 0; i < HeartRule.InitialHeartCount; i++)
            {
                VisualElement heart = new VisualElement();
                heart.AddToClassList("life-heart");
                heart.pickingMode = PickingMode.Ignore;
                _hearts.Add(heart);
                Add(heart);
            }
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

        public bool ContainsPoint(Vector2 worldPos)
        {
            return CanBeAttacked && worldBound.Contains(worldPos);
        }

        // 次に攻撃を受けるハート（右端）
        public VisualElement PeekNextHeart()
        {
            return _hearts.Count > 0 ? _hearts[_hearts.Count - 1] : null;
        }

        public void RemoveHeart()
        {
            if (_hearts.Count == 0)
            {
                return;
            }
            VisualElement heart = _hearts[_hearts.Count - 1];
            _hearts.RemoveAt(_hearts.Count - 1);
            heart.RemoveFromHierarchy();
        }
    }
}
