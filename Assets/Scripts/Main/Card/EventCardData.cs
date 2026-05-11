using System;

namespace Main.Card
{
    [Serializable]
    public sealed class EventCardData : CardData
    {
        public EventCardData() { }

        public EventCardData(string id, string name, int cost)
            : base(id, name, cost) { }
    }
}
