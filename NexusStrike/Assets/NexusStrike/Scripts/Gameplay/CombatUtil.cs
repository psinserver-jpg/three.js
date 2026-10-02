using System.Collections.Generic;
using UnityEngine;

namespace NexusStrike
{
    public struct TraceResult
    {
        public bool hit;
        public Vector3 point;
        public Vector3 normal;
        public float distance;
        public Combatant combatant;
        public Barrier barrier;
    }

    /// <summary>Marker for trigger colliders that should still stop shots (e.g. the payload).</summary>
    public class ShotBlocker : MonoBehaviour { }

    public static class CombatUtil
    {
        static readonly RaycastHit[] hitBuffer = new RaycastHit[64];
        static readonly HitComparer comparer = new HitComparer();

        class HitComparer : IComparer<RaycastHit>
        {
            public int Compare(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); }
        }

        /// <summary>
        /// Hitscan trace honoring hero-shooter rules: shots pass through yourself, friendly heroes
        /// (unless includeAllies) and friendly barriers; enemy barriers and world geometry stop them.
        /// </summary>
        public static TraceResult Trace(Combatant self, Vector3 origin, Vector3 dir, float range, bool includeAllies)
        {
            var result = new TraceResult();
            int n = Physics.RaycastNonAlloc(origin, dir, hitBuffer, range, Layers.Shootable, QueryTriggerInteraction.Collide);
            System.Array.Sort(hitBuffer, 0, n, comparer);
            for (int i = 0; i < n; i++)
            {
                var h = hitBuffer[i];
                var col = h.collider;
                var cb = col.GetComponent<Combatant>();
                if (cb != null)
                {
                    if (cb == self || !cb.alive) continue;
                    if (cb.team == self.team && !includeAllies) continue;
                    Fill(ref result, h);
                    result.combatant = cb;
                    return result;
                }
                var bar = col.GetComponent<Barrier>();
                if (bar != null)
                {
                    if (bar.team == self.team) continue;
                    Fill(ref result, h);
                    result.barrier = bar;
                    return result;
                }
                if (col.isTrigger && col.GetComponent<ShotBlocker>() == null) continue;
                Fill(ref result, h);
                return result;
            }
            result.point = origin + dir * range;
            result.distance = range;
            return result;
        }

        static void Fill(ref TraceResult r, RaycastHit h)
        {
            r.hit = true;
            r.point = h.point;
            r.normal = h.normal;
            r.distance = h.distance;
        }

        public static bool LineOfSight(Vector3 a, Vector3 b)
        {
            return !Physics.Linecast(a, b, Layers.World, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Random direction inside a cone of the given half-angle (degrees).</summary>
        public static Vector3 Spread(Vector3 dir, float degrees)
        {
            if (degrees <= 0f) return dir;
            Vector2 r = Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
            Quaternion q = Quaternion.LookRotation(dir);
            return (q * new Vector3(r.x, r.y, 1f)).normalized;
        }

        public static bool IsHeadshot(Combatant target, Vector3 point)
        {
            return point.y >= target.HeadPos.y - 0.2f;
        }

        public static float Falloff(float distance, float start, float end, float minMult = 0.5f)
        {
            if (distance <= start) return 1f;
            if (distance >= end) return minMult;
            return Mathf.Lerp(1f, minMult, (distance - start) / (end - start));
        }

        /// <summary>Radial damage with linear falloff, line-of-sight check and knockback.</summary>
        public static void Explode(Combatant owner, Vector3 center, float radius, float damage, float knockback,
            string source, bool damageSelf = true, float minFalloff = 0.3f)
        {
            Team team = owner != null ? owner.team : Team.Attack;
            for (int i = Combatant.All.Count - 1; i >= 0; i--)
            {
                var c = Combatant.All[i];
                if (!c.alive) continue;
                bool isSelf = c == owner;
                if (!isSelf && owner != null && c.team == team) continue;
                if (isSelf && !damageSelf && knockback <= 0f) continue;
                Vector3 closest = ClosestPointOnBody(c, center);
                float d = Vector3.Distance(center, closest);
                if (d > radius) continue;
                if (!LineOfSight(center + (c.ChestPos - center).normalized * 0.1f, c.ChestPos) &&
                    !LineOfSight(center, c.HeadPos)) continue;
                float mult = Mathf.Lerp(1f, minFalloff, d / radius);
                if (!isSelf || damageSelf) c.ApplyDamage(owner, damage * mult, false, source);
                if (knockback > 0f)
                {
                    Vector3 dir = (c.ChestPos - center);
                    dir.y = Mathf.Max(dir.y, 0.3f);
                    dir.Normalize();
                    if (c.alive) c.Knockback(dir * knockback * Mathf.Lerp(1f, 0.5f, d / radius));
                }
            }
            foreach (var b in Barrier.All)
            {
                if (!b.Active || b.team == team) continue;
                if (Vector3.Distance(b.transform.position, center) < radius + 1f)
                    b.Damage(damage * 0.5f, owner);
            }
        }

        public static Vector3 ClosestPointOnBody(Combatant c, Vector3 p)
        {
            Vector3 a = c.Feet + Vector3.up * c.def.radius;
            Vector3 b = c.Feet + Vector3.up * (c.def.height - c.def.radius);
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            Vector3 onAxis = a + ab * t;
            Vector3 toP = p - onAxis;
            if (toP.magnitude <= c.def.radius) return p;
            return onAxis + toP.normalized * c.def.radius;
        }

        public static List<Combatant> EnemiesInRadius(Combatant owner, Vector3 center, float radius, bool requireLos)
        {
            var list = new List<Combatant>();
            foreach (var c in Combatant.All)
            {
                if (!c.alive || c.team == owner.team) continue;
                if (Vector3.Distance(center, c.ChestPos) > radius) continue;
                if (requireLos && !LineOfSight(center, c.ChestPos)) continue;
                list.Add(c);
            }
            return list;
        }

        public static List<Combatant> AlliesInRadius(Combatant owner, Vector3 center, float radius, bool includeSelf)
        {
            var list = new List<Combatant>();
            foreach (var c in Combatant.All)
            {
                if (!c.alive || c.team != owner.team) continue;
                if (!includeSelf && c == owner) continue;
                if (Vector3.Distance(center, c.ChestPos) > radius) continue;
                list.Add(c);
            }
            return list;
        }

        /// <summary>Ground point under the crosshair (or straight down from the max range point).</summary>
        public static Vector3 AimGroundPoint(Combatant self, float maxRange)
        {
            RaycastHit h;
            Vector3 p;
            if (Physics.Raycast(self.EyePos, self.AimDir, out h, maxRange, Layers.World, QueryTriggerInteraction.Ignore))
                p = h.point + h.normal * 0.2f;
            else
                p = self.EyePos + self.AimDir * maxRange;
            if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out h, 40f, Layers.World, QueryTriggerInteraction.Ignore))
                return h.point;
            return new Vector3(p.x, 0f, p.z);
        }
    }
}
