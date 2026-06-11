using Main.Card;

namespace Main.Game
{
    // 緑属性の勝利条件のルール判定（純ロジック）
    public static class GreenRule
    {
        // この勝利点に到達したら勝利
        public const int WinPoints = 20;

        // 緑属性カードをプレイ（キャラ配置 or イベント使用）すると、勝利点表示が出現する
        public static bool ActivatesVictoryPoints(CardData playedCard)
        {
            return playedCard != null && playedCard.Attribute == CardAttribute.Green;
        }

        // 勝利点が WinPoints 以上になったら勝利
        public static bool IsGreenWin(int points)
        {
            return points >= WinPoints;
        }
    }
}
