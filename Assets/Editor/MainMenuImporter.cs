using UnityEditor;

namespace EarthRecovery.Editor
{
    public sealed class MainMenuImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/MainMenu/") && !assetPath.StartsWith("Assets/Resources/ExpeditionLobby/") && !assetPath.StartsWith("Assets/Resources/WaitingRoom/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
        }
    }
}
