using Main.Card;

namespace Main.Game
{
    // ハート勝利条件のルール判定（純ロジック）
    public static class HeartRule
    {
        public const int InitialHeartCount = 3;

        // 赤属性カードをプレイ（キャラ配置 or イベント使用）するとハートが出現する
        public static bool ActivatesHearts(CardData playedCard)
        {
            return playedCard != null && playedCard.Attribute == CardAttribute.Red;
        }

        // ATK 0 はハートを破壊できない（NO DAMAGE）
        public static bool CanBreakHeart(int attack)
        {
            return attack > 0;
        }
    }
}
