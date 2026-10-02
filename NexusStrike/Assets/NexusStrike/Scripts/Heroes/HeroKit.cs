using UnityEngine;

namespace NexusStrike
{
    public class Ability
    {
        public string name, key;
        public float cooldown;
        public float remaining;
        public float activeUntil;

        public Ability(string key, string name, float cooldown)
        {
            this.key = key;
            this.name = name;
            this.cooldown = cooldown;
        }

        public bool Ready { get { return remaining <= 0f; } }
        public bool Active { get { return Time.time < activeUntil; } }
        public float Fraction { get { return cooldown > 0f ? Mathf.Clamp01(remaining / cooldown) : 0f; } }
        public void Trigger() { remaining = cooldown; }
    }

    /// <summary>
    /// Base class for a hero's weapons and abilities. Subclasses implement the fire logic and the
    /// three ability hooks; the base handles cooldowns, ammo, reloads and ultimate gating.
    /// </summary>
    public abstract class HeroKit : MonoBehaviour
    {
        public Combatant self;
        public HeroMotor motor;

        public string primaryName = "Primary";
        public int maxAmmo;
        public int ammo;
        public float reloadDuration = 1.5f;
        public bool reloading;
        protected float reloadEnd;
        protected float nextFire;

        public Ability ab1, ab2, secondaryAbility;
        public string ultName = "Ultimate";
        public bool ultActive;
        public float ultActiveUntil;

        public float zoomFov;              // > 0 while zoomed (player camera uses it)
        public CrosshairStyle crosshair = CrosshairStyle.Cross;
        public string statusText;          // shown on HUD (e.g. barrier HP)
        public float statusFrac = -1f;     // optional bar for the status text

        protected Transform weapon;
        protected Transform muzzle;

        public Vector3 MuzzlePos { get { return muzzle != null ? muzzle.position : self.EyePos; } }

        public void Init(Combatant c)
        {
            self = c;
            motor = c.motor;
            Setup();
            ammo = maxAmmo;
            weapon = new GameObject("Weapon").transform;
            weapon.SetParent(c.weaponSocket, false);
            BuildWeapon(weapon);
            if (muzzle == null)
            {
                muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(weapon, false);
                muzzle.localPosition = new Vector3(0f, 0f, 0.7f);
            }
        }

        protected abstract void Setup();
        protected abstract void BuildWeapon(Transform root);

        public void Tick(HeroInput input, float dt, bool canAct)
        {
            if (ab1 != null && ab1.remaining > 0f) ab1.remaining -= dt;
            if (ab2 != null && ab2.remaining > 0f) ab2.remaining -= dt;
            if (secondaryAbility != null && secondaryAbility.remaining > 0f) secondaryAbility.remaining -= dt;

            if (reloading && Time.time >= reloadEnd)
            {
                reloading = false;
                ammo = maxAmmo;
            }
            if (ultActive && Time.time >= ultActiveUntil && ultActiveUntil > 0f)
            {
                ultActive = false;
                OnUltimateEnd();
            }

            bool stunned = self.IsStunned || !canAct;
            OnTick(input, dt, stunned);
            if (stunned)
            {
                zoomFov = 0f;
                return;
            }

            if (input.reload && !reloading && maxAmmo > 0 && ammo < maxAmmo && !ultActiveInfiniteAmmo) StartReload();
            if (input.ability1 && ab1 != null && ab1.Ready && UseAbility1())
            {
                ab1.Trigger();
                Sfx.PlayFor(self, "ability", 0.6f);
            }
            if (input.ability2 && ab2 != null && ab2.Ready && UseAbility2())
            {
                ab2.Trigger();
                Sfx.PlayFor(self, "ability", 0.6f, 1.2f);
            }
            if (input.ultimate && self.UltReady && !ultActive && UseUltimate())
            {
                self.ultCharge = 0f;
                Sfx.Play("ult", self.ChestPos, 1f);
                if (GameManager.I != null) GameManager.I.OnUltimate(self);
            }
            HandleFire(input, dt);
        }

        protected virtual bool ultActiveInfiniteAmmo { get { return false; } }

        protected virtual void OnTick(HeroInput input, float dt, bool stunned) { }
        protected virtual void HandleFire(HeroInput input, float dt) { }
        protected virtual bool UseAbility1() { return false; }
        protected virtual bool UseAbility2() { return false; }
        protected virtual bool UseUltimate() { return false; }
        protected virtual void OnUltimateEnd() { }

