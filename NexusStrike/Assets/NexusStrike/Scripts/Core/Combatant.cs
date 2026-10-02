using System.Collections.Generic;
using UnityEngine;

namespace NexusStrike
{
    /// <summary>
    /// A hero in the match: owns health pools, status effects, stats and ultimate charge,
    /// and drives its brain -> motor -> kit update order.
    /// </summary>
    public class Combatant : MonoBehaviour
    {
        public static readonly List<Combatant> All = new List<Combatant>();

        public HeroDefinition def;
        public Team team;
        public bool isPlayer;
        public string displayName;
        public int slot;

        public HeroMotor motor;
        public HeroKit kit;
        public Brain brain;
        public HeroModel visuals;
        public Transform eye;
        public Transform weaponSocket;

        public float health, armor, shield, overHealth;
        public float maxHealth, maxArmor, maxShield;
        public bool alive = true;
        public float respawnAt;
        public float deathTime;
        public Combatant lastKiller;

        public float yaw, pitch;

        // status effects
        float stunUntil, rootUntil, reductionUntil, reductionValue, speedUntil, speedValue, slowUntil, slowValue;
        float overHealthUntil;
        public bool unstoppable;
        public float lastDamagedTime = -99f;
        public Vector3 lastDamageFrom;

        // stats
        public int kills, deaths, eliminations, finalBlows;
        public float damageDone, healingDone, damageBlocked;
        public float ultCharge;

        readonly Dictionary<Combatant, float> recentDamagers = new Dictionary<Combatant, float>();

        public float MaxTotal { get { return maxHealth + maxArmor + maxShield; } }
        public float Total { get { return health + armor + shield; } }
        public float HealthFrac { get { return Total / MaxTotal; } }
        public bool IsStunned { get { return Time.time < stunUntil; } }
        public bool IsRooted { get { return Time.time < rootUntil || IsStunned; } }
        public bool UltReady { get { return ultCharge >= def.ultCost; } }
        public float UltFrac { get { return Mathf.Clamp01(ultCharge / def.ultCost); } }
        public float StunRemaining { get { return Mathf.Max(0f, stunUntil - Time.time); } }

        public Vector3 Feet { get { return transform.position; } }
        public Vector3 ChestPos { get { return transform.position + Vector3.up * def.height * 0.6f; } }
        public Vector3 HeadPos { get { return transform.position + Vector3.up * (def.height - 0.18f); } }
        public Vector3 EyePos { get { return eye.position; } }
        public Vector3 AimDir { get { return eye.forward; } }
        public Vector3 Forward { get { return Quaternion.Euler(0, yaw, 0) * Vector3.forward; } }

        public float SpeedFactor
        {
            get
            {
                float f = 1f;
                if (Time.time < speedUntil) f *= 1f + speedValue;
                if (Time.time < slowUntil) f *= 1f - slowValue;
                return f;
            }
        }

        public float DamageReduction { get { return Time.time < reductionUntil ? reductionValue : 0f; } }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public void Setup(HeroDefinition d, Team t, bool player, string nameTag, int slotIndex)
        {
            def = d;
            team = t;
            isPlayer = player;
            displayName = nameTag;
            slot = slotIndex;
            maxHealth = d.health;
            maxArmor = d.armor;
            maxShield = d.shield;
            ResetPools();
        }

        void ResetPools()
        {
            health = maxHealth;
            armor = maxArmor;
            shield = maxShield;
            overHealth = 0f;
            stunUntil = rootUntil = reductionUntil = speedUntil = slowUntil = 0f;
            recentDamagers.Clear();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var gm = GameManager.I;
            bool canAct = alive && gm != null && gm.CanAct(this);

            HeroInput input = default(HeroInput);
            if (brain != null && alive) input = brain.Think(dt);
            if (!canAct) input = default(HeroInput);

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            eye.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            if (alive)
            {
                motor.Tick(input, dt, canAct && !IsRooted);
                kit.Tick(input, dt, canAct);
                Passive(dt);
                if (transform.position.y < -25f) Kill(null, "Fell");
            }
        }

        void Passive(float dt)
        {
            // shields regenerate after 3s without damage
            if (maxShield > 0f && shield < maxShield && Time.time - lastDamagedTime > 3f)
                shield = Mathf.Min(maxShield, shield + 30f * dt);

            // supports self-heal when out of combat for 1.5s
            if (def.role == HeroRole.Support && Time.time - lastDamagedTime > 1.5f && health < maxHealth)
                health = Mathf.Min(maxHealth, health + 20f * dt);

            if (overHealth > 0f)
            {
                if (Time.time > overHealthUntil) overHealth = 0f;
                else overHealth = Mathf.Max(0f, overHealth - 6f * dt);
            }

            // passive ult gain
            if (!kit.ultActive) AddUltCharge(4f * dt);
        }

