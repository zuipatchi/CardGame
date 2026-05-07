using Main.Card;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Tests.EditMode
{
    public class FieldViewTests
    {
        private static readonly string TemplatePath = "Assets/AddressableAssets/Card/Card.uxml";

        private static CardView MakeCard()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            return new CardView(template, new CardData("c1", "カード", 1, 1, 1));
        }

        [Test]
        public void FieldView_初期状態は子要素がない()
        {
            FieldView view = new FieldView();
            Assert.AreEqual(0, view.childCount);
        }

        [Test]
        public void FieldView_PlaceCard_カードが追加される()
        {
            FieldView view = new FieldView();
            view.PlaceCard(MakeCard());
            Assert.AreEqual(1, view.childCount);
        }

        [Test]
        public void FieldView_5枚置くとIsFullがtrue()
        {
            FieldView view = new FieldView();
            for (int i = 0; i < 5; i++)
            {
                view.PlaceCard(MakeCard());
            }
            Assert.IsTrue(view.IsFull);
        }

        [Test]
        public void FieldView_満杯時にPlaceCardはfalseを返す()
        {
            FieldView view = new FieldView();
            for (int i = 0; i < 5; i++)
            {
                view.PlaceCard(MakeCard());
            }
            bool result = view.PlaceCard(MakeCard());
            Assert.IsFalse(result);
        }
    }
}
