using System.Collections.Generic;
using UnityEngine;

namespace NexusStrike
{
    public class SpawnRoom
    {
        public Team team;
        public int phase;
        public Vector3 center;
        public float yaw;
        public readonly List<Renderer> glow = new List<Renderer>();

        public Vector3 Forward { get { return Quaternion.Euler(0, yaw, 0) * Vector3.forward; } }

        public Vector3 RandomPoint()
        {
            var local = new Vector3(Random.Range(-4f, 4f), 0.1f, Random.Range(-2.5f, 1.5f));
            return center + Quaternion.Euler(0, yaw, 0) * local;
        }

        public bool Contains(Vector3 p)
        {
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0, yaw, 0)) * (p - center);
            return Mathf.Abs(local.x) < 6f && local.z > -4.5f && local.z < 4.8f && local.y < 4f && local.y > -1f;
        }
    }

    public class MapData
    {
        public Transform root;
        public List<Vector3> path = new List<Vector3>();
        public float[] checkpointFractions = { 0.33f, 0.66f };
        public SpawnRoom[] attackSpawns = new SpawnRoom[3];
        public SpawnRoom[] defendSpawns = new SpawnRoom[3];
        public List<HealthPack> healthPacks = new List<HealthPack>();
        public Bounds bounds;
        public string name = "PORT SOLACE";
    }

    /// <summary>
    /// Procedurally assembles the escort map "Port Solace": a coastal town with a winding payload
    /// route, buildings, high-ground platforms with ramps, cover, health packs and spawn rooms.
    /// Generation is seeded so the map is identical every run.
    /// </summary>
    public class MapBuilder
    {
        readonly MapData map = new MapData();
        readonly List<Bounds> solids = new List<Bounds>();
        readonly List<Vector4> blockedCircles = new List<Vector4>(); // xyz center, w radius
        readonly List<Vector3> pathSamples = new List<Vector3>();
        float[] cumulative;
        float total;
        System.Random rng;
        Transform root, geo, decor;

        static readonly Color Ground = new Color(0.76f, 0.71f, 0.6f);
        static readonly Color Street = new Color(0.56f, 0.53f, 0.5f);
        static readonly Color Rail = new Color(0.35f, 0.33f, 0.32f);
        static readonly Color Wood = new Color(0.58f, 0.42f, 0.26f);
        static readonly Color Window = new Color(0.2f, 0.27f, 0.36f);
        static readonly Color Sea = new Color(0.16f, 0.45f, 0.62f);
        static readonly Color[] WallColors =
        {
            new Color(0.94f, 0.89f, 0.79f), new Color(0.82f, 0.5f, 0.37f), new Color(0.87f, 0.77f, 0.6f),
            new Color(0.95f, 0.95f, 0.92f), new Color(0.63f, 0.76f, 0.86f), new Color(0.86f, 0.69f, 0.4f),
            new Color(0.75f, 0.82f, 0.7f)
        };
        static readonly Color[] RoofColors = { new Color(0.66f, 0.3f, 0.22f), new Color(0.36f, 0.32f, 0.32f), new Color(0.3f, 0.45f, 0.6f) };
        static readonly Color[] AwningColors = { new Color(0.85f, 0.25f, 0.25f), new Color(0.2f, 0.5f, 0.8f), new Color(0.95f, 0.8f, 0.3f), new Color(0.3f, 0.65f, 0.4f) };

        public static MapData Build()
        {
            return new MapBuilder().Generate();
        }

        MapData Generate()
        {
            rng = new System.Random(1337);
            root = new GameObject("PortSolace").transform;
            geo = new GameObject("Geometry").transform;
            geo.SetParent(root, false);
            decor = new GameObject("Decor").transform;
            decor.SetParent(root, false);
            map.root = root;

            map.path.AddRange(new[]
            {
                new Vector3(0, 0, 0), new Vector3(0, 0, 40), new Vector3(22, 0, 62), new Vector3(22, 0, 100),
                new Vector3(-4, 0, 126), new Vector3(-4, 0, 166), new Vector3(12, 0, 182), new Vector3(12, 0, 206)
            });
            cumulative = new float[map.path.Count];
            for (int i = 1; i < map.path.Count; i++)
            {
                total += Vector3.Distance(map.path[i - 1], map.path[i]);
                cumulative[i] = total;
            }
            for (float d = 0; d <= total; d += 1.5f) pathSamples.Add(Eval(d));

            float cp1 = map.checkpointFractions[0] * total;
            float cp2 = map.checkpointFractions[1] * total;

            // spawn rooms (phase = number of checkpoints reached)
            map.attackSpawns[0] = MakeSpawn(Team.Attack, 0, map.path[0] - Vector3.forward * 13f, 0f);
            map.attackSpawns[1] = SpawnBeside(Team.Attack, 1, cp1 - 16f, -1f, 18f);
            map.attackSpawns[2] = SpawnBeside(Team.Attack, 2, cp2 - 14f, 1f, 18f);
            map.defendSpawns[0] = SpawnBeside(Team.Defend, 0, cp1 + 24f, 1f, 18f);
            map.defendSpawns[1] = SpawnBeside(Team.Defend, 1, cp2 + 22f, -1f, 18f);
            map.defendSpawns[2] = MakeSpawn(Team.Defend, 2, map.path[map.path.Count - 1] + Vector3.forward * 13f, 180f);

            // bounds
            var b = new Bounds(map.path[0], Vector3.zero);
            foreach (var p in map.path) b.Encapsulate(p);
            b.Expand(new Vector3(60f, 0f, 30f));
            foreach (var s in map.attackSpawns) b.Encapsulate(new Bounds(s.center, Vector3.one * 18f));
            foreach (var s in map.defendSpawns) b.Encapsulate(new Bounds(s.center, Vector3.one * 18f));
            b.center = new Vector3(b.center.x, 10f, b.center.z);
            b.size = new Vector3(b.size.x, 20f, b.size.z);
            map.bounds = b;

            BuildGroundAndBorders();
            BuildRoute();
            foreach (var s in map.attackSpawns) BuildSpawnRoom(s);
            foreach (var s in map.defendSpawns) BuildSpawnRoom(s);
            BuildGates(cp1);
            BuildGates(cp2);
            BuildPlatforms();
            BuildBuildings();
            BuildCover();
            BuildDecor();

            Physics.SyncTransforms();
            BuildHealthPacks();
            return map;
        }

        // ------------------------------------------------------------------ path helpers

        Vector3 Eval(float d)
        {
            d = Mathf.Clamp(d, 0f, total);
            for (int i = 1; i < map.path.Count; i++)
                if (d <= cumulative[i])
                {
                    float seg = cumulative[i] - cumulative[i - 1];
                    return Vector3.Lerp(map.path[i - 1], map.path[i], seg > 0 ? (d - cumulative[i - 1]) / seg : 0f);
                }
            return map.path[map.path.Count - 1];
        }

        Vector3 Dir(float d)
        {
            d = Mathf.Clamp(d, 0f, total);
            for (int i = 1; i < map.path.Count; i++)
                if (d <= cumulative[i] + 0.001f) return (map.path[i] - map.path[i - 1]).normalized;
            return Vector3.forward;
        }

        Vector3 Right(float d)
        {
            Vector3 f = Dir(d);
            return new Vector3(f.z, 0f, -f.x);
        }

        float Range(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        bool Chance(float p) { return rng.NextDouble() < p; }
        T Pick<T>(T[] arr) { return arr[rng.Next(arr.Length)]; }

        bool ClearOfPath(Bounds b, float clearance)
        {
            float c2 = clearance * clearance;
            foreach (var s in pathSamples)
                if (b.SqrDistance(new Vector3(s.x, b.center.y, s.z)) < c2) return false;
            return true;
        }

        bool Free(Bounds b, float margin)
        {
            var e = b;
            e.Expand(new Vector3(margin * 2f, 0f, margin * 2f));
            foreach (var s in solids)
                if (e.Intersects(s)) return false;
            foreach (var c in blockedCircles)
                if (b.SqrDistance(new Vector3(c.x, b.center.y, c.z)) < c.w * c.w) return false;
            var inner = map.bounds;
            inner.Expand(new Vector3(-4f, 100f, -4f));
            if (!inner.Contains(new Vector3(b.min.x, inner.center.y, b.min.z)) || !inner.Contains(new Vector3(b.max.x, inner.center.y, b.max.z)))
                return false;
            return true;
        }

        // ------------------------------------------------------------------ primitives

        Transform Box(Vector3 center, Vector3 size, Color c, bool collider = true, Transform parent = null, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent != null ? parent : (collider ? geo : decor), false);
            go.transform.position = center;
            go.transform.rotation = rot ?? Quaternion.identity;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = MaterialLib.Lit(c);
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
            return go.transform;
        }

        Transform Cyl(Vector3 center, float radius, float height, Color c, bool collider = true, bool unlit = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.transform.SetParent(collider ? geo : decor, false);
            go.transform.position = center;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = unlit ? MaterialLib.Unlit(c) : MaterialLib.Lit(c);
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
            return go.transform;
        }

        Transform Sphere(Vector3 center, float radius, Color c, bool unlit = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.SetParent(decor, false);
            go.transform.position = center;
            go.transform.localScale = Vector3.one * radius * 2f;
            go.GetComponent<Renderer>().sharedMaterial = unlit ? MaterialLib.Unlit(c) : MaterialLib.Lit(c);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
            return go.transform;
        }

        // ------------------------------------------------------------------ sections

        void BuildGroundAndBorders()
        {
            var b = map.bounds;
            Box(new Vector3(b.center.x, -0.5f, b.center.z), new Vector3(b.size.x + 20f, 1f, b.size.z + 20f), Ground);
            var sea = Box(new Vector3(b.center.x, -1.2f, b.center.z), new Vector3(1200f, 0.2f, 1200f), Sea, false);
            sea.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Color wallC = new Color(0.78f, 0.74f, 0.66f);
            float h = 1.4f;
            // low visible sea wall + tall invisible blocker
            Vector3[] centers =
            {
                new Vector3(b.min.x, 0, b.center.z), new Vector3(b.max.x, 0, b.center.z),
                new Vector3(b.center.x, 0, b.min.z), new Vector3(b.center.x, 0, b.max.z)
            };
            Vector3[] sizes =
            {
                new Vector3(1f, 1f, b.size.z + 1f), new Vector3(1f, 1f, b.size.z + 1f),
                new Vector3(b.size.x + 1f, 1f, 1f), new Vector3(b.size.x + 1f, 1f, 1f)
            };
            for (int i = 0; i < 4; i++)
            {
                Box(centers[i] + Vector3.up * h * 0.5f, Vector3.Scale(sizes[i], new Vector3(1, h, 1)), wallC);
                var blocker = Box(centers[i] + Vector3.up * 20f, Vector3.Scale(sizes[i], new Vector3(1, 40f, 1)), wallC);
                Object.DestroyImmediate(blocker.GetComponent<Renderer>());
                Object.DestroyImmediate(blocker.GetComponent<MeshFilter>());
            }
            // lighthouse landmark near the end
            Vector3 lh = new Vector3(b.max.x + 14f, 0, b.max.z - 30f);
            Cyl(lh + Vector3.up * 9f, 3f, 18f, new Color(0.95f, 0.95f, 0.92f), false);
            Cyl(lh + Vector3.up * 6f, 3.05f, 2f, new Color(0.8f, 0.25f, 0.25f), false);
            Cyl(lh + Vector3.up * 13f, 3.05f, 2f, new Color(0.8f, 0.25f, 0.25f), false);
            Sphere(lh + Vector3.up * 19.5f, 2f, new Color(1f, 0.95f, 0.7f), true);
        }

        void BuildRoute()
        {
            for (int i = 1; i < map.path.Count; i++)
            {
                Vector3 a = map.path[i - 1], c = map.path[i];
                Vector3 mid = (a + c) * 0.5f;
                float len = Vector3.Distance(a, c);
                var rot = Quaternion.LookRotation(c - a);
                Box(mid + Vector3.up * 0.01f, new Vector3(7f, 0.02f, len + 7f), Street, false, null, rot);
                Vector3 right = rot * Vector3.right;
                Box(mid + right * 0.75f + Vector3.up * 0.04f, new Vector3(0.15f, 0.06f, len + 1.2f), Rail, false, null, rot);
                Box(mid - right * 0.75f + Vector3.up * 0.04f, new Vector3(0.15f, 0.06f, len + 1.2f), Rail, false, null, rot);
            }
            // finish marker
            Vector3 end = map.path[map.path.Count - 1];
            Box(end + Vector3.up * 0.03f, new Vector3(7f, 0.03f, 1f), new Color(1f, 0.85f, 0.3f), false);
        }

        SpawnRoom SpawnBeside(Team team, int phase, float d, float side, float offset)
        {
            Vector3 r = Right(d) * side;
            Vector3 c = Eval(d) + r * offset;
            float yaw = Mathf.Atan2(-r.x, -r.z) * Mathf.Rad2Deg;
            return MakeSpawn(team, phase, c, yaw);
        }

        SpawnRoom MakeSpawn(Team team, int phase, Vector3 center, float yaw)
        {
            var s = new SpawnRoom { team = team, phase = phase, center = new Vector3(center.x, 0f, center.z), yaw = yaw };
            blockedCircles.Add(new Vector4(s.center.x, 0, s.center.z, 9.5f));
            Vector3 door = s.center + s.Forward * 9f;
            blockedCircles.Add(new Vector4(door.x, 0, door.z, 5f));
            return s;
        }

        void BuildSpawnRoom(SpawnRoom s)
        {
            var t = new GameObject((s.team == Team.Attack ? "AttackSpawn" : "DefendSpawn") + s.phase).transform;
            t.SetParent(geo, false);
            t.position = s.center;
            t.rotation = Quaternion.Euler(0, s.yaw, 0);
            float w = 12f, dp = 9f, h = 4.5f;
            Color wall = new Color(0.88f, 0.86f, 0.82f);
            Color tc = TeamColors.Of(s.team);
            Local(t, new Vector3(0, h / 2, -dp / 2), new Vector3(w, h, 0.6f), wall);
            Local(t, new Vector3(-w / 2, h / 2, 0), new Vector3(0.6f, h, dp), wall);
            Local(t, new Vector3(w / 2, h / 2, 0), new Vector3(0.6f, h, dp), wall);
            Local(t, new Vector3(-(w / 2 - 2f), h / 2, dp / 2), new Vector3(4f, h, 0.6f), wall);
            Local(t, new Vector3(w / 2 - 2f, h / 2, dp / 2), new Vector3(4f, h, 0.6f), wall);
            Local(t, new Vector3(0, h + 0.25f, 0), new Vector3(w + 0.6f, 0.5f, dp + 0.6f), new Color(0.4f, 0.42f, 0.46f));
            s.glow.Add(LocalUnlit(t, new Vector3(0, 0.03f, 0), new Vector3(w - 0.8f, 0.03f, dp - 0.8f), new Color(tc.r, tc.g, tc.b, 0.35f)));
            s.glow.Add(LocalUnlit(t, new Vector3(0, h - 0.4f, dp / 2 + 0.31f), new Vector3(4f, 0.25f, 0.05f), tc));
            s.glow.Add(LocalUnlit(t, new Vector3(0, h - 0.3f, -dp / 2 + 0.31f), new Vector3(w - 1f, 0.15f, 0.05f), tc));
            var b = new Bounds(s.center + Vector3.up * h / 2, new Vector3(w + 1, h, w + 1));
            solids.Add(b);
        }

        Transform Local(Transform parent, Vector3 lp, Vector3 size, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = lp;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = MaterialLib.Lit(c);
            go.isStatic = true;
            return go.transform;
        }

        Renderer LocalUnlit(Transform parent, Vector3 lp, Vector3 size, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = lp;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = MaterialLib.Unlit(c);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        void BuildGates(float d)
        {
            Vector3 p = Eval(d);
            Vector3 r = Right(d);
            var rot = Quaternion.LookRotation(Dir(d));
            Color stone = new Color(0.82f, 0.78f, 0.7f);
            Vector3 l = p - r * 6.5f, rr = p + r * 6.5f;
            Box(l + Vector3.up * 3.5f, new Vector3(1.4f, 7f, 1.4f), stone, true, null, rot);
            Box(rr + Vector3.up * 3.5f, new Vector3(1.4f, 7f, 1.4f), stone, true, null, rot);
            Box(p + Vector3.up * 7.4f, new Vector3(15f, 1.2f, 1.6f), stone, true, null, rot);
            Box(p + Vector3.up * 6.4f - Dir(d) * 0.82f, new Vector3(9f, 0.6f, 0.05f), new Color(0.95f, 0.8f, 0.3f), false, null, rot);
            solids.Add(new Bounds(l + Vector3.up * 3.5f, new Vector3(2f, 7f, 2f)));
            solids.Add(new Bounds(rr + Vector3.up * 3.5f, new Vector3(2f, 7f, 2f)));
        }

        void BuildBuildings()
        {
            for (int pass = 0; pass < 2; pass++)
            {
                float minOff = pass == 0 ? 13f : 26f;
                float maxOff = pass == 0 ? 20f : 36f;
                for (float d = -10f; d <= total + 10f; d += pass == 0 ? 8f : 11f)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        if (pass == 0 && Chance(0.18f)) continue; // leave alleys
                        Vector3 baseP = Eval(d) + Right(d) * side * Range(minOff, maxOff);
                        if (d < 0) baseP += Dir(0) * d;
                        if (d > total) baseP += Dir(total) * (d - total);
                        var size = new Vector3(Range(6f, 12f), pass == 0 ? Range(5f, 10f) : Range(8f, 15f), Range(6f, 12f));
                        TryBuilding(baseP, size, pass == 0 ? 10f : 12f);
                    }
                }
            }
        }

        bool TryBuilding(Vector3 center, Vector3 size, float clearance)
        {
            var b = new Bounds(new Vector3(center.x, size.y / 2f, center.z), size);
            if (!ClearOfPath(b, clearance) || !Free(b, 2.2f)) return false;
            solids.Add(b);
            Color wallC = Pick(WallColors);
            Box(b.center, size, wallC);
            // roof
            Color roofC = Pick(RoofColors);
            Box(new Vector3(b.center.x, size.y + 0.2f, b.center.z), new Vector3(size.x + 0.6f, 0.4f, size.z + 0.6f), roofC, false);
            if (Chance(0.5f))
                Box(new Vector3(b.center.x, size.y + 0.9f, b.center.z), new Vector3(size.x * 0.5f, 1f, size.z * 0.5f), Color.Lerp(wallC, Color.white, 0.2f), false);
            // windows + door on each face
            for (int face = 0; face < 4; face++)
            {
                Vector3 n = face == 0 ? Vector3.forward : face == 1 ? Vector3.back : face == 2 ? Vector3.right : Vector3.left;
                Vector3 t = new Vector3(n.z, 0, -n.x);
                float faceW = Mathf.Abs(n.x) > 0 ? size.z : size.x;
                float half = Mathf.Abs(n.x) > 0 ? size.x / 2f : size.z / 2f;
                Vector3 fc = new Vector3(b.center.x, 0, b.center.z) + n * (half + 0.03f);
                int cols = Mathf.Max(1, Mathf.FloorToInt(faceW / 2.6f));
                for (int floor = 0; floor < 3; floor++)
                {
                    float y = 2.4f + floor * 3f;
                    if (y > size.y - 1.2f) break;
                    for (int cI = 0; cI < cols; cI++)
                    {
                        float off = (cI - (cols - 1) / 2f) * 2.6f;
                        var rot = Quaternion.LookRotation(n);
                        Box(fc + t * off + Vector3.up * y, new Vector3(0.9f, 1.2f, 0.05f), Window, false, null, rot);
                    }
                }
                if (Chance(0.35f))
                {
                    var rot = Quaternion.LookRotation(n);
                    Box(fc + n * 0.6f + Vector3.up * 2.6f, new Vector3(Mathf.Min(faceW - 1f, 4f), 0.1f, 1.2f), Pick(AwningColors), false, null, rot * Quaternion.Euler(-12f, 0, 0));
                }
            }
            return true;
        }

        void BuildPlatforms()
        {
            int side = 1;
            for (float d = 18f; d < total - 8f; d += 21f)
            {
                side = -side;
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    Vector3 c = Eval(d) + Right(d) * side * Range(9f, 12f);
                    // ramp descends back toward the attackers' side
                    Vector3 back = -Dir(d);
                    Vector3 axis = Mathf.Abs(back.x) > Mathf.Abs(back.z) ? new Vector3(Mathf.Sign(back.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(back.z));
                    if (attempt >= 2) axis = -axis;
                    if (TryPlatform(c, Range(2.6f, 3.4f), axis)) break;
                }
            }
        }

        bool TryPlatform(Vector3 c, float h, Vector3 rampDir)
        {
            float half = 3.2f;
            float L = h / Mathf.Tan(27f * Mathf.Deg2Rad);
            var pb = new Bounds(new Vector3(c.x, h / 2f, c.z), new Vector3(half * 2f, h, half * 2f));
            Vector3 rampCenter = c + rampDir * (half + L / 2f);
            Vector3 rampSize = Mathf.Abs(rampDir.x) > 0 ? new Vector3(L, h, 2.8f) : new Vector3(2.8f, h, L);
            var rb = new Bounds(new Vector3(rampCenter.x, h / 2f, rampCenter.z), rampSize);
            if (!ClearOfPath(pb, 5.5f) || !ClearOfPath(rb, 3.5f)) return false;
            if (!Free(pb, 1.5f) || !Free(rb, 1.5f)) return false;
            // keep the ramp foot accessible
            var foot = new Bounds(new Vector3(c.x, h / 2f, c.z) + rampDir * (half + L + 1.5f), new Vector3(2.5f, h, 2.5f));
            if (!Free(foot, 0.5f)) return false;
            solids.Add(pb);
            solids.Add(rb);
            solids.Add(foot);

            Color stone = new Color(0.7f, 0.66f, 0.6f);
            Box(pb.center, pb.size, stone);
            Box(new Vector3(c.x, h + 0.05f, c.z), new Vector3(half * 2f + 0.2f, 0.1f, half * 2f + 0.2f), new Color(0.6f, 0.5f, 0.42f), false);
            // parapet on the two sides perpendicular to the ramp gives cover up top
            Vector3 perp = new Vector3(rampDir.z, 0, -rampDir.x);
            Box(new Vector3(c.x, h, c.z) - rampDir * (half - 0.25f) + Vector3.up * 0.55f,
                Mathf.Abs(rampDir.x) > 0 ? new Vector3(0.5f, 1.1f, half * 2f) : new Vector3(half * 2f, 1.1f, 0.5f), stone);
            Box(new Vector3(c.x, h, c.z) + perp * (half - 0.25f) + Vector3.up * 0.55f,
                Mathf.Abs(perp.x) > 0 ? new Vector3(0.5f, 1.1f, half * 1.2f) : new Vector3(half * 1.2f, 1.1f, 0.5f), stone);

            Vector3 top = new Vector3(c.x, h, c.z) + rampDir * half;
            Vector3 bottom = new Vector3(c.x, 0f, c.z) + rampDir * (half + L);
            var rot = Quaternion.LookRotation(top - bottom);
            Vector3 mid = (top + bottom) * 0.5f - (rot * Vector3.up) * 0.2f;
            Box(mid, new Vector3(2.8f, 0.4f, Vector3.Distance(top, bottom) + 0.3f), new Color(0.62f, 0.55f, 0.48f), true, null, rot);
            return true;
        }

        void BuildCover()
        {
            for (float d = 6f; d < total - 3f; d += 6.5f)
            {
                int side = Chance(0.5f) ? 1 : -1;
                int count = Chance(0.35f) ? 2 : 1;
                for (int k = 0; k < count; k++, side = -side)
                {
                    Vector3 p = Eval(d + Range(-2f, 2f)) + Right(d) * side * Range(4.2f, 8.5f);
                    float roll = (float)rng.NextDouble();
                    if (roll < 0.45f)
                    {
                        float s = Range(1.2f, 1.6f);
                        var b = new Bounds(new Vector3(p.x, s / 2f, p.z), new Vector3(s, s, s));
                        if (!ClearOfPath(b, 3.2f) || !Free(b, 0.8f)) continue;
                        solids.Add(b);
                        Box(b.center, b.size, Wood, true, null, Quaternion.Euler(0, Range(0f, 30f), 0));
                        if (Chance(0.4f)) Box(b.center + Vector3.up * (s * 0.5f + 0.45f), Vector3.one * 0.9f, Color.Lerp(Wood, Color.black, 0.2f), true, null, Quaternion.Euler(0, Range(0f, 45f), 0));
                    }
                    else if (roll < 0.8f)
                    {
                        Vector3 f = Dir(d);
                        bool alongX = Mathf.Abs(f.x) > Mathf.Abs(f.z);
                        var size = alongX ? new Vector3(3.6f, 1.2f, 0.6f) : new Vector3(0.6f, 1.2f, 3.6f);
                        var b = new Bounds(new Vector3(p.x, 0.6f, p.z), size);
                        if (!ClearOfPath(b, 3.2f) || !Free(b, 1f)) continue;
                        solids.Add(b);
                        Box(b.center, size, new Color(0.8f, 0.76f, 0.68f));
                        Box(b.center + Vector3.up * 0.65f, size + new Vector3(0.1f, -1.1f, 0.1f), new Color(0.65f, 0.6f, 0.55f), false);
                    }
                    else
                    {
                        var b = new Bounds(new Vector3(p.x, 0.6f, p.z), new Vector3(1.8f, 1.2f, 1.8f));
                        if (!ClearOfPath(b, 3.2f) || !Free(b, 0.8f)) continue;
                        solids.Add(b);
                        Cyl(p + new Vector3(-0.4f, 0.6f, 0), 0.4f, 1.2f, new Color(0.25f, 0.45f, 0.6f));
                        Cyl(p + new Vector3(0.45f, 0.6f, 0.2f), 0.4f, 1.2f, new Color(0.7f, 0.3f, 0.25f));
                    }
                }
            }
        }

        void BuildDecor()
        {
            // lamp posts along the route
            for (float d = 10f; d < total; d += 18f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 p = Eval(d) + Right(d) * side * 4.3f;
                    var b = new Bounds(new Vector3(p.x, 2f, p.z), new Vector3(0.4f, 4f, 0.4f));
                    if (!ClearOfPath(b, 3.6f) || !Free(b, 0.3f)) continue;
                    solids.Add(b);
                    Cyl(p + Vector3.up * 2f, 0.1f, 4f, new Color(0.2f, 0.22f, 0.25f));
                    Sphere(p + Vector3.up * 4.1f, 0.25f, new Color(1f, 0.92f, 0.7f), true);
                }
            }
            // palm trees in open spots
            for (int i = 0; i < 70; i++)
            {
                float d = Range(0f, total);
                Vector3 p = Eval(d) + Right(d) * (Chance(0.5f) ? 1 : -1) * Range(9f, 24f);
                var b = new Bounds(new Vector3(p.x, 3f, p.z), new Vector3(0.8f, 6f, 0.8f));
                if (!ClearOfPath(b, 6f) || !Free(b, 1.5f)) continue;
                solids.Add(b);
                float h = Range(4.5f, 6.5f);
                Cyl(p + Vector3.up * h / 2f, 0.22f, h, new Color(0.5f, 0.38f, 0.25f));
                Color leaf = new Color(0.25f, Range(0.5f, 0.62f), 0.25f);
                for (int k = 0; k < 5; k++)
                {
                    var leafT = Box(p + Vector3.up * h, new Vector3(0.5f, 0.08f, 2.6f), leaf, false, null,
                        Quaternion.Euler(0, k * 72f + Range(0f, 20f), 0) * Quaternion.Euler(18f, 0, 0) * Quaternion.Euler(0, 0, 0));
                    leafT.position += leafT.forward * 1.1f;
                }
            }
            // boats in the harbor for atmosphere
            var bb = map.bounds;
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = new Vector3(Chance(0.5f) ? bb.min.x - Range(8f, 30f) : bb.max.x + Range(8f, 30f), -1f, Range(bb.min.z, bb.max.z));
                var rot = Quaternion.Euler(0, Range(0f, 360f), 0);
                Box(p + Vector3.up * 0.4f, new Vector3(3f, 1.2f, 8f), Pick(AwningColors), false, null, rot);
                Box(p + Vector3.up * 1.4f + rot * Vector3.back * 1.5f, new Vector3(2.2f, 1.2f, 2.5f), Color.white, false, null, rot);
            }
        }

        void BuildHealthPacks()
        {
            var parent = new GameObject("HealthPacks").transform;
            parent.SetParent(root, false);
            int side = 1;
            for (float d = 14f; d < total - 5f; d += 24f)
            {
                side = -side;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    float off = 7f + attempt * 1.2f;
                    Vector3 p = Eval(d + attempt) + Right(d) * side * off;
                    RaycastHit h;
                    if (!Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out h, 40f, Layers.World, QueryTriggerInteraction.Ignore)) continue;
                    if (h.point.y > 0.5f) continue;
                    if (Physics.CheckCapsule(h.point + Vector3.up * 0.6f, h.point + Vector3.up * 1.6f, 0.9f, Layers.World, QueryTriggerInteraction.Ignore)) continue;
                    map.healthPacks.Add(HealthPack.Create(h.point, (int)(d / 24f) % 3 == 1, parent));
                    break;
                }
            }
        }
    }
}
