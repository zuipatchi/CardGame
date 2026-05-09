using Main.Card;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tests.EditMode
{
    public class ArrowViewTests
    {
        [Test]
        public void ArrowView_初期状態のPickingModeはIgnore()
        {
            ArrowView view = new ArrowView();
            Assert.AreEqual(PickingMode.Ignore, view.pickingMode);
        }

        [Test]
        public void ArrowView_position_absoluteで配置される()
        {
            ArrowView view = new ArrowView();
            Assert.AreEqual(Position.Absolute, view.style.position.value);
        }

        [Test]
        public void ArrowView_StartPointを設定すると値が更新される()
        {
            ArrowView view = new ArrowView();
            Vector2 pos = new Vector2(100f, 200f);
            view.StartPoint = pos;
            Assert.AreEqual(pos, view.StartPoint);
        }

        [Test]
        public void ArrowView_EndPointを設定すると値が更新される()
        {
            ArrowView view = new ArrowView();
            Vector2 pos = new Vector2(300f, 400f);
            view.EndPoint = pos;
            Assert.AreEqual(pos, view.EndPoint);
        }
    }
}
