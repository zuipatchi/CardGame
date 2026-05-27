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

            if (_gameModel.Phase == TurnPhase.CharacterSet && _charSetInput.Tcs != null)
            {
                if (!_playerCharacterSlot.worldBound.Contains(worldPos))
                {
                    return false;
                }

                if (card.Data is not CharacterCardData)
                {
                    ShowToast("キャラカードのみセットできます");
                    return false;
                }

                if (_charSetInput.Card != null)
                {
                    return false;
                }

                _playerCharacterSlot.PlaceCard(card);
                _charSetInput.Card = card;
                card.FlipAsync(destroyCancellationToken).Forget();
                UpdateStagedButtons(_charSetInput.Card != null);
                UpdateCostWarning(_charSetInput.Card);
                return true;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle1 && _isLocalPreBattleActive)
            {
                if (!_playerFieldView.worldBound.Contains(worldPos) || _preBattleInput.Card != null)
                {
                    return false;
                }

                if (card.Data is EventCardData)
                {
                    ShowToast("スキルまたはキャラカードのみ使えます");
                    return false;
                }

                bool placed = _playerFieldView.TryPlace(card, worldPos);
                if (placed)
                {
                    _preBattleInput.Card = card;
                    card.FlipAsync(destroyCancellationToken).Forget();
                    UpdateStagedButtons(_preBattleInput.Card != null);
                    UpdateCostWarning(_preBattleInput.Card);
                }
                return placed;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle2 && _gameModel.IsLocalPreparationTurn)
            {
                if (!_playerFieldView.worldBound.Contains(worldPos) || _prepInput.Card != null)
                {
                    return false;
                }

                if (card.Data is not EventCardData)
                {
                    ShowToast("イベントカードのみ使えます");
                    return false;
                }

                bool placed = _playerFieldView.TryPlace(card, worldPos);
                if (placed)
                {
                    _prepInput.Card = card;
                    UpdateStagedButtons(true);
                    UpdateCostWarning(_prepInput.Card);
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
            TurnPhase phase = _gameModel.Phase;
            if (phase != TurnPhase.CharacterSet && phase != TurnPhase.PreBattle1)
            {
                await PlayOkFlashAsync(true, destroyCancellationToken);
            }
            tcs.TrySetResult(card);
        }

        private bool TryTakeStagedInput(out UniTaskCompletionSource<CardView> tcs, out CardView card)
        {
            if (_gameModel.Phase == TurnPhase.CharacterSet && _charSetInput.Tcs != null && _charSetInput.Card != null)
            {
                tcs = _charSetInput.Tcs;
                card = _charSetInput.Card;
                _charSetInput.Card = null;
                _charSetInput.Tcs = null;
                return true;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle1 && _preBattleInput.Tcs != null && _preBattleInput.Card != null)
            {
                tcs = _preBattleInput.Tcs;
                card = _preBattleInput.Card;
                _preBattleInput.Card = null;
                _preBattleInput.Tcs = null;
                return true;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle2 && _prepInput.Tcs != null && _prepInput.Card != null)
            {
                tcs = _prepInput.Tcs;
                card = _prepInput.Card;
                _prepInput.Card = null;
                _prepInput.Tcs = null;
                return true;
            }

            tcs = null;
            card = null;
            return false;
        }

        private void OnBackClicked()
        {
            if (_gameModel.Phase == TurnPhase.CharacterSet && _charSetInput.Tcs != null)
            {
                if (_charSetInput.Card != null)
                {
                    CardView card = _charSetInput.Card;
                    _charSetInput.Card = null;
                    ReturnStagedCardToHand(card, card.worldBound, () => _playerCharacterSlot.RemoveCard(), flipCard: true);
                }
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle1 && _preBattleInput.Tcs != null)
            {
                if (_preBattleInput.Card != null)
                {
                    CardView card = _preBattleInput.Card;
                    _preBattleInput.Card = null;
                    ReturnStagedCardToHand(card, card.worldBound, () => _playerFieldView.RemoveCard(card), flipCard: true);
                }
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle2 && _prepInput.Tcs != null)
            {
                if (_prepInput.Card != null)
                {
                    CardView card = _prepInput.Card;
                    _prepInput.Card = null;
                    ReturnStagedCardToHand(card, card.worldBound, () => _playerFieldView.RemoveCard(card), flipCard: false);
                    return;
                }

                // ステージなし = パス
                HideActionButtons();
                _prepInput.Tcs.TrySetResult(null);
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
            if (_gameModel.Phase == TurnPhase.CharacterSet && _charSetInput.Tcs != null && _charSetInput.Card == null)
            {
                _charSetInput.Tcs.TrySetResult(null);
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle1 && _preBattleInput.Tcs != null && _preBattleInput.Card == null)
            {
                _preBattleInput.Tcs.TrySetResult(null);
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle2 && _prepInput.Tcs != null && _prepInput.Card == null)
            {
                HideActionButtons();
                _prepInput.Tcs.TrySetResult(null);
            }
        }

        // ─── ドラッグ可否判定 ────────────────────────────────────────────

        private bool CanPlayerDragCard()
        {
            if (_isGameOver)
            {
                return false;
            }

            TurnPhase phase = _gameModel.Phase;
            if (phase == TurnPhase.CharacterSet)
            {
                return _charSetInput.Tcs != null && _charSetInput.Card == null;
            }

            if (phase == TurnPhase.PreBattle1)
            {
                return _preBattleInput.Tcs != null && _isLocalPreBattleActive && _preBattleInput.Card == null;
            }

            if (phase == TurnPhase.PreBattle2)
            {
                return _prepInput.Tcs != null && _gameModel.IsLocalPreparationTurn && _prepInput.Card == null;
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

            TurnPhase phase = _gameModel.Phase;
            if (phase == TurnPhase.CharacterSet && _charSetInput.Tcs != null && _charSetInput.Card == null)
            {
                return card.Data is CharacterCardData;
            }

            if (phase == TurnPhase.PreBattle1 && _preBattleInput.Tcs != null && _isLocalPreBattleActive && _preBattleInput.Card == null)
            {
                return card.Data is not EventCardData;
            }

            if (phase == TurnPhase.PreBattle2 && _prepInput.Tcs != null && _gameModel.IsLocalPreparationTurn && _prepInput.Card == null)
            {
                return card.Data is EventCardData;
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

        // ─── UI ヘルパー ─────────────────────────────────────────────────

        private void UpdateStagedButtons(bool hasStaged)
        {
            RefreshHandHighlights();
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
