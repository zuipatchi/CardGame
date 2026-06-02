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
        // ─── ATKカウンター・ダメージ式演出 ───────────────────────────────

        private async UniTask PlaySingleSideAtkCounterAsync(
            VisualElement atkOverlay, Label atkLabel, int atk,
            CharacterSlotView defSlot, int def,
            CancellationToken ct)
        {
            const float countDuration = 0.8f;
            const float holdDuration = 0.3f;

            atkOverlay.BringToFront();
            atkLabel.text = "0";
            atkOverlay.style.display = DisplayStyle.Flex;
            atkOverlay.style.opacity = 0f;

            bool showDef = defSlot.CurrentCard != null;
            if (showDef)
            {
                defSlot.SetDefValue(def);
                defSlot.DefOverlay.BringToFront();
                defSlot.DefOverlay.style.display = DisplayStyle.Flex;
                defSlot.DefOverlay.style.opacity = 0f;
            }

            float val = 0f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => atkOverlay.style.opacity.value, v => atkOverlay.style.opacity = v, 1f, 0.2f));

            if (showDef)
            {
                seq.Join(DOTween.To(() => defSlot.DefOverlay.style.opacity.value, v => defSlot.DefOverlay.style.opacity = v, 1f, 0.2f));
            }

            seq.Join(DOTween.To(() => val, v => { val = v; atkLabel.text = Mathf.RoundToInt(v).ToString(); }, atk, countDuration).SetEase(Ease.OutQuad))
                .AppendInterval(holdDuration)
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try
            {
                await tcs.Task;
            }
            catch (System.OperationCanceledException) { }

            // overlays はここで非表示にしない — 呼び出し側（Phases.cs）が処理する
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
            catch (System.OperationCanceledException) { }
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
                (float windupLeft, float windupTop, float targetLeft, float targetTop, float facingAngle, Vector2 kbEnd) = ComputeAttackGeometry(fromCenter, toCenter, CardWidth / 2f, CardHeight / 2f);
                float rotAngle = 0f;
                float kbT = 0f;

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
            catch (System.OperationCanceledException) { }

            foreach (CardView card in skills)
            {
                if (card.parent == _dragLayer)
                {
                    _dragLayer.Remove(card);
                }
            }
        }

        // ─── ATKカウンターオーバーレイが相手キャラスロットへ突撃 ──────────────

        private async UniTask PlayAttackIconAsync(
            VisualElement atkOverlay, CharacterSlotView defender, int atk, CancellationToken ct)
        {
            Rect fromRect = atkOverlay.worldBound;
            Rect toRect = defender.worldBound;
            float w = fromRect.width;
            float h = fromRect.height;

            // ATKカウンターと同じ見た目の飛翔要素を生成し、元のオーバーレイを隠す
            VisualElement flyingOverlay = new VisualElement();
            flyingOverlay.pickingMode = PickingMode.Ignore;
            flyingOverlay.style.position = Position.Absolute;
            flyingOverlay.style.width = w;
            flyingOverlay.style.height = h;
            flyingOverlay.style.alignItems = Align.Center;
            flyingOverlay.style.justifyContent = Justify.Center;
            flyingOverlay.style.flexDirection = FlexDirection.Column;
            float flyLeft = fromRect.x;
            float flyTop = fromRect.y;
            flyingOverlay.style.left = flyLeft;
            flyingOverlay.style.top = flyTop;

            VisualElement iconWrapper = new VisualElement();
            iconWrapper.pickingMode = PickingMode.Ignore;
            iconWrapper.AddToClassList("atk-counter-icon-wrapper");
            flyingOverlay.Add(iconWrapper);

            VisualElement icon = new VisualElement();
            icon.pickingMode = PickingMode.Ignore;
            icon.AddToClassList("atk-counter-icon");
            iconWrapper.Add(icon);

            Label atkLabel = new Label(atk.ToString());
            atkLabel.pickingMode = PickingMode.Ignore;
            atkLabel.AddToClassList("atk-counter-label");
            flyingOverlay.Add(atkLabel);

            atkOverlay.style.display = DisplayStyle.None;
            _dragLayer.Add(flyingOverlay);

            Vector2 fromCenter = fromRect.center;
            Vector2 toCenter = toRect.center;
            (float windupLeft, float windupTop, float targetLeft, float targetTop, float facingAngle, Vector2 kbEnd) = ComputeAttackGeometry(fromCenter, toCenter, w / 2f, h / 2f);
            float rotAngle = 0f;
            float kbT = 0f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => flyLeft, v => { flyLeft = v; flyingOverlay.style.left = v; }, windupLeft, AttackWindupDuration).SetEase(Ease.OutSine))
                .Join(DOTween.To(() => flyTop, v => { flyTop = v; flyingOverlay.style.top = v; }, windupTop, AttackWindupDuration).SetEase(Ease.OutSine))
                .Join(DOTween.To(() => rotAngle, v =>
                {
                    rotAngle = v;
                    iconWrapper.style.rotate = new Rotate(new Angle(v, AngleUnit.Degree));
                }, facingAngle, AttackWindupDuration).SetEase(Ease.OutSine))
                .Append(DOTween.To(() => flyLeft, v => { flyLeft = v; flyingOverlay.style.left = v; }, targetLeft, AttackFlyDuration).SetEase(Ease.InCubic))
                .Join(DOTween.To(() => flyTop, v => { flyTop = v; flyingOverlay.style.top = v; }, targetTop, AttackFlyDuration).SetEase(Ease.InCubic))
                .Append(DOTween.To(() => kbT, v =>
                {
                    kbT = v;
                    Vector2 pos = Vector2.Lerp(toCenter, kbEnd, v);
                    flyingOverlay.style.left = pos.x - w / 2f;
                    flyingOverlay.style.top = pos.y - h / 2f;
                }, 1f, AttackKnockbackDuration).SetEase(Ease.OutQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try
            {
                await tcs.Task;
            }
            catch (System.OperationCanceledException) { }

            if (flyingOverlay.parent != null)
            {
                flyingOverlay.RemoveFromHierarchy();
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
            (float windupLeft, float windupTop, float targetLeft, float targetTop, float facingAngle, Vector2 kbEnd) = ComputeAttackGeometry(fromCenter, toCenter, CardWidth / 2f, CardHeight / 2f);
            float rotAngle = 0f;

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
            catch (System.OperationCanceledException) { }

            if (flyingAtk != null && flyingAtk.parent != null)
            {
                flyingAtk.RemoveFromHierarchy();
            }

            if (card.parent == _dragLayer)
            {
                await FlyCharToSlotAsync(card, card.worldBound, slot, ct);
            }
        }

        // ─── 攻撃ジオメトリ計算 ──────────────────────────────────────────

        private static (float windupLeft, float windupTop, float targetLeft, float targetTop, float facingAngle, Vector2 kbEnd)
            ComputeAttackGeometry(Vector2 fromCenter, Vector2 toCenter, float halfW, float halfH)
        {
            Vector2 dir = (toCenter - fromCenter).normalized;
            float facingAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f;
            Vector2 windupCenter = fromCenter - dir * AttackWindupDistance;
            return (
                windupCenter.x - halfW, windupCenter.y - halfH,
                toCenter.x - halfW, toCenter.y - halfH,
                facingAngle,
                toCenter - dir * AttackKnockbackDistance
            );
        }
    }
}
