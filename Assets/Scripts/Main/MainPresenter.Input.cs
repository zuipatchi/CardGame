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

            if (_switchInput._tcs != null)
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

                if (_switchInput._card != null)
                {
                    return false;
                }

                _playerCharacterSlot.PlaceCard(card);
                _switchInput._card = card;
                UpdateStagedButtons(_switchInput._card != null);
                UpdateCostWarning(_switchInput._card);
                if (_optionModel.AutoOk.CurrentValue) { OnOkClicked(); }
                return true;
            }

            if (_gameModel.Phase == TurnPhase.CharacterSet && _charSetInput._tcs != null)
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

                if (_charSetInput._card != null)
                {
                    return false;
                }

                _playerCharacterSlot.PlaceCard(card);
                _charSetInput._card = card;
                card.FlipAsync(destroyCancellationToken).Forget();
                UpdateStagedButtons(_charSetInput._card != null);
                UpdateCostWarning(_charSetInput._card);
                if (_optionModel.AutoOk.CurrentValue) { OnOkClicked(); }
                return true;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle1 && _isLocalPreBattleActive)
            {
                if (!_playerFieldView.worldBound.Contains(worldPos) || _preBattleInput._card != null)
                {
                    return false;
                }

                if (card.Data is not SkillCardData)
                {
                    ShowToast("技カードのみ使えます");
                    return false;
                }

                bool placed = _playerFieldView.TryPlace(card, worldPos);
                if (placed)
                {
                    _preBattleInput._card = card;
                    card.FlipAsync(destroyCancellationToken).Forget();
                    UpdateStagedButtons(_preBattleInput._card != null);
                    UpdateCostWarning(_preBattleInput._card);
                    if (_optionModel.AutoOk.CurrentValue) { OnOkClicked(); }
                }
                return placed;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle2 && _gameModel.IsLocalPreparationTurn)
            {
                if (!_playerFieldView.worldBound.Contains(worldPos) || _prepInput._card != null)
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
                    _prepInput._card = card;
                    UpdateStagedButtons(true);
                    UpdateCostWarning(_prepInput._card);
                    if (_optionModel.AutoOk.CurrentValue) { OnOkClicked(); }
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
            if (phase != TurnPhase.CharacterSet && phase != TurnPhase.PreBattle1 && phase != TurnPhase.PreBattle2)
            {
                await PlayOkFlashAsync(true, destroyCancellationToken);
            }
            tcs.TrySetResult(card);
        }

        private bool TryTakeStagedInput(out UniTaskCompletionSource<CardView> tcs, out CardView card)
        {
            if (_switchInput._tcs != null && _switchInput._card != null)
            {
                tcs = _switchInput._tcs;
                card = _switchInput._card;
                _switchInput._card = null;
                _switchInput._tcs = null;
                return true;
            }

            if (_gameModel.Phase == TurnPhase.CharacterSet && _charSetInput._tcs != null && _charSetInput._card != null)
            {
                tcs = _charSetInput._tcs;
                card = _charSetInput._card;
                _charSetInput._card = null;
                _charSetInput._tcs = null;
                return true;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle1 && _preBattleInput._tcs != null && _preBattleInput._card != null)
            {
                tcs = _preBattleInput._tcs;
                card = _preBattleInput._card;
                _preBattleInput._card = null;
                _preBattleInput._tcs = null;
                return true;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle2 && _prepInput._tcs != null && _prepInput._card != null)
            {
                tcs = _prepInput._tcs;
                card = _prepInput._card;
                _prepInput._card = null;
                _prepInput._tcs = null;
                return true;
            }

            tcs = null;
            card = null;
            return false;
        }

        private void OnBackClicked()
        {
            if (_switchInput._tcs != null)
            {
                if (_switchInput._card != null)
                {
                    CardView card = _switchInput._card;
                    _switchInput._card = null;
                    ReturnStagedCardToHand(card, card.worldBound, () => _playerCharacterSlot.RemoveCard(), flipCard: false);
                }
                return;
            }

            if (_gameModel.Phase == TurnPhase.CharacterSet && _charSetInput._tcs != null)
            {
                if (_charSetInput._card != null)
                {
                    CardView card = _charSetInput._card;
                    _charSetInput._card = null;
                    ReturnStagedCardToHand(card, card.worldBound, () => _playerCharacterSlot.RemoveCard(), flipCard: true);
                }
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle1 && _preBattleInput._tcs != null)
            {
                if (_preBattleInput._card != null)
                {
                    CardView card = _preBattleInput._card;
                    _preBattleInput._card = null;
                    ReturnStagedCardToHand(card, card.worldBound, () => _playerFieldView.RemoveCard(card), flipCard: true);
                }
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle2 && _prepInput._tcs != null)
            {
                if (_prepInput._card != null)
                {
                    CardView card = _prepInput._card;
                    _prepInput._card = null;
                    ReturnStagedCardToHand(card, card.worldBound, () => _playerFieldView.RemoveCard(card), flipCard: false);
                    return;
                }

                // ステージなし = パス
                HideActionButtons();
                _prepInput._tcs.TrySetResult(null);
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
            if (_switchInput._tcs != null && _switchInput._card == null)
            {
                HideActionButtons();
                _switchInput._tcs.TrySetResult(null);
                return;
            }

            if (_gameModel.Phase == TurnPhase.CharacterSet && _charSetInput._tcs != null && _charSetInput._card == null)
            {
                _charSetInput._tcs.TrySetResult(null);
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle1 && _preBattleInput._tcs != null && _preBattleInput._card == null)
            {
                _preBattleInput._tcs.TrySetResult(null);
                return;
            }

            if (_gameModel.Phase == TurnPhase.PreBattle2 && _prepInput._tcs != null && _prepInput._card == null)
            {
                HideActionButtons();
                _prepInput._tcs.TrySetResult(null);
            }
        }

        // ─── ドラッグ可否判定 ────────────────────────────────────────────

        private bool CanPlayerDragCard()
        {
            if (_isGameOver)
            {
                return false;
            }

            if (_switchInput._tcs != null)
            {
                return _switchInput._card == null;
            }

            TurnPhase phase = _gameModel.Phase;
            if (phase == TurnPhase.CharacterSet)
            {
                return _charSetInput._tcs != null && _charSetInput._card == null;
            }

            if (phase == TurnPhase.PreBattle1)
            {
                return _preBattleInput._tcs != null && _isLocalPreBattleActive && _preBattleInput._card == null;
            }

            if (phase == TurnPhase.PreBattle2)
            {
                return _prepInput._tcs != null && _gameModel.IsLocalPreparationTurn && _prepInput._card == null;
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

            if (_switchInput._tcs != null && _switchInput._card == null)
            {
                return card.Data is CharacterCardData;
            }

            TurnPhase phase = _gameModel.Phase;
            if (phase == TurnPhase.CharacterSet && _charSetInput._tcs != null && _charSetInput._card == null)
            {
                return card.Data is CharacterCardData;
            }

            if (phase == TurnPhase.PreBattle1 && _preBattleInput._tcs != null && _isLocalPreBattleActive && _preBattleInput._card == null)
            {
                return card.Data is SkillCardData;
            }

            if (phase == TurnPhase.PreBattle2 && _prepInput._tcs != null && _gameModel.IsLocalPreparationTurn && _prepInput._card == null)
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
