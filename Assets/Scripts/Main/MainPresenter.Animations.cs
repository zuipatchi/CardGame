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

        private async UniTask PlayPassAnimationAsync(bool isLocal, CancellationToken ct)
        {
            string labelClass = isLocal ? "turn-announcement-label--player" : "turn-announcement-label--enemy";
            await PlayAnnouncementAsync("PASS", labelClass, ct);
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
            _turnLabel.RemoveFromClassList("turn-announcement-label--mulligan");
            _turnLabel.RemoveFromClassList("turn-announcement-label--cost");
            _turnLabel.RemoveFromClassList("turn-announcement-label--cost-opponent");
            _turnLabel.RemoveFromClassList("turn-announcement-label--set");
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
            CancellationToken ct)
        {
            const float countDuration = 0.8f;
            const float holdDuration = 0.3f;

            _playerAtkCounterOverlay.BringToFront();
            _opponentAtkCounterOverlay.BringToFront();
            _playerAtkCounterLabel.text = "0";
            _opponentAtkCounterLabel.text = "0";
            _playerAtkCounterOverlay.style.display = DisplayStyle.Flex;
            _opponentAtkCounterOverlay.style.display = DisplayStyle.Flex;
            _playerAtkCounterOverlay.style.opacity = 0f;
            _opponentAtkCounterOverlay.style.opacity = 0f;

            _playerDeckView.SetDefValue(playerDef);
            _opponentDeckView.SetDefValue(opponentDef);
            _playerDeckView.DefOverlay.style.display = DisplayStyle.Flex;
            _opponentDeckView.DefOverlay.style.display = DisplayStyle.Flex;
            _playerDeckView.DefOverlay.style.opacity = 0f;
            _opponentDeckView.DefOverlay.style.opacity = 0f;

            float playerVal = 0f;
            float opponentVal = 0f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => _playerAtkCounterOverlay.style.opacity.value, v => _playerAtkCounterOverlay.style.opacity = v, 1f, 0.2f))
                .Join(DOTween.To(() => _opponentAtkCounterOverlay.style.opacity.value, v => _opponentAtkCounterOverlay.style.opacity = v, 1f, 0.2f))
                .Join(DOTween.To(() => _playerDeckView.DefOverlay.style.opacity.value, v => _playerDeckView.DefOverlay.style.opacity = v, 1f, 0.2f))
                .Join(DOTween.To(() => _opponentDeckView.DefOverlay.style.opacity.value, v => _opponentDeckView.DefOverlay.style.opacity = v, 1f, 0.2f))
                .Join(DOTween.To(() => playerVal, v => { playerVal = v; _playerAtkCounterLabel.text = Mathf.RoundToInt(v).ToString(); }, (float)playerAtk, countDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => opponentVal, v => { opponentVal = v; _opponentAtkCounterLabel.text = Mathf.RoundToInt(v).ToString(); }, (float)opponentAtk, countDuration).SetEase(Ease.OutQuad))
                .AppendInterval(holdDuration)
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try
            {
                await tcs.Task;
            }
            catch (OperationCanceledException) { }

            // overlays はここで非表示にしない — 呼び出し側（Phases.cs）が処理する
        }

        // ─── 戦闘ボーナスラベル（技タイプ一致・弱点を突いた）────────────────────

        private async UniTask PlayBattleLabelAsync(
            string text, string cssClass, Label atkCounterLabel, float yOffset, CancellationToken ct)
        {
            Rect counterRect = atkCounterLabel.worldBound;
            const float gap = 16f;
            const float slideOffset = 30f;
            const float estimatedH = 80f;

            Label label = new Label(text);
            label.AddToClassList(cssClass);
            label.pickingMode = PickingMode.Ignore;
            label.style.position = Position.Absolute;
            label.style.opacity = 0f;

            float finalLeft = counterRect.xMax + gap;
            float finalTop = counterRect.center.y - estimatedH / 2f + yOffset;
            float currentLeft = finalLeft + slideOffset;
            label.style.left = currentLeft;
            label.style.top = finalTop;
            _dragLayer.Add(label);

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => currentLeft, v => { currentLeft = v; label.style.left = v; }, finalLeft, 0.2f).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => label.style.opacity.value, v => label.style.opacity = v, 1f, 0.2f))
                .AppendInterval(0.7f)
                .Append(DOTween.To(() => label.style.opacity.value, v => label.style.opacity = v, 0f, 0.3f))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }

            label.RemoveFromHierarchy();
        }

        // ─── 技カード+ATK数字をキャラスロットへ同時飛翔 ────────────────────

        private async UniTask FlySkillsWithAtkAsync(
            int playerAtk, int opponentAtk,
            System.Collections.Generic.List<CardView> playerSkill,
            System.Collections.Generic.List<CardView> opponentSkill,
            CancellationToken ct)
        {
            const float holdDuration = 0.8f;
            const float labelW = 200f;
            const float labelH = 160f;

            // 現在位置を記録（drag layer ローカル座標）
            Vector2 playerCounterCenter = _dragLayer.WorldToLocal(_playerAtkCounterOverlay.worldBound.center);
            Vector2 opponentCounterCenter = _dragLayer.WorldToLocal(_opponentAtkCounterOverlay.worldBound.center);
            Vector2 playerSlotCenter = _dragLayer.WorldToLocal(_playerCharacterSlot.worldBound.center);
            Vector2 opponentSlotCenter = _dragLayer.WorldToLocal(_opponentCharacterSlot.worldBound.center);

            // floating label を drag layer に追加
            Label playerLabel = MakeFloatingAtkLabel(playerAtk.ToString(), playerCounterCenter, labelW, labelH);
            Label opponentLabel = MakeFloatingAtkLabel(opponentAtk.ToString(), opponentCounterCenter, labelW, labelH);
            _dragLayer.Add(playerLabel);
            _dragLayer.Add(opponentLabel);

            // field ATK overlays を非表示（DEF overlays は攻撃力非表示と同タイミングで Phases.cs が処理）
            _playerAtkCounterOverlay.style.display = DisplayStyle.None;
            _opponentAtkCounterOverlay.style.display = DisplayStyle.None;

            // スロットの ATK 値を設定
            _playerCharacterSlot.SetAtkValue(playerAtk);
            _opponentCharacterSlot.SetAtkValue(opponentAtk);

            float pLeft = playerLabel.style.left.value.value;
            float pTop = playerLabel.style.top.value.value;
            float oLeft = opponentLabel.style.left.value.value;
            float oTop = opponentLabel.style.top.value.value;

            UniTaskCompletionSource labelTcs = new UniTaskCompletionSource();
            Sequence labelSeq = DOTween.Sequence()
                .Join(DOTween.To(() => pLeft, v => { pLeft = v; playerLabel.style.left = v; }, playerSlotCenter.x - labelW * 0.5f, CpuCardFlyDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => pTop, v => { pTop = v; playerLabel.style.top = v; }, playerSlotCenter.y - labelH * 0.5f, CpuCardFlyDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => oLeft, v => { oLeft = v; opponentLabel.style.left = v; }, opponentSlotCenter.x - labelW * 0.5f, CpuCardFlyDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => oTop, v => { oTop = v; opponentLabel.style.top = v; }, opponentSlotCenter.y - labelH * 0.5f, CpuCardFlyDuration).SetEase(Ease.OutQuad))
                .AppendCallback(() =>
                {
                    playerLabel.RemoveFromHierarchy();
                    opponentLabel.RemoveFromHierarchy();
                    _playerCharacterSlot.SetAtkOverlayVisible(true);
                    _opponentCharacterSlot.SetAtkOverlayVisible(true);
                })
                .AppendInterval(holdDuration)
                .OnComplete(() => labelTcs.TrySetResult());

            ct.Register(() => { labelSeq.Kill(); labelTcs.TrySetCanceled(); });

            System.Collections.Generic.List<UniTask> tasks = new System.Collections.Generic.List<UniTask>();
            tasks.Add(labelTcs.Task);
            foreach (CardView c in playerSkill)
            {
                Rect fromRect = c.worldBound;
                _playerFieldView.RemoveCard(c);
                tasks.Add(FlySkillToSlotAsync(c, fromRect, _playerCharacterSlot, ct));
            }
            foreach (CardView c in opponentSkill)
            {
                Rect fromRect = c.worldBound;
                _opponentFieldView.RemoveCard(c);
                tasks.Add(FlySkillToSlotAsync(c, fromRect, _opponentCharacterSlot, ct));
            }

            try
            {
                await UniTask.WhenAll(tasks);
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (playerLabel.parent != null) { playerLabel.RemoveFromHierarchy(); }
                if (opponentLabel.parent != null) { opponentLabel.RemoveFromHierarchy(); }
            }
        }

        private static Label MakeFloatingAtkLabel(string text, Vector2 centerInDragLayer, float width, float height)
        {
            Label label = new Label(text);
            label.AddToClassList("atk-counter-label");
            label.pickingMode = PickingMode.Ignore;
            label.style.position = Position.Absolute;
            label.style.width = width;
            label.style.height = height;
            label.style.left = centerInDragLayer.x - width * 0.5f;
            label.style.top = centerInDragLayer.y - height * 0.5f;
            return label;
        }

        // ─── 技カードが相手キャラへ突撃 ─────────────────────────────────

        private async UniTask PlaySkillsAttackCharacterAsync(
            List<CardView> skills, FieldView field, CharacterSlotView target, CancellationToken ct)
        {
            if (skills.Count == 0)
            {
                return;
            }

            const float windupDuration = 0.15f;
            const float windupDistance = 50f;
            const float flyDuration = 0.65f;
            const float knockbackDist = 35f;
            const float knockbackDuration = 0.15f;

            List<Rect> rects = skills.Select(c => c.worldBound).ToList();
            foreach (CardView c in skills)
            {
                field.RemoveCard(c);
            }

            Vector2 toCenter = target.worldBound.center;

            for (int i = 0; i < skills.Count; i++)
            {
                CardView card = skills[i];
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

            for (int i = 0; i < skills.Count; i++)
            {
                CardView card = skills[i];
                Vector2 fromCenter = rects[i].center;
                Vector2 dir = (toCenter - fromCenter).normalized;
                float facingAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f;

                Vector2 windupCenter = fromCenter - dir * windupDistance;
                float windupLeft = windupCenter.x - CardWidth / 2f;
                float windupTop = windupCenter.y - CardHeight / 2f;
                float targetLeft = toCenter.x - CardWidth / 2f;
                float targetTop = toCenter.y - CardHeight / 2f;
                float rotAngle = 0f;
                float kbT = 0f;
                Vector2 kbEnd = toCenter - dir * knockbackDist;

                Sequence cardSeq = DOTween.Sequence()
                    .Join(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, windupLeft, windupDuration).SetEase(Ease.OutSine))
                    .Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, windupTop, windupDuration).SetEase(Ease.OutSine))
                    .Join(DOTween.To(() => rotAngle, v =>
                    {
                        rotAngle = v;
                        card.style.rotate = new Rotate(new Angle(v, AngleUnit.Degree));
                    }, facingAngle, windupDuration).SetEase(Ease.OutSine));

                cardSeq.Append(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, targetLeft, flyDuration).SetEase(Ease.InCubic));
                cardSeq.Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, targetTop, flyDuration).SetEase(Ease.InCubic));

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

            foreach (CardView card in skills)
            {
                if (card.parent == _dragLayer)
                {
                    _dragLayer.Remove(card);
                }
            }
        }

        // ─── 技カード攻撃アニメーション（旧・未使用）────────────────────────────

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

        // ─── キャラスロット攻撃アニメーション ────────────────────────────────

        private async UniTask PlayCharacterSlotAttackAsync(
            CharacterSlotView slot, DeckView targetDeck, CancellationToken ct)
        {
            CardView card = slot.CurrentCard;
            if (card == null)
            {
                return;
            }

            const float windupDuration = 0.15f;
            const float windupDistance = 50f;
            const float flyDuration = 0.65f;
            const float knockbackDist = 35f;
            const float knockbackDuration = 0.15f;

            Rect slotRect = slot.worldBound;

            bool hasAtkOverlay = slot.IsAtkOverlayVisible;
            string atkText = slot.AtkLabelText;
            VisualElement flyingAtk = null;
            float flyAtkLeft = 0f;
            float flyAtkTop = 0f;

            if (hasAtkOverlay)
            {
                slot.SetAtkOverlayVisible(false);
                flyingAtk = new VisualElement();
                flyingAtk.pickingMode = PickingMode.Ignore;
                flyingAtk.style.position = Position.Absolute;
                flyingAtk.style.width = CardWidth;
                flyingAtk.style.height = CardHeight;
                flyingAtk.style.alignItems = Align.Center;
                flyingAtk.style.justifyContent = Justify.Center;
                flyAtkLeft = slotRect.center.x - CardWidth / 2f;
                flyAtkTop = slotRect.center.y - CardHeight / 2f;
                flyingAtk.style.left = flyAtkLeft;
                flyingAtk.style.top = flyAtkTop;

                VisualElement atkIcon = new VisualElement();
                atkIcon.AddToClassList("char-slot-atk-icon");
                atkIcon.pickingMode = PickingMode.Ignore;
                flyingAtk.Add(atkIcon);

                Label atkLabel = new Label(atkText);
                atkLabel.AddToClassList("char-slot-atk-label");
                atkLabel.pickingMode = PickingMode.Ignore;
                flyingAtk.Add(atkLabel);
            }

            slot.RemoveCard();

            card.style.position = Position.Absolute;
            card.style.left = slotRect.center.x - CardWidth / 2f;
            card.style.top = slotRect.center.y - CardHeight / 2f;
            card.style.width = StyleKeyword.Null;
            card.style.height = StyleKeyword.Null;
            card.style.rotate = new Rotate(new Angle(0f, AngleUnit.Degree));
            card.style.scale = new Scale(Vector3.one);
            card.style.transformOrigin = StyleKeyword.Null;
            card.style.marginLeft = StyleKeyword.Null;
            card.style.marginRight = StyleKeyword.Null;
            _dragLayer.Add(card);

            if (flyingAtk != null)
            {
                _dragLayer.Add(flyingAtk);
            }

            Vector2 fromCenter = slotRect.center;
            Vector2 toCenter = targetDeck.worldBound.center;
            Vector2 dir = (toCenter - fromCenter).normalized;
            float facingAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f;
            float rotAngle = 0f;

            Vector2 windupCenter = fromCenter - dir * windupDistance;
            float windupLeft = windupCenter.x - CardWidth / 2f;
            float windupTop = windupCenter.y - CardHeight / 2f;
            float targetLeft = toCenter.x - CardWidth / 2f;
            float targetTop = toCenter.y - CardHeight / 2f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();

            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, windupLeft, windupDuration).SetEase(Ease.OutSine))
                .Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, windupTop, windupDuration).SetEase(Ease.OutSine))
                .Join(DOTween.To(() => rotAngle, v =>
                {
                    rotAngle = v;
                    card.style.rotate = new Rotate(new Angle(v, AngleUnit.Degree));
                }, facingAngle, windupDuration).SetEase(Ease.OutSine));

            if (flyingAtk != null)
            {
                seq.Join(DOTween.To(() => flyAtkLeft, v => { flyAtkLeft = v; flyingAtk.style.left = v; }, windupLeft, windupDuration).SetEase(Ease.OutSine));
                seq.Join(DOTween.To(() => flyAtkTop, v => { flyAtkTop = v; flyingAtk.style.top = v; }, windupTop, windupDuration).SetEase(Ease.OutSine));
            }

            seq.Append(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, targetLeft, flyDuration).SetEase(Ease.InCubic));
            seq.Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, targetTop, flyDuration).SetEase(Ease.InCubic));

            if (flyingAtk != null)
            {
                seq.Join(DOTween.To(() => flyAtkLeft, v => { flyAtkLeft = v; flyingAtk.style.left = v; }, targetLeft, flyDuration).SetEase(Ease.InCubic));
                seq.Join(DOTween.To(() => flyAtkTop, v => { flyAtkTop = v; flyingAtk.style.top = v; }, targetTop, flyDuration).SetEase(Ease.InCubic));
            }

            float kbT = 0f;
            Vector2 kbEnd = toCenter - dir * knockbackDist;
            seq.Append(DOTween.To(() => kbT, v =>
            {
                kbT = v;
                Vector2 pos = Vector2.Lerp(toCenter, kbEnd, v);
                card.style.left = pos.x - CardWidth / 2f;
                card.style.top = pos.y - CardHeight / 2f;
                if (flyingAtk != null)
                {
                    flyingAtk.style.left = pos.x - CardWidth / 2f;
                    flyingAtk.style.top = pos.y - CardHeight / 2f;
                }
            }, 1f, knockbackDuration).SetEase(Ease.OutQuad));

            seq.OnComplete(() => tcs.TrySetResult());
            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try
            {
                await tcs.Task;
            }
            catch (OperationCanceledException) { }

            if (flyingAtk != null && flyingAtk.parent != null)
            {
                flyingAtk.RemoveFromHierarchy();
            }

            if (card.parent == _dragLayer)
            {
                await FlyCharToSlotAsync(card, card.worldBound, slot, ct);
            }
        }

        // ─── CPU ドロー演出 ──────────────────────────────────────────────

        private async UniTask PlayCpuDrawAsync(CardData data, Rect deckRect, CancellationToken ct)
        {
            const float FlyDuration = 0.35f;

            CardView card = new CardView(_cardStore.CardTemplate, data, _cardStore.CardBack, faceDown: true, _cardStore.AttributeDatabase);
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

        private async UniTask FlySkillToSlotAsync(CardView card, Rect fromRect, CharacterSlotView slot, CancellationToken ct)
        {
            await FlyCardToDestAsync(card, fromRect, slot, ct);
            card.style.position = Position.Absolute;
            card.style.left = 0;
            card.style.top = 0;
            card.style.width = new StyleLength(new Length(100f, LengthUnit.Percent));
            card.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
            card.style.rotate = new Rotate(0);
            card.style.scale = new Scale(Vector3.one);
            card.style.transformOrigin = StyleKeyword.Null;
            card.style.marginLeft = StyleKeyword.Null;
            card.style.marginRight = StyleKeyword.Null;
            slot.Insert(0, card);
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

        // ─── コスト払い（デッキ上から→墓地）───────────────────────────────

        private async UniTask PayCostAsync(CardView card, DeckView deck, GraveyardView graveyard, CancellationToken ct, bool announce = true)
        {
            if (_isGameOver)
            {
                return;
            }

            int cost = card.Data.Cost;
            if (cost <= 0)
            {
                return;
            }

            bool willLose = cost > deck.Count;

            if (announce)
            {
                string costClass = deck == _playerDeckView
                    ? "turn-announcement-label--cost"
                    : "turn-announcement-label--cost-opponent";
                await PlayAnnouncementAsync($"PAY {cost} COSTS", costClass, ct);
            }

            Rect deckRect = deck.worldBound;
            List<CardView> costCards = deck.TakeFromTop(cost);
            await PlayDeckDamageAsync(costCards, deckRect, graveyard, deck, ct);

            if (willLose)
            {
                _isGameOver = true;
                OnGameEnd(deck == _playerDeckView ? (bool?)false : true);
            }
        }

        // ─── デッキダメージ→墓地アニメーション ─────────────────────────────

        private async UniTask PlayDeckDamageAsync(
            List<CardView> cards, Rect fromRect, GraveyardView graveyard, DeckView sourceDeck, CancellationToken ct)
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
                card.FaceUp();

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
                sourceDeck.DecrementDisplayCount();

                if (i < cards.Count - 1)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(CardInterval), cancellationToken: ct);
                }
            }
        }

        // ─── マリガン：手札をデッキへ返すアニメーション ─────────────────────────

        private async UniTask PlayReturnHandToDeckAsync(HandView hand, DeckView deck, CancellationToken ct)
        {
            const float FlyDuration = 0.25f;
            const float Stagger = 0.06f;

            IReadOnlyList<CardView> snapshot = hand.Cards;
            if (snapshot.Count == 0)
            {
                return;
            }

            Rect deckRect = deck.worldBound;
            float targetLeft = deckRect.center.x - CardWidth / 2f;
            float targetTop = deckRect.center.y - CardHeight / 2f;

            List<(CardView card, Rect fromRect)> entries = new List<(CardView, Rect)>();
            foreach (CardView card in snapshot)
            {
                entries.Add((card, card.worldBound));
            }

            foreach ((CardView card, Rect _) in entries)
            {
                hand.RemoveCard(card);
            }

            List<UniTask> tasks = new List<UniTask>();
            for (int i = 0; i < entries.Count; i++)
            {
                (CardView card, Rect fromRect) = entries[i];
                tasks.Add(FlyCardToDeckPositionAsync(card, fromRect, targetLeft, targetTop, i * Stagger, FlyDuration, ct));
            }
            await UniTask.WhenAll(tasks);
        }

        private async UniTask FlyCardToDeckPositionAsync(
            CardView card, Rect fromRect,
            float targetLeft, float targetTop,
            float delay, float duration,
            CancellationToken ct)
        {
            card.style.position = Position.Absolute;
            card.style.left = fromRect.center.x - CardWidth / 2f;
            card.style.top = fromRect.center.y - CardHeight / 2f;
            card.style.width = StyleKeyword.Null;
            card.style.height = StyleKeyword.Null;
            card.style.rotate = new Rotate(0);
            card.style.scale = new Scale(Vector3.one);
            card.style.transformOrigin = StyleKeyword.Null;
            card.style.marginLeft = StyleKeyword.Null;
            card.style.marginRight = StyleKeyword.Null;
            _dragLayer.Add(card);

            if (delay > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
            }

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, targetLeft, duration).SetEase(Ease.InQuad))
                .Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, targetTop, duration).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try
            {
                await tcs.Task;
            }
            catch (OperationCanceledException) { }

            if (card.parent == _dragLayer)
            {
                _dragLayer.Remove(card);
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

        // ─── 勝敗演出 ───────────────────────────────────────────────────────

        private async UniTask PlayGameEndAsync(bool? playerWins, CancellationToken ct)
        {
            string text = playerWins == null ? "DRAW" : playerWins.Value ? "YOU WIN" : "YOU LOSE";
            string labelClass = playerWins == null ? "game-end-label--draw"
                : playerWins.Value ? "game-end-label--win" : "game-end-label--lose";

            _gameEndLabel.text = text;
            _gameEndLabel.RemoveFromClassList("game-end-label--win");
            _gameEndLabel.RemoveFromClassList("game-end-label--lose");
            _gameEndLabel.RemoveFromClassList("game-end-label--draw");
            _gameEndLabel.AddToClassList(labelClass);
            _gameEndLabel.style.scale = new Scale(new Vector3(0.3f, 0.3f, 1f));

            _gameEndTitleButton.style.opacity = 0f;
            _gameEndOverlay.style.display = DisplayStyle.Flex;
            _gameEndOverlay.style.opacity = 0f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Append(DOTween.To(
                    () => _gameEndOverlay.style.opacity.value,
                    v => _gameEndOverlay.style.opacity = v,
                    1f, 0.3f).SetEase(Ease.OutQuad))
                .Join(DOTween.To(
                    () => _gameEndLabel.style.scale.value.value.x,
                    v => _gameEndLabel.style.scale = new Scale(new Vector3(v, v, 1f)),
                    1f, 0.4f).SetEase(Ease.OutBack))
                .AppendInterval(0.3f)
                .Append(DOTween.To(
                    () => _gameEndTitleButton.style.opacity.value,
                    v => _gameEndTitleButton.style.opacity = v,
                    1f, 0.3f).SetEase(Ease.OutQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }
        }

        // ─── OK フラッシュ演出 ──────────────────────────────────────────

        private async UniTask PlayOkFlashAsync(bool isLocal, CancellationToken ct)
        {
            VisualElement wrapper = new VisualElement();
            wrapper.style.position = Position.Absolute;
            wrapper.style.left = 0;
            wrapper.style.right = 0;
            wrapper.style.top = 0;
            wrapper.style.bottom = 0;
            wrapper.style.alignItems = Align.Center;
            wrapper.style.justifyContent = Justify.Center;
            wrapper.pickingMode = PickingMode.Ignore;

            Label label = new Label("SET!");
            label.AddToClassList("turn-announcement-label");
            label.AddToClassList(isLocal ? "turn-announcement-label--set" : "turn-announcement-label--enemy");
            label.pickingMode = PickingMode.Ignore;
            label.style.scale = new Scale(new Vector3(0.5f, 0.5f, 1f));
            wrapper.Add(label);
            wrapper.style.opacity = 0f;
            _dragLayer.Add(wrapper);

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(
                    () => wrapper.style.opacity.value,
                    v => wrapper.style.opacity = v,
                    1f, 0.25f).SetEase(Ease.OutQuad))
                .Join(DOTween.To(
                    () => label.style.scale.value.value.x,
                    v => label.style.scale = new Scale(new Vector3(v, v, 1f)),
                    1f, 0.25f).SetEase(Ease.OutBack))
                .AppendInterval(0.5f)
                .Append(DOTween.To(
                    () => wrapper.style.opacity.value,
                    v => wrapper.style.opacity = v,
                    0f, 0.3f).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }

            wrapper.RemoveFromHierarchy();
        }
    }
}
