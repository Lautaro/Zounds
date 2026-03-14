using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zounds;
namespace ZoundsDemo
{
    public enum Team { A, B }

    /// <summary>
    /// A warrior that moves toward the best enemy target in full 2D, attacks it,
    /// and plays Zound events at every meaningful moment.
    ///
    /// Desync: every timer (attack, target search, stun recovery, decision pause)
    /// is initialised or jittered with per-instance randomness so that groups of
    /// warriors never act in perfect lock-step.
    ///
    /// All animations drive a "Visual" child transform so that transform.position
    /// stays clean for distance / targeting math.
    /// </summary>
    public class Warrior : MonoBehaviour
    {
        // ── Baked stats ──────────────────────────────────────────────────────────
        public Team Team { get; private set; }

        private float moveSpeed;
        private float maxHealth;
        private float damage;
        private float attackCooldown;
        private float stunDuration;
        private float attackRange;
        private float separationRadius;
        private float separationStrength;
        private float spawnCost;

        private string zoundOnSpawn;
        private string zoundOnAttack;
        private string zoundOnHit;
        private string zoundOnDeath;

        // ── Ranged ───────────────────────────────────────────────────────────────
        private bool isRanged;
        private LineRenderer shotBeam;

        // ── Juggernaut ───────────────────────────────────────────────────────────
        private bool isJuggernaut;
        private float pushbackDistance;
        private float stunImmunityTimer;
        private float lastAppliedStunDuration;

        // Strike lines: one LineRenderer per simultaneous target, pooled and reused.
        private readonly List<LineRenderer> strikeLines = new List<LineRenderer>();

        // ── Lightweight ──────────────────────────────────────────────────────────
        private bool isLightweight;

        // ── Runtime state ────────────────────────────────────────────────────────
        private float currentHealth;
        private float attackTimer;
        private float stunTimer;
        private Warrior currentTarget;
        private SpriteRenderer spriteRenderer;
        private bool isDead;
        private bool isMoving;

        // Coroutine handle — hit wobble must never cancel the death routine
        private Coroutine hitWobbleCoroutine;
        private Coroutine stunVisualCoroutine;

        // Visual child: all offset/rotation animations live here
        private Transform visualRoot;

        // ── Desync timing ────────────────────────────────────────────────────────
        // A small random pause inserted before the warrior commits to any new action
        // (picking a target, resuming after stun, resuming after kill).
        // This breaks the visual lockstep when many warriors share the same state.
        private float decisionPauseTimer;

        // How long between target re-evaluations (jittered per search)
        private float searchTimer;
        private const float SearchIntervalBase   = 0.4f;
        private const float SearchIntervalJitter = 0.25f;

        // Max random pause before acting on a new decision, in seconds
        private const float DecisionPauseMax = 0.25f;

        // Random extra time added to stun recovery before the warrior actually moves again
        private const float StunRecoveryJitterMax = 0.15f;
        private float stunRecoveryExtra;

        // ── Bounce state ─────────────────────────────────────────────────────────
        private float bounceTimer;
        private float bounceFrequency;
        private const float BounceAmplitude = 0.06f;

        // ── Health bar ───────────────────────────────────────────────────────────
        private Transform healthBarFill;
        private SpriteRenderer healthBarFillRenderer;
        private static readonly Color HealthColorFull = new Color(0.15f, 0.85f, 0.25f);
        private static readonly Color HealthColorMid  = new Color(0.95f, 0.75f, 0.1f);
        private static readonly Color HealthColorLow  = new Color(0.9f,  0.15f, 0.1f);

        // ── Initialization ───────────────────────────────────────────────────────

