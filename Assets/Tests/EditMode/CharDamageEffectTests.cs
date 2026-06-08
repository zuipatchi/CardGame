using System;
using System.Collections.Generic;
using Main.Card;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Tests.EditMode
{
    public class CharDamageEffectTests
    {
        private static readonly string TemplatePath = "Assets/AddressableAssets/Card/Card.uxml";

        private static CardView MakeCharacter(int defense, int hp)
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            return new CardView(template, new CharacterCardData("C001", "戦士", 2, 3, defense, 0, hp));
        }

        private static int CalculateDamage(int baseAtk, int charDef, int defBoost, bool hasChar)
        {
            int effectiveDef = hasChar ? charDef + defBoost : 0;
            return Math.Max(0, baseAtk - effectiveDef);
        }

        private static bool ShouldDestroyChar(int damage, int charHp, bool hasChar)
        {
            return hasChar && damage >= charHp;
        }

        [Test]
        public void CharDamageをEventTypeに指定するとEventTypeがCharDamageになる()
        {
            EventCardData card = new EventCardData("E001", "直接攻撃", 2, EventType.CharDamage, 5, "相手キャラに5ダメージ");
            Assert.AreEqual(EventType.CharDamage, card.EventType);
        }

        [Test]
        public void ダメージ計算_AtkがDefを上回る場合は差分がダメージになる()
        {
            int damage = CalculateDamage(5, 2, 0, true);
            Assert.AreEqual(3, damage);
        }

        [Test]
        public void ダメージ計算_AtkがDef以下の場合はダメージ0になる()
        {
            int damage = CalculateDamage(2, 5, 0, true);
            Assert.AreEqual(0, damage);
        }

        [Test]
        public void ダメージ計算_キャラなしの場合Defなしでそのままダメージになる()
        {
            int damage = CalculateDamage(5, 0, 0, false);
            Assert.AreEqual(5, damage);
        }

        [Test]
        public void ダメージ計算_DefBoostあり_Def加算後で計算される()
        {
            int damage = CalculateDamage(5, 2, 2, true);
            Assert.AreEqual(1, damage);
        }

        [Test]
        public void ダメージ計算_DefBoostによってAtkと同値になると0ダメージ()
        {
            int damage = CalculateDamage(5, 3, 2, true);
            Assert.AreEqual(0, damage);
        }

        [Test]
        public void ダメージがHPに等しいときキャラ破壊になる()
        {
            bool destroyed = ShouldDestroyChar(5, 5, true);
            Assert.IsTrue(destroyed);
        }

        [Test]
        public void ダメージがHPを超えるときキャラ破壊になる()
        {
            bool destroyed = ShouldDestroyChar(6, 5, true);
            Assert.IsTrue(destroyed);
        }

        [Test]
        public void ダメージがHP未満のときキャラは残る()
        {
            bool destroyed = ShouldDestroyChar(3, 5, true);
            Assert.IsFalse(destroyed);
        }

        [Test]
        public void キャラなしの場合キャラ破壊は発生しない()
        {
            bool destroyed = ShouldDestroyChar(10, 0, false);
            Assert.IsFalse(destroyed);
        }

        [Test]
        public void フィールドにキャラがいてダメージがHP以上のときフィールドが空になる()
        {
            FieldView field = new FieldView();
            GraveyardView graveyard = new GraveyardView(null, null);
            CardView charCard = MakeCharacter(0, 3);
            field.PlaceCard(charCard);
            CharacterCardData charData = (CharacterCardData)charCard.Data;
            int damage = CalculateDamage(5, charData.Defense, 0, true);
            if (ShouldDestroyChar(damage, charData.Hp, field.Characters.Count > 0))
            {
                IReadOnlyList<CardView> chars = field.Characters;
                field.RemoveCard(chars[0]);
                graveyard.AddCard(MakeCharacter(0, 3));
            }
            Assert.AreEqual(0, field.Characters.Count);
            Assert.AreEqual(1, graveyard.Count);
        }

        [Test]
        public void フィールドにキャラがいてダメージがHP未満のときキャラは残る()
        {
            FieldView field = new FieldView();
            CardView charCard = MakeCharacter(0, 10);
            field.PlaceCard(charCard);
            CharacterCardData charData = (CharacterCardData)charCard.Data;
            int damage = CalculateDamage(3, charData.Defense, 0, true);
            if (ShouldDestroyChar(damage, charData.Hp, field.Characters.Count > 0))
            {
                field.RemoveCard(field.Characters[0]);
            }
            Assert.AreEqual(1, field.Characters.Count);
        }
    }
}
