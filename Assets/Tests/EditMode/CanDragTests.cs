using Main.Game;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class CanDragTests
    {
        private static bool CanPlayerDragCard(
            bool isGameOver,
            TurnPhase phase,
            bool isLocalTurn,
            bool mainActionTcsNotNull,
            bool mainStagedCardNull)
        {
            if (isGameOver)
            {
                return false;
            }

            if (phase == TurnPhase.Main && isLocalTurn && mainActionTcsNotNull)
            {
                return mainStagedCardNull;
            }

            return false;
        }

        [Test]
        public void ゲームオーバー時は常にドラッグ不可()
        {
            Assert.IsFalse(CanPlayerDragCard(true, TurnPhase.Main, true, true, true));
        }

        [Test]
        public void メインフェーズでTcsがnullならドラッグ不可()
        {
            Assert.IsFalse(CanPlayerDragCard(false, TurnPhase.Main, true, false, true));
        }

        [Test]
        public void メインフェーズで相手ターンならドラッグ不可()
        {
            Assert.IsFalse(CanPlayerDragCard(false, TurnPhase.Main, false, true, true));
        }

        [Test]
        public void メインフェーズでカードステージング済みならドラッグ不可()
        {
            Assert.IsFalse(CanPlayerDragCard(false, TurnPhase.Main, true, true, false));
        }

        [Test]
        public void メインフェーズで自分ターンかつTcsありカードなしならドラッグ可能()
        {
            Assert.IsTrue(CanPlayerDragCard(false, TurnPhase.Main, true, true, true));
        }

        [Test]
        public void ドローフェーズはドラッグ不可()
        {
            Assert.IsFalse(CanPlayerDragCard(false, TurnPhase.Draw, true, true, true));
        }
    }
}
