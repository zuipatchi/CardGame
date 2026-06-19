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
        // ─── コイントス演出 ──────────────────────────────────────────────

        private async UniTask PlayCoinTossAsync(bool isLocalFirst, string resultText, string resultCssClass, CancellationToken ct)
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

            Label resultLabel = new Label(resultText);
            resultLabel.AddToClassList("turn-announcement-label");
            resultLabel.AddToClassList(resultCssClass);
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
            // 結果ラベル（先攻！/後攻！）の登場に合わせて Ready SE を鳴らす
            seq.AppendCallback(() =>
            {
                if (_soundStore.ReadySE != null)
                {
                    _soundPlayer.PlaySE(_soundStore.ReadySE);
                }
            });
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
            catch (System.OperationCanceledException) { }

            overlay.RemoveFromHierarchy();
        }

        // ─── 攻撃優先権行使演出（コイン飛翔） ────────────────────────────────

        private async UniTask PlayPriorityCoinTransferAsync(bool localUsedPriority, CancellationToken ct)
        {
            const float CoinSize = 72f;
            const float CoinGap = 8f;
            const float FlyToCenterDuration = 0.45f;
            const float FlyToDestDuration = 0.45f;

            VisualElement sourceCoin = localUsedPriority ? _playerPriorityCoin : _opponentPriorityCoin;
            GraveyardView destGraveyard = localUsedPriority ? _opponentGraveyardView : _playerGraveyardView;

            // ゴーストコインをソース位置に作成
            Vector2 sourceLocal = _dragLayer.WorldToLocal(sourceCoin.worldBound.center);
            VisualElement ghostCoin = new VisualElement();
            ghostCoin.AddToClassList("priority-coin");
            ghostCoin.pickingMode = PickingMode.Ignore;
            ghostCoin.style.position = Position.Absolute;
            ghostCoin.style.width = CoinSize;
            ghostCoin.style.height = CoinSize;
            if (_cardStore.CoinFront != null)
            {
                ghostCoin.style.backgroundImage = Background.FromSprite(_cardStore.CoinFront);
            }
            float ghostLeft = sourceLocal.x - CoinSize / 2f;
            float ghostTop = sourceLocal.y - CoinSize / 2f;
            ghostCoin.style.left = ghostLeft;
            ghostCoin.style.top = ghostTop;
            _dragLayer.Add(ghostCoin);
            sourceCoin.style.display = DisplayStyle.None;

            // 画面中央のローカル座標
            Rect dragLayerBounds = _dragLayer.worldBound;
            Vector2 centerLocal = _dragLayer.WorldToLocal(new Vector2(dragLayerBounds.center.x, dragLayerBounds.center.y));
            float centerLeft = centerLocal.x - CoinSize / 2f;
            float centerTop = centerLocal.y - CoinSize / 2f;

            // Phase 1: アナウンス表示と同時にコインを画面中央へ飛翔
            string announceLabelClass = localUsedPriority
                ? "turn-announcement-label--player"
                : "turn-announcement-label--enemy";
            UniTask announceTask = PlayAnnouncementAsync("優先権 行使！", announceLabelClass, ct);

            UniTaskCompletionSource tcs1 = new UniTaskCompletionSource();
            Sequence seq1 = DOTween.Sequence()
                .Append(DOTween.To(() => ghostLeft, v => { ghostLeft = v; ghostCoin.style.left = v; }, centerLeft, FlyToCenterDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => ghostTop, v => { ghostTop = v; ghostCoin.style.top = v; }, centerTop, FlyToCenterDuration).SetEase(Ease.OutQuad))
                .OnComplete(() => tcs1.TrySetResult());
            ct.Register(() => { seq1.Kill(); tcs1.TrySetCanceled(); });

            bool phase1Done = false;
            try { await UniTask.WhenAll(announceTask, tcs1.Task); phase1Done = true; }
            catch (System.OperationCanceledException) { }

            // Phase 2: アナウンス消去後にコインを相手の墓地隣へ飛翔
            if (phase1Done)
            {
                Rect destBounds = destGraveyard.worldBound;
                float destWorldX = localUsedPriority
                    ? (destBounds.xMax + CoinGap + CoinSize / 2f)
                    : (destBounds.xMin - CoinGap - CoinSize / 2f);
                Vector2 destLocal = _dragLayer.WorldToLocal(new Vector2(destWorldX, destBounds.center.y));
                float targetLeft = destLocal.x - CoinSize / 2f;
                float targetTop = destLocal.y - CoinSize / 2f;

                UniTaskCompletionSource tcs2 = new UniTaskCompletionSource();
                Sequence seq2 = DOTween.Sequence()
                    .Append(DOTween.To(() => ghostLeft, v => { ghostLeft = v; ghostCoin.style.left = v; }, targetLeft, FlyToDestDuration).SetEase(Ease.InOutQuad))
                    .Join(DOTween.To(() => ghostTop, v => { ghostTop = v; ghostCoin.style.top = v; }, targetTop, FlyToDestDuration).SetEase(Ease.InOutQuad))
                    .OnComplete(() => tcs2.TrySetResult());
                ct.Register(() => { seq2.Kill(); tcs2.TrySetCanceled(); });

                try { await tcs2.Task; }
                catch (System.OperationCanceledException) { }
            }

            if (ghostCoin.parent != null) { ghostCoin.RemoveFromHierarchy(); }
        }
    }
}
