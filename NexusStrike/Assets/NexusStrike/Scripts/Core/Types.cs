using UnityEngine;

namespace NexusStrike
{
    public enum Team { Attack = 0, Defend = 1 }

    public enum HeroRole { Tank, Damage, Support }

    public enum MatchState { MainMenu, HeroSelect, Setup, Playing, Ended }

    public enum Difficulty { Easy, Normal, Hard }

    public enum CrosshairStyle { Dot, Cross, Circle, Wide }

    /// <summary>Per-frame intent produced by a brain (player or bot) and consumed by motor + kit.</summary>
    public struct HeroInput
    {
        public Vector2 move;
        public bool jump;          // pressed this frame
        public bool jumpHeld;
        public bool primary;       // held
        public bool secondary;     // held
        public bool ability1;      // pressed this frame (Shift)
        public bool ability2;      // pressed this frame (E)
        public bool ultimate;      // pressed this frame (Q)
        public bool reload;        // pressed this frame (R)
    }

    public static class Layers
    {
        public const int Characters = 8;
        public const int IgnoreRaycast = 2;

        /// <summary>Static world geometry only (no characters, no FX). Use with QueryTriggerInteraction.Ignore.</summary>
        public static readonly int World = ~((1 << Characters) | (1 << IgnoreRaycast));

        /// <summary>Everything that can stop or receive a shot.</summary>
        public static readonly int Shootable = ~(1 << IgnoreRaycast);
    }

    public static class TeamColors
    {
        public static readonly Color Attack = new Color(0.25f, 0.62f, 1f);
        public static readonly Color Defend = new Color(1f, 0.32f, 0.3f);
        public static readonly Color Ally = new Color(0.3f, 0.85f, 1f);
        public static readonly Color Enemy = new Color(1f, 0.28f, 0.28f);

        public static Color Of(Team t) { return t == Team.Attack ? Attack : Defend; }

        /// <summary>Color relative to the local player's team (allies blue, enemies red).</summary>
        public static Color Relative(Team t)
        {
            var gm = GameManager.I;
            Team mine = gm != null ? gm.playerTeam : Team.Attack;
            return t == mine ? Ally : Enemy;
        }

        public static string Name(Team t) { return t == Team.Attack ? "ATTACK" : "DEFENSE"; }
    }
}
