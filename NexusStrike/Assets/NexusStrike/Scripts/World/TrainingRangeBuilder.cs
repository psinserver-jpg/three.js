using System.Collections.Generic;
using UnityEngine;

namespace NexusStrike
{
    public class TrainingSpot
    {
        public HeroDefinition def;
        public Team team;
        public TrainingBotMode mode;
        public string label;
        public Vector3 position;
        public Vector3 pointB;
        public float yaw;
        public float speedScale = 1f;
        public bool jumpy;
        public Vector3 aggroCenter;
    }

    public class TrainingSign
    {
        public Vector3 position;
        public string text;
        public Color color;
    }

    public class TrainingRange
    {
        public Transform root;
        public SpawnRoom spawn;
        public readonly List<TrainingSpot> spots = new List<TrainingSpot>();
        public readonly List<TrainingSign> signs = new List<TrainingSign>();
        public readonly List<HealthPack> healthPacks = new List<HealthPack>();
    }

    /// <summary>
    /// Builds the NEXUS TRAINING RANGE: a walled sci-fi test hall far from the escort map with
    /// a marked firing lane, moving target rails, a high-ground tower, a duel pit with robots that
    /// shoot back, and an ally bay with injured robots for healing practice.
    /// Layout is authored in local space (lane runs along +Z) and placed via a rotated root.
    /// </summary>
    public class TrainingRangeBuilder
    {
        static readonly Color Floor = new Color(0.6f, 0.63f, 0.68f);
        static readonly Color Wall = new Color(0.82f, 0.85f, 0.9f);
        static readonly Color WallDark = new Color(0.36f, 0.4f, 0.47f);
        static readonly Color Orange = new Color(1f, 0.6f, 0.2f);
        static readonly Color Cyan = new Color(0.35f, 0.85f, 1f);
        static readonly Color Line = new Color(0.95f, 0.95f, 1f, 0.85f);

        readonly TrainingRange range = new TrainingRange();
        Transform root;

        public static readonly Vector3 Origin = new Vector3(0f, 0f, -330f);
        public const float RootYaw = 180f;

        public static TrainingRange Build()
        {
            return new TrainingRangeBuilder().Generate();
        }

        Vector3 W(float x, float y, float z) { return root.TransformPoint(new Vector3(x, y, z)); }

        TrainingRange Generate()
        {
            root = new GameObject("TrainingRange").transform;
            root.position = Origin;
            root.rotation = Quaternion.Euler(0f, RootYaw, 0f);
            range.root = root;

            BuildShell();
            BuildLane();
            BuildMovingRails();
            BuildTower();
            BuildDuelPit();
            BuildAllyBay();

            range.spawn = new SpawnRoom { team = Team.Attack, phase = 0, center = W(0f, 0f, -4f), yaw = RootYaw };
            Sign(0f, 2.6f, -9f, "SPAWN  ·  [H] CHANGE HERO ANYWHERE", Cyan);

            Physics.SyncTransforms();
            var packs = new GameObject("TrainingHealthPacks").transform;
            packs.SetParent(root, false);
            range.healthPacks.Add(HealthPack.Create(W(-12f, 0f, -6f), false, packs));
            range.healthPacks.Add(HealthPack.Create(W(12f, 0f, -6f), true, packs));
            range.healthPacks.Add(HealthPack.Create(W(-36f, 0f, 70f), true, packs));
            return range;
        }

        // ------------------------------------------------------------------ helpers

        Transform Box(float x, float y, float z, float sx, float sy, float sz, Color c, bool collider = true, bool unlit = false, float yaw = 0f, float pitch = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            var t = go.transform;
            t.SetParent(root, false);
            t.localPosition = new Vector3(x, y, z);
            t.localRotation = Quaternion.Euler(pitch, yaw, 0f);
            t.localScale = new Vector3(sx, sy, sz);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = unlit ? MaterialLib.Unlit(c) : MaterialLib.Lit(c);
            if (unlit) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }

        void Sign(float x, float y, float z, string text, Color c)
        {
            range.signs.Add(new TrainingSign { position = W(x, y, z), text = text, color = c });
        }

        void Bot(HeroDefinition def, Team team, TrainingBotMode mode, string label, float x, float z, float y = 0f)
        {
            range.spots.Add(new TrainingSpot
            {
                def = def, team = team, mode = mode, label = label,
                position = W(x, y + 0.1f, z), pointB = W(x, y + 0.1f, z), yaw = RootYaw + 180f
            });
        }

        // ------------------------------------------------------------------ sections

