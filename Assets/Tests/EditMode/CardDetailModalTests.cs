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
            CharacterCardData data = new CharacterCardData("C001", "竜騎士", 3, 5);

            _modal.Show(data);

            Assert.AreEqual(1, _root.childCount);
            VisualElement overlay = _root.Q<VisualElement>(className: "card-detail-overlay");
            Assert.IsNotNull(overlay);
        }

        [Test]
        public void キャラクターカードのコストが表示される()
        {
            CharacterCardData data = new CharacterCardData("C001", "竜騎士", 3, 5);

            _modal.Show(data);

            System.Collections.Generic.List<Label> valueLabels = new System.Collections.Generic.List<Label>();
            _root.Query<Label>(className: "card-detail-row-value").ToList(valueLabels);
            Assert.AreEqual(4, valueLabels.Count, "コスト・体力・攻撃力・防御の4行");
            Assert.AreEqual("3", valueLabels[0].text);
        }

        [Test]
        public void イベントカードの説明テキストが表示される()
        {
            EventCardData data = new EventCardData("E003", "強化の書", 1, EventType.AtkBoost, 2, "次の戦闘で攻撃力を強化する");

            _modal.Show(data);

            System.Collections.Generic.List<Label> descLabels = new System.Collections.Generic.List<Label>();
            _root.Query<Label>(className: "card-detail-description").ToList(descLabels);
            Assert.AreEqual(1, descLabels.Count);
            Assert.AreEqual("次の戦闘で攻撃力を強化する", descLabels[0].text);
        }

        [Test]
        public void イベントカードの説明テキストが空のとき説明ラベルは表示されない()
        {
            EventCardData data = new EventCardData("E001", "ATKブースト", 1, EventType.AtkBoost, 3);

            _modal.Show(data);

            System.Collections.Generic.List<Label> descLabels = new System.Collections.Generic.List<Label>();
            _root.Query<Label>(className: "card-detail-description").ToList(descLabels);
            Assert.AreEqual(0, descLabels.Count);
        }

        [Test]
        public void Hideを呼ぶとオーバーレイが削除される()
        {
            CharacterCardData data = new CharacterCardData("C001", "竜騎士", 3, 5);
            _modal.Show(data);

            _modal.Hide();

            Assert.AreEqual(0, _root.childCount);
        }

        [Test]
        public void Showを2回呼ぶと以前のモーダルが閉じて新しいモーダルが表示される()
        {
            CharacterCardData data1 = new CharacterCardData("C001", "竜騎士", 3, 5);
            CharacterCardData data2 = new CharacterCardData("C002", "炎の魔法使い", 2, 3);

            _modal.Show(data1);
            _modal.Show(data2);

            Assert.AreEqual(1, _root.childCount);
            System.Collections.Generic.List<Label> nameLabels = new System.Collections.Generic.List<Label>();
            _root.Query<Label>(className: "card-detail-name").ToList(nameLabels);
            Assert.AreEqual("炎の魔法使い", nameLabels[0].text);
        }
    }
}
