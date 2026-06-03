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
            return new CardView(template, new EventCardData("e1", "テスト", 0));
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
        public void FieldView_5枚以下はスケールが0_9()
        {
            FieldView view = new FieldView();
            for (int i = 0; i < 5; i++)
            {
                view.PlaceCard(MakeCard());
            }
            Assert.AreEqual(0.9f, view.CurrentCardScale, 0.001f);
        }

        [Test]
        public void FieldView_6枚置くとスケールが縮小される()
        {
            FieldView view = new FieldView();
            for (int i = 0; i < 6; i++)
            {
                view.PlaceCard(MakeCard());
            }
            // 0.9 * 5 / 6 = 0.75
            Assert.AreEqual(0.75f, view.CurrentCardScale, 0.001f);
        }

        [Test]
        public void FieldView_10枚置くとスケールが最小付近になる()
        {
            FieldView view = new FieldView();
            for (int i = 0; i < 10; i++)
            {
                view.PlaceCard(MakeCard());
            }
            // 0.9 * 5 / 10 = 0.45
            Assert.AreEqual(0.45f, view.CurrentCardScale, 0.001f);
        }

        [Test]
        public void FieldView_カード削除後スケールが更新される()
        {
            FieldView view = new FieldView();
            CardView extraCard = MakeCard();
            for (int i = 0; i < 5; i++)
            {
                view.PlaceCard(MakeCard());
            }
            view.PlaceCard(extraCard);
            Assert.Less(view.CurrentCardScale, 0.9f);

            view.RemoveCard(extraCard);
            Assert.AreEqual(0.9f, view.CurrentCardScale, 0.001f);
        }
    }
}
