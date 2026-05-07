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
                new CardData("c1", "カード1", 1, 1, 1),
                new CardData("c2", "カード2", 2, 2, 2),
                new CardData("c3", "カード3", 3, 3, 3),
                new CardData("c4", "カード4", 4, 4, 4),
                new CardData("c5", "カード5", 5, 5, 5),
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
