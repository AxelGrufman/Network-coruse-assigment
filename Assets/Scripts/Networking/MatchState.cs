
using System;
using Unity.Netcode;

namespace NetworkTankArena.Networking
{
    public struct MatchState : INetworkSerializable, IEquatable<MatchState>
    {
        public int RedScore;
        public int BlueScore;
        public MatchPhase Phase;
        public TankTeam Winner;

        public MatchState(
            int redScore,
            int blueScore,
            MatchPhase phase,
            TankTeam winner)
        {
            RedScore = redScore;
            BlueScore = blueScore;
            Phase = phase;
            Winner = winner;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref RedScore);
            serializer.SerializeValue(ref BlueScore);

            byte phaseByte = (byte)Phase;
            byte winnerByte = (byte)Winner;

            serializer.SerializeValue(ref phaseByte);
            serializer.SerializeValue(ref winnerByte);

            if (serializer.IsReader)
            {
                Phase = (MatchPhase)phaseByte;
                Winner = (TankTeam)winnerByte;
            }
        }

        public bool Equals(MatchState other)
        {
            return RedScore == other.RedScore &&
                   BlueScore == other.BlueScore &&
                   Phase == other.Phase &&
                   Winner == other.Winner;
        }
    }
}
