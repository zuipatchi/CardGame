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

        // ローカルプレイヤーがこのカードをプレイする際の実効コスト。
        // NextCardCostFree 効果が立っていれば 0（次の1枚が無料）。
        private int EffectivePlayerCost(CardView card)
        {
            return _playerNextCardFree ? 0 : card.Data.Cost;
        }

        // 手札の指定カードを除いた、コストとして支払える総コスト値（各カードの CostPaymentValue 合計）。
        // payingForAttribute = プレイするカードの属性（CostBoost の属性連動判定に使う）
        private int CostCapacityExcluding(CardView excluded, CardAttribute payingForAttribute)
        {
            int sum = 0;
            foreach (CardView c in _handView.Cards)
            {
                if (c != excluded)
                {
                    sum += c.Data.CostPaymentValue(payingForAttribute);
                }
            }
            return sum;
        }

        // 現在選択中のコストカードの総コスト値（プレイするカードの属性で CostBoost を評価）
        private int SelectedCostValue()
        {
            int sum = 0;
            foreach (CardView c in _selectedCostCards)
            {
                sum += c.Data.CostPaymentValue(_playedCardAttribute);
            }
            return sum;
        }

        // ─── コスト選択待ち ──────────────────────────────────────────────

        private async UniTask<List<CardView>> WaitForPlayerCostSelectionAsync(int cost, CardAttribute playedAttribute, CancellationToken ct)
        {
            // BeginStagedCostSelection がドロップ時に先行して呼ばれた場合は TCS が既に存在する
            if (_costSelectionTcs == null)
            {
                int required = Mathf.Min(cost, CostCapacityExcluding(null, playedAttribute));
                if (required == 0)
                {
                    return new List<CardView>();
                }

                _requiredCost = required;
                _playedCardAttribute = playedAttribute;
                _costSelectionTcs = new UniTaskCompletionSource();
                _selectedCostCards.Clear();

                _costBaseMessage = _requiredCost < cost
                    ? $"手札が足りません（{_requiredCost}/{cost}）"
                    : $"コストを {_requiredCost} 支払ってください";
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
            _requiredCost = 0;

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
            else if (SelectedCostValue() < _requiredCost)
            {
                _selectedCostCards.Add(card);
                card.AddToClassList("cost-selected");
            }

            bool countOk = SelectedCostValue() >= _requiredCost;
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
            _endButton.style.display = DisplayStyle.None;
            _passButton.style.display = DisplayStyle.None;
            _backButton.style.display = DisplayStyle.None;
            _okButton.style.display = DisplayStyle.Flex;
            _okButton.SetEnabled(false);
            _actionButtonsArea.AddToClassList("main-action-buttons-area--visible");
        }

        private void BeginStagedCostSelection(CardView staged)
        {
            int cost = staged.Data.Cost;
            int required = Mathf.Min(cost, CostCapacityExcluding(staged, staged.Data.Attribute));
            _requiredCost = required;
            _playedCardAttribute = staged.Data.Attribute;
            _costSelectionTcs = new UniTaskCompletionSource();
            _selectedCostCards.Clear();

            _costBaseMessage = required < cost
                ? $"手札が足りません（{required}/{cost}）"
                : $"コストを {required} 支払ってください";
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
            _endButton.style.display = DisplayStyle.None;
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
            _requiredCost = 0;
        }

        // ─── 属性バリデーション ───────────────────────────────────────────

        private bool IsCostAttributeSatisfied()
        {
            if (_requiredCost == 0)
            {
                return true;
            }

            // 白も一般属性として扱う：プレイするカードと同じ属性のコストカードが最低1枚必要。
            // 白カードは他属性の要件を満たさない（数合わせとしては選べる）。
            foreach (CardView c in _selectedCostCards)
            {
                if (c.Data.Attribute == _playedCardAttribute)
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
