using UnityEngine;

namespace NexusStrike
{
    /// <summary>Weapon for training range robots: a weak, inaccurate blaster used only by the duel-pit sentinels.</summary>
    public class TrainingBotKit : HeroKit
    {
        protected override void Setup()
        {
            primaryName = "Training Blaster";
            maxAmmo = 0;
            ultName = "None";
            crosshair = CrosshairStyle.Dot;
        }

        protected override void BuildWeapon(Transform root)
        {
            ModelUtil.Part(PrimitiveType.Cube, root, new Vector3(0, 0, 0.15f), new Vector3(0.1f, 0.12f, 0.32f), new Color(0.2f, 0.22f, 0.26f));
            ModelUtil.Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.01f, 0.36f), new Vector3(0.05f, 0.08f, 0.05f), new Color(0.95f, 0.76f, 0.3f), false, new Vector3(90, 0, 0));
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root, false);
            muzzle.localPosition = new Vector3(0, 0.01f, 0.45f);
        }

        protected override void HandleFire(HeroInput input, float dt)
        {
            if (input.primary && CanFire)
            {
                nextFire = Time.time + 0.5f;
                FireHitscan(7f, 2.5f, 60f, 20f, 40f, false, primaryName, new Color(1f, 0.6f, 0.3f));
                Sfx.PlayFor(self, "rifle", 0.35f, 1.4f);
            }
        }
    }
}
