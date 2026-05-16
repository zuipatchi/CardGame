using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using Main.Game;
using UnityEngine;

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
            await PlayAnnouncementAsync("SET CHARACTERS", "turn-announcement-label--character", ct);

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
                    bool placed = await WaitForPlayerPreBattle1TurnAsync(ct);
                    if (!placed)
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

        private async UniTask<bool> WaitForPlayerPreBattle1TurnAsync(CancellationToken ct)
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

            return result != null;
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
                        await PlayPassAnimationAsync(true, ct);
                        if (_gameModel.Pass())
                        {
                            break;
                        }
                    }
                    else
                    {
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

            List<CardView> playerFieldChar = playerCards.Where(c => c.Data is CharacterCardData).ToList();
            List<CardView> opponentFieldChar = opponentCards.Where(c => c.Data is CharacterCardData).ToList();
            List<CardView> playerSkill = playerCards.Where(c => c.Data is SkillCardData).ToList();
            List<CardView> opponentSkill = opponentCards.Where(c => c.Data is SkillCardData).ToList();

            // フィールドのキャラをスロットへ飛翔（スロット更新・DEF確定）
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

            // スロットのキャラが攻撃（キャラなし→ATK=0）
            int playerATK = _playerCharacterSlot.CurrentCard != null
                ? _playerCharacterSlot.CurrentCard.Data.Attack + _playerAtkBoost
                : 0;
            int opponentATK = _opponentCharacterSlot.CurrentCard != null
                ? _opponentCharacterSlot.CurrentCard.Data.Attack + _opponentAtkBoost
                : 0;

            await UniTask.WhenAll(
                PlayCharacterSlotAttackAsync(_playerCharacterSlot, _opponentDeckView, ct),
                PlayCharacterSlotAttackAsync(_opponentCharacterSlot, _playerDeckView, ct)
            );

            // DEF・ダメージ計算（攻撃後もスロットにキャラが戻っている）
            int effectivePlayerDef = _playerCharacterSlot.Defense + _playerDefBoost;
            int effectiveOpponentDef = _opponentCharacterSlot.Defense + _opponentDefBoost;
            int damageToOpponent = Mathf.Max(0, playerATK - effectiveOpponentDef);
            int damageToPlayer = Mathf.Max(0, opponentATK - effectivePlayerDef);

            if (playerATK > 0 || opponentATK > 0)
            {
                await PlayAtkCounterAsync(playerATK, opponentATK, effectiveOpponentDef, effectivePlayerDef, damageToOpponent, damageToPlayer, ct);

                Rect opponentDeckRect = _opponentDeckView.worldBound;
                Rect playerDeckRect = _playerDeckView.worldBound;
                List<CardView> opponentDamageCards = _opponentDeckView.TakeFromTop(damageToOpponent);
                List<CardView> playerDamageCards = _playerDeckView.TakeFromTop(damageToPlayer);
                await UniTask.WhenAll(
                    PlayDeckDamageAsync(opponentDamageCards, opponentDeckRect, _opponentGraveyardView, ct),
                    PlayDeckDamageAsync(playerDamageCards, playerDeckRect, _playerGraveyardView, ct)
                );
            }

            _playerAtkBoost = 0;
            _opponentAtkBoost = 0;
            _playerDefBoost = 0;
            _opponentDefBoost = 0;

            CheckGameOver();

            // 技カードを墓地へ（攻撃なし）
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

        // ─── ゲーム終了判定 ──────────────────────────────────────────────

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
    }
}
