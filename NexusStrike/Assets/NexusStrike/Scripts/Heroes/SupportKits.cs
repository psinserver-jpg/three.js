using UnityEngine;

namespace NexusStrike
{
    /// <summary>LUMEN — tether beam healer.</summary>
    public class LumenKit : HeroKit
    {
        Combatant beamTarget;
        LineRenderer beam;
        Material beamMat;
        float nextHealSound;
        const float LockRange = 15f;
        const float KeepRange = 20f;

        public Combatant BeamTarget { get { return beamTarget; } }

        protected override void Setup()
        {
            primaryName = "Mend Beam";
            maxAmmo = 20;
            reloadDuration = 1.4f;
            ab1 = new Ability("SHIFT", "Phase Glide", 7f);
            ab2 = new Ability("E", "Halo Field", 12f);
            ultName = "Sanctuary";
            crosshair = CrosshairStyle.Circle;
            beam = Fx.NewLine(new Color(1f, 0.9f, 0.45f, 0.85f), 0.07f, out beamMat);
            beam.gameObject.name = "MendBeam";
            beam.gameObject.SetActive(false);
        }

        protected override void BuildWeapon(Transform root)
        {
            ModelUtil.Part(PrimitiveType.Cylinder, root, new Vector3(0, 0, 0.25f), new Vector3(0.05f, 0.25f, 0.05f), new Color(0.95f, 0.92f, 0.8f), false, new Vector3(90, 0, 0));
            ModelUtil.Part(PrimitiveType.Sphere, root, new Vector3(0, 0, 0.52f), new Vector3(0.12f, 0.12f, 0.12f), new Color(1f, 0.9f, 0.5f), true);
            ModelUtil.Part(PrimitiveType.Cube, root, new Vector3(0, 0, 0.52f), new Vector3(0.2f, 0.03f, 0.03f), new Color(1f, 0.95f, 0.7f), true);
            ModelUtil.Part(PrimitiveType.Cube, root, new Vector3(0, 0.05f, 0.1f), new Vector3(0.06f, 0.02f, 0.1f), TeamColors.Relative(self.team), true);
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root, false);
            muzzle.localPosition = new Vector3(0, 0, 0.55f);
        }

        Combatant FindBeamCandidate()
        {
            Combatant best = null;
            float bestAngle = 14f;
            foreach (var c in Combatant.All)
            {
                if (c == self || !c.alive || c.team != self.team) continue;
                Vector3 to = c.ChestPos - self.EyePos;
                if (to.magnitude > LockRange) continue;
                float a = Vector3.Angle(self.AimDir, to);
                if (a < bestAngle && CombatUtil.LineOfSight(self.EyePos, c.ChestPos))
                {
                    bestAngle = a;
                    best = c;
                }
            }
            return best;
        }

        protected override void HandleFire(HeroInput input, float dt)
        {
            if (input.primary)
            {
                if (beamTarget == null) beamTarget = FindBeamCandidate();
                if (beamTarget != null)
                {
                    if (!beamTarget.alive || Vector3.Distance(self.EyePos, beamTarget.ChestPos) > KeepRange ||
                        !CombatUtil.LineOfSight(self.EyePos, beamTarget.ChestPos))
                    {
                        beamTarget = null;
                    }
                    else
                    {
                        beamTarget.ApplyHeal(self, 60f * dt);
                        if (Time.time > nextHealSound && beamTarget.Total < beamTarget.MaxTotal)
                        {
                            nextHealSound = Time.time + 0.6f;
                            Sfx.PlayFor(self, "heal", 0.25f);
                        }
                    }
                }
            }
            else beamTarget = null;

            if (!input.primary && input.secondary && CanFire && ConsumeAmmo())
            {
                nextFire = Time.time + 0.25f;
                var p = Projectile.Spawn(self, ProjectileOrigin(), MuzzleAimDir() * 50f, 0.1f, new Color(1f, 0.85f, 0.4f), 0.14f, OnBolt);
                p.lifetime = 1.5f;
                Sfx.PlayFor(self, "bolt", 0.5f);
            }
        }

        void OnBolt(Projectile p, Vector3 point, Vector3 normal, Combatant direct, Barrier barrier)
        {
            if (direct != null) direct.ApplyDamage(self, 24f, CombatUtil.IsHeadshot(direct, point), "Spark Bolts");
            if (barrier != null) barrier.Damage(24f, self);
            Fx.Impact(point, normal, new Color(1f, 0.85f, 0.4f));
        }