        /// <summary>Configures this warrior from a WarriorData asset with per-instance stat deviation applied.</summary>
        public void Initialize(WarriorData data, Team team, float spawnY)
        {
            Team = team;

            // Visual child — all animations drive this, not the root transform
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(transform, false);
            visualRoot = visual.transform;

            // SpriteRenderer lives on the visual child only
            SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
            if (rootRenderer != null)
                DestroyImmediate(rootRenderer);
            spriteRenderer = visual.AddComponent<SpriteRenderer>();

            float Deviate(float value) =>
                value * (1f + Random.Range(-data.statDeviation, data.statDeviation));

            moveSpeed          = Deviate(data.moveSpeed);
            maxHealth          = Deviate(data.maxHealth);
            currentHealth      = maxHealth;
            damage             = Deviate(data.damage);
            attackCooldown     = Deviate(data.attackCooldown);
            stunDuration       = data.stunDuration;
            attackRange        = data.attackRange;
            separationRadius   = data.separationRadius;
            separationStrength = data.separationStrength;
            spawnCost          = data.spawnCost;

            zoundOnSpawn         = data.zoundOnSpawn;
            zoundOnAttack        = data.zoundOnAttack;
            zoundOnHit           = data.zoundOnHit;
            zoundOnDeath         = data.zoundOnDeath;

            isRanged = data.isRanged;
            if (isRanged)
                BuildShotBeam();

            isJuggernaut      = data.isJuggernaut;
            pushbackDistance  = data.pushbackDistance;
            isLightweight     = data.isLightweight;

            spriteRenderer.sprite = data.sprite;
            spriteRenderer.color  = data.tint;

            if (team == Team.B)
                transform.localScale = new Vector3(-1f, 1f, 1f);

            // ── Desync initial timers ─────────────────────────────────────────────
            bounceFrequency    = Random.Range(3.5f, 5.5f);
            bounceTimer        = Random.Range(0f, Mathf.PI * 2f);
            searchTimer        = Random.Range(0f, SearchIntervalBase + SearchIntervalJitter);
            attackTimer        = 0f;
            decisionPauseTimer = Random.Range(0f, DecisionPauseMax);

            BuildHealthBar();

            // Register AFTER team is set — OnEnable fires before Initialize
            if (BattleManager.Instance != null)
                BattleManager.Instance.Register(this, spawnCost);

          ZoundEngine.PlayZound(zoundOnSpawn);
        }

        // ── Unity lifecycle ──────────────────────────────────────────────────────

        private void OnEnable()
        {
            // Intentionally empty — registration happens inside Initialize()
        }

        private void OnDisable()
        {
            if (!isDead && BattleManager.Instance != null)
                BattleManager.Instance.Unregister(this);
        }

        private void Update()
        {
            if (isDead) return;

            // ── Decision pause (desync gate) ──────────────────────────────────────
            if (decisionPauseTimer > 0f)
            {
                decisionPauseTimer -= Time.deltaTime;
                isMoving = false;
                UpdateBounce();
                return;
            }

            // ── Stun ─────────────────────────────────────────────────────────────
            if (stunTimer > 0f)
            {
                stunTimer -= Time.deltaTime;
                isMoving = false;

                if (stunTimer <= 0f)
                {
                    if (isJuggernaut)
                        stunImmunityTimer = lastAppliedStunDuration;
                    else
                        decisionPauseTimer = stunRecoveryExtra;
                }

                UpdateBounce();

                // Juggernaut: cooldown keeps ticking during stun so they can
                // attack the instant the stun expires. TryAttack is called here
                // to fire the pending attack without leaving the stun branch.
                if (isJuggernaut)
                    TryAttack();

                return;
            }

            // ── Stun immunity (juggernaut only) ───────────────────────────────────
            if (stunImmunityTimer > 0f)
                stunImmunityTimer -= Time.deltaTime;

            // ── Target acquisition ────────────────────────────────────────────────
            searchTimer -= Time.deltaTime;
            if (searchTimer <= 0f || !TargetIsValid(currentTarget))
            {
                Warrior newTarget = BattleManager.Instance != null
                    ? BattleManager.Instance.GetBestEnemy(this)
                    : null;

                // Just lost a target — pause briefly before chasing the next one
                if (!TargetIsValid(currentTarget) && TargetIsValid(newTarget) && currentTarget != null)
                    decisionPauseTimer = Random.Range(0f, DecisionPauseMax);

                currentTarget = newTarget;
                searchTimer   = SearchIntervalBase + Random.Range(-SearchIntervalJitter, SearchIntervalJitter);
            }

            if (!TargetIsValid(currentTarget))
            {
                isMoving = false;
                UpdateBounce();
                return;
            }

            // ── Opportunistic range check ─────────────────────────────────────────
            // Every frame, check if any enemy is within attack range.
            // Juggernauts bypass the single-target funnel entirely — TryAttack
            // will query all enemies in range itself via PerformJuggernautAttack.
            if (BattleManager.Instance != null)
            {
                if (isJuggernaut)
                {
                    float nearest = BattleManager.Instance.GetNearestEnemyDistance(this);
                    if (nearest <= attackRange)
                    {
                        isMoving = false;
                        ApplyIdleSeparation();
                        TryAttack();
                        UpdateBounce();
                        return;
                    }
                }
                else
                {
                    Warrior inRange = BattleManager.Instance.GetEnemyInRange(this, attackRange);
                    if (inRange != null)
                    {
                        currentTarget = inRange;
                        isMoving      = false;
                        ApplyIdleSeparation();
                        TryAttack();
                        UpdateBounce();
                        return;
                    }
                }
            }

            // ── Advance toward assigned target ────────────────────────────────────
            float distToTarget = Vector2.Distance(transform.position, currentTarget.transform.position);

            if (distToTarget > attackRange)
            {
                MoveToward(currentTarget.transform.position);
                isMoving = true;
            }
            else
            {
                isMoving = false;
                ApplyIdleSeparation();
                TryAttack();
            }

            UpdateBounce();
        }

