using Main.Card;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class CardSOTests
    {
        private static CardDatabase MakeDatabase(CardData[] cards)
        {
            CardDatabase db = ScriptableObject.CreateInstance<CardDatabase>();
            db.Initialize(cards);
            return db;
        }

        [Test]
        public void CharacterCardData_Attackは設定値を返す()
        {
            CharacterCardData card = new CharacterCardData("c1", "戦士", 2, 3);

            Assert.AreEqual(3, card.Attack);
        }

        [Test]
        public void CharacterCardData_Defenseは設定値を返す()
        {
            CharacterCardData card = new CharacterCardData("c1", "戦士", 2, 0, defense: 5);

            Assert.AreEqual(5, card.Defense);
        }

        [Test]
        public void CharacterCardData_Hpは設定値を返す()
        {
            CharacterCardData card = new CharacterCardData("c1", "戦士", 2, 3, hp: 10);

            Assert.AreEqual(10, card.Hp);
        }

        [Test]
        public void CharacterCardData_Hp省略時は0を返す()
        {
            CharacterCardData card = new CharacterCardData("c1", "戦士", 2, 3);

            Assert.AreEqual(0, card.Hp);
        }

        [Test]
        public void EventCardData_Defenseは0を返す()
        {
            EventCardData card = new EventCardData("e1", "回復", 2);

            Assert.AreEqual(0, card.Defense);
        }

        [Test]
        public void EventCardData_Hpは0を返す()
        {
            EventCardData card = new EventCardData("e1", "回復", 2);

            Assert.AreEqual(0, card.Hp);
        }

        [Test]
        public void EventCardData_Attackは0を返す()
        {
            EventCardData card = new EventCardData("e1", "回復", 2);

            Assert.AreEqual(0, card.Attack);
        }

        [Test]
        public void BuildDeck_ID順にカードを返す()
        {
            CardDatabase db = MakeDatabase(new CardData[]
            {
                new CharacterCardData("char_01", "戦士", 2, 0),
                new EventCardData("event_01", "バフ", 1),
            });

            CardData[] deck = db.BuildDeck(new[] { "event_01", "char_01" });

            Assert.AreEqual(2, deck.Length);
            Assert.AreEqual("event_01", deck[0].Id);
            Assert.AreEqual("char_01", deck[1].Id);
        }

        [Test]
        public void BuildDeck_存在しないIDはスキップされる()
        {
            CardDatabase db = MakeDatabase(new CardData[] { new CharacterCardData("char_01", "戦士", 2, 0) });

            CardData[] deck = db.BuildDeck(new[] { "char_01", "unknown_id" });

            Assert.AreEqual(1, deck.Length);
        }

        [Test]
        public void BuildDeck_同じIDを複数回指定すると複数枚入る()
        {
            CardDatabase db = MakeDatabase(new CardData[] { new CharacterCardData("char_01", "戦士", 2, 0) });

            CardData[] deck = db.BuildDeck(new[] { "char_01", "char_01", "char_01" });

            Assert.AreEqual(3, deck.Length);
        }
    }
}
