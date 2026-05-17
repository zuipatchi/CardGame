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
            CharacterCardData data = new CharacterCardData("c1", "戦士", 2, 0, 3);

            CardView view = new CardView(template, data);

            Assert.AreEqual("3", view.Q<Label>("DefLabel").text);
        }

        [Test]
        public void SetChainNumber_1でラベルが表示され丸1になる()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            EventCardData data = new EventCardData("e1", "テスト", 0);
            CardView view = new CardView(template, data);

            view.SetChainNumber(1);

            Label label = view.Q<Label>("ChainLabel");
            Assert.AreEqual(DisplayStyle.Flex, label.style.display.value);
            Assert.AreEqual("①", label.text);
        }

        [Test]
        public void SetChainNumber_9でラベルが丸9になる()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            EventCardData data = new EventCardData("e1", "テスト", 0);
            CardView view = new CardView(template, data);

            view.SetChainNumber(9);

            Assert.AreEqual("⑨", view.Q<Label>("ChainLabel").text);
        }

        [Test]
        public void SetChainNumber_0でラベルが非表示になる()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            EventCardData data = new EventCardData("e1", "テスト", 0);
            CardView view = new CardView(template, data);

            view.SetChainNumber(3);
            view.SetChainNumber(0);

            Assert.AreEqual(DisplayStyle.None, view.Q<Label>("ChainLabel").style.display.value);
        }

        [Test]
        public void FaceUp_裏向きカードが表向きになる()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            CharacterCardData data = new CharacterCardData("c1", "戦士", 1, 0, 2);
            CardView view = new CardView(template, data, faceDown: true);

            view.FaceUp();

            Assert.IsFalse(view.IsFaceDown);
            Assert.AreEqual(DisplayStyle.Flex, view.Q<VisualElement>("FrontFace").style.display.value);
            Assert.AreEqual(DisplayStyle.None, view.Q<VisualElement>("BackFace").style.display.value);
        }

        [Test]
        public void FaceUp_すでに表向きのカードは変化しない()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            CharacterCardData data = new CharacterCardData("c1", "戦士", 1, 0, 2);
            CardView view = new CardView(template, data, faceDown: false);

            view.FaceUp();

            Assert.IsFalse(view.IsFaceDown);
            Assert.AreEqual(DisplayStyle.Flex, view.Q<VisualElement>("FrontFace").style.display.value);
        }
    }
}
