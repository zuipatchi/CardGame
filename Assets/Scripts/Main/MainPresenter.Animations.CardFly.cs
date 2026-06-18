using System;
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
        // ─── CPU ドロー演出 ──────────────────────────────────────────────

        private async UniTask PlayCpuDrawAsync(CardData data, Rect deckRect, CancellationToken ct)
        {
            const float FlyDuration = 0.35f;

            CardView card = new CardView(_cardStore.CardTemplate, data, _cardStore.CardBack, faceDown: true, isOpponent: true);
            card.style.position = Position.Absolute;
            card.style.left = deckRect.center.x - CardScaleConstants.CardWidth / 2f;
            card.style.top = deckRect.center.y - CardScaleConstants.CardHeight / 2f;
            card.style.scale = new Scale(new Vector3(CardScaleConstants.HandDeck, CardScaleConstants.HandDeck, 1f));
            card.pickingMode = PickingMode.Ignore;
            _dragLayer.Add(card);

            Rect handRect = _opponentHandView.worldBound;
            float targetLeft = handRect.center.x - CardScaleConstants.CardWidth / 2f;
            float targetTop = handRect.yMax - CardScaleConstants.CardHeight / 2f;

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
            card.pickingMode = PickingMode.Position;
            _opponentHandView.AcceptCard(card);
        }

        // ─── 配牌・マリガン時の1枚配り ──────────────────────────────────────
        // カードが飛び立つ瞬間にデッキを1枚減らして枚数バッジを更新する。
        // デッキは配る手札分を上に積んだ状態で構築されているため、DrawTop で配った分だけ正しく減り、
        // 残りは元のデッキ（手札を除いた並び）と一致する。
        private async UniTask DealCardFromDeckAsync(
            HandView hand, DeckView deck, CardData data, Rect deckRect, float startDelay, CancellationToken ct)
        {
            if (startDelay > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(startDelay), cancellationToken: ct);
            }
            deck.DrawTop();
            deck.RefreshCount();
            await hand.AddCardAnimatedAsync(data, deckRect, 0f, ct);
        }

        // ─── 手札上限バーン（満杯時に手札へ入るはずだったカードを墓地へ送る）──────

        // 既存の CardView を fromRect から墓地へ飛ばし、表向きにして墓地へ追加する。
        private async UniTask BurnCardToGraveyardAsync(CardView card, Rect fromRect, GraveyardView graveyard, CancellationToken ct)
        {
            const float FlyDuration = 0.3f;

            card.style.position = Position.Absolute;
            card.style.left = fromRect.center.x - CardScaleConstants.CardWidth / 2f;
            card.style.top = fromRect.center.y - CardScaleConstants.CardHeight / 2f;
            card.style.bottom = StyleKeyword.Null;
            card.style.width = StyleKeyword.Null;
            card.style.height = StyleKeyword.Null;
            card.style.rotate = new Rotate(0);
            card.style.scale = new Scale(new Vector3(CardScaleConstants.HandDeck, CardScaleConstants.HandDeck, 1f));
            card.style.transformOrigin = StyleKeyword.Null;
            card.style.marginLeft = StyleKeyword.Null;
            card.style.marginRight = StyleKeyword.Null;
            card.pickingMode = PickingMode.Ignore;
            _dragLayer.Add(card);

            // 墓地は公開情報のため表向きにする
            if (card.IsFaceDown)
            {
                await card.FlipAsync(ct);
            }

            Rect toRect = graveyard.worldBound;
            float targetLeft = toRect.center.x - CardScaleConstants.CardWidth / 2f;
            float targetTop = toRect.center.y - CardScaleConstants.CardHeight / 2f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, targetLeft, FlyDuration).SetEase(Ease.InQuad))
                .Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, targetTop, FlyDuration).SetEase(Ease.InQuad))
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
            graveyard.AddCard(card);
        }

        // ドロー由来（手札に入るはずだったカードデータ）を CardView 化してバーンする。
        private UniTask BurnDrawnCardAsync(CardData data, Rect fromRect, GraveyardView graveyard, bool isOpponent, CancellationToken ct)
        {
            CardView card = new CardView(_cardStore.CardTemplate, data, _cardStore.CardBack, faceDown: false, isOpponent);
            return BurnCardToGraveyardAsync(card, fromRect, graveyard, ct);
        }

        // キャラ等を所有者の手札へ戻す。手札が満杯なら墓地へ送る（バーン）。
        // toOpponentHand=true のときは相手の手札（裏向き表示）へ戻すため、戻す前に裏返す。
        private async UniTask ReturnCardToHandOrBurnAsync(CardView card, HandView hand, GraveyardView graveyard, Rect fromRect, bool toOpponentHand, CancellationToken ct)
        {
            // 手札に戻る前に場で受けた変化（バフ・ダメージ・付与キーワード）を初期状態へ戻す。
            card.ResetRuntimeState();

            if (hand.IsFull)
            {
                await BurnCardToGraveyardAsync(card, fromRect, graveyard, ct);
                return;
            }

            if (toOpponentHand && !card.IsFaceDown)
            {
                await card.FlipAsync(ct);
            }
            await hand.AddCardBackAsync(card, fromRect, ct);
        }

        // ─── カード移動ヘルパー ──────────────────────────────────────────

        private async UniTask FlyCardToDestAsync(CardView card, Rect fromWorldRect, VisualElement dest, CancellationToken ct, float delay = 0f, float duration = CpuCardFlyDuration)
        {
            card.style.position = Position.Absolute;
            card.style.left = fromWorldRect.center.x - CardScaleConstants.CardWidth / 2f;
            card.style.top = fromWorldRect.center.y - CardScaleConstants.CardHeight / 2f;
            card.style.width = StyleKeyword.Null;
            card.style.height = StyleKeyword.Null;
            card.style.rotate = new Rotate(0);
            card.style.scale = new Scale(Vector3.one);
            card.style.transformOrigin = StyleKeyword.Null;
            card.style.marginLeft = StyleKeyword.Null;
            card.style.marginRight = StyleKeyword.Null;
            card.pickingMode = PickingMode.Ignore;
            _dragLayer.Add(card);

            Rect destRect = dest.worldBound;
            float targetLeft = destRect.center.x - CardScaleConstants.CardWidth / 2f;
            float targetTop = destRect.center.y - CardScaleConstants.CardHeight / 2f;

            if (delay > 0f)
            {
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
                }
                catch (OperationCanceledException)
                {
                    _dragLayer.Remove(card);
                    card.pickingMode = PickingMode.Position;
                    return;
                }
            }

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, targetLeft, duration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, targetTop, duration).SetEase(Ease.OutQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try
            {
                await tcs.Task;
            }
            catch (OperationCanceledException) { }

            _dragLayer.Remove(card);
            card.pickingMode = PickingMode.Position;
        }

        // ─── 墓地の永続イベント（OnTurnStart）発動演出 ─────────────────────────
        // 墓地のイベントカードデータから一時カードを生成し、墓地 → フィールド中央へせり出させて
        // 効果を発動し、墓地へ戻す。墓地のデータ自体は減らさない（毎ターン発動し続ける）。
        private async UniTask PlayGraveyardEventEffectAsync(EventCardData data, bool isLocal, CancellationToken ct)
        {
            GraveyardView graveyard = isLocal ? _playerGraveyardView : _opponentGraveyardView;
            FieldView field = isLocal ? _playerFieldView : _opponentFieldView;

            Rect graveRect = graveyard.worldBound;
            Rect fieldRect = field.worldBound;

            float graveLeft = graveRect.center.x - CardScaleConstants.CardWidth / 2f;
            float graveTop = graveRect.center.y - CardScaleConstants.CardHeight / 2f;
            float displayLeft = fieldRect.center.x - CardScaleConstants.CardWidth / 2f;
            float displayTop = fieldRect.center.y - CardScaleConstants.CardHeight / 2f;

            CardView temp = new CardView(_cardStore.CardTemplate, data, _cardStore.CardBack, faceDown: false, isOpponent: !isLocal);
            temp.style.position = Position.Absolute;
            temp.style.left = graveLeft;
            temp.style.top = graveTop;
            temp.style.scale = new Scale(Vector3.zero);
            temp.pickingMode = PickingMode.Ignore;
            _dragLayer.Add(temp);

            // 墓地 → フィールド中央へせり出す
            await TweenCardAbsoluteAsync(temp, displayLeft, displayTop, 1f, 0.3f, Ease.OutBack, ct);

            // 効果発動（temp.worldBound が有効な状態で、種別ごとの演出込みで解決）
            await ResolveEventCardEffectAsync(data, temp, isLocal, ct);

            // フィールド → 墓地へ戻る（データは墓地に残ったまま）
            if (temp.parent == _dragLayer)
            {
                await TweenCardAbsoluteAsync(temp, graveLeft, graveTop, 0f, 0.25f, Ease.InQuad, ct);
                if (temp.parent == _dragLayer)
                {
                    _dragLayer.Remove(temp);
                }
            }
        }

        // ─── ダメージトリガー（デッキダメージ） ─────────────────────────────────────
        // デッキ攻撃のミル・将来のデッキミル効果でデッキ→墓地に送られた「ダメージトリガー」カードを、
        // デッキの持ち主（ownerIsLocal）がコストなしで使用する。
        //   キャラ：持ち主の場へ召喚（登場時効果 OnEnter も発動）し、場に残る。
        //   イベント：デッキ → 場中央へせり出して効果を解決し、墓地へ送る。
        // fromRect は演出の起点（ミル元のデッキ位置）。
        // カードデータ・盤面は同期済みのため両クライアントで決定的に発動する（追加同期不要）。
        private async UniTask ResolveGraveTriggerFromDeckAsync(CardData data, bool ownerIsLocal, Rect fromRect, CancellationToken ct)
        {
            if (data is CharacterCardData)
            {
                // 持ち主の場へコストなしで召喚（OnCardPlayed によるキャラ8体勝利判定・OnEnter 効果込み）
                FieldView field = ownerIsLocal ? _playerFieldView : _opponentFieldView;
                await SummonSingleCharAsync(data, field, ownerIsLocal, ct);
                return;
            }

            if (data is EventCardData eventData)
            {
                // デッキ → 場中央へせり出して効果を解決し、解決後に墓地へ送る
                GraveyardView graveyard = ownerIsLocal ? _playerGraveyardView : _opponentGraveyardView;
                FieldView field = ownerIsLocal ? _playerFieldView : _opponentFieldView;
                Rect fieldRect = field.worldBound;

                float fromLeft = fromRect.center.x - CardScaleConstants.CardWidth / 2f;
                float fromTop = fromRect.center.y - CardScaleConstants.CardHeight / 2f;
                float displayLeft = fieldRect.center.x - CardScaleConstants.CardWidth / 2f;
                float displayTop = fieldRect.center.y - CardScaleConstants.CardHeight / 2f;

                CardView temp = new CardView(_cardStore.CardTemplate, eventData, _cardStore.CardBack, faceDown: false, isOpponent: !ownerIsLocal);
                temp.style.position = Position.Absolute;
                temp.style.left = fromLeft;
                temp.style.top = fromTop;
                temp.style.scale = new Scale(Vector3.zero);
                temp.pickingMode = PickingMode.Ignore;
                _dragLayer.Add(temp);

                // デッキ → 場中央へせり出す
                await TweenCardAbsoluteAsync(temp, displayLeft, displayTop, 1f, 0.3f, Ease.OutBack, ct);

                // 効果発動（temp.worldBound が有効な状態で、種別ごとの演出込みで解決）
                await ResolveEventCardEffectAsync(eventData, temp, ownerIsLocal, ct);

                // 場 → 墓地へ送る（AddCard が _dragLayer から取り除いて墓地データに加える）
                if (temp.parent == _dragLayer)
                {
                    Rect graveRect = graveyard.worldBound;
                    float graveLeft = graveRect.center.x - CardScaleConstants.CardWidth / 2f;
                    float graveTop = graveRect.center.y - CardScaleConstants.CardHeight / 2f;
                    await TweenCardAbsoluteAsync(temp, graveLeft, graveTop, 0f, 0.25f, Ease.InQuad, ct);
                    graveyard.AddCard(temp);
                }
            }
        }

        // _dragLayer 上の絶対配置カードの left / top / scale を同時にトゥイーンする（カードは layer に残す）
        private async UniTask TweenCardAbsoluteAsync(CardView card, float targetLeft, float targetTop, float targetScale, float duration, Ease ease, CancellationToken ct)
        {
            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, targetLeft, duration).SetEase(ease))
                .Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, targetTop, duration).SetEase(ease))
                .Join(DOTween.To(
                    () => card.style.scale.value.value.x,
                    s => card.style.scale = new Scale(new Vector3(s, s, 1f)),
                    targetScale, duration).SetEase(ease))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try
            {
                await tcs.Task;
            }
            catch (OperationCanceledException) { }
        }

        // ─── Recover 飛翔アニメーション（墓地 → デッキへ同時飛翔）─────────────

        private async UniTask PlayRecoverFlyAsync(
            IReadOnlyList<CardData> cards, GraveyardView sourceGraveyard, DeckView targetDeck, CancellationToken ct)
        {
            const float FlyDuration = 0.35f;
            const float CardInterval = 0.05f;

            Rect fromRect = sourceGraveyard.worldBound;
            float startLeft = fromRect.center.x - CardScaleConstants.CardWidth / 2f;
            float startTop = fromRect.center.y - CardScaleConstants.CardHeight / 2f;
            Rect destRect = targetDeck.worldBound;
            float targetLeft = destRect.center.x - CardScaleConstants.CardWidth / 2f;
            float targetTop = destRect.center.y - CardScaleConstants.CardHeight / 2f;

            List<CardView> tempCards = new List<CardView>(cards.Count);
            List<UniTaskCompletionSource> tcsList = new List<UniTaskCompletionSource>(cards.Count);

            for (int i = 0; i < cards.Count; i++)
            {
                CardView tempCard = new CardView(_cardStore.CardTemplate, cards[i], _cardStore.CardBack, faceDown: false);
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

        // ─── 召喚キャラの登場演出（その場で出現）────────────────────────────
        // 飛来させず、配置済みのカードを「フラッシュ＋スケールポップ」でその場に出現させる。
        // 既存パーティクル（_evolveEffectPrefab）も同時に並行再生してテンポを保つ。
        private async UniTask PlaySummonAppearAsync(CardView card, CancellationToken ct)
        {
            // 着地スケール（フィールド枚数に応じた基準スケール）。PlaceCard 直後に適用済みの値を捕捉。
            float targetScale = card.style.scale.value.value.x;
            if (targetScale <= 0f)
            {
                targetScale = CardScaleConstants.FieldSlot;
            }

            // ポップ前に 0 スケールへ。PlaceCard と同フレームで隠すことで等倍の一瞬表示を防ぐ。
            card.style.scale = new Scale(Vector3.zero);

            // worldBound（パーティクル位置）の確定を待つ。
            await UniTask.NextFrame(ct);
            if (card.panel == null)
            {
                card.style.scale = new Scale(new Vector3(targetScale, targetScale, 1f));
                return;
            }

            // 白フラッシュ用オーバーレイ（カードの子。カードのスケール／位置に追従する）。
            VisualElement flash = new VisualElement();
            flash.pickingMode = PickingMode.Ignore;
            flash.style.position = Position.Absolute;
            flash.style.left = 0f;
            flash.style.right = 0f;
            flash.style.top = 0f;
            flash.style.bottom = 0f;
            flash.style.backgroundColor = Color.white;
            flash.style.opacity = 0f;
            flash.style.borderTopLeftRadius = 12f;
            flash.style.borderTopRightRadius = 12f;
            flash.style.borderBottomLeftRadius = 12f;
            flash.style.borderBottomRightRadius = 12f;
            card.Add(flash);

            // 既存パーティクル（魔法陣的な光）を並行再生（await はシーケンス後にまとめて行う）。
            UniTask particleTask = _evolveEffectPrefab != null
                ? PlayParticleAtCardAsync(card, _evolveEffectPrefab, ct, Quaternion.Euler(90f, 0f, 0f))
                : UniTask.CompletedTask;

            // スケールポップ（OutBack でオーバーシュート → 着地スケールへ）＋フラッシュの明滅。
            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            float popScale = 0f;
            Sequence seq = DOTween.Sequence()
                .Append(DOTween.To(() => popScale, v =>
                {
                    popScale = v;
                    card.style.scale = new Scale(new Vector3(v, v, 1f));
                }, targetScale, 0.28f).SetEase(Ease.OutBack))
                .Join(DOTween.To(() => flash.style.opacity.value, v => flash.style.opacity = v, 0.85f, 0.08f).SetEase(Ease.OutQuad))
                .Insert(0.08f, DOTween.To(() => flash.style.opacity.value, v => flash.style.opacity = v, 0f, 0.2f).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }

            // 着地スケールへ確定（OutBack のオーバーシュート誤差を消す）し、フラッシュを撤去。
            card.style.scale = new Scale(new Vector3(targetScale, targetScale, 1f));
            flash.RemoveFromHierarchy();

            await particleTask;
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
            float targetLeft = deckRect.center.x - CardScaleConstants.CardWidth / 2f;
            float targetTop = deckRect.center.y - CardScaleConstants.CardHeight / 2f;

            List<(CardView card, Rect fromRect)> entries = new List<(CardView, Rect)>();
            foreach (CardView c in snapshot)
            {
                entries.Add((c, c.worldBound));
            }

            foreach ((CardView c, Rect _) in entries)
            {
                hand.RemoveCard(c);
            }

            List<UniTask> tasks = new List<UniTask>();
            for (int i = 0; i < entries.Count; i++)
            {
                (CardView c, Rect fromRect) = entries[i];
                tasks.Add(FlyCardToDeckPositionAsync(c, fromRect, targetLeft, targetTop, i * Stagger, FlyDuration, ct));
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
            card.style.left = fromRect.center.x - CardScaleConstants.CardWidth / 2f;
            card.style.top = fromRect.center.y - CardScaleConstants.CardHeight / 2f;
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
    }
}
