using Main.Card;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Tests.EditMode
{
    public class DeckViewTests
    {
        private static readonly string TemplatePath = "Assets/AddressableAssets/Card/Card.uxml";

        private static CardData MakeCard(string id) =>
            new CardData(id, id, 1, 0, 0);

        private static VisualTreeAsset LoadTemplate() =>
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);

        [Test]
        public void 渡した枚数分のCardViewが生成される()
        {
            CardData[] cards = { MakeCard("a"), MakeCard("b"), MakeCard("c") };

            DeckView deck = new DeckView(LoadTemplate(), cards);

            Assert.AreEqual(3, deck.Count);
        }

        [Test]
        public void 枚数0のとき管理カード数が0になる()
        {
            DeckView deck = new DeckView(LoadTemplate(), System.Array.Empty<CardData>());

            Assert.AreEqual(0, deck.Count);
        }

        [Test]
        public void 各カードがAbsoluteで配置される()
        {
            CardData[] cards = { MakeCard("a"), MakeCard("b") };

            DeckView deck = new DeckView(LoadTemplate(), cards);

            foreach (VisualElement child in deck.Children())
            {
                if (child is CardView cardView)
                {
                    Assert.AreEqual(Position.Absolute, cardView.style.position.value);
                }
            }
        }

        [Test]
        public void カードが裏向きで生成される()
        {
            CardData[] cards = { MakeCard("a"), MakeCard("b") };

            DeckView deck = new DeckView(LoadTemplate(), cards);

            int checkedCount = 0;
            foreach (VisualElement child in deck.Children())
            {
                if (child is CardView cardView)
                {
                    Assert.IsTrue(cardView.IsFaceDown);
                    checkedCount++;
                }
            }
            Assert.AreEqual(cards.Length, checkedCount);
        }
    }
}
