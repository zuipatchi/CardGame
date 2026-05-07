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
            new CardData(id, id, 1, "テスト", 0, 0);

        [Test]
        public void 渡した枚数分のCardViewが生成される()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            CardData[] cards = { MakeCard("a"), MakeCard("b"), MakeCard("c") };

            DeckView deck = new DeckView(template, cards);

            Assert.AreEqual(3, deck.childCount);
        }

        [Test]
        public void 枚数0のとき子要素が空になる()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);

            DeckView deck = new DeckView(template, System.Array.Empty<CardData>());

            Assert.AreEqual(0, deck.childCount);
        }

        [Test]
        public void 各カードがAbsoluteで配置される()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            CardData[] cards = { MakeCard("a"), MakeCard("b") };

            DeckView deck = new DeckView(template, cards);

            foreach (VisualElement child in deck.Children())
            {
                Assert.AreEqual(Position.Absolute, child.style.position.value);
            }
        }

        [Test]
        public void カードが裏向きで生成される()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            CardData[] cards = { MakeCard("a"), MakeCard("b") };

            DeckView deck = new DeckView(template, cards);

            foreach (VisualElement child in deck.Children())
            {
                CardView cardView = child as CardView;
                Assert.IsNotNull(cardView);
                Assert.IsTrue(cardView.IsFaceDown);
            }
        }
    }
}
