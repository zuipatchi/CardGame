using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Main.Card;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // ─── ターン・フェーズ告知アニメーション ────────────────────────────

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

        // ─── ATKカウンター・ダメージ式演出 ───────────────────────────────

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

        // ─── 技カード攻撃アニメーション ──────────────────────────────────

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

        // ─── CPU ドロー演出 ──────────────────────────────────────────────

        private async UniTask PlayCpuDrawAsync(CardData data, Rect deckRect, CancellationToken ct)
        {
            const float FlyDuration = 0.35f;

            CardView card = new CardView(_cardStore.CardTemplate, data, _cardStore.CardBack, faceDown: true);
            card.style.position = Position.Absolute;
            card.style.left = deckRect.center.x - CardWidth / 2f;
            card.style.top = deckRect.center.y - CardHeight / 2f;
            _dragLayer.Add(card);

            Rect handRect = _opponentHandView.worldBound;
            float targetLeft = handRect.center.x - CardWidth / 2f;
            float targetTop = handRect.yMax - CardHeight / 2f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, targetLeft, FlyDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, targetTop, FlyDuration).SetEase(Ease.OutQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try
            {
                await tcs.Task;
            }
            catch (OperationCanceledException)
            {
                _dragLayer.Remove(card);
                return;
            }

            _dragLayer.Remove(card);
            _opponentHandView.AcceptCard(card);
        }

        // ─── カード移動ヘルパー ──────────────────────────────────────────

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

        // ─── デッキダメージ→墓地アニメーション ─────────────────────────────

        private async UniTask PlayDeckDamageAsync(
            List<CardView> cards, Rect fromRect, GraveyardView graveyard, CancellationToken ct)
        {
            if (cards.Count == 0)
            {
                return;
            }

            const float FlyDuration = 0.3f;
            const float CardInterval = 0.06f;

            Rect toRect = graveyard.worldBound;
            float startLeft = fromRect.center.x - CardWidth / 2f;
            float startTop = fromRect.center.y - CardHeight / 2f;
            float targetLeft = toRect.center.x - CardWidth / 2f;
            float targetTop = toRect.center.y - CardHeight / 2f;

            for (int i = 0; i < cards.Count; i++)
            {
                CardView card = cards[i];
                card.style.position = Position.Absolute;
                card.style.left = startLeft;
                card.style.top = startTop;
                card.style.width = StyleKeyword.Null;
                card.style.height = StyleKeyword.Null;
                card.style.rotate = new Rotate(0);
                card.style.scale = new Scale(Vector3.one);
                card.style.transformOrigin = StyleKeyword.Null;
                card.style.marginLeft = StyleKeyword.Null;
                card.style.marginRight = StyleKeyword.Null;
                _dragLayer.Add(card);

                UniTaskCompletionSource tcs = new UniTaskCompletionSource();
                Sequence seq = DOTween.Sequence()
                    .Join(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, targetLeft, FlyDuration).SetEase(Ease.InQuad))
                    .Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, targetTop, FlyDuration).SetEase(Ease.InQuad))
                    .Join(DOTween.To(
                        () => card.style.scale.value.value.x,
                        s => card.style.scale = new Scale(new Vector3(s, s, 1f)),
                        0f, FlyDuration).SetEase(Ease.InQuad))
                    .OnComplete(() => tcs.TrySetResult());

                ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

                try { await tcs.Task; }
                catch (OperationCanceledException) { }

                if (card.parent == _dragLayer)
                {
                    _dragLayer.Remove(card);
                }
                graveyard.AddCard(card);

                if (i < cards.Count - 1)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(CardInterval), cancellationToken: ct);
                }
            }
        }

        // ─── ユーティリティ ──────────────────────────────────────────────

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
