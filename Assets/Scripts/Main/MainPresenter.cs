using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using Main.Game;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace Main
{
    public sealed class MainPresenter : MonoBehaviour, IStartable
    {
        private const int InitialHandSize = 5;
        private const float DrawStagger = 0.12f;

        private CardStore _cardStore;
        private CardDatabase _cardDatabase;
        private GameModel _gameModel;

        private HandView _handView;
        private FieldView _playerFieldView;
        private FieldView _opponentFieldView;
        private DeckView _opponentDeckView;
        private GraveyardView _playerGraveyardView;
        private GraveyardView _opponentGraveyardView;
        private VisualElement _actionButtonsArea;
        private readonly Dictionary<CardView, AttackArrowManipulator> _attackManipulators = new Dictionary<CardView, AttackArrowManipulator>();

        private CardView _stagingCard;
        private PendingAction _stagedAction;

        [Inject]
        public void Construct(CardStore cardStore, CardDatabase cardDatabase, GameModel gameModel)
        {
            _cardStore = cardStore;
            _cardDatabase = cardDatabase;
            _gameModel = gameModel;
        }

        void IStartable.Start()
        {
            BuildAsync().Forget();
        }

        private async UniTaskVoid BuildAsync()
        {
            try
            {
                await _cardStore.Loaded;

                VisualElement root = GetComponent<UIDocument>().rootVisualElement;
                VisualElement mainRoot = root.Q<VisualElement>("MainRoot");
                VisualElement deckArea = root.Q<VisualElement>("DeckArea");
                VisualElement graveyardArea = root.Q<VisualElement>("GraveyardArea");
                VisualElement opponentDeckArea = root.Q<VisualElement>("OpponentDeckArea");
                VisualElement opponentGraveyardArea = root.Q<VisualElement>("OpponentGraveyardArea");
                VisualElement opponentHandArea = root.Q<VisualElement>("OpponentHandArea");
                VisualElement handArea = root.Q<VisualElement>("HandArea");
                VisualElement opponentFieldArea = root.Q<VisualElement>("OpponentFieldArea");
                VisualElement playerFieldArea = root.Q<VisualElement>("PlayerFieldArea");

                mainRoot.style.backgroundImage = new StyleBackground(_cardStore.BattleField);

                VisualElement dragLayer = new VisualElement();
                dragLayer.AddToClassList("main-drag-layer");
                dragLayer.pickingMode = PickingMode.Ignore;
                mainRoot.Add(dragLayer);

                CardData[] shuffled = Shuffle(_cardDatabase.AllCards.ToArray());
                int handSize = Mathf.Min(InitialHandSize, shuffled.Length);
                CardData[] handCards = shuffled.Take(handSize).ToArray();
                CardData[] deckCards = shuffled.Skip(handSize).ToArray();

                _opponentFieldView = new FieldView();
                opponentFieldArea.Add(_opponentFieldView);

                _playerFieldView = new FieldView();
                playerFieldArea.Add(_playerFieldView);

                HandView opponentHandView = new HandView(_cardStore.CardTemplate, new CardData[0], _cardStore.CardBack, dragLayer, faceDown: true, interactive: false);
                opponentHandArea.Add(opponentHandView);

                _handView = new HandView(_cardStore.CardTemplate, new CardData[0], _cardStore.CardBack, dragLayer);
                handArea.Add(_handView);
                _handView.OnCardDropped = (card, worldPos) =>
                {
                    if (_stagedAction != null)
                    {
                        return false;
                    }

                    bool placed = _playerFieldView.TryPlace(card, worldPos);
                    if (placed)
                    {
                        AttackArrowManipulator manipulator = new AttackArrowManipulator(dragLayer);
                        manipulator.CanStart = () =>
                        {
                            if (_stagedAction is AttackAction || _stagedAction is DeckAttackAction)
                            {
                                CancelAction();
                            }

                            return _stagedAction == null;
                        };
                        manipulator.OnAttackTarget = (pos) => OnAttackTarget(card, pos);
                        card.AddManipulator(manipulator);
                        _attackManipulators[card] = manipulator;
                        StageAction(card, new PlayCardAction());
                    }

                    return placed;
                };

                _gameModel.OnResolve += HandleResolve;

                _actionButtonsArea = root.Q<VisualElement>("ActionButtonsArea");
                Button okButton = root.Q<Button>("OkButton");
                Button backButton = root.Q<Button>("BackButton");
                okButton.clicked += ConfirmAction;
                backButton.clicked += CancelAction;

                DeckView deckView = new DeckView(_cardStore.CardTemplate, deckCards, _cardStore.CardBack);
                deckArea.Add(deckView);

                _opponentDeckView = new DeckView(_cardStore.CardTemplate, deckCards, _cardStore.CardBack);
                opponentDeckArea.Add(_opponentDeckView);

                _playerGraveyardView = new GraveyardView();
                graveyardArea.Add(_playerGraveyardView);

                _opponentGraveyardView = new GraveyardView();
                opponentGraveyardArea.Add(_opponentGraveyardView);

                CancellationToken ct = destroyCancellationToken;
                await UniTask.NextFrame(ct);

                Rect deckWorldRect = deckView.worldBound;
                Rect opponentDeckWorldRect = _opponentDeckView.worldBound;
                UniTask[] drawTasks = new UniTask[handCards.Length * 2];
                for (int i = 0; i < handCards.Length; i++)
                {
                    drawTasks[i] = _handView.AddCardAnimatedAsync(handCards[i], deckWorldRect, i * DrawStagger, ct);
                    drawTasks[handCards.Length + i] = opponentHandView.AddCardAnimatedAsync(handCards[i], opponentDeckWorldRect, i * DrawStagger, ct);
                }
                await UniTask.WhenAll(drawTasks);
            }
            catch (OperationCanceledException) { }
        }

        private bool OnAttackTarget(CardView attacker, Vector2 worldPos)
        {
            if (_stagedAction != null)
            {
                return false;
            }

            CardView fieldTarget = _opponentFieldView.TryGetCardAt(worldPos);
            if (fieldTarget != null)
            {
                StageAction(attacker, new AttackAction(fieldTarget));
                return true;
            }

            if (_opponentDeckView.worldBound.Contains(worldPos))
            {
                StageAction(attacker, new DeckAttackAction(_opponentDeckView));
                return true;
            }

            return false;
        }

        private void StageAction(CardView actor, PendingAction action)
        {
            _stagingCard = actor;
            _stagedAction = action;
            _actionButtonsArea.AddToClassList("main-action-buttons-area--visible");
        }

        private void ConfirmAction()
        {
            if (_stagedAction == null)
            {
                return;
            }

            CardView actor = _stagingCard;
            PendingAction action = _stagedAction;
            ClearStagedAction();

            if (action is AttackAction && _attackManipulators.TryGetValue(actor, out AttackArrowManipulator attackManipulator))
            {
                attackManipulator.ClearArrow();
            }

            _gameModel.DoAction(actor, action);
        }

        private void CancelAction()
        {
            if (_stagedAction == null)
            {
                return;
            }

            if (_stagedAction is PlayCardAction)
            {
                Rect cardRect = _stagingCard.worldBound;
                _playerFieldView.RemoveCard(_stagingCard);
                if (_attackManipulators.TryGetValue(_stagingCard, out AttackArrowManipulator m))
                {
                    _stagingCard.RemoveManipulator(m);
                    _attackManipulators.Remove(_stagingCard);
                }

                _handView.AddCardBackAsync(_stagingCard, cardRect, destroyCancellationToken).Forget();
            }
            else if (_stagedAction is AttackAction || _stagedAction is DeckAttackAction)
            {
                if (_attackManipulators.TryGetValue(_stagingCard, out AttackArrowManipulator m))
                {
                    m.ClearArrow();
                }
            }

            ClearStagedAction();
        }

        private void ClearStagedAction()
        {
            _stagingCard = null;
            _stagedAction = null;
            _actionButtonsArea.RemoveFromClassList("main-action-buttons-area--visible");
        }

        private void HandleResolve(CardView card, PendingAction action)
        {
            if (action is AttackAction attack)
            {
                if (card.Data.Attack >= attack.Target.Data.Defense)
                {
                    _opponentFieldView.RemoveCard(attack.Target);
                    _opponentGraveyardView.AddCard(attack.Target);
                }
                return;
            }

            if (action is DeckAttackAction deckAttack)
            {
                if (_attackManipulators.TryGetValue(card, out AttackArrowManipulator manipulator))
                {
                    manipulator.ClearArrow();
                    _attackManipulators.Remove(card);
                }
                deckAttack.Target.RemoveFromTop(card.Data.Attack);
                if (deckAttack.Target.Count == 0)
                {
                    OnWin();
                }
            }
        }

        private void OnWin()
        {
            Debug.Log("You Win!");
        }

        private static CardData[] Shuffle(CardData[] cards)
        {
            for (int i = cards.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }

            return cards;
        }
    }
}
