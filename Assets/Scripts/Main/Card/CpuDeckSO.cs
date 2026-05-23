using System.Collections.Generic;
using UnityEngine;

namespace Main.Card
{
    [CreateAssetMenu(fileName = "CpuDeck", menuName = "Card Game/CPU Deck")]
    public sealed class CpuDeckSO : ScriptableObject
    {
        public List<string> CardIds = new List<string>();
    }
}
