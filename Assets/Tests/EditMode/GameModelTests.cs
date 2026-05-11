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
                ? (CardData)new SkillCardData("s1", "ファイア", 1, damage: 3)
                : new CharacterCardData("c1", "戦士", 2, defense: 5);
            return new CardView(template, data);
        }

        [Test]
        public void BeginPreparation_IsLocalTurnがtrueならIsLocalPreparationTurnもtrue()
        {
            GameModel model = new GameModel();

            model.BeginPreparation();

            Assert.IsTrue(model.IsLocalPreparationTurn);
        }

        [Test]
        public void ReadyCard_ReadyQueueにカードが追加される()
        {
            GameModel model = new GameModel();
            model.BeginPreparation();
            CardView card = MakeCard();

            model.ReadyCard(card);

            Assert.AreEqual(1, model.ReadyQueue.Count);
            Assert.AreEqual(card, model.ReadyQueue[0]);
        }

        [Test]
        public void ReadyCard_カードがReady状態になる()
        {
            GameModel model = new GameModel();
            model.BeginPreparation();
            CardView card = MakeCard();

            model.ReadyCard(card);

            Assert.AreEqual(CardState.Ready, card.State);
        }

        [Test]
        public void ReadyCard_IsLocalPreparationTurnが反転する()
        {
            GameModel model = new GameModel();
            model.BeginPreparation();
            bool before = model.IsLocalPreparationTurn;

            model.ReadyCard(MakeCard());

            Assert.AreEqual(!before, model.IsLocalPreparationTurn);
        }

        [Test]
        public void Pass_1回パスしても準備フェーズは終わらない()
        {
            GameModel model = new GameModel();
            model.BeginPreparation();

            bool ended = model.Pass();

            Assert.IsFalse(ended);
        }

        [Test]
        public void Pass_2連続パスで準備フェーズが終わる()
        {
            GameModel model = new GameModel();
            model.BeginPreparation();

            model.Pass();
            bool ended = model.Pass();

            Assert.IsTrue(ended);
        }

        [Test]
        public void Pass_カードをReadyにすると連続パスカウントがリセットされる()
        {
            GameModel model = new GameModel();
            model.BeginPreparation();

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
            model.BeginPreparation();
            model.ReadyCard(MakeCard());

            model.EndTurn();

            Assert.AreEqual(0, model.ReadyQueue.Count);
        }

        [Test]
        public void BeginPreparation_IsLocalTurnがfalseならIsLocalPreparationTurnもfalse()
        {
            GameModel model = new GameModel();
            model.EndTurn(); // IsLocalTurn = false

            model.BeginPreparation();

            Assert.IsFalse(model.IsLocalPreparationTurn);
        }
    }
}
