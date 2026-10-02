using System.Collections.Generic;
using UnityEngine;

namespace NexusStrike
{
    /// <summary>
    /// Hard-light barrier: a trigger box that blocks enemy shots and projectiles (but not movement).
    /// Friendly fire passes through it.
    /// </summary>
    public class Barrier : MonoBehaviour
    {
        public static readonly List<Barrier> All = new List<Barrier>();

        public Team team;
        public Combatant owner;
        public float maxHp = 1000f;
        public float hp = 1000f;
        public System.Action onBroken;

        BoxCollider box;
        Material mat;
        Color baseColor;
        float flashUntil;
        bool active;

        public bool Active { get { return active; } }
        public float Frac { get { return hp / maxHp; } }

        public static Barrier Create(Combatant owner, Vector3 size, float maxHp)
        {
            var go = new GameObject("Barrier");
            var b = go.AddComponent<Barrier>();
            b.owner = owner;
            b.team = owner.team;
            b.maxHp = maxHp;
            b.hp = maxHp;
            b.box = go.AddComponent<BoxCollider>();
            b.box.isTrigger = true;
            b.box.size = size;

            var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(vis.GetComponent<Collider>());
            vis.transform.SetParent(go.transform, false);
            vis.transform.localScale = new Vector3(size.x, size.y, 0.08f);
            b.baseColor = TeamColors.Relative(owner.team);
            b.baseColor.a = 0.28f;
            b.mat = MaterialLib.NewUnlit(b.baseColor);
            var r = vis.GetComponent<Renderer>();
            r.sharedMaterial = b.mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // hexagonal-ish frame lines for readability
            var frameColor = new Color(b.baseColor.r, b.baseColor.g, b.baseColor.b, 0.8f);
            ModelUtil.Part(PrimitiveType.Cube, go.transform, new Vector3(0, size.y * 0.5f, 0), new Vector3(size.x, 0.06f, 0.1f), frameColor, true);
            ModelUtil.Part(PrimitiveType.Cube, go.transform, new Vector3(0, -size.y * 0.5f, 0), new Vector3(size.x, 0.06f, 0.1f), frameColor, true);
            ModelUtil.Part(PrimitiveType.Cube, go.transform, new Vector3(size.x * 0.5f, 0, 0), new Vector3(0.06f, size.y, 0.1f), frameColor, true);
            ModelUtil.Part(PrimitiveType.Cube, go.transform, new Vector3(-size.x * 0.5f, 0, 0), new Vector3(0.06f, size.y, 0.1f), frameColor, true);
            for (int i = 1; i < 4; i++)
                ModelUtil.Part(PrimitiveType.Cube, go.transform, new Vector3(-size.x * 0.5f + size.x * i / 4f, 0, 0), new Vector3(0.03f, size.y, 0.09f),
                    new Color(frameColor.r, frameColor.g, frameColor.b, 0.35f), true);

            All.Add(b);
            go.SetActive(false);
            return b;
        }

        /// <summary>Invisible damageable hitbox attached to an existing object (deployables like turrets).</summary>
        public static Barrier CreateBare(Combatant owner, GameObject host, Vector3 size, Vector3 center, float maxHp)
        {
            var b = host.AddComponent<Barrier>();
            b.owner = owner;
            b.team = owner.team;
            b.maxHp = maxHp;
            b.hp = maxHp;
            b.box = host.AddComponent<BoxCollider>();
            b.box.isTrigger = true;
            b.box.size = size;
            b.box.center = center;
            b.active = true;
            All.Add(b);
            return b;
        }

        public void SetActive(bool on)
        {
            active = on && hp > 0f;
            gameObject.SetActive(active);
        }

        public void Damage(float amount, Combatant attacker)
        {
            if (!active || amount <= 0f) return;
            float taken = Mathf.Min(hp, amount);
            hp -= taken;
            if (owner != null) owner.damageBlocked += taken;
            if (attacker != null) attacker.AddUltCharge(taken * 0.5f);
            flashUntil = Time.time + 0.06f;
            if (hp <= 0f)
            {
                hp = 0f;
                Fx.Burst(transform.position, baseColor, 14, 6f, 0.2f, 0.6f, 6f);
                Sfx.Play("barrier", transform.position, 1f, 0.6f);
                SetActive(false);
                if (onBroken != null) onBroken();
            }
        }

        void Update()
        {
            if (mat == null) return;
            Color c = baseColor;
            c.a = Time.time < flashUntil ? 0.55f : Mathf.Lerp(0.12f, 0.3f, Frac);
            mat.color = c;
        }

        void OnDestroy()
        {
            All.Remove(this);
            if (mat != null) Destroy(mat);
        }
    }
}
