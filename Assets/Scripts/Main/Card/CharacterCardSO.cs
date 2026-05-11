using System.Collections.Generic;
using UnityEngine;

namespace Main.Card
{
    [CreateAssetMenu(fileName = "CharacterCards", menuName = "Card/Character Cards")]
    public sealed class CharacterCardSO : ScriptableObject
    {
        [SerializeField] private List<CharacterCardData> _cards;

        public IReadOnlyList<CharacterCardData> Cards => _cards;
    }
}
