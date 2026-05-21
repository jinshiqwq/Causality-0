namespace Causality0.Core
{

    public struct ElevatorInteractFrame
    {
        public float Timestamp;
        public int PlayerId;
        public byte ElevatorGroup;
        public int DestinationLevel;

        public ElevatorInteractFrame(float timestamp, int playerId, byte elevatorGroup, int destinationLevel)
        {
            Timestamp = timestamp;
            PlayerId = playerId;
            ElevatorGroup = elevatorGroup;
            DestinationLevel = destinationLevel;
        }
    }
}
