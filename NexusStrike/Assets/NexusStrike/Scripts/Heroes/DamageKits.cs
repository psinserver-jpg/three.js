using UnityEngine;

namespace NexusStrike
{
    /// <summary>VEX — hitscan rifle skirmisher.</summary>
    public class VexKit : HeroKit
    {
        Vector2 lastMove;

        protected override void Setup()
        {
            primaryName = "Pulse Rifle";
            maxAmmo = 30;
            reloadDuration = 1.4f;
            ab1 = new Ability("SHIFT", "Blink", 5f);
            ab2 = new Ability("E", "Frag Grenade", 9f);
            ultName = "Overdrive";
            crosshair = CrosshairStyle.Cross;
        }

        protected override void BuildWeapon(Transform root)
        {
            Color body = new Color(0.18f, 0.2f, 0.22f);
            ModelUtil.Part(PrimitiveType.Cube, root, new Vector3(0, 0, 0.22f), new Vector3(0.09f, 0.14f, 0.6f), body);
            ModelUtil.Part(PrimitiveType.Cube, root, new Vector3(0, -0.11f, 0.12f), new Vector3(0.06f, 0.14f, 0.08f), body);
            ModelUtil.Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.02f, 0.6f), new Vector3(0.04f, 0.12f, 0.04f), body, false, new Vector3(90, 0, 0));
            ModelUtil.Part(PrimitiveType.Cube, root, new Vector3(0, 0.09f, 0.18f), new Vector3(0.05f, 0.05f, 0.18f), new Color(0.2f, 0.9f, 0.7f), true);
            ModelUtil.Part(PrimitiveType.Cube, root, new Vector3(0.05f, 0, 0.3f), new Vector3(0.01f, 0.03f, 0.3f), TeamColors.Relative(self.team), true);
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root, false);
            muzzle.localPosition = new Vector3(0, 0.02f, 0.74f);
        }

        protected override bool ultActiveInfiniteAmmo { get { return ultActive; } }

        protected override void OnTick(HeroInput input, float dt, bool stunned)
        {
            lastMove = input.move;
            if (ultActive)
            {
                statusText = "OVERDRIVE " + Mathf.Max(0f, ultActiveUntil - Time.time).ToString("0.0");
                statusFrac = (ultActiveUntil - Time.time) / 6f;
            }
            else { statusText = null; statusFrac = -1f; }
        }

        protected override void HandleFire(HeroInput input, float dt)
        {
            zoomFov = input.secondary ? 50f : 0f;
            motor.speedMultiplier = input.secondary ? 0.75f : 1f;
            if (input.primary && CanFire && ConsumeAmmo())
            {
                nextFire = Time.time + (ultActive ? 0.065f : 0.1f);
                float spread = input.secondary ? 0.25f : 0.9f;
                FireHitscan(20f, spread, 120f, 25f, 45f, true, ultActive ? ultName : primaryName,
                    ultActive ? new Color(1f, 0.4f, 0.9f) : new Color(0.4f, 1f, 0.85f));
                Sfx.PlayFor(self, "rifle", 0.55f, ultActive ? 1.2f : 1f);
                Recoil(0.25f);
            }
        }

        protected override bool UseAbility1()
        {
            Vector3 dir = Quaternion.Euler(0, self.yaw, 0) * new Vector3(lastMove.x, 0f, lastMove.y);
            Fx.Burst(self.ChestPos, new Color(0.3f, 1f, 0.8f), 10, 3f, 0.12f, 0.3f, 0f);
            motor.Dash(dir.sqrMagnitude > 0.01f ? dir : self.Forward, 8f, 0.12f);
            Sfx.PlayFor(self, "whoosh", 0.8f, 1.4f);
            return true;
        }

        protected override bool UseAbility2()
        {
            var p = Projectile.Spawn(self, ProjectileOrigin(), self.AimDir * 20f + Vector3.up * 3f, 0.15f, new Color(0.3f, 1f, 0.6f), 0.22f, OnFragImpact);
            p.gravity = 14f;
            p.bounces = -1;
            p.fuse = 1.1f;
            return true;
        }

        void OnFragImpact(Projectile p, Vector3 point, Vector3 normal, Combatant direct, Barrier barrier)
        {
            Fx.Explosion(point, 3f, new Color(0.4f, 1f, 0.6f));
            Sfx.Play("explosion", point, 0.9f, 1.2f);
            if (barrier != null) barrier.Damage(90f, self);
            CombatUtil.Explode(self, point, 4f, 90f, 5f, ab2.name);
        }

        protected override bool UseUltimate()
        {
            ultActive = true;
            ultActiveUntil = Time.time + 6f;
            ammo = maxAmmo;
            reloading = false;
            self.SpeedBoost(0.2f, 6f);
            Fx.Pillar(self.Feet, 0.7f, 2.2f, new Color(1f, 0.4f, 0.9f));
            return true;
        }

        public override void OnDealtDamage(Combatant victim, float amount)
        {
            if (ultActive) self.ApplyHeal(self, amount * 0.3f);
        }

        public override float EffectiveRange { get { return 40f; } }
        public override float PreferredRange { get { return 16f; } }

        public override void BotAbilities(BotBrain bot, ref HeroInput input)
        {
            var t = bot.Target;
            if (t == null) return;
            float d = bot.TargetDistance;
            if (ab1.Ready && self.HealthFrac < 0.45f && Time.time - self.lastDamagedTime < 0.4f) input.ability1 = true;
            if (ab2.Ready && d > 5f && d < 18f && bot.AimError < 12f && bot.Chance(0.5f)) input.ability2 = true;
            if (self.UltReady && d < 25f && bot.AimError < 10f) input.ultimate = true;
        }
    }

    /// <summary>KESTREL — aerial rocket launcher.</summary>
    public class KestrelKit : HeroKit
    {
        float fuel = 3f;
        const float MaxFuel = 3f;
        float nextSwarm;

        protected override void Setup()
        {
            primaryName = "Rocket Launcher";
            maxAmmo = 6;
            reloadDuration = 1.8f;
            ab1 = new Ability("SHIFT", "Jet Boost", 6f);
            ab2 = new Ability("E", "Shock Pulse", 8f);
            ultName = "Missile Swarm";
            crosshair = CrosshairStyle.Circle;
        }

        protected override void BuildWeapon(Transform root)
        {
            Color body = new Color(0.35f, 0.32f, 0.25f);
            ModelUtil.Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.02f, 0.25f), new Vector3(0.18f, 0.34f, 0.18f), body, false, new Vector3(90, 0, 0));
            ModelUtil.Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.02f, 0.6f), new Vector3(0.22f, 0.03f, 0.22f), new Color(0.85f, 0.65f, 0.25f), false, new Vector3(90, 0, 0));
            ModelUtil.Part(PrimitiveType.Cube, root, new Vector3(0, -0.13f, 0.12f), new Vector3(0.06f, 0.14f, 0.08f), body);
            ModelUtil.Part(PrimitiveType.Cube, root, new Vector3(0, 0.13f, 0.2f), new Vector3(0.04f, 0.04f, 0.2f), TeamColors.Relative(self.team), true);
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root, false);
            muzzle.localPosition = new Vector3(0, 0.02f, 0.65f);
        }

        protected override void OnTick(HeroInput input, float dt, bool stunned)
        {
            bool hovering = !ultActive && input.secondary && !motor.grounded && fuel > 0f && !stunned;
            if (hovering)
            {
                fuel -= dt;
                motor.gravityScale = 0.12f;
                if (motor.velocity.y < -1.5f) motor.velocity.y = -1.5f;
                if (Random.value < 0.3f) Fx.Flash(self.Feet + Vector3.up * 0.9f - self.Forward * 0.3f, new Color(1f, 0.6f, 0.2f), 0.2f, 0.08f);
            }
            else
            {
                motor.gravityScale = 1f;
                if (motor.grounded) fuel = Mathf.Min(MaxFuel, fuel + dt * 1.2f);
            }
            statusText = "JET FUEL";
            statusFrac = fuel / MaxFuel;

            if (ultActive && !stunned && Time.time >= nextSwarm)
            {
                nextSwarm = Time.time + 0.07f;
                Vector3 dir = CombatUtil.Spread(MuzzleAimDir(), 4f);
                var p = Projectile.Spawn(self, ProjectileOrigin(), dir * 30f, 0.12f, new Color(1f, 0.6f, 0.2f), 0.18f, OnMiniRocket);
                p.lifetime = 3f;
                Sfx.PlayFor(self, "rocket", 0.25f, 1.6f);
            }
        }

        protected override void HandleFire(HeroInput input, float dt)
        {
            if (ultActive) return;
            if (input.primary && CanFire && ConsumeAmmo())
            {
                nextFire = Time.time + 0.85f;
                var p = Projectile.Spawn(self, ProjectileOrigin(), MuzzleAimDir() * 34f, 0.18f, new Color(1f, 0.55f, 0.15f), 0.25f, OnRocket);
                p.lifetime = 4f;
                Sfx.PlayFor(self, "rocket", 0.8f);
                Fx.Flash(MuzzlePos, new Color(1f, 0.7f, 0.3f), 0.35f);
                Recoil(1.2f);
            }
        }

        void OnRocket(Projectile p, Vector3 point, Vector3 normal, Combatant direct, Barrier barrier)
        {
            if (direct != null) direct.ApplyDamage(self, 60f, false, primaryName);
            if (barrier != null) barrier.Damage(120f, self);
            Fx.Explosion(point, 2.2f, new Color(1f, 0.55f, 0.2f));
            Sfx.Play("explosion", point, 0.9f);
            CombatUtil.Explode(self, point + normal * 0.2f, 3f, 60f, 7f, primaryName);
        }

        void OnMiniRocket(Projectile p, Vector3 point, Vector3 normal, Combatant direct, Barrier barrier)
        {
            if (direct != null) direct.ApplyDamage(self, 20f, false, ultName);
            if (barrier != null) barrier.Damage(40f, self);
            Fx.Explosion(point, 1.2f, new Color(1f, 0.6f, 0.2f));
            if (Random.value < 0.4f) Sfx.Play("explosion", point, 0.4f, 1.6f);
            CombatUtil.Explode(self, point + normal * 0.2f, 2.2f, 22f, 2f, ultName, false);
        }

        protected override bool UseAbility1()
        {
            motor.velocity.y = 0f;
            motor.AddImpulse(Vector3.up * 13f);
            Fx.Burst(self.Feet, new Color(1f, 0.6f, 0.2f), 12, 5f, 0.18f, 0.5f);
            Sfx.PlayFor(self, "rocket", 0.7f, 0.7f);
            return true;
        }

        protected override bool UseAbility2()
        {
            var p = Projectile.Spawn(self, ProjectileOrigin(), MuzzleAimDir() * 40f, 0.25f, new Color(0.4f, 0.8f, 1f), 0.35f, OnConcussion);
            p.lifetime = 1.2f;
            return true;
        }

        void OnConcussion(Projectile p, Vector3 point, Vector3 normal, Combatant direct, Barrier barrier)
        {
            Fx.Ring(point, 4.5f, new Color(0.4f, 0.8f, 1f), 0.35f);
            Fx.Flash(point, new Color(0.6f, 0.9f, 1f), 2.5f, 0.15f);
            Sfx.Play("whoosh", point, 1f, 0.8f);
            CombatUtil.Explode(self, point + normal * 0.3f, 4.5f, 30f, 15f, ab2.name, false, 0.8f);
        }

        protected override bool UseUltimate()
        {
            ultActive = true;
            ultActiveUntil = Time.time + 3f;
            motor.flying = true;
            motor.AddImpulse(Vector3.up * 5f);
            return true;
        }

        protected override void OnUltimateEnd() { motor.flying = false; }

        public override void OnDeath()
        {
            base.OnDeath();
            motor.flying = false;
            fuel = MaxFuel;
        }

        public override float ProjectileSpeed { get { return 34f; } }
        public override bool AimAtFeet { get { return true; } }
        public override float EffectiveRange { get { return 32f; } }
        public override float PreferredRange { get { return 14f; } }

        public override void BotAbilities(BotBrain bot, ref HeroInput input)
        {
            if (!motor.grounded && fuel > 0.5f) input.secondary = true;
            var t = bot.Target;
            if (t == null) return;
            float d = bot.TargetDistance;
            if (ab1.Ready && motor.grounded && d < 25f && bot.Chance(0.35f)) input.ability1 = true;
            if (ab2.Ready && d < 6f) input.ability2 = true;
            if (self.UltReady && d < 28f && (bot.EnemiesWithin(t.Feet, 6f) >= 2 || t.HealthFrac > 0.7f)) input.ultimate = true;
        }
    }

    /// <summary>ROOK — area-denial engineer with turret and trap.</summary>
    public class RookKit : HeroKit
    {
        Turret turret;
        Projectile trap;

        protected override void Setup()
        {
            primaryName = "Arc Caster";
            maxAmmo = 10;
            reloadDuration = 1.7f;
            secondaryAbility = new Ability("RMB", "Overcharge", 5f);
            ab1 = new Ability("SHIFT", "Sentry Turret", 10f);
            ab2 = new Ability("E", "Arc Trap", 9f);
            ultName = "Tesla Field";
            crosshair = CrosshairStyle.Dot;
        }

        protected override void BuildWeapon(Transform root)
        {
            Color body = new Color(0.3f, 0.28f, 0.27f);
            ModelUtil.Part(PrimitiveType.Cube, root, new Vector3(0, 0, 0.2f), new Vector3(0.14f, 0.16f, 0.45f), body);
            ModelUtil.Part(PrimitiveType.Sphere, root, new Vector3(0, 0.02f, 0.45f), new Vector3(0.16f, 0.16f, 0.16f), new Color(0.45f, 0.75f, 1f), true);
            ModelUtil.Part(PrimitiveType.Cube, root, new Vector3(0, -0.12f, 0.1f), new Vector3(0.06f, 0.14f, 0.08f), body);
            ModelUtil.Part(PrimitiveType.Cube, root, new Vector3(0.075f, 0, 0.22f), new Vector3(0.01f, 0.04f, 0.3f), TeamColors.Relative(self.team), true);
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root, false);
            muzzle.localPosition = new Vector3(0, 0.02f, 0.56f);
        }

        protected override void HandleFire(HeroInput input, float dt)
        {
            if (input.secondary && secondaryAbility.Ready && !reloading && ammo >= 3)
            {
                secondaryAbility.Trigger();
                ConsumeAmmo(3);
                nextFire = Time.time + 0.6f;
                var big = Projectile.Spawn(self, ProjectileOrigin(), MuzzleAimDir() * 24f, 0.3f, new Color(0.5f, 0.85f, 1f), 0.5f, OnOvercharge);
                big.lifetime = 3f;
                Sfx.PlayFor(self, "bolt", 1f, 0.5f);
                Recoil(1.5f);
                return;
            }
            if (input.primary && CanFire && ConsumeAmmo())
            {
                nextFire = Time.time + 0.5f;
                var p = Projectile.Spawn(self, ProjectileOrigin(), MuzzleAimDir() * 32f, 0.16f, new Color(0.45f, 0.8f, 1f), 0.25f, OnOrb);
                p.bounces = 1;
                p.lifetime = 2f;
                p.fuse = 1.6f;
                Sfx.PlayFor(self, "bolt", 0.7f, 0.8f);
                Recoil(0.6f);
            }
        }

        void OnOrb(Projectile p, Vector3 point, Vector3 normal, Combatant direct, Barrier barrier)
        {
            if (direct != null) direct.ApplyDamage(self, 40f, false, primaryName);
            if (barrier != null) barrier.Damage(60f, self);
            Fx.Explosion(point, 1.4f, new Color(0.45f, 0.8f, 1f));
            Sfx.Play("bolt", point, 0.6f, 0.6f);
            CombatUtil.Explode(self, point + normal * 0.2f, 2.5f, 30f, 3f, primaryName);
        }

        void OnOvercharge(Projectile p, Vector3 point, Vector3 normal, Combatant direct, Barrier barrier)
        {
            if (direct != null) direct.ApplyDamage(self, 70f, false, secondaryAbility.name);
            if (barrier != null) barrier.Damage(160f, self);
            Fx.Explosion(point, 3f, new Color(0.5f, 0.85f, 1f));
            Sfx.Play("explosion", point, 1f, 1.1f);
            CombatUtil.Explode(self, point + normal * 0.2f, 3.8f, 60f, 6f, secondaryAbility.name);
        }

        protected override bool UseAbility1()
        {
            Vector3 p = CombatUtil.AimGroundPoint(self, 7f);
            if (Vector3.Distance(p, self.Feet) > 8f) p = self.Feet + self.Forward * 2f;
            if (turret != null) Destroy(turret.gameObject);
            turret = Turret.Create(self, p, self.yaw);
            Fx.Burst(p, new Color(1f, 0.6f, 0.2f), 8, 3f, 0.12f);
            return true;
        }

        protected override bool UseAbility2()
        {
            if (trap != null) trap.Kill();
            trap = Projectile.Spawn(self, ProjectileOrigin(), self.AimDir * 14f + Vector3.up * 2f, 0.2f, new Color(0.45f, 0.8f, 1f), 0.45f, OnTrap, false);
            trap.gravity = 14f;
            trap.stickToGround = true;
            trap.lifetime = 40f;
            trap.transform.localScale = new Vector3(0.6f, 0.12f, 0.6f);
            return true;
        }

        void OnTrap(Projectile p, Vector3 point, Vector3 normal, Combatant direct, Barrier barrier)
        {
            if (direct == null) return;
            direct.ApplyDamage(self, 60f, false, ab2.name);
            direct.Root(1.8f);
            Fx.Pillar(direct.Feet, 0.6f, 2f, new Color(0.45f, 0.8f, 1f), 1.8f);
            Sfx.Play("stun", direct.ChestPos, 0.8f, 1.3f);
        }

        protected override bool UseUltimate()
        {
            Vector3 p = CombatUtil.AimGroundPoint(self, 25f);
            var z = TimedZone.Create(self, p, 8f, 5f, new Color(0.45f, 0.75f, 1f));
            z.onTick = (zone, dt) =>
            {
                foreach (var e in zone.Inside(false))
                {
                    e.ApplyDamage(self, 45f * dt, false, ultName);
                    e.Slow(0.4f, 0.25f);
                    if (Random.value < 4f * dt) Fx.Tracer(zone.transform.position + Vector3.up * 3f, e.ChestPos, new Color(0.6f, 0.9f, 1f), 0.05f, 0.08f);
                }
            };
            Sfx.Play("ult", p, 1f, 1.2f);
            return true;
        }

        void OnDestroy()
        {
            if (turret != null) Destroy(turret.gameObject);
            if (trap != null) trap.Kill();
        }

        public override float ProjectileSpeed { get { return 32f; } }
        public override bool AimAtFeet { get { return true; } }
        public override float EffectiveRange { get { return 26f; } }
        public override float PreferredRange { get { return 13f; } }

        public override void BotAbilities(BotBrain bot, ref HeroInput input)
        {
            if (ab1.Ready && turret == null && (bot.Target != null || bot.NearObjective)) input.ability1 = true;
            var t = bot.Target;
            if (t == null) return;
            float d = bot.TargetDistance;
            if (ab2.Ready && d < 9f && bot.Chance(0.5f)) input.ability2 = true;
            if (secondaryAbility.Ready && d < 18f && bot.Chance(0.3f)) input.secondary = true;
            if (self.UltReady && d < 22f && bot.EnemiesWithin(t.Feet, 7f) >= 2) input.ultimate = true;
        }
    }
}
