using System.Collections.Generic;
using UnityEngine;

namespace NexusStrike
{
    /// <summary>Escort objective: moves along a polyline while attackers stand near it uncontested.</summary>
    public class Payload : MonoBehaviour
    {
        public List<Vector3> path = new List<Vector3>();
        float[] cumulative;
        public float total;
        public float distance;
        public float[] checkpoints = new float[0];
        public int checkpointsReached;

        public int attackersOn, defendersOn;
        public bool Contested { get { return attackersOn > 0 && defendersOn > 0; } }
        public bool Moving { get { return attackersOn > 0 && defendersOn == 0; } }
        public bool RollingBack { get; private set; }
        public float lastAttackerTime;
        public const float Radius = 4.2f;
        public const float BaseSpeed = 1.5f;

        Transform visual;
        Material ringMat;
        Material coreMat;
        Transform core;
        Transform[] wheels;

        public float Progress { get { return total > 0f ? distance / total : 0f; } }
        public Vector3 Position { get { return Evaluate(distance); } }
        public Vector3 Forward { get { return Direction(distance); } }

        public void Init(List<Vector3> points, float[] checkpointFractions)
        {
            path = points;
            cumulative = new float[path.Count];
            total = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                total += Vector3.Distance(path[i - 1], path[i]);
                cumulative[i] = total;
            }
            checkpoints = new float[checkpointFractions.Length];
            for (int i = 0; i < checkpoints.Length; i++) checkpoints[i] = checkpointFractions[i] * total;
            BuildVisual();
            ResetState();
        }

        public void ResetState()
        {
            distance = 0f;
            checkpointsReached = 0;
            attackersOn = defendersOn = 0;
            lastAttackerTime = Time.time;
            RollingBack = false;
            Place();
        }

        public Vector3 Evaluate(float d)
        {
            d = Mathf.Clamp(d, 0f, total);
            for (int i = 1; i < path.Count; i++)
            {
                if (d <= cumulative[i])
                {
                    float seg = cumulative[i] - cumulative[i - 1];
                    float t = seg > 0f ? (d - cumulative[i - 1]) / seg : 0f;
                    return Vector3.Lerp(path[i - 1], path[i], t);
                }
            }
            return path[path.Count - 1];
        }

        public Vector3 Direction(float d)
        {
            d = Mathf.Clamp(d, 0f, total);
            for (int i = 1; i < path.Count; i++)
                if (d <= cumulative[i] + 0.001f) return (path[i] - path[i - 1]).normalized;
            return (path[path.Count - 1] - path[path.Count - 2]).normalized;
        }

