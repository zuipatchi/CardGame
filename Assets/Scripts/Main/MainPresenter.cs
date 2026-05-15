using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Common.Deck;
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
        private DeckModel _deckModel;
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
        private Label _playerDamageFormulaLabel;
        private VisualElement _opponentAtkCounterOverlay;
        private Label _opponentAtkCounterLabel;
        private Label _opponentDamageFormulaLabel;
        private VisualElement _dragLayer;

        private readonly HashSet<CardView> _cpuCards = new HashSet<CardView>();
        private bool _isGameOver;

        private int _playerAtkBoost;
        private int _opponentAtkBoost;
        private int _playerDefBoost;
        private int _opponentDefBoost;

        // キャラセットフェーズの入力待ち（null=パス、card=スロットに置くカード）
        private UniTaskCompletionSource<CardView> _charSetInputTcs;
        private CardView _stagedCharSetCard;

        // 準備フェーズの入力待ち（null=パス、card=Ready するカード）
        private UniTaskCompletionSource<CardView> _prepInputTcs;
        private CardView _stagedPrepCard;

        // 戦闘前フェーズの入力待ち（null=パス、card=プレイしたカード）
        private UniTaskCompletionSource<CardView> _preBattleInputTcs;
        private CardView _stagedPreBattleCard;
        private bool _isLocalPreBattleActive;

        [Inject]
        public void Construct(CardStore cardStore, CardDatabase cardDatabase, DeckModel deckModel, GameModel gameModel)
        {
            _cardStore = cardStore;
            _cardDatabase = cardDatabase;
            _deckModel = deckModel;
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
                CardData[] playerDeckFull = _deckModel.Count > 0
                    ? _cardDatabase.BuildDeck(_deckModel.CardIds)
                    : allCards;
                int handSize = Mathf.Min(InitialHandSize, playerDeckFull.Length);

                CardData[] playerShuffled = Shuffle((CardData[])playerDeckFull.Clone());
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
                _playerDamageFormulaLabel = new Label();
                _playerDamageFormulaLabel.pickingMode = PickingMode.Ignore;
                _playerDamageFormulaLabel.AddToClassList("atk-counter-damage-label");
                _playerAtkCounterOverlay.Add(_playerDamageFormulaLabel);
                _playerFieldView.Add(_playerAtkCounterOverlay);

                _opponentAtkCounterOverlay = new VisualElement();
                _opponentAtkCounterOverlay.AddToClassList("atk-counter-overlay");
                _opponentAtkCounterOverlay.pickingMode = PickingMode.Ignore;
                _opponentAtkCounterOverlay.style.display = DisplayStyle.None;
                _opponentAtkCounterLabel = new Label("0");
                _opponentAtkCounterLabel.pickingMode = PickingMode.Ignore;
                _opponentAtkCounterLabel.AddToClassList("atk-counter-label");
                _opponentAtkCounterOverlay.Add(_opponentAtkCounterLabel);
                _opponentDamageFormulaLabel = new Label();
                _opponentDamageFormulaLabel.pickingMode = PickingMode.Ignore;
                _opponentDamageFormulaLabel.AddToClassList("atk-counter-damage-label");
                _opponentAtkCounterOverlay.Add(_opponentDamageFormulaLabel);
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
                await RunCharacterSetPhaseAsync(ct);
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

            _gameModel.BeginPreBattle1();
            await RunPreBattle1PhaseAsync(isLocalTurn, ct);
            if (_isGameOver)
            {
                return;
            }

            _gameModel.BeginPreBattle2();
            await RunPreBattle2PhaseAsync(ct);
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

            _gameModel.BeginBattle();
            await RunBattlePhaseAsync(ct);

            _gameModel.EndTurn();
        }

        // ─── キャラセットフェーズ（ゲーム開始時1回のみ） ───────────────────────

        private async UniTask RunCharacterSetPhaseAsync(CancellationToken ct)
        {
            await PlayAnnouncementAsync("SET CHARACTERS", "turn-announcement-label--character", ct);

            bool isLocalFirst = _gameModel.IsLocalTurn;

            for (int i = 0; i < 2; i++)
            {
                bool isLocalTurn = (i == 0) ? isLocalFirst : !isLocalFirst;

                if (isLocalTurn)
                {
                    CardView placed = await WaitForPlayerCharSetInputAsync(ct);
                    if (placed != null)
                    {
                        await placed.FlipAsync(ct);
                    }
                }
                else
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(CpuThinkSeconds), cancellationToken: ct);
                    IReadOnlyList<CardView> cpuHand = _opponentHandView.Cards;
                    int idx = -1;
                    for (int j = 0; j < cpuHand.Count; j++)
                    {
                        if (cpuHand[j].Data is CharacterCardData)
                        {
                            idx = j;
                            break;
                        }
                    }

                    if (idx >= 0)
                    {
                        CardView card = cpuHand[idx];
                        Rect fromRect = card.worldBound;
                        _opponentHandView.RemoveCard(card);
                        await FlyCardToDestAsync(card, fromRect, _opponentCharacterSlot, ct);
                        _opponentCharacterSlot.PlaceCard(card);
                    }
                }
            }

            await PlayResolveAnimationAsync(ct);

            if (_playerCharacterSlot.CurrentCard != null)
            {
                await _playerCharacterSlot.CurrentCard.FlipAsync(ct);
            }
            if (_opponentCharacterSlot.CurrentCard != null)
            {
                await _opponentCharacterSlot.CurrentCard.FlipAsync(ct);
            }
        }

        private async UniTask<CardView> WaitForPlayerCharSetInputAsync(CancellationToken ct)
        {
            _charSetInputTcs = new UniTaskCompletionSource<CardView>();
            _stagedCharSetCard = null;
            ShowActionButtons();
            UpdateStagedButtons(_stagedCharSetCard != null);

            try
            {
                return await _charSetInputTcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _charSetInputTcs = null;
                HideActionButtons();
            }
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

            CheckGameOver();
        }

        // ─── 戦闘前1フェーズ（Skill/Character 裏向き1枚）─────────────────────

        private async UniTask RunPreBattle1PhaseAsync(bool isLocalTurn, CancellationToken ct)
        {
            await PlayAnnouncementAsync("SET CARDS", "turn-announcement-label--skill", ct);

            bool isLocalFirst = isLocalTurn;

            for (int i = 0; i < 2; i++)
            {
                bool isLocalAct = (i == 0) ? isLocalFirst : !isLocalFirst;

                if (isLocalAct)
                {
                    await WaitForPlayerPreBattle1TurnAsync(ct);
                }
                else
                {
                    await RunCpuPreBattle1SubTurnAsync(ct);
                }
            }
        }

        private async UniTask WaitForPlayerPreBattle1TurnAsync(CancellationToken ct)
        {
            _isLocalPreBattleActive = true;
            _preBattleInputTcs = new UniTaskCompletionSource<CardView>();
            _stagedPreBattleCard = null;
            ShowActionButtons();
            UpdateStagedButtons(_stagedPreBattleCard != null);

            try
            {
                await _preBattleInputTcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _isLocalPreBattleActive = false;
                _preBattleInputTcs = null;
                HideActionButtons();
            }
        }

        private async UniTask RunCpuPreBattle1SubTurnAsync(CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(CpuThinkSeconds), cancellationToken: ct);
            IReadOnlyList<CardView> cpuHand = _opponentHandView.Cards;
            int idx = CpuAgent.ChoosePreBattle1CardIndex(cpuHand.Select(c => c.Data).ToList());

            if (idx < 0)
            {
                return;
            }

            CardView card = cpuHand[idx];
            Rect fromRect = card.worldBound;
            _opponentHandView.RemoveCard(card);
            await FlyCardToDestAsync(card, fromRect, _opponentFieldView, ct);
            _opponentFieldView.PlaceCard(card);
        }

        // ─── 戦闘前2フェーズ（Event のみ・交互・2連続パス）──────────────────

        private async UniTask RunPreBattle2PhaseAsync(CancellationToken ct)
        {
            await PlayAnnouncementAsync("SET EVENTS", "turn-announcement-label--event", ct);

            while (true)
            {
                if (_gameModel.IsLocalPreparationTurn)
                {
                    CardView readied = await WaitForPlayerPreBattle2InputAsync(ct);
                    if (readied == null)
                    {
                        if (_gameModel.Pass())
                        {
                            break;
                        }
                    }
                    else
                    {
                        _playerFieldView.PlaceCard(readied);
                        _gameModel.ReadyCard(readied);
                    }
                }
                else
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(CpuThinkSeconds), cancellationToken: ct);
                    IReadOnlyList<CardView> cpuHand = _opponentHandView.Cards;
                    int idx = CpuAgent.ChooseEventCardIndex(cpuHand.Select(c => c.Data).ToList());

                    if (idx >= 0)
                    {
                        CardView card = cpuHand[idx];
                        Rect fromRect = card.worldBound;
                        _opponentHandView.RemoveCard(card);
                        await FlyCardToDestAsync(card, fromRect, _opponentFieldView, ct);
                        _opponentFieldView.PlaceCard(card);
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

        private async UniTask<CardView> WaitForPlayerPreBattle2InputAsync(CancellationToken ct)
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
                    if (card.Data is EventCardData eventData)
                    {
                        ApplyEventEffect(eventData, isLocal);
                    }

                    FieldView field = isLocal ? _playerFieldView : _opponentFieldView;
                    field.RemoveCard(card);
                    GraveyardView graveyard = isLocal ? _playerGraveyardView : _opponentGraveyardView;
                    graveyard.AddCard(card);
                }

                card.SetState(CardState.Normal);
            }
        }

        private void ApplyEventEffect(EventCardData data, bool isLocal)
        {
            switch (data.EffectType)
            {
                case EffectType.AtkBoost:
                    if (isLocal)
                    {
                        _playerAtkBoost += data.EffectValue;
                    }
                    else
                    {
                        _opponentAtkBoost += data.EffectValue;
                    }
                    break;
                case EffectType.DefBoost:
                    if (isLocal)
                    {
                        _playerDefBoost += data.EffectValue;
                    }
                    else
                    {
                        _opponentDefBoost += data.EffectValue;
                    }
                    break;
            }
        }

        // ─── 戦闘フェーズ ────────────────────────────────────────────────

        private async UniTask RunBattlePhaseAsync(CancellationToken ct)
        {
            List<CardView> playerCards = _playerFieldView.Cards.ToList();
            List<CardView> opponentCards = _opponentFieldView.Cards.ToList();

            if (playerCards.Count == 0 && opponentCards.Count == 0)
            {
                return;
            }

            await PlayAnnouncementAsync("FIGHT", "turn-announcement-label--fight", ct);

            // 全フィールドカードを同時に表向き
            UniTask[] flipTasks = playerCards.Concat(opponentCards).Select(c => c.FlipAsync(ct)).ToArray();
            await UniTask.WhenAll(flipTasks);

            await UniTask.Delay(TimeSpan.FromSeconds(0.3f), cancellationToken: ct);

            // キャラカードをスロットへ飛翔（両者同時、DEF更新）
            List<(CardView Card, Rect FromRect, CharacterSlotView Slot)> charMoves
                = new List<(CardView, Rect, CharacterSlotView)>();

            foreach (CardView c in playerCards)
            {
                if (c.Data is CharacterCardData)
                {
                    charMoves.Add((c, c.worldBound, _playerCharacterSlot));
                    _playerFieldView.RemoveCard(c);
                }
            }
            foreach (CardView c in opponentCards)
            {
                if (c.Data is CharacterCardData)
                {
                    charMoves.Add((c, c.worldBound, _opponentCharacterSlot));
                    _opponentFieldView.RemoveCard(c);
                }
            }

            if (charMoves.Count > 0)
            {
                UniTask[] moveTasks = new UniTask[charMoves.Count];
                for (int i = 0; i < charMoves.Count; i++)
                {
                    moveTasks[i] = FlyCharToSlotAsync(charMoves[i].Card, charMoves[i].FromRect, charMoves[i].Slot, ct);
                }
                await UniTask.WhenAll(moveTasks);
            }

            // 技カードのみでダメージ計算
            List<CardView> playerSkill = playerCards.Where(c => c.Data is SkillCardData).ToList();
            List<CardView> opponentSkill = opponentCards.Where(c => c.Data is SkillCardData).ToList();

            if (playerSkill.Count > 0 || opponentSkill.Count > 0)
            {
                int playerATK = playerSkill.Sum(c => c.Data.Attack) + _playerAtkBoost;
                int opponentATK = opponentSkill.Sum(c => c.Data.Attack) + _opponentAtkBoost;
                int effectivePlayerDef = _playerCharacterSlot.Defense + _playerDefBoost;
                int effectiveOpponentDef = _opponentCharacterSlot.Defense + _opponentDefBoost;
                int damageToOpponent = Mathf.Max(0, playerATK - effectiveOpponentDef);
                int damageToPlayer = Mathf.Max(0, opponentATK - effectivePlayerDef);

                await PlayAtkCounterAsync(playerATK, opponentATK, effectiveOpponentDef, effectivePlayerDef, damageToOpponent, damageToPlayer, ct);

                await UniTask.WhenAll(
                    PlaySkillCardsAttackAsync(playerSkill, _playerFieldView, _opponentDeckView, ct),
                    PlaySkillCardsAttackAsync(opponentSkill, _opponentFieldView, _playerDeckView, ct)
                );

                if (damageToOpponent > 0)
                {
                    _opponentDeckView.RemoveFromTop(damageToOpponent);
                }
                if (damageToPlayer > 0)
                {
                    _playerDeckView.RemoveFromTop(damageToPlayer);
                }
            }

            _playerAtkBoost = 0;
            _opponentAtkBoost = 0;
            _playerDefBoost = 0;
            _opponentDefBoost = 0;

            CheckGameOver();

            // 技カードを墓地へ（キャラカードはスロット済み）
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

            if (_gameModel.Phase == TurnPhase.CharacterSet && _charSetInputTcs != null)
            {
                if (card.Data is not CharacterCardData || _stagedCharSetCard != null)
                {
                    return false;
                }

                if (!_playerCharacterSlot.worldBound.Contains(worldPos))
                {
                    return false;
                }

                _playerCharacterSlot.PlaceCard(card);
                _stagedCharSetCard = card;
                UpdateStagedButtons(_stagedCharSetCard != null);
                return true;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle1 && _isLocalPreBattleActive)
            {
                if ((card.Data is not SkillCardData && card.Data is not CharacterCardData) || _stagedPreBattleCard != null)
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

            if (_gameModel.Phase == TurnPhase.PreBattle2 && _gameModel.IsLocalPreparationTurn)
            {
                if (card.Data is not EventCardData || _stagedPrepCard != null)
                {
                    return false;
                }

                if (!_playerFieldView.worldBound.Contains(worldPos))
                {
                    return false;
                }

                _stagedPrepCard = card;
                UpdateStagedButtons(_stagedPrepCard != null);
                return true;
            }

            return false;
        }

        private void OnOkClicked()
        {
            if (_gameModel.Phase == TurnPhase.CharacterSet && _charSetInputTcs != null)
            {
                if (_stagedCharSetCard == null)
                {
                    return;
                }

                CardView card = _stagedCharSetCard;
                _stagedCharSetCard = null;
                _charSetInputTcs.TrySetResult(card);
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle1 && _preBattleInputTcs != null)
            {
                if (_stagedPreBattleCard == null)
                {
                    return;
                }

                CardView card = _stagedPreBattleCard;
                _stagedPreBattleCard = null;
                _preBattleInputTcs.TrySetResult(card);
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle2 && _prepInputTcs != null)
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
        }

        private void OnBackClicked()
        {
            if (_gameModel.Phase == TurnPhase.CharacterSet && _charSetInputTcs != null)
            {
                if (_stagedCharSetCard != null)
                {
                    Rect rect = _stagedCharSetCard.worldBound;
                    _playerCharacterSlot.RemoveCard();
                    _handView.AddCardBackAsync(_stagedCharSetCard, rect, destroyCancellationToken).Forget();
                    _stagedCharSetCard = null;
                    UpdateStagedButtons(_stagedCharSetCard != null);
                }
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle1 && _preBattleInputTcs != null)
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
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle2 && _prepInputTcs != null)
            {
                if (_stagedPrepCard != null)
                {
                    Rect rect = _stagedPrepCard.worldBound;
                    _handView.AddCardBackAsync(_stagedPrepCard, rect, destroyCancellationToken).Forget();
                    _stagedPrepCard = null;
                    UpdateStagedButtons(_stagedPrepCard != null);
                    return;
                }

                // ステージなし = パス
                HideActionButtons();
                _prepInputTcs.TrySetResult(null);
            }
        }

        private void OnPassClicked()
        {
            if (_gameModel.Phase == TurnPhase.CharacterSet && _charSetInputTcs != null && _stagedCharSetCard == null)
            {
                _charSetInputTcs.TrySetResult(null);
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle1 && _preBattleInputTcs != null && _stagedPreBattleCard == null)
            {
                _preBattleInputTcs.TrySetResult(null);
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle2 && _prepInputTcs != null && _stagedPrepCard == null)
            {
                HideActionButtons();
                _prepInputTcs.TrySetResult(null);
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

        private void CheckGameOver()
        {
            if (_opponentDeckView.Count == 0 || _playerDeckView.Count == 0)
            {
                _isGameOver = true;
                bool bothZero = _opponentDeckView.Count == 0 && _playerDeckView.Count == 0;
                OnGameEnd(bothZero ? (bool?)null : _opponentDeckView.Count == 0);
            }
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
            _turnLabel.RemoveFromClassList("turn-announcement-label--character");
            _turnLabel.RemoveFromClassList("turn-announcement-label--event");
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

        private async UniTask PlayAtkCounterAsync(
            int playerAtk, int opponentAtk,
            int opponentDef, int playerDef,
            int damageToOpponent, int damageToPlayer,
            CancellationToken ct)
        {
            const float countDuration = 0.8f;
            const float holdDuration = 0.3f;
            const float formulaHoldDuration = 0.8f;
            const float fadeDuration = 0.3f;

            _playerAtkCounterOverlay.BringToFront();
            _opponentAtkCounterOverlay.BringToFront();
            _playerAtkCounterLabel.text = "0";
            _opponentAtkCounterLabel.text = "0";
            _playerDamageFormulaLabel.text = string.Empty;
            _opponentDamageFormulaLabel.text = string.Empty;
            _playerAtkCounterOverlay.style.display = DisplayStyle.Flex;
            _opponentAtkCounterOverlay.style.display = DisplayStyle.Flex;
            _playerAtkCounterOverlay.style.opacity = 0f;
            _opponentAtkCounterOverlay.style.opacity = 0f;

            float playerVal = 0f;
            float opponentVal = 0f;

            bool showPlayerFormula = opponentDef > 0;
            bool showOpponentFormula = playerDef > 0;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => _playerAtkCounterOverlay.style.opacity.value, v => _playerAtkCounterOverlay.style.opacity = v, 1f, 0.2f))
                .Join(DOTween.To(() => _opponentAtkCounterOverlay.style.opacity.value, v => _opponentAtkCounterOverlay.style.opacity = v, 1f, 0.2f))
                .Join(DOTween.To(() => playerVal, v => { playerVal = v; _playerAtkCounterLabel.text = Mathf.RoundToInt(v).ToString(); }, (float)playerAtk, countDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => opponentVal, v => { opponentVal = v; _opponentAtkCounterLabel.text = Mathf.RoundToInt(v).ToString(); }, (float)opponentAtk, countDuration).SetEase(Ease.OutQuad))
                .AppendInterval(holdDuration);

            if (showPlayerFormula || showOpponentFormula)
            {
                seq.AppendCallback(() =>
                {
                    if (showPlayerFormula)
                    {
                        _playerDamageFormulaLabel.text = $"{playerAtk} - {opponentDef} = {damageToOpponent}";
                    }
                    if (showOpponentFormula)
                    {
                        _opponentDamageFormulaLabel.text = $"{opponentAtk} - {playerDef} = {damageToPlayer}";
                    }
                })
                .AppendInterval(formulaHoldDuration);
            }

            seq.Append(DOTween.To(() => _playerAtkCounterOverlay.style.opacity.value, v => _playerAtkCounterOverlay.style.opacity = v, 0f, fadeDuration))
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

                masterSeq.Append(cardSeq);
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

        private async UniTask FlyCharToSlotAsync(CardView card, Rect fromRect, CharacterSlotView slot, CancellationToken ct)
        {
            await FlyCardToDestAsync(card, fromRect, slot, ct);
            slot.PlaceCard(card);
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
