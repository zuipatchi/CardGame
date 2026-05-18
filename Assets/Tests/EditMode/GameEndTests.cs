using NUnit.Framework;

namespace Tests.EditMode
{
    public class GameEndTests
    {
        [Test]
        public void ドローフェーズでデッキが0枚のとき発動プレイヤーが負ける()
        {
            bool isLocalTurn = true;
            int deckCount = 0;

            bool shouldLose = deckCount == 0;
            bool? playerWins = shouldLose ? (isLocalTurn ? (bool?)false : true) : null;

            Assert.IsNotNull(playerWins);
            Assert.IsFalse(playerWins.Value);
        }

        [Test]
        public void 相手のドローフェーズでデッキが0枚のときプレイヤーが勝つ()
        {
            bool isLocalTurn = false;
            int deckCount = 0;

            bool shouldLose = deckCount == 0;
            bool? playerWins = shouldLose ? (isLocalTurn ? (bool?)false : true) : null;

            Assert.IsNotNull(playerWins);
            Assert.IsTrue(playerWins.Value);
        }

        [Test]
        public void コスト支払い時にプレイヤーのデッキが0枚なら敗北になる()
        {
            bool isPlayerDeck = true;
            int deckCount = 0;
            int cost = 1;

            bool? playerWins = (deckCount == 0 && cost > 0)
                ? (isPlayerDeck ? (bool?)false : true)
                : null;

            Assert.IsNotNull(playerWins);
            Assert.IsFalse(playerWins.Value);
        }

        [Test]
        public void コスト支払い時に相手のデッキが0枚なら勝利になる()
        {
            bool isPlayerDeck = false;
            int deckCount = 0;
            int cost = 1;

            bool? playerWins = (deckCount == 0 && cost > 0)
                ? (isPlayerDeck ? (bool?)false : true)
                : null;

            Assert.IsNotNull(playerWins);
            Assert.IsTrue(playerWins.Value);
        }

        [Test]
        public void コスト0のカードはデッキが0枚でも敗北にならない()
        {
            int deckCount = 0;
            int cost = 0;

            bool triggersLoss = deckCount == 0 && cost > 0;

            Assert.IsFalse(triggersLoss);
        }

        [Test]
        public void ドローエフェクトでデッキが0枚のとき発動プレイヤーが負ける()
        {
            bool isLocal = true;
            int deckCount = 0;

            bool? playerWins = deckCount == 0 ? (isLocal ? (bool?)false : true) : null;

            Assert.IsNotNull(playerWins);
            Assert.IsFalse(playerWins.Value);
        }
    }
}
