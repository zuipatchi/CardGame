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
        private readonly bool _isOpponent;

        public bool IsActive { get; private set; }
        public int Remaining => _hearts.Count;
        public bool CanBeAttacked => IsActive && _hearts.Count > 0;

        // 相手側コンテナは右端アンカーのため、削除時に残ハートが右へ詰まる。
        // 見た目のギャップとパーティクル位置を一致させるため、相手側は左端・自分側は右端を破壊する
        private int NextHeartIndex => _isOpponent ? 0 : _hearts.Count - 1;

        public LifeHeartsView(bool isOpponent)
        {
            _isOpponent = isOpponent;
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

        // ワールド座標が攻撃可能なハート領域内かどうか（VisualElement.ContainsPoint はローカル座標のため別名）
        public bool ContainsWorldPoint(Vector2 worldPos)
        {
            return CanBeAttacked && worldBound.Contains(worldPos);
        }

        // 攻撃対象としてのハイライト（自分のターンに攻撃可能なとき各ハートを強調）
        public void SetAttackTargetHighlight(bool highlight)
        {
            foreach (VisualElement heart in _hearts)
            {
                if (highlight)
                {
                    heart.AddToClassList("life-heart--attack-target");
                }
                else
                {
                    heart.RemoveFromClassList("life-heart--attack-target");
                }
            }
        }

        // 次に攻撃を受けるハート
        public VisualElement PeekNextHeart()
        {
            return _hearts.Count > 0 ? _hearts[NextHeartIndex] : null;
        }

        public void RemoveHeart()
        {
            if (_hearts.Count == 0)
            {
                return;
            }
            int index = NextHeartIndex;
            VisualElement heart = _hearts[index];
            _hearts.RemoveAt(index);
            heart.RemoveFromHierarchy();
        }
    }
}
