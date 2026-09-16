using UnityEngine;

namespace EarthRecovery
{
    public sealed class PlayerAvatar : MonoBehaviour
    {
        public Animator animator;
        public Transform facing;
        public string CharacterId { get; internal set; } = PlayerCharacterCatalog.DefaultId;
        static readonly int Gait = Animator.StringToHash("Gait");
        Vector3 previousPosition;
        bool sampled;
        float speed;
        public float Speed => speed;

        public void Present(PlayerState player, GameRules rules, bool expedition, float deltaTime)
        {
            var position = transform.parent.position;
            var displacement = position - previousPosition;
            previousPosition = position;
            displacement.y = 0;
            bool active = player.alive && player.connected;
            animator.enabled = active;
            float measured = sampled && expedition && active && deltaTime > .0001f
                ? displacement.magnitude / deltaTime : 0;
            sampled = true;
            // Teleports and spawn corrections must not look like a sprint.
            if (measured > rules.runSpeed * 2.5f) measured = 0;
            speed = Mathf.Lerp(speed, measured, 1 - Mathf.Exp(-12 * Mathf.Max(0, deltaTime)));
            if (!active || !expedition) speed = 0;
            if (!active) return;
            animator.SetFloat(Gait, Locomotion(speed, rules.walkSpeed, rules.runSpeed));
            if (speed > .15f && displacement.sqrMagnitude > .00001f)
            {
                var direction = transform.parent.InverseTransformDirection(displacement.normalized);
                var rotation = Quaternion.LookRotation(direction, Vector3.up);
                facing.localRotation = Quaternion.Slerp(facing.localRotation, rotation, 1 - Mathf.Exp(-15 * deltaTime));
            }
        }

        public static float Locomotion(float speed, float walkSpeed, float runSpeed)
        {
            if (speed < .12f) return 0;
            walkSpeed = Mathf.Max(.1f, walkSpeed);
            if (speed <= walkSpeed) return Mathf.Clamp01(speed / walkSpeed);
            return 1 + 2 * Mathf.Clamp01((speed - walkSpeed) / Mathf.Max(.1f, runSpeed - walkSpeed));
        }
    }
}
