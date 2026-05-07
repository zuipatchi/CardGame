using Main.Card;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class CardDatabaseTests
    {
        private CardDatabase CreateDatabase(CardData[] cards)
        {
            CardDatabase db = ScriptableObject.CreateInstance<CardDatabase>();
            db.Initialize(cards);
            return db;
        }

        [Test]
        public void 登録したIDでCardDataを取得できる()
        {
            CardDatabase db = CreateDatabase(new[]
            {
                new CardData("fire_001", "ファイアボール", 3, 0, 0),
            });

            bool found = db.TryGet("fire_001", out CardData result);

            Assert.IsTrue(found);
            Assert.AreEqual("ファイアボール", result.CardName);
        }

        [Test]
        public void 未登録のIDはTryGetでfalseを返す()
        {
            CardDatabase db = CreateDatabase(System.Array.Empty<CardData>());

            bool found = db.TryGet("unknown", out CardData _);

            Assert.IsFalse(found);
        }

        [Test]
        public void インデクサで登録済みカードを取得できる()
        {
            CardDatabase db = CreateDatabase(new[]
            {
                new CardData("ice_001", "アイスランス", 2, 0, 0),
            });

            CardData result = db["ice_001"];

            Assert.AreEqual("アイスランス", result.CardName);
        }

        [Test]
        public void 複数カードをそれぞれIDで取得できる()
        {
            CardDatabase db = CreateDatabase(new[]
            {
                new CardData("fire_001", "ファイアボール", 3, 0, 0),
                new CardData("shield_001", "シールド", 1, 0, 2),
            });

            db.TryGet("fire_001", out CardData r1);
            db.TryGet("shield_001", out CardData r2);

            Assert.AreEqual("ファイアボール", r1.CardName);
            Assert.AreEqual("シールド", r2.CardName);
        }
    }
}