        void BuildShell()
        {
            Box(0f, -0.5f, 55f, 86f, 1f, 136f, Floor);
            // floor grid
            for (int i = -4; i <= 4; i++) Box(i * 10f, 0.012f, 55f, 0.06f, 0.02f, 134f, new Color(0.5f, 0.53f, 0.58f), false);
            for (int j = 0; j <= 13; j++) Box(0f, 0.012f, -12f + j * 10f, 84f, 0.02f, 0.06f, new Color(0.5f, 0.53f, 0.58f), false);
            // perimeter
            Box(-43f, 3.5f, 55f, 1f, 7f, 136f, Wall);
            Box(43f, 3.5f, 55f, 1f, 7f, 136f, Wall);
            Box(0f, 3.5f, -13f, 86f, 7f, 1f, Wall);
            Box(0f, 3.5f, 123f, 86f, 7f, 1f, Wall);
            Box(-42.4f, 6.2f, 55f, 0.2f, 0.25f, 134f, Orange, false, true);
            Box(42.4f, 6.2f, 55f, 0.2f, 0.25f, 134f, Orange, false, true);
            Box(0f, 6.2f, 122.4f, 84f, 0.25f, 0.2f, Orange, false, true);
            Box(0f, 6.2f, -12.4f, 84f, 0.25f, 0.2f, Orange, false, true);
            for (int k = 0; k < 7; k++)
            {
                float z = k * 20f;
                Box(-42.3f, 3.5f, z, 1.4f, 7f, 1.4f, WallDark);
                Box(42.3f, 3.5f, z, 1.4f, 7f, 1.4f, WallDark);
            }
            // spawn pad + back-wall logo panel
            Box(0f, 0.02f, -4f, 10f, 0.03f, 8f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f), false, true);
            Box(0f, 3.4f, -12.45f, 18f, 3.2f, 0.1f, WallDark, false);
            Box(0f, 3.4f, -12.38f, 16f, 0.25f, 0.05f, Cyan, false, true);
            Box(0f, 2.4f, -12.38f, 10f, 0.15f, 0.05f, Orange, false, true);
            Box(0f, 4.4f, -12.38f, 10f, 0.15f, 0.05f, Orange, false, true);
        }

        void BuildLane()
        {
            Sign(0f, 3.4f, 4f, "FIRING LANE", Orange);
            // lane edge rails
            Box(-13f, 0.2f, 38f, 0.3f, 0.4f, 68f, WallDark);
            Box(13f, 0.2f, 38f, 0.3f, 0.4f, 68f, WallDark);
            for (int d = 10; d <= 70; d += 10)
            {
                Box(0f, 0.02f, d, 25.6f, 0.025f, 0.18f, Line, false, true);
                Box(-12.4f, 0.6f, d, 0.5f, 1.2f, 0.5f, Orange);
                Box(12.4f, 0.6f, d, 0.5f, 1.2f, 0.5f, Orange);
                Sign(-12.4f, 1.6f, d, d + " m", Color.white);
                Sign(12.4f, 1.6f, d, d + " m", Color.white);
            }
            // peek walls
            Box(-8f, 1.1f, 24f, 3.5f, 2.2f, 0.6f, Wall);
            Box(8f, 1.1f, 44f, 3.5f, 2.2f, 0.6f, Wall);
            Box(-4f, 0.6f, 56f, 2.5f, 1.2f, 0.6f, Wall);

            var bot = HeroRoster.TrainingBot;
            Bot(bot, Team.Defend, TrainingBotMode.Static, "TARGET 10", 0f, 10f);
            Bot(bot, Team.Defend, TrainingBotMode.Static, "TARGET 20", -4f, 20f);
            Bot(bot, Team.Defend, TrainingBotMode.Static, "TARGET 30", 4f, 30f);
            Bot(bot, Team.Defend, TrainingBotMode.Static, "TARGET 40", -2f, 40f);
            Bot(bot, Team.Defend, TrainingBotMode.Static, "TARGET 50", 5f, 50f);
            Bot(bot, Team.Defend, TrainingBotMode.Static, "TARGET 60", 0f, 62f);
            Bot(HeroRoster.HeavyTrainingBot, Team.Defend, TrainingBotMode.Static, "HEAVY", 7f, 18f);
            Sign(7f, 3.2f, 18f, "ARMORED TARGET", new Color(1f, 0.85f, 0.4f));
        }

        void BuildMovingRails()
        {
            Sign(27f, 3.4f, 8f, "MOVING TARGETS", Orange);
            float[] zs = { 18f, 32f, 46f };
            float[] speeds = { 0.45f, 0.8f, 1f };
            string[] labels = { "SLOW", "STRAFE", "JUMPER" };
            for (int i = 0; i < zs.Length; i++)
            {
                float z = zs[i];
                Box(27f, 0.02f, z, 20f, 0.03f, 0.25f, new Color(Orange.r, Orange.g, Orange.b, 0.9f), false, true);
                Box(16.5f, 0.4f, z, 0.6f, 0.8f, 0.6f, WallDark);
                Box(37.5f, 0.4f, z, 0.6f, 0.8f, 0.6f, WallDark);
                range.spots.Add(new TrainingSpot
                {
                    def = HeroRoster.TrainingBot, team = Team.Defend, mode = TrainingBotMode.Strafe, label = labels[i],
                    position = W(18.5f, 0.1f, z), pointB = W(35.5f, 0.1f, z), yaw = RootYaw + 180f,
                    speedScale = speeds[i], jumpy = i == 2
                });
                Sign(39.5f, 1.8f, z, labels[i], Color.white);
            }
        }

