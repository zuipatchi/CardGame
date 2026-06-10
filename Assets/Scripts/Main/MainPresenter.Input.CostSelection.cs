using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main
{
    public sealed partial class MainPresenter
    {
        private CardAttribute _playedCardAttribute;
        private string _costBaseMessage;

        // ─── コスト選択待ち ──────────────────────────────────────────────

        private async UniTask<List<CardView>> WaitForPlayerCostSelectionAsync(int cost, CardAttribute playedAttribute, CancellationToken ct)
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
                _playedCardAttribute = playedAttribute;
                _costSelectionTcs = new UniTaskCompletionSource();
                _selectedCostCards.Clear();

                _costBaseMessage = _requiredCostCount < cost
                    ? $"手札が足りません（{_requiredCostCount}/{cost}枚）"
                    : $"コストを {_requiredCostCount} 枚選んでください";
                _costWarningLabel.text = _costBaseMessage;
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

            bool countOk = _selectedCostCards.Count >= _requiredCostCount;
            bool attrOk = IsCostAttributeSatisfied();
            bool canConfirm = countOk && attrOk;

            _okButton.SetEnabled(canConfirm);

            if (countOk && !attrOk)
            {
                _costWarningLabel.text = $"「{AttributeDisplayName(_playedCardAttribute)}」のカードを1枚以上含めてください";
            }
            else
            {
                _costWarningLabel.text = _costBaseMessage;
            }

            if (canConfirm && _optionModel.AutoOk.CurrentValue)
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

        private void BeginStagedCostSelection(CardView staged, int availableForCost)
        {
            int cost = staged.Data.Cost;
            int required = Mathf.Min(cost, availableForCost);
            _requiredCostCount = required;
            _playedCardAttribute = staged.Data.Attribute;
            _costSelectionTcs = new UniTaskCompletionSource();
            _selectedCostCards.Clear();

            _costBaseMessage = required < cost
                ? $"手札が足りません（{required}/{cost}枚）"
                : $"コストを {required} 枚選んでください";
            _costWarningLabel.text = _costBaseMessage;
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
            bool initialOk = IsCostAttributeSatisfied();
            _okButton.SetEnabled(initialOk);
            _actionButtonsArea.AddToClassList("main-action-buttons-area--visible");

            if (autoOk && initialOk)
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

        // ─── 属性バリデーション ───────────────────────────────────────────

        private bool IsCostAttributeSatisfied()
        {
            if (_requiredCostCount == 0 || _playedCardAttribute == CardAttribute.White)
            {
                return true;
            }

            foreach (CardView c in _selectedCostCards)
            {
                if (c.Data.Attribute == _playedCardAttribute || c.Data.Attribute == CardAttribute.White)
                {
                    return true;
                }
            }

            return false;
        }

        private static string AttributeDisplayName(CardAttribute attr)
        {
            return attr switch
            {
                CardAttribute.Red => "赤",
                CardAttribute.Blue => "青",
                CardAttribute.Green => "緑",
                CardAttribute.Yellow => "黄",
                CardAttribute.Black => "黒",
                CardAttribute.Purple => "紫",
                CardAttribute.White => "白",
                _ => attr.ToString()
            };
        }
    }
}
