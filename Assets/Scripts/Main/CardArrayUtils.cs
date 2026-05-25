using Main.Card;
using UnityEngine;

namespace Main
{
    internal static class CardArrayUtils
    {
        internal static CardData[] Shuffle(CardData[] cards)
        {
            for (int i = cards.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
            return cards;
        }
    }
}
