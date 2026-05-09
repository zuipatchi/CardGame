using System;
using System.Collections.Generic;
using Main.Card;

namespace Main.Game
{
    public sealed class GameModel
    {
        public event Action<CardView, PendingAction> OnResolve;
        public event Action<bool> OnTurnChanged;

        public bool IsLocalTurn { get; private set; } = true;

        private readonly List<(CardView card, PendingAction action)> _readyCards = new List<(CardView card, PendingAction action)>();

        public void DoAction(CardView actor, PendingAction action)
        {
            List<(CardView card, PendingAction action)> toResolve = new List<(CardView card, PendingAction action)>(_readyCards);
            _readyCards.Clear();

            actor.SetState(CardState.Ready);
            _readyCards.Add((actor, action));

            foreach ((CardView card, PendingAction act) in toResolve)
            {
                card.SetState(CardState.Resolve);
                OnResolve?.Invoke(card, act);
                card.SetState(CardState.Normal);
            }

            IsLocalTurn = !IsLocalTurn;
            OnTurnChanged?.Invoke(IsLocalTurn);
        }
    }
}
