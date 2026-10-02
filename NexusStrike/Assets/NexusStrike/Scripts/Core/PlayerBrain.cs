using UnityEngine;

namespace NexusStrike
{
    /// <summary>Local player input + first-person camera (and death / kill cam).</summary>
    public class PlayerBrain : Brain
    {
        public static float sensitivity = 2f;
        public const float BaseFov = 75f;
        Camera cam;
        float currentFov = BaseFov;
        float shake;

        public void Bind(Camera c)
        {
            cam = c;
            self.visuals.SetFirstPerson(true);
        }

        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }

        public override HeroInput Think(float dt)
        {
            var input = default(HeroInput);
            var gm = GameManager.I;
            if (gm == null || gm.InputBlocked) return input;

            Vector2 md = GameInput.MouseDelta;
            float zoomScale = self.kit.zoomFov > 0f ? self.kit.zoomFov / BaseFov : 1f;
            self.yaw += md.x * sensitivity * zoomScale;
            self.pitch = Mathf.Clamp(self.pitch - md.y * sensitivity * zoomScale, -89f, 89f);

            float x = (GameInput.Key(GKey.D) ? 1f : 0f) - (GameInput.Key(GKey.A) ? 1f : 0f);
            float y = (GameInput.Key(GKey.W) ? 1f : 0f) - (GameInput.Key(GKey.S) ? 1f : 0f);
            input.move = new Vector2(x, y);
            input.jump = GameInput.KeyDown(GKey.Space);
            input.jumpHeld = GameInput.Key(GKey.Space);
            input.primary = GameInput.MouseHeld(0);
            input.secondary = GameInput.MouseHeld(1);
            input.ability1 = GameInput.KeyDown(GKey.LeftShift);
            input.ability2 = GameInput.KeyDown(GKey.E);
            input.ultimate = GameInput.KeyDown(GKey.Q);
            input.reload = GameInput.KeyDown(GKey.R);
            return input;
        }

        void LateUpdate()
        {
            if (cam == null || self == null) return;
            float dt = Time.deltaTime;
            if (self.alive)
            {
                float targetFov = self.kit.zoomFov > 0f ? self.kit.zoomFov : BaseFov;
                currentFov = Mathf.Lerp(currentFov, targetFov, dt * 14f);
                cam.fieldOfView = currentFov;
                Vector3 offset = Vector3.zero;
                if (shake > 0f)
                {
                    offset = Random.insideUnitSphere * shake * 0.06f;
                    shake = Mathf.MoveTowards(shake, 0f, dt * 4f);
                }
                cam.transform.SetPositionAndRotation(self.eye.position + offset, self.eye.rotation);
            }
            else
            {
                // death cam: orbit above the body, looking at the killer when known
                float t = Time.time - self.deathTime;
                Vector3 focus = self.ChestPos;
                Vector3 look = self.lastKiller != null && self.lastKiller.alive ? self.lastKiller.ChestPos : focus;
                Vector3 dir = Quaternion.Euler(0, self.yaw + t * 12f, 0) * new Vector3(0, 0, -1);
                Vector3 desired = focus + dir * 5f + Vector3.up * 3f;
                RaycastHit h;
                if (Physics.Linecast(focus, desired, out h, Layers.World, QueryTriggerInteraction.Ignore))
                    desired = h.point + (focus - desired).normalized * 0.3f;
                cam.transform.position = Vector3.Lerp(cam.transform.position, desired, dt * 3f);
                Quaternion want = Quaternion.LookRotation((look - cam.transform.position).normalized);
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, want, dt * 3f);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, BaseFov, dt * 5f);
            }
        }
    }
}
