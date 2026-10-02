using System.Collections.Generic;
using UnityEngine;

namespace NexusStrike
{
    /// <summary>Simulated projectile using sphere casts (no rigidbody). Calls onImpact once.</summary>
    public class Projectile : MonoBehaviour
    {
        public delegate void ImpactHandler(Projectile p, Vector3 point, Vector3 normal, Combatant direct, Barrier barrier);

        public Combatant owner;
        public Team team;
        public Vector3 velocity;
        public float gravity;
        public float radius = 0.12f;
        public float lifetime = 5f;
        public float fuse = -1f;          // explode after this many seconds (grenades)
        public int bounces;               // world bounces left before impact (-1 = infinite until fuse)
        public bool hitsAllies;
        public bool stickToGround;        // stops instead of bouncing (traps)
        public ImpactHandler onImpact;
        public System.Action<Projectile> onTick;

        float born;
        bool done;
        bool stuck;
        static readonly RaycastHit[] buffer = new RaycastHit[32];
        static readonly List<RaycastHit> sorted = new List<RaycastHit>();

        public float Age { get { return Time.time - born; } }

        public static Projectile Spawn(Combatant owner, Vector3 pos, Vector3 velocity, float radius, Color color,
            float visualSize, ImpactHandler onImpact, bool trail = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "Projectile";
            go.layer = Layers.IgnoreRaycast;
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * visualSize;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = MaterialLib.Unlit(color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (trail)
            {
                var tr = go.AddComponent<TrailRenderer>();
                tr.sharedMaterial = MaterialLib.Unlit(new Color(color.r, color.g, color.b, 0.5f));
                tr.time = 0.18f;
                tr.startWidth = visualSize * 0.8f;
                tr.endWidth = 0f;
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var p = go.AddComponent<Projectile>();
            p.owner = owner;
            p.team = owner.team;
            p.velocity = velocity;
            p.radius = radius;
            p.onImpact = onImpact;
            p.born = Time.time;
            return p;
        }

        void Update()
        {
            if (done) return;
            if (owner == null) { Kill(); return; }
            float dt = Time.deltaTime;
            if (onTick != null) onTick(this);
            if (done) return;

            if (fuse > 0f && Age >= fuse) { Impact(transform.position, Vector3.up, null, null); return; }
            if (Age >= lifetime)
            {
                if (fuse > 0f || stickToGround) Impact(transform.position, Vector3.up, null, null);
                else Destroy(gameObject);
                return;
            }
            if (stuck) { CheckProximity(); return; }

            velocity.y -= gravity * dt;
            Vector3 step = velocity * dt;
            float dist = step.magnitude;
            if (dist < 1e-5f) return;
            Vector3 dir = step / dist;
            Vector3 pos = transform.position;

            int n = Physics.SphereCastNonAlloc(pos, radius, dir, buffer, dist, Layers.Shootable, QueryTriggerInteraction.Collide);
            sorted.Clear();
            for (int i = 0; i < n; i++) sorted.Add(buffer[i]);
            sorted.Sort((a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < sorted.Count; i++)
            {
                var h = sorted[i];
                Vector3 point = (h.distance <= 0f && h.point == Vector3.zero) ? pos : h.point;
                Vector3 normal = h.distance <= 0f ? -dir : h.normal;
                var col = h.collider;
                var cb = col.GetComponent<Combatant>();
                if (cb != null)
                {
                    if (cb == owner || !cb.alive) continue;
                    if (cb.team == team && !hitsAllies) continue;
                    if (stickToGround) continue;
                    Impact(point, normal, cb, null);
                    return;
                }
                var bar = col.GetComponent<Barrier>();
                if (bar != null)
                {
                    if (bar.team == team) continue;
                    Impact(point, normal, null, bar);
                    return;
                }
                if (col.isTrigger && col.GetComponent<ShotBlocker>() == null) continue;

                // world geometry
                if (stickToGround)
                {
                    transform.position = point + normal * radius;
                    if (normal.y > 0.6f) { stuck = true; velocity = Vector3.zero; }
                    else velocity = Vector3.Reflect(velocity, normal) * 0.3f;
                    return;
                }
                if (bounces != 0)
                {
                    if (bounces > 0) bounces--;
                    velocity = Vector3.Reflect(velocity, normal) * 0.55f;
                    transform.position = point + normal * (radius + 0.02f);
                    return;
                }
                Impact(point, normal, null, null);
                return;
            }
            transform.position = pos + step;
            if (velocity.sqrMagnitude > 1f) transform.rotation = Quaternion.LookRotation(velocity);
        }

        void CheckProximity()
        {
            foreach (var c in Combatant.All)
            {
                if (!c.alive || c.team == team) continue;
                if (Vector3.Distance(c.Feet, transform.position) < 1.3f)
                {
                    Impact(transform.position, Vector3.up, c, null);
                    return;
                }
            }
        }

        public void Impact(Vector3 point, Vector3 normal, Combatant direct, Barrier barrier)
        {
            if (done) return;
            done = true;
            transform.position = point;
            if (onImpact != null) onImpact(this, point, normal, direct, barrier);
            Destroy(gameObject);
        }

        public void Kill()
        {
            done = true;
            Destroy(gameObject);
        }
    }
}