        public virtual void OnDealtDamage(Combatant victim, float amount) { }
        public virtual void OnStunned() { }
        public virtual void OnDeath()
        {
            ultActive = false;
            reloading = false;
            zoomFov = 0f;
        }
        public virtual void OnRespawn()
        {
            ammo = maxAmmo;
            reloading = false;
            if (ab1 != null) ab1.remaining = 0f;
            if (ab2 != null) ab2.remaining = 0f;
            if (secondaryAbility != null) secondaryAbility.remaining = 0f;
        }

        protected bool CanFire { get { return Time.time >= nextFire && !reloading && !motor.Dashing; } }

        protected bool ConsumeAmmo(int n = 1)
        {
            if (maxAmmo <= 0 || ultActiveInfiniteAmmo) return true;
            if (reloading) return false;
            if (ammo < n)
            {
                StartReload();
                return false;
            }
            ammo -= n;
            if (ammo <= 0) StartReload();
            return true;
        }

        public void StartReload()
        {
            if (reloading || maxAmmo <= 0) return;
            reloading = true;
            reloadEnd = Time.time + reloadDuration;
            Sfx.PlayFor(self, "reload", 0.5f);
        }

        public float ReloadProgress { get { return reloading ? 1f - Mathf.Clamp01((reloadEnd - Time.time) / reloadDuration) : 1f; } }

        // ------------------------------------------------------------------ shooting helpers

        /// <summary>Fire one hitscan bullet from the eye along the (spread) aim direction.</summary>
        protected TraceResult FireHitscan(float damage, float spread, float range, float falloffStart, float falloffEnd,
            bool canHeadshot, string source, Color tracer, bool includeAllies = false)
        {
            Vector3 dir = CombatUtil.Spread(self.AimDir, spread);
            var tr = CombatUtil.Trace(self, self.EyePos, dir, range, includeAllies);
            Fx.Tracer(MuzzlePos, tr.point, tracer);
            if (tr.combatant != null && tr.combatant.team != self.team)
            {
                bool head = canHeadshot && CombatUtil.IsHeadshot(tr.combatant, tr.point);
                float dmg = damage * CombatUtil.Falloff(tr.distance, falloffStart, falloffEnd) * (head ? 2f : 1f);
                tr.combatant.ApplyDamage(self, dmg, head, source);
                Fx.Impact(tr.point, tr.normal, new Color(1f, 0.85f, 0.5f));
            }
            else if (tr.barrier != null)
            {
                tr.barrier.Damage(damage, self);
                Fx.Impact(tr.point, tr.normal, TeamColors.Relative(tr.barrier.team));
            }
            else if (tr.hit && tr.combatant == null)
            {
                Fx.Impact(tr.point, tr.normal, new Color(0.9f, 0.85f, 0.7f));
            }
            return tr;
        }

        /// <summary>Direction from the muzzle toward whatever the crosshair is pointing at.</summary>
        protected Vector3 MuzzleAimDir(float range = 80f)
        {
            var tr = CombatUtil.Trace(self, self.EyePos, self.AimDir, range, false);
            Vector3 target = tr.point;
            Vector3 d = target - MuzzlePos;
            if (d.sqrMagnitude < 1f || Vector3.Dot(d, self.AimDir) < 0f) return self.AimDir;
            return d.normalized;
        }

        protected Vector3 ProjectileOrigin()
        {
            // spawn just in front of the eye so projectiles never start inside walls the player can see past
            Vector3 eye = self.EyePos;
            Vector3 m = MuzzlePos;
            RaycastHit h;
            if (Physics.Linecast(eye, m, out h, Layers.World, QueryTriggerInteraction.Ignore))
                return eye + self.AimDir * 0.3f;
            return m;
        }

        protected void Recoil(float pitchKick)
        {
            if (self.isPlayer) self.pitch = Mathf.Clamp(self.pitch - pitchKick, -89f, 89f);
        }

        // ------------------------------------------------------------------ AI hooks

        /// <summary>Projectile speed for target leading (0 = hitscan).</summary>
        public virtual float ProjectileSpeed { get { return 0f; } }
        public virtual float EffectiveRange { get { return 30f; } }
        public virtual float PreferredRange { get { return 15f; } }
        public virtual bool AimAtFeet { get { return false; } }
        public virtual float HealRange { get { return 0f; } }
        public virtual bool AttackWithSecondary { get { return false; } }
        /// <summary>Let the kit press ability buttons for a bot.</summary>
        public virtual void BotAbilities(BotBrain bot, ref HeroInput input) { }
    }
}
