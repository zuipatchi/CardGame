using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // ─── マリガン ────────────────────────────────────────────────────

        private async UniTask RunOpponentMulliganAnimationAsync(CardData placeholder, int handSize, CardData[] newDeck, CancellationToken ct)
        {
            await PlayReturnHandToDeckAsync(_opponentHandView, _opponentDeckView, ct);

            _opponentDeckView.Rebuild(newDeck);

            await UniTask.NextFrame(ct);

            Rect deckRect = _opponentDeckView.worldBound;
            UniTask[] drawTasks = new UniTask[handSize];
            for (int i = 0; i < handSize; i++)
            {
                drawTasks[i] = _opponentHandView.AddCardAnimatedAsync(placeholder, deckRect, i * DrawStagger, ct);
            }
            await UniTask.WhenAll(drawTasks);
        }

        private async UniTask<bool> RunPlayerMulliganAsync(
            CardData[] fullDeck, HandView hand, DeckView deck, int handSize, CancellationToken ct)
        {
            bool chose = await WaitForMulliganChoiceAsync(ct);
            if (!chose)
            {
                return false;
            }

            await PlayAnnouncementAsync("マリガン", "turn-announcement-label--mulligan", ct);
            await PlayReturnHandToDeckAsync(hand, deck, ct);

            CardData[] reshuffled = CardArrayUtils.Shuffle((CardData[])fullDeck.Clone());
            CardData[] newHandCards = reshuffled.Take(handSize).ToArray();
            CardData[] newDeckCards = reshuffled.Skip(handSize).ToArray();

            deck.Rebuild(newDeckCards);

            await UniTask.NextFrame(ct);

            Rect deckRect = deck.worldBound;
            UniTask[] drawTasks = new UniTask[handSize];
            for (int i = 0; i < handSize; i++)
            {
                drawTasks[i] = hand.AddCardAnimatedAsync(newHandCards[i], deckRect, i * DrawStagger, ct);
            }
            await UniTask.WhenAll(drawTasks);
            return true;
        }

        private async UniTask<bool> WaitForMulliganChoiceAsync(CancellationToken ct)
        {
            _mulliganChoicePending = true;

            VisualElement overlay = new VisualElement();
            overlay.AddToClassList("mulligan-overlay");
            // 背景部分のクリックは下の手札カードへ透過させ、確認中もカード詳細を開けるようにする
            // （Yes/No ボタンは子要素として通常どおりクリックを受け取る）
            overlay.pickingMode = PickingMode.Ignore;

            Label label = new Label("マリガンしますか？");
            label.AddToClassList("mulligan-label");
            label.pickingMode = PickingMode.Ignore;
            overlay.Add(label);

            VisualElement buttonRow = new VisualElement();
            buttonRow.AddToClassList("mulligan-button-row");

            Button yesButton = new Button();
            yesButton.AddToClassList("mulligan-button");
            yesButton.style.backgroundImage = new StyleBackground(_cardStore.YesButtonImage);

            Button noButton = new Button();
            noButton.AddToClassList("mulligan-button");
            noButton.style.backgroundImage = new StyleBackground(_cardStore.NoButtonImage);

            buttonRow.Add(yesButton);
            buttonRow.Add(noButton);
            overlay.Add(buttonRow);
            _mainRoot.Add(overlay);
            _mulliganOverlay = overlay;

            UniTaskCompletionSource<bool> tcs = new UniTaskCompletionSource<bool>();
            yesButton.clicked += () => tcs.TrySetResult(true);
            noButton.clicked += () => tcs.TrySetResult(false);
            ct.Register(() => tcs.TrySetCanceled());

            bool result;
            try
            {
                result = await tcs.Task;
            }
            catch (OperationCanceledException)
            {
                overlay.RemoveFromHierarchy();
                _mulliganOverlay = null;
                _mulliganChoicePending = false;
                return false;
            }

            overlay.RemoveFromHierarchy();
            _mulliganOverlay = null;
            _mulliganChoicePending = false;
            return result;
        }

        private async UniTask RunCpuMulliganIfNeededAsync(
            CardData[] fullDeck, HandView hand, DeckView deck, int handSize, CancellationToken ct)
        {
            IReadOnlyList<CardView> cards = hand.Cards;
            bool hasCharacter = false;
            foreach (CardView card in cards)
            {
                if (card.Data is CharacterCardData)
                {
                    hasCharacter = true;
                    break;
                }
            }

            if (hasCharacter)
            {
                return;
            }

            await PlayReturnHandToDeckAsync(hand, deck, ct);

            CardData[] reshuffled = CardArrayUtils.Shuffle((CardData[])fullDeck.Clone());
            CardData[] newHandCards = reshuffled.Take(handSize).ToArray();
            CardData[] newDeckCards = reshuffled.Skip(handSize).ToArray();

            deck.Rebuild(newDeckCards);

            await UniTask.NextFrame(ct);

            Rect deckRect = deck.worldBound;
            UniTask[] drawTasks = new UniTask[handSize];
            for (int i = 0; i < handSize; i++)
            {
                drawTasks[i] = hand.AddCardAnimatedAsync(newHandCards[i], deckRect, i * DrawStagger, ct);
            }
            await UniTask.WhenAll(drawTasks);
        }
    }
}
