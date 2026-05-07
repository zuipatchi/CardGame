namespace Matching
{
    public enum MatchingState
    {
        Idle,
        Authenticating,
        BrowsingRooms,
        CreatingRoom,
        JoiningRoom,
        WaitingForPlayer,
        Starting,
        Ready,
        TimedOut,
        Error
    }
}
