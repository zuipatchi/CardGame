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
            EventCardData data = new EventCardData("e1", "ファイアボール", 3);

            CardView view = new CardView(template, data);

            Assert.AreEqual("ファイアボール", view.Q<Label>("NameLabel").text);
        }

        [Test]
        public void コストが正しくバインドされる()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            EventCardData data = new EventCardData("e1", "アイスランス", 2);

            CardView view = new CardView(template, data);

            Assert.AreEqual("2", view.Q<Label>("CostLabel").text);
        }

        [Test]
        public void スキルカードの攻撃力が正しくバインドされる()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            SkillCardData data = new SkillCardData("s1", "ファイア", 1, damage: 4);

            CardView view = new CardView(template, data);

            Assert.AreEqual("4", view.Q<Label>("AtkLabel").text);
        }

        [Test]
        public void キャラカードの防御力が正しくバインドされる()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            CharacterCardData data = new CharacterCardData("c1", "戦士", 2, defense: 3);

            CardView view = new CardView(template, data);

            Assert.AreEqual("3", view.Q<Label>("DefLabel").text);
        }
    }
}
