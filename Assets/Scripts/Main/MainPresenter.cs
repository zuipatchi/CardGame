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
        private const float CpuThinkSeconds = 0.8f;
        private const float CpuCardFlyDuration = 0.3f;
        private const float CardWidth = 160f;
        private const float CardHeight = 220f;

        private CardStore _cardStore;
        private CardDatabase _cardDatabase;
        private GameModel _gameModel;

        private HandView _handView;
        private HandView _opponentHandView;
        private FieldView _playerFieldView;
        private FieldView _opponentFieldView;
        private CharacterSlotView _playerCharacterSlot;
        private CharacterSlotView _opponentCharacterSlot;
        private DeckView _playerDeckView;
        private DeckView _opponentDeckView;
        private GraveyardView _playerGraveyardView;
        private GraveyardView _opponentGraveyardView;
        private VisualElement _actionButtonsArea;
        private Button _okButton;
        private Button _backButton;
        private Button _passButton;
        private VisualElement _turnOverlay;
        private Label _turnLabel;
        private VisualElement _resolveOverlay;
        private Label _resolveLabel;
        private VisualElement _playerAtkCounterOverlay;
        private Label _playerAtkCounterLabel;
        private VisualElement _opponentAtkCounterOverlay;
        private Label _opponentAtkCounterLabel;
        private VisualElement _dragLayer;

        private readonly HashSet<CardView> _cpuCards = new HashSet<CardView>();
        private bool _isGameOver;

        // 準備フェーズの入力待ち（null=パス、card=Ready するカード）
        private UniTaskCompletionSource<CardView> _prepInputTcs;
        private CardView _stagedPrepCard;

        // 戦闘前フェーズの入力待ち（null=パス、card=プレイしたカード）
        private UniTaskCompletionSource<CardView> _preBattleInputTcs;
        private CardView _stagedPreBattleCard;
        private bool _isLocalPreBattleActive;

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
                VisualElement playerCharacterArea = root.Q<VisualElement>("PlayerCharacterArea");
                VisualElement opponentCharacterArea = root.Q<VisualElement>("OpponentCharacterArea");

                mainRoot.style.backgroundImage = new StyleBackground(_cardStore.BattleField);

                _dragLayer = new VisualElement();
                _dragLayer.AddToClassList("main-drag-layer");
                _dragLayer.pickingMode = PickingMode.Ignore;
                mainRoot.Add(_dragLayer);

                CardData[] allCards = _cardDatabase.AllCards.ToArray();
                int handSize = Mathf.Min(InitialHandSize, allCards.Length);

                CardData[] playerShuffled = Shuffle((CardData[])allCards.Clone());
                CardData[] playerHandCards = playerShuffled.Take(handSize).ToArray();
                CardData[] playerDeckCards = playerShuffled.Skip(handSize).ToArray();

                CardData[] cpuShuffled = Shuffle((CardData[])allCards.Clone());
                CardData[] cpuHandCards = cpuShuffled.Take(handSize).ToArray();
                CardData[] cpuDeckCards = cpuShuffled.Skip(handSize).ToArray();

                _playerCharacterSlot = new CharacterSlotView();
                _playerCharacterSlot.OnCardDisplaced += card => _playerGraveyardView.AddCard(card);
                playerCharacterArea.Add(_playerCharacterSlot);

                _opponentCharacterSlot = new CharacterSlotView();
                _opponentCharacterSlot.OnCardDisplaced += card => _opponentGraveyardView.AddCard(card);
                opponentCharacterArea.Add(_opponentCharacterSlot);

                _opponentFieldView = new FieldView();
                opponentFieldArea.Add(_opponentFieldView);

                _playerFieldView = new FieldView();
                playerFieldArea.Add(_playerFieldView);

                _playerAtkCounterOverlay = new VisualElement();
                _playerAtkCounterOverlay.AddToClassList("atk-counter-overlay");
                _playerAtkCounterOverlay.pickingMode = PickingMode.Ignore;
                _playerAtkCounterOverlay.style.display = DisplayStyle.None;
                _playerAtkCounterLabel = new Label("0");
                _playerAtkCounterLabel.pickingMode = PickingMode.Ignore;
                _playerAtkCounterLabel.AddToClassList("atk-counter-label");
                _playerAtkCounterOverlay.Add(_playerAtkCounterLabel);
                _playerFieldView.Add(_playerAtkCounterOverlay);

                _opponentAtkCounterOverlay = new VisualElement();
                _opponentAtkCounterOverlay.AddToClassList("atk-counter-overlay");
                _opponentAtkCounterOverlay.pickingMode = PickingMode.Ignore;
                _opponentAtkCounterOverlay.style.display = DisplayStyle.None;
                _opponentAtkCounterLabel = new Label("0");
                _opponentAtkCounterLabel.pickingMode = PickingMode.Ignore;
                _opponentAtkCounterLabel.AddToClassList("atk-counter-label");
                _opponentAtkCounterOverlay.Add(_opponentAtkCounterLabel);
                _opponentFieldView.Add(_opponentAtkCounterOverlay);

                _opponentHandView = new HandView(
                    _cardStore.CardTemplate, new CardData[0],
                    _cardStore.CardBack, _dragLayer, faceDown: true, interactive: false);
                opponentHandArea.Add(_opponentHandView);

                _handView = new HandView(
                    _cardStore.CardTemplate, new CardData[0],
                    _cardStore.CardBack, _dragLayer);
                handArea.Add(_handView);
                _handView.OnCardDropped = HandlePlayerCardDrop;

                _resolveOverlay = new VisualElement();
                _resolveOverlay.AddToClassList("resolve-overlay");
                _resolveOverlay.pickingMode = PickingMode.Ignore;
                _resolveOverlay.style.display = DisplayStyle.None;
                _resolveLabel = new Label("Resolve");
                _resolveLabel.pickingMode = PickingMode.Ignore;
                _resolveLabel.AddToClassList("resolve-label");
                _resolveOverlay.Add(_resolveLabel);
                mainRoot.Add(_resolveOverlay);

                _turnOverlay = new VisualElement();
                _turnOverlay.AddToClassList("turn-announcement-overlay");
                _turnOverlay.pickingMode = PickingMode.Ignore;
                _turnOverlay.style.display = DisplayStyle.None;
                _turnLabel = new Label();
                _turnLabel.pickingMode = PickingMode.Ignore;
                _turnLabel.AddToClassList("turn-announcement-label");
                _turnOverlay.Add(_turnLabel);
                mainRoot.Add(_turnOverlay);

                _actionButtonsArea = root.Q<VisualElement>("ActionButtonsArea");
                _okButton = root.Q<Button>("OkButton");
                _backButton = root.Q<Button>("BackButton");
                _passButton = root.Q<Button>("PassButton");
                _okButton.clicked += OnOkClicked;
                _backButton.clicked += OnBackClicked;
                _passButton.clicked += OnPassClicked;

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

                RunGameAsync(ct).Forget();
            }
            catch (OperationCanceledException) { }
        }

        // ─── ゲームループ ───────────────────────────────────────────────

        private async UniTaskVoid RunGameAsync(CancellationToken ct)
        {
            try
            {
                while (!_isGameOver)
                {
                    await RunTurnAsync(ct);
                }
            }
            catch (OperationCanceledException) { }
        }

        private async UniTask RunTurnAsync(CancellationToken ct)
        {
            bool isLocalTurn = _gameModel.IsLocalTurn;
            await RunDrawPhaseAsync(isLocalTurn, ct);
            if (_isGameOver)
            {
                return;
            }

            _gameModel.BeginPreparation();
            await RunPreparationPhaseAsync(ct);
            if (_isGameOver)
            {
                return;
            }

            _gameModel.BeginResolution();
            await RunResolutionPhaseAsync(ct);
            if (_isGameOver)
            {
                return;
            }

            _gameModel.BeginPreBattle();
            await RunPreBattlePhaseAsync(isLocalTurn, ct);
            if (_isGameOver)
            {
                return;
            }

            _gameModel.BeginBattle();
            await RunBattlePhaseAsync(ct);

            _gameModel.EndTurn();
        }

        // ─── ドローフェーズ ─────────────────────────────────────────────

        private async UniTask RunDrawPhaseAsync(bool isLocalTurn, CancellationToken ct)
        {
            DeckView sourceDeck = isLocalTurn ? _playerDeckView : _opponentDeckView;
            HandView targetHand = isLocalTurn ? _handView : _opponentHandView;
            Rect deckRect = sourceDeck.worldBound;
            CardData drawn = sourceDeck.DrawTop();

            UniTask announcementTask = PlayTurnAnnouncementAsync(isLocalTurn, ct);
            UniTask drawTask = drawn != null
                ? targetHand.AddCardAnimatedAsync(drawn, deckRect, 0f, ct)
                : UniTask.CompletedTask;
            await UniTask.WhenAll(announcementTask, drawTask);

            if (!isLocalTurn && drawn != null)
            {
                IReadOnlyList<CardView> cards = _opponentHandView.Cards;
                if (cards.Count > 0)
                {
                    _cpuCards.Add(cards[cards.Count - 1]);
                }
            }
        }

        // ─── 準備フェーズ ────────────────────────────────────────────────

        private async UniTask RunPreparationPhaseAsync(CancellationToken ct)
        {
            while (true)
            {
                if (_gameModel.IsLocalPreparationTurn)
                {
                    CardView readied = await WaitForPlayerPrepInputAsync(ct);
                    if (readied == null)
                    {
                        if (_gameModel.Pass())
                        {
                            break;
                        }
                    }
                    else
                    {
                        if (readied.Data is not CharacterCardData)
                        {
                            _playerFieldView.PlaceCard(readied);
                        }
                        // キャラカードはドロップ時に既にスロット配置済み

                        _gameModel.ReadyCard(readied);
                    }
                }
                else
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(CpuThinkSeconds), cancellationToken: ct);
                    IReadOnlyList<CardView> cpuHand = _opponentHandView.Cards;
                    int idx = CpuAgent.ChooseCardToReadyIndex(cpuHand.Select(c => c.Data).ToList());

                    if (idx >= 0)
                    {
                        CardView card = cpuHand[idx];
                        Rect fromRect = card.worldBound;
                        _opponentHandView.RemoveCard(card);
                        if (card.Data is CharacterCardData)
                        {
                            await FlyCardToDestAsync(card, fromRect, _opponentCharacterSlot, ct);
                            _opponentCharacterSlot.PlaceCard(card);
                        }
                        else
                        {
                            await FlyCardToDestAsync(card, fromRect, _opponentFieldView, ct);
                            _opponentFieldView.PlaceCard(card);
                        }
                        await card.FlipAsync(ct);
                        _gameModel.ReadyCard(card);
                    }
                    else
                    {
                        if (_gameModel.Pass())
                        {
                            break;
                        }
                    }
                }
            }

            HideActionButtons();
        }

        private async UniTask<CardView> WaitForPlayerPrepInputAsync(CancellationToken ct)
        {
            _prepInputTcs = new UniTaskCompletionSource<CardView>();
            _stagedPrepCard = null;
            ShowActionButtons();
            UpdateStagedButtons(_stagedPrepCard != null);

            try
            {
                return await _prepInputTcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _prepInputTcs = null;
            }
        }

        // ─── 解決フェーズ ────────────────────────────────────────────────

        private async UniTask RunResolutionPhaseAsync(CancellationToken ct)
        {
            IReadOnlyList<CardView> queue = _gameModel.ReadyQueue;
            if (queue.Count == 0)
            {
                return;
            }

            await PlayResolveAnimationAsync(ct);

            for (int i = queue.Count - 1; i >= 0; i--)
            {
                CardView card = queue[i];
                card.SetState(CardState.Resolve);
                await UniTask.Delay(TimeSpan.FromSeconds(0.3f), cancellationToken: ct);

                bool isLocal = !_cpuCards.Contains(card);

                if (card.Data is CharacterCardData)
                {
                    // 準備フェーズでスロット配置済み（プレイヤー・CPU 共通）
                }
                else
                {
                    FieldView field = isLocal ? _playerFieldView : _opponentFieldView;
                    field.RemoveCard(card);
                    GraveyardView graveyard = isLocal ? _playerGraveyardView : _opponentGraveyardView;
                    graveyard.AddCard(card);
                }

                card.SetState(CardState.Normal);
            }
        }

        // ─── 戦闘前フェーズ ──────────────────────────────────────────────

        private async UniTask RunPreBattlePhaseAsync(bool isLocalTurn, CancellationToken ct)
        {
            await PlayAnnouncementAsync("SET SKILLS", "turn-announcement-label--skill", ct);

            bool isPlayerTurn = isLocalTurn;
            int consecutivePasses = 0;

            while (true)
            {
                if (isPlayerTurn)
                {
                    CardView played = await WaitForPlayerPreBattleTurnAsync(ct);
                    if (played != null)
                    {
                        consecutivePasses = 0;
                    }
                    else
                    {
                        if (++consecutivePasses >= 2)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    bool played = await RunCpuPreBattleSubTurnAsync(ct);
                    if (played)
                    {
                        consecutivePasses = 0;
                    }
                    else
                    {
                        if (++consecutivePasses >= 2)
                        {
                            break;
                        }
                    }
                }

                isPlayerTurn = !isPlayerTurn;
            }
        }

        private async UniTask<CardView> WaitForPlayerPreBattleTurnAsync(CancellationToken ct)
        {
            _isLocalPreBattleActive = true;
            _preBattleInputTcs = new UniTaskCompletionSource<CardView>();
            _stagedPreBattleCard = null;
            ShowActionButtons();
            UpdateStagedButtons(_stagedPreBattleCard != null);

            try
            {
                return await _preBattleInputTcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _isLocalPreBattleActive = false;
                _preBattleInputTcs = null;
                HideActionButtons();
            }
        }

        private async UniTask<bool> RunCpuPreBattleSubTurnAsync(CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(CpuThinkSeconds), cancellationToken: ct);
            IReadOnlyList<CardView> cpuHand = _opponentHandView.Cards;
            int idx = CpuAgent.ChooseSkillCardIndex(cpuHand.Select(c => c.Data).ToList());

            if (idx < 0)
            {
                return false;
            }

            CardView card = cpuHand[idx];
            Rect fromRect = card.worldBound;
            _opponentHandView.RemoveCard(card);
            await FlyCardToDestAsync(card, fromRect, _opponentFieldView, ct);
            _opponentFieldView.PlaceCard(card);
            return true;
        }

        // ─── 戦闘フェーズ ────────────────────────────────────────────────

        private async UniTask RunBattlePhaseAsync(CancellationToken ct)
        {
            List<CardView> playerSkill = _playerFieldView.Cards.ToList();
            List<CardView> opponentSkill = _opponentFieldView.Cards.ToList();

            if (playerSkill.Count == 0 && opponentSkill.Count == 0)
            {
                return;
            }

            await PlayAnnouncementAsync("FIGHT", "turn-announcement-label--fight", ct);

            // 両者の技カードを同時に表向き
            UniTask[] flipTasks = new UniTask[playerSkill.Count + opponentSkill.Count];
            int flipIdx = 0;
            foreach (CardView c in playerSkill)
            {
                flipTasks[flipIdx++] = c.FlipAsync(ct);
            }
            foreach (CardView c in opponentSkill)
            {
                flipTasks[flipIdx++] = c.FlipAsync(ct);
            }
            await UniTask.WhenAll(flipTasks);

            await UniTask.Delay(TimeSpan.FromSeconds(0.3f), cancellationToken: ct);

            // ダメージ計算（ATK合計 - 相手DEF）
            int playerATK = playerSkill.Sum(c => c.Data.Attack);
            int opponentATK = opponentSkill.Sum(c => c.Data.Attack);
            int damageToOpponent = Mathf.Max(0, playerATK - _opponentCharacterSlot.Defense);
            int damageToPlayer = Mathf.Max(0, opponentATK - _playerCharacterSlot.Defense);

            // アニメーション①: ATKカウンター（両者同時）
            await PlayAtkCounterAsync(playerATK, opponentATK, ct);

            // アニメーション②: 技カードが相手デッキへ飛翔（両者同時）
            await UniTask.WhenAll(
                PlaySkillCardsAttackAsync(playerSkill, _playerFieldView, _opponentDeckView, ct),
                PlaySkillCardsAttackAsync(opponentSkill, _opponentFieldView, _playerDeckView, ct)
            );

            // ダメージ適用
            if (damageToOpponent > 0)
            {
                _opponentDeckView.RemoveFromTop(damageToOpponent);
            }
            if (damageToPlayer > 0)
            {
                _playerDeckView.RemoveFromTop(damageToPlayer);
            }

            // 勝敗判定
            if (_opponentDeckView.Count == 0 || _playerDeckView.Count == 0)
            {
                _isGameOver = true;
                bool bothZero = _opponentDeckView.Count == 0 && _playerDeckView.Count == 0;
                OnGameEnd(bothZero ? (bool?)null : _opponentDeckView.Count == 0);
            }

            // 全技カードを墓地へ
            foreach (CardView c in playerSkill)
            {
                if (c.parent != null)
                {
                    c.RemoveFromHierarchy();
                }
                _playerGraveyardView.AddCard(c);
            }
            foreach (CardView c in opponentSkill)
            {
                if (c.parent != null)
                {
                    c.RemoveFromHierarchy();
                }
                _opponentGraveyardView.AddCard(c);
            }
        }

        // ─── ボタンハンドラ ──────────────────────────────────────────────

        private bool HandlePlayerCardDrop(CardView card, Vector2 worldPos)
        {
            if (_isGameOver)
            {
                return false;
            }

            if (_gameModel.Phase == TurnPhase.Preparation && _gameModel.IsLocalPreparationTurn)
            {
                if (card.Data is SkillCardData || _stagedPrepCard != null)
                {
                    return false;
                }

                if (card.Data is CharacterCardData)
                {
                    if (!_playerCharacterSlot.worldBound.Contains(worldPos))
                    {
                        return false;
                    }

                    _playerCharacterSlot.PlaceCard(card);
                }
                else
                {
                    if (!_playerFieldView.worldBound.Contains(worldPos))
                    {
                        return false;
                    }
                }

                _stagedPrepCard = card;
                UpdateStagedButtons(_stagedPrepCard != null);
                return true;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle && _isLocalPreBattleActive)
            {
                if (card.Data is not SkillCardData || _stagedPreBattleCard != null)
                {
                    return false;
                }

                bool placed = _playerFieldView.TryPlace(card, worldPos);
                if (placed)
                {
                    _stagedPreBattleCard = card;
                    card.FlipAsync(destroyCancellationToken).Forget();
                    UpdateStagedButtons(_stagedPreBattleCard != null);
                }
                return placed;
            }

            return false;
        }

        private void OnOkClicked()
        {
            if (_gameModel.Phase == TurnPhase.Preparation && _prepInputTcs != null)
            {
                if (_stagedPrepCard == null)
                {
                    return;
                }

                CardView card = _stagedPrepCard;
                _stagedPrepCard = null;
                HideActionButtons();
                _prepInputTcs.TrySetResult(card);
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle && _preBattleInputTcs != null)
            {
                if (_stagedPreBattleCard == null)
                {
                    return;
                }

                CardView card = _stagedPreBattleCard;
                _stagedPreBattleCard = null;
                _preBattleInputTcs.TrySetResult(card);
            }
        }

        private void OnBackClicked()
        {
            if (_gameModel.Phase == TurnPhase.Preparation && _prepInputTcs != null)
            {
                if (_stagedPrepCard != null)
                {
                    // カードのキャンセルのみ。プレイヤーのサブターンは継続
                    Rect rect = _stagedPrepCard.worldBound; // 階層から外す前に取得
                    if (_stagedPrepCard.Data is CharacterCardData)
                    {
                        _playerCharacterSlot.RemoveCard();
                    }

                    _handView.AddCardBackAsync(_stagedPrepCard, rect, destroyCancellationToken).Forget();
                    _stagedPrepCard = null;
                    UpdateStagedButtons(_stagedPrepCard != null);
                    return;
                }

                // ステージなし = パス
                HideActionButtons();
                _prepInputTcs.TrySetResult(null);
            }

            if (_gameModel.Phase == TurnPhase.PreBattle && _preBattleInputTcs != null)
            {
                if (_stagedPreBattleCard != null)
                {
                    Rect rect = _stagedPreBattleCard.worldBound;
                    _playerFieldView.RemoveCard(_stagedPreBattleCard);
                    CardView card = _stagedPreBattleCard;
                    _stagedPreBattleCard = null;
                    card.FlipAsync(destroyCancellationToken).Forget();
                    _handView.AddCardBackAsync(card, rect, destroyCancellationToken).Forget();
                    UpdateStagedButtons(_stagedPreBattleCard != null);
                }
            }
        }

        private void OnPassClicked()
        {
            if (_gameModel.Phase == TurnPhase.Preparation && _prepInputTcs != null && _stagedPrepCard == null)
            {
                HideActionButtons();
                _prepInputTcs.TrySetResult(null);
            }

            if (_gameModel.Phase == TurnPhase.PreBattle && _preBattleInputTcs != null && _stagedPreBattleCard == null)
            {
                _preBattleInputTcs.TrySetResult(null);
            }
        }

        // ─── UI ヘルパー ─────────────────────────────────────────────────

        private void UpdateStagedButtons(bool hasStaged)
        {
            _passButton.style.display = hasStaged ? DisplayStyle.None : DisplayStyle.Flex;
            _backButton.style.display = hasStaged ? DisplayStyle.Flex : DisplayStyle.None;
            _okButton.style.display = hasStaged ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ShowActionButtons()
        {
            _actionButtonsArea.AddToClassList("main-action-buttons-area--visible");
        }

        private void HideActionButtons()
        {
            _actionButtonsArea.RemoveFromClassList("main-action-buttons-area--visible");
        }

        private void OnGameEnd(bool? playerWins)
        {
            string message = playerWins == null ? "引き分け！" : playerWins.Value ? "あなたの勝ち！" : "CPU の勝ち！";
            Debug.Log(message);
        }

        private async UniTask PlayTurnAnnouncementAsync(bool isLocalTurn, CancellationToken ct)
        {
            string labelClass = isLocalTurn ? "turn-announcement-label--player" : "turn-announcement-label--enemy";
            await PlayAnnouncementAsync(isLocalTurn ? "YOUR TURN" : "ENEMY TURN", labelClass, ct);
        }

        private async UniTask PlayAnnouncementAsync(string text, string labelClass, CancellationToken ct)
        {
            _turnLabel.text = text;
            _turnLabel.RemoveFromClassList("turn-announcement-label--player");
            _turnLabel.RemoveFromClassList("turn-announcement-label--enemy");
            _turnLabel.RemoveFromClassList("turn-announcement-label--skill");
            _turnLabel.RemoveFromClassList("turn-announcement-label--fight");
            _turnLabel.AddToClassList(labelClass);

            _turnOverlay.style.display = DisplayStyle.Flex;
            _turnOverlay.style.opacity = 0f;
            _turnLabel.style.scale = new Scale(new Vector3(0.5f, 0.5f, 1f));

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => _turnOverlay.style.opacity.value, v => _turnOverlay.style.opacity = v, 1f, 0.25f).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => _turnLabel.style.scale.value.value.x, v => _turnLabel.style.scale = new Scale(new Vector3(v, v, 1f)), 1f, 0.25f).SetEase(Ease.OutBack))
                .AppendInterval(0.5f)
                .Append(DOTween.To(() => _turnOverlay.style.opacity.value, v => _turnOverlay.style.opacity = v, 0f, 0.3f).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }

            _turnOverlay.style.display = DisplayStyle.None;
        }

        private async UniTask PlayResolveAnimationAsync(CancellationToken ct)
        {
            _resolveOverlay.style.display = DisplayStyle.Flex;
            _resolveOverlay.style.opacity = 0f;
            _resolveLabel.style.scale = new Scale(new Vector3(0.5f, 0.5f, 1f));

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => _resolveOverlay.style.opacity.value, v => _resolveOverlay.style.opacity = v, 1f, 0.25f).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => _resolveLabel.style.scale.value.value.x, v => _resolveLabel.style.scale = new Scale(new Vector3(v, v, 1f)), 1f, 0.25f).SetEase(Ease.OutBack))
                .AppendInterval(0.4f)
                .Append(DOTween.To(() => _resolveOverlay.style.opacity.value, v => _resolveOverlay.style.opacity = v, 0f, 0.3f).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }

            _resolveOverlay.style.display = DisplayStyle.None;
        }

        private async UniTask PlayAtkCounterAsync(int playerAtk, int opponentAtk, CancellationToken ct)
        {
            const float countDuration = 0.8f;
            const float holdDuration = 0.3f;
            const float fadeDuration = 0.3f;

            _playerAtkCounterOverlay.BringToFront();
            _opponentAtkCounterOverlay.BringToFront();
            _playerAtkCounterLabel.text = "0";
            _opponentAtkCounterLabel.text = "0";
            _playerAtkCounterOverlay.style.display = DisplayStyle.Flex;
            _opponentAtkCounterOverlay.style.display = DisplayStyle.Flex;
            _playerAtkCounterOverlay.style.opacity = 0f;
            _opponentAtkCounterOverlay.style.opacity = 0f;

            float playerVal = 0f;
            float opponentVal = 0f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => _playerAtkCounterOverlay.style.opacity.value, v => _playerAtkCounterOverlay.style.opacity = v, 1f, 0.2f))
                .Join(DOTween.To(() => _opponentAtkCounterOverlay.style.opacity.value, v => _opponentAtkCounterOverlay.style.opacity = v, 1f, 0.2f))
                .Join(DOTween.To(() => playerVal, v => { playerVal = v; _playerAtkCounterLabel.text = Mathf.RoundToInt(v).ToString(); }, (float)playerAtk, countDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => opponentVal, v => { opponentVal = v; _opponentAtkCounterLabel.text = Mathf.RoundToInt(v).ToString(); }, (float)opponentAtk, countDuration).SetEase(Ease.OutQuad))
                .AppendInterval(holdDuration)
                .Append(DOTween.To(() => _playerAtkCounterOverlay.style.opacity.value, v => _playerAtkCounterOverlay.style.opacity = v, 0f, fadeDuration))
                .Join(DOTween.To(() => _opponentAtkCounterOverlay.style.opacity.value, v => _opponentAtkCounterOverlay.style.opacity = v, 0f, fadeDuration))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try
            {
                await tcs.Task;
            }
            catch (OperationCanceledException) { }

            _playerAtkCounterOverlay.style.display = DisplayStyle.None;
            _opponentAtkCounterOverlay.style.display = DisplayStyle.None;
        }

        private async UniTask PlaySkillCardsAttackAsync(
            List<CardView> cards, FieldView field, DeckView targetDeck, CancellationToken ct)
        {
            if (cards.Count == 0)
            {
                return;
            }

            const float windupDuration = 0.15f;
            const float windupDistance = 50f;
            const float flyDuration = 0.65f;
            const float stagger = 0.12f;
            const float knockbackDist = 35f;
            const float knockbackDuration = 0.15f;

            List<Rect> rects = cards.Select(c => c.worldBound).ToList();
            foreach (CardView c in cards)
            {
                field.RemoveCard(c);
            }

            Vector2 toCenter = targetDeck.worldBound.center;

            for (int i = 0; i < cards.Count; i++)
            {
                CardView card = cards[i];
                Rect rect = rects[i];
                card.style.position = Position.Absolute;
                card.style.left = rect.center.x - CardWidth / 2f;
                card.style.top = rect.center.y - CardHeight / 2f;
                card.style.width = StyleKeyword.Null;
                card.style.height = StyleKeyword.Null;
                card.style.rotate = new Rotate(new Angle(0f, AngleUnit.Degree));
                card.style.scale = new Scale(Vector3.one);
                card.style.transformOrigin = StyleKeyword.Null;
                card.style.marginLeft = StyleKeyword.Null;
                card.style.marginRight = StyleKeyword.Null;
                _dragLayer.Add(card);
            }

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence masterSeq = DOTween.Sequence();

            for (int i = 0; i < cards.Count; i++)
            {
                CardView card = cards[i];
                Vector2 fromCenter = rects[i].center;
                Vector2 dir = (toCenter - fromCenter).normalized;
                float facingAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f;

                Vector2 windupCenter = fromCenter - dir * windupDistance;
                float windupLeft = windupCenter.x - CardWidth / 2f;
                float windupTop = windupCenter.y - CardHeight / 2f;
                float targetLeft = toCenter.x - CardWidth / 2f;
                float targetTop = toCenter.y - CardHeight / 2f;
                float rotAngle = 0f;

                // Phase 1: 予備動作（後退 + デッキ方向を向く）
                Sequence cardSeq = DOTween.Sequence()
                    .Join(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, windupLeft, windupDuration).SetEase(Ease.OutSine))
                    .Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, windupTop, windupDuration).SetEase(Ease.OutSine))
                    .Join(DOTween.To(() => rotAngle, v =>
                    {
                        rotAngle = v;
                        card.style.rotate = new Rotate(new Angle(v, AngleUnit.Degree));
                    }, facingAngle, windupDuration).SetEase(Ease.OutSine));

                // Phase 2: 直線突撃
                cardSeq.Append(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, targetLeft, flyDuration).SetEase(Ease.InCubic));
                cardSeq.Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, targetTop, flyDuration).SetEase(Ease.InCubic));

                // Phase 3: ノックバック（着弾後に跳ね返る）
                float kbT = 0f;
                Vector2 kbEnd = toCenter - dir * knockbackDist;
                cardSeq.Append(DOTween.To(() => kbT, v =>
                {
                    kbT = v;
                    Vector2 pos = Vector2.Lerp(toCenter, kbEnd, v);
                    card.style.left = pos.x - CardWidth / 2f;
                    card.style.top = pos.y - CardHeight / 2f;
                }, 1f, knockbackDuration).SetEase(Ease.OutQuad));

                // 両サイドの1枚目は同時、以降はずらす
                masterSeq.Insert(stagger * i, cardSeq);
            }

            masterSeq.OnComplete(() => tcs.TrySetResult());
            ct.Register(() => { masterSeq.Kill(); tcs.TrySetCanceled(); });

            try
            {
                await tcs.Task;
            }
            catch (OperationCanceledException) { }

            foreach (CardView card in cards)
            {
                if (card.parent == _dragLayer)
                {
                    _dragLayer.Remove(card);
                }
            }
        }

        private async UniTask FlyCardToDestAsync(CardView card, Rect fromWorldRect, VisualElement dest, CancellationToken ct)
        {
            card.style.position = Position.Absolute;
            card.style.left = fromWorldRect.center.x - CardWidth / 2f;
            card.style.top = fromWorldRect.center.y - CardHeight / 2f;
            card.style.width = StyleKeyword.Null;
            card.style.height = StyleKeyword.Null;
            card.style.rotate = new Rotate(0);
            card.style.scale = new Scale(Vector3.one);
            card.style.transformOrigin = StyleKeyword.Null;
            card.style.marginLeft = StyleKeyword.Null;
            card.style.marginRight = StyleKeyword.Null;
            _dragLayer.Add(card);

            Rect destRect = dest.worldBound;
            float targetLeft = destRect.center.x - CardWidth / 2f;
            float targetTop = destRect.center.y - CardHeight / 2f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, targetLeft, CpuCardFlyDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, targetTop, CpuCardFlyDuration).SetEase(Ease.OutQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try
            {
                await tcs.Task;
            }
            catch (OperationCanceledException) { }

            _dragLayer.Remove(card);
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
