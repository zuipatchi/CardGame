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
            CardData data = new CardData("fire_001", "ファイアボール", 3, "3ダメージを与える", 0, 0);

            CardView view = new CardView(template, data);

            Assert.AreEqual("ファイアボール", view.Q<Label>("NameLabel").text);
        }

        [Test]
        public void コストが正しくバインドされる()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            CardData data = new CardData("ice_001", "アイスランス", 2, "2ダメージ", 0, 0);

            CardView view = new CardView(template, data);

            Assert.AreEqual("2", view.Q<Label>("CostLabel").text);
        }

        [Test]
        public void 効果テキストが正しくバインドされる()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            CardData data = new CardData("shield_001", "シールド", 1, "防御力を2上げる", 0, 2);

            CardView view = new CardView(template, data);

            Assert.AreEqual("防御力を2上げる", view.Q<Label>("EffectLabel").text);
        }

        [Test]
        public void 攻撃力と防御力が正しくバインドされる()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            CardData data = new CardData("dragon_001", "ドラゴン", 5, "強力なクリーチャー", 4, 3);

            CardView view = new CardView(template, data);

            Assert.AreEqual("ATK 4", view.Q<Label>("AtkLabel").text);
            Assert.AreEqual("DEF 3", view.Q<Label>("DefLabel").text);
        }
    }
}
