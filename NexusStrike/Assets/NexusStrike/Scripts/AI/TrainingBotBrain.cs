using UnityEngine;

namespace NexusStrike
{
    public enum TrainingBotMode { Static, Strafe, Sentinel, Ally }

    /// <summary>
    /// Scripted behaviour for training range robots: stand still, strafe along a rail,
    /// fight back inside the duel pit, or act as a hurt ally for healing practice.
    /// </summary>
    public class TrainingBotBrain : Brain
    {
        public TrainingBotMode mode;
        public Vector3 anchor;
        public Vector3 pointB;
        public float homeYaw;
        public float speedScale = 1f;
        public bool jumpy;
        public Vector3 aggroCenter;
        public float aggroRadius = 18f;

        bool towardB = true;
        float nextJump;
        float nextDodge;
        Vector3 dodgeTarget;
        float nextPulse;
        Vector3 aimJitter;
        float nextJitter;

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        public override HeroInput Think(float dt)
        {
            var input = default(HeroInput);
            var gm = GameManager.I;
            if (gm == null || !gm.CanAct(self)) return input;
            var player = gm.player;
            bool playerOk = player != null && player.alive;

            Vector3 look = self.EyePos + Quaternion.Euler(0f, homeYaw, 0f) * Vector3.forward * 5f;
            float turn = 360f;
            Vector3 moveTo = self.Feet;

            switch (mode)
            {
                case TrainingBotMode.Static:
                    if (playerOk && Vector3.Distance(player.Feet, self.Feet) < 90f) look = player.ChestPos;
                    moveTo = anchor;
                    break;

                case TrainingBotMode.Strafe:
                {
                    Vector3 target = towardB ? pointB : anchor;
                    if (Flat(target - self.Feet).magnitude < 0.6f) towardB = !towardB;
                    moveTo = target;
                    if (jumpy && self.motor.grounded && Time.time > nextJump)
                    {
                        nextJump = Time.time + Random.Range(1.2f, 2.4f);
                        input.jump = true;
                    }
                    if (playerOk) look = player.ChestPos;
                    break;
                }

                case TrainingBotMode.Sentinel:
                {
                    bool engaged = playerOk && Vector3.Distance(player.Feet, aggroCenter) < aggroRadius &&
                                   CombatUtil.LineOfSight(self.EyePos, player.ChestPos);
                    if (engaged)
                    {
                        if (Time.time > nextJitter)
                        {
                            nextJitter = Time.time + 0.4f;
                            aimJitter = Random.insideUnitSphere * 0.6f;
                        }
                        look = player.ChestPos + aimJitter;
                        turn = 200f;
                        if (Time.time > nextDodge)
                        {
                            nextDodge = Time.time + Random.Range(0.8f, 1.6f);
                            Vector2 r = Random.insideUnitCircle * 4f;
                            dodgeTarget = anchor + new Vector3(r.x, 0f, r.y);
                        }
                        moveTo = dodgeTarget;
                        if (Vector3.Angle(self.AimDir, look - self.EyePos) < 6f) input.primary = true;
                    }
                    else moveTo = anchor;
                    break;
                }

                case TrainingBotMode.Ally:
                    moveTo = anchor;
                    if (Time.time > nextPulse)
                    {
                        nextPulse = Time.time + 3.5f;
                        if (self.Total > 90f) self.ApplyDamage(null, 45f, false, "Training");
                    }
                    if (playerOk && Vector3.Distance(player.Feet, self.Feet) < 25f) look = player.ChestPos;
                    break;
            }

            // movement toward moveTo (relative to current yaw)
            Vector3 to = Flat(moveTo - self.Feet);
            if (to.magnitude > 0.4f)
            {
                Vector3 wish = to.normalized * Mathf.Clamp01(speedScale);
                Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, self.yaw, 0f)) * wish;
                input.move = new Vector2(local.x, local.z);
            }

            // aim
            Vector3 d = look - self.EyePos;
            if (d.sqrMagnitude > 0.01f)
            {
                float wantYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                float wantPitch = -Mathf.Atan2(d.y, Flat(d).magnitude) * Mathf.Rad2Deg;
                self.yaw = Mathf.MoveTowardsAngle(self.yaw, wantYaw, turn * dt);
                self.pitch = Mathf.MoveTowards(self.pitch, Mathf.Clamp(wantPitch, -60f, 60f), turn * dt);
            }
            return input;
        }
    }
}
