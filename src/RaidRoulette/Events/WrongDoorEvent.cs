namespace RaidRoulette.Events
{
    public sealed class WrongDoorEvent() : ServerRuleEvent(
        "wrong_door",
        "Wrong Door",
        "Crossing an ordinary open doorway has a 50% chance to transport you beside another safe " +
        "open door on the same map. Keyed rooms, extraction mechanisms and blocked landing spaces " +
        "are excluded. Thirty seconds between successful teleports. Normal AI and player equipment " +
        "remain unchanged. Requires the matching client."
    );
}
