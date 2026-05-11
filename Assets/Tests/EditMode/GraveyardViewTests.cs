using Main.Card;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Tests.EditMode
{
    public class GraveyardViewTests
    {
        private static readonly string TemplatePath = "Assets/AddressableAssets/Card/Card.uxml";

        private static VisualTreeAsset LoadTemplate() =>
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);

        private static CardView MakeCardView() =>
            new CardView(LoadTemplate(), new EventCardData("e1", "テスト", 0));

        [Test]
        public void 初期状態のCountは0()
        {
            GraveyardView graveyard = new GraveyardView();

            Assert.AreEqual(0, graveyard.Count);
        }

        [Test]
        public void AddCardでCountが増える()
        {
            GraveyardView graveyard = new GraveyardView();

            graveyard.AddCard(MakeCardView());

            Assert.AreEqual(1, graveyard.Count);
        }

        [Test]
        public void 複数カードを追加できる()
        {
            GraveyardView graveyard = new GraveyardView();

            graveyard.AddCard(MakeCardView());
            graveyard.AddCard(MakeCardView());
            graveyard.AddCard(MakeCardView());

            Assert.AreEqual(3, graveyard.Count);
        }

        [Test]
        public void AddCard後にカードがGraveyardViewの子になる()
        {
            GraveyardView graveyard = new GraveyardView();
            CardView card = MakeCardView();

            graveyard.AddCard(card);

            Assert.AreEqual(graveyard, card.parent);
        }
    }
}