        void LateUpdate()
        {
            bool show = beamTarget != null && self.alive;
            if (beam.gameObject.activeSelf != show) beam.gameObject.SetActive(show);
            if (show)
            {
                beam.SetPosition(0, MuzzlePos);
                beam.SetPosition(1, beamTarget.ChestPos);
                float w = 0.05f + 0.02f * Mathf.Sin(Time.time * 20f);
                beam.startWidth = w;
                beam.endWidth = w * 1.6f;
            }
        }

        protected override bool UseAbility1()
        {
            self.SpeedBoost(0.6f, 2.5f);
            ab1.activeUntil = Time.time + 2.5f;
            Fx.Burst(self.Feet + Vector3.up, new Color(1f, 0.95f, 0.6f), 10, 3f, 0.1f, 0.4f, 0f);
            return true;
        }

        protected override bool UseAbility2()
        {
            Vector3 p;
            var bot = self.brain as BotBrain;
            if (bot != null && bot.HealTarget != null) p = bot.HealTarget.Feet;
            else p = CombatUtil.AimGroundPoint(self, 20f);
            var z = TimedZone.Create(self, p, 4.5f, 5f, new Color(1f, 0.9f, 0.45f));
            z.onTick = (zone, dt) =>
            {
                foreach (var a in zone.Inside(true)) a.ApplyHeal(self, 45f * dt);
            };
            Sfx.Play("heal", p, 1f, 0.8f);
            return true;
        }

        protected override bool UseUltimate()
        {
            foreach (var a in CombatUtil.AlliesInRadius(self, self.ChestPos, 20f, true))
            {
                if (a != self && !CombatUtil.LineOfSight(self.EyePos, a.ChestPos)) continue;
                a.ApplyHeal(self, 1000f);
                a.AddDamageReduction(0.5f, 4f);
                Fx.Pillar(a.Feet, 0.8f, 3.5f, new Color(1f, 0.92f, 0.5f), 1.2f);
            }
            Fx.Ring(self.Feet, 20f, new Color(1f, 0.9f, 0.5f), 0.8f);
            Sfx.Play("heal", self.ChestPos, 1f, 0.6f);
            return true;
        }

        public override void OnDeath()
        {
            base.OnDeath();
            beamTarget = null;
        }

        void OnDestroy()
        {
            if (beam != null) Destroy(beam.gameObject);
            if (beamMat != null) Destroy(beamMat);
        }

        public override float ProjectileSpeed { get { return 50f; } }
        public override float HealRange { get { return LockRange - 1f; } }
        public override bool AttackWithSecondary { get { return true; } }
        public override float EffectiveRange { get { return 25f; } }
        public override float PreferredRange { get { return 12f; } }

