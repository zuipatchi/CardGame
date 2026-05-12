using System.Collections.Generic;

namespace Common.Deck
{
    public sealed class DeckModel
    {
        public const int MinSize = 5;
        public const int MaxSize = 20;

        private readonly List<string> _cardIds = new List<string>();

        public IReadOnlyList<string> CardIds => _cardIds;
        public int Count => _cardIds.Count;
        public bool IsReady => Count >= MinSize;
        public bool IsFull => Count >= MaxSize;

        public bool TryAdd(string id)
        {
            if (IsFull)
            {
                return false;
            }

            _cardIds.Add(id);
            return true;
        }

        public bool Remove(string id)
        {
            return _cardIds.Remove(id);
        }

        public void Clear()
        {
            _cardIds.Clear();
        }
    }
}
