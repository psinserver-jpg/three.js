using System;
using System.Collections.Generic;
using UnityEngine;

namespace NexusStrike
{
    public class AbilityText
    {
        public string key, name, desc;
        public AbilityText(string key, string name, string desc) { this.key = key; this.name = name; this.desc = desc; }
    }

    public class HeroDefinition
    {
        public string id, name, title, description;
        public HeroRole role;
        public float health, armor, shield;
        public float speed = 5.5f;
        public float radius = 0.4f, height = 1.8f;
        public float ultCost = 1800f;
        public Color color;
        public Type kitType;
        public AbilityText[] abilities;

        public float MaxTotal { get { return health + armor + shield; } }
    }

    /// <summary>All playable heroes. Original characters created for Nexus Strike.</summary>
    public static class HeroRoster
    {
        public static readonly List<HeroDefinition> All = new List<HeroDefinition>();

        static HeroRoster()
        {
            All.Add(new HeroDefinition
            {
                id = "ironclad", name = "IRONCLAD", title = "Frontline Bulwark", role = HeroRole.Tank,
                description = "Heavy assault frame that shields the team behind a hard-light barrier and breaks enemy lines.",
                health = 350, armor = 250, shield = 0, speed = 5.2f, radius = 0.55f, height = 2.15f, ultCost = 1800f,
                color = new Color(0.55f, 0.58f, 0.62f), kitType = typeof(IroncladKit),
                abilities = new[]
                {
                    new AbilityText("LMB", "Rotary Autocannon", "Rapid-fire hitscan cannon with moderate spread."),
                    new AbilityText("RMB", "Bastion Barrier", "Hold to project a 1000 HP barrier. Regenerates when lowered."),
                    new AbilityText("SHIFT", "Rocket Leap", "Launch forward and slam down, damaging and knocking back enemies."),
                    new AbilityText("E", "Fortify", "Take 50% less damage and become unstoppable for 3s."),
                    new AbilityText("Q", "Seismic Overload", "Slam the ground, stunning all nearby enemies for 2.5s."),
                }
            });
            All.Add(new HeroDefinition
            {
                id = "titan", name = "TITAN", title = "Gravity Brawler", role = HeroRole.Tank,
                description = "Close-range dive tank that crashes into the backline and drags enemies together.",
                health = 400, armor = 200, shield = 0, speed = 5.4f, radius = 0.55f, height = 2.1f, ultCost = 1900f,
                color = new Color(0.42f, 0.36f, 0.55f), kitType = typeof(TitanKit),
                abilities = new[]
                {
                    new AbilityText("LMB", "Impact Scatter", "Short-range energy shotgun."),
                    new AbilityText("RMB", "Kinetic Shell", "Gain a 300 HP personal shield for 2.5s."),
                    new AbilityText("SHIFT", "Meteor Drop", "Leap high and crash down at your aim point."),
                    new AbilityText("E", "Shockwave", "Frontal blast that knocks enemies back."),
                    new AbilityText("Q", "Singularity", "Throw a gravity well that pulls enemies in, then detonates."),
                }
            });
            All.Add(new HeroDefinition
            {
                id = "vex", name = "VEX", title = "Phase Skirmisher", role = HeroRole.Damage,
                description = "Agile rifle specialist who blinks around fights and finishes targets.",
                health = 200, armor = 0, shield = 0, speed = 5.7f, ultCost = 1700f,
                color = new Color(0.2f, 0.75f, 0.6f), kitType = typeof(VexKit),
                abilities = new[]
                {
                    new AbilityText("LMB", "Pulse Rifle", "Accurate fully-automatic hitscan rifle. Headshots deal double damage."),
                    new AbilityText("RMB", "Focus Sight", "Zoom in for tighter spread."),
                    new AbilityText("SHIFT", "Blink", "Instantly dash a short distance in your movement direction."),
                    new AbilityText("E", "Frag Grenade", "Bouncing grenade that explodes after a short fuse."),
                    new AbilityText("Q", "Overdrive", "6s: faster fire rate, unlimited ammo and lifesteal."),
                }
            });
            All.Add(new HeroDefinition
            {
                id = "kestrel", name = "KESTREL", title = "Aerial Bombardier", role = HeroRole.Damage,
                description = "Jet-equipped demolitions expert who rains rockets from above.",
                health = 200, armor = 0, shield = 0, speed = 5.5f, ultCost = 2000f,
                color = new Color(0.85f, 0.65f, 0.25f), kitType = typeof(KestrelKit),
                abilities = new[]
                {
                    new AbilityText("LMB", "Rocket Launcher", "Explosive rockets with splash damage."),
                    new AbilityText("RMB", "Hover Jets", "Hold in the air to glide and hover."),
                    new AbilityText("SHIFT", "Jet Boost", "Rocket straight up into the air."),
                    new AbilityText("E", "Concussion Blast", "Knock enemies away with an explosive pulse."),
                    new AbilityText("Q", "Missile Swarm", "Hover and unleash a barrage of mini-rockets."),
                }
            });
            All.Add(new HeroDefinition
            {
                id = "rook", name = "ROOK", title = "Arc Engineer", role = HeroRole.Damage,
                description = "Area-denial specialist who locks down routes with turrets and arc traps.",
                health = 225, armor = 0, shield = 0, speed = 5.5f, ultCost = 1900f,
                color = new Color(0.9f, 0.45f, 0.2f), kitType = typeof(RookKit),
                abilities = new[]
                {
                    new AbilityText("LMB", "Arc Caster", "Fires bouncing plasma orbs."),
                    new AbilityText("RMB", "Overcharge", "Charged single shot that explodes on impact."),
                    new AbilityText("SHIFT", "Sentry Turret", "Deploy an auto-turret that fires at enemies."),
                    new AbilityText("E", "Arc Trap", "Throw a trap that roots the first enemy to touch it."),
                    new AbilityText("Q", "Tesla Field", "Massive electric field that damages and slows enemies."),
                }
            });
            All.Add(new HeroDefinition
            {
                id = "lumen", name = "LUMEN", title = "Radiant Medic", role = HeroRole.Support,
                description = "Beam healer who keeps a single ally alive and protects the whole team with Sanctuary.",
                health = 150, armor = 0, shield = 75, speed = 5.6f, ultCost = 2000f,
                color = new Color(0.95f, 0.92f, 0.75f), kitType = typeof(LumenKit),
                abilities = new[]
                {
                    new AbilityText("LMB", "Mend Beam", "Hold on an ally to tether and heal them continuously."),
                    new AbilityText("RMB", "Spark Bolts", "Fast energy projectiles for self-defense."),
                    new AbilityText("SHIFT", "Phase Glide", "Burst of movement speed."),
                    new AbilityText("E", "Halo Field", "Throw a zone that heals allies inside it."),
                    new AbilityText("Q", "Sanctuary", "Fully heal nearby allies and grant 50% damage reduction."),
                }
            });
            All.Add(new HeroDefinition
            {
                id = "cypress", name = "CYPRESS", title = "Field Botanist", role = HeroRole.Support,
                description = "Long-range support whose thorn darts heal allies and wound enemies.",
                health = 200, armor = 0, shield = 0, speed = 5.5f, ultCost = 1800f,
                color = new Color(0.35f, 0.62f, 0.3f), kitType = typeof(CypressKit),
                abilities = new[]
                {
                    new AbilityText("LMB", "Thorn Rifle", "Hitscan darts: heal allies, damage enemies."),
                    new AbilityText("RMB", "Scope", "Zoom in for long-range shots."),
                    new AbilityText("SHIFT", "Thorn Dash", "Quick evasive dash."),
                    new AbilityText("E", "Vine Snare", "Projectile that roots and damages an enemy."),
                    new AbilityText("Q", "Bloom", "Heal all nearby allies and grant bonus overhealth."),
                }
            });
        }

        public static HeroDefinition Get(string id)
        {
            foreach (var h in All) if (h.id == id) return h;
            return All[0];
        }

        public static List<HeroDefinition> ByRole(HeroRole r)
        {
            var l = new List<HeroDefinition>();
            foreach (var h in All) if (h.role == r) l.Add(h);
            return l;
        }

        public static string RoleName(HeroRole r)
        {
            switch (r)
            {
                case HeroRole.Tank: return "TANK";
                case HeroRole.Damage: return "DAMAGE";
                default: return "SUPPORT";
            }
        }

        public static Color RoleColor(HeroRole r)
        {
            switch (r)
            {
                case HeroRole.Tank: return new Color(0.35f, 0.65f, 1f);
                case HeroRole.Damage: return new Color(1f, 0.45f, 0.35f);
                default: return new Color(0.45f, 1f, 0.55f);
            }
        }
    }
}
