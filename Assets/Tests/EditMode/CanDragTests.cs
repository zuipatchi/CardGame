using Main.Game;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class CanDragTests
    {
        private static bool CanPlayerDragCard(
            bool isGameOver,
            TurnPhase phase,
            bool charSetTcsNotNull,
            bool charSetCardNull,
            bool preBattle2TcsNotNull,
            bool isLocalPreparationTurn,
            bool prepCardNull)
        {
            if (isGameOver)
            {
                return false;
            }

            if (phase == TurnPhase.CharacterSet)
            {
                return charSetTcsNotNull && charSetCardNull;
            }

            if (phase == TurnPhase.PreBattle2)
            {
                return preBattle2TcsNotNull && isLocalPreparationTurn && prepCardNull;
            }

            return false;
        }

        // キャラセットフェーズ
        [Test]
        public void ゲームオーバー時は常にドラッグ不可()
        {
            Assert.IsFalse(CanPlayerDragCard(true, TurnPhase.CharacterSet, true, true, false, false, true));
        }

        [Test]
        public void キャラセットフェーズでTcsがnullならドラッグ不可()
        {
            Assert.IsFalse(CanPlayerDragCard(false, TurnPhase.CharacterSet, false, true, false, false, true));
        }

        [Test]
        public void キャラセットフェーズでカードステージング済みならドラッグ不可()
        {
            Assert.IsFalse(CanPlayerDragCard(false, TurnPhase.CharacterSet, true, false, false, false, true));
        }

        [Test]
        public void キャラセットフェーズでTcsありカードなしならドラッグ可能()
        {
            Assert.IsTrue(CanPlayerDragCard(false, TurnPhase.CharacterSet, true, true, false, false, true));
        }

        // 戦闘前2フェーズ
        [Test]
        public void 戦闘前2フェーズでTcsがnullならドラッグ不可()
        {
            Assert.IsFalse(CanPlayerDragCard(false, TurnPhase.PreBattle2, false, true, false, true, true));
        }

        [Test]
        public void 戦闘前2フェーズでIsLocalPreparationTurnがfalseならドラッグ不可()
        {
            Assert.IsFalse(CanPlayerDragCard(false, TurnPhase.PreBattle2, false, true, true, false, true));
        }

        [Test]
        public void 戦闘前2フェーズでカードステージング済みならドラッグ不可()
        {
            Assert.IsFalse(CanPlayerDragCard(false, TurnPhase.PreBattle2, false, true, true, true, false));
        }

        [Test]
        public void 戦闘前2フェーズでTcsありIsLocalPreparationTurnがtrueかつカードなしならドラッグ可能()
        {
            Assert.IsTrue(CanPlayerDragCard(false, TurnPhase.PreBattle2, false, true, true, true, true));
        }

        [Test]
        public void 戦闘前2フェーズでOKクリック後にTcsがnullになるとドラッグ不可()
        {
            Assert.IsFalse(CanPlayerDragCard(false, TurnPhase.PreBattle2, false, true, false, true, true));
        }

        // その他フェーズ
        [Test]
        public void ドローフェーズはドラッグ不可()
        {
            Assert.IsFalse(CanPlayerDragCard(false, TurnPhase.Draw, false, true, false, false, true));
        }

        [Test]
        public void 戦闘フェーズはドラッグ不可()
        {
            Assert.IsFalse(CanPlayerDragCard(false, TurnPhase.Battle, false, true, false, false, true));
        }
    }
}
