using Main.Card;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Tests.EditMode
{
    public class BanishCharEffectTests
    {
        private static readonly string TemplatePath = "Assets/AddressableAssets/Card/Card.uxml";

        private static CardView MakeCharacter()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            return new CardView(template, new CharacterCardData("C001", "戦士", 2, 3));
        }

        // ApplyEventEffectAsync の BanishChar ロジックをシミュレートする
        private static void SimulateBanishChar(CharacterSlotView targetSlot, GraveyardView targetGraveyard)
        {
            CardView charCard = targetSlot.CurrentCard;
            if (charCard != null)
            {
                targetSlot.RemoveCard();
                targetGraveyard.AddCard(charCard);
            }
        }

        [Test]
        public void BanishCharをEventTypeに指定するとEventTypeがBanishCharになる()
        {
            EventCardData card = new EventCardData("e1", "キャラ除去", 1, EventType.BanishChar, 0);

            Assert.AreEqual(EventType.BanishChar, card.EventType);
        }

        [Test]
        public void キャラがいる場合スロットが空になる()
        {
            CharacterSlotView slot = new CharacterSlotView();
            GraveyardView graveyard = new GraveyardView(null, null);
            slot.PlaceCard(MakeCharacter());

            SimulateBanishChar(slot, graveyard);

            Assert.IsNull(slot.CurrentCard);
        }

        [Test]
        public void キャラがいる場合墓地に1枚追加される()
        {
            CharacterSlotView slot = new CharacterSlotView();
            GraveyardView graveyard = new GraveyardView(null, null);
            slot.PlaceCard(MakeCharacter());

            SimulateBanishChar(slot, graveyard);

            Assert.AreEqual(1, graveyard.Count);
        }

        [Test]
        public void スロットが空の場合墓地枚数は変わらない()
        {
            CharacterSlotView slot = new CharacterSlotView();
            GraveyardView graveyard = new GraveyardView(null, null);

            SimulateBanishChar(slot, graveyard);

            Assert.AreEqual(0, graveyard.Count);
        }

        [Test]
        public void スロットが空の場合スロットは空のまま()
        {
            CharacterSlotView slot = new CharacterSlotView();
            GraveyardView graveyard = new GraveyardView(null, null);

            SimulateBanishChar(slot, graveyard);

            Assert.IsNull(slot.CurrentCard);
        }
    }
}
