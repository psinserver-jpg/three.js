using System.Collections.Generic;
using UnityEngine;

namespace NexusStrike
{
    /// <summary>
    /// Runtime material factory. Everything in the game is built from primitives, so instead of
    /// shipping material assets we clone Unity's default lit material and use the always-included
    /// "Sprites/Default" shader for unlit / transparent effects.
    /// </summary>
    public static class MaterialLib
    {
        static Material litBase;
        static Shader unlitShader;
        static readonly Dictionary<Color, Material> litCache = new Dictionary<Color, Material>();
        static readonly Dictionary<Color, Material> unlitCache = new Dictionary<Color, Material>();

        static void Init()
        {
            if (litBase != null) return;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            litBase = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
            Object.DestroyImmediate(tmp);
            unlitShader = Shader.Find("Sprites/Default");
            if (unlitShader == null) unlitShader = Shader.Find("Unlit/Color");
            if (unlitShader == null) unlitShader = litBase.shader;
        }

        /// <summary>Shared opaque lit material (do not modify the result).</summary>
        public static Material Lit(Color c)
        {
            Init();
            Material m;
            if (litCache.TryGetValue(c, out m)) return m;
            m = new Material(litBase);
            m.color = c;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            litCache[c] = m;
            return m;
        }

        /// <summary>Shared unlit (self-illuminated looking) material. Supports alpha.</summary>
        public static Material Unlit(Color c)
        {
            Init();
            Material m;
            if (unlitCache.TryGetValue(c, out m)) return m;
            m = new Material(unlitShader);
            m.color = c;
            unlitCache[c] = m;
            return m;
        }

        /// <summary>A fresh unlit material instance (caller owns it, e.g. for fading FX).</summary>
        public static Material NewUnlit(Color c)
        {
            Init();
            var m = new Material(unlitShader);
            m.color = c;
            return m;
        }
    }

    public static class ModelUtil
    {
        /// <summary>Create a collider-less primitive part for visuals.</summary>
        public static Transform Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale,
            Color color, bool unlit = false, Vector3 euler = default(Vector3))
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            go.layer = parent != null ? parent.gameObject.layer : 0;
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localRotation = Quaternion.Euler(euler);
            t.localScale = localScale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = unlit ? MaterialLib.Unlit(color) : MaterialLib.Lit(color);
            if (unlit) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }
    }
}
