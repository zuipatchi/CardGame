using System.Collections.Generic;
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

        private static GraveyardView MakeGraveyard() =>
            new GraveyardView(LoadTemplate(), null);

        private static CardView MakeCardView() =>
            new CardView(LoadTemplate(), new EventCardData("e1", "テスト", 0));

        [Test]
        public void 初期状態のCountは0()
        {
            GraveyardView graveyard = MakeGraveyard();

            Assert.AreEqual(0, graveyard.Count);
        }

        [Test]
        public void AddCardでCountが増える()
        {
            GraveyardView graveyard = MakeGraveyard();

            graveyard.AddCard(MakeCardView());

            Assert.AreEqual(1, graveyard.Count);
        }

        [Test]
        public void 複数カードを追加できる()
        {
            GraveyardView graveyard = MakeGraveyard();

            graveyard.AddCard(MakeCardView());
            graveyard.AddCard(MakeCardView());
            graveyard.AddCard(MakeCardView());

            Assert.AreEqual(3, graveyard.Count);
        }

        [Test]
        public void AddCard後にカードはGraveyardViewから切り離される()
        {
            GraveyardView graveyard = MakeGraveyard();
            CardView card = MakeCardView();

            graveyard.AddCard(card);

            Assert.IsNull(card.parent);
        }

        [Test]
        public void TakeFromTop_n枚取り出すとCountがn減る()
        {
            GraveyardView graveyard = MakeGraveyard();
            graveyard.AddCard(MakeCardView());
            graveyard.AddCard(MakeCardView());
            graveyard.AddCard(MakeCardView());

            graveyard.TakeFromTop(2);

            Assert.AreEqual(1, graveyard.Count);
        }

        [Test]
        public void TakeFromTop_指定枚数のカードデータを返す()
        {
            GraveyardView graveyard = MakeGraveyard();
            graveyard.AddCard(MakeCardView());
            graveyard.AddCard(MakeCardView());
            graveyard.AddCard(MakeCardView());

            List<CardData> taken = graveyard.TakeFromTop(2);

            Assert.AreEqual(2, taken.Count);
        }

        [Test]
        public void TakeFromTop_墓地枚数より多く指定しても全枚数を返す()
        {
            GraveyardView graveyard = MakeGraveyard();
            graveyard.AddCard(MakeCardView());
            graveyard.AddCard(MakeCardView());

            List<CardData> taken = graveyard.TakeFromTop(10);

            Assert.AreEqual(2, taken.Count);
            Assert.AreEqual(0, graveyard.Count);
        }
    }
}
