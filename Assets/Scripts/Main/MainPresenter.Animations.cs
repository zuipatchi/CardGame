using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // ─── ターン・フェーズ告知アニメーション ────────────────────────────

        private async UniTask PlayPassAnimationAsync(bool isLocal, CancellationToken ct)
        {
            string labelClass = isLocal ? "turn-announcement-label--player" : "turn-announcement-label--enemy";
            await PlayAnnouncementAsync("PASS", labelClass, ct);
        }

        // ターン開始告知：自分の番は「YOUR TURN」（シアン）、相手の番は「ENEMY TURN」（赤）
        private async UniTask PlayTurnStartAnnouncementAsync(bool isLocalTurn, CancellationToken ct)
        {
            string text = isLocalTurn ? "YOUR TURN" : "ENEMY TURN";
            string labelClass = isLocalTurn ? "turn-announcement-label--player" : "turn-announcement-label--enemy";
            await PlayAnnouncementAsync(text, labelClass, ct);
        }

        // オーバーリミット告知：デッキが初めて0枚になった瞬間（リミットブレイク）に「リミットブレイク！」を表示する。
        private async UniTask PlayLimitBreakAnnouncementAsync(CancellationToken ct)
        {
            await PlayAnnouncementAsync("リミットブレイク！", "turn-announcement-label--limit-break", ct);
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
            _turnLabel.RemoveFromClassList("turn-announcement-label--draw");
            _turnLabel.RemoveFromClassList("turn-announcement-label--limit-break");
            _turnLabel.RemoveFromClassList("turn-announcement-label--cost");
            _turnLabel.RemoveFromClassList("turn-announcement-label--cost-opponent");
            _turnLabel.RemoveFromClassList("turn-announcement-label--set");
            _turnLabel.AddToClassList(labelClass);

            _turnOverlay.style.display = DisplayStyle.Flex;
            _turnOverlay.style.opacity = 0f;
            _turnLabel.style.scale = new Scale(new Vector3(0.5f, 0.5f, 1f));

            float overlayOpacity = 0f;
            float labelScale = 0.5f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => overlayOpacity, v => { overlayOpacity = v; _turnOverlay.style.opacity = v; }, 1f, 0.25f).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => labelScale, v => { labelScale = v; _turnLabel.style.scale = new Scale(new Vector3(v, v, 1f)); }, 1f, 0.25f).SetEase(Ease.OutBack))
                .AppendInterval(0.5f)
                .Append(DOTween.To(() => overlayOpacity, v => { overlayOpacity = v; _turnOverlay.style.opacity = v; }, 0f, 0.3f).SetEase(Ease.InQuad))
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

            float overlayOpacity = 0f;
            float labelScale = 0.5f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => overlayOpacity, v => { overlayOpacity = v; _resolveOverlay.style.opacity = v; }, 1f, 0.25f).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => labelScale, v => { labelScale = v; _resolveLabel.style.scale = new Scale(new Vector3(v, v, 1f)); }, 1f, 0.25f).SetEase(Ease.OutBack))
                .AppendInterval(0.4f)
                .Append(DOTween.To(() => overlayOpacity, v => { overlayOpacity = v; _resolveOverlay.style.opacity = v; }, 0f, 0.3f).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }

            _resolveOverlay.style.display = DisplayStyle.None;
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

        // ─── VS 演出 ──────────────────────────────────────────────────────

        private async UniTask PlayVsAnnouncementAsync(
            string localName,
            string opponentName,
            CancellationToken ct)
        {
            VisualElement overlay = new VisualElement();
            overlay.AddToClassList("vs-overlay");
            overlay.pickingMode = PickingMode.Ignore;
            overlay.style.opacity = 0f;

            VisualElement container = new VisualElement();
            container.AddToClassList("vs-container");
            container.pickingMode = PickingMode.Ignore;
            overlay.Add(container);

            VisualElement leftPanel = BuildVsPanel("vs-panel--local", localName);
            container.Add(leftPanel);

            Label vsLabel = new Label("VS");
            vsLabel.AddToClassList("vs-label");
            vsLabel.pickingMode = PickingMode.Ignore;
            vsLabel.style.opacity = 0f;
            vsLabel.style.scale = new Scale(new Vector3(0.3f, 0.3f, 1f));
            container.Add(vsLabel);

            VisualElement rightPanel = BuildVsPanel("vs-panel--opponent", opponentName);
            container.Add(rightPanel);

            _mainRoot.Add(overlay);

            float overlayOpacity = 0f;
            float leftX = -600f;
            float rightX = 600f;
            float vsOpacity = 0f;
            float vsScale = 0.3f;

            leftPanel.style.translate = new StyleTranslate(new Translate(leftX, 0f));
            rightPanel.style.translate = new StyleTranslate(new Translate(rightX, 0f));

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Append(DOTween.To(() => overlayOpacity, v => { overlayOpacity = v; overlay.style.opacity = v; }, 1f, 0.3f).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => leftX, v => { leftX = v; leftPanel.style.translate = new StyleTranslate(new Translate(v, 0f)); }, 0f, 0.4f).SetEase(Ease.OutCubic))
                .Join(DOTween.To(() => rightX, v => { rightX = v; rightPanel.style.translate = new StyleTranslate(new Translate(v, 0f)); }, 0f, 0.4f).SetEase(Ease.OutCubic))
                .Append(DOTween.To(() => vsOpacity, v => { vsOpacity = v; vsLabel.style.opacity = v; }, 1f, 0.25f).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => vsScale, v => { vsScale = v; vsLabel.style.scale = new Scale(new Vector3(v, v, 1f)); }, 1f, 0.25f).SetEase(Ease.OutBack))
                .AppendInterval(1.0f)
                .Append(DOTween.To(() => overlayOpacity, v => { overlayOpacity = v; overlay.style.opacity = v; }, 0f, 0.4f).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }

            overlay.RemoveFromHierarchy();
        }

        private static VisualElement BuildVsPanel(string colorClass, string name)
        {
            VisualElement panel = new VisualElement();
            panel.AddToClassList("vs-panel");
            panel.AddToClassList(colorClass);
            panel.pickingMode = PickingMode.Ignore;

            Label nameLabel = new Label(name);
            nameLabel.AddToClassList("vs-player-name");
            nameLabel.pickingMode = PickingMode.Ignore;
            panel.Add(nameLabel);

            return panel;
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
