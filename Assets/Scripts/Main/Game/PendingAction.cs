using Main.Card;

namespace Main.Game
{
    public abstract class PendingAction { }

    public sealed class PlayCardAction : PendingAction { }

    public sealed class AttackAction : PendingAction
    {
        public CardView Target { get; }

        public AttackAction(CardView target)
        {
            Target = target;
        }
    }

    public sealed class DeckAttackAction : PendingAction
    {
        public DeckView Target { get; }

        public DeckAttackAction(DeckView target)
        {
            Target = target;
        }
    }
}
