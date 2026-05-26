using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using Main.Game;
using CardEventType = Main.Card.EventType;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // ─── ゲームループ ───────────────────────────────────────────────

        private async UniTaskVoid RunGameAsync(bool isLocalFirst, CancellationToken ct)
        {
            try
            {
                _gameModel.SetInitialTurn(isLocalFirst);
                await PlayCoinTossAsync(isLocalFirst, ct);

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
            UpdatePhaseIndicator(TurnPhase.CharacterSet);
            await PlayAnnouncementAsync("キャラセットフェーズ", "turn-announcement-label--character", ct);

            bool isLocalFirst = _gameModel.IsLocalTurn;

            for (int i = 0; i < 2; i++)
            {
                bool isLocalTurn = (i == 0) ? isLocalFirst : !isLocalFirst;

                if (isLocalTurn)
                {
                    CardView placed = await WaitForPlayerCharSetInputAsync(ct);
                    if (placed == null)
                    {
                        await PlayPassAnimationAsync(true, ct);
                        if (_isOnline)
                        {
                            _networkGameService.SendCharSetAction(null);
                        }
                    }
                    else if (_isOnline)
                    {
                        _networkGameService.SendCharSetAction(placed.Data.Id);
                    }
                }
                else if (_isOnline)
                {
                    string opponentCardId = await _networkGameService.WaitForOpponentCharSetAsync(ct);
                    await PlayOpponentCharSetOnlineAsync(opponentCardId, ct);
                }
                else
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(CpuThinkSeconds), cancellationToken: ct);
                    IReadOnlyList<CardView> cpuHand = _opponentHandView.Cards;
                    int idx = CpuAgent.ChooseCharacterSetCardIndex(cpuHand.Select(c => c.Data).ToList());

                    if (idx >= 0)
                    {
                        CardView card = cpuHand[idx];
                        Rect fromRect = card.worldBound;
                        _opponentHandView.RemoveCard(card);
                        await FlyCardToDestAsync(card, fromRect, _opponentCharacterSlot, ct);
                        _opponentCharacterSlot.PlaceCard(card);
                        await PlayOkFlashAsync(false, ct);
                    }
                    else
                    {
                        await PlayPassAnimationAsync(false, ct);
                    }
                }
            }

            await PlayResolveAnimationAsync(ct);

            if (_playerCharacterSlot.CurrentCard != null)
            {
                await _playerCharacterSlot.CurrentCard.FlipAsync(ct);
                await PayCostAsync(_playerCharacterSlot.CurrentCard, _playerDeckView, _playerGraveyardView, ct);
                if (_isGameOver) return;
            }
            if (_opponentCharacterSlot.CurrentCard != null)
            {
                await _opponentCharacterSlot.CurrentCard.FlipAsync(ct);
                await PayCostAsync(_opponentCharacterSlot.CurrentCard, _opponentDeckView, _opponentGraveyardView, ct);
            }
        }

        private async UniTask<CardView> WaitForPlayerCharSetInputAsync(CancellationToken ct)
        {
            _charSetInput.Tcs = new UniTaskCompletionSource<CardView>();
            _charSetInput.Card = null;
            ShowActionButtons();
            UpdateStagedButtons(false);

            try
            {
                return await _charSetInput.Tcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _charSetInput.Tcs = null;
                HideActionButtons();
                RefreshHandHighlights();
            }
        }

        private async UniTask PlayOpponentCharSetOnlineAsync(string cardId, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(cardId))
            {
                await PlayPassAnimationAsync(false, ct);
                return;
            }

            if (!_cardDatabase.TryGet(cardId, out CardData cardData))
            {
                await PlayPassAnimationAsync(false, ct);
                return;
            }

            IReadOnlyList<CardView> hand = _opponentHandView.Cards;
            Rect fromRect = hand.Count > 0 ? hand[0].worldBound : _opponentHandView.worldBound;
            if (hand.Count > 0)
            {
                _opponentHandView.RemoveCard(hand[0]);
            }

            CardView card = new CardView(_cardStore.CardTemplate, cardData, _cardStore.CardBack, faceDown: true, _cardStore.AttributeDatabase, isOpponent: true);
            await FlyCardToDestAsync(card, fromRect, _opponentCharacterSlot, ct);
            _opponentCharacterSlot.PlaceCard(card);
            await PlayOkFlashAsync(false, ct);
        }

        // ─── ドローフェーズ ─────────────────────────────────────────────

        private async UniTask RunDrawPhaseAsync(bool isLocalTurn, CancellationToken ct)
        {
            UpdatePhaseIndicator(TurnPhase.Draw);
            DeckView sourceDeck = isLocalTurn ? _playerDeckView : _opponentDeckView;

            if (sourceDeck.Count == 0)
            {
                _isGameOver = true;
                OnGameEnd(isLocalTurn ? (bool?)false : true);
                return;
            }

            Rect deckRect = sourceDeck.worldBound;
            CardData drawn = sourceDeck.DrawTop();

            await PlayTurnAnnouncementAsync(isLocalTurn, ct);

            sourceDeck.RefreshCount();
            if (drawn != null)
            {
                if (isLocalTurn)
                {
                    await _handView.AddCardAnimatedAsync(drawn, deckRect, 0f, ct);
                    if (_isOnline)
                    {
                        _networkGameService.SendDrawNotification();
                    }
                }
                else
                {
                    if (_isOnline)
                    {
                        await _networkGameService.WaitForOpponentDrawAsync(ct);
                    }
                    await PlayCpuDrawAsync(drawn, deckRect, ct);
                }
            }
        }

        // ─── 戦闘前1フェーズ（Skill/Character 裏向き1枚）─────────────────────

        private async UniTask RunPreBattle1PhaseAsync(bool isLocalTurn, CancellationToken ct)
        {
            UpdatePhaseIndicator(TurnPhase.PreBattle1);
            await PlayAnnouncementAsync("準備フェーズ", "turn-announcement-label--skill", ct);

            bool isLocalFirst = isLocalTurn;

            for (int i = 0; i < 2; i++)
            {
                bool isLocalAct = (i == 0) ? isLocalFirst : !isLocalFirst;

                if (isLocalAct)
                {
                    CardView placed = await WaitForPlayerPreBattle1TurnAsync(ct);
                    if (_isOnline)
                    {
                        _networkGameService.SendPreBattle1Action(placed?.Data.Id);
                    }
                    if (placed == null)
                    {
                        await PlayPassAnimationAsync(true, ct);
                    }
                }
                else
                {
                    if (_isOnline)
                    {
                        string cardId = await _networkGameService.WaitForOpponentPreBattle1Async(ct);
                        await PlayOpponentPreBattle1OnlineAsync(cardId, ct);
                    }
                    else
                    {
                        await RunCpuPreBattle1SubTurnAsync(ct);
                    }
                }
            }
        }

        private async UniTask<CardView> WaitForPlayerPreBattle1TurnAsync(CancellationToken ct)
        {
            _isLocalPreBattleActive = true;
            _preBattleInput.Tcs = new UniTaskCompletionSource<CardView>();
            _preBattleInput.Card = null;
            ShowActionButtons();
            UpdateStagedButtons(false);

            CardView result = null;
            try
            {
                result = await _preBattleInput.Tcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _isLocalPreBattleActive = false;
                _preBattleInput.Tcs = null;
                HideActionButtons();
                RefreshHandHighlights();
            }

            return result;
        }

        private async UniTask RunCpuPreBattle1SubTurnAsync(CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(CpuThinkSeconds), cancellationToken: ct);
            IReadOnlyList<CardView> cpuHand = _opponentHandView.Cards;
            int idx = CpuAgent.ChoosePreBattle1CardIndex(cpuHand.Select(c => c.Data).ToList());

            if (idx < 0)
            {
                await PlayPassAnimationAsync(false, ct);
                return;
            }

            CardView card = cpuHand[idx];
            Rect fromRect = card.worldBound;
            _opponentHandView.RemoveCard(card);
            await FlyCardToDestAsync(card, fromRect, _opponentFieldView, ct);
            _opponentFieldView.PlaceCard(card);
            await PlayOkFlashAsync(false, ct);
        }

        private async UniTask PlayOpponentPreBattle1OnlineAsync(string cardId, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(cardId))
            {
                await PlayPassAnimationAsync(false, ct);
                return;
            }
            if (!_cardDatabase.TryGet(cardId, out CardData cardData))
            {
                await PlayPassAnimationAsync(false, ct);
                return;
            }
            IReadOnlyList<CardView> hand = _opponentHandView.Cards;
            Rect fromRect = hand.Count > 0 ? hand[0].worldBound : _opponentHandView.worldBound;
            if (hand.Count > 0)
            {
                _opponentHandView.RemoveCard(hand[0]);
            }
            CardView card = new CardView(_cardStore.CardTemplate, cardData, _cardStore.CardBack, faceDown: true, _cardStore.AttributeDatabase, isOpponent: true);
            await FlyCardToDestAsync(card, fromRect, _opponentFieldView, ct);
            _opponentFieldView.PlaceCard(card);
            await PlayOkFlashAsync(false, ct);
        }

        // ─── 戦闘前2フェーズ（Event のみ・交互・2連続パス）──────────────────

        private async UniTask RunPreBattle2PhaseAsync(CancellationToken ct)
        {
            UpdatePhaseIndicator(TurnPhase.PreBattle2);
            await PlayAnnouncementAsync("イベントフェーズ", "turn-announcement-label--event", ct);

            while (true)
            {
                if (_gameModel.IsLocalPreparationTurn)
                {
                    CardView readied = await WaitForPlayerPreBattle2InputAsync(ct);
                    if (_isOnline)
                    {
                        _networkGameService.SendPreBattle2Action(readied?.Data.Id);
                    }
                    if (readied == null)
                    {
                        await PlayPassAnimationAsync(true, ct);
                        if (_gameModel.Pass())
                        {
                            break;
                        }
                    }
                    else
                    {
                        _gameModel.ReadyCard(readied);
                        readied.SetChainNumber(_gameModel.ReadyQueue.Count);
                        await PayCostAsync(readied, _playerDeckView, _playerGraveyardView, ct);
                        if (_isGameOver) break;
                    }
                }
                else
                {
                    if (_isOnline)
                    {
                        string cardId = await _networkGameService.WaitForOpponentPreBattle2Async(ct);
                        if (string.IsNullOrEmpty(cardId))
                        {
                            await PlayPassAnimationAsync(false, ct);
                            if (_gameModel.Pass())
                            {
                                break;
                            }
                        }
                        else
                        {
                            await PlayOpponentPreBattle2OnlineAsync(cardId, ct);
                            if (_isGameOver) break;
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
                            card.SetChainNumber(_gameModel.ReadyQueue.Count);
                            await PlayOkFlashAsync(false, ct);
                            await PayCostAsync(card, _opponentDeckView, _opponentGraveyardView, ct);
                            if (_isGameOver) break;
                        }
                        else
                        {
                            await PlayPassAnimationAsync(false, ct);
                            if (_gameModel.Pass())
                            {
                                break;
                            }
                        }
                    }
                }
            }

            HideActionButtons();
        }

        private async UniTask PlayOpponentPreBattle2OnlineAsync(string cardId, CancellationToken ct)
        {
            if (!_cardDatabase.TryGet(cardId, out CardData cardData))
            {
                return;
            }
            IReadOnlyList<CardView> hand = _opponentHandView.Cards;
            Rect fromRect = hand.Count > 0 ? hand[0].worldBound : _opponentHandView.worldBound;
            if (hand.Count > 0)
            {
                _opponentHandView.RemoveCard(hand[0]);
            }
            CardView card = new CardView(_cardStore.CardTemplate, cardData, _cardStore.CardBack, faceDown: true, _cardStore.AttributeDatabase, isOpponent: true);
            await FlyCardToDestAsync(card, fromRect, _opponentFieldView, ct);
            _opponentFieldView.PlaceCard(card);
            await card.FlipAsync(ct);
            _gameModel.ReadyCard(card);
            card.SetChainNumber(_gameModel.ReadyQueue.Count);
            await PlayOkFlashAsync(false, ct);
            await PayCostAsync(card, _opponentDeckView, _opponentGraveyardView, ct);
        }

        private async UniTask<CardView> WaitForPlayerPreBattle2InputAsync(CancellationToken ct)
        {
            _prepInput.Tcs = new UniTaskCompletionSource<CardView>();
            _prepInput.Card = null;
            ShowActionButtons();
            UpdateStagedButtons(false);

            try
            {
                return await _prepInput.Tcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _prepInput.Tcs = null;
                RefreshHandHighlights();
            }
        }

        // ─── 解決フェーズ ────────────────────────────────────────────────

        private async UniTask RunResolutionPhaseAsync(CancellationToken ct)
        {
            UpdatePhaseIndicator(TurnPhase.Resolution);
            IReadOnlyList<CardView> queue = _gameModel.ReadyQueue;
            if (queue.Count == 0)
            {
                return;
            }

            await PlayResolveAnimationAsync(ct);

            bool skipNextEffect = false;

            for (int i = queue.Count - 1; i >= 0; i--)
            {
                CardView card = queue[i];
                card.SetChainNumber(0);
                card.SetState(CardState.Resolve);
                await UniTask.Delay(TimeSpan.FromSeconds(0.3f), cancellationToken: ct);

                bool isLocal = !card.IsOpponent;

                if (card.Data is CharacterCardData)
                {
                    // 準備フェーズでスロット配置済み（プレイヤー・CPU 共通）
                }
                else
                {
                    if (card.Data is EventCardData eventData)
                    {
                        if (skipNextEffect)
                        {
                            skipNextEffect = false;
                        }
                        else if (eventData.EventType == CardEventType.Negate)
                        {
                            skipNextEffect = true;
                        }
                        else
                        {
                            await ApplyEventEffectAsync(eventData, isLocal, ct);
                        }
                    }

                    if (_isGameOver)
                    {
                        break;
                    }

                    FieldView field = isLocal ? _playerFieldView : _opponentFieldView;
                    field.RemoveCard(card);
                    GraveyardView graveyard = isLocal ? _playerGraveyardView : _opponentGraveyardView;
                    graveyard.AddCard(card);
                }

                card.SetState(CardState.Normal);

                if (_isGameOver)
                {
                    break;
                }
            }
        }

        private async UniTask ApplyEventEffectAsync(EventCardData data, bool isLocal, CancellationToken ct)
        {
            switch (data.EventType)
            {
                case CardEventType.AtkBoost:
                    if (isLocal)
                    {
                        _playerAtkBoost += data.EventValue;
                    }
                    else
                    {
                        _opponentAtkBoost += data.EventValue;
                    }
                    break;
                case CardEventType.DefBoost:
                    if (isLocal)
                    {
                        _playerDefBoost += data.EventValue;
                    }
                    else
                    {
                        _opponentDefBoost += data.EventValue;
                    }
                    break;
                case CardEventType.Draw:
                    await ApplyDrawEffectAsync(data.EventValue, isLocal, ct);
                    break;
                case CardEventType.BanishChar:
                {
                    CharacterSlotView targetSlot = isLocal ? _opponentCharacterSlot : _playerCharacterSlot;
                    GraveyardView targetGraveyard = isLocal ? _opponentGraveyardView : _playerGraveyardView;
                    CardView charCard = targetSlot.CurrentCard;
                    if (charCard != null)
                    {
                        targetSlot.RemoveCard();
                        targetGraveyard.AddCard(charCard);
                    }
                    break;
                }
            }
        }

        private async UniTask ApplyDrawEffectAsync(int count, bool isLocal, CancellationToken ct)
        {
            if (count <= 0)
            {
                return;
            }

            DeckView deck = isLocal ? _playerDeckView : _opponentDeckView;

            for (int i = 0; i < count; i++)
            {
                if (deck.Count == 0)
                {
                    _isGameOver = true;
                    OnGameEnd(isLocal ? (bool?)false : true);
                    break;
                }

                Rect deckRect = deck.worldBound;
                CardData drawn = deck.DrawTop();
                deck.RefreshCount();

                if (drawn == null)
                {
                    break;
                }

                if (isLocal)
                {
                    await _handView.AddCardAnimatedAsync(drawn, deckRect, 0f, ct);
                }
                else
                {
                    await PlayCpuDrawAsync(drawn, deckRect, ct);
                }
            }
        }

        // ─── 戦闘フェーズ ────────────────────────────────────────────────

        private async UniTask RunBattlePhaseAsync(CancellationToken ct)
        {
            UpdatePhaseIndicator(TurnPhase.Battle);
            List<CardView> playerCards = _playerFieldView.Cards.ToList();
            List<CardView> opponentCards = _opponentFieldView.Cards.ToList();

            if (playerCards.Count == 0 && opponentCards.Count == 0
                && _playerCharacterSlot.CurrentCard == null && _opponentCharacterSlot.CurrentCard == null)
            {
                return;
            }

            await PlayAnnouncementAsync("FIGHT", "turn-announcement-label--fight", ct);

            // 全フィールドカードを同時に表向き
            UniTask[] flipTasks = playerCards.Concat(opponentCards).Select(c => c.FlipAsync(ct)).ToArray();
            await UniTask.WhenAll(flipTasks);

            await UniTask.Delay(TimeSpan.FromSeconds(0.3f), cancellationToken: ct);

            // コスト払い（キャラ・技カード：ターンプレイヤー→相手の順にアナウンス＋支払い）
            bool isLocalTurn = _gameModel.IsLocalTurn;
            List<CardView> firstCards = isLocalTurn ? playerCards : opponentCards;
            DeckView firstDeck = isLocalTurn ? _playerDeckView : _opponentDeckView;
            GraveyardView firstGraveyard = isLocalTurn ? _playerGraveyardView : _opponentGraveyardView;
            List<CardView> secondCards = isLocalTurn ? opponentCards : playerCards;
            DeckView secondDeck = isLocalTurn ? _opponentDeckView : _playerDeckView;
            GraveyardView secondGraveyard = isLocalTurn ? _opponentGraveyardView : _playerGraveyardView;

            string firstCostClass = isLocalTurn ? "turn-announcement-label--cost" : "turn-announcement-label--cost-opponent";
            int firstCost = firstCards.Sum(c => c.Data.Cost);
            await PlayAnnouncementAsync($"PAY {firstCost} COSTS", firstCostClass, ct);
            await UniTask.WhenAll(firstCards.Select(c => PayCostAsync(c, firstDeck, firstGraveyard, ct, announce: false)));
            if (_isGameOver)
            {
                return;
            }

            string secondCostClass = isLocalTurn ? "turn-announcement-label--cost-opponent" : "turn-announcement-label--cost";
            int secondCost = secondCards.Sum(c => c.Data.Cost);
            await PlayAnnouncementAsync($"PAY {secondCost} COSTS", secondCostClass, ct);
            await UniTask.WhenAll(secondCards.Select(c => PayCostAsync(c, secondDeck, secondGraveyard, ct, announce: false)));
            if (_isGameOver)
            {
                return;
            }

            List<CardView> playerFieldChar = playerCards.Where(c => c.Data is CharacterCardData).ToList();
            List<CardView> opponentFieldChar = opponentCards.Where(c => c.Data is CharacterCardData).ToList();
            List<CardView> playerSkill = playerCards.Where(c => c.Data is SkillCardData).ToList();
            List<CardView> opponentSkill = opponentCards.Where(c => c.Data is SkillCardData).ToList();

            // フィールドのキャラをスロットへ飛翔
            List<UniTask> charMoveTasks = new List<UniTask>();
            foreach (CardView c in playerFieldChar)
            {
                Rect fromRect = c.worldBound;
                _playerFieldView.RemoveCard(c);
                charMoveTasks.Add(FlyCharToSlotAsync(c, fromRect, _playerCharacterSlot, ct));
            }
            foreach (CardView c in opponentFieldChar)
            {
                Rect fromRect = c.worldBound;
                _opponentFieldView.RemoveCard(c);
                charMoveTasks.Add(FlyCharToSlotAsync(c, fromRect, _opponentCharacterSlot, ct));
            }
            if (charMoveTasks.Count > 0)
            {
                await UniTask.WhenAll(charMoveTasks);
            }

            // 戦闘前1でキャラを出した場合は攻撃しない（ATK=0・モーションなし）
            bool playerHasAttackingChar = _playerCharacterSlot.CurrentCard != null && playerFieldChar.Count == 0;
            bool opponentHasAttackingChar = _opponentCharacterSlot.CurrentCard != null && opponentFieldChar.Count == 0;

            CardAttribute playerCharAttr = _playerCharacterSlot.CurrentCard?.Data.Attribute ?? CardAttribute.None;
            CardAttribute opponentCharAttr = _opponentCharacterSlot.CurrentCard?.Data.Attribute ?? CardAttribute.None;

            BattleCalculator.SideBattleStats playerStats = BattleCalculator.Calculate(
                playerSkill, playerCharAttr, opponentCharAttr, _playerAtkBoost, playerHasAttackingChar, _cardStore.AttributeDatabase);
            BattleCalculator.SideBattleStats opponentStats = BattleCalculator.Calculate(
                opponentSkill, opponentCharAttr, playerCharAttr, _opponentAtkBoost, opponentHasAttackingChar, _cardStore.AttributeDatabase);

            int effectivePlayerDef = _playerCharacterSlot.Defense + _playerDefBoost;
            int effectiveOpponentDef = _opponentCharacterSlot.Defense + _opponentDefBoost;
            int damageToOpponent = Mathf.Max(0, playerStats.ATK - effectiveOpponentDef);
            int damageToPlayer = Mathf.Max(0, opponentStats.ATK - effectivePlayerDef);

            // ATKカウントアップ表示（技カードはフィールドに残ったまま）
            await PlayAtkCounterAsync(playerStats.ATK, opponentStats.ATK, effectiveOpponentDef, effectivePlayerDef, ct);
            await PlayBattleLabelsAsync(playerStats, opponentStats, ct);

            // 技カードが相手キャラへ突撃
            await UniTask.WhenAll(
                playerSkill.Count > 0
                    ? PlaySkillsAttackCharacterAsync(playerSkill, _playerFieldView, _opponentCharacterSlot, ct)
                    : UniTask.CompletedTask,
                opponentSkill.Count > 0
                    ? PlaySkillsAttackCharacterAsync(opponentSkill, _opponentFieldView, _playerCharacterSlot, ct)
                    : UniTask.CompletedTask
            );

            _playerAtkCounterOverlay.style.display = DisplayStyle.None;
            _opponentAtkCounterOverlay.style.display = DisplayStyle.None;
            _playerCharacterSlot.DefOverlay.style.display = DisplayStyle.None;
            _opponentCharacterSlot.DefOverlay.style.display = DisplayStyle.None;

            // デッキダメージ
            if (damageToOpponent > 0 || damageToPlayer > 0)
            {
                Rect opponentDeckRect = _opponentDeckView.worldBound;
                Rect playerDeckRect = _playerDeckView.worldBound;
                List<CardView> opponentDamageCards = _opponentDeckView.TakeFromTop(damageToOpponent);
                List<CardView> playerDamageCards = _playerDeckView.TakeFromTop(damageToPlayer);
                await UniTask.WhenAll(
                    PlayDeckDamageAsync(opponentDamageCards, opponentDeckRect, _opponentGraveyardView, _opponentDeckView, ct),
                    PlayDeckDamageAsync(playerDamageCards, playerDeckRect, _playerGraveyardView, _playerDeckView, ct)
                );

                bool playerDeckEmpty = damageToPlayer > 0 && _playerDeckView.Count == 0;
                bool opponentDeckEmpty = damageToOpponent > 0 && _opponentDeckView.Count == 0;
                if (playerDeckEmpty || opponentDeckEmpty)
                {
                    SendSkillsToGraveyard(playerSkill, _playerGraveyardView);
                    SendSkillsToGraveyard(opponentSkill, _opponentGraveyardView);
                    _isGameOver = true;
                    if (playerDeckEmpty && opponentDeckEmpty)
                    {
                        OnGameEnd(null);
                    }
                    else
                    {
                        OnGameEnd(!playerDeckEmpty);
                    }
                    return;
                }
            }

            // 技カードを墓地へ
            SendSkillsToGraveyard(playerSkill, _playerGraveyardView);
            SendSkillsToGraveyard(opponentSkill, _opponentGraveyardView);

            _playerAtkBoost = 0;
            _opponentAtkBoost = 0;
            _playerDefBoost = 0;
            _opponentDefBoost = 0;
        }

        private void OnGameEnd(bool? playerWins)
        {
            PlayGameEndAsync(playerWins, destroyCancellationToken).Forget();
        }

        // ─── 戦闘フェーズ ヘルパー ───────────────────────────────────────

        private async UniTask PlayBattleLabelsAsync(
            BattleCalculator.SideBattleStats playerStats,
            BattleCalculator.SideBattleStats opponentStats,
            CancellationToken ct)
        {
            if (!playerStats.TypeMatch && !playerStats.WeaknessHit && !playerStats.StrengthBlocked
                && !opponentStats.TypeMatch && !opponentStats.WeaknessHit && !opponentStats.StrengthBlocked)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1.2f), cancellationToken: ct);
                return;
            }

            const float labelH = 80f;
            const float labelGap = 8f;
            const float stackOffset = labelH / 2f + labelGap / 2f;

            List<UniTask> tasks = new List<UniTask>();
            CollectBattleLabelTasks(tasks, playerStats, _playerAtkCounterLabel, stackOffset, ct);
            CollectBattleLabelTasks(tasks, opponentStats, _opponentAtkCounterLabel, stackOffset, ct);
            await UniTask.WhenAll(tasks);
        }

        private void CollectBattleLabelTasks(
            List<UniTask> tasks,
            BattleCalculator.SideBattleStats stats,
            Label counterLabel,
            float stackOffset,
            CancellationToken ct)
        {
            if (stats.StrengthBlocked)
            {
                tasks.Add(PlayBattleLabelAsync("効果がない", "no-effect-label", counterLabel, 0f, ct));
            }
            else if (stats.TypeMatch && stats.WeaknessHit)
            {
                tasks.Add(PlayBattleLabelAsync("スキルタイプ一致", "type-match-label", counterLabel, -stackOffset, ct));
                tasks.Add(PlayBattleLabelAsync("弱点を突いた", "weakness-hit-label", counterLabel, stackOffset, ct));
            }
            else if (stats.TypeMatch)
            {
                tasks.Add(PlayBattleLabelAsync("スキルタイプ一致", "type-match-label", counterLabel, 0f, ct));
            }
            else if (stats.WeaknessHit)
            {
                tasks.Add(PlayBattleLabelAsync("弱点を突いた", "weakness-hit-label", counterLabel, 0f, ct));
            }
        }

        private void SendSkillsToGraveyard(IEnumerable<CardView> skills, GraveyardView graveyard)
        {
            foreach (CardView c in skills)
            {
                if (c.parent != null)
                {
                    c.RemoveFromHierarchy();
                }
                graveyard.AddCard(c);
            }
        }
    }
}
