namespace Main.Game
{
    // ゲーム共通の勝利条件（属性に依らない3条件）の判定ロジックと定数（純ロジック）。
    // 1. デッキ切れ: あるプレイヤーがデッキを引き切って0枚になると、その相手が勝利
    // 2. 勝利点: 勝利点が VictoryPointsToWin 以上になった側が勝利
    // 3. フィールドのキャラ: 自フィールドに FieldCharsToWin 体のキャラを同時に並べた側が勝利
    public static class WinRule
    {
        // 勝利点がこの値以上になったら勝利
        public const int VictoryPointsToWin = 20;

        // 自フィールドにこの数のキャラを同時に並べたら勝利
        public const int FieldCharsToWin = 8;

        // 勝利点が規定値に到達したか
        public static bool IsVictoryPointsWin(int points)
        {
            return points >= VictoryPointsToWin;
        }

        // フィールドのキャラ数が規定値に到達したか
        public static bool IsFieldCharsWin(int characterCount)
        {
            return characterCount >= FieldCharsToWin;
        }

        // デッキを引き切った（0枚になった）か。引き切ったプレイヤーは敗北する
        public static bool IsDeckOut(int deckCount)
        {
            return deckCount <= 0;
        }
    }
}
