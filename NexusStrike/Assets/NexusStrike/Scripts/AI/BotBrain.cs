using System.Collections.Generic;
using UnityEngine;

namespace NexusStrike
{
    /// <summary>
    /// AI controller: role-aware objective play (escort / contest the payload), target selection,
    /// imperfect aiming tuned by difficulty, healing for supports, strafing and ability usage.
    /// </summary>
    public class BotBrain : Brain
    {
        public Difficulty difficulty = Difficulty.Normal;

        readonly List<Vector3> path = new List<Vector3>();
        int pathIndex;
        float nextRepath;
        Vector3 goal;
        Vector3 pathGoal;

        Combatant target;
        float targetSince;
        Combatant healTarget;
        float nextPerceive;
        int visibleEnemies;

        float strafeDir = 1f;
        float nextStrafeSwitch;
        Vector3 lastPos;
        float stuckTimer;
        bool jumpNext;

        Vector3 aimJitter;
        float nextJitter;
        float aimError;
        float personalOffset;

        // tuning
        float reaction, turnSpeed, errorDeg, leadSkill, headChance;

        public Combatant Target { get { return target; } }
        public Combatant HealTarget { get { return healTarget; } }
        public float TargetDistance { get { return target != null ? Vector3.Distance(self.EyePos, target.ChestPos) : 999f; } }
        public int VisibleEnemies { get { return visibleEnemies; } }
        public float AimError { get { return aimError; } }
        public float DistanceToGoal { get { return Flat(goal - self.Feet).magnitude; } }
        public bool NearObjective
        {
            get
            {
                var pl = GameManager.I.payload;
                return pl != null && Vector3.Distance(self.Feet, pl.Position) < 14f;
            }
        }

        public void Configure(Difficulty d)
        {
            difficulty = d;
            switch (d)
            {
                case Difficulty.Easy:
                    reaction = 0.65f; turnSpeed = 160f; errorDeg = 5.5f; leadSkill = 0.4f; headChance = 0.05f; break;
                case Difficulty.Hard:
                    reaction = 0.18f; turnSpeed = 480f; errorDeg = 1.3f; leadSkill = 0.95f; headChance = 0.4f; break;
                default:
                    reaction = 0.35f; turnSpeed = 300f; errorDeg = 2.8f; leadSkill = 0.75f; headChance = 0.15f; break;
            }
            personalOffset = Random.Range(-1f, 1f);
        }

        public bool Chance(float perSecond) { return Random.value < perSecond * Time.deltaTime * 5f; }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        public int EnemiesWithin(Vector3 p, float r)
        {
            int n = 0;
            foreach (var c in Combatant.All)
                if (c.alive && c.team != self.team && Vector3.Distance(c.ChestPos, p) < r) n++;
            return n;
        }

        public int AlliesHurtNear(Vector3 p, float r, float frac)
        {
            int n = 0;
            foreach (var c in Combatant.All)
                if (c.alive && c.team == self.team && c.HealthFrac < frac && Vector3.Distance(c.Feet, p) < r) n++;
            return n;
        }

        public override HeroInput Think(float dt)
        {
            var input = default(HeroInput);
            var gm = GameManager.I;
            if (gm == null || !gm.CanAct(self)) return input;

            if (Time.time >= nextPerceive)
            {
                nextPerceive = Time.time + 0.2f;
                Perceive();
            }
            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + Random.Range(0.8f, 1.2f);
                goal = ChooseGoal();
                if (path.Count == 0 || (goal - pathGoal).sqrMagnitude > 4f || pathIndex >= path.Count)
                    Repath();
            }

            // ---------------- aim
            Vector3 aimPoint;
            bool attacking = false, healing = false;
            if (healTarget != null && (target == null || healTarget.HealthFrac < 0.6f || self.kit.HealRange > 20f && healTarget.HealthFrac < 0.85f))
            {
                aimPoint = healTarget.ChestPos;
                healing = true;
            }
            else if (target != null)
            {
                aimPoint = AimPointFor(target);
                attacking = true;
            }
            else
            {
                Vector3 look = path.Count > pathIndex ? path[pathIndex] : goal;
                look.y = self.EyePos.y;
                if (Flat(look - self.Feet).sqrMagnitude < 1f) look = self.EyePos + self.Forward;
                aimPoint = look;
            }
            TurnTowards(aimPoint, dt);
            Vector3 toAim = aimPoint - self.EyePos;
            aimError = Vector3.Angle(self.AimDir, toAim);

            if (attacking && Time.time - targetSince > reaction)
            {
                float dist = toAim.magnitude;
                float tolerance = Mathf.Max(2f, Mathf.Atan2(0.6f, dist) * Mathf.Rad2Deg * 1.3f);
                if (aimError < tolerance && dist < self.kit.EffectiveRange)
                {
                    if (self.kit.AttackWithSecondary) input.secondary = true;
                    else input.primary = true;
                }
            }
            if (healing)
            {
                float healTol = self.kit is LumenKit ? 8f : Mathf.Max(1.2f, Mathf.Atan2(0.45f, toAim.magnitude) * Mathf.Rad2Deg * 1.2f);
                if (aimError < healTol) input.primary = true;
            }
            if (!attacking && !healing && self.kit.maxAmmo > 0 && self.kit.ammo < self.kit.maxAmmo * 0.5f) input.reload = true;

