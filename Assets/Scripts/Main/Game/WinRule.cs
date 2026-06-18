namespace Main.Game
{
    // ゲーム共通の勝利条件（属性に依らない3条件）の判定ロジックと定数（純ロジック）。
    // 1. デッキ切れ（オーバーリミット）: デッキが0枚の状態でカードを引く／ミルしようとした瞬間に、その本人が敗北＝相手が勝利。
    //    デッキを0枚にした引き／ミルそのものでは負けず、その後さらに引く／ミルする手番で初めて敗北する。
    //    ターン最初のドローは例外で、デッキ枚数分だけ引いて止まり（0枚でも0枚引く）、決して敗北しない。
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

        // デッキが空（0枚）か。空デッキからカードを引く／ミルしようとすると、そのプレイヤーは敗北する（オーバーリミット）
        public static bool IsDeckOut(int deckCount)
        {
            return deckCount <= 0;
        }
    }
}
