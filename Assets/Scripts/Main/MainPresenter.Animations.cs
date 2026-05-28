using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Main.Card;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // ─── コイントス演出 ──────────────────────────────────────────────

        private async UniTask PlayCoinTossAsync(bool isLocalFirst, CancellationToken ct)
        {
            const float overlayFadeIn = 0.25f;
            const float holdDuration = 0.8f;
            const float fadeOutDuration = 0.4f;

            VisualElement overlay = new VisualElement();
            overlay.pickingMode = PickingMode.Ignore;
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.right = 0;
            overlay.style.top = 0;
            overlay.style.bottom = 0;
            overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.flexDirection = FlexDirection.Column;
            overlay.style.opacity = 0f;

            Sprite coinFront = _cardStore.CoinFront;
            Sprite coinBack = _cardStore.CoinBack;

            VisualElement coin = new VisualElement();
            coin.pickingMode = PickingMode.Ignore;
            coin.style.width = 160f;
            coin.style.height = 160f;
            if (coinFront != null)
            {
                coin.style.backgroundImage = Background.FromSprite(coinFront);
            }
            coin.style.marginBottom = 32f;

            Label resultLabel = new Label(isLocalFirst ? "先攻" : "後攻");
            resultLabel.AddToClassList("turn-announcement-label");
            resultLabel.AddToClassList(isLocalFirst ? "turn-announcement-label--player" : "turn-announcement-label--enemy");
            resultLabel.pickingMode = PickingMode.Ignore;
            resultLabel.style.opacity = 0f;
            resultLabel.style.scale = new Scale(new Vector3(0.5f, 0.5f, 1f));

            overlay.Add(coin);
            overlay.Add(resultLabel);
            _dragLayer.Add(overlay);

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            float scaleX = 1f;
            bool showFront = true;

            // 各半周期の秒数（高速→低速でコインが減速しながら停止する）
            float[] halfPeriods = { 0.07f, 0.07f, 0.07f, 0.07f, 0.07f, 0.07f, 0.09f, 0.09f, 0.12f, 0.12f, 0.15f, 0.15f, 0.18f };

            Sequence seq = DOTween.Sequence()
                .Append(DOTween.To(() => overlay.style.opacity.value, v => overlay.style.opacity = v, 1f, overlayFadeIn).SetEase(Ease.OutQuad));

            for (int i = 0; i < halfPeriods.Length; i++)
            {
                float target = (i % 2 == 0) ? 0f : 1f;
                float duration = halfPeriods[i];
                seq.Append(DOTween.To(() => scaleX, v =>
                {
                    scaleX = v;
                    coin.style.scale = new Scale(new Vector3(v, 1f, 1f));
                }, target, duration).SetEase(Ease.Linear));

                // scaleX が 0 になったタイミング（コインが横向き）で表裏を入れ替え
                if (i % 2 == 0)
                {
                    seq.AppendCallback(() =>
                    {
                        showFront = !showFront;
                        Sprite next = showFront ? coinFront : coinBack;
                        if (next != null)
                        {
                            coin.style.backgroundImage = Background.FromSprite(next);
                        }
                    });
                }
            }

            // halfPeriods が奇数個 → ループ終了時 scaleX=0 なので 1 まで戻す（settle）
            // settle 直前に isLocalFirst に合わせた面を確定する
            if (halfPeriods.Length % 2 != 0)
            {
                seq.AppendCallback(() =>
                {
                    showFront = isLocalFirst;
                    Sprite settle = isLocalFirst ? coinFront : coinBack;
                    if (settle != null)
                    {
                        coin.style.backgroundImage = Background.FromSprite(settle);
                    }
                });
                seq.Append(DOTween.To(() => scaleX, v =>
                {
                    scaleX = v;
                    coin.style.scale = new Scale(new Vector3(v, 1f, 1f));
                }, 1f, 0.18f).SetEase(Ease.OutBack));
            }

            // settle 完了後 0.5 秒待ってから結果テキストをスケールイン
            seq.AppendInterval(0.5f);
            seq.Append(DOTween.To(
                () => resultLabel.style.opacity.value,
                v => resultLabel.style.opacity = v,
                1f, 0.25f).SetEase(Ease.OutQuad));
            seq.Join(DOTween.To(
                () => resultLabel.style.scale.value.value.x,
                v => resultLabel.style.scale = new Scale(new Vector3(v, v, 1f)),
                1f, 0.25f).SetEase(Ease.OutBack));

            seq.AppendInterval(holdDuration);
            seq.Append(DOTween.To(() => overlay.style.opacity.value, v => overlay.style.opacity = v, 0f, fadeOutDuration).SetEase(Ease.InQuad));
            seq.OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }

            overlay.RemoveFromHierarchy();
        }

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

            _playerCharacterSlot.SetDefValue(playerDef);
            _opponentCharacterSlot.SetDefValue(opponentDef);
            _playerCharacterSlot.DefOverlay.BringToFront();
            _opponentCharacterSlot.DefOverlay.BringToFront();
            _playerCharacterSlot.DefOverlay.style.display = DisplayStyle.Flex;
            _opponentCharacterSlot.DefOverlay.style.display = DisplayStyle.Flex;
            _playerCharacterSlot.DefOverlay.style.opacity = 0f;
            _opponentCharacterSlot.DefOverlay.style.opacity = 0f;

            float playerVal = 0f;
            float opponentVal = 0f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => _playerAtkCounterOverlay.style.opacity.value, v => _playerAtkCounterOverlay.style.opacity = v, 1f, 0.2f))
                .Join(DOTween.To(() => _opponentAtkCounterOverlay.style.opacity.value, v => _opponentAtkCounterOverlay.style.opacity = v, 1f, 0.2f))
                .Join(DOTween.To(() => _playerCharacterSlot.DefOverlay.style.opacity.value, v => _playerCharacterSlot.DefOverlay.style.opacity = v, 1f, 0.2f))
                .Join(DOTween.To(() => _opponentCharacterSlot.DefOverlay.style.opacity.value, v => _opponentCharacterSlot.DefOverlay.style.opacity = v, 1f, 0.2f))
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
                .AppendInterval(0.5f)
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

                Vector2 windupCenter = fromCenter - dir * AttackWindupDistance;
                float windupLeft = windupCenter.x - CardWidth / 2f;
                float windupTop = windupCenter.y - CardHeight / 2f;
                float targetLeft = toCenter.x - CardWidth / 2f;
                float targetTop = toCenter.y - CardHeight / 2f;
                float rotAngle = 0f;
                float kbT = 0f;
                Vector2 kbEnd = toCenter - dir * AttackKnockbackDistance;

                Sequence cardSeq = DOTween.Sequence()
                    .Join(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, windupLeft, AttackWindupDuration).SetEase(Ease.OutSine))
                    .Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, windupTop, AttackWindupDuration).SetEase(Ease.OutSine))
                    .Join(DOTween.To(() => rotAngle, v =>
                    {
                        rotAngle = v;
                        card.style.rotate = new Rotate(new Angle(v, AngleUnit.Degree));
                    }, facingAngle, AttackWindupDuration).SetEase(Ease.OutSine));

                cardSeq.Append(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, targetLeft, AttackFlyDuration).SetEase(Ease.InCubic));
                cardSeq.Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, targetTop, AttackFlyDuration).SetEase(Ease.InCubic));

                cardSeq.Append(DOTween.To(() => kbT, v =>
                {
                    kbT = v;
                    Vector2 pos = Vector2.Lerp(toCenter, kbEnd, v);
                    card.style.left = pos.x - CardWidth / 2f;
                    card.style.top = pos.y - CardHeight / 2f;
                }, 1f, AttackKnockbackDuration).SetEase(Ease.OutQuad));

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

        // ─── キャラスロット攻撃アニメーション ────────────────────────────────

        private async UniTask PlayCharacterSlotAttackAsync(
            CharacterSlotView slot, DeckView targetDeck, CancellationToken ct)
        {
            CardView card = slot.CurrentCard;
            if (card == null)
            {
                return;
            }

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

            Vector2 windupCenter = fromCenter - dir * AttackWindupDistance;
            float windupLeft = windupCenter.x - CardWidth / 2f;
            float windupTop = windupCenter.y - CardHeight / 2f;
            float targetLeft = toCenter.x - CardWidth / 2f;
            float targetTop = toCenter.y - CardHeight / 2f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();

            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, windupLeft, AttackWindupDuration).SetEase(Ease.OutSine))
                .Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, windupTop, AttackWindupDuration).SetEase(Ease.OutSine))
                .Join(DOTween.To(() => rotAngle, v =>
                {
                    rotAngle = v;
                    card.style.rotate = new Rotate(new Angle(v, AngleUnit.Degree));
                }, facingAngle, AttackWindupDuration).SetEase(Ease.OutSine));

            if (flyingAtk != null)
            {
                seq.Join(DOTween.To(() => flyAtkLeft, v => { flyAtkLeft = v; flyingAtk.style.left = v; }, windupLeft, AttackWindupDuration).SetEase(Ease.OutSine));
                seq.Join(DOTween.To(() => flyAtkTop, v => { flyAtkTop = v; flyingAtk.style.top = v; }, windupTop, AttackWindupDuration).SetEase(Ease.OutSine));
            }

            seq.Append(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, targetLeft, AttackFlyDuration).SetEase(Ease.InCubic));
            seq.Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, targetTop, AttackFlyDuration).SetEase(Ease.InCubic));

            if (flyingAtk != null)
            {
                seq.Join(DOTween.To(() => flyAtkLeft, v => { flyAtkLeft = v; flyingAtk.style.left = v; }, targetLeft, AttackFlyDuration).SetEase(Ease.InCubic));
                seq.Join(DOTween.To(() => flyAtkTop, v => { flyAtkTop = v; flyingAtk.style.top = v; }, targetTop, AttackFlyDuration).SetEase(Ease.InCubic));
            }

            float kbT = 0f;
            Vector2 kbEnd = toCenter - dir * AttackKnockbackDistance;
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
            }, 1f, AttackKnockbackDuration).SetEase(Ease.OutQuad));

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

        // ─── ダメージ数字飛翔演出 ──────────────────────────────────────────

        private async UniTask PlayDamageNumbersFlyAsync(
            int damageToOpponent, int damageToPlayer,
            Vector2 damageToOpponentFrom, Vector2 damageToPlayerFrom,
            CancellationToken ct)
        {
            List<UniTask> tasks = new List<UniTask>();
            if (damageToOpponent > 0)
            {
                tasks.Add(PlayDamageNumberFlyAsync(damageToOpponent, damageToOpponentFrom, _opponentDeckView, ct));
            }
            if (damageToPlayer > 0)
            {
                tasks.Add(PlayDamageNumberFlyAsync(damageToPlayer, damageToPlayerFrom, _playerDeckView, ct));
            }
            if (tasks.Count > 0)
            {
                await UniTask.WhenAll(tasks);
            }
        }

        private async UniTask PlayDamageNumberFlyAsync(
            int damage, Vector2 fromWorldCenter, DeckView targetDeck, CancellationToken ct)
        {
            const float AppearDuration = 0.3f;
            const float HoldDuration = 0.3f;
            const float FlyDuration = 0.75f;
            const float LabelW = 320f;
            const float LabelH = 90f;

            Label label = new Label($"{damage}ダメージ");
            label.AddToClassList("damage-number-label");
            label.pickingMode = PickingMode.Ignore;
            label.style.position = Position.Absolute;
            label.style.opacity = 0f;
            label.style.scale = new Scale(new Vector3(0.5f, 0.5f, 1f));

            Vector2 fromLocal = _dragLayer.WorldToLocal(fromWorldCenter);
            float left = fromLocal.x - LabelW / 2f;
            float top = fromLocal.y - LabelH / 2f;
            label.style.left = left;
            label.style.top = top;
            _dragLayer.Add(label);

            Vector2 deckLocal = _dragLayer.WorldToLocal(targetDeck.worldBound.center);
            float targetLeft = deckLocal.x - LabelW / 2f;
            float targetTop = deckLocal.y - LabelH / 2f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => label.style.opacity.value, v => label.style.opacity = v, 1f, AppearDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => label.style.scale.value.value.x, v => label.style.scale = new Scale(new Vector3(v, v, 1f)), 1f, AppearDuration).SetEase(Ease.OutBack))
                .AppendInterval(HoldDuration)
                .Append(DOTween.To(() => left, v => { left = v; label.style.left = v; }, targetLeft, FlyDuration).SetEase(Ease.InQuad))
                .Join(DOTween.To(() => top, v => { top = v; label.style.top = v; }, targetTop, FlyDuration).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }

            label.RemoveFromHierarchy();
        }

        // ─── CPU ドロー演出 ──────────────────────────────────────────────

        private async UniTask PlayCpuDrawAsync(CardData data, Rect deckRect, CancellationToken ct)
        {
            const float FlyDuration = 0.35f;

            CardView card = new CardView(_cardStore.CardTemplate, data, _cardStore.CardBack, faceDown: true, _cardStore.AttributeDatabase, isOpponent: true);
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

        // ─── Negate エフェクト（ラベル上昇 + パーティクル同時再生）─────────────

        private async UniTask PlayNegateEffectAsync(CardView targetCard, CancellationToken ct)
        {
            List<UniTask> tasks = new List<UniTask>();
            tasks.Add(PlayNegateFloatingLabelAsync(targetCard, "NEGATE!", "negate-label", ct));
            if (_negateEffectPrefab != null)
            {
                tasks.Add(PlayParticleAtCardAsync(targetCard, _negateEffectPrefab, ct));
            }
            await UniTask.WhenAll(tasks);
        }

        private async UniTask PlayNegatedEffectAsync(CardView card, CancellationToken ct)
        {
            List<UniTask> tasks = new List<UniTask>();
            tasks.Add(PlayNegateFloatingLabelAsync(card, "NEGATED!", "negated-label", ct));
            if (_negateEffectPrefab != null)
            {
                tasks.Add(PlayParticleAtCardAsync(card, _negateEffectPrefab, ct));
            }
            await UniTask.WhenAll(tasks);
        }

        private async UniTask PlayNegateFloatingLabelAsync(CardView card, string text, string cssClass, CancellationToken ct)
        {
            const float LabelW = 200f;
            const float LabelH = 60f;
            const float RiseDist = 70f;
            const float AppearDuration = 0.2f;
            const float HoldDuration = 0.3f;
            const float FadeDuration = 0.5f;

            Label label = new Label(text);
            label.AddToClassList(cssClass);
            label.pickingMode = PickingMode.Ignore;
            label.style.position = Position.Absolute;
            label.style.opacity = 0f;
            label.style.scale = new Scale(new Vector3(0.7f, 0.7f, 1f));

            Vector2 cardLocal = _dragLayer.WorldToLocal(card.worldBound.center);
            float left = cardLocal.x - LabelW / 2f;
            float top = cardLocal.y - LabelH / 2f;
            float targetTop = top - RiseDist;
            label.style.left = left;
            label.style.top = top;
            _dragLayer.Add(label);

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => label.style.opacity.value, v => label.style.opacity = v, 1f, AppearDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => label.style.scale.value.value.x, v => label.style.scale = new Scale(new Vector3(v, v, 1f)), 1f, AppearDuration).SetEase(Ease.OutBack))
                .AppendInterval(HoldDuration)
                .Append(DOTween.To(() => top, v => { top = v; label.style.top = v; }, targetTop, FadeDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => label.style.opacity.value, v => label.style.opacity = v, 0f, FadeDuration).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }

            label.RemoveFromHierarchy();
        }

        // ─── BanishChar エフェクト（対象キャラスロット位置にラベル + パーティクル同時再生）────

        private async UniTask PlayBanishCharEffectAsync(CharacterSlotView targetSlot, CancellationToken ct)
        {
            if (targetSlot.CurrentCard == null)
            {
                return;
            }

            List<UniTask> tasks = new List<UniTask>();
            tasks.Add(PlayBanishCharLabelAsync(targetSlot, ct));
            if (_banishCharEffectPrefab != null)
            {
                tasks.Add(PlayParticleAtCardAsync(targetSlot.CurrentCard, _banishCharEffectPrefab, ct));
            }
            await UniTask.WhenAll(tasks);
        }

        private async UniTask PlayBanishCharLabelAsync(CharacterSlotView targetSlot, CancellationToken ct)
        {
            const float LabelW = 200f;
            const float LabelH = 60f;
            const float RiseDist = 70f;
            const float AppearDuration = 0.2f;
            const float HoldDuration = 0.3f;
            const float FadeDuration = 0.5f;

            Label label = new Label("BANISH!");
            label.AddToClassList("banish-char-label");
            label.pickingMode = PickingMode.Ignore;
            label.style.position = Position.Absolute;
            label.style.opacity = 0f;
            label.style.scale = new Scale(new Vector3(0.7f, 0.7f, 1f));

            Vector2 slotLocal = _dragLayer.WorldToLocal(targetSlot.worldBound.center);
            float left = slotLocal.x - LabelW / 2f;
            float top = slotLocal.y - LabelH / 2f;
            float targetTop = top - RiseDist;
            label.style.left = left;
            label.style.top = top;
            _dragLayer.Add(label);

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => label.style.opacity.value, v => label.style.opacity = v, 1f, AppearDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => label.style.scale.value.value.x, v => label.style.scale = new Scale(new Vector3(v, v, 1f)), 1f, AppearDuration).SetEase(Ease.OutBack))
                .AppendInterval(HoldDuration)
                .Append(DOTween.To(() => top, v => { top = v; label.style.top = v; }, targetTop, FadeDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => label.style.opacity.value, v => label.style.opacity = v, 0f, FadeDuration).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }

            label.RemoveFromHierarchy();
        }

        // ─── Draw エフェクト（ラベル上昇 + パーティクル同時再生）────────────────

        private async UniTask PlayDrawEffectAsync(CardView card, int value, CancellationToken ct)
        {
            List<UniTask> tasks = new List<UniTask>();
            tasks.Add(PlayDrawLabelAsync(card, value, ct));
            if (_drawEffectPrefab != null)
            {
                tasks.Add(PlayParticleAtCardAsync(card, _drawEffectPrefab, ct));
            }
            await UniTask.WhenAll(tasks);
        }

        private async UniTask PlayDrawLabelAsync(CardView card, int value, CancellationToken ct)
        {
            const float LabelW = 200f;
            const float LabelH = 60f;
            const float RiseDist = 70f;
            const float AppearDuration = 0.2f;
            const float HoldDuration = 0.3f;
            const float FadeDuration = 0.5f;

            Label label = new Label($"DRAW +{value}");
            label.AddToClassList("draw-label");
            label.pickingMode = PickingMode.Ignore;
            label.style.position = Position.Absolute;
            label.style.opacity = 0f;
            label.style.scale = new Scale(new Vector3(0.7f, 0.7f, 1f));

            Vector2 cardLocal = _dragLayer.WorldToLocal(card.worldBound.center);
            float left = cardLocal.x - LabelW / 2f;
            float top = cardLocal.y - LabelH / 2f;
            float targetTop = top - RiseDist;
            label.style.left = left;
            label.style.top = top;
            _dragLayer.Add(label);

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => label.style.opacity.value, v => label.style.opacity = v, 1f, AppearDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => label.style.scale.value.value.x, v => label.style.scale = new Scale(new Vector3(v, v, 1f)), 1f, AppearDuration).SetEase(Ease.OutBack))
                .AppendInterval(HoldDuration)
                .Append(DOTween.To(() => top, v => { top = v; label.style.top = v; }, targetTop, FadeDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => label.style.opacity.value, v => label.style.opacity = v, 0f, FadeDuration).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }

            label.RemoveFromHierarchy();
        }

        // ─── コストエフェクト（カード中心に Prefab を再生）──────────────────

        private async UniTask PlayCostEffectAtCardAsync(CardView card, CancellationToken ct)
        {
            if (_costEffectPrefab == null)
            {
                return;
            }

            await PlayParticleAtCardAsync(card, _costEffectPrefab, ct);
        }

        private async UniTask PlayParticleAtCardAsync(CardView card, GameObject prefab, CancellationToken ct)
        {
            const int EffectLayer = 6;

            // UI Toolkit の worldBound はパネル空間座標（Y=0 が上）
            // パネル全体サイズで正規化してビューポート [0,1] に変換し、
            // ViewportPointToRay → Z=0 平面交差でワールド座標を求める
            Camera mainCam = Camera.main;

            Rect panelBounds = card.panel.visualTree.worldBound;
            Vector2 uiCenter = card.worldBound.center;
            float nx = uiCenter.x / panelBounds.width;
            float ny = 1f - uiCenter.y / panelBounds.height;
            Ray ray = mainCam.ViewportPointToRay(new Vector3(nx, ny, 0f));
            float tDist = -ray.origin.z / ray.direction.z;
            Vector3 worldPos = ray.origin + ray.direction * tDist;
            int originalCullingMask = mainCam.cullingMask;
            mainCam.cullingMask &= ~(1 << EffectLayer);

            RenderTexture rt = new RenderTexture(Screen.width, Screen.height, 0);
            rt.Create();

            GameObject camObj = new GameObject("EffectCamera");
            Camera effectCam = camObj.AddComponent<Camera>();
            effectCam.clearFlags = CameraClearFlags.SolidColor;
            effectCam.backgroundColor = Color.black;
            effectCam.cullingMask = 1 << EffectLayer;
            effectCam.targetTexture = rt;
            effectCam.fieldOfView = mainCam.fieldOfView;
            effectCam.nearClipPlane = mainCam.nearClipPlane;
            effectCam.farClipPlane = mainCam.farClipPlane;
            camObj.transform.SetPositionAndRotation(mainCam.transform.position, mainCam.transform.rotation);

            GameObject canvasObj = new GameObject("EffectCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            GameObject imgObj = new GameObject("EffectImage");
            imgObj.transform.SetParent(canvasObj.transform, false);
            RawImage img = imgObj.AddComponent<RawImage>();
            img.texture = rt;
            Material mat = new Material(Shader.Find("Custom/FireworkAdditiveUI"));
            img.material = mat;

            RectTransform effectRect = imgObj.GetComponent<RectTransform>();
            effectRect.anchorMin = Vector2.zero;
            effectRect.anchorMax = Vector2.one;
            effectRect.sizeDelta = Vector2.zero;
            effectRect.anchoredPosition = Vector2.zero;

            GameObject effect = Instantiate(prefab, worldPos, Quaternion.identity);
            SetLayerRecursive(effect, EffectLayer);

            float waitSeconds = 2f;
            ParticleSystem ps = effect.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                ParticleSystem.MainModule main = ps.main;
                float lifetime = main.startLifetime.mode == ParticleSystemCurveMode.Constant
                    ? main.startLifetime.constant
                    : main.startLifetime.constantMax;
                float duration = main.duration + lifetime;
                if (duration > 0f)
                {
                    waitSeconds = duration;
                }
            }

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(waitSeconds), cancellationToken: ct);
            }
            catch (OperationCanceledException) { }

            mainCam.cullingMask = originalCullingMask;
            Destroy(effect);
            Destroy(canvasObj);
            Destroy(camObj);
            Destroy(mat);
            rt.Release();
            Destroy(rt);
        }

        // ─── AtkBoost エフェクト（ラベル上昇 + パーティクル同時再生）────────────

        private async UniTask PlayAtkBoostEffectAsync(CardView card, int value, CancellationToken ct)
        {
            List<UniTask> tasks = new List<UniTask>();
            tasks.Add(PlayAtkBoostLabelAsync(card, value, ct));
            if (_atkBoostEffectPrefab != null)
            {
                tasks.Add(PlayParticleAtCardAsync(card, _atkBoostEffectPrefab, ct));
            }
            await UniTask.WhenAll(tasks);
        }

        private async UniTask PlayAtkBoostLabelAsync(CardView card, int value, CancellationToken ct)
        {
            const float LabelW = 200f;
            const float LabelH = 60f;
            const float RiseDist = 70f;
            const float AppearDuration = 0.2f;
            const float HoldDuration = 0.3f;
            const float FadeDuration = 0.5f;

            Label label = new Label($"ATK +{value}");
            label.AddToClassList("atk-boost-label");
            label.pickingMode = PickingMode.Ignore;
            label.style.position = Position.Absolute;
            label.style.opacity = 0f;
            label.style.scale = new Scale(new Vector3(0.7f, 0.7f, 1f));

            Vector2 cardLocal = _dragLayer.WorldToLocal(card.worldBound.center);
            float left = cardLocal.x - LabelW / 2f;
            float top = cardLocal.y - LabelH / 2f;
            float targetTop = top - RiseDist;
            label.style.left = left;
            label.style.top = top;
            _dragLayer.Add(label);

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => label.style.opacity.value, v => label.style.opacity = v, 1f, AppearDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => label.style.scale.value.value.x, v => label.style.scale = new Scale(new Vector3(v, v, 1f)), 1f, AppearDuration).SetEase(Ease.OutBack))
                .AppendInterval(HoldDuration)
                .Append(DOTween.To(() => top, v => { top = v; label.style.top = v; }, targetTop, FadeDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => label.style.opacity.value, v => label.style.opacity = v, 0f, FadeDuration).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }

            label.RemoveFromHierarchy();
        }

        // ─── DefBoost エフェクト（ラベル上昇 + パーティクル同時再生）────────────

        private async UniTask PlayDefBoostEffectAsync(CardView card, int value, CancellationToken ct)
        {
            List<UniTask> tasks = new List<UniTask>();
            tasks.Add(PlayDefBoostLabelAsync(card, value, ct));
            if (_defBoostEffectPrefab != null)
            {
                tasks.Add(PlayParticleAtCardAsync(card, _defBoostEffectPrefab, ct));
            }
            await UniTask.WhenAll(tasks);
        }

        private async UniTask PlayDefBoostLabelAsync(CardView card, int value, CancellationToken ct)
        {
            const float LabelW = 200f;
            const float LabelH = 60f;
            const float RiseDist = 70f;
            const float AppearDuration = 0.2f;
            const float HoldDuration = 0.3f;
            const float FadeDuration = 0.5f;

            Label label = new Label($"DEF +{value}");
            label.AddToClassList("def-boost-label");
            label.pickingMode = PickingMode.Ignore;
            label.style.position = Position.Absolute;
            label.style.opacity = 0f;
            label.style.scale = new Scale(new Vector3(0.7f, 0.7f, 1f));

            Vector2 cardLocal = _dragLayer.WorldToLocal(card.worldBound.center);
            float left = cardLocal.x - LabelW / 2f;
            float top = cardLocal.y - LabelH / 2f;
            float targetTop = top - RiseDist;
            label.style.left = left;
            label.style.top = top;
            _dragLayer.Add(label);

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => label.style.opacity.value, v => label.style.opacity = v, 1f, AppearDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => label.style.scale.value.value.x, v => label.style.scale = new Scale(new Vector3(v, v, 1f)), 1f, AppearDuration).SetEase(Ease.OutBack))
                .AppendInterval(HoldDuration)
                .Append(DOTween.To(() => top, v => { top = v; label.style.top = v; }, targetTop, FadeDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => label.style.opacity.value, v => label.style.opacity = v, 0f, FadeDuration).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }

            label.RemoveFromHierarchy();
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

            Rect deckRect = deck.worldBound;
            List<CardView> costCards = deck.TakeFromTop(cost);

            List<UniTask> tasks = new List<UniTask>();
            tasks.Add(PlayDeckDamageAsync(costCards, deckRect, graveyard, deck, ct));
            tasks.Add(PlayCostEffectAtCardAsync(card, ct));
            if (announce)
            {
                string costClass = deck == _playerDeckView
                    ? "turn-announcement-label--cost"
                    : "turn-announcement-label--cost-opponent";
                tasks.Add(UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: ct)
                    .ContinueWith(() => PlayAnnouncementAsync($"PAY {cost} COSTS", costClass, ct)));
            }
            await UniTask.WhenAll(tasks);

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
                _dragLayer.Add(card);           // UI Toolkit が DeckView から自動除去
                sourceDeck.OnCardRemovedVisually(); // デッキ表示を1枚分縮小
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

        // ─── 勝敗演出 ───────────────────────────────────────────────────────

        private async UniTask PlayGameEndAsync(bool? playerWins, CancellationToken ct)
        {
            if (playerWins == true && _fireworkPrefab != null)
            {
                SpawnFireworksAsync(destroyCancellationToken).Forget();
                // オーバーレイを見せる前に花火を視認させる
                try { await UniTask.Delay(300, cancellationToken: ct); }
                catch (OperationCanceledException) { }
            }

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

        private async UniTaskVoid SpawnFireworksAsync(CancellationToken ct)
        {
            const int FireworkLayer = 6;

            // 花火を RenderTexture に描画し、加算ブレンドの RawImage で UI の上に重ねる
            Camera mainCam = Camera.main;
            int originalCullingMask = mainCam.cullingMask;
            mainCam.cullingMask &= ~(1 << FireworkLayer);

            RenderTexture rt = new RenderTexture(Screen.width, Screen.height, 0);
            rt.Create();

            GameObject camObj = new GameObject("FireworkCamera");
            Camera fwCam = camObj.AddComponent<Camera>();
            fwCam.clearFlags = CameraClearFlags.SolidColor;
            fwCam.backgroundColor = Color.black;
            fwCam.cullingMask = 1 << FireworkLayer;
            fwCam.targetTexture = rt;
            fwCam.fieldOfView = mainCam.fieldOfView;
            fwCam.nearClipPlane = mainCam.nearClipPlane;
            fwCam.farClipPlane = mainCam.farClipPlane;
            camObj.transform.SetPositionAndRotation(mainCam.transform.position, mainCam.transform.rotation);

            GameObject canvasObj = new GameObject("FireworkCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            GameObject imgObj = new GameObject("FireworkImage");
            imgObj.transform.SetParent(canvasObj.transform, false);
            RawImage img = imgObj.AddComponent<RawImage>();
            img.texture = rt;

            Material mat = new Material(Shader.Find("Custom/FireworkAdditiveUI"));
            img.material = mat;

            RectTransform rect = imgObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            float[] xPositions = { -2.5f, 2.5f, -1f, 2f, 0f, -2f, 1.5f };
            foreach (float x in xPositions)
            {
                if (ct.IsCancellationRequested) break;
                GameObject fw = Instantiate(_fireworkPrefab, new Vector3(x, -7f, 0f), Quaternion.identity);
                SetLayerRecursive(fw, FireworkLayer);
                try { await UniTask.Delay(400, cancellationToken: ct); }
                catch (OperationCanceledException) { break; }
            }

            try { await UniTask.Delay(4000, cancellationToken: ct); }
            catch (OperationCanceledException) { }

            mainCam.cullingMask = originalCullingMask;
            Destroy(canvasObj);
            Destroy(camObj);
            Destroy(mat);
            rt.Release();
            Destroy(rt);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursive(child.gameObject, layer);
            }
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

        // ─── トースト ────────────────────────────────────────────────────

        private void ShowToast(string message)
        {
            _toastCts?.Cancel();
            _toastCts?.Dispose();
            _toastCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            ShowToastAsync(message, _toastCts.Token).Forget();
        }

        private async UniTask ShowToastAsync(string message, CancellationToken ct)
        {
            _toastLabel.text = message;
            _toastContainer.style.display = DisplayStyle.Flex;
            _toastContainer.style.opacity = 0f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Append(DOTween.To(
                    () => _toastContainer.style.opacity.value,
                    v => _toastContainer.style.opacity = v,
                    1f, 0.15f).SetEase(Ease.OutQuad))
                .AppendInterval(0.9f)
                .Append(DOTween.To(
                    () => _toastContainer.style.opacity.value,
                    v => _toastContainer.style.opacity = v,
                    0f, 0.4f).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try
            {
                await tcs.Task;
                _toastContainer.style.display = DisplayStyle.None;
            }
            catch (OperationCanceledException) { }
        }
    }
}