        // ── Movement ─────────────────────────────────────────────────────────────

        private void MoveToward(Vector2 targetPosition)
        {
            Vector2 toTarget = ((Vector3)targetPosition - transform.position).normalized;

            // Separation from allies — prevents friendly pile-ups while advancing
            Vector2 allySep = BattleManager.Instance != null
                ? BattleManager.Instance.GetSeparationForce(this, separationRadius)
                : Vector2.zero;

            // Soft repulsion from enemies that are already too close (inside half attack range).
            // Stops warriors from walking into each other when the navmesh is just movement code.
            Vector2 enemySep = BattleManager.Instance != null
                ? BattleManager.Instance.GetEnemySeparationForce(this, attackRange * 0.6f)
                : Vector2.zero;

            Vector2 desired = toTarget + allySep * separationStrength + enemySep * separationStrength;
            if (desired.sqrMagnitude < 0.001f)
                desired = toTarget;

            transform.position += (Vector3)(desired.normalized * moveSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Applies a gentle ally-separation nudge even while standing still.
        /// Warriors that pile up during combat slowly drift apart without visibly sliding.
        /// </summary>
        private void ApplyIdleSeparation()
        {
            if (BattleManager.Instance == null) return;

            Vector2 sep = BattleManager.Instance.GetSeparationForce(this, separationRadius * 0.7f);
            if (sep.sqrMagnitude < 0.001f) return;

            // Apply at a fraction of move speed so it's a gentle drift, not a sprint
            const float IdleSepScale = 0.25f;
            transform.position += (Vector3)(sep.normalized * moveSpeed * IdleSepScale * Time.deltaTime);
        }

        private void UpdateBounce()
        {
            if (visualRoot == null || isDead) return;

            // ── Stun tilt ─────────────────────────────────────────────────────────
            // Snap immediately to 45° on stun, lerp smoothly back to upright on recovery.
            // Snapping in guarantees the tilt is visible for the full stun duration even
            // when stunDuration is very short (0.2–0.25 s).
            const float StunTiltAngle = 45f;
            const float TiltOutSpeed  = 8f;

            float currentZ = visualRoot.localEulerAngles.z;
            if (currentZ > 180f) currentZ -= 360f;

            float newZ;
            if (stunTimer > 0f)
            {
                newZ = StunTiltAngle;   // hold exactly at 45° for the entire stun
            }
            else
            {
                newZ = Mathf.LerpAngle(currentZ, 0f, Time.deltaTime * TiltOutSpeed);
            }
            visualRoot.localRotation = Quaternion.Euler(0f, 0f, newZ);

            // ── Vertical bounce ───────────────────────────────────────────────────
            if (isMoving)
            {
                bounceTimer += Time.deltaTime * bounceFrequency;
                float offsetY = Mathf.Abs(Mathf.Sin(bounceTimer)) * BounceAmplitude;
                Vector3 local = visualRoot.localPosition;
                visualRoot.localPosition = new Vector3(local.x, offsetY, local.z);
            }
            else
            {
                Vector3 local  = visualRoot.localPosition;
                float settledY = Mathf.Lerp(local.y, 0f, Time.deltaTime * 12f);
                visualRoot.localPosition = new Vector3(local.x, settledY, local.z);
            }
        }

        // ── Attack & lunge ───────────────────────────────────────────────────────

        private void TryAttack()
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer > 0f) return;

            // Jitter cooldown so same-type warriors drift out of sync over time
            attackTimer = attackCooldown * Random.Range(0.9f, 1.1f);

            if (isJuggernaut)
            {
                PerformJuggernautAttack();
                return;
            }

            if (!TargetIsValid(currentTarget)) return;

           ZoundEngine.PlayZound(zoundOnAttack);

            if (isRanged)
            {
                StartCoroutine(ShotBeamRoutine(currentTarget.transform.position));
            }
            else
            {
                Vector2 targetPos = currentTarget.transform.position;
                StartCoroutine(LungeRoutine(targetPos));
            }

            currentTarget.TakeDamage(damage, transform.position, 0f, isLightweight ? 0f : stunDuration);
        }

