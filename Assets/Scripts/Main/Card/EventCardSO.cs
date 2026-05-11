using System.Collections.Generic;
using UnityEngine;

namespace Main.Card
{
    [CreateAssetMenu(fileName = "EventCards", menuName = "Card/Event Cards")]
    public sealed class EventCardSO : ScriptableObject
    {
        [SerializeField] private List<EventCardData> _cards;

        public IReadOnlyList<EventCardData> Cards => _cards;
    }
}
