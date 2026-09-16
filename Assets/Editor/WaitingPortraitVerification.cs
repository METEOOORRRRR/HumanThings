using System.IO;
using UnityEditor;
using UnityEngine;

namespace EarthRecovery.Editor
{
    public static class WaitingPortraitVerification
    {
        [MenuItem("Earth Recovery/Verify Waiting Character Portraits")]
        public static void Capture()
        {
            const string output = "QA/WaitingPortrait/Editor";
            Directory.CreateDirectory(output);
            var root = new GameObject("Waiting portrait verification");
            var previous = RenderTexture.active;
            try
            {
                var portraits = root.AddComponent<WaitingCharacterPortraits>(); portraits.Initialize();
                portraits.Prepare(3, true);
                foreach (var camera in root.GetComponentsInChildren<Camera>()) { camera.Render(); camera.Render(); }
                foreach (var character in PlayerCharacterCatalog.Load().characters)
                {
                    var texture = portraits.TextureFor(character.id);
                    RenderTexture.active = texture;
                    var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                    try
                    {
                        pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply();
                        File.WriteAllBytes(Path.Combine(output, character.id + ".png"), pixels.EncodeToPNG());
                    }
                    finally { Object.DestroyImmediate(pixels); }
                }
                Debug.Log("WAITING_PORTRAIT_EDITOR_CAPTURE_PASS");
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(root); }
        }
    }
}
