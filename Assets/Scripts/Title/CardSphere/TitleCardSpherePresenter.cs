using System.Collections.Generic;
using Main.Card;
using UnityEngine;

namespace Title.CardSphere
{
    public sealed class TitleCardSpherePresenter : MonoBehaviour
    {
        [SerializeField] private CardDatabase _cardDatabase;
        [SerializeField] private float _radius = 3f;
        [SerializeField] private float _cardScale = 0.4f;
        [SerializeField] private int _totalCards = 6;
        [SerializeField] private float _tiltDeg = 30f;
        [SerializeField] private float _periodSec = 20f;

        private readonly List<Transform> _cardTransforms = new List<Transform>();
        private Camera _camera;

        private void Start()
        {
            _camera = Camera.main;
            if (_cardDatabase == null)
            {
                return;
            }

            IReadOnlyList<CardData> allCards = _cardDatabase.AllCards;
            if (allCards.Count == 0)
            {
                return;
            }

            transform.localRotation = Quaternion.Euler(_tiltDeg, 0f, 0f);

            for (int i = 0; i < _totalCards; i++)
            {
                float angle = i * (360f / _totalCards);
                float rad = angle * Mathf.Deg2Rad;
                Vector3 localPos = new Vector3(Mathf.Cos(rad) * _radius, 0f, Mathf.Sin(rad) * _radius);

                CardData cardData = allCards[i % allCards.Count];

                GameObject cardObj = new GameObject($"Card_{angle:F0}");
                cardObj.transform.SetParent(transform);
                cardObj.transform.localPosition = localPos;
                cardObj.transform.localScale = Vector3.one * _cardScale;

                SpriteRenderer sr = cardObj.AddComponent<SpriteRenderer>();
                if (cardData.Image != null)
                {
                    sr.sprite = cardData.Image;
                }

                _cardTransforms.Add(cardObj.transform);
            }
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, 360f / _periodSec * Time.deltaTime, Space.Self);

            if (_camera == null)
            {
                return;
            }

            Quaternion camRotation = _camera.transform.rotation;
            foreach (Transform cardTransform in _cardTransforms)
            {
                cardTransform.rotation = camRotation;
            }
        }
    }
}
