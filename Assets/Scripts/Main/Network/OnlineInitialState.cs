using Main.Card;

namespace Main.Network
{
    public sealed class OnlineInitialState
    {
        public CardData[] LocalHand;
        public CardData[] LocalDeck;
        public int OpponentHandCount;
        public CardData[] OpponentDeck;
        public bool IsLocalFirst;
        public string OpponentUsername;
    }
}