        void BuildTower()
        {
            // platform with a ramp descending toward the spawn
            float h = 4f;
            Box(28f, h / 2f, 72f, 9f, h, 9f, WallDark);
            Box(28f, h + 0.05f, 72f, 9.2f, 0.1f, 9.2f, Wall, false);
            Box(28f, h + 0.6f, 76.3f, 9f, 1.1f, 0.4f, Wall);
            float L = h / Mathf.Tan(26f * Mathf.Deg2Rad);
            float len = Mathf.Sqrt(L * L + h * h);
            float ang = Mathf.Atan2(h, L) * Mathf.Rad2Deg;
            Box(28f, h / 2f - 0.2f, 67.5f - L / 2f, 3f, 0.4f, len + 0.3f, Wall, true, false, 0f, -ang);
            Box(28f, h + 0.3f, 67.6f, 9f, 0.05f, 0.2f, Orange, false, true);
            Bot(HeroRoster.TrainingBot, Team.Defend, TrainingBotMode.Static, "TOWER", 28f, 74f, h);
            Sign(28f, h + 3f, 72f, "HIGH GROUND", Orange);
            // far-wall static targets for long range
            Bot(HeroRoster.TrainingBot, Team.Defend, TrainingBotMode.Static, "TARGET 100", -6f, 104f);
            Bot(HeroRoster.TrainingBot, Team.Defend, TrainingBotMode.Static, "TARGET 100", 6f, 104f);
            Sign(0f, 3.4f, 104f, "100 m", Color.white);
            Box(0f, 0.02f, 104f, 26f, 0.025f, 0.18f, Line, false, true);
        }

        void BuildDuelPit()
        {
            // separated from the lane by a wall with two doorways
            Box(-16f, 1.6f, 30f, 0.6f, 3.2f, 6f, Wall);
            Box(-16f, 1.6f, 49f, 0.6f, 3.2f, 20f, Wall);
            Box(-16f, 1.6f, 72f, 0.6f, 3.2f, 12f, Wall);
            Box(-29f, 1.6f, 27f, 26f, 3.2f, 0.6f, Wall);
            Box(-29f, 0.02f, 52f, 25f, 0.03f, 49f, new Color(1f, 0.35f, 0.3f, 0.12f), false, true);
            // cover
            Box(-22f, 0.7f, 38f, 2.2f, 1.4f, 2.2f, WallDark);
            Box(-31f, 0.7f, 44f, 3.5f, 1.4f, 1f, WallDark);
            Box(-25f, 1.1f, 56f, 1f, 2.2f, 3.5f, WallDark);
            Box(-35f, 0.7f, 58f, 2f, 1.4f, 2f, WallDark);
            Box(-20f, 0.7f, 66f, 3f, 1.4f, 1f, WallDark);
            Sign(-16f, 3.8f, 36.5f, "DUEL PIT  ·  ROBOTS FIGHT BACK", new Color(1f, 0.45f, 0.4f));

            Vector3 center = W(-29f, 0f, 52f);
            float[,] posts = { { -27f, 64f }, { -33f, 52f }, { -21f, 48f } };
            for (int i = 0; i < posts.GetLength(0); i++)
            {
                range.spots.Add(new TrainingSpot
                {
                    def = HeroRoster.TrainingBot, team = Team.Defend, mode = TrainingBotMode.Sentinel, label = "SENTINEL",
                    position = W(posts[i, 0], 0.1f, posts[i, 1]), pointB = W(posts[i, 0], 0.1f, posts[i, 1]),
                    yaw = RootYaw + 180f, aggroCenter = center
                });
            }
        }

        void BuildAllyBay()
        {
            Box(-28f, 0.02f, 8f, 22f, 0.03f, 20f, new Color(0.4f, 1f, 0.5f, 0.12f), false, true);
            Box(-17f, 0.6f, 10f, 0.4f, 1.2f, 12f, Wall);
            Sign(-28f, 3.2f, 0f, "ALLY BAY  ·  PRACTICE HEALING", new Color(0.5f, 1f, 0.55f));
            float[,] posts = { { -23f, 6f }, { -29f, 12f }, { -35f, 5f } };
            for (int i = 0; i < posts.GetLength(0); i++)
            {
                range.spots.Add(new TrainingSpot
                {
                    def = HeroRoster.TrainingBot, team = Team.Attack, mode = TrainingBotMode.Ally, label = "ALLY " + (i + 1),
                    position = W(posts[i, 0], 0.1f, posts[i, 1]), pointB = W(posts[i, 0], 0.1f, posts[i, 1]),
                    yaw = RootYaw
                });
            }
        }
    }
}