        public void AddUltCharge(float amount)
        {
            if (kit != null && kit.ultActive) return;
            bool wasReady = UltReady;
            ultCharge = Mathf.Min(def.ultCost, ultCharge + amount);
            if (!wasReady && UltReady && isPlayer)
            {
                Sfx.Play2D("announce", 0.6f, 1.4f);
                if (GameManager.I != null) GameManager.I.Announce("ULTIMATE READY  [Q]", new Color(1f, 0.85f, 0.3f), 2f);
            }
        }

        /// <summary>Apply damage through the pool order overhealth -> shield -> armor -> health. Returns damage dealt.</summary>
        public float ApplyDamage(Combatant attacker, float amount, bool headshot, string source)
        {
            if (!alive || amount <= 0f) return 0f;
            var gm = GameManager.I;
            if (gm != null && gm.state == MatchState.Ended) return 0f;

            amount *= 1f - DamageReduction;
            if (attacker == this) amount *= 0.5f;
            float dealt = 0f;
            float remaining = amount;

            float take = Mathf.Min(overHealth, remaining);
            overHealth -= take; remaining -= take; dealt += take;

            take = Mathf.Min(shield, remaining);
            shield -= take; remaining -= take; dealt += take;

            if (remaining > 0f && armor > 0f)
            {
                // armor absorbs 30% of incoming damage
                float effective = remaining * 0.7f;
                take = Mathf.Min(armor, effective);
                armor -= take; dealt += take;
                remaining = (effective - take) / 0.7f;
            }

            take = Mathf.Min(health, remaining);
            health -= take; dealt += take;

            lastDamagedTime = Time.time;
            if (attacker != null && attacker != this)
            {
                lastDamageFrom = attacker.ChestPos;
                recentDamagers[attacker] = Time.time;
                attacker.damageDone += dealt;
                attacker.AddUltCharge(dealt);
                if (attacker.kit != null) attacker.kit.OnDealtDamage(this, dealt);
            }
            if (gm != null) gm.OnDamage(attacker, this, dealt, headshot);

            if (health <= 0.01f) Kill(attacker, source);
            return dealt;
        }

        /// <summary>Heal health first, then armor, then shields. Returns amount healed.</summary>
        public float ApplyHeal(Combatant healer, float amount)
        {
            if (!alive || amount <= 0f) return 0f;
            float healed = 0f;
            float t = Mathf.Min(maxHealth - health, amount);
            health += t; amount -= t; healed += t;
            t = Mathf.Min(maxArmor - armor, amount);
            armor += t; amount -= t; healed += t;
            t = Mathf.Min(maxShield - shield, amount);
            shield += t; healed += t;
            if (healer != null && healed > 0f)
            {
                healer.healingDone += healed;
                if (healer != this) healer.AddUltCharge(healed);
            }
            return healed;
        }

        public void AddOverHealth(float amount, float duration)
        {
            overHealth = Mathf.Max(overHealth, amount);
            overHealthUntil = Time.time + duration;
        }

        public void Stun(float duration)
        {
            if (!alive || unstoppable) return;
            stunUntil = Mathf.Max(stunUntil, Time.time + duration);
            if (kit != null) kit.OnStunned();
        }

        public void Root(float duration)
        {
            if (!alive || unstoppable) return;
            rootUntil = Mathf.Max(rootUntil, Time.time + duration);
        }

        public void AddDamageReduction(float value, float duration)
        {
            reductionValue = Time.time < reductionUntil ? Mathf.Max(reductionValue, value) : value;
            reductionUntil = Mathf.Max(reductionUntil, Time.time + duration);
        }

        public void SpeedBoost(float value, float duration)
        {
            speedValue = value;
            speedUntil = Time.time + duration;
        }

        public void Slow(float value, float duration)
        {
            if (unstoppable) return;
            slowValue = value;
            slowUntil = Mathf.Max(slowUntil, Time.time + duration);
        }

        public void Knockback(Vector3 impulse)
        {
            if (!alive || unstoppable) return;
            if (def.role == HeroRole.Tank) impulse *= 0.6f;
            motor.AddImpulse(impulse);
        }

        public void Kill(Combatant killer, string source)
        {
            if (!alive) return;
            alive = false;
            health = 0f;
            deathTime = Time.time;
            deaths++;
            lastKiller = killer;
            kit.OnDeath();
            motor.OnDeath();
            if (visuals != null) visuals.OnDeath();

            var assisters = new List<Combatant>();
            foreach (var kv in recentDamagers)
                if (kv.Key != null && kv.Key != killer && kv.Key.team != team && Time.time - kv.Value < 6f)
                    assisters.Add(kv.Key);
            recentDamagers.Clear();

            Sfx.Play("death", ChestPos, 0.8f);
            if (GameManager.I != null) GameManager.I.OnKill(killer, this, source, assisters);
        }

        public void Respawn(Vector3 pos, float facingYaw)
        {
            ResetPools();
            alive = true;
            yaw = facingYaw;
            pitch = 0f;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            motor.Teleport(pos);
            kit.OnRespawn();
            if (visuals != null) visuals.OnRespawn();
        }

        public bool IsEnemy(Combatant other) { return other != null && other.team != team; }
    }
}
