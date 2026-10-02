using UnityEngine;

namespace NexusStrike
{
    /// <summary>CharacterController-based movement: walking, jumping, air control, knockback and dashes.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class HeroMotor : MonoBehaviour
    {
        public CharacterController cc;
        public Combatant self;
        public float baseSpeed = 5.5f;
        public float jumpSpeed = 6.2f;
        public float gravity = 19f;
        public float gravityScale = 1f;
        public float speedMultiplier = 1f;   // kit-controlled (e.g. holding a barrier)
        public bool flying;                  // kit-controlled (gravity off, vertical velocity damped)

        public Vector3 velocity;
        public bool grounded;
        public float lastGroundedTime;
        public System.Action onLanded;

        float dashUntil;
        Vector3 dashVelocity;

        public Vector3 HorizontalVelocity { get { return new Vector3(velocity.x, 0f, velocity.z); } }
        public bool Dashing { get { return Time.time < dashUntil; } }

        public void Init(Combatant c)
        {
            self = c;
            cc = GetComponent<CharacterController>();
            baseSpeed = c.def.speed;
        }

        public void Tick(HeroInput input, float dt, bool canMove)
        {
            if (!cc.enabled) return;
            bool wasGrounded = grounded;

            if (Dashing)
            {
                cc.Move(dashVelocity * dt);
                velocity = dashVelocity;
                grounded = cc.isGrounded;
                if (Time.time + dt >= dashUntil) velocity = dashVelocity * 0.25f;
                return;
            }

            Quaternion yawRot = Quaternion.Euler(0f, self.yaw, 0f);
            Vector3 wish = Vector3.zero;
            if (canMove)
            {
                wish = yawRot * new Vector3(input.move.x, 0f, input.move.y);
                if (wish.sqrMagnitude > 1f) wish.Normalize();
                // backpedal / strafe slightly slower like most hero shooters
                if (input.move.y < 0f) wish *= 0.9f;
            }
            float speed = baseSpeed * speedMultiplier * self.SpeedFactor;
            wish *= speed;

            Vector3 horiz = HorizontalVelocity;
            if (grounded)
            {
                float accel = horiz.magnitude > speed + 0.5f ? 22f : 70f;
                horiz = Vector3.MoveTowards(horiz, wish, accel * dt);
            }
            else if (wish.sqrMagnitude > 0.01f)
            {
                // air control: steer toward wish without killing existing momentum
                Vector3 target = wish;
                if (horiz.magnitude > speed) target = wish.normalized * horiz.magnitude;
                horiz = Vector3.MoveTowards(horiz, target, 14f * dt);
            }

            float vy = velocity.y;
            if (grounded && vy < 0f) vy = -2f;
            if (canMove && input.jump && grounded)
            {
                vy = jumpSpeed;
                grounded = false;
            }

            if (flying)
            {
                vy = Mathf.MoveTowards(vy, 0f, 25f * dt);
            }
            else
            {
                vy -= gravity * gravityScale * dt;
            }
            vy = Mathf.Max(vy, -45f);

            velocity = new Vector3(horiz.x, vy, horiz.z);
            CollisionFlags flags = cc.Move(velocity * dt);
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;

            grounded = cc.isGrounded;
            if (grounded) lastGroundedTime = Time.time;
            if (grounded && !wasGrounded && onLanded != null) onLanded();
        }

        public void AddImpulse(Vector3 impulse)
        {
            velocity += impulse;
            if (impulse.y > 0.5f)
            {
                velocity.y = Mathf.Max(velocity.y, impulse.y);
                grounded = false;
            }
        }

        public void Dash(Vector3 direction, float distance, float duration)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) direction = self.Forward;
            direction.Normalize();
            dashVelocity = direction * (distance / duration);
            dashUntil = Time.time + duration;
        }

        public void CancelDash() { dashUntil = 0f; }

        public void Teleport(Vector3 pos)
        {
            cc.enabled = false;
            transform.position = pos;
            velocity = Vector3.zero;
            dashUntil = 0f;
            cc.enabled = true;
        }

        public void OnDeath()
        {
            velocity = Vector3.zero;
            dashUntil = 0f;
            flying = false;
            gravityScale = 1f;
            speedMultiplier = 1f;
            cc.enabled = false;
        }
    }

    /// <summary>Source of intent for a combatant: the local player or an AI.</summary>
    public abstract class Brain : MonoBehaviour
    {
        public Combatant self;
        public abstract HeroInput Think(float dt);
    }
}
