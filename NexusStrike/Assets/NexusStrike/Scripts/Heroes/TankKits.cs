using UnityEngine;

namespace NexusStrike
{
    /// <summary>IRONCLAD — barrier tank.</summary>
    public class IroncladKit : HeroKit
    {
        Barrier barrier;
        float barrierBrokenUntil;
        float barrierLoweredAt;
        bool holding;
        bool leaping;
        float leapStart;
        float fortifyUntil;
        float aiBarrierUntil, aiBarrierNext;

        protected override void Setup()
        {
            primaryName = "Rotary Autocannon";
            maxAmmo = 80;
            reloadDuration = 2.2f;
            ab1 = new Ability("SHIFT", "Rocket Leap", 7f);
            ab2 = new Ability("E", "Fortify", 10f);
            ultName = "Seismic Overload";
            crosshair = CrosshairStyle.Circle;
            barrier = Barrier.Create(self, new Vector3(5.2f, 3.2f, 0.6f), 1000f);
            barrier.onBroken = () => { barrierBrokenUntil = Time.time + 4f; holding = false; motor.speedMultiplier = 1f; };
        }

        protected override void BuildWeapon(Transform root)
        {
            root.localPosition += new Vector3(0.05f, -0.05f, 0f);
            ModelUtil.Part(PrimitiveType.Cube, root, new Vector3(0, 0, 0.25f), new Vector3(0.22f, 0.22f, 0.5f), new Color(0.35f, 0.36f, 0.4f));
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                ModelUtil.Part(PrimitiveType.Cylinder, root, new Vector3(Mathf.Cos(a) * 0.06f, Mathf.Sin(a) * 0.06f, 0.65f),
                    new Vector3(0.05f, 0.2f, 0.05f), new Color(0.2f, 0.2f, 0.22f), false, new Vector3(90, 0, 0));
            }
            ModelUtil.Part(PrimitiveType.Cube, root, new Vector3(0, 0.13f, 0.2f), new Vector3(0.12f, 0.03f, 0.3f), TeamColors.Relative(self.team), true);
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root, false);
            muzzle.localPosition = new Vector3(0, 0, 0.88f);
        }

        protected override void OnTick(HeroInput input, float dt, bool stunned)
        {
            if (!holding && barrier.hp < barrier.maxHp && Time.time > barrierBrokenUntil && Time.time - barrierLoweredAt > 2f)
                barrier.hp = Mathf.Min(barrier.maxHp, barrier.hp + 180f * dt);
            if (barrier.hp <= 0f && Time.time > barrierBrokenUntil) barrier.hp = barrier.maxHp * 0.3f;

            statusText = Time.time < barrierBrokenUntil ? "BARRIER BROKEN" : "BARRIER " + Mathf.CeilToInt(barrier.hp);
            statusFrac = barrier.hp / barrier.maxHp;

            if (leaping && motor.grounded && Time.time - leapStart > 0.25f)
            {
                leaping = false;
                Slam(5.5f, 50f, 9f, 0f);
            }
            if (fortifyUntil > 0f && Time.time > fortifyUntil)
            {
                fortifyUntil = 0f;
                self.unstoppable = false;
            }
            if (stunned) SetHolding(false);
        }

        void SetHolding(bool on)
        {
            if (holding == on) { if (on) PlaceBarrier(); return; }
            holding = on;
            if (on)
            {
                barrier.SetActive(true);
                PlaceBarrier();
                Sfx.PlayFor(self, "barrier", 0.5f);
            }
            else
            {
                barrier.SetActive(false);
                barrierLoweredAt = Time.time;
            }
            motor.speedMultiplier = on ? 0.7f : 1f;
        }

        void PlaceBarrier()
        {
            Vector3 fwd = self.Forward;
            barrier.transform.position = self.Feet + fwd * 2.3f + Vector3.up * 1.5f;
            barrier.transform.rotation = Quaternion.LookRotation(fwd);
        }

        protected override void HandleFire(HeroInput input, float dt)
        {
            bool wantBarrier = input.secondary && barrier.hp > 0f && Time.time > barrierBrokenUntil;
            SetHolding(wantBarrier);
            if (holding) return;
            if (input.primary && CanFire && ConsumeAmmo())
            {
                nextFire = Time.time + 1f / 14f;
                FireHitscan(11f, 2.2f, 60f, 18f, 32f, true, primaryName, new Color(1f, 0.85f, 0.5f));
                Sfx.PlayFor(self, "cannon", 0.45f);
                Recoil(0.12f);
            }
        }

        protected override bool UseAbility1()
        {
            Vector3 fwd = self.Forward;
            motor.velocity = Vector3.zero;
            motor.AddImpulse(fwd * 13f + Vector3.up * 8.5f);
            leaping = true;
            leapStart = Time.time;
            Fx.Burst(self.Feet, new Color(1f, 0.6f, 0.2f), 8, 4f, 0.15f);
            return true;
        }

        protected override bool UseAbility2()
        {
            fortifyUntil = Time.time + 3f;
            ab2.activeUntil = fortifyUntil;
            self.AddDamageReduction(0.5f, 3f);
            self.unstoppable = true;
            Fx.Pillar(self.Feet, 0.9f, self.def.height, new Color(1f, 0.85f, 0.3f), 0.5f);
            return true;
        }

        protected override bool UseUltimate()
        {
            Slam(9f, 60f, 4f, 2.5f);
            Fx.Ring(self.Feet, 9f, new Color(1f, 0.7f, 0.25f), 0.7f);
            return true;
        }

        void Slam(float radius, float damage, float knock, float stun)
        {
            Sfx.Play("slam", self.Feet, 1f);
            Fx.Ring(self.Feet, radius, new Color(1f, 0.8f, 0.4f));
            Fx.Burst(self.Feet, new Color(0.7f, 0.6f, 0.5f), 12, 7f, 0.25f, 0.7f);
            foreach (var e in CombatUtil.EnemiesInRadius(self, self.Feet + Vector3.up, radius, true))
            {
                e.ApplyDamage(self, damage, false, stun > 0f ? ultName : ab1.name);
                if (!e.alive) continue;
                if (stun > 0f) { e.Stun(stun); Sfx.Play("stun", e.ChestPos, 0.6f); }
                Vector3 d = e.Feet - self.Feet; d.y = 0f;
                e.Knockback(d.normalized * knock + Vector3.up * 4f);
            }
        }

        public override void OnDeath()
        {
            base.OnDeath();
            SetHolding(false);
            leaping = false;
            self.unstoppable = false;
            fortifyUntil = 0f;
        }

        public override void OnRespawn()
        {
            base.OnRespawn();
            barrier.hp = barrier.maxHp;
            barrierBrokenUntil = 0f;
        }

        void OnDestroy() { if (barrier != null) Destroy(barrier.gameObject); }

        public override float EffectiveRange { get { return 28f; } }
        public override float PreferredRange { get { return 9f; } }

        public override void BotAbilities(BotBrain bot, ref HeroInput input)
        {
            var t = bot.Target;
            if (t != null)
            {
                float d = bot.TargetDistance;
                if (Time.time > aiBarrierNext && d > 8f && barrier.hp > 300f && (self.HealthFrac < 0.75f || bot.VisibleEnemies >= 2))
                {
                    aiBarrierUntil = Time.time + Random.Range(1.5f, 3f);
                    aiBarrierNext = aiBarrierUntil + Random.Range(1.5f, 3f);
                }
                if (Time.time < aiBarrierUntil && barrier.hp > 150f)
                {
                    input.secondary = true;
                    input.primary = false;
                }
                if (ab1.Ready && d > 7f && d < 16f && self.HealthFrac > 0.6f && bot.Chance(0.6f)) input.ability1 = true;
                if (ab2.Ready && self.HealthFrac < 0.45f) input.ability2 = true;
                if (self.UltReady && bot.EnemiesWithin(self.Feet, 8f) >= 2) input.ultimate = true;
                if (self.UltReady && bot.EnemiesWithin(self.Feet, 7f) >= 1 && self.HealthFrac < 0.3f) input.ultimate = true;
            }
        }
    }

    /// <summary>TITAN — gravity brawler dive tank.</summary>
    public class TitanKit : HeroKit
    {
        bool leaping;
        float leapStart;
        GameObject shellVis;
        Material shellMat;
        float shellUntil;

        protected override void Setup()
        {
            primaryName = "Impact Scatter";
            maxAmmo = 6;
            reloadDuration = 1.6f;
            secondaryAbility = new Ability("RMB", "Kinetic Shell", 8f);
            ab1 = new Ability("SHIFT", "Meteor Drop", 8f);
            ab2 = new Ability("E", "Shockwave", 7f);
            ultName = "Singularity";
            crosshair = CrosshairStyle.Wide;

            shellVis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyImmediate(shellVis.GetComponent<Collider>());
            shellVis.layer = Layers.IgnoreRaycast;
            shellVis.transform.SetParent(transform, false);
            shellVis.transform.localPosition = Vector3.up * self.def.height * 0.5f;
            shellVis.transform.localScale = Vector3.one * (self.def.height + 0.3f);
            Color c = TeamColors.Relative(self.team);
            shellMat = MaterialLib.NewUnlit(new Color(c.r, c.g, c.b, 0.22f));
            var r = shellVis.GetComponent<Renderer>();
            r.sharedMaterial = shellMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shellVis.SetActive(false);
        }

        protected override void BuildWeapon(Transform root)
        {
            ModelUtil.Part(PrimitiveType.Cube, root, new Vector3(0, 0, 0.2f), new Vector3(0.28f, 0.26f, 0.42f), new Color(0.3f, 0.26f, 0.38f));
            ModelUtil.Part(PrimitiveType.Cylinder, root, new Vector3(0, 0, 0.5f), new Vector3(0.24f, 0.1f, 0.24f), new Color(0.2f, 0.18f, 0.25f), false, new Vector3(90, 0, 0));
            ModelUtil.Part(PrimitiveType.Sphere, root, new Vector3(0, 0, 0.6f), new Vector3(0.16f, 0.16f, 0.06f), new Color(0.7f, 0.5f, 1f), true);
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root, false);
            muzzle.localPosition = new Vector3(0, 0, 0.65f);
        }

        protected override void OnTick(HeroInput input, float dt, bool stunned)
        {
            bool shellOn = Time.time < shellUntil && self.overHealth > 0f;
            if (shellVis.activeSelf != shellOn) shellVis.SetActive(shellOn);
            if (shellOn)
            {
                statusText = "SHELL " + Mathf.CeilToInt(self.overHealth);
                statusFrac = self.overHealth / 300f;
            }
            else
            {
                statusText = null;
                statusFrac = -1f;
            }
            if (leaping && motor.grounded && Time.time - leapStart > 0.3f)
            {
                leaping = false;
                motor.gravityScale = 1f;
                Sfx.Play("slam", self.Feet, 1f);
                Fx.Ring(self.Feet, 6f, new Color(0.7f, 0.5f, 1f));
                Fx.Burst(self.Feet, new Color(0.6f, 0.5f, 0.45f), 14, 8f, 0.25f, 0.7f);
                foreach (var e in CombatUtil.EnemiesInRadius(self, self.Feet + Vector3.up, 6f, true))
                {
                    e.ApplyDamage(self, 60f, false, ab1.name);
                    if (e.alive) e.Knockback(Vector3.up * 7f);
                }
            }
        }

        protected override void HandleFire(HeroInput input, float dt)
        {
            if (input.secondary && secondaryAbility.Ready)
            {
                secondaryAbility.Trigger();
                self.AddOverHealth(300f, 2.5f);
                shellUntil = Time.time + 2.5f;
                secondaryAbility.activeUntil = shellUntil;
                Sfx.PlayFor(self, "barrier", 0.6f, 1.3f);
            }
            if (input.primary && CanFire && ConsumeAmmo())
            {
                nextFire = Time.time + 0.75f;
                for (int i = 0; i < 10; i++)
                    FireHitscan(7f, 6f, 30f, 8f, 18f, false, primaryName, new Color(0.75f, 0.55f, 1f));
                Sfx.PlayFor(self, "cannon", 0.9f, 0.7f);
                Fx.Flash(MuzzlePos, new Color(0.8f, 0.6f, 1f), 0.4f);
                Recoil(1.5f);
            }
        }

        protected override bool UseAbility1()
        {
            Vector3 fwd = self.AimDir;
            fwd.y = 0f;
            fwd.Normalize();
            float pitchFactor = Mathf.Clamp01((-self.pitch + 10f) / 50f);
            motor.velocity = Vector3.zero;
            motor.AddImpulse(fwd * Mathf.Lerp(9f, 16f, pitchFactor) + Vector3.up * 13f);
            leaping = true;
            leapStart = Time.time;
            return true;
        }

        protected override bool UseAbility2()
        {
            Vector3 fwd = self.Forward;
            Fx.Burst(self.ChestPos + fwd, new Color(0.75f, 0.55f, 1f), 16, 10f, 0.2f, 0.4f, 0f);
            Sfx.Play("whoosh", self.ChestPos, 1f, 0.7f);
            foreach (var e in CombatUtil.EnemiesInRadius(self, self.ChestPos, 10f, true))
            {
                Vector3 d = e.ChestPos - self.ChestPos;
                if (Vector3.Angle(fwd, new Vector3(d.x, 0, d.z)) > 40f) continue;
                e.ApplyDamage(self, 40f, false, ab2.name);
                if (e.alive) e.Knockback(fwd * 14f + Vector3.up * 5f);
            }
            return true;
        }

        protected override bool UseUltimate()
        {
            var p = Projectile.Spawn(self, ProjectileOrigin(), MuzzleAimDir() * 26f, 0.3f, new Color(0.5f, 0.3f, 0.9f), 0.5f, OnSingularityImpact);
            p.gravity = 8f;
            return true;
        }

        void OnSingularityImpact(Projectile p, Vector3 point, Vector3 normal, Combatant direct, Barrier barrier)
        {
            Vector3 center = point + normal * 0.5f;
            RaycastHit h;
            if (Physics.Raycast(center + Vector3.up, Vector3.down, out h, 10f, Layers.World, QueryTriggerInteraction.Ignore))
                center = h.point;
            var z = TimedZone.Create(self, center, 7f, 3f, new Color(0.55f, 0.3f, 1f), true);
            Sfx.Play("ult", center, 1f, 0.6f);
            z.onTick = (zone, dt) =>
            {
                foreach (var e in Combatant.All)
                {
                    if (!e.alive || e.team == zone.team) continue;
                    Vector3 d = zone.transform.position + Vector3.up * 1.2f - e.ChestPos;
                    if (d.magnitude > zone.radius + 1f) continue;
                    Vector3 pull = d;
                    pull.y = Mathf.Max(pull.y, 0f);
                    if (pull.magnitude > 0.4f && e.motor.cc.enabled) e.motor.cc.Move(pull.normalized * Mathf.Min(8f * dt, pull.magnitude));
                    e.Slow(0.5f, 0.2f);
                    e.ApplyDamage(self, 25f * dt, false, ultName);
                }
            };
            z.onEnd = zone =>
            {
                Fx.Explosion(zone.transform.position + Vector3.up * 1.2f, 4f, new Color(0.6f, 0.35f, 1f));
                Sfx.Play("explosion", zone.transform.position, 1f, 0.8f);
                CombatUtil.Explode(self, zone.transform.position + Vector3.up * 1.2f, 7f, 110f, 6f, ultName, false, 0.6f);
            };
        }

        public override void OnDeath()
        {
            base.OnDeath();
            leaping = false;
            shellUntil = 0f;
            if (shellVis != null) shellVis.SetActive(false);
        }

        void OnDestroy() { if (shellMat != null) Destroy(shellMat); }

        public override float EffectiveRange { get { return 16f; } }
        public override float PreferredRange { get { return 5f; } }

        public override void BotAbilities(BotBrain bot, ref HeroInput input)
        {
            if (secondaryAbility.Ready && self.HealthFrac < 0.7f && Time.time - self.lastDamagedTime < 0.5f) input.secondary = true;
            var t = bot.Target;
            if (t == null) return;
            float d = bot.TargetDistance;
            if (ab1.Ready && d > 10f && d < 24f && self.HealthFrac > 0.5f && bot.Chance(0.5f)) input.ability1 = true;
            if (ab2.Ready && d < 7f) input.ability2 = true;
            if (self.UltReady && bot.EnemiesWithin(t.Feet, 7f) >= 2 && d < 25f) input.ultimate = true;
        }
    }
}
