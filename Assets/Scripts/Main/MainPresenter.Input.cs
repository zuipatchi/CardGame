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

        private void OnOkClicked()
        {
            if (_gameModel.Phase == TurnPhase.CharacterSet && _charSetInputTcs != null)
            {
                if (_stagedCharSetCard == null)
                {
                    return;
                }

                CardView card = _stagedCharSetCard;
                _stagedCharSetCard = null;
                _charSetInputTcs.TrySetResult(card);
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle1 && _preBattleInputTcs != null)
            {
                if (_stagedPreBattleCard == null)
                {
                    return;
                }

                CardView card = _stagedPreBattleCard;
                _stagedPreBattleCard = null;
                _preBattleInputTcs.TrySetResult(card);
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle2 && _prepInputTcs != null)
            {
                if (_stagedPrepCard == null)
                {
                    return;
                }

                CardView card = _stagedPrepCard;
                _stagedPrepCard = null;
                HideActionButtons();
                _prepInputTcs.TrySetResult(card);
                return;
            }
        }

        private void OnBackClicked()
        {
            if (_gameModel.Phase == TurnPhase.CharacterSet && _charSetInputTcs != null)
            {
                if (_stagedCharSetCard != null)
                {
                    Rect rect = _stagedCharSetCard.worldBound;
                    _playerCharacterSlot.RemoveCard();
                    CardView charCard = _stagedCharSetCard;
                    _stagedCharSetCard = null;
                    charCard.FlipAsync(destroyCancellationToken).Forget();
                    _handView.AddCardBackAsync(charCard, rect, destroyCancellationToken).Forget();
                    UpdateStagedButtons(_stagedCharSetCard != null);
                }
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle1 && _preBattleInputTcs != null)
            {
                if (_stagedPreBattleCard != null)
                {
                    Rect rect = _stagedPreBattleCard.worldBound;
                    _playerFieldView.RemoveCard(_stagedPreBattleCard);
                    CardView card = _stagedPreBattleCard;
                    _stagedPreBattleCard = null;
                    card.FlipAsync(destroyCancellationToken).Forget();
                    _handView.AddCardBackAsync(card, rect, destroyCancellationToken).Forget();
                    UpdateStagedButtons(_stagedPreBattleCard != null);
                }
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle2 && _prepInputTcs != null)
            {
                if (_stagedPrepCard != null)
                {
                    Rect rect = _stagedPrepCard.worldBound;
                    _playerFieldView.RemoveCard(_stagedPrepCard);
                    _handView.AddCardBackAsync(_stagedPrepCard, rect, destroyCancellationToken).Forget();
                    _stagedPrepCard = null;
                    UpdateStagedButtons(_stagedPrepCard != null);
                    return;
                }

                // ステージなし = パス
                HideActionButtons();
                _prepInputTcs.TrySetResult(null);
            }
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
