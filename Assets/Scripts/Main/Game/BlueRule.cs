using Main.Card;

namespace Main.Game
{
    // 青属性の勝利条件のルール判定（純ロジック）
    public static class BlueRule
    {
        // 青属性カードをプレイ（キャラ配置 or イベント使用）すると、デッキ0勝利条件が武装される
        public static bool ActivatesBlueWin(CardData playedCard)
        {
            return playedCard != null && playedCard.Attribute == CardAttribute.Blue;
        }

        // 武装済みでデッキが 0 枚になったら、そのプレイヤーの勝利
        public static bool IsBlueWin(bool armed, int deckCount)
        {
            return armed && deckCount == 0;
        }
    }
}
