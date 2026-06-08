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
        // ─── ATKカウンター演出 ────────────────────────────────────────────

        private async UniTask PlaySingleSideAtkCounterAsync(
            VisualElement atkOverlay, Label atkLabel, int atk, CancellationToken ct)
        {
            const float countDuration = 0.8f;
            const float holdDuration = 0.3f;

            atkOverlay.BringToFront();
            atkLabel.text = "0";
            atkOverlay.style.display = DisplayStyle.Flex;
            atkOverlay.style.opacity = 0f;

            float val = 0f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => atkOverlay.style.opacity.value, v => atkOverlay.style.opacity = v, 1f, 0.2f))
                .Join(DOTween.To(() => val, v => { val = v; atkLabel.text = Mathf.RoundToInt(v).ToString(); }, atk, countDuration).SetEase(Ease.OutQuad))
                .AppendInterval(holdDuration)
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try
            {
                await tcs.Task;
            }
            catch (System.OperationCanceledException) { }
        }

        // ─── ATKカウンターオーバーレイが対象キャラへ突撃 ────────────────────

        private async UniTask PlayAttackIconAsync(
            VisualElement atkOverlay, VisualElement defender, int atk, CancellationToken ct)
        {
            Rect fromRect = atkOverlay.worldBound;
            Rect toRect = defender.worldBound;
            float w = fromRect.width;
            float h = fromRect.height;

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
