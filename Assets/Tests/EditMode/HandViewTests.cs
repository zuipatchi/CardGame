using Main.Card;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Tests.EditMode
{
    public class HandViewTests
    {
        private static readonly string TemplatePath = "Assets/AddressableAssets/Card/Card.uxml";

        [Test]
        public void HandView_5枚のカードが表向きで表示される()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            CardData[] cards =
            {
                new EventCardData("e1", "テスト", 0),
                new EventCardData("e2", "テスト", 0),
                new EventCardData("e3", "テスト", 0),
                new EventCardData("e4", "テスト", 0),
                new EventCardData("e5", "テスト", 0),
            };

            HandView view = new HandView(template, cards);

            Assert.AreEqual(5, view.childCount);
            foreach (VisualElement child in view.Children())
            {
                CardView cardView = child as CardView;
                Assert.IsNotNull(cardView);
                Assert.IsFalse(cardView.IsFaceDown);
            }
        }

        [Test]
        public void HandView_0枚のとき子要素がない()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            HandView view = new HandView(template, new CardData[0]);
            Assert.AreEqual(0, view.childCount);
        }
    }
}
