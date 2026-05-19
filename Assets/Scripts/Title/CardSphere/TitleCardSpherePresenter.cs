using System.Collections.Generic;
using Main.Card;
using UnityEngine;

namespace Title.CardSphere
{
    public sealed class TitleCardSpherePresenter : MonoBehaviour
    {
        [SerializeField] private CardDatabase _cardDatabase;
        [SerializeField] private Camera _camera;
        [SerializeField] private float _radius = 3f;
        [SerializeField] private float _cardScale = 0.4f;
        [SerializeField] private int _totalCards = 6;
        [SerializeField] private float _tiltDeg = 30f;
        [SerializeField] private float _periodSec = 20f;

        private readonly List<Transform> _cardTransforms = new List<Transform>();
        private readonly List<SpriteRenderer> _spriteRenderers = new List<SpriteRenderer>();
        private IReadOnlyList<CardData> _allCards;
        private float _elapsed = 0f;

        private void Start()
        {
            if (_cardDatabase == null)
            {
                return;
            }

            _allCards = _cardDatabase.AllCards;
            if (_allCards.Count == 0)
            {
                return;
            }

            transform.localRotation = Quaternion.Euler(_tiltDeg, 0f, 0f);

            for (int i = 0; i < _totalCards; i++)
            {
                float angle = i * (360f / _totalCards);
                float rad = angle * Mathf.Deg2Rad;
                Vector3 localPos = new Vector3(Mathf.Cos(rad) * _radius, 0f, Mathf.Sin(rad) * _radius);

                GameObject cardObj = new GameObject($"Card_{angle:F0}");
                cardObj.transform.SetParent(transform);
                cardObj.transform.localPosition = localPos;
                cardObj.transform.localScale = Vector3.one * _cardScale;

                SpriteRenderer sr = cardObj.AddComponent<SpriteRenderer>();
                _cardTransforms.Add(cardObj.transform);
                _spriteRenderers.Add(sr);
            }

            AssignRandomCards();
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, 360f / _periodSec * Time.deltaTime, Space.Self);

            _elapsed += Time.deltaTime;
            if (_elapsed >= _periodSec)
            {
                _elapsed -= _periodSec;
                AssignRandomCards();
            }

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

        private void AssignRandomCards()
        {
            List<int> indices = new List<int>(_allCards.Count);
            for (int i = 0; i < _allCards.Count; i++)
            {
                indices.Add(i);
            }

            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int temp = indices[i];
                indices[i] = indices[j];
                indices[j] = temp;
            }

            for (int i = 0; i < _spriteRenderers.Count; i++)
            {
                _spriteRenderers[i].sprite = _allCards[indices[i % indices.Count]].Image;
            }
        }
    }
}
