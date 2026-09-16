using UnityEditor;
using UnityEngine;

namespace EarthRecovery.Editor
{
    public sealed class MainMenuImporter : AssetPostprocessor
    {
        public override uint GetVersion() => 1;
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/MainMenu/") && !assetPath.StartsWith("Assets/Resources/ExpeditionLobby/") && !assetPath.StartsWith("Assets/Resources/WaitingRoom/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = assetPath.EndsWith("/MainMenu_BG_Clean_Base.png") || assetPath.EndsWith("/Logo_HumanThings2.png") || assetPath.EndsWith("/Background_Artwork.png");
            if (importer.mipmapEnabled) importer.filterMode = FilterMode.Trilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
        }
    }
}
