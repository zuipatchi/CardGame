using Main.Card;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Tests.EditMode
{
    public class GraveyardViewTests
    {
        private static readonly string TemplatePath = "Assets/AddressableAssets/Card/Card.uxml";

        private static CardData MakeCard(string id) =>
            new CardData(id, id, 1, 1, 1);

        private static VisualTreeAsset LoadTemplate() =>
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);

        [Test]
        public void 初期状態のCountは0()
        {
            GraveyardView graveyard = new GraveyardView();

            Assert.AreEqual(0, graveyard.Count);
        }

        [Test]
        public void AddCardでCountが増える()
        {
            VisualTreeAsset template = LoadTemplate();
            GraveyardView graveyard = new GraveyardView();
            CardView card = new CardView(template, MakeCard("a"));

            graveyard.AddCard(card);

            Assert.AreEqual(1, graveyard.Count);
        }

        [Test]
        public void 複数カードを追加できる()
        {
            VisualTreeAsset template = LoadTemplate();
            GraveyardView graveyard = new GraveyardView();

            graveyard.AddCard(new CardView(template, MakeCard("a")));
            graveyard.AddCard(new CardView(template, MakeCard("b")));
            graveyard.AddCard(new CardView(template, MakeCard("c")));

            Assert.AreEqual(3, graveyard.Count);
        }

        [Test]
        public void AddCard後にカードがGraveyardViewの子になる()
        {
            VisualTreeAsset template = LoadTemplate();
            GraveyardView graveyard = new GraveyardView();
            CardView card = new CardView(template, MakeCard("a"));

            graveyard.AddCard(card);

            Assert.AreEqual(graveyard, card.parent);
        }
    }
}
