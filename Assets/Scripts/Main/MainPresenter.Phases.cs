using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using Main.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main
{
    public sealed partial class MainPresenter
    {
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
            await PlayAnnouncementAsync("PLACE CHARACTERS", "turn-announcement-label--character", ct);

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
                await (isLocalTurn
                    ? _handView.AddCardAnimatedAsync(drawn, deckRect, 0f, ct)
                    : PlayCpuDrawAsync(drawn, deckRect, ct));
            }

            if (!isLocalTurn && drawn != null)
            {
                IReadOnlyList<CardView> cards = _opponentHandView.Cards;
                if (cards.Count > 0)
                {
                    _cpuCards.Add(cards[cards.Count - 1]);
                }
            }
        }

        // ─── 戦闘前1フェーズ（Skill/Character 裏向き1枚）─────────────────────

        private async UniTask RunPreBattle1PhaseAsync(bool isLocalTurn, CancellationToken ct)
        {
            await PlayAnnouncementAsync("PLACE CARDS", "turn-announcement-label--skill", ct);

            bool isLocalFirst = isLocalTurn;

            for (int i = 0; i < 2; i++)
            {
                bool isLocalAct = (i == 0) ? isLocalFirst : !isLocalFirst;

                if (isLocalAct)
                {
                    CardView placed = await WaitForPlayerPreBattle1TurnAsync(ct);
                    if (placed == null)
                    {
                        await PlayPassAnimationAsync(true, ct);
                    }
                }
                else
                {
                    await RunCpuPreBattle1SubTurnAsync(ct);
                }
            }
        }

        private async UniTask<CardView> WaitForPlayerPreBattle1TurnAsync(CancellationToken ct)
        {
            _isLocalPreBattleActive = true;
            _preBattleInputTcs = new UniTaskCompletionSource<CardView>();
            _stagedPreBattleCard = null;
            ShowActionButtons();
            UpdateStagedButtons(_stagedPreBattleCard != null);

            CardView result = null;
            try
            {
                result = await _preBattleInputTcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _isLocalPreBattleActive = false;
                _preBattleInputTcs = null;
                HideActionButtons();
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

        // ─── 戦闘前2フェーズ（Event のみ・交互・2連続パス）──────────────────

        private async UniTask RunPreBattle2PhaseAsync(CancellationToken ct)
        {
            await PlayAnnouncementAsync("PLACE EVENTS", "turn-announcement-label--event", ct);

            while (true)
            {
                if (_gameModel.IsLocalPreparationTurn)
                {
                    CardView readied = await WaitForPlayerPreBattle2InputAsync(ct);
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

            bool skipNextEffect = false;

            for (int i = queue.Count - 1; i >= 0; i--)
            {
                CardView card = queue[i];
                card.SetChainNumber(0);
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
                        if (skipNextEffect)
                        {
                            skipNextEffect = false;
                        }
                        else if (eventData.EffectType == EffectType.Negate)
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
                case EffectType.Draw:
                    await ApplyDrawEffectAsync(data.EffectValue, isLocal, ct);
                    break;
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
                    IReadOnlyList<CardView> cards = _opponentHandView.Cards;
                    if (cards.Count > 0)
                    {
                        _cpuCards.Add(cards[cards.Count - 1]);
                    }
                }
            }
        }

        // ─── 戦闘フェーズ ────────────────────────────────────────────────

        private async UniTask RunBattlePhaseAsync(CancellationToken ct)
        {
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

            // コスト払い（キャラ・技カード：オープン時）
            List<UniTask> costPayTasks = new List<UniTask>();
            foreach (CardView c in playerCards)
            {
                costPayTasks.Add(PayCostAsync(c, _playerDeckView, _playerGraveyardView, ct, announce: false));
            }
            foreach (CardView c in opponentCards)
            {
                costPayTasks.Add(PayCostAsync(c, _opponentDeckView, _opponentGraveyardView, ct, announce: false));
            }
            if (costPayTasks.Count > 0)
            {
                bool anyCost = playerCards.Concat(opponentCards).Any(c => c.Data.Cost > 0);
                if (anyCost)
                {
                    await PlayAnnouncementAsync("PAY THE COST", "turn-announcement-label--cost", ct);
                }
                await UniTask.WhenAll(costPayTasks);
                if (_isGameOver)
                {
                    return;
                }
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
            // ATKは技カードのダメージ値の合計（キャラなし or 新キャラ配置→0）
            bool playerHasAttackingChar = _playerCharacterSlot.CurrentCard != null && playerFieldChar.Count == 0;
            bool opponentHasAttackingChar = _opponentCharacterSlot.CurrentCard != null && opponentFieldChar.Count == 0;

            CardAttribute playerCharAttr = _playerCharacterSlot.CurrentCard?.Data.Attribute ?? CardAttribute.None;
            CardAttribute opponentCharAttr = _opponentCharacterSlot.CurrentCard?.Data.Attribute ?? CardAttribute.None;

            bool playerTypeMatch = playerSkill.Any(c => c.Data.Attribute != CardAttribute.None && c.Data.Attribute == playerCharAttr);
            CardAttribute opponentWeakness = _cardStore.AttributeDatabase != null ? _cardStore.AttributeDatabase.GetWeakness(opponentCharAttr) : CardAttribute.None;
            bool playerWeaknessHit = opponentWeakness != CardAttribute.None && playerSkill.Any(c => c.Data.Attribute == opponentWeakness);
            int playerATK = playerHasAttackingChar
                ? (playerSkill.Sum(c => c.Data.Attack) + _playerAtkBoost) * (playerTypeMatch ? 2 : 1) * (playerWeaknessHit ? 3 : 1)
                : 0;

            bool opponentTypeMatch = opponentSkill.Any(c => c.Data.Attribute != CardAttribute.None && c.Data.Attribute == opponentCharAttr);
            CardAttribute playerWeakness = _cardStore.AttributeDatabase != null ? _cardStore.AttributeDatabase.GetWeakness(playerCharAttr) : CardAttribute.None;
            bool opponentWeaknessHit = playerWeakness != CardAttribute.None && opponentSkill.Any(c => c.Data.Attribute == playerWeakness);
            int opponentATK = opponentHasAttackingChar
                ? (opponentSkill.Sum(c => c.Data.Attack) + _opponentAtkBoost) * (opponentTypeMatch ? 2 : 1) * (opponentWeaknessHit ? 3 : 1)
                : 0;

            int effectivePlayerDef = _playerCharacterSlot.Defense + _playerDefBoost;
            int effectiveOpponentDef = _opponentCharacterSlot.Defense + _opponentDefBoost;
            int damageToOpponent = Mathf.Max(0, playerATK - effectiveOpponentDef);
            int damageToPlayer = Mathf.Max(0, opponentATK - effectivePlayerDef);

            // ATKカウントアップ表示（技カードはフィールドに残ったまま）
            await PlayAtkCounterAsync(playerATK, opponentATK, effectiveOpponentDef, effectivePlayerDef, ct);

            // ATK数字・技カードをキャラスロットへ同時に飛翔
            await FlySkillsWithAtkAsync(playerATK, opponentATK, playerSkill, opponentSkill, ct);

            // 技カードを墓地へ
            foreach (CardView c in playerSkill)
            {
                if (c.parent != null) { c.RemoveFromHierarchy(); }
                _playerGraveyardView.AddCard(c);
            }
            foreach (CardView c in opponentSkill)
            {
                if (c.parent != null) { c.RemoveFromHierarchy(); }
                _opponentGraveyardView.AddCard(c);
            }

            // 攻撃アニメーション（ATK > 0 のときのみ実行）
            await UniTask.WhenAll(
                playerHasAttackingChar && playerATK > 0
                    ? PlayCharacterSlotAttackAsync(_playerCharacterSlot, _opponentDeckView, ct)
                    : UniTask.CompletedTask,
                opponentHasAttackingChar && opponentATK > 0
                    ? PlayCharacterSlotAttackAsync(_opponentCharacterSlot, _playerDeckView, ct)
                    : UniTask.CompletedTask
            );

            _playerCharacterSlot.SetAtkOverlayVisible(false);
            _opponentCharacterSlot.SetAtkOverlayVisible(false);
            _playerDeckView.DefOverlay.style.display = DisplayStyle.None;
            _opponentDeckView.DefOverlay.style.display = DisplayStyle.None;

            // DEF・ダメージ計算（攻撃後もスロットにキャラが戻っている）
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

            _playerAtkBoost = 0;
            _opponentAtkBoost = 0;
            _playerDefBoost = 0;
            _opponentDefBoost = 0;
        }

        private void OnGameEnd(bool? playerWins)
        {
            PlayGameEndAsync(playerWins, destroyCancellationToken).Forget();
        }
    }
}
