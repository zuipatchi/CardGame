using Main.Card;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Tests.EditMode
{
    public class CardViewTests
    {
        private static readonly string TemplatePath = "Assets/AddressableAssets/Card/Card.uxml";

        [Test]
        public void カード名が正しくバインドされる()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            CardData data = new CardData("fire_001", "ファイアボール", 3, 0, 0);

            CardView view = new CardView(template, data);

            Assert.AreEqual("ファイアボール", view.Q<Label>("NameLabel").text);
        }

        [Test]
        public void コストが正しくバインドされる()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            CardData data = new CardData("ice_001", "アイスランス", 2, 0, 0);

            CardView view = new CardView(template, data);

            Assert.AreEqual("2", view.Q<Label>("CostLabel").text);
        }

        [Test]
        public void 攻撃力と防御力が正しくバインドされる()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            CardData data = new CardData("dragon_001", "ドラゴン", 5, 4, 3);

            CardView view = new CardView(template, data);

            Assert.AreEqual("4", view.Q<Label>("AtkLabel").text);
            Assert.AreEqual("3", view.Q<Label>("DefLabel").text);
        }
    }
}
