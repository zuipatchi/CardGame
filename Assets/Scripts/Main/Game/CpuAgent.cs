using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using UnityEngine.UIElements;
using VContainer;

namespace Main.Game
{
    public enum CpuDecisionType { PlayCard, AttackField, AttackDeck }

    public readonly struct CpuDecision
    {
        public CpuDecisionType Type { get; }
        public int AttackerIndex { get; }
        public int FieldTargetIndex { get; }
        public int HandCardIndex { get; }

        private CpuDecision(CpuDecisionType type, int attackerIndex, int fieldTargetIndex, int handCardIndex)
        {
            Type = type;
            AttackerIndex = attackerIndex;
            FieldTargetIndex = fieldTargetIndex;
            HandCardIndex = handCardIndex;
        }

        public static CpuDecision MakePlayCard(int handIndex) =>
            new CpuDecision(CpuDecisionType.PlayCard, 0, 0, handIndex);

        public static CpuDecision MakeAttackField(int attackerIndex, int targetIndex) =>
            new CpuDecision(CpuDecisionType.AttackField, attackerIndex, targetIndex, 0);

        public static CpuDecision MakeAttackDeck(int attackerIndex) =>
            new CpuDecision(CpuDecisionType.AttackDeck, attackerIndex, 0, 0);
    }

    public sealed class CpuAgent
    {
        private readonly GameModel _gameModel;

        [Inject]
        public CpuAgent(GameModel gameModel)
        {
            _gameModel = gameModel;
        }

        public async UniTask TakeTurnAsync(
            HandView cpuHandView,
            FieldView cpuFieldView,
            FieldView playerFieldView,
            DeckView playerDeckView,
            VisualElement dragLayer,
            IDictionary<CardView, ArrowView> pendingArrows,
            CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(1f), cancellationToken: ct);

            IReadOnlyList<CardView> cpuFieldCards = cpuFieldView.Cards;
            IReadOnlyList<CardView> playerFieldCards = playerFieldView.Cards;
            IReadOnlyList<CardView> cpuHandCards = cpuHandView.Cards;

            List<CardData> cpuFieldData = new List<CardData>(cpuFieldCards.Count);
            foreach (CardView c in cpuFieldCards)
            {
                cpuFieldData.Add(c.Data);
            }

            List<CardData> playerFieldData = new List<CardData>(playerFieldCards.Count);
            foreach (CardView c in playerFieldCards)
            {
                playerFieldData.Add(c.Data);
            }

            if (!TryDecide(cpuFieldData, playerFieldData, cpuHandCards.Count, out CpuDecision decision))
            {
                return;
            }

            switch (decision.Type)
            {
                case CpuDecisionType.AttackField:
                {
                    CardView attacker = cpuFieldCards[decision.AttackerIndex];
                    CardView target = playerFieldCards[decision.FieldTargetIndex];
                    pendingArrows[attacker] = CreateArrow(attacker, target.worldBound.center, dragLayer);
                    await _gameModel.DoAction(attacker, new AttackAction(target));
                    break;
                }
                case CpuDecisionType.AttackDeck:
                {
                    CardView attacker = cpuFieldCards[decision.AttackerIndex];
                    pendingArrows[attacker] = CreateArrow(attacker, playerDeckView.worldBound.center, dragLayer);
                    await _gameModel.DoAction(attacker, new DeckAttackAction(playerDeckView));
                    break;
                }
                case CpuDecisionType.PlayCard:
                {
                    CardView card = cpuHandCards[decision.HandCardIndex];
                    cpuHandView.RemoveCard(card);
                    cpuFieldView.PlaceCard(card);
                    await card.FlipAsync(ct);
                    await _gameModel.DoAction(card, new PlayCardAction());
                    break;
                }
            }
        }

        private static ArrowView CreateArrow(CardView attacker, UnityEngine.Vector2 targetCenter, VisualElement dragLayer)
        {
            ArrowView arrow = new ArrowView();
            arrow.StartPoint = attacker.worldBound.center;
            arrow.EndPoint = targetCenter;
            dragLayer.Add(arrow);
            return arrow;
        }

        public static bool TryDecide(
            IReadOnlyList<CardData> cpuFieldCards,
            IReadOnlyList<CardData> playerFieldCards,
            int cpuHandCount,
            out CpuDecision decision)
        {
            if (cpuFieldCards.Count > 0)
            {
                for (int i = 0; i < cpuFieldCards.Count; i++)
                {
                    for (int j = 0; j < playerFieldCards.Count; j++)
                    {
                        if (cpuFieldCards[i].Attack >= playerFieldCards[j].Defense)
                        {
                            decision = CpuDecision.MakeAttackField(i, j);
                            return true;
                        }
                    }
                }

                int strongestIdx = 0;
                for (int i = 1; i < cpuFieldCards.Count; i++)
                {
                    if (cpuFieldCards[i].Attack > cpuFieldCards[strongestIdx].Attack)
                    {
                        strongestIdx = i;
                    }
                }

                decision = CpuDecision.MakeAttackDeck(strongestIdx);
                return true;
            }

            if (cpuHandCount > 0)
            {
                decision = CpuDecision.MakePlayCard(0);
                return true;
            }

            decision = default;
            return false;
        }
    }
}
