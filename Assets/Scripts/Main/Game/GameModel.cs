using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Main.Card;

namespace Main.Game
{
    public sealed class GameModel
    {
        public event Action<CardView, PendingAction> OnResolve;
        public event Action<bool> OnTurnChanged;
        public Func<UniTask> OnResolvePhaseStartAsync;
        public Func<CardView, PendingAction, UniTask> OnResolveAsync;
        public Func<bool, UniTask> OnTurnStartAsync;

        public bool IsLocalTurn { get; private set; } = true;

        private readonly List<(CardView card, PendingAction action)> _readyCards = new List<(CardView card, PendingAction action)>();

        public async UniTask DoAction(CardView actor, PendingAction action)
        {
            List<(CardView card, PendingAction action)> toResolve = new List<(CardView card, PendingAction action)>(_readyCards);
            _readyCards.Clear();

            actor.SetState(CardState.Ready);
            _readyCards.Add((actor, action));

            UniTask resolveOverlayTask = OnResolvePhaseStartAsync != null
                ? OnResolvePhaseStartAsync.Invoke()
                : UniTask.CompletedTask;

            foreach ((CardView card, PendingAction act) in toResolve)
            {
                card.SetState(CardState.Resolve);
                if (OnResolveAsync != null)
                {
                    await OnResolveAsync.Invoke(card, act);
                }
                OnResolve?.Invoke(card, act);
                card.SetState(CardState.Normal);
            }

            await resolveOverlayTask;

            IsLocalTurn = !IsLocalTurn;
            if (OnTurnStartAsync != null)
            {
                await OnTurnStartAsync.Invoke(IsLocalTurn);
            }
            OnTurnChanged?.Invoke(IsLocalTurn);
        }
    }
}
