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

        private VisualTreeAsset LoadTemplate() =>
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);

        private CardView CreateCard(int attack = 1, int defense = 1)
        {
            CardData data = new CardData("test", "テスト", 1, attack, defense);
            return new CardView(LoadTemplate(), data);
        }

        private DeckView CreateDeck(int cardCount)
        {
            CardData[] cards = new CardData[cardCount];
            for (int i = 0; i < cardCount; i++)
            {
                cards[i] = new CardData($"d{i}", "カード", 1, 1, 1);
            }
            return new DeckView(LoadTemplate(), cards);
        }

        [Test]
        public void DoAction_アクション後にカードがReady状態になる()
        {
            GameModel model = new GameModel();
            CardView card = CreateCard();

            model.DoAction(card, new PlayCardAction()).GetAwaiter().GetResult();

            Assert.AreEqual(CardState.Ready, card.State);
        }

        [Test]
        public void DoAction_次のアクションで以前のReadyカードがNormalに戻る()
        {
            GameModel model = new GameModel();
            CardView card1 = CreateCard();
            CardView card2 = CreateCard();

            model.DoAction(card1, new PlayCardAction()).GetAwaiter().GetResult();
            model.DoAction(card2, new PlayCardAction()).GetAwaiter().GetResult();

            Assert.AreEqual(CardState.Normal, card1.State);
            Assert.AreEqual(CardState.Ready, card2.State);
        }

        [Test]
        public void DoAction_アクション後にターンが切り替わる()
        {
            GameModel model = new GameModel();
            CardView card = CreateCard();

            Assert.IsTrue(model.IsLocalTurn);
            model.DoAction(card, new PlayCardAction()).GetAwaiter().GetResult();
            Assert.IsFalse(model.IsLocalTurn);
            model.DoAction(card, new PlayCardAction()).GetAwaiter().GetResult();
            Assert.IsTrue(model.IsLocalTurn);
        }

        [Test]
        public void DoAction_OnResolveイベントが以前のReadyカードで発火する()
        {
            GameModel model = new GameModel();
            CardView card1 = CreateCard();
            CardView card2 = CreateCard();
            CardView resolvedCard = null;
            PendingAction resolvedAction = null;
            model.OnResolve += (c, a) =>
            {
                resolvedCard = c;
                resolvedAction = a;
            };

            PlayCardAction action1 = new PlayCardAction();
            model.DoAction(card1, action1).GetAwaiter().GetResult();
            Assert.IsNull(resolvedCard);

            model.DoAction(card2, new PlayCardAction()).GetAwaiter().GetResult();
            Assert.AreEqual(card1, resolvedCard);
            Assert.AreEqual(action1, resolvedAction);
        }

        [Test]
        public void DoAction_攻撃アクションでOnResolveにAttackActionが渡される()
        {
            GameModel model = new GameModel();
            CardView attacker = CreateCard(attack: 3, defense: 1);
            CardView target = CreateCard(attack: 1, defense: 2);
            PendingAction resolvedAction = null;
            model.OnResolve += (_, a) => resolvedAction = a;

            model.DoAction(attacker, new AttackAction(target)).GetAwaiter().GetResult();
            model.DoAction(target, new PlayCardAction()).GetAwaiter().GetResult();

            Assert.IsInstanceOf<AttackAction>(resolvedAction);
            Assert.AreEqual(target, ((AttackAction)resolvedAction).Target);
        }

        [Test]
        public void DoAction_デッキ攻撃アクションでOnResolveにDeckAttackActionが渡される()
        {
            GameModel model = new GameModel();
            CardView attacker = CreateCard(attack: 2, defense: 1);
            DeckView deck = CreateDeck(5);
            PendingAction resolvedAction = null;
            model.OnResolve += (_, a) => resolvedAction = a;

            model.DoAction(attacker, new DeckAttackAction(deck)).GetAwaiter().GetResult();
            model.DoAction(CreateCard(), new PlayCardAction()).GetAwaiter().GetResult();

            Assert.IsInstanceOf<DeckAttackAction>(resolvedAction);
            Assert.AreEqual(deck, ((DeckAttackAction)resolvedAction).Target);
        }

        [Test]
        public void DeckView_RemoveFromTop_指定枚数だけカードが減る()
        {
            DeckView deck = CreateDeck(5);

            deck.RemoveFromTop(2);

            Assert.AreEqual(3, deck.Count);
        }

        [Test]
        public void DeckView_RemoveFromTop_残枚数を超えて指定しても0以下にならない()
        {
            DeckView deck = CreateDeck(3);

            deck.RemoveFromTop(10);

            Assert.AreEqual(0, deck.Count);
        }
    }
}
