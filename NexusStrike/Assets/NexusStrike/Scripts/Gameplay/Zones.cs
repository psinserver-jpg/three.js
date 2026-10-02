using System.Collections.Generic;
using UnityEngine;

namespace NexusStrike
{
    /// <summary>A ground area effect that ticks for a duration (heal fields, tesla field, gravity well).</summary>
    public class TimedZone : MonoBehaviour
    {
        public Combatant owner;
        public Team team;
        public float radius;
        public float height = 4f;
        public float until;
        public System.Action<TimedZone, float> onTick;
        public System.Action<TimedZone> onEnd;
        Material mat;
        Material coreMat;
        Transform core;
        Color color;

        public static TimedZone Create(Combatant owner, Vector3 pos, float radius, float duration, Color color, bool withCore = false)
        {
            var go = new GameObject("Zone");
            go.transform.position = pos;
            var z = go.AddComponent<TimedZone>();
            z.owner = owner;
            z.team = owner.team;
            z.radius = radius;
            z.until = Time.time + duration;
            z.color = color;

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.layer = Layers.IgnoreRaycast;
            disc.transform.SetParent(go.transform, false);
            disc.transform.localPosition = Vector3.up * 0.06f;
            disc.transform.localScale = new Vector3(radius * 2f, 0.03f, radius * 2f);
            z.mat = MaterialLib.NewUnlit(new Color(color.r, color.g, color.b, 0.3f));
            var r = disc.GetComponent<Renderer>();
            r.sharedMaterial = z.mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(rim.GetComponent<Collider>());
            rim.layer = Layers.IgnoreRaycast;
            rim.transform.SetParent(go.transform, false);
            rim.transform.localPosition = Vector3.up * 0.6f;
            rim.transform.localScale = new Vector3(radius * 2f, 0.6f, radius * 2f);
            var rr = rim.GetComponent<Renderer>();
            rr.sharedMaterial = MaterialLib.Unlit(new Color(color.r, color.g, color.b, 0.08f));
            rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (withCore)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(c.GetComponent<Collider>());
                c.layer = Layers.IgnoreRaycast;
                c.transform.SetParent(go.transform, false);
                c.transform.localPosition = Vector3.up * 1.2f;
                z.core = c.transform;
                z.coreMat = MaterialLib.NewUnlit(new Color(color.r * 0.4f, color.g * 0.4f, color.b * 0.6f, 0.9f));
                c.GetComponent<Renderer>().sharedMaterial = z.coreMat;
            }
            return z;
        }

        public List<Combatant> Inside(bool allies)
        {
            var list = new List<Combatant>();
            Vector3 p = transform.position;
            foreach (var c in Combatant.All)
            {
                if (!c.alive) continue;
                if ((c.team == team) != allies) continue;
                Vector3 d = c.Feet - p;
                if (d.y < -1.5f || d.y > height) continue;
                d.y = 0f;
                if (d.magnitude <= radius + c.def.radius) list.Add(c);
            }
            return list;
        }