        private void PerformJuggernautAttack()
        {
            if (BattleManager.Instance == null) return;

            // Trigger requires a target within attackRange. Once firing, the sweep
            // covers double that distance with damage fall-off in the outer ring.
            float innerRange = attackRange;
            float outerRange = attackRange * 2f;

            List<Warrior> targets = BattleManager.Instance.GetAllEnemiesInRange(this, outerRange);

            if (targets.Count == 0) return;

           ZoundEngine.PlayZound(zoundOnAttack);

            // Damage applied first — visuals must never block or abort the hit loop.
            foreach (Warrior target in targets)
            {
                float dist = Vector2.Distance(transform.position, target.transform.position);

                // Inner ring: full damage. Outer ring: linear fall-off from 1.0 to 0.5.
                float damageMult = dist <= innerRange
                    ? 1f
                    : Mathf.Lerp(1f, 0.5f, (dist - innerRange) / innerRange);

                target.TakeDamage(damage * damageMult, transform.position, pushbackDistance, isLightweight ? 0f : stunDuration);
            }

            // Grow the strike-line pool if this attack hits more targets than before
            while (strikeLines.Count < targets.Count)
                strikeLines.Add(BuildStrikeLine());

            if (isRanged)
            {
                for (int i = 0; i < targets.Count; i++)
                    StartCoroutine(StrikeLineRoutine(strikeLines[i], targets[i].transform.position));
            }
            else
            {
                // Lunge toward the nearest target for the visual
                Warrior nearest     = targets[0];
                float   nearestDist = float.MaxValue;
                foreach (Warrior target in targets)
                {
                    float d = Vector2.Distance(transform.position, target.transform.position);
                    if (d < nearestDist) { nearestDist = d; nearest = target; }
                }
                StartCoroutine(LungeRoutine(nearest.transform.position));

                for (int i = 0; i < targets.Count; i++)
                    StartCoroutine(StrikeLineRoutine(strikeLines[i], targets[i].transform.position));
            }
        }

        private LineRenderer BuildStrikeLine()
        {
            var child = new GameObject("StrikeLine");
            child.transform.SetParent(transform, false);
            var lr = child.AddComponent<LineRenderer>();
            lr.positionCount   = 2;
            lr.useWorldSpace   = true;
            lr.widthMultiplier = 0.06f;
            lr.material        = GetLineMaterial();
            lr.startColor      = new Color(0.8f, 0.0f, 1.0f, 1.0f);   // purple
            lr.endColor        = new Color(1.0f, 0.0f, 0.6f, 0.8f);   // magenta
            lr.enabled         = false;
            return lr;
        }

        private IEnumerator StrikeLineRoutine(LineRenderer line, Vector2 targetPosition)
        {
            line.SetPosition(0, transform.position);
            line.SetPosition(1, targetPosition);
            line.enabled = true;

            yield return new WaitForSeconds(StrikeLineVisibleDuration);

            line.enabled = false;
        }

        private const float PushbackDuration = 0.12f;

        // Slides transform.position to destination over a short fixed duration.
        // Using an eased curve so the shove feels impactful at the start and
        // settles quickly rather than snapping.
        private IEnumerator PushbackRoutine(Vector2 destination)
        {
            Vector2 origin  = transform.position;
            float   elapsed = 0f;

            while (elapsed < PushbackDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / PushbackDuration);
                // Ease-out: fast at start, decelerates to rest
                float eased = 1f - (1f - t) * (1f - t);
                transform.position = Vector2.Lerp(origin, destination, eased);
                yield return null;
            }

