using System;
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

                if (card.Data.Cost > 0 && card.Data.Cost > CostCapacityExcluding(card))
                {
                    ShowToast("コストが払えません");
                    return false;
                }

                _playerFieldView.PlaceCard(card);
                _switchInput._card = card;
                if (card.Data.Cost > 0)
                {
                    BeginStagedCostSelection(card);
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
                    if (_playerFieldView.IsCharactersFull)
                    {
                        ShowToast("フィールドがいっぱいです");
                        return false;
                    }
                    if (card.Data.Cost > 0 && card.Data.Cost > CostCapacityExcluding(card))
                    {
                        ShowToast("コストが払えません");
                        return false;
                    }
                    _playerFieldView.PlaceCard(card);
                    _mainStagedCard = card;
                    _mainStagedType = MainPhaseActionType.PlaceChar;
                    if (card.Data.Cost > 0)
                    {
                        BeginStagedCostSelection(card);
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
                    if (card.Data.Cost > 0 && card.Data.Cost > CostCapacityExcluding(card))
                    {
                        ShowToast("コストが払えません");
                        return false;
                    }
                    bool placed = _playerFieldView.TryPlace(card, worldPos);
                    if (placed)
                    {
                        _mainStagedCard = card;
                        _mainStagedType = MainPhaseActionType.PlayEvent;
                        if (card.Data.Cost > 0)
                        {
                            BeginStagedCostSelection(card);
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
                if (SelectedCostValue() < _requiredCost)
                {
                    return;
                }
                if (!IsCostAttributeSatisfied())
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

    }
}
