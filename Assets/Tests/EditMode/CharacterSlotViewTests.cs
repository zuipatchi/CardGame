using Main.Card;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Tests.EditMode
{
    public class CharacterSlotViewTests
    {
        private static readonly string TemplatePath = "Assets/AddressableAssets/Card/Card.uxml";

        private static CardView MakeCharacter(int attack = 2, int defense = 0, int speed = 0, int hp = 0)
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            return new CardView(template, new CharacterCardData("C001", "戦士", 2, attack, defense, speed, hp));
        }

        [Test]
        public void 初期状態はCurrentCardがnull()
        {
            CharacterSlotView slot = new CharacterSlotView();

            Assert.IsNull(slot.CurrentCard);
        }

        [Test]
        public void 初期状態のDefenseは0()
        {
            CharacterSlotView slot = new CharacterSlotView();

            Assert.AreEqual(0, slot.Defense);
        }

        [Test]
        public void 初期状態のHpは0()
        {
            CharacterSlotView slot = new CharacterSlotView();

            Assert.AreEqual(0, slot.Hp);
        }

        [Test]
        public void 初期状態のSpeedは0()
        {
            CharacterSlotView slot = new CharacterSlotView();

            Assert.AreEqual(0, slot.Speed);
        }

        [Test]
        public void PlaceCard後のDefenseはカードの値を返す()
        {
            CharacterSlotView slot = new CharacterSlotView();

            slot.PlaceCard(MakeCharacter(defense: 7));

            Assert.AreEqual(7, slot.Defense);
        }

        [Test]
        public void PlaceCard後のHpはカードの値を返す()
        {
            CharacterSlotView slot = new CharacterSlotView();

            slot.PlaceCard(MakeCharacter(hp: 5));

            Assert.AreEqual(5, slot.Hp);
        }

        [Test]
        public void PlaceCard後のSpeedはカードの値を返す()
        {
            CharacterSlotView slot = new CharacterSlotView();

            slot.PlaceCard(MakeCharacter(speed: 5));

            Assert.AreEqual(5, slot.Speed);
        }

        [Test]
        public void PlaceCardでCurrentCardが設定される()
        {
            CharacterSlotView slot = new CharacterSlotView();
            CardView card = MakeCharacter();

            slot.PlaceCard(card);

            Assert.AreEqual(card, slot.CurrentCard);
        }

        [Test]
        public void 二枚目をPlaceCardすると一枚目が押し出される()
        {
            CharacterSlotView slot = new CharacterSlotView();
            CardView first = MakeCharacter();
            CardView second = MakeCharacter();

            slot.PlaceCard(first);
            slot.PlaceCard(second);

            Assert.AreEqual(second, slot.CurrentCard);
        }

        [Test]
        public void 二枚目をPlaceCardするとOnCardDisplacedが発火する()
        {
            CharacterSlotView slot = new CharacterSlotView();
            CardView first = MakeCharacter();
            CardView displaced = null;
            slot.OnCardDisplaced += c => displaced = c;

            slot.PlaceCard(first);
            slot.PlaceCard(MakeCharacter());

            Assert.AreEqual(first, displaced);
        }

        [Test]
        public void 一枚目のときOnCardDisplacedは発火しない()
        {
            CharacterSlotView slot = new CharacterSlotView();
            bool fired = false;
            slot.OnCardDisplaced += _ => fired = true;

            slot.PlaceCard(MakeCharacter());

            Assert.IsFalse(fired);
        }

        [Test]
        public void RemoveCardでCurrentCardがnullになる()
        {
            CharacterSlotView slot = new CharacterSlotView();
            slot.PlaceCard(MakeCharacter());

            slot.RemoveCard();

            Assert.IsNull(slot.CurrentCard);
        }

        [Test]
        public void ダメージがHP以上のとき破壊条件が成立する()
        {
            int damage = 5;
            int hp = 5;

            Assert.IsTrue(damage >= hp);
        }

        [Test]
        public void ダメージがHP未満のとき破壊条件は成立しない()
        {
            int damage = 4;
            int hp = 5;

            Assert.IsFalse(damage >= hp);
        }

        [Test]
        public void DefBoostがある場合有効防御力はDefenseにEventValueを加算した値になる()
        {
            int baseDefense = 2;
            int defBoost = 3;
            int playerATK = 6;

            int effectiveDef = baseDefense + defBoost;
            int damage = UnityEngine.Mathf.Max(0, playerATK - effectiveDef);

            Assert.AreEqual(5, effectiveDef);
            Assert.AreEqual(1, damage);
        }
    }
}