            transform.position = destination;
        }

        private IEnumerator LungeRoutine(Vector2 targetPosition)
        {
            if (visualRoot == null) yield break;

            // worldToLocalMatrix.MultiplyVector correctly accounts for scale (unlike
            // InverseTransformDirection which only handles rotation). Team B has
            // root scale.x = -1, which flips the local X axis — without this the
            // lunge direction is reversed for Team B.
            Vector3 worldDir = ((Vector3)targetPosition - transform.position).normalized;
            Vector3 localDir = transform.worldToLocalMatrix.MultiplyVector(worldDir);

            // Lunge drives only X. UpdateBounce owns Y every frame, so sharing Y
            // causes accumulation drift — keeping axes separate avoids the conflict.
            float lungeX       = localDir.x * attackRange * 0.5f;
            float lungeDuration = Random.Range(0.05f, 0.08f);
            float snapDuration  = Random.Range(0.10f, 0.16f);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / lungeDuration;
                float s     = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                Vector3 local = visualRoot.localPosition;
                visualRoot.localPosition = new Vector3(lungeX * s, local.y, local.z);
                yield return null;
            }

            t = 0f;
            float startX = visualRoot.localPosition.x;
            while (t < 1f)
            {
                t += Time.deltaTime / snapDuration;
                Vector3 local = visualRoot.localPosition;
                visualRoot.localPosition = new Vector3(Mathf.Lerp(startX, 0f, t), local.y, local.z);
                yield return null;
            }

