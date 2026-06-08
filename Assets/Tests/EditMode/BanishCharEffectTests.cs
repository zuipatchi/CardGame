using System.Collections.Generic;
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
        private static void SimulateBanishChar(FieldView targetField, GraveyardView targetGraveyard)
        {
            IReadOnlyList<CardView> chars = targetField.Characters;
            if (chars.Count > 0)
            {
                CardView charCard = chars[0];
                targetField.RemoveCard(charCard);
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
        public void キャラがいる場合フィールドが空になる()
        {
            FieldView field = new FieldView();
            GraveyardView graveyard = new GraveyardView(null, null);
            field.PlaceCard(MakeCharacter());

            SimulateBanishChar(field, graveyard);

            Assert.AreEqual(0, field.Characters.Count);
        }

        [Test]
        public void キャラがいる場合墓地に1枚追加される()
        {
            FieldView field = new FieldView();
            GraveyardView graveyard = new GraveyardView(null, null);
            field.PlaceCard(MakeCharacter());

            SimulateBanishChar(field, graveyard);

            Assert.AreEqual(1, graveyard.Count);
        }

        [Test]
        public void フィールドが空の場合墓地枚数は変わらない()
        {
            FieldView field = new FieldView();
            GraveyardView graveyard = new GraveyardView(null, null);

            SimulateBanishChar(field, graveyard);

            Assert.AreEqual(0, graveyard.Count);
        }

        [Test]
        public void フィールドが空の場合キャラは0枚のまま()
        {
            FieldView field = new FieldView();
            GraveyardView graveyard = new GraveyardView(null, null);

            SimulateBanishChar(field, graveyard);

            Assert.AreEqual(0, field.Characters.Count);
        }
    }
}
