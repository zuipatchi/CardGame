using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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
        private CpuAgent _cpuAgent;

        private HandView _handView;
        private HandView _opponentHandView;
        private FieldView _playerFieldView;
        private FieldView _opponentFieldView;
        private DeckView _playerDeckView;
        private DeckView _opponentDeckView;
        private GraveyardView _playerGraveyardView;
        private GraveyardView _opponentGraveyardView;
        private VisualElement _actionButtonsArea;
        private readonly Dictionary<CardView, AttackArrowManipulator> _attackManipulators = new Dictionary<CardView, AttackArrowManipulator>();
        private readonly Dictionary<CardView, ArrowView> _pendingArrows = new Dictionary<CardView, ArrowView>();
        private readonly HashSet<CardView> _cpuCards = new HashSet<CardView>();
        private VisualElement _dragLayer;

        private CardView _stagingCard;
        private PendingAction _stagedAction;
        private bool _isGameOver;
        private bool _isAnimating;

        [Inject]
        public void Construct(CardStore cardStore, CardDatabase cardDatabase, GameModel gameModel, CpuAgent cpuAgent)
        {
            _cardStore = cardStore;
            _cardDatabase = cardDatabase;
            _gameModel = gameModel;
            _cpuAgent = cpuAgent;
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

                _dragLayer = new VisualElement();
                _dragLayer.AddToClassList("main-drag-layer");
                _dragLayer.pickingMode = PickingMode.Ignore;
                mainRoot.Add(_dragLayer);
                VisualElement dragLayer = _dragLayer;

                CardData[] allCards = _cardDatabase.AllCards.ToArray();
                int handSize = Mathf.Min(InitialHandSize, allCards.Length);

                CardData[] playerShuffled = Shuffle((CardData[])allCards.Clone());
                CardData[] playerHandCards = playerShuffled.Take(handSize).ToArray();
                CardData[] playerDeckCards = playerShuffled.Skip(handSize).ToArray();

                CardData[] cpuShuffled = Shuffle((CardData[])allCards.Clone());
                CardData[] cpuHandCards = cpuShuffled.Take(handSize).ToArray();
                CardData[] cpuDeckCards = cpuShuffled.Skip(handSize).ToArray();

                _opponentFieldView = new FieldView();
                opponentFieldArea.Add(_opponentFieldView);

                _playerFieldView = new FieldView();
                playerFieldArea.Add(_playerFieldView);

                _opponentHandView = new HandView(_cardStore.CardTemplate, new CardData[0], _cardStore.CardBack, dragLayer, faceDown: true, interactive: false);
                opponentHandArea.Add(_opponentHandView);

                _handView = new HandView(_cardStore.CardTemplate, new CardData[0], _cardStore.CardBack, dragLayer);
                handArea.Add(_handView);
                _handView.OnCardDropped = (card, worldPos) =>
                {
                    if (_stagedAction != null || !_gameModel.IsLocalTurn || _isGameOver || _isAnimating)
                    {
                        return false;
                    }

                    bool placed = _playerFieldView.TryPlace(card, worldPos);
                    if (placed)
                    {
                        AttackArrowManipulator manipulator = new AttackArrowManipulator(dragLayer);
                        manipulator.CanStart = () =>
                        {
                            if (!_gameModel.IsLocalTurn || _isGameOver || _isAnimating)
                            {
                                return false;
                            }

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

                VisualElement resolveOverlay = new VisualElement();
                resolveOverlay.AddToClassList("resolve-overlay");
                resolveOverlay.pickingMode = PickingMode.Ignore;
                resolveOverlay.style.display = DisplayStyle.None;
                Label resolveLabel = new Label("Resolve");
                resolveLabel.pickingMode = PickingMode.Ignore;
                resolveLabel.AddToClassList("resolve-label");
                resolveOverlay.Add(resolveLabel);
                mainRoot.Add(resolveOverlay);

                _gameModel.OnResolveAsync = HandleResolveAsync;
                _gameModel.OnTurnChanged += OnTurnChanged;
                _gameModel.OnResolvePhaseStart += () =>
                    PlayResolveAnimationAsync(resolveOverlay, resolveLabel, destroyCancellationToken).Forget();

                _actionButtonsArea = root.Q<VisualElement>("ActionButtonsArea");
                Button okButton = root.Q<Button>("OkButton");
                Button backButton = root.Q<Button>("BackButton");
                okButton.clicked += ConfirmAction;
                backButton.clicked += CancelAction;

                _playerDeckView = new DeckView(_cardStore.CardTemplate, playerDeckCards, _cardStore.CardBack);
                deckArea.Add(_playerDeckView);

                _opponentDeckView = new DeckView(_cardStore.CardTemplate, cpuDeckCards, _cardStore.CardBack);
                opponentDeckArea.Add(_opponentDeckView);

                _playerGraveyardView = new GraveyardView();
                graveyardArea.Add(_playerGraveyardView);

                _opponentGraveyardView = new GraveyardView();
                opponentGraveyardArea.Add(_opponentGraveyardView);

                CancellationToken ct = destroyCancellationToken;
                await UniTask.NextFrame(ct);

                Rect deckWorldRect = _playerDeckView.worldBound;
                Rect opponentDeckWorldRect = _opponentDeckView.worldBound;
                UniTask[] drawTasks = new UniTask[handSize * 2];
                for (int i = 0; i < handSize; i++)
                {
                    drawTasks[i] = _handView.AddCardAnimatedAsync(playerHandCards[i], deckWorldRect, i * DrawStagger, ct);
                    drawTasks[handSize + i] = _opponentHandView.AddCardAnimatedAsync(cpuHandCards[i], opponentDeckWorldRect, i * DrawStagger, ct);
                }
                await UniTask.WhenAll(drawTasks);

                foreach (CardView cpuCard in _opponentHandView.Cards)
                {
                    _cpuCards.Add(cpuCard);
                }
            }
            catch (OperationCanceledException) { }
        }

        private void OnTurnChanged(bool isLocalTurn)
        {
            if (!isLocalTurn && !_isGameOver)
            {
                ProcessCpuTurnAsync(destroyCancellationToken).Forget();
            }
        }

        private async UniTaskVoid ProcessCpuTurnAsync(CancellationToken ct)
        {
            try
            {
                await _cpuAgent.TakeTurnAsync(
                    _opponentHandView,
                    _opponentFieldView,
                    _playerFieldView,
                    _playerDeckView,
                    _dragLayer,
                    _pendingArrows,
                    ct);
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

        private async void ConfirmAction()
        {
            if (_stagedAction == null)
            {
                return;
            }

            CardView actor = _stagingCard;
            PendingAction action = _stagedAction;
            ClearStagedAction();

            _isAnimating = true;
            try
            {
                await _gameModel.DoAction(actor, action);
            }
            finally
            {
                _isAnimating = false;
            }
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

        private async UniTask HandleResolveAsync(CardView card, PendingAction action)
        {
            if (action is AttackAction attack)
            {
                ClearArrow(card);
                await PlayAttackAnimationAsync(card, attack.Target.worldBound, destroyCancellationToken);
                if (card.Data.Attack >= attack.Target.Data.Defense)
                {
                    if (_cpuCards.Contains(attack.Target))
                    {
                        _opponentFieldView.RemoveCard(attack.Target);
                        _opponentGraveyardView.AddCard(attack.Target);
                    }
                    else
                    {
                        _playerFieldView.RemoveCard(attack.Target);
                        _playerGraveyardView.AddCard(attack.Target);
                        _attackManipulators.Remove(attack.Target);
                    }
                }
                return;
            }

            if (action is DeckAttackAction deckAttack)
            {
                ClearArrow(card);
                await PlayAttackAnimationAsync(card, deckAttack.Target.worldBound, destroyCancellationToken);
                deckAttack.Target.RemoveFromTop(card.Data.Attack);
                if (deckAttack.Target.Count == 0)
                {
                    _isGameOver = true;
                    OnGameEnd(deckAttack.Target == _playerDeckView);
                }
            }
        }

        private void ClearArrow(CardView card)
        {
            if (_attackManipulators.TryGetValue(card, out AttackArrowManipulator manipulator))
            {
                manipulator.ClearArrow();
            }

            if (_pendingArrows.TryGetValue(card, out ArrowView arrow))
            {
                arrow.RemoveFromHierarchy();
                _pendingArrows.Remove(card);
            }
        }

        private void OnGameEnd(bool cpuWins)
        {
            Debug.Log(cpuWins ? "CPU の勝ち！" : "あなたの勝ち！");
        }

        private static async UniTask PlayAttackAnimationAsync(CardView attacker, Rect targetRect, CancellationToken ct)
        {
            Rect attackerRect = attacker.worldBound;
            Vector2 delta = targetRect.center - attackerRect.center;
            Vector2 rushTarget = delta * 0.6f;

            float progress = 0f;

            void ApplyOffset(float t)
            {
                Vector2 pos = rushTarget * t;
                attacker.style.translate = new StyleTranslate(new Translate(new Length(pos.x), new Length(pos.y)));
            }

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Append(DOTween.To(() => progress, v => { progress = v; ApplyOffset(v); }, 1f, 0.12f).SetEase(Ease.OutQuad))
                .AppendInterval(0.05f)
                .Append(DOTween.To(() => progress, v => { progress = v; ApplyOffset(v); }, 0f, 0.2f).SetEase(Ease.OutQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() =>
            {
                seq.Kill();
                tcs.TrySetCanceled();
            });

            try
            {
                await tcs.Task;
            }
            catch (OperationCanceledException) { }

            attacker.style.translate = StyleKeyword.Null;
        }

        private async UniTaskVoid PlayResolveAnimationAsync(VisualElement overlay, Label label, CancellationToken ct)
        {
            overlay.style.display = DisplayStyle.Flex;
            overlay.style.opacity = 0f;
            label.style.scale = new Scale(new Vector3(0.5f, 0.5f, 1f));

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => overlay.style.opacity.value, v => overlay.style.opacity = v, 1f, 0.25f).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => label.style.scale.value.value.x, v => label.style.scale = new Scale(new Vector3(v, v, 1f)), 1f, 0.25f).SetEase(Ease.OutBack))
                .AppendInterval(0.4f)
                .Append(DOTween.To(() => overlay.style.opacity.value, v => overlay.style.opacity = v, 0f, 0.3f).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() =>
            {
                seq.Kill();
                tcs.TrySetCanceled();
            });

            try
            {
                await tcs.Task;
            }
            catch (OperationCanceledException) { }

            overlay.style.display = DisplayStyle.None;
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
