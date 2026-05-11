using Main.Card;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Tests.EditMode
{
    public class CharacterSlotViewTests
    {
        private static readonly string TemplatePath = "Assets/AddressableAssets/Card/Card.uxml";

        private static CardView MakeCharacter(int defense = 3)
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            return new CardView(template, new CharacterCardData("C001", "戦士", 2, defense));
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
        public void PlaceCardでCurrentCardが設定される()
        {
            CharacterSlotView slot = new CharacterSlotView();
            CardView card = MakeCharacter(defense: 5);

            slot.PlaceCard(card);

            Assert.AreEqual(card, slot.CurrentCard);
        }

        [Test]
        public void PlaceCard後のDefenseはカードの値を返す()
        {
            CharacterSlotView slot = new CharacterSlotView();

            slot.PlaceCard(MakeCharacter(defense: 7));

            Assert.AreEqual(7, slot.Defense);
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
    }
}
