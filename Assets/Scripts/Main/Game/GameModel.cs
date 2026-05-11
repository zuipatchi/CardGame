using System.Collections.Generic;
using Main.Card;

namespace Main.Game
{
    public sealed class GameModel
    {
        public TurnPhase Phase { get; private set; } = TurnPhase.Draw;
        public bool IsLocalTurn { get; private set; } = true;

        // 準備フェーズでローカルプレイヤーが行動する番か
        public bool IsLocalPreparationTurn { get; private set; }

        private readonly List<CardView> _readyQueue = new List<CardView>();
        private int _consecutivePasses;

        public IReadOnlyList<CardView> ReadyQueue => _readyQueue;

        public void BeginPreparation()
        {
            Phase = TurnPhase.Preparation;
            IsLocalPreparationTurn = IsLocalTurn;
            _consecutivePasses = 0;
        }

        public void ReadyCard(CardView card)
        {
            _consecutivePasses = 0;
            card.SetState(CardState.Ready);
            _readyQueue.Add(card);
            IsLocalPreparationTurn = !IsLocalPreparationTurn;
        }

        // 2連続パスで true を返す（準備フェーズ終了）
        public bool Pass()
        {
            _consecutivePasses++;
            IsLocalPreparationTurn = !IsLocalPreparationTurn;
            return _consecutivePasses >= 2;
        }

        public void BeginResolution() { Phase = TurnPhase.Resolution; }
        public void BeginPreBattle() { Phase = TurnPhase.PreBattle; }
        public void BeginBattle() { Phase = TurnPhase.Battle; }

        public void EndTurn()
        {
            _readyQueue.Clear();
            IsLocalTurn = !IsLocalTurn;
            Phase = TurnPhase.Draw;
            _consecutivePasses = 0;
        }
    }
}
