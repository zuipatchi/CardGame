using Main.Card;

namespace Main.Network
{
    public sealed class OnlineInitialState
    {
        public CardData[] LocalHand;
        public CardData[] LocalDeck;
        public int OpponentHandCount;
        public int OpponentDeckCount;
        public bool IsLocalFirst;
        public bool LocalNeedsMulligan;
        public bool OpponentNeedsMulligan;
    }
}
