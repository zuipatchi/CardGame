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

            if (_gameModel.Phase == TurnPhase.CharacterSet && _charSetInputTcs != null)
            {
                if (card.Data is not CharacterCardData || _stagedCharSetCard != null)
                {
                    return false;
                }

                if (!_playerCharacterSlot.worldBound.Contains(worldPos))
                {
                    return false;
                }

                _playerCharacterSlot.PlaceCard(card);
                _stagedCharSetCard = card;
                card.FlipAsync(destroyCancellationToken).Forget();
                UpdateStagedButtons(_stagedCharSetCard != null);
                UpdateCostWarning(_stagedCharSetCard);
                return true;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle1 && _isLocalPreBattleActive)
            {
                if ((card.Data is not SkillCardData && card.Data is not CharacterCardData) || _stagedPreBattleCard != null)
                {
                    return false;
                }

                bool placed = _playerFieldView.TryPlace(card, worldPos);
                if (placed)
                {
                    _stagedPreBattleCard = card;
                    card.FlipAsync(destroyCancellationToken).Forget();
                    UpdateStagedButtons(_stagedPreBattleCard != null);
                    UpdateCostWarning(_stagedPreBattleCard);
                }
                return placed;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle2 && _gameModel.IsLocalPreparationTurn)
            {
                if (card.Data is not EventCardData || _stagedPrepCard != null)
                {
                    return false;
                }

                bool placed = _playerFieldView.TryPlace(card, worldPos);
                if (placed)
                {
                    _stagedPrepCard = card;
                    UpdateStagedButtons(true);
                    UpdateCostWarning(_stagedPrepCard);
                }
                return placed;
            }

            return false;
        }

        // ─── ボタンハンドラ ──────────────────────────────────────────────

        private async void OnOkClicked()
        {
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
            if (_gameModel.Phase == TurnPhase.CharacterSet && _charSetInputTcs != null && _stagedCharSetCard != null)
            {
                tcs = _charSetInputTcs;
                card = _stagedCharSetCard;
                _stagedCharSetCard = null;
                return true;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle1 && _preBattleInputTcs != null && _stagedPreBattleCard != null)
            {
                tcs = _preBattleInputTcs;
                card = _stagedPreBattleCard;
                _stagedPreBattleCard = null;
                return true;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle2 && _prepInputTcs != null && _stagedPrepCard != null)
            {
                tcs = _prepInputTcs;
                card = _stagedPrepCard;
                _stagedPrepCard = null;
                return true;
            }

            tcs = null;
            card = null;
            return false;
        }

        private void OnBackClicked()
        {
            if (_gameModel.Phase == TurnPhase.CharacterSet && _charSetInputTcs != null)
            {
                if (_stagedCharSetCard != null)
                {
                    CardView card = _stagedCharSetCard;
                    _stagedCharSetCard = null;
                    ReturnStagedCardToHand(card, card.worldBound, () => _playerCharacterSlot.RemoveCard(), flipCard: true);
                }
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle1 && _preBattleInputTcs != null)
            {
                if (_stagedPreBattleCard != null)
                {
                    CardView card = _stagedPreBattleCard;
                    _stagedPreBattleCard = null;
                    ReturnStagedCardToHand(card, card.worldBound, () => _playerFieldView.RemoveCard(card), flipCard: true);
                }
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle2 && _prepInputTcs != null)
            {
                if (_stagedPrepCard != null)
                {
                    CardView card = _stagedPrepCard;
                    _stagedPrepCard = null;
                    ReturnStagedCardToHand(card, card.worldBound, () => _playerFieldView.RemoveCard(card), flipCard: false);
                    return;
                }

                // ステージなし = パス
                HideActionButtons();
                _prepInputTcs.TrySetResult(null);
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
            if (_gameModel.Phase == TurnPhase.CharacterSet && _charSetInputTcs != null && _stagedCharSetCard == null)
            {
                _charSetInputTcs.TrySetResult(null);
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle1 && _preBattleInputTcs != null && _stagedPreBattleCard == null)
            {
                _preBattleInputTcs.TrySetResult(null);
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle2 && _prepInputTcs != null && _stagedPrepCard == null)
            {
                HideActionButtons();
                _prepInputTcs.TrySetResult(null);
            }
        }

        // ─── UI ヘルパー ─────────────────────────────────────────────────

        private void UpdateStagedButtons(bool hasStaged)
        {
            _passButton.style.display = hasStaged ? DisplayStyle.None : DisplayStyle.Flex;
            _backButton.style.display = hasStaged ? DisplayStyle.Flex : DisplayStyle.None;
            _okButton.style.display = hasStaged ? DisplayStyle.Flex : DisplayStyle.None;
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

        private void UpdateCostWarning(CardView card)
        {
            bool show = card != null && card.Data.Cost > 0 && card.Data.Cost >= _playerDeckView.Count;
            _costWarningLabel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
