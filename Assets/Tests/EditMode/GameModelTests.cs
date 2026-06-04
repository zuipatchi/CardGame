using Main.Card;
using Main.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Tests.EditMode
{
    public class GameModelTests
    {
        private static readonly string TemplatePath = "Assets/AddressableAssets/Card/Card.uxml";

        private static CardView MakeCard(bool isSkill = false)
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            CardData data = isSkill
                ? (CardData)new SkillCardData("s1", "ファイア", 1, SkillType.Attack, 3)
                : new CharacterCardData("c1", "戦士", 2, 0);
            return new CardView(template, data);
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
        public void SetInitialTurn後もEndTurnでIsLocalTurnが反転する()
        {
            GameModel model = new GameModel();
            model.SetInitialTurn(false);

            model.EndTurn();

            Assert.IsTrue(model.IsLocalTurn);
        }

        [Test]
        public void 初期PhaseはDrawである()
        {
            GameModel model = new GameModel();

            Assert.AreEqual(TurnPhase.Draw, model.Phase);
        }

        [Test]
        public void BeginCharacterSet_PhaseがCharacterSetになる()
        {
            GameModel model = new GameModel();
            model.BeginPreBattle2();

            model.BeginCharacterSet();

            Assert.AreEqual(TurnPhase.CharacterSet, model.Phase);
        }

        [Test]
        public void BeginPreBattle2_IsLocalTurnがtrueならIsLocalPreparationTurnもtrue()
        {
            GameModel model = new GameModel();

            model.BeginPreBattle2();

            Assert.IsTrue(model.IsLocalPreparationTurn);
        }

        [Test]
        public void ReadyCard_ReadyQueueにカードが追加される()
        {
            GameModel model = new GameModel();
            model.BeginPreBattle2();
            CardView card = MakeCard();

            model.ReadyCard(card);

            Assert.AreEqual(1, model.ReadyQueue.Count);
            Assert.AreEqual(card, model.ReadyQueue[0]);
        }

        [Test]
        public void ReadyCard_カードがReady状態になる()
        {
            GameModel model = new GameModel();
            model.BeginPreBattle2();
            CardView card = MakeCard();

            model.ReadyCard(card);

            Assert.AreEqual(CardState.Ready, card.State);
        }

        [Test]
        public void ReadyCard_IsLocalPreparationTurnが反転する()
        {
            GameModel model = new GameModel();
            model.BeginPreBattle2();
            bool before = model.IsLocalPreparationTurn;

            model.ReadyCard(MakeCard());

            Assert.AreEqual(!before, model.IsLocalPreparationTurn);
        }

        [Test]
        public void Pass_1回パスしても戦闘前2フェーズは終わらない()
        {
            GameModel model = new GameModel();
            model.BeginPreBattle2();

            bool ended = model.Pass();

            Assert.IsFalse(ended);
        }

        [Test]
        public void Pass_2連続パスで戦闘前2フェーズが終わる()
        {
            GameModel model = new GameModel();
            model.BeginPreBattle2();

            model.Pass();
            bool ended = model.Pass();

            Assert.IsTrue(ended);
        }

        [Test]
        public void Pass_カードをReadyにすると連続パスカウントがリセットされる()
        {
            GameModel model = new GameModel();
            model.BeginPreBattle2();

            model.Pass(); // 1回パス
            model.ReadyCard(MakeCard()); // Ready → カウントリセット
            model.Pass(); // 再び1回パス

            bool ended = model.Pass(); // 2回目のパス（通算では3回目）

            Assert.IsTrue(ended);
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
        public void EndTurn_ReadyQueueがクリアされる()
        {
            GameModel model = new GameModel();
            model.BeginPreBattle2();
            model.ReadyCard(MakeCard());

            model.EndTurn();

            Assert.AreEqual(0, model.ReadyQueue.Count);
        }

        [Test]
        public void BeginPreBattle2_IsLocalTurnがfalseならIsLocalPreparationTurnもfalse()
        {
            GameModel model = new GameModel();
            model.SetInitialTurn(false);

            model.BeginPreBattle2();

            Assert.IsFalse(model.IsLocalPreparationTurn);
        }
    }
}
