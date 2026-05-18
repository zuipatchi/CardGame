using Main.Card;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Tests.EditMode
{
    public sealed class CardDetailModalTests
    {
        private VisualElement _root;
        private CardDetailModal _modal;

        [SetUp]
        public void SetUp()
        {
            _root = new VisualElement();
            _modal = new CardDetailModal(_root);
        }

        [Test]
        public void キャラクターカードを表示するとオーバーレイが追加される()
        {
            CharacterCardData data = new CharacterCardData("C001", "竜騎士", 3, 5, 4);

            _modal.Show(data);

            Assert.AreEqual(1, _root.childCount);
            VisualElement overlay = _root.Q<VisualElement>(className: "card-detail-overlay");
            Assert.IsNotNull(overlay);
        }

        [Test]
        public void キャラクターカードのDEFが表示される()
        {
            CharacterCardData data = new CharacterCardData("C001", "竜騎士", 3, 5, 4);

            _modal.Show(data);

            System.Collections.Generic.List<Label> valueLabels = new System.Collections.Generic.List<Label>();
            _root.Query<Label>(className: "card-detail-row-value").ToList(valueLabels);
            Assert.AreEqual(2, valueLabels.Count, "コスト・DEF の2行");
            Assert.AreEqual("3", valueLabels[0].text);
            Assert.AreEqual("4", valueLabels[1].text);
        }

        [Test]
        public void 技カードのダメージが表示される()
        {
            SkillCardData data = new SkillCardData("S001", "ファイア", 2, 6);

            _modal.Show(data);

            System.Collections.Generic.List<Label> valueLabels = new System.Collections.Generic.List<Label>();
            _root.Query<Label>(className: "card-detail-row-value").ToList(valueLabels);
            Assert.AreEqual(2, valueLabels.Count, "コスト・ダメージの2行");
            Assert.AreEqual("6", valueLabels[1].text);
        }

        [Test]
        public void イベントカードのAtkBoost効果がフォーマットされる()
        {
            EventCardData data = new EventCardData("E001", "ATKブースト", 1, EffectType.AtkBoost, 3);

            _modal.Show(data);

            System.Collections.Generic.List<Label> valueLabels = new System.Collections.Generic.List<Label>();
            _root.Query<Label>(className: "card-detail-row-value").ToList(valueLabels);
            Assert.AreEqual("ATK Boost +3", valueLabels[1].text);
        }

        [Test]
        public void イベントカードのDraw効果がフォーマットされる()
        {
            EventCardData data = new EventCardData("E002", "ドロー", 0, EffectType.Draw, 2);

            _modal.Show(data);

            System.Collections.Generic.List<Label> valueLabels = new System.Collections.Generic.List<Label>();
            _root.Query<Label>(className: "card-detail-row-value").ToList(valueLabels);
            Assert.AreEqual("Draw ×2", valueLabels[1].text);
        }

        [Test]
        public void Hideを呼ぶとオーバーレイが削除される()
        {
            CharacterCardData data = new CharacterCardData("C001", "竜騎士", 3, 5, 4);
            _modal.Show(data);

            _modal.Hide();

            Assert.AreEqual(0, _root.childCount);
        }

        [Test]
        public void Showを2回呼ぶと以前のモーダルが閉じて新しいモーダルが表示される()
        {
            CharacterCardData data1 = new CharacterCardData("C001", "竜騎士", 3, 5, 4);
            CharacterCardData data2 = new CharacterCardData("C002", "炎の魔法使い", 2, 3, 2);

            _modal.Show(data1);
            _modal.Show(data2);

            Assert.AreEqual(1, _root.childCount);
            System.Collections.Generic.List<Label> nameLabels = new System.Collections.Generic.List<Label>();
            _root.Query<Label>(className: "card-detail-name").ToList(nameLabels);
            Assert.AreEqual("炎の魔法使い", nameLabels[0].text);
        }
    }
}
