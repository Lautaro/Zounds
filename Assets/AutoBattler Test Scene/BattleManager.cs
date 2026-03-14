using System.Collections.Generic;
using UnityEngine;
using Zounds;
namespace ZoundsDemo
{
    /// <summary>
    /// Singleton that keeps track of all living warriors per team,
    /// exposes targeting queries, and tracks total spawn-cost value per side
    /// so the credit-based spawner can keep the battle balanced.
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        public static BattleManager Instance { get; private set; }

        [Tooltip("Weight applied to Y-distance when scoring potential targets. " +
                 "Lower = stronger same-lane preference. 0 = pure Euclidean distance.")]
        public float yDistanceWeight = 0.5f;

        // Parallel cost-tracking lists so GetTeamValue is O(n) with no extra allocation
        private readonly List<Warrior> teamA     = new List<Warrior>();
        private readonly List<Warrior> teamB     = new List<Warrior>();
        private readonly List<float>   teamACost = new List<float>();
        private readonly List<float>   teamBCost = new List<float>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>Registers a warrior with its spawn cost into the appropriate team list.</summary>
        public void Register(Warrior warrior, float cost)
        {
            if (warrior.Team == Team.A) { teamA.Add(warrior); teamACost.Add(cost); }
            else                        { teamB.Add(warrior); teamBCost.Add(cost); }
        }

        /// <summary>Removes a warrior from its team list.</summary>
        public void Unregister(Warrior warrior)
        {
            RemoveFromTeam(warrior, warrior.Team == Team.A ? teamA : teamB,
                                    warrior.Team == Team.A ? teamACost : teamBCost);
        }

        /// <summary>Returns the total alive spawn-cost value for a given team.</summary>
        public float GetTeamValue(Team team)
        {
            List<float> costs = team == Team.A ? teamACost : teamBCost;
            float total = 0f;
            foreach (float c in costs) total += c;
            return total;
        }

        /// <summary>
        /// Returns the best enemy target for the given warrior using a weighted score.
        /// X-distance is the primary axis; Y-distance adds a lane-preference penalty.
        /// </summary>
        public Warrior GetBestEnemy(Warrior warrior)
        {
            List<Warrior> enemies = GetEnemyTeam(warrior.Team);
            List<float>   costs   = GetEnemyCosts(warrior.Team);
            Warrior best      = null;
            float   bestScore = float.MaxValue;
            Vector2 myPos     = warrior.transform.position;

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Warrior enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive())
                {
                    enemies.RemoveAt(i);
                    costs.RemoveAt(i);
                    continue;
                }

                Vector2 enemyPos = enemy.transform.position;
                float xDist  = Mathf.Abs(myPos.x - enemyPos.x);
                float yDist  = Mathf.Abs(myPos.y - enemyPos.y);
                float score  = xDist + yDist * yDistanceWeight;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = enemy;
                }
            }

            return best;
        }

        /// <summary>Returns the distance to the nearest living enemy (any lane).</summary>
        public float GetNearestEnemyDistance(Warrior warrior)
        {
            List<Warrior> enemies = GetEnemyTeam(warrior.Team);
            List<float>   costs   = GetEnemyCosts(warrior.Team);
            float nearest = float.MaxValue;

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Warrior enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive())
                {
                    enemies.RemoveAt(i);
                    costs.RemoveAt(i);
                    continue;
                }

                float dist = Vector2.Distance(warrior.transform.position, enemy.transform.position);
                if (dist < nearest)
                    nearest = dist;
            }

            return nearest;
        }

        /// <summary>
        /// Returns the closest living enemy within range, or null if none are that close.
        /// Used for opportunistic targeting — a warrior in motion stops and switches
        /// target the moment any enemy enters its attack range.
        /// </summary>
        public Warrior GetEnemyInRange(Warrior warrior, float range)
        {
            List<Warrior> enemies = GetEnemyTeam(warrior.Team);
            List<float>   costs   = GetEnemyCosts(warrior.Team);
            Warrior closest     = null;
            float   closestDist = range;

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Warrior enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive())
                {
                    enemies.RemoveAt(i);
                    costs.RemoveAt(i);
                    continue;
                }

                float dist = Vector2.Distance(warrior.transform.position, enemy.transform.position);
                if (dist <= closestDist)
                {
                    closestDist = dist;
                    closest     = enemy;
                }
            }

            return closest;
        }

        /// <summary>
        /// Returns all living enemies within range. Used by juggernaut warriors
        /// to hit every enemy in attack range simultaneously.
        /// </summary>
        public List<Warrior> GetAllEnemiesInRange(Warrior warrior, float range)
        {
            List<Warrior> enemies = GetEnemyTeam(warrior.Team);
            List<Warrior> result  = new List<Warrior>();

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Warrior enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive())
                    continue;

                float dist = Vector2.Distance(warrior.transform.position, enemy.transform.position);
                if (dist <= range)
                    result.Add(enemy);
            }

            return result;
        }

        /// <summary>
        /// Calculates a separation steering force pushing the warrior away from
        /// nearby allies. Call this every frame from Warrior.Update and add it to
        /// the movement direction before normalising.
        /// </summary>
        public Vector2 GetSeparationForce(Warrior warrior, float radius)
        {
            List<Warrior> allies = GetTeam(warrior.Team);
            Vector2 force = Vector2.zero;
            Vector2 myPos = warrior.transform.position;

            foreach (Warrior ally in allies)
            {
                if (ally == null || ally == warrior) continue;

                Vector2 toMe = myPos - (Vector2)ally.transform.position;
                float dist   = toMe.magnitude;

                if (dist < radius && dist > 0.001f)
                    force += toMe.normalized * (1f - dist / radius);
            }

            return force;
        }

        /// <summary>
        /// Calculates a soft repulsion force from enemies that are closer than radius.
        /// Prevents warriors from walking fully through the enemy they're fighting.
        /// </summary>
        public Vector2 GetEnemySeparationForce(Warrior warrior, float radius)
        {
            List<Warrior> enemies = GetEnemyTeam(warrior.Team);
            Vector2 force = Vector2.zero;
            Vector2 myPos = warrior.transform.position;

            foreach (Warrior enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive()) continue;

                Vector2 toMe = myPos - (Vector2)enemy.transform.position;
                float dist   = toMe.magnitude;

                if (dist < radius && dist > 0.001f)
                    force += toMe.normalized * (1f - dist / radius);
            }

            return force;
        }

        // ── Private helpers ──────────────────────────────────────────────────────

        private static void RemoveFromTeam(Warrior warrior, List<Warrior> warriors, List<float> costs)
        {
            int idx = warriors.IndexOf(warrior);
            if (idx >= 0)
            {
                warriors.RemoveAt(idx);
                costs.RemoveAt(idx);
            }
        }

        private List<Warrior> GetTeam(Team team)      => team == Team.A ? teamA : teamB;
        private List<Warrior> GetEnemyTeam(Team team) => team == Team.A ? teamB : teamA;
        private List<float>   GetEnemyCosts(Team team) => team == Team.A ? teamBCost : teamACost;
    }
}
