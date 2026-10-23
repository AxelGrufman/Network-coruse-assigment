
using UnityEngine;
using Unity.Netcode;

namespace NetworkTankArena.Networking
{
    public enum MatchPhase : byte
    {
        Playing,
        Finished
    }

    [RequireComponent(typeof(NetworkObject))]
    public class NetworkMatchManager : NetworkBehaviour
    {
        private const int ScoreToWin = 3;

        private readonly NetworkVariable<MatchState> matchState =
            new NetworkVariable<MatchState>(
                new MatchState(
                    0, 0,
                    MatchPhase.Playing,
                    TankTeam.Unassigned),
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        public bool IsPlaying =>
            IsSpawned &&
            matchState.Value.Phase == MatchPhase.Playing;

        public override void OnNetworkSpawn()
        {
            matchState.OnValueChanged += OnMatchStateChanged;

            if (IsServer)
            {
                matchState.Value = new MatchState(
                    0, 0,
                    MatchPhase.Playing,
                    TankTeam.Unassigned);
            }

            // Also check the initial state for late joiners.
            ValidateMatchState(matchState.Value);
        }

        public override void OnNetworkDespawn()
        {
            matchState.OnValueChanged -= OnMatchStateChanged;
        }

        public void RecordElimination(
            NetworkTankController attacker,
            NetworkTankController victim)
        {
            if (!IsServer || !IsPlaying ||
                attacker == null || victim == null ||
                !attacker.IsSpawned || !victim.IsSpawned ||
                attacker == victim)
                return;

            TankTeam attackerTeam = attacker.Team;
            TankTeam victimTeam = victim.Team;

            if (attackerTeam == TankTeam.Unassigned ||
                victimTeam == TankTeam.Unassigned ||
                attackerTeam == victimTeam)
                return;

            NetworkTankHealth victimHealth =
                victim.GetComponent<NetworkTankHealth>();

            if (victimHealth == null || victimHealth.IsAlive)
                return;

            // Create a copy of the existing match state.
            MatchState next = matchState.Value;

            int newScore;

            if (attackerTeam == TankTeam.Red)
            {
                next.RedScore++;
                newScore = next.RedScore;
            }
            else if (attackerTeam == TankTeam.Blue)
            {
                next.BlueScore++;
                newScore = next.BlueScore;
            }
            else
            {
                return;
            }

            // Finish the match within this same snapshot.
            if (newScore >= ScoreToWin)
            {
                next.Phase = MatchPhase.Finished;
                next.Winner = attackerTeam;
            }

            // Publish all changes together.
            matchState.Value = next;
        }

        private void OnMatchStateChanged(
            MatchState previous,
            MatchState current)
        {
            ValidateMatchState(current);
        }

        private void ValidateMatchState(MatchState state)
        {
            bool valid = state.RedScore >= 0 &&
                         state.BlueScore >= 0;

            if (state.Phase == MatchPhase.Playing)
            {
                valid &= state.Winner == TankTeam.Unassigned &&
                         state.RedScore < ScoreToWin &&
                         state.BlueScore < ScoreToWin;
            }
            else if (state.Phase == MatchPhase.Finished)
            {
                valid &= (state.Winner == TankTeam.Red &&
                          state.RedScore == ScoreToWin &&
                          state.BlueScore < ScoreToWin)
                      || (state.Winner == TankTeam.Blue &&
                          state.BlueScore == ScoreToWin &&
                          state.RedScore < ScoreToWin);
            }
            else
            {
                valid = false;
            }

            if (!valid)
            {
                Debug.LogError(
                    "Invalid match state received!", this);
            }

            Debug.Log(
                $"MATCH STATE | Server: {IsServer} | " +
                $"Red: {state.RedScore} | " +
                $"Blue: {state.BlueScore} | " +
                $"Phase: {state.Phase} | " +
                $"Winner: {state.Winner} | " +
                $"Valid: {valid}", this);
        }

        private void OnGUI()
        {
            if (!IsSpawned || !IsClient)
                return;

            // Read one complete snapshot for the UI.
            MatchState current = matchState.Value;

            Rect board = new Rect(
                Screen.width - 310f, 12f, 300f, 100f);

            GUILayout.BeginArea(board, GUI.skin.box);
            GUILayout.Label("TEAM DEATHMATCH");
            GUILayout.Label($"RED: {current.RedScore}");
            GUILayout.Label($"BLUE: {current.BlueScore}");
            GUILayout.EndArea();

            if (current.Phase == MatchPhase.Finished)
            {
                Rect result = new Rect(
                    (Screen.width - 400f) / 2f,
                    (Screen.height - 150f) / 2f,
                    400f, 150f);

                GUI.Box(result,
                    $"{current.Winner.ToString().ToUpper()} TEAM WINS!\n\n" +
                    $"Final Score: {current.RedScore} - {current.BlueScore}");
            }
        }
    }
}
