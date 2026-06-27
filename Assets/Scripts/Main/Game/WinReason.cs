namespace Main.Game
{
    // ゲーム共通の勝利条件の種別（属性に依らない2条件）。勝敗演出の勝因表示に使う。
    public enum WinReason
    {
        DeckOut,        // 相手がデッキを引き切って0枚になった（デッキ切れ）
        VictoryPoints,  // 勝利点が規定値（WinRule.VictoryPointsToWin）に到達した
        HandCollection, // HandCollectionWin 効果で勝利条件カードを手札にそろえた（タロー勝利）
    }
}
