using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Main.Card;
using Main.Game;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace Main
{
    public sealed partial class MainPresenter
    {
        internal async UniTask PlayFloatingLabelAsync(string text, string cssClass, VisualElement anchor, CancellationToken ct)
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

            Vector2 anchorLocal = _dragLayer.WorldToLocal(anchor.worldBound.center);
            float left = anchorLocal.x - LabelW / 2f;
            float top = anchorLocal.y - LabelH / 2f;
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

            // 演出終了後の共通の余韻ディレイ。パーティクルと並行（WhenAll）で使われる場合も
            // 双方に同じ待ちが付くだけで、WhenAll は最大値を取るため二重にはならない。
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(EffectTrailingDelaySeconds), cancellationToken: ct);
            }
            catch (OperationCanceledException) { }
        }

        // ─── MedalIcon フローティング（勝利点を得るカードの上に表示）──────
        // amount は得る勝利点。1〜5 はそれぞれ Medal1〜Medal5Icon を表示し、0 や 6 以上は Medal1Icon を表示する。
        internal async UniTask PlayFloatingMedalAsync(VisualElement anchor, int amount, CancellationToken ct)
        {
            const float IconSize = 140f;
            const float RiseDist = 80f;
            const float AppearDuration = 0.2f;
            const float HoldDuration = 0.35f;
            const float FadeDuration = 0.45f;

            VisualElement container = new VisualElement();
            container.pickingMode = PickingMode.Ignore;
            container.style.position = Position.Absolute;
            container.style.width = IconSize;
            container.style.height = IconSize;
            container.style.opacity = 0f;
            container.style.scale = new Scale(new Vector3(0.5f, 0.5f, 1f));

            VisualElement icon = new VisualElement();
            icon.AddToClassList("medal-fly-icon");
            int medalIndex = (amount >= 1 && amount <= 5) ? amount : 1;
            icon.AddToClassList($"medal-fly-icon--{medalIndex}");
            icon.pickingMode = PickingMode.Ignore;
            container.Add(icon);

            Vector2 anchorLocal = _dragLayer.WorldToLocal(anchor.worldBound.center);
            float left = anchorLocal.x - IconSize / 2f;
            float top = anchorLocal.y - IconSize / 2f;
            float targetTop = top - RiseDist;
            container.style.left = left;
            container.style.top = top;
            _dragLayer.Add(container);

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => container.style.opacity.value, v => container.style.opacity = v, 1f, AppearDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => container.style.scale.value.value.x, v => container.style.scale = new Scale(new Vector3(v, v, 1f)), 1f, AppearDuration).SetEase(Ease.OutBack))
                .AppendInterval(HoldDuration)
                .Append(DOTween.To(() => top, v => { top = v; container.style.top = v; }, targetTop, FadeDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => container.style.opacity.value, v => container.style.opacity = v, 0f, FadeDuration).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }

            container.RemoveFromHierarchy();

            // 演出終了後の共通の余韻ディレイ（ラベルと同様）。
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(EffectTrailingDelaySeconds), cancellationToken: ct);
            }
            catch (OperationCanceledException) { }
        }

        // ─── シールドブロック演出（ダメージ 0 時にアイコン表示）──────────────────

        private async UniTask PlayShieldBlockEffectAsync(VisualElement anchor, CancellationToken ct)
        {
            const float IconSize = 160f;
            const float RiseDist = 50f;
            const float AppearDuration = 0.2f;
            const float HoldDuration = 0.5f;
            const float FadeDuration = 0.4f;

            VisualElement container = new VisualElement();
            container.pickingMode = PickingMode.Ignore;
            container.style.position = Position.Absolute;
            container.style.width = IconSize;
            container.style.height = IconSize;
            container.style.opacity = 0f;
            container.style.scale = new Scale(new Vector3(0.5f, 0.5f, 1f));

            VisualElement icon = new VisualElement();
            icon.AddToClassList("shield-fly-icon");
            icon.pickingMode = PickingMode.Ignore;
            container.Add(icon);

            Vector2 anchorLocal = _dragLayer.WorldToLocal(anchor.worldBound.center);
            float left = anchorLocal.x - IconSize / 2f;
            float top = anchorLocal.y - IconSize / 2f;
            float targetTop = top - RiseDist;
            container.style.left = left;
            container.style.top = top;
            _dragLayer.Add(container);

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => container.style.opacity.value, v => container.style.opacity = v, 1f, AppearDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => container.style.scale.value.value.x, v => container.style.scale = new Scale(new Vector3(v, v, 1f)), 1f, AppearDuration).SetEase(Ease.OutBack))
                .AppendInterval(HoldDuration)
                .Append(DOTween.To(() => top, v => { top = v; container.style.top = v; }, targetTop, FadeDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => container.style.opacity.value, v => container.style.opacity = v, 0f, FadeDuration).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }

            container.RemoveFromHierarchy();
        }

        // ─── AoE ダメージエフェクト（敵フィールド中央にパーティクル再生）──────────────
        // DamageAllEnemies 用。敵キャラの有無に関わらずフィールド中央で再生する。
        private async UniTask PlayAreaDamageEffectAsync(FieldView field, CancellationToken ct)
        {
            if (field == null || _areaDamageEffectPrefab == null)
            {
                return;
            }
            // フィールド全体を覆うよう少し拡大して再生する
            const float AreaDamageEffectScale = 1.8f;
            await PlayParticleAtUiPositionAsync(field, field.worldBound.center, _areaDamageEffectPrefab, ct, scale: AreaDamageEffectScale);
        }

        // ─── 勝利点獲得演出（勝利点の勝利条件）──────────────────────────────
        // 数字を from→to へカウントアップ、「+N」を浮かび上がらせ、メダルを弾ませる。
        private async UniTask PlayVictoryPointGainAsync(VictoryPointsView view, int from, int to, int amount, CancellationToken ct)
        {
            // 「+N」フローティングラベルをメダル位置に生成
            Label plus = new Label($"+{amount}");
            plus.AddToClassList("victory-points-gain-label");
            plus.pickingMode = PickingMode.Ignore;
            plus.style.position = Position.Absolute;
            Vector2 center = _dragLayer.WorldToLocal(view.worldBound.center);
            float startTop = center.y;
            plus.style.left = center.x;
            plus.style.top = startTop;
            plus.style.opacity = 0f;
            _dragLayer.Add(plus);

            int shown = from;
            float pulse = 1f;
            float floatTop = startTop;

            // 先に「+N」フローティング（2）とメダルの弾み（3）を同時、その後に数字カウントアップ（1）
            const float CountUpStart = 0.55f;
            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                // メダルを弾ませる（3）
                .Append(DOTween.To(() => pulse, v => { pulse = v; view.style.scale = new Scale(new Vector3(v, v, 1f)); }, 1.45f, 0.15f).SetEase(Ease.OutQuad))
                .Insert(0.15f, DOTween.To(() => pulse, v => { pulse = v; view.style.scale = new Scale(new Vector3(v, v, 1f)); }, 1f, 0.4f).SetEase(Ease.OutBack))
                // 「+N」を出現 → 上昇しながらフェードアウト（2）
                .Insert(0f, DOTween.To(() => plus.style.opacity.value, v => plus.style.opacity = v, 1f, 0.15f).SetEase(Ease.OutQuad))
                .Insert(0.15f, DOTween.To(() => floatTop, v => { floatTop = v; plus.style.top = v; }, startTop - 64f, 0.4f).SetEase(Ease.OutQuad))
                .Insert(0.15f, DOTween.To(() => plus.style.opacity.value, v => plus.style.opacity = v, 0f, 0.4f).SetEase(Ease.InQuad))
                // 数字カウントアップ（1）— 2・3 が終わってから
                .Insert(CountUpStart, DOTween.To(() => shown, v => { shown = v; view.SetDisplayedPoints(v); }, to, 0.5f).SetEase(Ease.OutQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }

            view.SetDisplayedPoints(to);
            view.style.scale = new Scale(Vector3.one);
            plus.RemoveFromHierarchy();
        }

        // ─── Switch エフェクト（イベントカードとキャラ位置にラベル表示）───────────────

        internal async UniTask PlaySwitchEffectAsync(CardView eventCard, CardView sacrificedChar, CancellationToken ct)
        {
            if (_switchEffectPrefab != null)
            {
                PlayParticleAtCardAsync(eventCard, _switchEffectPrefab, ct).Forget();
            }
            await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
            if (sacrificedChar == null)
            {
                return;
            }
            await PlayFloatingLabelAsync("SWITCH!", "switch-label", sacrificedChar, ct);
        }

        // ─── BanishChar エフェクト（対象キャラ位置にラベル + パーティクル同時再生）────────

        private async UniTask PlayBanishCharEffectAsync(CardView targetChar, CancellationToken ct)
        {
            if (targetChar == null)
            {
                return;
            }

            List<UniTask> tasks = new List<UniTask>();
            tasks.Add(PlayFloatingLabelAsync("BANISH!", "banish-char-label", targetChar, ct));
            if (_banishCharEffectPrefab != null)
            {
                tasks.Add(PlayParticleAtCardAsync(targetChar, _banishCharEffectPrefab, ct));
            }
            await UniTask.WhenAll(tasks);
        }

        // ─── キャラ破壊エフェクト（パーティクル再生）────────────────────────────

        private async UniTask PlayCharDestroyEffectAsync(CardView targetChar, CancellationToken ct)
        {
            if (targetChar == null)
            {
                return;
            }
            GameObject prefab = _charDestroyEffectPrefab;
            if (prefab == null)
            {
                return;
            }
            Rect bounds = targetChar.worldBound;
            Vector2 bottomCenter = new Vector2(bounds.center.x, bounds.yMax);
            await PlayParticleAtUiPositionAsync(targetChar, bottomCenter, prefab, ct);
        }

        // ─── Draw エフェクト（ラベル上昇 + パーティクル同時再生）────────────────

        internal async UniTask PlayDrawEffectAsync(CardView card, int value, CancellationToken ct)
        {
            List<UniTask> tasks = new List<UniTask>();
            tasks.Add(PlayFloatingLabelAsync($"DRAW {value}", "draw-label", card, ct));
            if (_drawEffectPrefab != null)
            {
                tasks.Add(PlayParticleAtCardAsync(card, _drawEffectPrefab, ct));
            }
            await UniTask.WhenAll(tasks);
        }

        // ─── Recover エフェクト（ラベル上昇 + パーティクル同時再生）────────────

        internal async UniTask PlayRecoverEffectAsync(CardView card, int value, CancellationToken ct)
        {
            List<UniTask> tasks = new List<UniTask>();
            tasks.Add(PlayFloatingLabelAsync($"RECOVER +{value}", "recover-label", card, ct));
            if (_recoverEffectPrefab != null)
            {
                tasks.Add(PlayParticleAtCardAsync(card, _recoverEffectPrefab, ct));
            }
            await UniTask.WhenAll(tasks);
        }

        // ─── コストエフェクト（カード中心に Prefab を再生）──────────────────

        private async UniTask PlayCostEffectAtCardAsync(CardView card, CancellationToken ct)
        {
            if (_costEffectPrefab == null)
            {
                return;
            }

            // PlaceCard 直後はレイアウトパスが未実行で worldBound が不正値になるため
            // 1フレーム待って座標を確定させる
            await UniTask.Yield(cancellationToken: ct);
            await PlayParticleAtCardAsync(card, _costEffectPrefab, ct);
        }

        private UniTask PlayParticleAtCardAsync(CardView card, GameObject prefab, CancellationToken ct, Quaternion rotation = default)
        {
            if (card.panel == null)
            {
                return UniTask.CompletedTask;
            }
            return PlayParticleAtUiPositionAsync(card, card.worldBound.center, prefab, ct, rotation);
        }

        private async UniTask PlayParticleAtUiPositionAsync(VisualElement panelRef, Vector2 uiPos, GameObject prefab, CancellationToken ct, Quaternion rotation = default, float scale = 1f)
        {
            if (panelRef.panel == null)
            {
                return;
            }

            const int EffectLayer = 6;

            // UI Toolkit の worldBound はパネル空間座標（Y=0 が上）
            // パネル全体サイズで正規化してビューポート [0,1] に変換し、
            // ViewportPointToRay → Z=0 平面交差でワールド座標を求める
            Camera mainCam = Camera.main;

            Rect panelBounds = panelRef.panel.visualTree.worldBound;
            float nx = uiPos.x / panelBounds.width;
            float ny = 1f - uiPos.y / panelBounds.height;
            Ray ray = mainCam.ViewportPointToRay(new Vector3(nx, ny, 0f));
            float tDist = -ray.origin.z / ray.direction.z;
            Vector3 worldPos = ray.origin + ray.direction * tDist;
            int originalCullingMask = mainCam.cullingMask;
            mainCam.cullingMask &= ~(1 << EffectLayer);

            RenderTexture rt = new RenderTexture(Screen.width, Screen.height, 24);
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
            Material mat = new Material(_fireworkAdditiveUIShader);
            img.material = mat;

            RectTransform effectRect = imgObj.GetComponent<RectTransform>();
            effectRect.anchorMin = Vector2.zero;
            effectRect.anchorMax = Vector2.one;
            effectRect.sizeDelta = Vector2.zero;
            effectRect.anchoredPosition = Vector2.zero;

            GameObject effect = Instantiate(prefab, worldPos, rotation == default ? Quaternion.identity : rotation);
            SetLayerRecursive(effect, EffectLayer);

            // scale 指定時はパーティクル全体を Transform スケールに追従させて縮小する
            if (!Mathf.Approximately(scale, 1f))
            {
                foreach (ParticleSystem childPs in effect.GetComponentsInChildren<ParticleSystem>())
                {
                    ParticleSystem.MainModule childMain = childPs.main;
                    childMain.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
                effect.transform.localScale *= scale;
            }

            float waitSeconds = 2f;
            ParticleSystem ps = effect.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                ParticleSystem.MainModule main = ps.main;
                float lifetime = main.startLifetime.mode == ParticleSystemCurveMode.Constant
                    ? main.startLifetime.constant
                    : main.startLifetime.constantMax;
                // 実際の再生時間（PlaybackTime）に合わせる。バースト放出だと duration（放出期間）と
                // lifetime（粒子寿命）は加算ではなく「長い方」が見た目の長さになるため max を取る
                // （+ にすると二重カウントで Prefab の PlaybackTime より長く待ってしまう）。
                // simulationSpeed で再生が速く/遅くなる分は実時間に割って補正する。
                float playbackTime = Mathf.Max(main.duration, lifetime) / Mathf.Max(0.0001f, main.simulationSpeed);
                if (playbackTime > 0f)
                {
                    waitSeconds = playbackTime;
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

            // 演出（パーティクル）が消えてから次の処理へ移る前に共通の余韻ディレイを入れる。
            // これによりカード効果全般で「演出終了 → 0.25秒 → 次の処理」のテンポを統一する。
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(EffectTrailingDelaySeconds), cancellationToken: ct);
            }
            catch (OperationCanceledException) { }
        }

        // ─── 勝敗演出 ───────────────────────────────────────────────────────

        private async UniTask PlayGameEndAsync(bool? playerWins, bool isSurrenderWin, bool isPlayerSurrender, WinReason? winReason, CancellationToken ct)
        {
            if (_mulliganOverlay != null)
            {
                _mulliganOverlay.RemoveFromHierarchy();
                _mulliganOverlay = null;
                _mulliganChoicePending = false;
            }

            if (playerWins == true && _fireworkPrefab != null)
            {
                SpawnFireworksAsync(destroyCancellationToken).Forget();
                // オーバーレイを見せる前に花火を視認させる
                try { await UniTask.Delay(300, cancellationToken: ct); }
                catch (OperationCanceledException) { }
            }

            if (playerWins == false && _rainDefeatEffectPrefab != null)
            {
                _rainDefeatEffect = Instantiate(_rainDefeatEffectPrefab, new Vector3(0f, 15f, 0f), Quaternion.identity);
            }

            _gameEndLabel.RemoveFromClassList("game-end-label--win");
            _gameEndLabel.RemoveFromClassList("game-end-label--lose");
            _gameEndLabel.RemoveFromClassList("game-end-label--draw");

            if (winReason.HasValue)
            {
                // 共通の勝利条件: 勝因ごとのテキストを勝因色で表示し、対応アイコンを紋章として出す
                bool isWin = playerWins == true;
                _gameEndLabel.text = GetWinReasonText(winReason.Value, isWin);

                Color color = GetWinReasonColor(winReason.Value);
                if (!isWin)
                {
                    color = new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 1f);
                }
                _gameEndLabel.style.color = color;

                ApplyEmblemReason(winReason.Value);
                _gameEndEmblem.style.display = DisplayStyle.Flex;
            }
            else
            {
                // 降参・タイムアウト・引き分けなど色に依らない決着
                _gameEndLabel.text = playerWins == null ? "DRAW" : playerWins.Value ? "YOU WIN" : "YOU LOSE";
                _gameEndLabel.style.color = StyleKeyword.Null;
                _gameEndLabel.AddToClassList(playerWins == null ? "game-end-label--draw"
                    : playerWins.Value ? "game-end-label--win" : "game-end-label--lose");
                _gameEndEmblem.style.display = DisplayStyle.None;
            }

            _gameEndLabel.style.scale = new Scale(new Vector3(0.3f, 0.3f, 1f));
            _gameEndEmblem.style.scale = new Scale(new Vector3(0.3f, 0.3f, 1f));

            if (isSurrenderWin)
            {
                _gameEndSubLabel.text = "対戦相手が降参しました";
                _gameEndSubLabel.style.display = DisplayStyle.Flex;
            }
            else if (isPlayerSurrender)
            {
                _gameEndSubLabel.text = "降参しました";
                _gameEndSubLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _gameEndSubLabel.style.display = DisplayStyle.None;
            }

            // 勝敗 SE をオーバーレイの登場に合わせて再生する（引き分けは鳴らさない）
            if (playerWins == true && _soundStore.WinSE != null)
            {
                _soundPlayer.PlaySE(_soundStore.WinSE);
            }
            else if (playerWins == false && _soundStore.LoseSE != null)
            {
                _soundPlayer.PlaySE(_soundStore.LoseSE);
            }

            _gameEndButtonRow.style.opacity = 0f;
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
                .Join(DOTween.To(
                    () => _gameEndEmblem.style.scale.value.value.x,
                    v => _gameEndEmblem.style.scale = new Scale(new Vector3(v, v, 1f)),
                    1f, 0.4f).SetEase(Ease.OutBack))
                .AppendInterval(0.3f)
                .Append(DOTween.To(
                    () => _gameEndButtonRow.style.opacity.value,
                    v => _gameEndButtonRow.style.opacity = v,
                    1f, 0.3f).SetEase(Ease.OutQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }
        }

        // 勝因に対応する紋章（アイコン）クラスを適用する
        private void ApplyEmblemReason(WinReason reason)
        {
            _gameEndEmblem.EnableInClassList("game-end-emblem--deckout", reason == WinReason.DeckOut);
            _gameEndEmblem.EnableInClassList("game-end-emblem--victorypoints", reason == WinReason.VictoryPoints);
            _gameEndEmblem.EnableInClassList("game-end-emblem--fieldchars", reason == WinReason.FieldChars);
        }

        // 勝因ごとの勝敗テキスト。
        private static string GetWinReasonText(WinReason reason, bool isWin)
        {
            switch (reason)
            {
                case WinReason.DeckOut:
                    return isWin ? "デッキ切れ勝利" : "デッキ切れ敗北";
                case WinReason.VictoryPoints:
                    return isWin ? "勝利点勝利" : "勝利点敗北";
                case WinReason.FieldChars:
                    return isWin ? "制圧勝利" : "制圧敗北";
                default:
                    return "";
            }
        }

        private static Color GetWinReasonColor(WinReason reason)
        {
            return reason switch
            {
                WinReason.DeckOut => new Color(0.40f, 0.65f, 0.96f, 1f),       // 青系
                WinReason.VictoryPoints => new Color(0.95f, 0.80f, 0.30f, 1f), // 金系
                WinReason.FieldChars => new Color(0.96f, 0.55f, 0.30f, 1f),    // 橙系
                _ => Color.white
            };
        }

        private async UniTaskVoid SpawnFireworksAsync(CancellationToken ct)
        {
            const int FireworkLayer = 6;

            // 花火を RenderTexture に描画し、加算ブレンドの RawImage で UI の上に重ねる
            Camera mainCam = Camera.main;
            int originalCullingMask = mainCam.cullingMask;
            mainCam.cullingMask &= ~(1 << FireworkLayer);

            RenderTexture rt = new RenderTexture(Screen.width, Screen.height, 24);
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

            Material mat = new Material(_fireworkAdditiveUIShader);
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
    }
}
