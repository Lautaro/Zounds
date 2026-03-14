using UnityEngine;

namespace ZoundsDemo
{
    /// <summary>
    /// ScriptableObject that defines the base stats for a warrior type.
    /// Each spawned instance applies a random deviation to these values.
    /// </summary>
    [CreateAssetMenu(fileName = "WarriorData", menuName = "ZoundsDemo/Warrior Data")]
    public class WarriorData : ScriptableObject
    {
        [Header("Visuals")]
        public Sprite sprite;
        public Color tint = Color.white;

        [Header("Ranged")]
        [Tooltip("When enabled the warrior fires a shot beam instead of lunging. Attack mechanics are identical.")]
        public bool isRanged = false;

        [Header("Juggernaut")]
        [Tooltip("When enabled this warrior hits all enemies in attack range simultaneously and pushes them back.")]
        public bool isJuggernaut = false;

        [Tooltip("World units each hit enemy is pushed away from this warrior's position.")]
        public float pushbackDistance = 0.5f;

        [Header("Lightweight")]
        [Tooltip("When enabled this warrior's attacks do not apply stun to their targets.")]
        public bool isLightweight = false;

        [Header("Stats")]
        [Tooltip("Base movement speed in world units per second.")]
        public float moveSpeed = 2f;

        [Tooltip("Base maximum health points.")]
        public float maxHealth = 100f;

        [Tooltip("Damage dealt per hit.")]
        public float damage = 20f;

        [Tooltip("Seconds between attacks.")]
        public float attackCooldown = 1f;

        [Tooltip("Seconds this warrior's attacks stun their targets.")]
        public float stunDuration = 0.2f;

        [Tooltip("World-unit distance at which this warrior starts attacking. Should be larger than the sprite so warriors don't overlap.")]
        public float attackRange = 1.2f;

        [Header("Economy")]
        [Tooltip("Spawn cost. The credit system uses this to keep both sides balanced.")]
        public float spawnCost = 10f;

        [Header("Separation")]
        [Tooltip("World-unit radius within which this warrior steers away from allies.")]
        public float separationRadius = 0.8f;

        [Tooltip("How strongly the warrior steers away from crowded allies. 0 = no separation.")]
        public float separationStrength = 1.8f;

        [Header("Stat Deviation")]
        [Range(0f, 1f)]
        [Tooltip("Fraction of each stat that can deviate randomly. E.g. 0.2 = ±20%.")]
        public float statDeviation = 0.2f;

        [Header("Zound Event Names")]
        [Tooltip("Zound triggered when this warrior spawns.")]
        public string zoundOnSpawn = "warrior_spawn";

        [Tooltip("Zound triggered when this warrior attacks.")]
        public string zoundOnAttack = "warrior_attack";

        [Tooltip("Zound triggered when this warrior takes a hit.")]
        public string zoundOnHit = "warrior_hit";

        [Tooltip("Zound triggered when this warrior dies.")]
        public string zoundOnDeath = "warrior_death";
    }
}
