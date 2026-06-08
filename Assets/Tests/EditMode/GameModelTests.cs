using Main.Game;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class GameModelTests
    {
        [Test]
        public void 初期PhaseはDrawである()
        {
            GameModel model = new GameModel();

            Assert.AreEqual(TurnPhase.Draw, model.Phase);
        }

        [Test]
        public void 初期IsLocalTurnはtrue()
        {
            GameModel model = new GameModel();

            Assert.IsTrue(model.IsLocalTurn);
        }

        [Test]
        public void SetInitialTurn_trueを渡すとIsLocalTurnがtrue()
        {
            GameModel model = new GameModel();

            model.SetInitialTurn(true);

            Assert.IsTrue(model.IsLocalTurn);
        }

        [Test]
        public void SetInitialTurn_falseを渡すとIsLocalTurnがfalse()
        {
            GameModel model = new GameModel();

            model.SetInitialTurn(false);

            Assert.IsFalse(model.IsLocalTurn);
        }

        [Test]
        public void BeginMain_PhaseがMainになる()
        {
            GameModel model = new GameModel();

            model.BeginMain();

            Assert.AreEqual(TurnPhase.Main, model.Phase);
        }

        [Test]
        public void EndTurn_PhaseがDrawに戻る()
        {
            GameModel model = new GameModel();
            model.BeginMain();

            model.EndTurn();

            Assert.AreEqual(TurnPhase.Draw, model.Phase);
        }

        [Test]
        public void EndTurn_IsLocalTurnが反転する()
        {
            GameModel model = new GameModel();
            Assert.IsTrue(model.IsLocalTurn);

            model.EndTurn();

            Assert.IsFalse(model.IsLocalTurn);
        }

        [Test]
        public void SetInitialTurn後にEndTurnでIsLocalTurnが反転する()
        {
            GameModel model = new GameModel();
            model.SetInitialTurn(false);

            model.EndTurn();

            Assert.IsTrue(model.IsLocalTurn);
        }
    }
}
