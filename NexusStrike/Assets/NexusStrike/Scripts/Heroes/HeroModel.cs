using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NexusStrike
{
    /// <summary>
    /// Procedural, rigged hero body built from primitives.
    ///
    /// Every hero shares one skeleton (pelvis, spine, chest, neck, head, two 3-bone arms, two 3-bone legs)
    /// and gets a hero-specific outfit. Parts are declared one per line with J (joint), R (rest pose),
    /// P (part) and Ring calls in a strict format so the same data can also be parsed by external tools
    /// (e.g. a quick three.js preview renderer).
    /// Units are metres for a 1.8 m reference hero; the root is scaled to the hero's real height.
    /// </summary>
    public class HeroModel : MonoBehaviour
    {
        public enum B
        {
            Root, Hips, Spine, Chest, Neck, Head, Back, Drone, Orbit,
            UpperArmL, ForeArmL, HandL, UpperArmR, ForeArmR, HandR,
            ThighL, ShinL, FootL, ThighR, ShinR, FootR
        }

        public enum S { Cube, Sphere, Capsule, Cylinder }

        /// <summary>Palette slots. Glow and Team are unlit (emissive look); Team is ally-blue / enemy-red.</summary>
        public enum K { Main, Dark, Accent, Trim, Cloth, Glow, Skin, Hair, Metal, Black, Team }

        const int BoneCount = 21;
        const float ReferenceHeight = 1.8f;

        Combatant self;
        HeroDefinition def;
        Team team;
        int layer;
        readonly Transform[] bones = new Transform[BoneCount];
        readonly Vector3[] restPos = new Vector3[BoneCount];
        readonly Quaternion[] restRot = new Quaternion[BoneCount];
        Color[] palette;
        readonly List<Renderer> renderers = new List<Renderer>();
        float walkPhase;
        bool firstPerson;
        float deathAnim;
        bool previewMode;

        // ------------------------------------------------------------------ construction

        public static HeroModel Build(Combatant c)
        {
            var m = Create(c.transform, c.def, c.team, 0);
            m.self = c;
            return m;
        }

        /// <summary>Standalone model (no combatant) used for hero select portraits.</summary>
        public static HeroModel BuildPreview(Transform parent, HeroDefinition def, Team team, int layer)
        {
            var m = Create(parent, def, team, layer);
            m.previewMode = true;
            return m;
        }

        static HeroModel Create(Transform parent, HeroDefinition def, Team team, int layer)
        {
            var root = new GameObject("Model");
            root.layer = layer;
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * (def.height / ReferenceHeight);
            var m = root.AddComponent<HeroModel>();
            m.def = def;
            m.team = team;
            m.layer = layer;
            m.palette = Palette(def.id);
            m.Construct();
            m.GetComponentsInChildren(true, m.renderers);
            return m;
        }

        void Construct()
        {
            var rootBone = new GameObject("Body").transform;
            rootBone.gameObject.layer = layer;
            rootBone.SetParent(transform, false);
            bones[(int)B.Root] = rootBone;

            BuildSkeleton();
            switch (def.id)
            {
                case "ironclad": Skel_ironclad(); break;
                case "titan": Skel_titan(); break;
            }
            ApplyPose();
            Build_base();
            switch (def.id)
            {
                case "ironclad": Build_ironclad(); break;
                case "titan": Build_titan(); break;
                case "vex": Build_vex(); break;
                case "kestrel": Build_kestrel(); break;
                case "rook": Build_rook(); break;
                case "lumen": Build_lumen(); break;
                case "cypress": Build_cypress(); break;
            }
            for (int i = 0; i < BoneCount; i++)
            {
                if (bones[i] == null) continue;
                restPos[i] = bones[i].localPosition;
                restRot[i] = bones[i].localRotation;
            }
        }

        /// <summary>Joint: create (or move) bone b under parent at a local offset.</summary>
        void J(B b, B parent, float x, float y, float z)
        {
            var t = bones[(int)b];
            if (t == null)
            {
                t = new GameObject(b.ToString()).transform;
                t.gameObject.layer = layer;
                bones[(int)b] = t;
            }
            t.SetParent(bones[(int)parent], false);
            t.localPosition = new Vector3(x, y, z);
        }

        /// <summary>Rest rotation of a bone (Euler degrees).</summary>
        void R(B b, float x, float y, float z)
        {
            if (bones[(int)b] != null) bones[(int)b].localRotation = Quaternion.Euler(x, y, z);
        }

        /// <summary>Part: primitive s on bone b at local position, scale, palette slot and optional rotation.</summary>
        void P(B b, S s, float x, float y, float z, float sx, float sy, float sz, K k, float rx = 0f, float ry = 0f, float rz = 0f)
        {
            PrimitiveType type = s == S.Cube ? PrimitiveType.Cube : s == S.Sphere ? PrimitiveType.Sphere :
                s == S.Capsule ? PrimitiveType.Capsule : PrimitiveType.Cylinder;
            bool unlit = k == K.Glow || k == K.Team;
            var t = ModelUtil.Part(type, bones[(int)b], new Vector3(x, y, z), new Vector3(sx, sy, sz), ColorOf(k), unlit, new Vector3(rx, ry, rz));
            t.gameObject.layer = layer;
        }

        /// <summary>Ring of small cubes. axis 0 = ring in the XY plane (faces forward/back), 1 = XZ plane (horizontal).</summary>
        void Ring(B b, float x, float y, float z, float radius, int count, float size, K k, int axis)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                float deg = a * Mathf.Rad2Deg;
                if (axis == 0)
                    P(b, S.Cube, x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius, z, size * 1.6f, size, size, k, 0f, 0f, deg + 90f);
                else
                    P(b, S.Cube, x + Mathf.Cos(a) * radius, y, z + Mathf.Sin(a) * radius, size * 1.6f, size, size, k, 0f, -deg + 90f, 0f);
            }
        }

        Color ColorOf(K k)
        {
            switch (k)
            {
                case K.Team: return TeamColors.Relative(team);
                case K.Metal: return Hex("5B6168");
                case K.Black: return Hex("1E2126");
                default: return palette[(int)k];
            }
        }

        static Color Hex(string h)
        {
            Color c;
            ColorUtility.TryParseHtmlString("#" + h, out c);
            return c;
        }

        /// <summary>main, dark, accent, trim, cloth, glow, skin, hair</summary>
        static Color[] Palette(string id)
        {
            string[] p;
            switch (id)
            {
                case "ironclad": p = new[] { "717A84", "2F343B", "E3A33A", "BCC3CA", "3B4048", "FFB347", "C99A7A", "3A2E26" }; break;
                case "titan": p = new[] { "5A4E7A", "26223A", "9B7BFF", "C9C3DD", "34304A", "B48CFF", "8D5A3E", "1A1512" }; break;
                case "vex": p = new[] { "2FB89A", "23272D", "E8EEF0", "9AA4AD", "2E343C", "5CFFE0", "E1B899", "1C1F24" }; break;
                case "kestrel": p = new[] { "D98A2B", "3A3530", "F2E3C2", "8A7F70", "7C6A4E", "FF8A3D", "C08A68", "4A3426" }; break;
                case "rook": p = new[] { "E06A2C", "2E2A28", "F2C14E", "9C9590", "5B4A3C", "7FD4FF", "A4704F", "1F1A17" }; break;
                case "lumen": p = new[] { "26325E", "1A1F33", "E9C46A", "F3EBD6", "32406F", "FFD27A", "EBC6A8", "D9D2C5" }; break;
                case "cypress": p = new[] { "5E7F3A", "3B2F22", "C9A55A", "E7DCC2", "6B5A3F", "9CFF7A", "7A5236", "2A1E14" }; break;
                default: p = new[] { "888888", "333333", "CCCCCC", "AAAAAA", "555555", "FFFFFF", "D0A080", "222222" }; break;
            }
            var c = new Color[p.Length];
            for (int i = 0; i < p.Length; i++) c[i] = Hex(p[i]);
            return c;
        }

        // ------------------------------------------------------------------ skeleton + pose

        void BuildSkeleton()
        {
            J(B.Hips, B.Root, 0f, 0.95f, 0f);
            J(B.Spine, B.Hips, 0f, 0.08f, 0f);
            J(B.Chest, B.Spine, 0f, 0.24f, 0f);
            J(B.Neck, B.Chest, 0f, 0.27f, 0f);
            J(B.Head, B.Neck, 0f, 0.07f, 0f);
            J(B.Back, B.Chest, 0f, 0.12f, -0.15f);
            J(B.Drone, B.Chest, 0.36f, 0.52f, -0.12f);
            J(B.Orbit, B.Chest, 0f, 0.12f, 0f);
            J(B.UpperArmL, B.Chest, -0.23f, 0.24f, 0f);
            J(B.ForeArmL, B.UpperArmL, 0f, -0.28f, 0f);
            J(B.HandL, B.ForeArmL, 0f, -0.25f, 0f);
            J(B.UpperArmR, B.Chest, 0.23f, 0.24f, 0f);
            J(B.ForeArmR, B.UpperArmR, 0f, -0.28f, 0f);
            J(B.HandR, B.ForeArmR, 0f, -0.25f, 0f);
            J(B.ThighL, B.Hips, -0.11f, -0.03f, 0f);
            J(B.ShinL, B.ThighL, 0f, -0.43f, 0f);
            J(B.FootL, B.ShinL, 0f, -0.44f, 0f);
            J(B.ThighR, B.Hips, 0.11f, -0.03f, 0f);
            J(B.ShinR, B.ThighR, 0f, -0.43f, 0f);
            J(B.FootR, B.ShinR, 0f, -0.44f, 0f);
        }

        void ApplyPose()
        {
            R(B.UpperArmR, -30f, 0f, 6f);
            R(B.ForeArmR, -62f, 0f, 0f);
            R(B.UpperArmL, -60f, 0f, 34f);
            R(B.ForeArmL, -14f, 0f, 0f);
            R(B.ThighL, -4f, 0f, 3f);
            R(B.ThighR, 4f, 0f, -3f);
            R(B.ShinL, 6f, 0f, 0f);
            R(B.ShinR, 6f, 0f, 0f);
        }

        void Skel_ironclad()
        {
            J(B.UpperArmL, B.Chest, -0.3f, 0.22f, 0f);
            J(B.UpperArmR, B.Chest, 0.3f, 0.22f, 0f);
            J(B.ThighL, B.Hips, -0.14f, -0.03f, 0f);
            J(B.ThighR, B.Hips, 0.14f, -0.03f, 0f);
        }

        void Skel_titan()
        {
            J(B.UpperArmL, B.Chest, -0.31f, 0.2f, 0f);
            J(B.UpperArmR, B.Chest, 0.31f, 0.2f, 0f);
            J(B.ThighL, B.Hips, -0.14f, -0.03f, 0f);
            J(B.ThighR, B.Hips, 0.14f, -0.03f, 0f);
        }

        // ------------------------------------------------------------------ shared undersuit

        void Build_base()
        {
            P(B.Hips, S.Cube, 0f, -0.02f, 0f, 0.32f, 0.18f, 0.21f, K.Cloth);
            P(B.Spine, S.Cube, 0f, 0.1f, 0f, 0.29f, 0.22f, 0.19f, K.Cloth);
            P(B.Chest, S.Cube, 0f, 0.13f, 0f, 0.42f, 0.32f, 0.25f, K.Cloth);
            P(B.Neck, S.Cylinder, 0f, 0.03f, 0f, 0.1f, 0.05f, 0.1f, K.Skin);
            P(B.Head, S.Sphere, 0f, 0.1f, 0f, 0.21f, 0.25f, 0.23f, K.Skin);
            P(B.Head, S.Cube, 0f, 0.1f, 0.112f, 0.13f, 0.025f, 0.01f, K.Black);
            P(B.UpperArmL, S.Capsule, 0f, -0.13f, 0f, 0.12f, 0.16f, 0.12f, K.Cloth);
            P(B.UpperArmR, S.Capsule, 0f, -0.13f, 0f, 0.12f, 0.16f, 0.12f, K.Cloth);
            P(B.ForeArmL, S.Capsule, 0f, -0.12f, 0f, 0.1f, 0.14f, 0.1f, K.Cloth);
            P(B.ForeArmR, S.Capsule, 0f, -0.12f, 0f, 0.1f, 0.14f, 0.1f, K.Cloth);
            P(B.HandL, S.Cube, 0f, -0.04f, 0f, 0.08f, 0.1f, 0.07f, K.Black);
            P(B.HandR, S.Cube, 0f, -0.04f, 0f, 0.08f, 0.1f, 0.07f, K.Black);
            P(B.ThighL, S.Capsule, 0f, -0.2f, 0f, 0.16f, 0.23f, 0.16f, K.Cloth);
            P(B.ThighR, S.Capsule, 0f, -0.2f, 0f, 0.16f, 0.23f, 0.16f, K.Cloth);
            P(B.ShinL, S.Capsule, 0f, -0.21f, 0f, 0.13f, 0.22f, 0.13f, K.Cloth);
            P(B.ShinR, S.Capsule, 0f, -0.21f, 0f, 0.13f, 0.22f, 0.13f, K.Cloth);
            P(B.FootL, S.Cube, 0f, 0f, 0.05f, 0.12f, 0.09f, 0.26f, K.Black);
            P(B.FootR, S.Cube, 0f, 0f, 0.05f, 0.12f, 0.09f, 0.26f, K.Black);
        }

        // ------------------------------------------------------------------ heroes

        /// <summary>IRONCLAD — heavy assault frame: slab armor, huge pauldrons, reactor backpack.</summary>
        void Build_ironclad()
        {
            P(B.Chest, S.Cube, 0f, 0.15f, 0.02f, 0.6f, 0.42f, 0.36f, K.Main);
            P(B.Chest, S.Cube, 0f, 0.2f, 0.19f, 0.36f, 0.2f, 0.04f, K.Trim);
            P(B.Chest, S.Cylinder, 0f, 0.2f, 0.215f, 0.14f, 0.015f, 0.14f, K.Team, 90f, 0f, 0f);
            P(B.Chest, S.Cube, -0.22f, 0.13f, 0.195f, 0.06f, 0.3f, 0.02f, K.Accent);
            P(B.Chest, S.Cube, 0.22f, 0.13f, 0.195f, 0.06f, 0.3f, 0.02f, K.Accent);
            P(B.Chest, S.Cube, 0f, 0.37f, 0f, 0.42f, 0.1f, 0.34f, K.Dark);
            P(B.Chest, S.Sphere, -0.4f, 0.3f, 0f, 0.36f, 0.3f, 0.42f, K.Main);
            P(B.Chest, S.Sphere, 0.4f, 0.3f, 0f, 0.36f, 0.3f, 0.42f, K.Main);
            P(B.Chest, S.Cube, -0.42f, 0.42f, 0f, 0.34f, 0.06f, 0.44f, K.Trim, 0f, 0f, 14f);
            P(B.Chest, S.Cube, 0.42f, 0.42f, 0f, 0.34f, 0.06f, 0.44f, K.Trim, 0f, 0f, -14f);
            P(B.Chest, S.Cube, -0.42f, 0.36f, 0.2f, 0.2f, 0.05f, 0.02f, K.Accent, 0f, 0f, 14f);
            P(B.Chest, S.Cube, 0.42f, 0.36f, 0.2f, 0.2f, 0.05f, 0.02f, K.Accent, 0f, 0f, -14f);
            P(B.Spine, S.Cube, 0f, 0.08f, 0.03f, 0.4f, 0.14f, 0.27f, K.Dark);
            P(B.Spine, S.Cube, 0f, 0.08f, 0.165f, 0.2f, 0.1f, 0.02f, K.Trim);
            P(B.Hips, S.Cube, 0f, 0.03f, 0f, 0.46f, 0.1f, 0.3f, K.Dark);
            P(B.Hips, S.Cube, -0.13f, -0.13f, 0.15f, 0.15f, 0.2f, 0.04f, K.Main, -10f, 0f, 0f);
            P(B.Hips, S.Cube, 0.13f, -0.13f, 0.15f, 0.15f, 0.2f, 0.04f, K.Main, -10f, 0f, 0f);
            P(B.Hips, S.Cube, -0.24f, -0.12f, 0f, 0.04f, 0.22f, 0.24f, K.Main, 0f, 0f, 8f);
            P(B.Hips, S.Cube, 0.24f, -0.12f, 0f, 0.04f, 0.22f, 0.24f, K.Main, 0f, 0f, -8f);
            P(B.Head, S.Cube, 0f, 0.1f, 0f, 0.3f, 0.3f, 0.32f, K.Main);
            P(B.Head, S.Cube, 0f, 0.13f, 0.163f, 0.24f, 0.05f, 0.02f, K.Team);
            P(B.Head, S.Cube, 0f, 0.0f, 0.15f, 0.26f, 0.1f, 0.06f, K.Dark);
            P(B.Head, S.Cube, 0f, 0.27f, -0.02f, 0.05f, 0.07f, 0.3f, K.Accent);
            P(B.Head, S.Cylinder, -0.16f, 0.1f, 0f, 0.1f, 0.03f, 0.1f, K.Dark, 0f, 0f, 90f);
            P(B.Head, S.Cylinder, 0.16f, 0.1f, 0f, 0.1f, 0.03f, 0.1f, K.Dark, 0f, 0f, 90f);
            P(B.UpperArmL, S.Cylinder, 0f, -0.13f, 0f, 0.18f, 0.13f, 0.18f, K.Dark);
            P(B.UpperArmR, S.Cylinder, 0f, -0.13f, 0f, 0.18f, 0.13f, 0.18f, K.Dark);
            P(B.ForeArmL, S.Cube, 0f, -0.12f, 0f, 0.21f, 0.27f, 0.22f, K.Main);
            P(B.ForeArmR, S.Cube, 0f, -0.12f, 0f, 0.21f, 0.27f, 0.22f, K.Main);
            P(B.ForeArmL, S.Cube, 0f, -0.04f, 0.112f, 0.14f, 0.04f, 0.01f, K.Glow);
            P(B.ForeArmR, S.Cube, 0f, -0.04f, 0.112f, 0.14f, 0.04f, 0.01f, K.Glow);
            P(B.HandL, S.Cube, 0f, -0.05f, 0f, 0.15f, 0.13f, 0.13f, K.Dark);
            P(B.HandR, S.Cube, 0f, -0.05f, 0f, 0.15f, 0.13f, 0.13f, K.Dark);
            P(B.ThighL, S.Cube, 0f, -0.2f, 0.02f, 0.23f, 0.33f, 0.25f, K.Main);
            P(B.ThighR, S.Cube, 0f, -0.2f, 0.02f, 0.23f, 0.33f, 0.25f, K.Main);
            P(B.ShinL, S.Sphere, 0f, 0f, 0.07f, 0.17f, 0.17f, 0.17f, K.Trim);
            P(B.ShinR, S.Sphere, 0f, 0f, 0.07f, 0.17f, 0.17f, 0.17f, K.Trim);
            P(B.ShinL, S.Cube, 0f, -0.22f, 0.02f, 0.2f, 0.36f, 0.23f, K.Main);
            P(B.ShinR, S.Cube, 0f, -0.22f, 0.02f, 0.2f, 0.36f, 0.23f, K.Main);
            P(B.ShinL, S.Cube, 0f, -0.16f, 0.137f, 0.06f, 0.2f, 0.02f, K.Accent);
            P(B.ShinR, S.Cube, 0f, -0.16f, 0.137f, 0.06f, 0.2f, 0.02f, K.Accent);
            P(B.FootL, S.Cube, 0f, 0f, 0.06f, 0.21f, 0.13f, 0.36f, K.Dark);
            P(B.FootR, S.Cube, 0f, 0f, 0.06f, 0.21f, 0.13f, 0.36f, K.Dark);
            P(B.Back, S.Cube, 0f, 0f, -0.06f, 0.5f, 0.52f, 0.24f, K.Dark);
            P(B.Back, S.Cube, 0f, 0.05f, -0.185f, 0.3f, 0.3f, 0.02f, K.Metal);
            P(B.Back, S.Cube, 0f, 0.12f, -0.197f, 0.24f, 0.03f, 0.01f, K.Glow);
            P(B.Back, S.Cube, 0f, 0.04f, -0.197f, 0.24f, 0.03f, 0.01f, K.Glow);
            P(B.Back, S.Cube, 0f, -0.04f, -0.197f, 0.24f, 0.03f, 0.01f, K.Glow);
            P(B.Back, S.Cylinder, -0.16f, 0.32f, -0.1f, 0.11f, 0.16f, 0.11f, K.Metal);
            P(B.Back, S.Cylinder, 0.16f, 0.32f, -0.1f, 0.11f, 0.16f, 0.11f, K.Metal);
            P(B.Back, S.Cylinder, -0.16f, 0.49f, -0.1f, 0.08f, 0.015f, 0.08f, K.Glow);
            P(B.Back, S.Cylinder, 0.16f, 0.49f, -0.1f, 0.08f, 0.015f, 0.08f, K.Glow);
        }

        /// <summary>TITAN — gravity exosuit: barrel chest, orb shoulders, gauntlets, orbiting graviton spheres.</summary>
        void Build_titan()
        {
            P(B.Chest, S.Sphere, 0f, 0.15f, 0.02f, 0.62f, 0.5f, 0.44f, K.Main);
            P(B.Chest, S.Cube, 0f, 0.2f, 0.2f, 0.34f, 0.18f, 0.06f, K.Trim);
            P(B.Chest, S.Sphere, 0f, 0.2f, 0.235f, 0.13f, 0.13f, 0.06f, K.Glow);
            P(B.Chest, S.Cube, 0f, 0.36f, -0.02f, 0.4f, 0.1f, 0.32f, K.Dark);
            P(B.Chest, S.Sphere, -0.38f, 0.26f, 0f, 0.36f, 0.34f, 0.36f, K.Main);
            P(B.Chest, S.Sphere, 0.38f, 0.26f, 0f, 0.36f, 0.34f, 0.36f, K.Main);
            P(B.Chest, S.Cylinder, -0.38f, 0.26f, 0f, 0.42f, 0.012f, 0.42f, K.Accent, 0f, 0f, 35f);
            P(B.Chest, S.Cylinder, 0.38f, 0.26f, 0f, 0.42f, 0.012f, 0.42f, K.Accent, 0f, 0f, -35f);
            P(B.Spine, S.Cube, 0f, 0.08f, 0.02f, 0.38f, 0.16f, 0.26f, K.Dark);
            P(B.Hips, S.Cube, 0f, 0.03f, 0f, 0.46f, 0.12f, 0.32f, K.Dark);
            P(B.Hips, S.Sphere, 0f, 0.03f, 0.165f, 0.1f, 0.1f, 0.04f, K.Glow);
            P(B.Hips, S.Cube, 0f, -0.14f, 0.15f, 0.22f, 0.22f, 0.04f, K.Cloth, -8f, 0f, 0f);
            P(B.Head, S.Sphere, 0f, 0.11f, 0f, 0.3f, 0.3f, 0.31f, K.Main);
            P(B.Head, S.Cube, 0f, 0.12f, 0.14f, 0.26f, 0.07f, 0.04f, K.Team);
            P(B.Head, S.Cube, 0f, 0.25f, -0.02f, 0.06f, 0.09f, 0.28f, K.Dark);
            P(B.Head, S.Cube, 0f, -0.01f, 0.12f, 0.2f, 0.07f, 0.06f, K.Dark);
            P(B.UpperArmL, S.Capsule, 0f, -0.13f, 0f, 0.19f, 0.16f, 0.19f, K.Dark);
            P(B.UpperArmR, S.Capsule, 0f, -0.13f, 0f, 0.19f, 0.16f, 0.19f, K.Dark);
            P(B.ForeArmL, S.Cylinder, 0f, -0.13f, 0f, 0.25f, 0.14f, 0.25f, K.Main);
            P(B.ForeArmR, S.Cylinder, 0f, -0.13f, 0f, 0.25f, 0.14f, 0.25f, K.Main);
            P(B.ForeArmL, S.Cylinder, 0f, -0.05f, 0f, 0.26f, 0.02f, 0.26f, K.Glow);
            P(B.ForeArmR, S.Cylinder, 0f, -0.05f, 0f, 0.26f, 0.02f, 0.26f, K.Glow);
            P(B.HandL, S.Cube, 0f, -0.05f, 0f, 0.18f, 0.13f, 0.17f, K.Dark);
            P(B.HandR, S.Cube, 0f, -0.05f, 0f, 0.18f, 0.13f, 0.17f, K.Dark);
            P(B.ThighL, S.Capsule, 0f, -0.2f, 0f, 0.23f, 0.24f, 0.23f, K.Main);
            P(B.ThighR, S.Capsule, 0f, -0.2f, 0f, 0.23f, 0.24f, 0.23f, K.Main);
            P(B.ShinL, S.Cube, 0f, -0.21f, 0.01f, 0.2f, 0.38f, 0.22f, K.Dark);
            P(B.ShinR, S.Cube, 0f, -0.21f, 0.01f, 0.2f, 0.38f, 0.22f, K.Dark);
            P(B.ShinL, S.Cube, 0f, -0.08f, 0.122f, 0.12f, 0.04f, 0.01f, K.Glow);
            P(B.ShinR, S.Cube, 0f, -0.08f, 0.122f, 0.12f, 0.04f, 0.01f, K.Glow);
            P(B.FootL, S.Cube, 0f, 0f, 0.06f, 0.22f, 0.13f, 0.34f, K.Dark);
            P(B.FootR, S.Cube, 0f, 0f, 0.06f, 0.22f, 0.13f, 0.34f, K.Dark);
            Ring(B.Back, 0f, 0.05f, -0.12f, 0.3f, 14, 0.06f, K.Accent, 0);
            P(B.Back, S.Sphere, 0f, 0.05f, -0.1f, 0.2f, 0.2f, 0.12f, K.Glow);
            P(B.Back, S.Cube, 0f, 0.05f, -0.03f, 0.3f, 0.3f, 0.1f, K.Dark);
            P(B.Orbit, S.Sphere, -0.55f, 0f, 0f, 0.12f, 0.12f, 0.12f, K.Glow);
            P(B.Orbit, S.Sphere, 0.55f, 0f, 0f, 0.12f, 0.12f, 0.12f, K.Glow);
        }

        /// <summary>VEX — sleek skirmisher: fitted vest, asymmetric pauldron, trailing scarf, goggles.</summary>
        void Build_vex()
        {
            P(B.Chest, S.Cube, 0f, 0.14f, 0f, 0.43f, 0.33f, 0.27f, K.Dark);
            P(B.Chest, S.Cube, 0f, 0.18f, 0.137f, 0.3f, 0.18f, 0.02f, K.Main);
            P(B.Chest, S.Cube, 0.02f, 0.14f, 0.15f, 0.05f, 0.42f, 0.02f, K.Accent, 0f, 0f, 35f);
            P(B.Chest, S.Cube, -0.15f, 0.1f, 0.141f, 0.02f, 0.24f, 0.01f, K.Glow);
            P(B.Chest, S.Cube, -0.28f, 0.3f, 0f, 0.22f, 0.08f, 0.26f, K.Main, 0f, 0f, 22f);
            P(B.Chest, S.Cube, -0.3f, 0.25f, 0f, 0.18f, 0.06f, 0.24f, K.Trim, 0f, 0f, 30f);
            P(B.Neck, S.Cylinder, 0f, 0.0f, 0f, 0.21f, 0.06f, 0.21f, K.Main);
            P(B.Chest, S.Cube, 0.08f, 0.08f, -0.17f, 0.12f, 0.48f, 0.03f, K.Main, 14f, 0f, 6f);
            P(B.Chest, S.Cube, 0.1f, -0.17f, -0.21f, 0.11f, 0.08f, 0.03f, K.Glow, 14f, 0f, 6f);
            P(B.Spine, S.Cube, 0f, 0.08f, 0.01f, 0.31f, 0.2f, 0.21f, K.Cloth);
            P(B.Hips, S.Cube, 0f, 0.04f, 0f, 0.35f, 0.06f, 0.23f, K.Dark);
            P(B.Hips, S.Cube, 0f, 0.04f, 0.116f, 0.06f, 0.05f, 0.01f, K.Glow);
            P(B.Head, S.Sphere, 0f, 0.15f, -0.015f, 0.235f, 0.2f, 0.25f, K.Hair);
            P(B.Head, S.Cube, 0.04f, 0.19f, 0.07f, 0.14f, 0.05f, 0.1f, K.Hair, 0f, 0f, -15f);
            P(B.Head, S.Cube, 0f, 0.12f, 0.108f, 0.23f, 0.06f, 0.05f, K.Team);
            P(B.Head, S.Cube, 0f, 0.12f, 0f, 0.24f, 0.025f, 0.22f, K.Dark);
            P(B.Head, S.Cylinder, 0.12f, 0.1f, 0f, 0.06f, 0.02f, 0.06f, K.Metal, 0f, 0f, 90f);
            P(B.ForeArmL, S.Cube, 0f, -0.13f, 0f, 0.12f, 0.18f, 0.12f, K.Main);
            P(B.ForeArmR, S.Cube, 0f, -0.13f, 0f, 0.12f, 0.18f, 0.12f, K.Main);
            P(B.ForeArmL, S.Cube, 0f, -0.08f, 0f, 0.125f, 0.02f, 0.125f, K.Glow);
            P(B.ForeArmR, S.Cube, 0f, -0.08f, 0f, 0.125f, 0.02f, 0.125f, K.Glow);
            P(B.ThighR, S.Cube, 0.1f, -0.18f, 0f, 0.07f, 0.18f, 0.12f, K.Dark);
            P(B.ThighR, S.Cube, 0.1f, -0.1f, 0f, 0.075f, 0.02f, 0.125f, K.Main);
            P(B.ShinL, S.Sphere, 0f, 0f, 0.06f, 0.13f, 0.13f, 0.12f, K.Trim);
            P(B.ShinR, S.Sphere, 0f, 0f, 0.06f, 0.13f, 0.13f, 0.12f, K.Trim);
            P(B.ShinL, S.Cube, 0f, -0.34f, 0.01f, 0.14f, 0.17f, 0.16f, K.Dark);
            P(B.ShinR, S.Cube, 0f, -0.34f, 0.01f, 0.14f, 0.17f, 0.16f, K.Dark);
            P(B.FootL, S.Cube, 0f, 0f, 0.05f, 0.13f, 0.11f, 0.28f, K.Dark);
            P(B.FootR, S.Cube, 0f, 0f, 0.05f, 0.13f, 0.11f, 0.28f, K.Dark);
            P(B.FootL, S.Cube, 0f, -0.04f, 0.05f, 0.135f, 0.02f, 0.285f, K.Main);
            P(B.FootR, S.Cube, 0f, -0.04f, 0.05f, 0.135f, 0.02f, 0.285f, K.Main);
        }

        /// <summary>KESTREL — aviator: flight jacket with fur collar, harness, finned helmet, winged jetpack.</summary>
        void Build_kestrel()
        {
            P(B.Chest, S.Cube, 0f, 0.13f, 0f, 0.46f, 0.34f, 0.28f, K.Cloth);
            P(B.Chest, S.Cube, 0f, 0.32f, -0.01f, 0.42f, 0.09f, 0.31f, K.Accent);
            P(B.Chest, S.Cube, -0.1f, 0.13f, 0.145f, 0.05f, 0.36f, 0.02f, K.Dark);
            P(B.Chest, S.Cube, 0.1f, 0.13f, 0.145f, 0.05f, 0.36f, 0.02f, K.Dark);
            P(B.Chest, S.Cube, 0f, 0.1f, 0.155f, 0.26f, 0.05f, 0.02f, K.Dark);
            P(B.Chest, S.Cube, 0f, 0.1f, 0.165f, 0.09f, 0.08f, 0.02f, K.Metal);
            P(B.Chest, S.Cube, -0.24f, 0.24f, 0f, 0.06f, 0.12f, 0.2f, K.Main);
            P(B.Chest, S.Cube, 0.24f, 0.24f, 0f, 0.06f, 0.12f, 0.2f, K.Main);
            P(B.Spine, S.Cube, 0f, 0.08f, 0f, 0.31f, 0.22f, 0.21f, K.Cloth);
            P(B.Hips, S.Cube, 0f, 0.04f, 0f, 0.35f, 0.07f, 0.24f, K.Dark);
            P(B.Hips, S.Cube, -0.17f, -0.01f, 0.1f, 0.09f, 0.1f, 0.06f, K.Trim);
            P(B.Hips, S.Cube, 0.17f, -0.01f, 0.1f, 0.09f, 0.1f, 0.06f, K.Trim);
            P(B.Head, S.Sphere, 0f, 0.12f, -0.005f, 0.27f, 0.27f, 0.28f, K.Main);
            P(B.Head, S.Cube, 0f, 0.1f, 0.115f, 0.23f, 0.09f, 0.05f, K.Team);
            P(B.Head, S.Cube, 0f, 0.26f, -0.03f, 0.03f, 0.1f, 0.22f, K.Main);
            P(B.Head, S.Cube, 0f, 0.3f, -0.03f, 0.035f, 0.02f, 0.18f, K.Glow);
            P(B.Head, S.Cylinder, -0.14f, 0.1f, 0f, 0.09f, 0.02f, 0.09f, K.Dark, 0f, 0f, 90f);
            P(B.Head, S.Cylinder, 0.14f, 0.1f, 0f, 0.09f, 0.02f, 0.09f, K.Dark, 0f, 0f, 90f);
            P(B.Back, S.Cube, 0f, 0f, -0.04f, 0.36f, 0.4f, 0.18f, K.Main);
            P(B.Back, S.Cube, 0f, 0.05f, -0.135f, 0.2f, 0.18f, 0.02f, K.Dark);
            P(B.Back, S.Cylinder, -0.15f, -0.05f, -0.12f, 0.14f, 0.2f, 0.14f, K.Metal);
            P(B.Back, S.Cylinder, 0.15f, -0.05f, -0.12f, 0.14f, 0.2f, 0.14f, K.Metal);
            P(B.Back, S.Cylinder, -0.15f, -0.28f, -0.12f, 0.11f, 0.04f, 0.11f, K.Dark);
            P(B.Back, S.Cylinder, 0.15f, -0.28f, -0.12f, 0.11f, 0.04f, 0.11f, K.Dark);
            P(B.Back, S.Sphere, -0.15f, -0.34f, -0.12f, 0.09f, 0.12f, 0.09f, K.Glow);
            P(B.Back, S.Sphere, 0.15f, -0.34f, -0.12f, 0.09f, 0.12f, 0.09f, K.Glow);
            P(B.Back, S.Cube, -0.36f, 0.14f, -0.08f, 0.44f, 0.15f, 0.03f, K.Main, 0f, 0f, 18f);
            P(B.Back, S.Cube, 0.36f, 0.14f, -0.08f, 0.44f, 0.15f, 0.03f, K.Main, 0f, 0f, -18f);
            P(B.Back, S.Cube, -0.36f, 0.09f, -0.096f, 0.4f, 0.03f, 0.01f, K.Accent, 0f, 0f, 18f);
            P(B.Back, S.Cube, 0.36f, 0.09f, -0.096f, 0.4f, 0.03f, 0.01f, K.Accent, 0f, 0f, -18f);
            P(B.Back, S.Cube, -0.57f, 0.21f, -0.08f, 0.04f, 0.17f, 0.04f, K.Glow, 0f, 0f, 18f);
            P(B.Back, S.Cube, 0.57f, 0.21f, -0.08f, 0.04f, 0.17f, 0.04f, K.Glow, 0f, 0f, -18f);
            P(B.ForeArmL, S.Cube, 0f, -0.2f, 0f, 0.12f, 0.08f, 0.12f, K.Main);
            P(B.ForeArmR, S.Cube, 0f, -0.2f, 0f, 0.12f, 0.08f, 0.12f, K.Main);
            P(B.ShinL, S.Sphere, 0f, 0f, 0.06f, 0.13f, 0.13f, 0.12f, K.Dark);
            P(B.ShinR, S.Sphere, 0f, 0f, 0.06f, 0.13f, 0.13f, 0.12f, K.Dark);
            P(B.ShinL, S.Cube, 0f, -0.32f, 0.01f, 0.15f, 0.21f, 0.16f, K.Dark);
            P(B.ShinR, S.Cube, 0f, -0.32f, 0.01f, 0.15f, 0.21f, 0.16f, K.Dark);
            P(B.FootL, S.Cube, 0f, 0f, 0.05f, 0.14f, 0.12f, 0.29f, K.Dark);
            P(B.FootR, S.Cube, 0f, 0f, 0.05f, 0.14f, 0.12f, 0.29f, K.Dark);
        }

        /// <summary>ROOK — field engineer: hi-vis work vest, goggles, cybernetic right arm, cable-spool tool rig.</summary>
        void Build_rook()
        {
            P(B.Chest, S.Cube, 0f, 0.13f, 0f, 0.46f, 0.34f, 0.28f, K.Main);
            P(B.Chest, S.Cube, 0f, 0.05f, 0f, 0.47f, 0.035f, 0.29f, K.Accent);
            P(B.Chest, S.Cube, -0.11f, 0.17f, 0.145f, 0.11f, 0.09f, 0.03f, K.Dark);
            P(B.Chest, S.Cube, 0.11f, 0.17f, 0.145f, 0.11f, 0.09f, 0.03f, K.Dark);
            P(B.Chest, S.Cube, 0f, 0.3f, 0f, 0.36f, 0.06f, 0.29f, K.Cloth);
            P(B.Spine, S.Cube, 0f, 0.08f, 0f, 0.31f, 0.22f, 0.21f, K.Cloth);
            P(B.Hips, S.Cube, 0f, 0.04f, 0f, 0.37f, 0.08f, 0.25f, K.Dark);
            P(B.Hips, S.Cube, -0.13f, -0.03f, 0.12f, 0.09f, 0.11f, 0.06f, K.Accent);
            P(B.Hips, S.Cube, 0.02f, -0.03f, 0.13f, 0.08f, 0.1f, 0.05f, K.Trim);
            P(B.Hips, S.Cube, 0.17f, -0.05f, 0.06f, 0.06f, 0.16f, 0.1f, K.Dark);
            P(B.Head, S.Sphere, 0f, 0.17f, -0.01f, 0.25f, 0.16f, 0.26f, K.Dark);
            P(B.Head, S.Cube, 0f, 0.18f, 0.095f, 0.24f, 0.06f, 0.07f, K.Metal);
            P(B.Head, S.Cylinder, -0.055f, 0.18f, 0.13f, 0.07f, 0.015f, 0.07f, K.Glow, 90f, 0f, 0f);
            P(B.Head, S.Cylinder, 0.055f, 0.18f, 0.13f, 0.07f, 0.015f, 0.07f, K.Glow, 90f, 0f, 0f);
            P(B.Head, S.Cube, 0f, 0.0f, 0.085f, 0.18f, 0.09f, 0.08f, K.Hair);
            P(B.Head, S.Cube, 0f, 0.12f, 0.11f, 0.1f, 0.02f, 0.01f, K.Team);
            P(B.UpperArmR, S.Cylinder, 0f, -0.13f, 0f, 0.13f, 0.14f, 0.13f, K.Metal);
            P(B.UpperArmR, S.Sphere, 0f, 0f, 0f, 0.15f, 0.15f, 0.15f, K.Dark);
            P(B.ForeArmR, S.Sphere, 0f, 0f, 0f, 0.12f, 0.12f, 0.12f, K.Glow);
            P(B.ForeArmR, S.Cube, 0f, -0.13f, 0f, 0.14f, 0.24f, 0.14f, K.Metal);
            P(B.ForeArmR, S.Cube, 0f, -0.13f, 0.072f, 0.04f, 0.18f, 0.01f, K.Accent);
            P(B.HandR, S.Cube, 0f, -0.05f, 0f, 0.12f, 0.1f, 0.12f, K.Dark);
            P(B.ForeArmL, S.Cube, 0f, -0.2f, 0f, 0.115f, 0.07f, 0.115f, K.Accent);
            P(B.Back, S.Cube, 0f, 0f, -0.05f, 0.4f, 0.44f, 0.22f, K.Trim);
            P(B.Back, S.Cylinder, 0f, 0.04f, -0.18f, 0.26f, 0.05f, 0.26f, K.Dark, 90f, 0f, 0f);
            P(B.Back, S.Cylinder, 0f, 0.04f, -0.2f, 0.12f, 0.06f, 0.12f, K.Accent, 90f, 0f, 0f);
            P(B.Back, S.Cylinder, 0.15f, 0.42f, -0.05f, 0.02f, 0.25f, 0.02f, K.Dark);
            P(B.Back, S.Sphere, 0.15f, 0.68f, -0.05f, 0.05f, 0.05f, 0.05f, K.Glow);
            P(B.Back, S.Cube, -0.24f, -0.12f, -0.05f, 0.08f, 0.2f, 0.16f, K.Dark);
            P(B.ShinL, S.Sphere, 0f, 0f, 0.06f, 0.14f, 0.13f, 0.12f, K.Dark);
            P(B.ShinR, S.Sphere, 0f, 0f, 0.06f, 0.14f, 0.13f, 0.12f, K.Dark);
            P(B.ShinL, S.Cube, 0f, -0.33f, 0.01f, 0.16f, 0.2f, 0.17f, K.Dark);
            P(B.ShinR, S.Cube, 0f, -0.33f, 0.01f, 0.16f, 0.2f, 0.17f, K.Dark);
            P(B.FootL, S.Cube, 0f, 0f, 0.05f, 0.15f, 0.13f, 0.3f, K.Dark);
            P(B.FootR, S.Cube, 0f, 0f, 0.05f, 0.15f, 0.13f, 0.3f, K.Dark);
            P(B.FootL, S.Cube, 0f, 0.02f, 0.18f, 0.155f, 0.08f, 0.05f, K.Accent);
            P(B.FootR, S.Cube, 0f, 0.02f, 0.18f, 0.155f, 0.08f, 0.05f, K.Accent);
        }

        /// <summary>LUMEN — lantern medic: hooded long coat, lantern cells on the back, a floating light drone.</summary>
        void Build_lumen()
        {
            P(B.Chest, S.Cube, 0f, 0.13f, 0f, 0.42f, 0.34f, 0.27f, K.Main);
            P(B.Chest, S.Cube, -0.08f, 0.13f, 0.137f, 0.03f, 0.34f, 0.02f, K.Accent);
            P(B.Chest, S.Cube, 0.08f, 0.13f, 0.137f, 0.03f, 0.34f, 0.02f, K.Accent);
            P(B.Chest, S.Cube, 0f, 0.13f, 0.142f, 0.06f, 0.42f, 0.02f, K.Trim, 0f, 0f, -32f);
            P(B.Chest, S.Cube, 0f, 0.25f, 0.141f, 0.07f, 0.07f, 0.02f, K.Glow);
            P(B.Chest, S.Cube, 0f, -0.2f, -0.16f, 0.46f, 0.86f, 0.04f, K.Main, -6f, 0f, 0f);
            P(B.Chest, S.Cube, 0f, -0.62f, -0.205f, 0.46f, 0.03f, 0.045f, K.Accent, -6f, 0f, 0f);
            P(B.Chest, S.Cube, -0.3f, 0.29f, 0f, 0.16f, 0.06f, 0.26f, K.Trim, 0f, 0f, 18f);
            P(B.Chest, S.Cube, 0.3f, 0.29f, 0f, 0.16f, 0.06f, 0.26f, K.Trim, 0f, 0f, -18f);
            P(B.Spine, S.Cube, 0f, 0.08f, 0f, 0.31f, 0.22f, 0.21f, K.Main);
            P(B.Hips, S.Cube, 0f, 0.04f, 0f, 0.34f, 0.06f, 0.23f, K.Accent);
            P(B.Hips, S.Cube, -0.1f, -0.25f, 0.12f, 0.17f, 0.46f, 0.04f, K.Main, -6f, 0f, 0f);
            P(B.Hips, S.Cube, 0.1f, -0.25f, 0.12f, 0.17f, 0.46f, 0.04f, K.Main, -6f, 0f, 0f);
            P(B.Hips, S.Cube, -0.1f, -0.47f, 0.145f, 0.17f, 0.03f, 0.045f, K.Accent, -6f, 0f, 0f);
            P(B.Hips, S.Cube, 0.1f, -0.47f, 0.145f, 0.17f, 0.03f, 0.045f, K.Accent, -6f, 0f, 0f);
            P(B.Hips, S.Cube, -0.19f, -0.24f, 0f, 0.04f, 0.46f, 0.22f, K.Main, 0f, 0f, 6f);
            P(B.Hips, S.Cube, 0.19f, -0.24f, 0f, 0.04f, 0.46f, 0.22f, K.Main, 0f, 0f, -6f);
            P(B.Head, S.Sphere, 0f, 0.13f, -0.05f, 0.3f, 0.31f, 0.28f, K.Main);
            P(B.Head, S.Cube, 0f, 0.2f, 0.085f, 0.2f, 0.05f, 0.05f, K.Hair);
            P(B.Head, S.Cube, 0f, 0.175f, 0.1f, 0.22f, 0.02f, 0.02f, K.Accent);
            P(B.Head, S.Cube, 0f, 0.27f, 0.05f, 0.26f, 0.02f, 0.02f, K.Team);
            P(B.Head, S.Cube, 0f, 0.18f, 0.112f, 0.03f, 0.03f, 0.01f, K.Glow);
            P(B.ForeArmL, S.Cylinder, 0f, -0.13f, 0f, 0.15f, 0.13f, 0.15f, K.Main);
            P(B.ForeArmR, S.Cylinder, 0f, -0.13f, 0f, 0.15f, 0.13f, 0.15f, K.Main);
            P(B.ForeArmL, S.Cylinder, 0f, -0.24f, 0f, 0.16f, 0.015f, 0.16f, K.Accent);
            P(B.ForeArmR, S.Cylinder, 0f, -0.24f, 0f, 0.16f, 0.015f, 0.16f, K.Accent);
            P(B.HandL, S.Cube, 0f, -0.04f, 0f, 0.08f, 0.1f, 0.07f, K.Trim);
            P(B.HandR, S.Cube, 0f, -0.04f, 0f, 0.08f, 0.1f, 0.07f, K.Trim);
            P(B.Back, S.Cube, 0f, 0.04f, 0.0f, 0.3f, 0.36f, 0.06f, K.Dark);
            P(B.Back, S.Cylinder, -0.09f, -0.02f, -0.06f, 0.09f, 0.11f, 0.09f, K.Glow);
            P(B.Back, S.Cylinder, 0.09f, -0.02f, -0.06f, 0.09f, 0.11f, 0.09f, K.Glow);
            P(B.Back, S.Cylinder, 0f, 0.16f, -0.06f, 0.09f, 0.08f, 0.09f, K.Glow);
            P(B.Back, S.Cylinder, -0.09f, 0.1f, -0.06f, 0.1f, 0.015f, 0.1f, K.Accent);
            P(B.Back, S.Cylinder, 0.09f, 0.1f, -0.06f, 0.1f, 0.015f, 0.1f, K.Accent);
            P(B.Back, S.Cylinder, 0f, 0.25f, -0.06f, 0.1f, 0.015f, 0.1f, K.Accent);
            P(B.Drone, S.Sphere, 0f, 0f, 0f, 0.13f, 0.13f, 0.13f, K.Glow);
            P(B.Drone, S.Cylinder, 0f, 0f, 0f, 0.22f, 0.01f, 0.22f, K.Accent);
            P(B.Drone, S.Cube, 0f, -0.1f, 0f, 0.02f, 0.06f, 0.02f, K.Accent);
            P(B.ShinL, S.Cube, 0f, -0.32f, 0.01f, 0.14f, 0.22f, 0.16f, K.Dark);
            P(B.ShinR, S.Cube, 0f, -0.32f, 0.01f, 0.14f, 0.22f, 0.16f, K.Dark);
            P(B.ShinL, S.Cube, 0f, -0.21f, 0.01f, 0.145f, 0.02f, 0.165f, K.Accent);
            P(B.ShinR, S.Cube, 0f, -0.21f, 0.01f, 0.145f, 0.02f, 0.165f, K.Accent);
            P(B.FootL, S.Cube, 0f, 0f, 0.05f, 0.12f, 0.1f, 0.27f, K.Dark);
            P(B.FootR, S.Cube, 0f, 0f, 0.05f, 0.12f, 0.1f, 0.27f, K.Dark);
        }

        /// <summary>CYPRESS — field botanist: woven poncho, wide-brim hat, vine wraps, sprouting planter pack.</summary>
        void Build_cypress()
        {
            P(B.Chest, S.Cube, 0f, 0.3f, 0f, 0.58f, 0.07f, 0.4f, K.Main);
            P(B.Chest, S.Cube, 0f, 0.12f, 0.17f, 0.52f, 0.36f, 0.04f, K.Main, 6f, 0f, 0f);
            P(B.Chest, S.Cube, 0f, 0.09f, -0.17f, 0.52f, 0.42f, 0.04f, K.Main, -6f, 0f, 0f);
            P(B.Chest, S.Cube, 0f, 0.0f, 0.18f, 0.52f, 0.035f, 0.045f, K.Accent, 6f, 0f, 0f);
            P(B.Chest, S.Cube, 0f, 0.07f, 0.181f, 0.52f, 0.02f, 0.045f, K.Trim, 6f, 0f, 0f);
            P(B.Chest, S.Cube, 0f, -0.05f, -0.18f, 0.52f, 0.035f, 0.045f, K.Accent, -6f, 0f, 0f);
            P(B.Chest, S.Cube, 0f, 0.22f, 0.195f, 0.06f, 0.06f, 0.02f, K.Glow);
            P(B.Spine, S.Cube, 0f, 0.08f, 0f, 0.3f, 0.22f, 0.2f, K.Cloth);
            P(B.Hips, S.Cube, 0f, 0.04f, 0f, 0.35f, 0.06f, 0.23f, K.Dark);
            P(B.Hips, S.Cube, -0.21f, -0.04f, 0.05f, 0.08f, 0.17f, 0.15f, K.Dark);
            P(B.Hips, S.Cube, -0.255f, -0.02f, 0.05f, 0.01f, 0.06f, 0.06f, K.Accent);
            P(B.Head, S.Cylinder, 0f, 0.19f, 0f, 0.46f, 0.012f, 0.46f, K.Dark);
            P(B.Head, S.Cylinder, 0f, 0.25f, 0f, 0.24f, 0.06f, 0.24f, K.Dark);
            P(B.Head, S.Cylinder, 0f, 0.21f, 0f, 0.245f, 0.015f, 0.245f, K.Accent);
            P(B.Head, S.Cube, 0.1f, 0.26f, 0.05f, 0.08f, 0.01f, 0.15f, K.Main, 0f, 30f, 20f);
            P(B.Head, S.Cube, 0f, 0.13f, 0.108f, 0.16f, 0.025f, 0.02f, K.Team);
            P(B.Head, S.Cube, 0f, 0.03f, 0.1f, 0.15f, 0.04f, 0.04f, K.Hair);
            P(B.ForeArmL, S.Cylinder, 0f, -0.05f, 0f, 0.13f, 0.015f, 0.13f, K.Main, 10f, 0f, 0f);
            P(B.ForeArmL, S.Cylinder, 0f, -0.12f, 0f, 0.125f, 0.015f, 0.125f, K.Main, -12f, 0f, 0f);
            P(B.ForeArmL, S.Cylinder, 0f, -0.19f, 0f, 0.12f, 0.015f, 0.12f, K.Main, 8f, 0f, 0f);
            P(B.ForeArmL, S.Sphere, 0.05f, -0.15f, 0.04f, 0.04f, 0.04f, 0.04f, K.Glow);
            P(B.ForeArmR, S.Cylinder, 0f, -0.2f, 0f, 0.115f, 0.04f, 0.115f, K.Trim);
            P(B.Back, S.Cube, 0f, 0.02f, 0.0f, 0.24f, 0.26f, 0.06f, K.Dark);
            P(B.Back, S.Cylinder, 0f, -0.04f, -0.08f, 0.22f, 0.1f, 0.22f, K.Dark);
            P(B.Back, S.Cylinder, 0f, 0.065f, -0.08f, 0.2f, 0.01f, 0.2f, K.Hair);
            P(B.Back, S.Cube, -0.04f, 0.18f, -0.08f, 0.04f, 0.24f, 0.1f, K.Main, 0f, 0f, 20f);
            P(B.Back, S.Cube, 0.05f, 0.2f, -0.08f, 0.04f, 0.28f, 0.11f, K.Main, 0f, 0f, -25f);
            P(B.Back, S.Cube, 0f, 0.16f, -0.11f, 0.1f, 0.2f, 0.04f, K.Main, -25f, 0f, 0f);
            P(B.Back, S.Sphere, 0f, 0.32f, -0.08f, 0.07f, 0.07f, 0.07f, K.Glow);
            P(B.ShinL, S.Cylinder, 0f, -0.06f, 0f, 0.15f, 0.03f, 0.15f, K.Trim);
            P(B.ShinR, S.Cylinder, 0f, -0.06f, 0f, 0.15f, 0.03f, 0.15f, K.Trim);
            P(B.ShinL, S.Cube, 0f, -0.33f, 0.01f, 0.14f, 0.2f, 0.16f, K.Dark);
            P(B.ShinR, S.Cube, 0f, -0.33f, 0.01f, 0.14f, 0.2f, 0.16f, K.Dark);
            P(B.FootL, S.Cube, 0f, 0f, 0.05f, 0.13f, 0.11f, 0.28f, K.Dark);
            P(B.FootR, S.Cube, 0f, 0f, 0.05f, 0.13f, 0.11f, 0.28f, K.Dark);
        }

        // ------------------------------------------------------------------ runtime

        Transform Bone(B b) { return bones[(int)b]; }

        public void SetFirstPerson(bool fp)
        {
            firstPerson = fp;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.shadowCastingMode = fp ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            }
        }

        public void OnDeath() { deathAnim = 0.001f; }

        public void OnRespawn()
        {
            deathAnim = 0f;
            Bone(B.Root).localRotation = Quaternion.identity;
            Bone(B.Root).localPosition = Vector3.zero;
            gameObject.SetActive(true);
            SetFirstPerson(firstPerson);
        }

        void Pose(B b, float x, float y, float z)
        {
            bones[(int)b].localRotation = Quaternion.Euler(x, y, z) * restRot[(int)b];
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time;
            var root = Bone(B.Root);

            // floating accessories
            Bone(B.Orbit).localRotation = Quaternion.Euler(0f, t * 120f, 0f);
            Bone(B.Drone).localPosition = restPos[(int)B.Drone] + new Vector3(0f, Mathf.Sin(t * 2.2f) * 0.05f, 0f);
            Bone(B.Drone).localRotation = Quaternion.Euler(0f, t * 90f, 0f);

            if (deathAnim > 0f)
            {
                deathAnim += dt;
                float k = Mathf.Clamp01(deathAnim / 0.5f);
                root.localRotation = Quaternion.Euler(-85f * k * k, 0, 0);
                root.localPosition = new Vector3(0, -0.05f * k, -0.3f * k);
                Pose(B.UpperArmL, 40f * k, 0f, -30f * k);
                Pose(B.UpperArmR, 40f * k, 0f, 30f * k);
                if (deathAnim > 3f) gameObject.SetActive(false);
                return;
            }

            if (previewMode)
            {
                float breathe = Mathf.Sin(t * 1.8f);
                Bone(B.Chest).localPosition = restPos[(int)B.Chest] + new Vector3(0f, breathe * 0.006f, 0f);
                Bone(B.UpperArmL).localRotation = Quaternion.Euler(4f, 0f, -12f + breathe * 1.5f);
                Bone(B.UpperArmR).localRotation = Quaternion.Euler(-8f, 0f, 14f - breathe * 1.5f);
                Bone(B.ForeArmL).localRotation = Quaternion.Euler(-18f, 0f, 0f);
                Bone(B.ForeArmR).localRotation = Quaternion.Euler(-40f, 0f, 0f);
                Pose(B.Head, breathe * 2f, Mathf.Sin(t * 0.7f) * 8f, 0f);
                return;
            }

            if (self == null || self.motor == null) return;
            float speed = self.motor.HorizontalVelocity.magnitude;
            bool grounded = self.motor.grounded;
            float amt = Mathf.Clamp01(speed / 5f);
            walkPhase += dt * speed * 1.9f;

            if (grounded)
            {
                float sL = Mathf.Sin(walkPhase), sR = Mathf.Sin(walkPhase + Mathf.PI);
                Pose(B.ThighL, -sL * 32f * amt, 0f, 0f);
                Pose(B.ThighR, -sR * 32f * amt, 0f, 0f);
                Pose(B.ShinL, Mathf.Max(0f, Mathf.Sin(walkPhase - 1.3f)) * 50f * amt, 0f, 0f);
                Pose(B.ShinR, Mathf.Max(0f, Mathf.Sin(walkPhase + Mathf.PI - 1.3f)) * 50f * amt, 0f, 0f);
                Pose(B.FootL, sL * 10f * amt, 0f, 0f);
                Pose(B.FootR, sR * 10f * amt, 0f, 0f);
                Bone(B.Hips).localPosition = restPos[(int)B.Hips] + new Vector3(0f, Mathf.Abs(Mathf.Cos(walkPhase)) * 0.035f * amt - 0.02f * amt, 0f);
            }
            else
            {
                Pose(B.ThighL, -30f, 0f, 0f);
                Pose(B.ThighR, -10f, 0f, 0f);
                Pose(B.ShinL, 50f, 0f, 0f);
                Pose(B.ShinR, 30f, 0f, 0f);
                Bone(B.Hips).localPosition = restPos[(int)B.Hips];
            }

            // upper body follows the aim pitch; arms keep holding the weapon
            float pitch = self.pitch;
            Pose(B.Spine, pitch * 0.25f + amt * 4f, 0f, 0f);
            Pose(B.UpperArmL, pitch * 0.6f, 0f, 0f);
            Pose(B.UpperArmR, pitch * 0.6f, 0f, 0f);
            Pose(B.Head, pitch * 0.35f, 0f, 0f);
            Bone(B.Chest).localPosition = restPos[(int)B.Chest] + new Vector3(0f, Mathf.Sin(t * 1.8f) * 0.005f, 0f);

            float stunWobble = self.IsStunned ? Mathf.Sin(t * 18f) * 6f : 0f;
            root.localRotation = Quaternion.Euler(0, 0, stunWobble);
        }
    }
}
