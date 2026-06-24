using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class GraveyardView : VisualElement
    {
        private readonly List<CardData> _cards = new List<CardData>();
        private readonly VisualTreeAsset _cardTemplate;
        private readonly VisualElement _modalRoot;

        public int Count => _cards.Count;
        public Action<CardData> OnCardClicked { get; set; }

        public GraveyardView(VisualTreeAsset cardTemplate, VisualElement modalRoot)
        {
            _cardTemplate = cardTemplate;
            _modalRoot = modalRoot;

            AddToClassList("graveyard-view");

            VisualElement icon = new VisualElement();
            icon.AddToClassList("graveyard-count-icon");
            Add(icon);

            RegisterCallback<ClickEvent>(_ =>
            {
                if (_cards.Count > 0)
                {
                    OpenModal();
                }
            });
        }

        public void AddCard(CardView card)
        {
            card.RemoveFromHierarchy();
            _cards.Add(card.Data);
        }

        // 墓地にある指定属性のカード枚数を返す（GainVPPerGreenGrave などの動的な効果値に使う）
        public int CountByAttribute(CardAttribute attribute)
        {
            int count = 0;
            foreach (CardData card in _cards)
            {
                if (card.Attribute == attribute)
                {
                    count++;
                }
            }
            return count;
        }

        // 墓地のカード一覧のスナップショット（下＝0／上＝末尾。インデックスは内部リストと一致）。
        // SummonFromGrave などインデックス指定で取り除く効果で使う（両クライアントで同期済み）。
        // 呼び出し側がループ中に RemoveCardAt しても崩れないよう、コピーを返す（DeckView と同じ）。
        public IReadOnlyList<CardData> GetCardDataSnapshot()
        {
            return _cards.ToArray();
        }

        // 指定インデックスのカードを墓地から取り除いて返す（範囲外は null）。
        // 内部リストはインデックスが詰まるため、複数取り除くときは降順で呼ぶこと。
        public CardData RemoveCardAt(int index)
        {
            if (index < 0 || index >= _cards.Count)
            {
                return null;
            }
            CardData data = _cards[index];
            _cards.RemoveAt(index);
            return data;
        }

        public List<CardData> TakeFromTop(int count)
        {
            int toTake = Mathf.Min(count, _cards.Count);
            List<CardData> taken = new List<CardData>(toTake);
            for (int i = 0; i < toTake; i++)
            {
                taken.Add(_cards[_cards.Count - 1]);
                _cards.RemoveAt(_cards.Count - 1);
            }
            return taken;
        }

        private void OpenModal()
        {
            if (_modalRoot == null)
            {
                return;
            }

            VisualElement overlay = new VisualElement();
            overlay.AddToClassList("graveyard-modal-overlay");

            VisualElement panel = new VisualElement();
            panel.AddToClassList("graveyard-modal-panel");

            Label title = new Label($"墓地（{_cards.Count}枚）");
            title.AddToClassList("graveyard-modal-title");
            panel.Add(title);

            ScrollView scrollView = new ScrollView(ScrollViewMode.Horizontal);
            scrollView.AddToClassList("graveyard-modal-scroll");
            scrollView.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            scrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scrollView.contentContainer.style.flexDirection = FlexDirection.Row;
            scrollView.contentContainer.style.alignItems = Align.Center;

            for (int i = 0; i < _cards.Count; i++)
            {
                VisualElement wrapper = new VisualElement();
                wrapper.AddToClassList("graveyard-modal-card-wrapper");

                string labelText = string.Empty;
                if (_cards.Count == 1)
                {
                    labelText = "トップ / ボトム";
                }
                else if (i == _cards.Count - 1)
                {
                    labelText = "トップ";
                }
                else if (i == 0)
                {
                    labelText = "ボトム";
                }

                Label posLabel = new Label(labelText);
                posLabel.AddToClassList("graveyard-modal-card-label");
                wrapper.Add(posLabel);

                CardView display = new CardView(_cardTemplate, _cards[i]);
                CardData capturedData = _cards[i];
                display.RegisterCallback<ClickEvent>(evt =>
                {
                    OnCardClicked?.Invoke(capturedData);
                    evt.StopPropagation();
                });
                wrapper.Add(display);

                scrollView.Add(wrapper);
            }

            panel.Add(scrollView);
            overlay.Add(panel);

            overlay.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == overlay)
                {
                    overlay.RemoveFromHierarchy();
                }
            });

            _modalRoot.Add(overlay);
        }
    }
}
