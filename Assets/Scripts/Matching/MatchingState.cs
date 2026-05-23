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
        WaitingInCreatedRoom,
        Starting,
        Ready,
        TimedOut,
        Error
    }
}
