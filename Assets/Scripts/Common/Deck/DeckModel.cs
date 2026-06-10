using System.Collections.Generic;

namespace Common.Deck
{
    public sealed class DeckModel
    {
        public const int MaxCards = 30;
        public const int MaxCost = 80;

        private readonly List<(string id, int cost)> _entries = new List<(string id, int cost)>();

        public IReadOnlyList<(string id, int cost)> Entries => _entries;
        public int Count => _entries.Count;
        public int TotalCost
        {
            get
            {
                int total = 0;
                foreach ((string _, int cost) in _entries)
                {
                    total += cost;
                }
                return total;
            }
        }

        public bool IsReady => Count == MaxCards;
        public bool IsOver => Count > MaxCards;
        public bool IsCostOver => TotalCost > MaxCost;
        public bool IsValid => IsReady && !IsCostOver;

        public IReadOnlyList<string> CardIds
        {
            get
            {
                List<string> ids = new List<string>(_entries.Count);
                foreach ((string id, int _) in _entries)
                {
                    ids.Add(id);
                }
                return ids;
            }
        }

        public void Add(string id, int cost)
        {
            _entries.Add((id, cost));
        }

        public bool Remove(string id)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].id == id)
                {
                    _entries.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public void Clear()
        {
            _entries.Clear();
        }

        public void SortById()
        {
            _entries.Sort((a, b) =>
            {
                int orderA = GetIdSortOrder(a.id);
                int orderB = GetIdSortOrder(b.id);
                if (orderA != orderB)
                {
                    return orderA.CompareTo(orderB);
                }
                return string.Compare(a.id, b.id, System.StringComparison.Ordinal);
            });
        }

        private static int GetIdSortOrder(string id)
        {
            if (id.StartsWith("C"))
            {
                return 0;
            }
            if (id.StartsWith("S"))
            {
                return 1;
            }
            if (id.StartsWith("E"))
            {
                return 2;
            }
            return 3;
        }

        public void Reorder(IReadOnlyList<string> orderedIds)
        {
            Dictionary<string, List<(string entryId, int entryCost)>> groups =
                new Dictionary<string, List<(string entryId, int entryCost)>>();
            List<string> originalOrder = new List<string>();

            foreach ((string entryId, int entryCost) in _entries)
            {
                if (!groups.ContainsKey(entryId))
                {
                    groups[entryId] = new List<(string entryId, int entryCost)>();
                    originalOrder.Add(entryId);
                }
                groups[entryId].Add((entryId, entryCost));
            }

            _entries.Clear();

            HashSet<string> placed = new HashSet<string>();
            foreach (string id in orderedIds)
            {
                if (!groups.TryGetValue(id, out List<(string entryId, int entryCost)> group))
                {
                    continue;
                }
                foreach ((string eId, int eCost) in group)
                {
                    _entries.Add((eId, eCost));
                }
                placed.Add(id);
            }

            foreach (string id in originalOrder)
            {
                if (placed.Contains(id))
                {
                    continue;
                }
                foreach ((string eId, int eCost) in groups[id])
                {
                    _entries.Add((eId, eCost));
                }
            }
        }
    }
}
