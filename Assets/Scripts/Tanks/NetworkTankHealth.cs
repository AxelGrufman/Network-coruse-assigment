
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using NetworkTankArena.Combat;
using NetworkTankArena.Tanks;

namespace NetworkTankArena.Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(
        typeof(NetworkObject),
        typeof(NetworkTransform),
        typeof(CharacterController))]
    public class NetworkTankHealth : NetworkBehaviour, IDamageable
    {
        private const int MaxHealth = 100;

        [SerializeField, Min(0.1f)]
        private float respawnDelaySeconds = 3f;

        private readonly NetworkVariable<int> health =
            new NetworkVariable<int>(
                MaxHealth,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private double respawnAtTime;

        private NetworkTankController tankController;
        private NetworkTransform networkTransform;
        private CharacterController characterController;
        private TankSimulation tankSimulation;

        public int Health => health.Value;
        public bool IsAlive => health.Value > 0;

        private void Awake()
        {
            tankController = GetComponent<NetworkTankController>();
            networkTransform = GetComponent<NetworkTransform>();
            characterController = GetComponent<CharacterController>();
            tankSimulation = GetComponent<TankSimulation>();
        }

        public void ApplyDamage(int amount, GameObject source)
        {
            if (!IsSpawned || !IsServer ||
                amount <= 0 || !IsAlive)
                return;

            if (source == gameObject)
                return;


            NetworkMatchManager match =
                FindFirstObjectByType<NetworkMatchManager>();

            if (match == null || !match.IsPlaying)
                return;


            health.Value = Mathf.Max(0, health.Value - amount);

            Debug.Log(
                $"Tank {OwnerClientId}: {health.Value} HP",
                this);

            if (!IsAlive)
                Die(source, match);
        }


        private void Die(
            GameObject source,
            NetworkMatchManager match)
        {
            // Stop the dead tank's stored movement.
            tankController?.ClearServerMovement();

            // Schedule the respawn.
            respawnAtTime =
                Time.unscaledTimeAsDouble + respawnDelaySeconds;

            // Find which tank fired the projectile.
            NetworkTankController attacker =
                source != null
                    ? source.GetComponent<NetworkTankController>()
                    : null;

            // Report the confirmed elimination to the match.
            match.RecordElimination(attacker, tankController);

            Debug.Log($"Tank {OwnerClientId} died.", this);
        }


        private void Update()
        {
            if (!IsSpawned || !IsServer || IsAlive)
                return;

            NetworkMatchManager match =
                FindFirstObjectByType<NetworkMatchManager>();

            if (match == null || !match.IsPlaying)
                return;


            if (Time.unscaledTimeAsDouble >= respawnAtTime)
                Respawn();
        }

        private void Respawn()
        {
            LocalNetworkSession session =
                FindFirstObjectByType<LocalNetworkSession>();

            if (session == null ||
                !session.TryGetSpawnPose(
                    OwnerClientId,
                    out Vector3 position,
                    out Quaternion rotation))
            {
                Debug.LogError(
                    "Could not find respawn position.", this);

                // Retry later rather than revive in the wrong place.
                respawnAtTime = Time.unscaledTimeAsDouble + 1d;
                return;
            }

            // Clear old movement before reviving.
            tankController?.ClearServerMovement();

            // Temporarily disable the CharacterController
            // so it doesn't interfere with the teleport.
            bool wasEnabled = characterController.enabled;
            characterController.enabled = false;

            networkTransform.Teleport(
                position, rotation, transform.localScale);

            Physics.SyncTransforms();

            characterController.enabled = wasEnabled;

            // Reset the direction of the turret too.
            tankSimulation.ApplyReplicatedTurretYaw(
                rotation.eulerAngles.y);

            // Restore health last.
            health.Value = MaxHealth;

            Debug.Log(
                $"Tank {OwnerClientId} respawned.", this);
        }

        private void OnGUI()
        {
            if (!IsSpawned || !IsClient || !IsOwner)
                return;

            GUI.Box(
                new Rect(12f, 230f, 300f, 45f),
                $"Your Health: {Health} / {MaxHealth}");

            NetworkMatchManager match =
    FindFirstObjectByType<NetworkMatchManager>();

            if (!IsAlive && (match == null || match.IsPlaying))
            {
                float width = 360f;
                float height = 130f;

                Rect screen = new Rect(
                    (Screen.width - width) / 2f,
                    (Screen.height - height) / 2f,
                    width,
                    height);

                GUI.Box(
                    screen,
                    "YOU DIED\n\nRespawning soon...");
            }
        }
    }
}
