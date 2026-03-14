using UnityEngine;

namespace ZoundsDemo
{
    /// <summary>
    /// Spawns warriors for both teams using a credit economy that keeps the battle balanced.
    ///
    /// Each team accumulates credits per second. The team with less total alive value on
    /// screen earns credits faster, proportional to the imbalance. When a team has enough
    /// credits to afford a warrior it spawns one and deducts the cost.
    ///
    /// Warriors spawn at the left/right screen edges at stratified Y positions so they
    /// spread across the full vertical range of the screen.
    /// </summary>
    public class WarriorSpawner : MonoBehaviour
    {
        [Header("Warrior Types")]
        [Tooltip("Pool of warrior types available for Team A.")]
        public WarriorData[] teamAWarriorTypes;

        [Tooltip("Pool of warrior types available for Team B.")]
        public WarriorData[] teamBWarriorTypes;

        [Header("Credit Economy")]
        [Tooltip("Credits both teams earn per second at baseline (when values are equal).")]
        public float baseCreditsPerSecond = 8f;

        [Tooltip("How aggressively the weaker side earns extra credits. " +
                 "1 = proportional to value gap. 0 = no catch-up.")]
        public float catchUpFactor = 1.2f;

        [Tooltip("Maximum total alive spawn-cost value allowed per team. " +
                 "A team stops spawning once it reaches this cap.")]
        public float maxTeamValue = 80f;

        [Header("Spawning")]
        [Tooltip("Minimum seconds between any two spawns for the same team.")]
        public float minSpawnInterval = 0.8f;

        [Tooltip("Horizontal offset from the screen edge where warriors spawn.")]
        public float spawnEdgeOffset = 0.5f;

        [Tooltip("Number of vertical lanes warriors are distributed across.")]
        public int laneCount = 8;

        [Header("Warrior Prefab")]
        [Tooltip("Prefab used for all warriors. Must have a Warrior component.")]
        public GameObject warriorPrefab;

        // ── Runtime state ────────────────────────────────────────────────────────
        private float creditsA;
        private float creditsB;
        private float spawnCooldownA;
        private float spawnCooldownB;
        private Camera mainCamera;

        private int nextLaneA;
        private int nextLaneB;

        private void Awake()
        {
            mainCamera = Camera.main;
            nextLaneB  = laneCount / 2;
            // Stagger the first spawns between teams
            creditsA = Random.Range(0f, baseCreditsPerSecond);
            creditsB = Random.Range(0f, baseCreditsPerSecond);
        }

        private void Update()
        {
            if (BattleManager.Instance == null) return;

            float valueA = BattleManager.Instance.GetTeamValue(Team.A);
            float valueB = BattleManager.Instance.GetTeamValue(Team.B);

            AccumulateCredits(ref creditsA, valueA, valueB);
            AccumulateCredits(ref creditsB, valueB, valueA);

            spawnCooldownA -= Time.deltaTime;
            spawnCooldownB -= Time.deltaTime;

            TrySpend(Team.A, ref creditsA, ref spawnCooldownA, valueA);
            TrySpend(Team.B, ref creditsB, ref spawnCooldownB, valueB);
        }

        // ── Credit logic ─────────────────────────────────────────────────────────

        /// <summary>
        /// Earns credits for a team. The team with less alive value earns proportionally
        /// more, creating a catch-up effect that keeps the battle going indefinitely.
        /// </summary>
        private void AccumulateCredits(ref float credits, float myValue, float theirValue)
        {
            float gap        = Mathf.Max(0f, theirValue - myValue);
            float totalValue = myValue + theirValue;
            float imbalance  = totalValue > 0f ? gap / totalValue : 0f;
            float rate       = baseCreditsPerSecond * (1f + imbalance * catchUpFactor);
            credits         += rate * Time.deltaTime;
        }

