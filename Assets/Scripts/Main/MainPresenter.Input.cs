using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using Main.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // ─── ドロップハンドラ ────────────────────────────────────────────

        private bool HandlePlayerCardDrop(CardView card, Vector2 worldPos)
        {
            if (_isGameOver)
            {
                return false;
            }

            if (_evolveInput._tcs != null)
            {
                if (!_playerFieldView.worldBound.Contains(worldPos))
                {
                    return false;
                }

                if (card.Data is not CharacterCardData)
                {
                    ShowToast("キャラカードのみ選択できます");
                    return false;
                }

                if (card.Data.Cost <= _evolveMinCost)
                {
                    ShowToast($"コスト{_evolveMinCost + 1}以上のカードが必要です");
                    return false;
                }

                if (_evolveInput._card != null)
                {
                    return false;
                }

                _playerFieldView.PlaceCard(card);
                _evolveInput._card = card;
                UpdateStagedButtons(true);
                if (_optionModel.AutoOk.CurrentValue) { AutoOkAsync().Forget(); }
                return true;
            }

            if (_switchInput._tcs != null)
            {
                if (!_playerFieldView.worldBound.Contains(worldPos))
                {
                    return false;
                }

                if (card.Data is not CharacterCardData)
                {
                    ShowToast("キャラカードのみセットできます");
                    return false;
                }

                if (_switchInput._card != null)
                {
                    return false;
                }

                _playerFieldView.PlaceCard(card);
                _switchInput._card = card;
                if (card.Data.Cost > 0)
                {
                    BeginStagedCostSelection(card, _handView.Cards.Count - 1);
                }
                else
                {
                    UpdateStagedButtons(true);
                    if (_optionModel.AutoOk.CurrentValue) { AutoOkAsync().Forget(); }
                }
                return true;
            }

            if (_gameModel.Phase == TurnPhase.Main && _gameModel.IsLocalTurn
                && _mainActionTcs != null && _mainStagedCard == null)
            {
                if (!_playerFieldView.worldBound.Contains(worldPos))
                {
                    return false;
                }

                if (card.Data is CharacterCardData)
                {
                    _playerFieldView.PlaceCard(card);
                    _mainStagedCard = card;
                    _mainStagedType = MainPhaseActionType.PlaceChar;
                    if (card.Data.Cost > 0)
                    {
                        BeginStagedCostSelection(card, _handView.Cards.Count - 1);
                    }
                    else
                    {
                        UpdateStagedButtons(true);
                        if (_optionModel.AutoOk.CurrentValue) { AutoOkAsync().Forget(); }
                    }
                    return true;
                }

                if (card.Data is EventCardData)
                {
                    bool placed = _playerFieldView.TryPlace(card, worldPos);
                    if (placed)
                    {
                        _mainStagedCard = card;
                        _mainStagedType = MainPhaseActionType.PlayEvent;
                        if (card.Data.Cost > 0)
                        {
                            BeginStagedCostSelection(card, _handView.Cards.Count - 1);
                        }
                        else
                        {
                            UpdateStagedButtons(true);
                            if (_optionModel.AutoOk.CurrentValue) { AutoOkAsync().Forget(); }
                        }
                    }
                    return placed;
                }

                return false;
            }

            return false;
        }

        // ─── コスト選択待ち ──────────────────────────────────────────────

        private async UniTask<List<CardView>> WaitForPlayerCostSelectionAsync(int cost, CancellationToken ct)
        {
            // BeginStagedCostSelection がドロップ時に先行して呼ばれた場合は TCS が既に存在する
            if (_costSelectionTcs == null)
            {
                int required = Mathf.Min(cost, _handView.Cards.Count);
                if (required == 0)
                {
                    return new List<CardView>();
                }

                _requiredCostCount = required;
                _costSelectionTcs = new UniTaskCompletionSource();
                _selectedCostCards.Clear();

                _costWarningLabel.text = _requiredCostCount < cost
                    ? $"手札が足りません（{_requiredCostCount}/{cost}枚）"
                    : $"コストを {_requiredCostCount} 枚選んでください";
                _costWarningLabel.style.display = DisplayStyle.Flex;

                foreach (CardView c in _handView.Cards)
                {
                    c.AddToClassList("cost-selectable");
                }
                ShowCostSelectionButtons();
            }

            try
            {
                await _costSelectionTcs.Task.AttachExternalCancellation(ct);
            }
            catch (OperationCanceledException) { }

            foreach (CardView c in _handView.Cards)
            {
                c.RemoveFromClassList("cost-selectable");
                c.RemoveFromClassList("cost-selected");
            }
            foreach (CardView c in _selectedCostCards)
            {
                c.RemoveFromClassList("cost-selectable");
                c.RemoveFromClassList("cost-selected");
            }

            _costWarningLabel.text = "手札が足りません";
            _okButton.SetEnabled(true);
            HideActionButtons();

            List<CardView> result = new List<CardView>(_selectedCostCards);
            _selectedCostCards.Clear();
            _costSelectionTcs = null;
            _requiredCostCount = 0;

            return result;
        }

        private void HandleCostCardClick(CardView card)
        {
            if (_costSelectionTcs == null)
            {
                return;
            }

            if (_selectedCostCards.Contains(card))
            {
                _selectedCostCards.Remove(card);
                card.RemoveFromClassList("cost-selected");
            }
            else if (_selectedCostCards.Count < _requiredCostCount)
            {
                _selectedCostCards.Add(card);
                card.AddToClassList("cost-selected");
            }

            bool enough = _selectedCostCards.Count >= _requiredCostCount;
            _okButton.SetEnabled(enough);

            if (enough && _optionModel.AutoOk.CurrentValue)
            {
                AutoOkAsync().Forget();
            }
        }

        private void ShowCostSelectionButtons()
        {
            _passButton.style.display = DisplayStyle.None;
            _backButton.style.display = DisplayStyle.None;
            _okButton.style.display = DisplayStyle.Flex;
            _okButton.SetEnabled(false);
            _actionButtonsArea.AddToClassList("main-action-buttons-area--visible");
        }

        // ─── ボタンハンドラ ──────────────────────────────────────────────

        private async UniTaskVoid AutoOkAsync()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: destroyCancellationToken);
            OnOkClicked();
        }

        private async void OnOkClicked()
        {
            if (_costSelectionTcs != null)
            {
                if (_selectedCostCards.Count < _requiredCostCount)
                {
                    return;
                }
                // コスト TCS を解決し、このままステージ済みアクションの確定へ fall-through する
                _costSelectionTcs.TrySetResult();
            }

            // メインフェーズのカード確定
            if (_gameModel.Phase == TurnPhase.Main && _mainActionTcs != null && _mainStagedCard != null)
            {
                MainPhaseAction action = new MainPhaseAction
                {
                    _actionType = _mainStagedType,
                    _card = _mainStagedCard
                };
                _mainStagedCard = null;
                _mainStagedType = MainPhaseActionType.None;
                HideActionButtons();
                _mainActionTcs?.TrySetResult(action);
                return;
            }

            if (!TryTakeStagedInput(out UniTaskCompletionSource<CardView> tcs, out CardView card))
            {
                return;
            }

            HideActionButtons();
            await PlayOkFlashAsync(true, destroyCancellationToken);
            tcs.TrySetResult(card);
        }

        private bool TryTakeStagedInput(out UniTaskCompletionSource<CardView> tcs, out CardView card)
        {
            if (_evolveInput._tcs != null && _evolveInput._card != null)
            {
                tcs = _evolveInput._tcs;
                card = _evolveInput._card;
                _evolveInput._card = null;
                _evolveInput._tcs = null;
                return true;
            }

            if (_switchInput._tcs != null && _switchInput._card != null)
            {
                tcs = _switchInput._tcs;
                card = _switchInput._card;
                _switchInput._card = null;
                _switchInput._tcs = null;
                return true;
            }

            tcs = null;
            card = null;
            return false;
        }

        private void OnBackClicked()
        {
            if (_evolveInput._tcs != null)
            {
                if (_evolveInput._card != null)
                {
                    CardView card = _evolveInput._card;
                    _evolveInput._card = null;
                    ReturnStagedCardToHand(card, card.worldBound, () => _playerFieldView.RemoveCard(card), flipCard: false);
                }
                return;
            }

            if (_switchInput._tcs != null)
            {
                if (_switchInput._card != null)
                {
                    CardView card = _switchInput._card;
                    _switchInput._card = null;
                    CancelStagedCostSelection();
                    ReturnStagedCardToHand(card, card.worldBound, () => _playerFieldView.RemoveCard(card), flipCard: false);
                }
                return;
            }

            if (_gameModel.Phase == TurnPhase.Main && _mainActionTcs != null && _mainStagedCard != null)
            {
                CardView card = _mainStagedCard;
                _mainStagedCard = null;
                _mainStagedType = MainPhaseActionType.None;
                CancelStagedCostSelection();
                ReturnStagedCardToHand(card, card.worldBound, () => _playerFieldView.RemoveCard(card), flipCard: false);
            }
        }

        private void ReturnStagedCardToHand(CardView card, Rect fromRect, Action removeFromPlace, bool flipCard)
        {
            removeFromPlace();
            if (flipCard)
            {
                card.FlipAsync(destroyCancellationToken).Forget();
            }
            _handView.AddCardBackAsync(card, fromRect, destroyCancellationToken).Forget();
            UpdateStagedButtons(false);
        }

        private void OnPassClicked()
        {
            if (_evolveInput._tcs != null && _evolveInput._card == null)
            {
                HideActionButtons();
                _evolveInput._tcs.TrySetResult(null);
                return;
            }

            if (_switchInput._tcs != null && _switchInput._card == null)
            {
                HideActionButtons();
                _switchInput._tcs.TrySetResult(null);
                return;
            }

            if (_gameModel.Phase == TurnPhase.Main && _mainActionTcs != null && _mainStagedCard == null)
            {
                HideActionButtons();
                _mainActionTcs.TrySetResult(new MainPhaseAction { _actionType = MainPhaseActionType.Pass });
            }
        }

        // ─── ドラッグ可否判定 ────────────────────────────────────────────

        private bool CanPlayerDragCard()
        {
            if (_isGameOver)
            {
                return false;
            }

            if (_mulliganChoicePending)
            {
                return false;
            }

            if (_evolveInput._tcs != null)
            {
                return _evolveInput._card == null;
            }

            if (_switchInput._tcs != null)
            {
                return _switchInput._card == null;
            }

            if (_gameModel.Phase == TurnPhase.Main && _gameModel.IsLocalTurn && _mainActionTcs != null)
            {
                return _mainStagedCard == null;
            }

            return false;
        }

        // ─── ハイライト ─────────────────────────────────────────────────

        private bool IsCardPlayable(CardView card)
        {
            if (_isGameOver)
            {
                return false;
            }

            if (_evolveInput._tcs != null && _evolveInput._card == null)
            {
                return card.Data is CharacterCardData && card.Data.Cost > _evolveMinCost;
            }

            if (_switchInput._tcs != null && _switchInput._card == null)
            {
                return card.Data is CharacterCardData;
            }

            if (_gameModel.Phase == TurnPhase.Main && _gameModel.IsLocalTurn
                && _mainActionTcs != null && _mainStagedCard == null)
            {
                return card.Data is CharacterCardData || card.Data is EventCardData;
            }

            return false;
        }

        private void RefreshHandHighlights()
        {
            foreach (CardView card in _handView.Cards)
            {
                card.SetPlayableHighlight(IsCardPlayable(card));
            }
        }

        private bool HasPlayableCards()
        {
            foreach (CardView card in _handView.Cards)
            {
                if (IsCardPlayable(card))
                {
                    return true;
                }
            }
            return false;
        }

        // ─── UI ヘルパー ─────────────────────────────────────────────────

        private void UpdateStagedButtons(bool hasStaged)
        {
            RefreshHandHighlights();
            bool autoOk = _optionModel.AutoOk.CurrentValue;
            _passButton.style.display = hasStaged ? DisplayStyle.None : DisplayStyle.Flex;
            _backButton.style.display = (hasStaged && !autoOk) ? DisplayStyle.Flex : DisplayStyle.None;
            _okButton.style.display = (hasStaged && !autoOk) ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasStaged)
            {
                _costWarningLabel.style.display = DisplayStyle.None;
            }
        }

        private void ShowActionButtons()
        {
            _actionButtonsArea.AddToClassList("main-action-buttons-area--visible");
        }

        private void HideActionButtons()
        {
            _actionButtonsArea.RemoveFromClassList("main-action-buttons-area--visible");
            _costWarningLabel.style.display = DisplayStyle.None;
        }

        private void BeginStagedCostSelection(CardView staged, int availableForCost)
        {
            int cost = staged.Data.Cost;
            int required = Mathf.Min(cost, availableForCost);
            _requiredCostCount = required;
            _costSelectionTcs = new UniTaskCompletionSource();
            _selectedCostCards.Clear();

            _costWarningLabel.text = required < cost
                ? $"手札が足りません（{required}/{cost}枚）"
                : $"コストを {required} 枚選んでください";
            _costWarningLabel.style.display = DisplayStyle.Flex;

            foreach (CardView c in _handView.Cards)
            {
                if (c != staged)
                {
                    c.AddToClassList("cost-selectable");
                }
            }

            bool autoOk = _optionModel.AutoOk.CurrentValue;
            _passButton.style.display = DisplayStyle.None;
            _backButton.style.display = DisplayStyle.Flex;
            _okButton.style.display = autoOk ? DisplayStyle.None : DisplayStyle.Flex;
            _okButton.SetEnabled(required == 0);
            _actionButtonsArea.AddToClassList("main-action-buttons-area--visible");

            if (autoOk && required == 0)
            {
                AutoOkAsync().Forget();
            }
        }

        private void CancelStagedCostSelection()
        {
            if (_costSelectionTcs == null)
            {
                return;
            }

            foreach (CardView c in _handView.Cards)
            {
                c.RemoveFromClassList("cost-selectable");
                c.RemoveFromClassList("cost-selected");
            }
            foreach (CardView c in _selectedCostCards)
            {
                c.RemoveFromClassList("cost-selectable");
                c.RemoveFromClassList("cost-selected");
            }
            _selectedCostCards.Clear();
            _costSelectionTcs = null;
            _requiredCostCount = 0;
        }
    }
}