        public override void BotAbilities(BotBrain bot, ref HeroInput input)
        {
            if (ab1.Ready && (self.HealthFrac < 0.5f && bot.Target != null || bot.DistanceToGoal > 25f)) input.ability1 = true;
            var ht = bot.HealTarget;
            if (ab2.Ready && ht != null && (ht.HealthFrac < 0.5f || bot.AlliesHurtNear(ht.Feet, 6f, 0.8f) >= 2)) input.ability2 = true;
            if (self.UltReady && (bot.AlliesHurtNear(self.Feet, 18f, 0.5f) >= 2 || (self.HealthFrac < 0.3f && bot.Target != null))) input.ultimate = true;
        }
    }

    /// <summary>CYPRESS — long range dart support.</summary>
    public class CypressKit : HeroKit
    {
        Vector2 lastMove;

        protected override void Setup()
        {
            primaryName = "Thorn Rifle";
            maxAmmo = 10;
            reloadDuration = 1.5f;
            ab1 = new Ability("SHIFT", "Thorn Dash", 6f);
            ab2 = new Ability("E", "Vine Snare", 10f);
            ultName = "Bloom";
            crosshair = CrosshairStyle.Dot;
        }

        protected override void BuildWeapon(Transform root)
        {
            Color wood = new Color(0.45f, 0.32f, 0.2f);
            ModelUtil.Part(PrimitiveType.Cube, root, new Vector3(0, 0, 0.25f), new Vector3(0.08f, 0.12f, 0.7f), wood);
            ModelUtil.Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.03f, 0.7f), new Vector3(0.04f, 0.15f, 0.04f), new Color(0.2f, 0.25f, 0.2f), false, new Vector3(90, 0, 0));
            ModelUtil.Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.12f, 0.3f), new Vector3(0.06f, 0.12f, 0.06f), new Color(0.2f, 0.2f, 0.2f), false, new Vector3(90, 0, 0));
            ModelUtil.Part(PrimitiveType.Sphere, root, new Vector3(0, -0.05f, 0.05f), new Vector3(0.1f, 0.1f, 0.1f), new Color(0.5f, 1f, 0.5f), true);
            ModelUtil.Part(PrimitiveType.Cube, root, new Vector3(0.045f, 0, 0.3f), new Vector3(0.01f, 0.04f, 0.3f), TeamColors.Relative(self.team), true);
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root, false);
            muzzle.localPosition = new Vector3(0, 0.03f, 0.86f);
        }

        protected override void OnTick(HeroInput input, float dt, bool stunned) { lastMove = input.move; }

        protected override void HandleFire(HeroInput input, float dt)
        {
            zoomFov = input.secondary ? 35f : 0f;
            motor.speedMultiplier = input.secondary ? 0.7f : 1f;
            if (input.primary && CanFire && ConsumeAmmo())
            {
                nextFire = Time.time + 0.8f;
                var tr = CombatUtil.Trace(self, self.EyePos, self.AimDir, 120f, true);
                if (tr.combatant != null && tr.combatant.team == self.team)
                {
                    tr.combatant.ApplyHeal(self, 70f);
                    Fx.Tracer(MuzzlePos, tr.point, new Color(0.5f, 1f, 0.5f), 0.04f, 0.12f);
                    Fx.Burst(tr.combatant.ChestPos, new Color(0.5f, 1f, 0.5f), 6, 2f, 0.1f, 0.5f, -2f);
                    Sfx.Play("heal", tr.combatant.ChestPos, 0.5f, 1.3f);
                }
                else
                {
                    Fx.Tracer(MuzzlePos, tr.point, new Color(0.7f, 1f, 0.4f), 0.03f, 0.1f);
                    if (tr.combatant != null) tr.combatant.ApplyDamage(self, 50f, false, primaryName);
                    else if (tr.barrier != null) tr.barrier.Damage(50f, self);
                    if (tr.hit) Fx.Impact(tr.point, tr.normal, new Color(0.7f, 1f, 0.4f));
                }
                Sfx.PlayFor(self, "dart", 0.8f);
                Recoil(1.2f);
            }
        }

        protected override bool UseAbility1()
        {
            Vector3 dir = Quaternion.Euler(0, self.yaw, 0) * new Vector3(lastMove.x, 0f, lastMove.y);
            motor.Dash(dir.sqrMagnitude > 0.01f ? dir : self.Forward, 7f, 0.15f);
            Fx.Burst(self.ChestPos, new Color(0.5f, 0.9f, 0.4f), 8, 3f, 0.1f, 0.3f, 0f);
            return true;
        }

        protected override bool UseAbility2()
        {
            var p = Projectile.Spawn(self, ProjectileOrigin(), MuzzleAimDir() * 32f, 0.3f, new Color(0.35f, 0.8f, 0.3f), 0.3f, OnSnare);
            p.gravity = 4f;
            p.lifetime = 2f;
            return true;
        }

        void OnSnare(Projectile p, Vector3 point, Vector3 normal, Combatant direct, Barrier barrier)
        {
            Fx.Burst(point, new Color(0.35f, 0.8f, 0.3f), 12, 4f, 0.15f, 0.6f);
            Sfx.Play("stun", point, 0.6f, 1.6f);
            foreach (var e in CombatUtil.EnemiesInRadius(self, point, 2f, true))
            {
                e.ApplyDamage(self, 20f, false, ab2.name);
                e.Root(1.8f);
                Fx.Pillar(e.Feet, 0.6f, 1.2f, new Color(0.3f, 0.7f, 0.25f), 1.8f);
            }
        }

        protected override bool UseUltimate()
        {
            foreach (var a in CombatUtil.AlliesInRadius(self, self.ChestPos, 25f, true))
            {
                a.ApplyHeal(self, 200f);
                a.AddOverHealth(100f, 6f);
                Fx.Burst(a.ChestPos, new Color(0.5f, 1f, 0.5f), 12, 3f, 0.12f, 0.8f, -3f);
                Fx.Pillar(a.Feet, 0.7f, 2.5f, new Color(0.45f, 1f, 0.45f), 1f);
            }
            Fx.Ring(self.Feet, 25f, new Color(0.45f, 1f, 0.45f), 0.9f);
            return true;
        }

        public override float HealRange { get { return 35f; } }
        public override float EffectiveRange { get { return 45f; } }
        public override float PreferredRange { get { return 20f; } }

        public override void BotAbilities(BotBrain bot, ref HeroInput input)
        {
            if (ab1.Ready && self.HealthFrac < 0.45f && Time.time - self.lastDamagedTime < 0.5f) input.ability1 = true;
            var t = bot.Target;
            if (ab2.Ready && t != null && bot.TargetDistance < 14f && bot.HealTarget == null && bot.Chance(0.5f)) input.ability2 = true;
            if (self.UltReady && bot.AlliesHurtNear(self.Feet, 25f, 0.5f) >= 2) input.ultimate = true;
        }
    }
}
