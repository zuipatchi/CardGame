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

        // ─── コインドロー用の軽量コイン演出（カード付近で素早く1回振る）───────
        // コインドロー（Draw 値2フラグ）で1トスごとに呼ぶ。isHeads=true で表（＝ドロー継続）、
        // false で裏（＝終了）に着地する。全画面の PlayCoinTossAsync と違い、アンカーの上で
        // 短時間に回るため連続トスでもテンポを損なわない。
        internal async UniTask PlayCoinFlipAsync(VisualElement anchor, bool isHeads, CancellationToken ct)
        {
            const float CoinSize = 96f;
            const float RiseOffset = 96f;

            Sprite coinFront = _cardStore.CoinFront;
            Sprite coinBack = _cardStore.CoinBack;

            VisualElement coin = new VisualElement();
            coin.pickingMode = PickingMode.Ignore;
            coin.style.position = Position.Absolute;
            coin.style.width = CoinSize;
            coin.style.height = CoinSize;
            coin.style.opacity = 0f;
            if (coinFront != null)
            {
                coin.style.backgroundImage = Background.FromSprite(coinFront);
            }

            Vector2 anchorLocal = _dragLayer.WorldToLocal(anchor.worldBound.center);
            coin.style.left = anchorLocal.x - CoinSize / 2f;
            coin.style.top = anchorLocal.y - CoinSize / 2f - RiseOffset;
            _dragLayer.Add(coin);

            float scaleX = 1f;
            bool showFront = true;

            // 各半周期（横向きで表裏を入れ替えながら回転）。奇数個でループ終了時 scaleX=0 → 着地面へ settle。
            float[] halfPeriods = { 0.06f, 0.06f, 0.06f, 0.08f, 0.10f };

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Append(DOTween.To(() => coin.style.opacity.value, v => coin.style.opacity = v, 1f, 0.1f).SetEase(Ease.OutQuad));

            for (int i = 0; i < halfPeriods.Length; i++)
            {
                float target = (i % 2 == 0) ? 0f : 1f;
                seq.Append(DOTween.To(() => scaleX, v =>
                {
                    scaleX = v;
                    coin.style.scale = new Scale(new Vector3(v, 1f, 1f));
                }, target, halfPeriods[i]).SetEase(Ease.Linear));

                // scaleX が 0 になったタイミング（横向き）で表裏を入れ替える
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

            // settle 直前に着地面（表／裏）を確定してから scaleX を 1 へ戻す
            seq.AppendCallback(() =>
            {
                Sprite settle = isHeads ? coinFront : coinBack;
                if (settle != null)
                {
                    coin.style.backgroundImage = Background.FromSprite(settle);
                }
            });
            seq.Append(DOTween.To(() => scaleX, v =>
            {
                scaleX = v;
                coin.style.scale = new Scale(new Vector3(v, 1f, 1f));
            }, 1f, 0.14f).SetEase(Ease.OutBack));

            seq.AppendInterval(0.3f);
            seq.Append(DOTween.To(() => coin.style.opacity.value, v => coin.style.opacity = v, 0f, 0.2f).SetEase(Ease.InQuad));
            seq.OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (System.OperationCanceledException) { }

            coin.RemoveFromHierarchy();
        }

        // ─── サイコロドロー用のダイス演出（カード付近で転がして出目に着地）───────
        // サイコロドロー（Draw 値2=2）で呼ぶ。result（1〜6）の出目に着地する。転がし中の見せ数字は
        // 演出のみで出目とは無関係（クライアント間でズレても問題ない）。出目 result は同期済み。
        internal async UniTask PlayDiceRollAsync(VisualElement anchor, int result, CancellationToken ct)
        {
            const float DiceSize = 120f;
            const float RiseOffset = 110f;

            VisualElement container = new VisualElement();
            container.pickingMode = PickingMode.Ignore;
            container.style.position = Position.Absolute;
            container.style.width = DiceSize;
            container.style.height = DiceSize;
            container.style.opacity = 0f;
            container.style.alignItems = Align.Center;
            container.style.justifyContent = Justify.Center;

            VisualElement dice = new VisualElement();
            dice.AddToClassList("dice-roll-icon");
            dice.pickingMode = PickingMode.Ignore;
            container.Add(dice);

            Label number = new Label("1");
            number.AddToClassList("dice-roll-number");
            number.pickingMode = PickingMode.Ignore;
            container.Add(number);

            Vector2 anchorLocal = _dragLayer.WorldToLocal(anchor.worldBound.center);
            container.style.left = anchorLocal.x - DiceSize / 2f;
            container.style.top = anchorLocal.y - DiceSize / 2f - RiseOffset;
            _dragLayer.Add(container);

            float rotation = 0f;
            int tick = 0;

            // 転がし：ダイス本体を回転させながら見せ数字を高速に入れ替え、だんだん遅くする
            float[] tickDurations = { 0.05f, 0.05f, 0.05f, 0.06f, 0.08f, 0.11f, 0.14f };

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Append(DOTween.To(() => container.style.opacity.value, v => container.style.opacity = v, 1f, 0.1f).SetEase(Ease.OutQuad));

            float buildRotation = 0f;
            foreach (float dur in tickDurations)
            {
                seq.AppendCallback(() =>
                {
                    tick++;
                    // 見せ数字（演出のみ）。5 と 6 が互いに素なので 1〜6 全面を巡回する。
                    number.text = (((tick * 5) % 6) + 1).ToString();
                });
                buildRotation += 90f;
                float end = buildRotation;
                seq.Append(DOTween.To(() => rotation, v =>
                {
                    rotation = v;
                    dice.style.rotate = new Rotate(new Angle(v));
                }, end, dur).SetEase(Ease.Linear));
            }

            // settle：出目を確定し、次の 360 度の倍数まで回して直立で止める（軽くバウンド）
            float settleRotation = Mathf.Ceil(buildRotation / 360f) * 360f;
            if (settleRotation <= buildRotation)
            {
                settleRotation += 360f;
            }
            seq.AppendCallback(() => number.text = result.ToString());
            seq.Append(DOTween.To(() => rotation, v =>
            {
                rotation = v;
                dice.style.rotate = new Rotate(new Angle(v));
            }, settleRotation, 0.22f).SetEase(Ease.OutBack));

            seq.AppendInterval(0.5f);
            seq.Append(DOTween.To(() => container.style.opacity.value, v => container.style.opacity = v, 0f, 0.25f).SetEase(Ease.InQuad));
            seq.OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (System.OperationCanceledException) { }

            container.RemoveFromHierarchy();
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
