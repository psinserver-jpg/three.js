using UnityEngine;

namespace NexusStrike
{
    /// <summary>Short-lived visual piece: moves, scales and fades, then destroys itself.</summary>
    public class FxPiece : MonoBehaviour
    {
        public float life = 0.3f;
        public Vector3 velocity;
        public float gravity;
        public float startScale = 1f, endScale = 0f;
        public float startAlpha = 1f;
        public Material ownedMaterial;
        public LineRenderer line;
        float age;
        Color baseColor;

        void Start()
        {
            if (ownedMaterial != null) baseColor = ownedMaterial.color;
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / life);
            velocity.y -= gravity * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            if (line == null)
                transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);
            if (ownedMaterial != null)
            {
                var c = baseColor;
                c.a = startAlpha * (1f - t);
                ownedMaterial.color = c;
                if (line != null) { line.startColor = c; line.endColor = c; }
            }
            if (age >= life) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (ownedMaterial != null) Destroy(ownedMaterial);
        }
    }

    /// <summary>Static helpers for all transient combat visuals (tracers, flashes, explosions, rings).</summary>
    public static class Fx
    {
        static Transform root;

        static Transform Root
        {
            get
            {
                if (root == null) root = new GameObject("FX").transform;
                return root;
            }
        }

        static GameObject Prim(PrimitiveType type, Vector3 pos, Color color, out Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.layer = Layers.IgnoreRaycast;
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            var r = go.GetComponent<Renderer>();
            mat = MaterialLib.NewUnlit(color);
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        public static LineRenderer NewLine(Color color, float width, out Material mat)
        {
            var go = new GameObject("Line");
            go.layer = Layers.IgnoreRaycast;
            go.transform.SetParent(Root, false);
            var lr = go.AddComponent<LineRenderer>();
            mat = MaterialLib.NewUnlit(color);
            lr.sharedMaterial = mat;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = color;
            lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        public static void Tracer(Vector3 from, Vector3 to, Color color, float width = 0.035f, float life = 0.08f)
        {
            Material mat;
            var lr = NewLine(color, width, out mat);
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.endWidth = width * 0.5f;
            var p = lr.gameObject.AddComponent<FxPiece>();
            p.life = life;
            p.ownedMaterial = mat;
            p.line = lr;
        }

        public static void Flash(Vector3 pos, Color color, float size, float life = 0.06f)
        {
            Material mat;
            var go = Prim(PrimitiveType.Sphere, pos, color, out mat);
            var p = go.AddComponent<FxPiece>();
            p.life = life;
            p.startScale = size;
            p.endScale = size * 0.4f;
            p.ownedMaterial = mat;
        }

        public static void Burst(Vector3 pos, Color color, int count, float speed, float size, float life = 0.4f, float gravity = 9f)
        {
            for (int i = 0; i < count; i++)
            {
                Material mat;
                var go = Prim(PrimitiveType.Cube, pos, color, out mat);
                go.transform.rotation = Random.rotation;
                var p = go.AddComponent<FxPiece>();
                p.life = life * Random.Range(0.6f, 1.2f);
                p.velocity = Random.onUnitSphere * speed * Random.Range(0.4f, 1f);
                p.gravity = gravity;
                p.startScale = size;
                p.endScale = 0f;
                p.ownedMaterial = mat;
            }
        }

        public static void Impact(Vector3 pos, Vector3 normal, Color color)
        {
            Flash(pos + normal * 0.05f, color, 0.25f, 0.05f);
            Burst(pos, color, 3, 4f, 0.07f, 0.25f, 12f);
        }

        public static void Explosion(Vector3 pos, float radius, Color color)
        {
            Material mat;
            var go = Prim(PrimitiveType.Sphere, pos, color, out mat);
            var p = go.AddComponent<FxPiece>();
            p.life = 0.35f;
            p.startScale = radius * 0.6f;
            p.endScale = radius * 2.1f;
            p.startAlpha = 0.85f;
            p.ownedMaterial = mat;
            var core = new Color(1f, 0.95f, 0.75f, 1f);
            Flash(pos, core, radius * 0.9f, 0.12f);
            Burst(pos, color, 10, radius * 5f, 0.18f, 0.6f, 10f);
        }

        public static void Ring(Vector3 pos, float radius, Color color, float life = 0.45f)
        {
            Material mat;
            var go = Prim(PrimitiveType.Cylinder, pos + Vector3.up * 0.05f, color, out mat);
            var p = go.AddComponent<FxPiece>();
            p.life = life;
            p.startScale = 0.5f;
            p.endScale = radius * 2f;
            p.startAlpha = 0.6f;
            p.ownedMaterial = mat;
            // flatten: FxPiece scales uniformly, so wrap in a squashed parent
            var holder = new GameObject("Ring").transform;
            holder.SetParent(Root, false);
            holder.position = go.transform.position;
            holder.localScale = new Vector3(1f, 0.02f, 1f);
            go.transform.SetParent(holder, true);
            go.transform.localPosition = Vector3.zero;
            Object.Destroy(holder.gameObject, life + 0.05f);
        }

        public static void Pillar(Vector3 pos, float radius, float height, Color color, float life = 0.6f)
        {
            Material mat;
            var go = Prim(PrimitiveType.Cylinder, pos + Vector3.up * height * 0.5f, color, out mat);
            var holder = new GameObject("Pillar").transform;
            holder.SetParent(Root, false);
            holder.position = pos + Vector3.up * height * 0.5f;
            holder.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.transform.SetParent(holder, false);
            go.transform.localPosition = Vector3.zero;
            var p = go.AddComponent<FxPiece>();
            p.life = life;
            p.startScale = 1f;
            p.endScale = 1.2f;
            p.startAlpha = 0.45f;
            p.ownedMaterial = mat;
            Object.Destroy(holder.gameObject, life + 0.05f);
        }
    }
}
