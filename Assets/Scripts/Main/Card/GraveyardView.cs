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
