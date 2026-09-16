using UnityEditor;
using UnityEngine;

namespace EarthRecovery.Editor
{
    public sealed class TerminalImporter : AssetPostprocessor
    {
        public override uint GetVersion() => 1;
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/BaseCampTerminal/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = assetPath.EndsWith("/Terminal_Background.png");
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = importer.mipmapEnabled ? FilterMode.Trilinear : FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
