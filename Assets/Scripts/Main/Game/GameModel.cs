using System.Collections.Generic;
using Main.Card;

namespace Main.Game
{
    public sealed class GameModel
    {
        public TurnPhase Phase { get; private set; } = TurnPhase.Draw;
        public bool IsLocalTurn { get; private set; } = true;

        // 戦闘前2フェーズでローカルプレイヤーが行動する番か
        public bool IsLocalPreparationTurn { get; private set; }

        private readonly List<CardView> _readyQueue = new List<CardView>();
        private int _consecutivePasses;

        private bool _playerCharSetThisTurn;
        private bool _opponentCharSetThisTurn;

        public bool CanPlayerSetChar => !_playerCharSetThisTurn;
        public bool CanOpponentSetChar => !_opponentCharSetThisTurn;

        public void RecordPlayerCharSet() { _playerCharSetThisTurn = true; }
        public void RecordOpponentCharSet() { _opponentCharSetThisTurn = true; }

        public IReadOnlyList<CardView> ReadyQueue => _readyQueue;

        public void SetInitialTurn(bool isLocalFirst) { IsLocalTurn = isLocalFirst; }

        public void BeginCharacterSet() { Phase = TurnPhase.CharacterSet; }

        public void BeginPreBattle2()
        {
            Phase = TurnPhase.PreBattle2;
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

        // 2連続パスで true を返す（戦闘前2フェーズ終了）
        public bool Pass()
        {
            _consecutivePasses++;
            IsLocalPreparationTurn = !IsLocalPreparationTurn;
            return _consecutivePasses >= 2;
        }

        public void BeginBattle() { Phase = TurnPhase.Battle; }

        public void EndTurn()
        {
            _readyQueue.Clear();
            Phase = TurnPhase.Draw;
            _consecutivePasses = 0;
            IsLocalTurn = !IsLocalTurn;
            _playerCharSetThisTurn = false;
            _opponentCharSetThisTurn = false;
        }
    }
}
