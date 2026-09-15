using TMPro;
using UnityEngine;

namespace EarthRecovery
{
    public sealed class POIDebugLabel : MonoBehaviour
    {
        public static void Create(Transform parent, string name, Bounds worldBounds, Vector3 front, TMP_FontAsset font, bool visible)
        {
            var go = new GameObject("DebugLocationLabel"); go.transform.SetParent(parent, false); go.AddComponent<POIDebugLabel>();
            front.y = 0; front.Normalize();
            float extent = Mathf.Abs(front.x) * worldBounds.extents.x + Mathf.Abs(front.z) * worldBounds.extents.z;
            go.transform.position = worldBounds.center + front * (extent + .3f);
            go.transform.position = new Vector3(go.transform.position.x, worldBounds.min.y + Mathf.Max(2.5f, worldBounds.size.y * .8f), go.transform.position.z);
            go.transform.rotation = Quaternion.LookRotation(-front, Vector3.up);
            var text = go.AddComponent<TextMeshPro>(); text.text = "[" + name + "]"; text.font = font;
            text.fontSize = 6; text.alignment = TextAlignmentOptions.Center; text.color = Color.white;
            text.enableAutoSizing = true; text.fontSizeMin = 2; text.fontSizeMax = 6;
            text.rectTransform.sizeDelta = new Vector2(Mathf.Max(4, Mathf.Abs(front.x) * worldBounds.size.z + Mathf.Abs(front.z) * worldBounds.size.x), 1.5f);
            text.ForceMeshUpdate(true); go.SetActive(visible);
        }
        public static void SetVisible(GameObject root, bool visible)
        {
            if (root == null) return;
            foreach (var label in root.GetComponentsInChildren<POIDebugLabel>(true)) label.gameObject.SetActive(visible);
            if (visible) Refresh(root);
        }
        public static void Refresh(GameObject root)
        {
            foreach (var label in root.GetComponentsInChildren<POIDebugLabel>(true)) label.GetComponent<TextMeshPro>().ForceMeshUpdate(true, true);
        }
    }
}
