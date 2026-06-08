namespace Main.Game
{
    public sealed class GameModel
    {
        public TurnPhase Phase { get; private set; } = TurnPhase.Draw;
        public bool IsLocalTurn { get; private set; } = true;

        public void SetInitialTurn(bool isLocalFirst) { IsLocalTurn = isLocalFirst; }

        public void BeginMain() { Phase = TurnPhase.Main; }

        public void EndTurn()
        {
            Phase = TurnPhase.Draw;
            IsLocalTurn = !IsLocalTurn;
        }
    }
}