        /// <summary>
        /// Spends credits to spawn a warrior if the team can afford one and is below cap.
        /// Picks a random affordable entry from the pool each time.
        /// </summary>
        private void TrySpend(Team team, ref float credits, ref float cooldown, float currentValue)
        {
            if (cooldown > 0f) return;
            if (currentValue >= maxTeamValue) return;

            WarriorData[] pool = team == Team.A ? teamAWarriorTypes : teamBWarriorTypes;
            if (pool == null || pool.Length == 0) return;

            // Iterate from a random start so no single entry is always preferred
            WarriorData chosen   = null;
            float       chosenCost = 0f;
            int         startIdx = Random.Range(0, pool.Length);

            for (int i = 0; i < pool.Length; i++)
            {
                WarriorData candidate = pool[(startIdx + i) % pool.Length];
                if (candidate != null && candidate.spawnCost <= credits)
                {
                    chosen     = candidate;
                    chosenCost = candidate.spawnCost;
                    break;
                }
            }

            if (chosen == null) return;

            credits  -= chosenCost;
            cooldown  = minSpawnInterval;
            SpawnWarrior(chosen, team);
        }

        // ── Spawn helpers ────────────────────────────────────────────────────────

        private void SpawnWarrior(WarriorData data, Team team)
        {
            float   laneY         = GetNextLaneY(team);
            Vector3 spawnPosition = GetSpawnPosition(team, laneY);

            GameObject warriorGO = Instantiate(warriorPrefab, spawnPosition, Quaternion.identity);
            Warrior    warrior   = warriorGO.GetComponent<Warrior>();

            if (warrior == null)
            {
                Debug.LogError("WarriorSpawner: warriorPrefab is missing a Warrior component.", warriorPrefab);
                Destroy(warriorGO);
                return;
            }

            warrior.Initialize(data, team, laneY);
        }

        private float GetNextLaneY(Team team)
        {
            float screenHalfHeight = mainCamera != null ? mainCamera.orthographicSize : 4f;
            float margin           = screenHalfHeight * 0.1f;
            float usableTop        = screenHalfHeight - margin;
            float usableBot        = -screenHalfHeight + margin;
            float laneSpacing      = (usableTop - usableBot) / Mathf.Max(laneCount - 1, 1);

            ref int laneIndex = ref (team == Team.A ? ref nextLaneA : ref nextLaneB);
            float   laneCenter = usableBot + laneIndex * laneSpacing;
            laneIndex          = (laneIndex + 1) % laneCount;

            float jitter = Random.Range(-laneSpacing * 0.3f, laneSpacing * 0.3f);
            return Mathf.Clamp(laneCenter + jitter, usableBot, usableTop);
        }

        private Vector3 GetSpawnPosition(Team team, float laneY)
        {
            float halfWidth   = mainCamera != null ? mainCamera.orthographicSize * mainCamera.aspect : 8f;
            float screenEdgeX = team == Team.A
                ? -halfWidth + spawnEdgeOffset
                :  halfWidth - spawnEdgeOffset;
            return new Vector3(screenEdgeX, laneY, 0f);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            float halfWidth  = cam.orthographicSize * cam.aspect;
            float halfHeight = cam.orthographicSize;
            float margin     = halfHeight * 0.1f;
            float usableTop  = halfHeight - margin;
            float usableBot  = -halfHeight + margin;

            Gizmos.color = new Color(0f, 0.5f, 1f, 0.5f);
            Gizmos.DrawLine(new Vector3(-halfWidth + spawnEdgeOffset, -halfHeight, 0f),
                            new Vector3(-halfWidth + spawnEdgeOffset,  halfHeight, 0f));

            Gizmos.color = new Color(1f, 0.3f, 0f, 0.5f);
            Gizmos.DrawLine(new Vector3(halfWidth - spawnEdgeOffset, -halfHeight, 0f),
                            new Vector3(halfWidth - spawnEdgeOffset,  halfHeight, 0f));

            float laneSpacing = (usableTop - usableBot) / Mathf.Max(laneCount - 1, 1);
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            for (int i = 0; i < laneCount; i++)
            {
                float y = usableBot + i * laneSpacing;
                Gizmos.DrawLine(new Vector3(-halfWidth, y, 0f), new Vector3(halfWidth, y, 0f));
            }
        }
#endif
    }
}
