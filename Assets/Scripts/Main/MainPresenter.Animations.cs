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
            catch (OperationCanceledException) { }

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
                catch (OperationCanceledException) { }
            }

            if (ghostCoin.parent != null) { ghostCoin.RemoveFromHierarchy(); }
        }

        // ─── ターン・フェーズ告知アニメーション ────────────────────────────

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
            _turnLabel.RemoveFromClassList("turn-announcement-label--draw");
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

            defSlot.SetDefValue(def);
            defSlot.DefOverlay.BringToFront();
            defSlot.DefOverlay.style.display = DisplayStyle.Flex;
            defSlot.DefOverlay.style.opacity = 0f;

            float val = 0f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => atkOverlay.style.opacity.value, v => atkOverlay.style.opacity = v, 1f, 0.2f))
                .Join(DOTween.To(() => defSlot.DefOverlay.style.opacity.value, v => defSlot.DefOverlay.style.opacity = v, 1f, 0.2f))
                .Join(DOTween.To(() => val, v => { val = v; atkLabel.text = Mathf.RoundToInt(v).ToString(); }, atk, countDuration).SetEase(Ease.OutQuad))
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
            catch (OperationCanceledException) { }

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
            catch (OperationCanceledException) { }

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

        private async UniTask PlayDamageNumberFlyAsync(
            int damage, Vector2 fromWorldCenter, DeckView targetDeck, CancellationToken ct)
        {
            const float AppearDuration = 0.3f;
            const float HoldDuration = 0.3f;
            const float FlyDuration = 0.75f;
            const float ContainerSize = 220f;

            // ATKアイコン + ダメージ数字を重ねたコンテナ
            VisualElement container = new VisualElement();
            container.pickingMode = PickingMode.Ignore;
            container.style.position = Position.Absolute;
            container.style.width = ContainerSize;
            container.style.height = ContainerSize;
            container.style.opacity = 0f;
            container.style.scale = new Scale(new Vector3(0.5f, 0.5f, 1f));

            Vector2 fromLocal = _dragLayer.WorldToLocal(fromWorldCenter);
            float left = fromLocal.x - ContainerSize / 2f;
            float top = fromLocal.y - ContainerSize / 2f;
            container.style.left = left;
            container.style.top = top;

            VisualElement iconWrapper = new VisualElement();
            iconWrapper.pickingMode = PickingMode.Ignore;
            iconWrapper.AddToClassList("atk-counter-icon-wrapper");
            container.Add(iconWrapper);

            VisualElement icon = new VisualElement();
            icon.pickingMode = PickingMode.Ignore;
            icon.AddToClassList("damage-fly-icon");
            iconWrapper.Add(icon);

            // ダメージ数字をアイコンに重ねて表示
            Label label = new Label(damage.ToString());
            label.pickingMode = PickingMode.Ignore;
            label.AddToClassList("damage-number-label");
            label.style.position = Position.Absolute;
            label.style.left = 0;
            label.style.right = 0;
            label.style.top = new StyleLength(new Length(35f, LengthUnit.Percent));
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            container.Add(label);

            _dragLayer.Add(container);

            Vector2 deckLocal = _dragLayer.WorldToLocal(targetDeck.worldBound.center);
            float targetLeft = deckLocal.x - ContainerSize / 2f;
            float targetTop = deckLocal.y - ContainerSize / 2f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => container.style.opacity.value, v => container.style.opacity = v, 1f, AppearDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => container.style.scale.value.value.x, v => container.style.scale = new Scale(new Vector3(v, v, 1f)), 1f, AppearDuration).SetEase(Ease.OutBack))
                .AppendInterval(HoldDuration)
                .Append(DOTween.To(() => left, v => { left = v; container.style.left = v; }, targetLeft, FlyDuration).SetEase(Ease.InQuad))
                .Join(DOTween.To(() => top, v => { top = v; container.style.top = v; }, targetTop, FlyDuration).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }

            container.RemoveFromHierarchy();
        }

        // ─── CPU ドロー演出 ──────────────────────────────────────────────

        private async UniTask PlayCpuDrawAsync(CardData data, Rect deckRect, CancellationToken ct)
        {
            const float FlyDuration = 0.35f;

            CardView card = new CardView(_cardStore.CardTemplate, data, _cardStore.CardBack, faceDown: true, _cardStore.AttributeDatabase, isOpponent: true);
            card.style.position = Position.Absolute;
            card.style.left = deckRect.center.x - CardWidth / 2f;
            card.style.top = deckRect.center.y - CardHeight / 2f;
            card.style.scale = new Scale(new Vector3(CardScaleConstants.HandDeck, CardScaleConstants.HandDeck, 1f));
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
            tasks.Add(PlayFloatingLabelAsync("NEGATE!", "negate-label", targetCard, ct));
            if (_negateEffectPrefab != null)
            {
                tasks.Add(PlayParticleAtCardAsync(targetCard, _negateEffectPrefab, ct));
            }
            await UniTask.WhenAll(tasks);
        }

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
            await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: ct);
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

        // ─── Recover 飛翔アニメーション（墓地 → デッキへ同時飛翔）─────────────

        private async UniTask PlayRecoverFlyAsync(
            IReadOnlyList<CardData> cards, GraveyardView sourceGraveyard, DeckView targetDeck, CancellationToken ct)
        {
            const float FlyDuration = 0.35f;
            const float CardInterval = 0.05f;

            Rect fromRect = sourceGraveyard.worldBound;
            float startLeft = fromRect.center.x - CardWidth / 2f;
            float startTop = fromRect.center.y - CardHeight / 2f;
            Rect destRect = targetDeck.worldBound;
            float targetLeft = destRect.center.x - CardWidth / 2f;
            float targetTop = destRect.center.y - CardHeight / 2f;

            List<CardView> tempCards = new List<CardView>(cards.Count);
            List<UniTaskCompletionSource> tcsList = new List<UniTaskCompletionSource>(cards.Count);

            for (int i = 0; i < cards.Count; i++)
            {
                CardView tempCard = new CardView(_cardStore.CardTemplate, cards[i], _cardStore.CardBack, faceDown: false, _cardStore.AttributeDatabase);
                tempCard.style.position = Position.Absolute;
                tempCard.style.left = startLeft;
                tempCard.style.top = startTop;
                tempCard.style.width = StyleKeyword.Null;
                tempCard.style.height = StyleKeyword.Null;
                tempCard.style.scale = new Scale(Vector3.one);
                _dragLayer.Add(tempCard);
                tempCards.Add(tempCard);

                UniTaskCompletionSource tcs = new UniTaskCompletionSource();
                tcsList.Add(tcs);

                float delay = i * CardInterval;
                CardView captured = tempCard;
                Sequence seq = DOTween.Sequence()
                    .AppendInterval(delay)
                    .Append(DOTween.To(() => captured.style.left.value.value, v => captured.style.left = v, targetLeft, FlyDuration).SetEase(Ease.InQuad))
                    .Join(DOTween.To(() => captured.style.top.value.value, v => captured.style.top = v, targetTop, FlyDuration).SetEase(Ease.InQuad))
                    .Join(DOTween.To(
                        () => captured.style.scale.value.value.x,
                        s => captured.style.scale = new Scale(new Vector3(s, s, 1f)),
                        0f, FlyDuration).SetEase(Ease.InQuad))
                    .OnComplete(() => tcs.TrySetResult());

                ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });
            }

            try
            {
                await UniTask.WhenAll(tcsList.Select(t => t.Task));
            }
            catch (OperationCanceledException) { }

            foreach (CardView tempCard in tempCards)
            {
                if (tempCard.parent == _dragLayer)
                {
                    _dragLayer.Remove(tempCard);
                }
            }
        }

        // ─── デッキシャッフルパルス（デッキがスケールアップしてから戻る）────────

        private async UniTask PlayDeckShufflePulseAsync(DeckView deck, CancellationToken ct)
        {
            const float PulseDuration = 0.15f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Append(DOTween.To(
                    () => deck.style.scale.value.value.x,
                    s => deck.style.scale = new Scale(new Vector3(s, s, 1f)),
                    1.15f, PulseDuration).SetEase(Ease.OutQuad))
                .Append(DOTween.To(
                    () => deck.style.scale.value.value.x,
                    s => deck.style.scale = new Scale(new Vector3(s, s, 1f)),
                    1f, PulseDuration).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }

            deck.style.scale = new Scale(Vector3.one);
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

        private UniTask PlayParticleAtCardAsync(CardView card, GameObject prefab, CancellationToken ct)
        {
            if (card.panel == null)
            {
                return UniTask.CompletedTask;
            }
            return PlayParticleAtUiPositionAsync(card, card.worldBound.center, prefab, ct);
        }

        private async UniTask PlayParticleAtUiPositionAsync(VisualElement panelRef, Vector2 uiPos, GameObject prefab, CancellationToken ct)
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
            card.style.scale = new Scale(new Vector3(CardScaleConstants.HandDeck, CardScaleConstants.HandDeck, 1f));
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
