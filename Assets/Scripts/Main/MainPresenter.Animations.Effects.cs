using System;
using System.Collections.Generic;
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
        private async UniTask PlayFloatingLabelAsync(string text, string cssClass, VisualElement anchor, CancellationToken ct)
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
        }

        // ─── Switch エフェクト（自分のキャラスロット位置にラベル表示）──────────────

        private async UniTask PlaySwitchEffectAsync(CardView eventCard, CharacterSlotView slot, CancellationToken ct)
        {
            if (_switchEffectPrefab != null)
            {
                PlayParticleAtCardAsync(eventCard, _switchEffectPrefab, ct).Forget();
            }
            await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
            if (slot.CurrentCard == null)
            {
                return;
            }
            await PlayFloatingLabelAsync("SWITCH!", "switch-label", slot.CurrentCard, ct);
        }

        // ─── BanishChar エフェクト（対象キャラスロット位置にラベル + パーティクル同時再生）────

        private async UniTask PlayBanishCharEffectAsync(CharacterSlotView targetSlot, CancellationToken ct)
        {
            if (targetSlot.CurrentCard == null)
            {
                return;
            }

            List<UniTask> tasks = new List<UniTask>();
            tasks.Add(PlayFloatingLabelAsync("BANISH!", "banish-char-label", targetSlot, ct));
            if (_banishCharEffectPrefab != null)
            {
                tasks.Add(PlayParticleAtCardAsync(targetSlot.CurrentCard, _banishCharEffectPrefab, ct));
            }
            await UniTask.WhenAll(tasks);
        }

        // ─── Poison エフェクト（キャラスロット位置にパーティクル再生）──────────────

        private async UniTask PlayPoisonEffectAsync(CharacterSlotView slot, CancellationToken ct)
        {
            if (slot.CurrentCard == null || _poisonEffectPrefab == null)
            {
                return;
            }
            await PlayParticleAtCardAsync(slot.CurrentCard, _poisonEffectPrefab, ct);
        }

        // ─── キャラ破壊エフェクト（パーティクル再生）────────────────────────────

        private async UniTask PlayCharDestroyEffectAsync(CharacterSlotView slot, CancellationToken ct)
        {
            if (slot.CurrentCard == null)
            {
                return;
            }
            GameObject prefab = _charDestroyEffectPrefab;
            if (prefab == null)
            {
                return;
            }
            CardView card = slot.CurrentCard;
            Rect bounds = card.worldBound;
            Vector2 bottomCenter = new Vector2(bounds.center.x, bounds.yMax);
            await PlayParticleAtUiPositionAsync(card, bottomCenter, prefab, ct);
        }

        // ─── Draw エフェクト（ラベル上昇 + パーティクル同時再生）────────────────

        private async UniTask PlayDrawEffectAsync(CardView card, int value, CancellationToken ct)
        {
            List<UniTask> tasks = new List<UniTask>();
            tasks.Add(PlayFloatingLabelAsync($"DRAW +{value}", "draw-label", card, ct));
            if (_drawEffectPrefab != null)
            {
                tasks.Add(PlayParticleAtCardAsync(card, _drawEffectPrefab, ct));
            }
            await UniTask.WhenAll(tasks);
        }

        // ─── Recover エフェクト（ラベル上昇 + パーティクル同時再生）────────────

        private async UniTask PlayRecoverEffectAsync(CardView card, int value, CancellationToken ct)
        {
            List<UniTask> tasks = new List<UniTask>();
            tasks.Add(PlayFloatingLabelAsync($"RECOVER +{value}", "recover-label", card, ct));
            if (_recoverEffectPrefab != null)
            {
                tasks.Add(PlayParticleAtCardAsync(card, _recoverEffectPrefab, ct));
            }
            await UniTask.WhenAll(tasks);
        }

        // ─── AtkBoost エフェクト（ラベル上昇 + パーティクル同時再生）────────────

        private async UniTask PlayAtkBoostEffectAsync(CardView card, int value, CancellationToken ct)
        {
            List<UniTask> tasks = new List<UniTask>();
            tasks.Add(PlayFloatingLabelAsync($"ATK +{value}", "atk-boost-label", card, ct));
            if (_atkBoostEffectPrefab != null)
            {
                tasks.Add(PlayParticleAtCardAsync(card, _atkBoostEffectPrefab, ct));
            }
            await UniTask.WhenAll(tasks);
        }

        // ─── DefBoost エフェクト（ラベル上昇 + パーティクル同時再生）────────────

        private async UniTask PlayDefBoostEffectAsync(CardView card, int value, CancellationToken ct)
        {
            List<UniTask> tasks = new List<UniTask>();
            tasks.Add(PlayFloatingLabelAsync($"DEF +{value}", "def-boost-label", card, ct));
            if (_defBoostEffectPrefab != null)
            {
                tasks.Add(PlayParticleAtCardAsync(card, _defBoostEffectPrefab, ct));
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

        private async UniTask PlayParticleAtUiPositionAsync(VisualElement panelRef, Vector2 uiPos, GameObject prefab, CancellationToken ct, Quaternion rotation = default)
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

        // ─── 勝敗演出 ───────────────────────────────────────────────────────

        private async UniTask PlayGameEndAsync(bool? playerWins, bool isSurrenderWin, bool isPlayerSurrender, CancellationToken ct)
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

            string text = playerWins == null ? "DRAW" : playerWins.Value ? "YOU WIN" : "YOU LOSE";
            string labelClass = playerWins == null ? "game-end-label--draw"
                : playerWins.Value ? "game-end-label--win" : "game-end-label--lose";

            _gameEndLabel.text = text;
            _gameEndLabel.RemoveFromClassList("game-end-label--win");
            _gameEndLabel.RemoveFromClassList("game-end-label--lose");
            _gameEndLabel.RemoveFromClassList("game-end-label--draw");
            _gameEndLabel.AddToClassList(labelClass);
            _gameEndLabel.style.scale = new Scale(new Vector3(0.3f, 0.3f, 1f));

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
