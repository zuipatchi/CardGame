namespace Main.Game
{
    public sealed class GameModel
    {
        public TurnPhase Phase { get; private set; } = TurnPhase.Draw;
        public bool IsLocalTurn { get; private set; } = true;

        // ゲーム開始からの通算ターン数（1始まり）。ターン1は必ず先攻の初手。
        public int TurnNumber { get; private set; } = 1;

        public void SetInitialTurn(bool isLocalFirst) { IsLocalTurn = isLocalFirst; }

        public void BeginMain() { Phase = TurnPhase.Main; }

        public void EndTurn()
        {
            Phase = TurnPhase.Draw;
            IsLocalTurn = !IsLocalTurn;
            TurnNumber++;
        }
    }
}