            Vector3 rest = visualRoot.localPosition;
            visualRoot.localPosition = new Vector3(0f, rest.y, rest.z);
        }

        // ── Ranged beam ──────────────────────────────────────────────────────────

        // Cached at first use so Shader.Find only runs once
        private static Material lineMaterial;

        private static Material GetLineMaterial()
        {
            if (lineMaterial != null) return lineMaterial;
            Shader shader = Shader.Find(LineShaderName);
            if (shader == null)
            {
                Debug.LogError($"[Warrior] Shader '{LineShaderName}' not found. Check it is included in Always Included Shaders.");
                shader = Shader.Find("Sprites/Default");
            }
            lineMaterial = new Material(shader);
            return lineMaterial;
        }

        private void BuildShotBeam()
        {
            var child = new GameObject("ShotBeam");
            child.transform.SetParent(transform, false);
            shotBeam = child.AddComponent<LineRenderer>();
            shotBeam.positionCount   = 2;
            shotBeam.useWorldSpace   = true;
            shotBeam.widthMultiplier = 0.04f;
            shotBeam.material        = GetLineMaterial();
            shotBeam.startColor      = new Color(1f, 0.85f, 0.1f, 1f);
            shotBeam.endColor        = new Color(1f, 0.4f,  0.05f, 0.6f);
            shotBeam.enabled         = false;
        }

        private const string LineShaderName          = "Universal Render Pipeline/Unlit";
        private const float  ShotBeamMinVisibleDuration = 0.3f;
        private const float  StrikeLineVisibleDuration  = 0.3f;

        private IEnumerator ShotBeamRoutine(Vector2 targetPosition)
        {
            if (shotBeam == null) yield break;

            shotBeam.SetPosition(0, transform.position);
            shotBeam.SetPosition(1, targetPosition);
            shotBeam.enabled = true;

            yield return new WaitForSeconds(ShotBeamMinVisibleDuration);

            shotBeam.enabled = false;
        }

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>Applies damage. Triggers hit/death Zounds and animations. Optionally pushes this warrior away from the attacker.</summary>
        public void TakeDamage(float amount, Vector2 attackerPosition, float pushback, float inflictedStunDuration)
        {
            if (isDead) return;

            currentHealth -= amount;

            // Use the stun duration the *attacker* inflicts. Zero means no stun.
            bool canBeStunned = inflictedStunDuration > 0f && stunImmunityTimer <= 0f;
            if (canBeStunned)
            {
                bool applyStun = !isJuggernaut || stunTimer <= 0f;
                if (applyStun)
                {
                    stunTimer                = inflictedStunDuration * Random.Range(0.85f, 1.15f);
                    lastAppliedStunDuration  = stunTimer;
                    stunRecoveryExtra        = Random.Range(0f, StunRecoveryJitterMax);
                    StartStunVisual(stunTimer);
                }
            }

            if (pushback > 0.001f)
            {
                Vector2 awayDir = ((Vector2)transform.position - attackerPosition);
                if (awayDir.sqrMagnitude < 0.0001f)
                    awayDir = Random.insideUnitCircle.normalized;
                else
                    awayDir = awayDir.normalized;

                Vector2 destination = (Vector2)transform.position + awayDir * pushback;
                StartCoroutine(PushbackRoutine(destination));
            }

            UpdateHealthBar();

            if (currentHealth <= 0f)
                Die();
            else
            {
               ZoundEngine.PlayZound(zoundOnHit);
                PlayHitWobble();
            }
        }

        /// <summary>Returns true while the warrior is alive and not yet destroyed.</summary>
        public bool IsAlive() => !isDead;

        // ── Death ────────────────────────────────────────────────────────────────

        private void Die()
        {
            isDead = true;
           ZoundEngine.PlayZound(zoundOnDeath);

            if (BattleManager.Instance != null)
                BattleManager.Instance.Unregister(this);

            if (hitWobbleCoroutine != null)
            {
                StopCoroutine(hitWobbleCoroutine);
                hitWobbleCoroutine = null;

            if (stunVisualCoroutine != null)
            {
                StopCoroutine(stunVisualCoroutine);
                stunVisualCoroutine = null;
            }

            }

            StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            if (visualRoot == null) yield break;

            if (healthBarFill != null)
                healthBarFill.parent.gameObject.SetActive(false);

            // Reset any in-progress hit wobble state so the spin starts clean
            visualRoot.localPosition = new Vector3(0f, 0f, 0f);
            visualRoot.localScale    = Vector3.one;

            Color startColor      = spriteRenderer.color;
            float spinRevolutions = Random.Range(1.5f, 2.5f);
            float spinDuration    = Random.Range(0.6f, 0.85f);
            const float FadeDuration = 0.35f;

            float elapsed = 0f;
            while (elapsed < spinDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / spinDuration;
                float angle    = progress * spinRevolutions * 360f;
                float scale    = 1f + Mathf.Sin(progress * Mathf.PI) * 0.28f;

                visualRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
                visualRoot.localScale    = new Vector3(scale, scale, 1f);
                yield return null;
            }

            elapsed = 0f;
            visualRoot.localRotation = Quaternion.identity;
            while (elapsed < FadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / FadeDuration);
                spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }

            Destroy(gameObject);
        }

        // ── Hit wobble ───────────────────────────────────────────────────────────

        private void PlayHitWobble()
        {
            if (hitWobbleCoroutine != null)
                StopCoroutine(hitWobbleCoroutine);
            hitWobbleCoroutine = StartCoroutine(HitWobbleRoutine());
        }

        private IEnumerator HitWobbleRoutine()
        {
            if (visualRoot == null) yield break;

            float duration  = Random.Range(0.18f, 0.26f);
            float frequency = Random.Range(26f, 34f);
            float amplitude = Random.Range(0.07f, 0.11f);
            float elapsed   = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t      = elapsed / duration;
                float shake  = Mathf.Sin(elapsed * frequency) * amplitude * (1f - t);
                float scaleX = 1f + Mathf.Abs(shake) * 1.4f;
                float scaleY = 1f - Mathf.Abs(shake) * 0.7f;

                Vector3 local = visualRoot.localPosition;
                visualRoot.localPosition = new Vector3(shake, local.y, local.z);
                visualRoot.localScale    = new Vector3(scaleX, scaleY, 1f);
                yield return null;
            }

            Vector3 end = visualRoot.localPosition;
            visualRoot.localPosition = new Vector3(0f, end.y, end.z);
            visualRoot.localScale    = Vector3.one;
            hitWobbleCoroutine = null;
        }

        private void StartStunVisual(float duration)
        {
            // Cancel any in-progress wobble so it doesn't fight the tilt
            if (hitWobbleCoroutine != null)
            {
                StopCoroutine(hitWobbleCoroutine);
                hitWobbleCoroutine = null;
            }

            if (stunVisualCoroutine != null)
                StopCoroutine(stunVisualCoroutine);

            stunVisualCoroutine = StartCoroutine(StunVisualRoutine(duration));
        }

        private const float StunTiltAngle = 55f;

        private IEnumerator StunVisualRoutine(float duration)
        {
            if (visualRoot == null) yield break;

            const float SnapInDuration  = 0.06f;
            const float SnapOutDuration = 0.1f;

            float tiltDir = Random.value > 0.5f ? 1f : -1f;
            float targetAngle = StunTiltAngle * tiltDir;

            // Snap quickly into the tilt
            float elapsed = 0f;
            while (elapsed < SnapInDuration)
            {
                elapsed += Time.deltaTime;
                float angle = Mathf.Lerp(0f, targetAngle, elapsed / SnapInDuration);
                visualRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }

            visualRoot.localRotation = Quaternion.Euler(0f, 0f, targetAngle);

            // Hold the tilt for the remainder of the stun duration
            float holdTime = duration - SnapInDuration - SnapOutDuration;
            if (holdTime > 0f)
                yield return new WaitForSeconds(holdTime);

            // Snap back upright
            elapsed = 0f;
            Quaternion tiltedRot = visualRoot.localRotation;
            while (elapsed < SnapOutDuration)
            {
                elapsed += Time.deltaTime;
                visualRoot.localRotation = Quaternion.Lerp(tiltedRot, Quaternion.identity, elapsed / SnapOutDuration);
                yield return null;
            }

            visualRoot.localRotation = Quaternion.identity;
            stunVisualCoroutine = null;
        }

        // ── Health bar ───────────────────────────────────────────────────────────

        private void BuildHealthBar()
        {
            GameObject barRoot = new GameObject("HealthBar");
            barRoot.transform.SetParent(transform, false);
            barRoot.transform.localPosition = new Vector3(0f, 0.62f, 0f);

            GameObject bg          = new GameObject("BG");
            bg.transform.SetParent(barRoot.transform, false);
            SpriteRenderer bgRend  = bg.AddComponent<SpriteRenderer>();
            bgRend.sprite          = CreateRectSprite();
            bgRend.color           = new Color(0.1f, 0.1f, 0.1f, 0.75f);
            bgRend.sortingOrder    = 1;
            bg.transform.localScale = new Vector3(0.5f, 0.065f, 1f);

            GameObject fill            = new GameObject("Fill");
            fill.transform.SetParent(barRoot.transform, false);
            healthBarFillRenderer      = fill.AddComponent<SpriteRenderer>();
            healthBarFillRenderer.sprite       = CreateRectSprite();
            healthBarFillRenderer.color        = HealthColorFull;
            healthBarFillRenderer.sortingOrder = 2;
            fill.transform.localScale  = new Vector3(0.5f, 0.065f, 1f);
            fill.transform.localPosition = Vector3.zero;
            healthBarFill = fill.transform;
        }

        private static Sprite CreateRectSprite()
        {
            Texture2D tex    = new Texture2D(4, 4);
            Color[]   pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }

        private void UpdateHealthBar()
        {
            if (healthBarFill == null) return;

            float fraction  = Mathf.Clamp01(currentHealth / maxHealth);
            float fullWidth = 0.5f;
            healthBarFill.localScale    = new Vector3(fullWidth * fraction, 0.065f, 1f);
            healthBarFill.localPosition = new Vector3(-fullWidth * (1f - fraction) * 0.5f, 0f, 0f);

            Color barColor = fraction > 0.5f
                ? Color.Lerp(HealthColorMid, HealthColorFull, (fraction - 0.5f) * 2f)
                : Color.Lerp(HealthColorLow, HealthColorMid,  fraction * 2f);
            healthBarFillRenderer.color = barColor;
        }

        // ── Null-safety ──────────────────────────────────────────────────────────

        /// <summary>
        /// Unity-safe validity check. C# '?.' does not use Unity's overloaded '=='
        /// so destroyed MonoBehaviours would pass through. Always use this instead.
        /// </summary>
        private static bool TargetIsValid(Warrior target)
        {
            return target != null && target.IsAlive();
        }
    }
}