            // ---------------- movement
            Vector3 wish = Vector3.zero;
            Vector3 waypoint = NextWaypoint();
            Vector3 toWp = Flat(waypoint - self.Feet);
            float goalDist = DistanceToGoal;
            if (goalDist > 1.5f && toWp.sqrMagnitude > 0.01f) wish = toWp.normalized;

            if (target != null)
            {
                if (Time.time >= nextStrafeSwitch)
                {
                    nextStrafeSwitch = Time.time + Random.Range(0.4f, 1.3f);
                    strafeDir = Random.value < 0.5f ? -1f : 1f;
                }
                Vector3 toT = Flat(target.Feet - self.Feet).normalized;
                Vector3 side = new Vector3(toT.z, 0, -toT.x) * strafeDir;
                float strafeAmt = self.def.role == HeroRole.Tank ? 0.4f : 0.85f;
                wish = wish * (goalDist > 6f ? 0.8f : 0.3f) + side * strafeAmt;
                float d = TargetDistance;
                if (d < self.kit.PreferredRange * 0.6f && self.def.role != HeroRole.Tank) wish -= toT * 0.6f;
                else if (d > self.kit.PreferredRange * 1.8f && goalDist < 8f) wish += toT * 0.4f;
                if (difficulty == Difficulty.Hard && Random.value < 0.6f * dt && self.motor.grounded) input.jump = true;
            }
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, self.yaw, 0f)) * wish;
            input.move = new Vector2(local.x, local.z);

            // stuck detection
            if (wish.sqrMagnitude > 0.2f && self.motor.grounded)
            {
                if (Flat(self.Feet - lastPos).magnitude < self.def.speed * 0.25f * dt) stuckTimer += dt;
                else stuckTimer = Mathf.Max(0f, stuckTimer - dt);
                if (stuckTimer > 0.6f)
                {
                    jumpNext = true;
                    stuckTimer = 0f;
                    nextRepath = Time.time;
                    strafeDir = -strafeDir;
                    path.Clear();
                }
            }
            lastPos = self.Feet;
            if (jumpNext && self.motor.grounded)
            {
                input.jump = true;
                jumpNext = false;
            }

            self.kit.BotAbilities(this, ref input);
            return input;
        }

        void Perceive()
        {
            Combatant best = null;
            float bestScore = float.MaxValue;
            visibleEnemies = 0;
            Vector3 eye = self.EyePos;
            foreach (var c in Combatant.All)
            {
                if (!c.alive || c.team == self.team) continue;
                Vector3 to = c.ChestPos - eye;
                float d = to.magnitude;
                if (d > 55f) continue;
                bool recentlyHurtBy = self.lastDamagedTime > Time.time - 1.5f && (self.lastDamageFrom - c.ChestPos).sqrMagnitude < 4f;
                if (d > 14f && !recentlyHurtBy && Vector3.Angle(self.Forward, Flat(to)) > 80f) continue;
                if (!CombatUtil.LineOfSight(eye, c.ChestPos) && !CombatUtil.LineOfSight(eye, c.HeadPos)) continue;
                visibleEnemies++;
                float score = d + c.HealthFrac * 12f;
                if (c == target) score -= 8f;
                if (c.def.role == HeroRole.Support) score -= 3f;
                if (score < bestScore) { bestScore = score; best = c; }
            }
            if (best != target)
            {
                target = best;
                targetSince = Time.time;
            }

            healTarget = null;
            float range = self.kit.HealRange;
            if (range > 0f)
            {
                float worst = 0.95f;
                foreach (var c in Combatant.All)
                {
                    if (!c.alive || c.team != self.team || c == self) continue;
                    if (Vector3.Distance(eye, c.ChestPos) > range) continue;
                    float f = c.HealthFrac;
                    var lumen = self.kit as LumenKit;
                    if (lumen != null && lumen.BeamTarget == c && f < 0.999f) f -= 0.15f; // stick with current beam target
                    if (f < worst && CombatUtil.LineOfSight(eye, c.ChestPos)) { worst = f; healTarget = c; }
                }
            }
        }

        Vector3 AimPointFor(Combatant t)
        {
            if (Time.time >= nextJitter)
            {
                nextJitter = Time.time + Random.Range(0.25f, 0.5f);
                float dist = Vector3.Distance(self.EyePos, t.ChestPos);
                aimJitter = Random.insideUnitSphere * dist * Mathf.Tan(errorDeg * Mathf.Deg2Rad);
                aimJitter.y *= 0.6f;
            }
            Vector3 p = Random.value < headChance * 0.1f || (difficulty == Difficulty.Hard && headChance > 0.3f && t.def.role != HeroRole.Tank)
                ? Vector3.Lerp(t.ChestPos, t.HeadPos, 0.7f) : t.ChestPos;
            float speed = self.kit.ProjectileSpeed;
            if (speed > 0f)
            {
                float time = Vector3.Distance(self.EyePos, p) / speed;
                p += t.motor.velocity * time * leadSkill;
                if (self.kit.AimAtFeet && t.motor.grounded) p.y = t.Feet.y + 0.25f;
            }
            return p + aimJitter;
        }

        void TurnTowards(Vector3 point, float dt)
        {
            Vector3 d = point - self.EyePos;
            if (d.sqrMagnitude < 0.0001f) return;
            float wantYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float wantPitch = -Mathf.Atan2(d.y, Flat(d).magnitude) * Mathf.Rad2Deg;
            float yawDiff = Mathf.Abs(Mathf.DeltaAngle(self.yaw, wantYaw));
            float rate = turnSpeed * Mathf.Clamp(yawDiff / 25f, 0.35f, 1f);
            self.yaw = Mathf.MoveTowardsAngle(self.yaw, wantYaw, rate * dt);
            self.pitch = Mathf.MoveTowards(self.pitch, Mathf.Clamp(wantPitch, -80f, 80f), turnSpeed * 0.7f * dt);
        }

        Vector3 ChooseGoal()
        {
            var gm = GameManager.I;
            var pl = gm.payload;

            if (self.HealthFrac < 0.35f && self.def.role != HeroRole.Tank)
            {
                HealthPack best = null;
                float bd = 30f;
                foreach (var hp in gm.map.healthPacks)
                {
                    if (!hp.available) continue;
                    float d = Vector3.Distance(hp.transform.position, self.Feet);
                    if (d < bd) { bd = d; best = hp; }
                }
                if (best != null) return best.transform.position;
            }

            // supports stick near their heal target
            if (healTarget != null && self.def.role == HeroRole.Support)
            {
                Vector3 away = Flat(self.Feet - healTarget.Feet);
                float keep = self.kit.HealRange > 20f ? 12f : 6f;
                if (away.sqrMagnitude < 0.01f) away = -healTarget.Forward;
                return healTarget.Feet + away.normalized * keep;
            }

            Vector3 fwd = pl.Forward;
            Vector3 right = new Vector3(fwd.z, 0, -fwd.x);
            float lateral = (self.slot % 2 == 0 ? 1f : -1f) * (2f + 2.5f * Mathf.Abs(personalOffset));
            Vector3 anchor;
            if (self.team == Team.Attack)
            {
                switch (self.def.role)
                {
                    case HeroRole.Tank: anchor = pl.Position + fwd * 4f; break;
                    case HeroRole.Damage: anchor = pl.Position + fwd * 2f + right * lateral * 0.6f; break;
                    default: anchor = pl.Position - fwd * 3f + right * lateral; break;
                }
            }
            else
            {
                bool mustContest = pl.attackersOn > 0 && pl.defendersOn == 0 || gm.TimeLeft < 20f && pl.attackersOn > 0;
                if (mustContest || self.def.role == HeroRole.Tank && pl.attackersOn > 0)
                    anchor = pl.Position + right * lateral * 0.5f;
                else
                {
                    float ahead = Mathf.Min(pl.total, pl.distance + 12f + personalOffset * 3f);
                    Vector3 hold = pl.Evaluate(ahead);
                    Vector3 hf = pl.Direction(ahead);
                    Vector3 hr = new Vector3(hf.z, 0, -hf.x);
                    switch (self.def.role)
                    {
                        case HeroRole.Tank: anchor = hold - hf * 2f; break;
                        case HeroRole.Damage: anchor = hold + hr * lateral * 2f; break;
                        default: anchor = hold + hf * 4f + hr * lateral; break;
                    }
                }
            }
            return anchor;
        }

        void Repath()
        {
            pathGoal = goal;
            pathIndex = 0;
            var nav = GameManager.I.nav;
            if (nav == null || !nav.FindPath(self.Feet, goal, path))
            {
                path.Clear();
                path.Add(goal);
            }
        }

        Vector3 NextWaypoint()
        {
            if (path.Count == 0) return goal;
            while (pathIndex < path.Count - 1)
            {
                Vector3 d = Flat(path[pathIndex] - self.Feet);
                if (d.magnitude < 1.4f) { pathIndex++; continue; }
                // skip ahead when the next waypoint is directly walkable
                if (pathIndex + 1 < path.Count && Mathf.Abs(path[pathIndex + 1].y - self.Feet.y) < 0.4f && Mathf.Abs(path[pathIndex].y - self.Feet.y) < 0.4f)
                {
                    Vector3 a = self.Feet + Vector3.up * 1.0f;
                    Vector3 b = path[pathIndex + 1] + Vector3.up * 1.0f;
                    if (!Physics.SphereCast(a, 0.35f, (b - a).normalized, out RaycastHit hit, (b - a).magnitude, Layers.World, QueryTriggerInteraction.Ignore))
                    {
                        pathIndex++;
                        continue;
                    }
                }
                break;
            }
            return path[Mathf.Min(pathIndex, path.Count - 1)];
        }

        public void ResetBrain()
        {
            path.Clear();
            target = null;
            healTarget = null;
            nextRepath = 0f;
        }
    }
}
