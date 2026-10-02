using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NexusStrike
{
    /// <summary>
    /// Procedural primitive-built character body with simple walk animation and death topple.
    /// Each hero gets a distinct silhouette; a team-colored visor/stripes identify friend vs foe.
    /// </summary>
    public class HeroModel : MonoBehaviour
    {
        Combatant self;
        Transform body, legL, legR, armL, torso;
        readonly List<Renderer> renderers = new List<Renderer>();
        float walkPhase;
        bool firstPerson;
        float deathAnim;

        public static HeroModel Build(Combatant c)
        {
            var root = new GameObject("Model").transform;
            root.SetParent(c.transform, false);
            var m = root.gameObject.AddComponent<HeroModel>();
            m.self = c;
            m.body = new GameObject("Body").transform;
            m.body.SetParent(root, false);
            m.Construct();
            m.GetComponentsInChildren(true, m.renderers);
            return m;
        }

        void Construct()
        {
            var d = self.def;
            float h = d.height;
            float s = h / 1.8f;              // size scale
            float w = d.radius / 0.4f;       // width scale
            Color main = d.color;
            Color dark = Color.Lerp(main, Color.black, 0.55f);
            Color team = TeamColors.Relative(self.team);
            Color skin = new Color(0.85f, 0.7f, 0.58f);

            // legs (pivot at hip)
            legL = Pivot("LegL", new Vector3(-0.15f * w, 0.85f * s, 0));
            legR = Pivot("LegR", new Vector3(0.15f * w, 0.85f * s, 0));
            ModelUtil.Part(PrimitiveType.Capsule, legL, new Vector3(0, -0.42f * s, 0), new Vector3(0.22f * w, 0.45f * s, 0.24f * w), dark);
            ModelUtil.Part(PrimitiveType.Capsule, legR, new Vector3(0, -0.42f * s, 0), new Vector3(0.22f * w, 0.45f * s, 0.24f * w), dark);
            ModelUtil.Part(PrimitiveType.Cube, legL, new Vector3(0, -0.8f * s, 0.06f), new Vector3(0.22f * w, 0.1f, 0.34f * w), Color.Lerp(dark, Color.black, 0.4f));
            ModelUtil.Part(PrimitiveType.Cube, legR, new Vector3(0, -0.8f * s, 0.06f), new Vector3(0.22f * w, 0.1f, 0.34f * w), Color.Lerp(dark, Color.black, 0.4f));

            // torso
            torso = Pivot("Torso", new Vector3(0, 0.85f * s, 0));
            ModelUtil.Part(PrimitiveType.Cube, torso, new Vector3(0, 0.32f * s, 0), new Vector3(0.52f * w, 0.62f * s, 0.32f * w), main);
            ModelUtil.Part(PrimitiveType.Cube, torso, new Vector3(0, 0.05f * s, 0), new Vector3(0.48f * w, 0.14f * s, 0.3f * w), dark);
            // team stripe across chest
            ModelUtil.Part(PrimitiveType.Cube, torso, new Vector3(0, 0.42f * s, 0.165f * w), new Vector3(0.46f * w, 0.06f, 0.02f), team, true);

            // head
            var head = Pivot("Head", new Vector3(0, 0.82f * s, 0), torso);
            ModelUtil.Part(PrimitiveType.Sphere, head, new Vector3(0, 0.08f, 0), new Vector3(0.28f, 0.3f, 0.28f), skin);
            ModelUtil.Part(PrimitiveType.Cube, head, new Vector3(0, 0.1f, 0.12f), new Vector3(0.24f, 0.07f, 0.06f), team, true);

            // arms
            armL = Pivot("ArmL", new Vector3(-0.33f * w, 0.58f * s, 0), torso);
            ModelUtil.Part(PrimitiveType.Capsule, armL, new Vector3(0, -0.22f * s, 0.05f), new Vector3(0.15f * w, 0.3f * s, 0.15f * w), dark);

            switch (d.id)
            {
                case "ironclad":
                    ModelUtil.Part(PrimitiveType.Cube, torso, new Vector3(-0.38f * w, 0.6f * s, 0), new Vector3(0.36f, 0.24f, 0.46f), main);
                    ModelUtil.Part(PrimitiveType.Cube, torso, new Vector3(0.38f * w, 0.6f * s, 0), new Vector3(0.36f, 0.24f, 0.46f), main);
                    ModelUtil.Part(PrimitiveType.Cube, torso, new Vector3(0, 0.38f * s, -0.26f * w), new Vector3(0.5f, 0.55f, 0.22f), dark);
                    ModelUtil.Part(PrimitiveType.Cylinder, torso, new Vector3(-0.15f, 0.55f * s, -0.36f * w), new Vector3(0.12f, 0.12f, 0.12f), team, true);
                    ModelUtil.Part(PrimitiveType.Cylinder, torso, new Vector3(0.15f, 0.55f * s, -0.36f * w), new Vector3(0.12f, 0.12f, 0.12f), team, true);
                    ModelUtil.Part(PrimitiveType.Cube, head, new Vector3(0, 0.08f, 0), new Vector3(0.34f, 0.32f, 0.34f), main);
                    ModelUtil.Part(PrimitiveType.Cube, head, new Vector3(0, 0.1f, 0.17f), new Vector3(0.26f, 0.06f, 0.02f), team, true);
                    break;
                case "titan":
                    ModelUtil.Part(PrimitiveType.Sphere, torso, new Vector3(-0.4f * w, 0.6f * s, 0), new Vector3(0.42f, 0.36f, 0.42f), main);
                    ModelUtil.Part(PrimitiveType.Sphere, torso, new Vector3(0.4f * w, 0.6f * s, 0), new Vector3(0.42f, 0.36f, 0.42f), main);
                    ModelUtil.Part(PrimitiveType.Sphere, torso, new Vector3(0, 0.38f * s, -0.22f), new Vector3(0.4f, 0.4f, 0.3f), team, true);
                    ModelUtil.Part(PrimitiveType.Sphere, armL, new Vector3(0, -0.5f * s, 0.05f), new Vector3(0.32f, 0.32f, 0.32f), dark);
                    break;
                case "vex":
                    ModelUtil.Part(PrimitiveType.Cube, torso, new Vector3(0, 0.5f * s, -0.2f), new Vector3(0.32f, 0.28f, 0.12f), dark);
                    ModelUtil.Part(PrimitiveType.Cube, head, new Vector3(0, 0.2f, -0.05f), new Vector3(0.3f, 0.08f, 0.32f), main);
                    ModelUtil.Part(PrimitiveType.Cube, torso, new Vector3(-0.1f, 0.15f * s, -0.18f), new Vector3(0.05f, 0.5f, 0.02f), team, true);
                    break;
                case "kestrel":
                    ModelUtil.Part(PrimitiveType.Cylinder, torso, new Vector3(-0.14f, 0.38f * s, -0.24f), new Vector3(0.14f, 0.24f, 0.14f), dark);
                    ModelUtil.Part(PrimitiveType.Cylinder, torso, new Vector3(0.14f, 0.38f * s, -0.24f), new Vector3(0.14f, 0.24f, 0.14f), dark);
                    ModelUtil.Part(PrimitiveType.Cube, torso, new Vector3(-0.4f, 0.5f * s, -0.24f), new Vector3(0.38f, 0.04f, 0.18f), main, false, new Vector3(0, 0, 15));
                    ModelUtil.Part(PrimitiveType.Cube, torso, new Vector3(0.4f, 0.5f * s, -0.24f), new Vector3(0.38f, 0.04f, 0.18f), main, false, new Vector3(0, 0, -15));
                    ModelUtil.Part(PrimitiveType.Sphere, torso, new Vector3(-0.14f, 0.12f * s, -0.24f), new Vector3(0.1f, 0.1f, 0.1f), new Color(1f, 0.6f, 0.2f), true);
                    ModelUtil.Part(PrimitiveType.Sphere, torso, new Vector3(0.14f, 0.12f * s, -0.24f), new Vector3(0.1f, 0.1f, 0.1f), new Color(1f, 0.6f, 0.2f), true);
                    ModelUtil.Part(PrimitiveType.Cube, head, new Vector3(0, 0.1f, 0), new Vector3(0.3f, 0.28f, 0.3f), main);
                    ModelUtil.Part(PrimitiveType.Cube, head, new Vector3(0, 0.1f, 0.155f), new Vector3(0.24f, 0.08f, 0.02f), team, true);
                    break;
                case "rook":
                    ModelUtil.Part(PrimitiveType.Cube, torso, new Vector3(0, 0.35f * s, -0.24f), new Vector3(0.44f, 0.5f, 0.18f), new Color(0.35f, 0.3f, 0.28f));
                    ModelUtil.Part(PrimitiveType.Cylinder, torso, new Vector3(0.12f, 0.8f * s, -0.26f), new Vector3(0.04f, 0.2f, 0.04f), dark);
                    ModelUtil.Part(PrimitiveType.Sphere, torso, new Vector3(0.12f, 1.0f * s, -0.26f), new Vector3(0.08f, 0.08f, 0.08f), new Color(0.5f, 0.8f, 1f), true);
                    ModelUtil.Part(PrimitiveType.Cube, head, new Vector3(0, 0.12f, 0.1f), new Vector3(0.3f, 0.1f, 0.12f), new Color(0.2f, 0.2f, 0.22f));
                    break;
                case "lumen":
                    var halo = Pivot("Halo", new Vector3(0, 0.38f, 0), head);
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i * Mathf.PI * 2f / 10f;
                        ModelUtil.Part(PrimitiveType.Cube, halo, new Vector3(Mathf.Cos(a) * 0.2f, 0, Mathf.Sin(a) * 0.2f), new Vector3(0.09f, 0.025f, 0.03f),
                            new Color(1f, 0.92f, 0.55f), true, new Vector3(0, -a * Mathf.Rad2Deg + 90f, 0));
                    }
                    ModelUtil.Part(PrimitiveType.Cube, torso, new Vector3(-0.32f, 0.45f * s, -0.22f), new Vector3(0.5f, 0.7f, 0.03f), new Color(1f, 0.95f, 0.8f, 0.8f), true, new Vector3(0, 20, 25));
                    ModelUtil.Part(PrimitiveType.Cube, torso, new Vector3(0.32f, 0.45f * s, -0.22f), new Vector3(0.5f, 0.7f, 0.03f), new Color(1f, 0.95f, 0.8f, 0.8f), true, new Vector3(0, -20, -25));
                    break;
                case "cypress":
                    ModelUtil.Part(PrimitiveType.Cylinder, head, new Vector3(0, 0.2f, -0.02f), new Vector3(0.38f, 0.04f, 0.38f), new Color(0.45f, 0.35f, 0.2f));
                    ModelUtil.Part(PrimitiveType.Cylinder, head, new Vector3(0, 0.28f, -0.02f), new Vector3(0.22f, 0.07f, 0.22f), new Color(0.45f, 0.35f, 0.2f));
                    ModelUtil.Part(PrimitiveType.Cube, torso, new Vector3(0, 0.0f, 0), new Vector3(0.56f, 0.4f, 0.36f), dark);
                    ModelUtil.Part(PrimitiveType.Sphere, torso, new Vector3(0.18f, 0.55f * s, -0.22f), new Vector3(0.18f, 0.18f, 0.18f), new Color(0.5f, 1f, 0.5f), true);
                    break;
            }
        }

        Transform Pivot(string name, Vector3 pos, Transform parent = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent != null ? parent : body, false);
            t.localPosition = pos;
            return t;
        }

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
            body.localRotation = Quaternion.identity;
            body.localPosition = Vector3.zero;
            gameObject.SetActive(true);
            SetFirstPerson(firstPerson);
        }

        void Update()
        {
            if (deathAnim > 0f)
            {
                deathAnim += Time.deltaTime;
                float t = Mathf.Clamp01(deathAnim / 0.5f);
                body.localRotation = Quaternion.Euler(-85f * t * t, 0, 0);
                body.localPosition = new Vector3(0, -0.05f * t, -0.3f * t);
                if (deathAnim > 3f) gameObject.SetActive(false);
                return;
            }
            if (self == null || self.motor == null) return;
            float speed = self.motor.HorizontalVelocity.magnitude;
            bool grounded = self.motor.grounded;
            walkPhase += Time.deltaTime * speed * 2.2f;
            float swing = grounded ? Mathf.Sin(walkPhase) * Mathf.Clamp01(speed / 5f) * 35f : 18f;
            legL.localRotation = Quaternion.Euler(swing, 0, 0);
            legR.localRotation = Quaternion.Euler(grounded ? -swing : -10f, 0, 0);
            armL.localRotation = Quaternion.Euler(-swing * 0.6f - 20f, 0, 0);
            torso.localRotation = Quaternion.Euler(self.pitch * 0.15f, 0, 0);
            float stunWobble = self.IsStunned ? Mathf.Sin(Time.time * 18f) * 6f : 0f;
            body.localRotation = Quaternion.Euler(0, 0, stunWobble);
        }
    }
}
