using System.Collections.Generic;
using UnityEngine;

namespace NexusStrike
{
    /// <summary>
    /// Renders live 3D portraits of every hero into RenderTextures for the hero select screen.
    /// Each hero stands on its own hidden "stage" (a dedicated layer far below the map) with its own camera and key light.
    /// </summary>
    public class PortraitStudio : MonoBehaviour
    {
        public const int Layer = 9;
        const int TexW = 384, TexH = 512;

        class Stage
        {
            public Transform pivot;
            public Camera cam;
            public RenderTexture rt;
            public Team builtFor;
            public HeroModel model;
        }

        readonly Dictionary<string, Stage> stages = new Dictionary<string, Stage>();
        bool active;

        public static PortraitStudio Create(Transform parent)
        {
            var go = new GameObject("PortraitStudio");
            go.transform.SetParent(parent, false);
            return go.AddComponent<PortraitStudio>();
        }

        public Texture Get(string heroId)
        {
            Stage s;
            return stages.TryGetValue(heroId, out s) ? s.rt : null;
        }

        /// <summary>Turn rendering on only while a hero select screen is visible.</summary>
        public void SetActive(bool on)
        {
            if (on && stages.Count == 0) Build();
            if (on) RefreshTeamColors();
            if (active == on) return;
            active = on;
            foreach (var s in stages.Values) s.cam.enabled = on;
        }

        void Build()
        {
            var heroes = HeroRoster.All;
            for (int i = 0; i < heroes.Count; i++)
            {
                var def = heroes[i];
                var stage = new Stage();
                var root = new GameObject("Stage_" + def.id).transform;
                root.SetParent(transform, false);
                root.position = new Vector3(i * 25f, -500f, 0f);

                stage.pivot = new GameObject("Pivot").transform;
                stage.pivot.SetParent(root, false);

                float scale = def.height / 1.8f;
                var floor = ModelUtil.Part(PrimitiveType.Cylinder, root, new Vector3(0, -0.02f, 0), new Vector3(1.6f, 0.02f, 1.6f), new Color(0.12f, 0.15f, 0.22f));
                floor.gameObject.layer = Layer;
                var rim = ModelUtil.Part(PrimitiveType.Cylinder, root, new Vector3(0, -0.01f, 0), new Vector3(1.7f, 0.01f, 1.7f), new Color(1f, 0.8f, 0.35f, 0.5f), true);
                rim.gameObject.layer = Layer;

                var camGo = new GameObject("PortraitCam");
                camGo.transform.SetParent(root, false);
                camGo.transform.localPosition = new Vector3(0.35f, 1.35f * scale, 3.6f * scale);
                camGo.transform.LookAt(root.position + Vector3.up * 0.98f * scale);
                stage.cam = camGo.AddComponent<Camera>();
                stage.cam.cullingMask = 1 << Layer;
                stage.cam.clearFlags = CameraClearFlags.SolidColor;
                stage.cam.backgroundColor = new Color(0.07f, 0.09f, 0.14f, 1f);
                stage.cam.fieldOfView = 30f;
                stage.cam.nearClipPlane = 0.1f;
                stage.cam.farClipPlane = 20f;
                stage.rt = new RenderTexture(TexW, TexH, 16);
                stage.rt.antiAliasing = 2;
                stage.cam.targetTexture = stage.rt;
                stage.cam.enabled = false;

                AddLight(root, new Vector3(-1.6f, 2.6f, 2.4f), new Color(1f, 0.95f, 0.85f), 2.2f);
                AddLight(root, new Vector3(1.8f, 1.6f, -1.8f), TeamColors.Ally, 1.6f);
                AddLight(root, new Vector3(0.5f, 0.6f, 2.2f), new Color(0.6f, 0.7f, 0.9f), 0.7f);

                stages[def.id] = stage;
                BuildModel(stage, def);
            }
            active = false;
        }

        static void AddLight(Transform parent, Vector3 pos, Color color, float intensity)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = 7f;
            l.cullingMask = 1 << Layer;
            l.shadows = LightShadows.None;
        }

        void BuildModel(Stage stage, HeroDefinition def)
        {
            if (stage.model != null) Destroy(stage.model.gameObject);
            stage.model = HeroModel.BuildPreview(stage.pivot, def, GameManager.I != null ? GameManager.I.playerTeam : Team.Attack, Layer);
            stage.builtFor = GameManager.I != null ? GameManager.I.playerTeam : Team.Attack;
        }

        void RefreshTeamColors()
        {
            Team t = GameManager.I != null ? GameManager.I.playerTeam : Team.Attack;
            foreach (var def in HeroRoster.All)
            {
                Stage s;
                if (stages.TryGetValue(def.id, out s) && s.builtFor != t) BuildModel(s, def);
            }
        }

        void Update()
        {
            if (!active) return;
            float t = Time.unscaledTime;
            int i = 0;
            foreach (var s in stages.Values)
            {
                s.pivot.localRotation = Quaternion.Euler(0f, 22f + Mathf.Sin(t * 0.5f + i) * 28f, 0f);
                i++;
            }
        }

        void OnDestroy()
        {
            foreach (var s in stages.Values)
                if (s.rt != null) s.rt.Release();
        }
    }
}
