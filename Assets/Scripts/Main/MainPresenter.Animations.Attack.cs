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
        // ─── カード突撃演出 ───────────────────────────────────────────────

        private async UniTask PlayCardChargeAsync(CardView attacker, VisualElement defender, CancellationToken ct)
        {
            Rect fromRect = attacker.worldBound;
            float w = CardScaleConstants.CardWidth;
            float h = CardScaleConstants.CardHeight;
            float flyLeft = fromRect.center.x - w / 2f;
            float flyTop = fromRect.center.y - h / 2f;

            attacker.style.visibility = Visibility.Hidden;

            CardView tempCard = new CardView(
                _cardStore.CardTemplate, attacker.Data, _cardStore.CardBack,
                faceDown: false, isOpponent: attacker.IsOpponent);
            tempCard.pickingMode = PickingMode.Ignore;
            tempCard.style.position = Position.Absolute;
            tempCard.style.left = flyLeft;
            tempCard.style.top = flyTop;
            tempCard.style.width = StyleKeyword.Null;
            tempCard.style.height = StyleKeyword.Null;
            _dragLayer.Add(tempCard);

            Vector2 fromCenter = fromRect.center;
            Vector2 toCenter = defender.worldBound.center;
            (float windupLeft, float windupTop, float targetLeft, float targetTop, _, Vector2 kbEnd) =
                ComputeAttackGeometry(fromCenter, toCenter, w / 2f, h / 2f);
            float kbEndLeft = kbEnd.x - w / 2f;
            float kbEndTop = kbEnd.y - h / 2f;
            float startLeft = flyLeft;
            float startTop = flyTop;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Append(DOTween.To(() => flyLeft, v => { flyLeft = v; tempCard.style.left = v; }, windupLeft, AttackWindupDuration).SetEase(Ease.OutSine))
                .Join(DOTween.To(() => flyTop, v => { flyTop = v; tempCard.style.top = v; }, windupTop, AttackWindupDuration).SetEase(Ease.OutSine))
                .Append(DOTween.To(() => flyLeft, v => { flyLeft = v; tempCard.style.left = v; }, targetLeft, AttackFlyDuration).SetEase(Ease.InCubic))
                .Join(DOTween.To(() => flyTop, v => { flyTop = v; tempCard.style.top = v; }, targetTop, AttackFlyDuration).SetEase(Ease.InCubic))
                .Append(DOTween.To(() => flyLeft, v => { flyLeft = v; tempCard.style.left = v; }, kbEndLeft, AttackKnockbackDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => flyTop, v => { flyTop = v; tempCard.style.top = v; }, kbEndTop, AttackKnockbackDuration).SetEase(Ease.OutQuad))
                .Append(DOTween.To(() => flyLeft, v => { flyLeft = v; tempCard.style.left = v; }, startLeft, AttackChargeReturnDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => flyTop, v => { flyTop = v; tempCard.style.top = v; }, startTop, AttackChargeReturnDuration).SetEase(Ease.OutQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try
            {
                await tcs.Task;
            }
            catch (System.OperationCanceledException) { }

            if (tempCard.parent != null)
            {
                tempCard.RemoveFromHierarchy();
            }

            attacker.style.visibility = Visibility.Visible;
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
