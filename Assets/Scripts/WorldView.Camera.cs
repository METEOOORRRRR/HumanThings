using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace EarthRecovery
{
    public sealed partial class WorldView
    {
        GameObject cameraTarget;
        Vector3 previousFocus;
        float boomLength = 3.6f;

        public static float CameraCollisionRadius(Camera camera)
        {
            float halfHeight = camera.nearClipPlane * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
            return Mathf.Max(.2f, new Vector3(halfHeight * camera.aspect, halfHeight, camera.nearClipPlane).magnitude + .03f);
        }

        void FollowCamera(Vector3 focus, Quaternion rotation, GameObject target)
        {
            float safeLength = Vector3.Distance(focus, ResolveCameraPosition(focus, rotation, CameraCollisionRadius(eye)));
            bool cut = cameraTarget != target || (focus - previousFocus).sqrMagnitude > 4;
            // Obstructions retract immediately. Only the return to the full boom is eased.
            boomLength = cut ? safeLength : Mathf.Min(safeLength, Mathf.Lerp(boomLength, safeLength, 1 - Mathf.Exp(-10 * Time.deltaTime)));
            eye.transform.SetPositionAndRotation(focus + rotation * Vector3.back * boomLength, rotation);
            cameraTarget = target; previousFocus = focus;
            if (cut) ResetCameraHistory();
        }

        void ResetCameraHistory() => eye.GetUniversalAdditionalCameraData().resetHistory = true;
    }
}