        void Update()
        {
            if (owner == null) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            if (onTick != null) onTick(this, dt);
            if (mat != null)
            {
                var c = color;
                c.a = 0.22f + 0.1f * Mathf.Sin(Time.time * 6f);
                mat.color = c;
            }
            if (core != null)
            {
                float s = 1.2f + 0.3f * Mathf.Sin(Time.time * 12f);
                core.localScale = Vector3.one * s;
                core.Rotate(0, 300f * dt, 0);
            }
            if (Time.time >= until)
            {
                if (onEnd != null) onEnd(this);
                Destroy(gameObject);
            }
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
            if (coreMat != null) Destroy(coreMat);
        }
    }

    /// <summary>Rook's automated sentry: tracks and shoots the nearest visible enemy.</summary>
    public class Turret : MonoBehaviour
    {
        public Combatant owner;
        public Barrier body;
        Transform head;
        Transform barrel;
        float nextShot;
        Combatant target;
        float retarget;
        public const float Range = 26f;

        public static Turret Create(Combatant owner, Vector3 pos, float yaw)
        {
            var go = new GameObject("Turret");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var t = go.AddComponent<Turret>();
            t.owner = owner;
            Color tc = TeamColors.Relative(owner.team);
            Color metal = new Color(0.4f, 0.4f, 0.45f);
            ModelUtil.Part(PrimitiveType.Cylinder, go.transform, new Vector3(0, 0.25f, 0), new Vector3(0.7f, 0.25f, 0.7f), metal);
            ModelUtil.Part(PrimitiveType.Cylinder, go.transform, new Vector3(0, 0.62f, 0), new Vector3(0.18f, 0.2f, 0.18f), new Color(0.25f, 0.25f, 0.28f));
            t.head = new GameObject("Head").transform;
            t.head.SetParent(go.transform, false);
            t.head.localPosition = new Vector3(0, 0.95f, 0);
            ModelUtil.Part(PrimitiveType.Cube, t.head, Vector3.zero, new Vector3(0.45f, 0.32f, 0.55f), new Color(0.9f, 0.5f, 0.2f));
            ModelUtil.Part(PrimitiveType.Cube, t.head, new Vector3(0, 0.05f, 0.28f), new Vector3(0.3f, 0.08f, 0.02f), tc, true);
            t.barrel = ModelUtil.Part(PrimitiveType.Cylinder, t.head, new Vector3(0, -0.02f, 0.45f), new Vector3(0.09f, 0.22f, 0.09f), metal, false, new Vector3(90, 0, 0));

            t.body = Barrier.CreateBare(owner, go, new Vector3(0.8f, 1.3f, 0.8f), new Vector3(0, 0.65f, 0), 200f);
            t.body.onBroken = () =>
            {
                Fx.Explosion(t.transform.position + Vector3.up * 0.7f, 1.5f, new Color(1f, 0.6f, 0.2f));
                Sfx.Play("explosion", t.transform.position, 0.6f, 1.4f);
                Destroy(go);
            };
            return t;
        }

        void Update()
        {
            if (owner == null) { Destroy(gameObject); return; }
            if (GameManager.I != null && GameManager.I.state == MatchState.Ended) return;
            Vector3 eye = head.position;
            if (Time.time >= retarget || target == null || !target.alive)
            {
                retarget = Time.time + 0.3f;
                target = null;
                float best = Range;
                foreach (var c in Combatant.All)
                {
                    if (!c.alive || c.team == owner.team) continue;
                    float d = Vector3.Distance(eye, c.ChestPos);
                    if (d < best && CombatUtil.LineOfSight(eye, c.ChestPos)) { best = d; target = c; }
                }
            }
            if (target == null) { head.Rotate(0, 40f * Time.deltaTime, 0, Space.World); return; }
            Vector3 dir = target.ChestPos - eye;
            head.rotation = Quaternion.RotateTowards(head.rotation, Quaternion.LookRotation(dir), 360f * Time.deltaTime);
            if (Time.time >= nextShot && Vector3.Angle(head.forward, dir) < 8f)
            {
                nextShot = Time.time + 0.22f;
                Vector3 shotDir = CombatUtil.Spread(dir.normalized, 1.5f);
                var tr = CombatUtil.Trace(owner, eye + head.forward * 0.5f, shotDir, Range + 2f, false);
                Fx.Tracer(barrel.position + head.forward * 0.2f, tr.point, new Color(1f, 0.7f, 0.3f), 0.03f);
                if (tr.combatant != null) tr.combatant.ApplyDamage(owner, 9f, false, "Sentry Turret");
                else if (tr.barrier != null) tr.barrier.Damage(9f, owner);
                Sfx.Play("rifle", eye, 0.35f, 1.5f);
            }
        }
    }
}