        /// <summary>Distance along the path of the closest point to p (used for checkpoint-relative positions).</summary>
        public float Project(Vector3 p)
        {
            float best = float.MaxValue, bestD = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 a = path[i - 1], b = path[i];
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
                Vector3 q = a + ab * t;
                float dist = (new Vector3(p.x - q.x, 0, p.z - q.z)).sqrMagnitude;
                if (dist < best) { best = dist; bestD = cumulative[i - 1] + ab.magnitude * t; }
            }
            return bestD;
        }

        /// <summary>Returns the index of a checkpoint reached this tick, -2 if the end was reached, -1 otherwise.</summary>
        public int Tick(float dt, bool setup)
        {
            attackersOn = defendersOn = 0;
            Vector3 pos = Position;
            foreach (var c in Combatant.All)
            {
                if (!c.alive) continue;
                Vector3 d = c.Feet - pos;
                if (Mathf.Abs(d.y) > 3f) continue;
                d.y = 0f;
                if (d.magnitude > Radius) continue;
                if (c.team == Team.Attack) attackersOn++;
                else defendersOn++;
            }
            if (attackersOn > 0) lastAttackerTime = Time.time;
            RollingBack = false;
            int result = -1;
            if (!setup)
            {
                if (Moving)
                {
                    float speed = BaseSpeed * (1f + 0.15f * Mathf.Min(attackersOn - 1, 2));
                    distance = Mathf.Min(total, distance + speed * dt);
                    foreach (var c in Combatant.All)
                        if (c.alive && c.team == Team.Attack && Vector3.Distance(c.Feet, pos) < Radius) c.ApplyHeal(null, 10f * dt);
                }
                else if (attackersOn == 0 && Time.time - lastAttackerTime > 10f)
                {
                    float floor = checkpointsReached > 0 ? checkpoints[checkpointsReached - 1] : 0f;
                    if (distance > floor)
                    {
                        distance = Mathf.Max(floor, distance - 0.5f * dt);
                        RollingBack = true;
                    }
                }
                if (checkpointsReached < checkpoints.Length && distance >= checkpoints[checkpointsReached])
                {
                    result = checkpointsReached;
                    checkpointsReached++;
                }
                if (distance >= total - 0.01f) result = -2;
            }
            Place();
            UpdateVisual(dt);
            return result;
        }

        void Place()
        {
            if (path.Count < 2) return;
            transform.position = Position;
            Vector3 f = Forward;
            if (f.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(f), 0.1f);
        }

        void BuildVisual()
        {
            visual = new GameObject("PayloadVisual").transform;
            visual.SetParent(transform, false);
            Color hull = new Color(0.32f, 0.34f, 0.4f);
            Color trim = new Color(0.85f, 0.75f, 0.4f);
            ModelUtil.Part(PrimitiveType.Cube, visual, new Vector3(0, 0.75f, 0), new Vector3(2.2f, 0.9f, 3.6f), hull);
            ModelUtil.Part(PrimitiveType.Cube, visual, new Vector3(0, 1.25f, 0), new Vector3(2.0f, 0.12f, 3.4f), trim);
            ModelUtil.Part(PrimitiveType.Cube, visual, new Vector3(0, 1.6f, -1.1f), new Vector3(1.6f, 0.7f, 1f), hull);
            ModelUtil.Part(PrimitiveType.Cube, visual, new Vector3(0, 1.55f, 1.25f), new Vector3(1.8f, 0.5f, 0.6f), hull);
            core = ModelUtil.Part(PrimitiveType.Sphere, visual, new Vector3(0, 1.8f, 0.1f), Vector3.one * 1.1f, Color.white, true);
            coreMat = MaterialLib.NewUnlit(new Color(0.7f, 0.9f, 1f));
            core.GetComponent<Renderer>().sharedMaterial = coreMat;
            for (int i = 0; i < 4; i++)
                ModelUtil.Part(PrimitiveType.Cube, visual, new Vector3(0, 1.8f, 0.1f), new Vector3(0.08f, 1.5f, 0.08f), trim, false, new Vector3(0, i * 45f, 45f));
            wheels = new Transform[4];
            int w = 0;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    wheels[w++] = ModelUtil.Part(PrimitiveType.Cylinder, visual, new Vector3(sx * 1.15f, 0.4f, sz * 1.2f), new Vector3(0.8f, 0.12f, 0.8f), new Color(0.15f, 0.15f, 0.17f), false, new Vector3(0, 0, 90));

            var ring = ModelUtil.Part(PrimitiveType.Cylinder, transform, new Vector3(0, 0.04f, 0), new Vector3(Radius * 2f, 0.01f, Radius * 2f), Color.white, true);
            ringMat = MaterialLib.NewUnlit(new Color(1f, 1f, 1f, 0.12f));
            ring.GetComponent<Renderer>().sharedMaterial = ringMat;

            // solid to bullets, passable for movement
            var col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0, 1.1f, 0);
            col.size = new Vector3(2.2f, 1.8f, 3.6f);
            gameObject.AddComponent<ShotBlocker>();
        }

        void UpdateVisual(float dt)
        {
            Color state;
            var gm = GameManager.I;
            Team mine = gm != null ? gm.playerTeam : Team.Attack;
            if (Contested) state = new Color(1f, 0.6f, 0.15f);
            else if (Moving) state = mine == Team.Attack ? TeamColors.Ally : TeamColors.Enemy;
            else state = new Color(0.9f, 0.9f, 0.9f);
            ringMat.color = new Color(state.r, state.g, state.b, 0.18f + 0.06f * Mathf.Sin(Time.time * 4f));
            coreMat.color = Color.Lerp(coreMat.color, state, dt * 4f);
            core.localRotation = Quaternion.Euler(0, Time.time * 60f, 0);
            if (Moving)
                foreach (var wl in wheels) wl.Rotate(0, BaseSpeed * dt * 140f, 0, Space.Self);
        }
    }

    /// <summary>Health pack pickup (small or large) that respawns after a delay.</summary>
    public class HealthPack : MonoBehaviour
    {
        public bool large;
        public float amount;
        public float respawnTime;
        public bool available = true;
        float respawnAt;
        Transform visual;

        public static HealthPack Create(Vector3 pos, bool large, Transform parent)
        {
            var go = new GameObject(large ? "HealthPackLarge" : "HealthPackSmall");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var hp = go.AddComponent<HealthPack>();
            hp.large = large;
            hp.amount = large ? 250f : 75f;
            hp.respawnTime = large ? 15f : 10f;
            ModelUtil.Part(PrimitiveType.Cylinder, go.transform, new Vector3(0, 0.05f, 0), new Vector3(large ? 1.4f : 1f, 0.05f, large ? 1.4f : 1f), new Color(0.3f, 0.3f, 0.33f));
            hp.visual = new GameObject("Cross").transform;
            hp.visual.SetParent(go.transform, false);
            hp.visual.localPosition = new Vector3(0, 0.7f, 0);
            float s = large ? 0.7f : 0.45f;
            Color c = new Color(1f, 0.85f, 0.3f);
            ModelUtil.Part(PrimitiveType.Cube, hp.visual, Vector3.zero, new Vector3(s, s * 0.32f, s * 0.32f), c, true);
            ModelUtil.Part(PrimitiveType.Cube, hp.visual, Vector3.zero, new Vector3(s * 0.32f, s, s * 0.32f), c, true);
            return hp;
        }

        public void ResetState()
        {
            available = true;
            visual.gameObject.SetActive(true);
        }

        void Update()
        {
            if (!available)
            {
                if (Time.time >= respawnAt)
                {
                    available = true;
                    visual.gameObject.SetActive(true);
                }
                return;
            }
            visual.Rotate(0, 90f * Time.deltaTime, 0);
            visual.localPosition = new Vector3(0, 0.7f + Mathf.Sin(Time.time * 2f) * 0.1f, 0);
            foreach (var c in Combatant.All)
            {
                if (!c.alive || c.Total >= c.MaxTotal - 0.5f) continue;
                Vector3 d = c.Feet - transform.position;
                if (Mathf.Abs(d.y) > 1.5f) continue;
                d.y = 0f;
                if (d.magnitude > 1.1f + c.def.radius) continue;
                c.ApplyHeal(null, amount);
                available = false;
                respawnAt = Time.time + respawnTime;
                visual.gameObject.SetActive(false);
                if (c.isPlayer) Sfx.Play2D("pickup", 0.8f);
                else Sfx.Play("pickup", transform.position, 0.6f);
                break;
            }
        }
    }
}
