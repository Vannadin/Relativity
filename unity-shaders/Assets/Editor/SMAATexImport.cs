// SMAA 룩업 텍스처(AreaTex/SearchTex) 임포트 설정 강제 — 무압축·리니어·클램프, Area는 bilinear·Search는 point
using UnityEditor;
using UnityEngine;

// The SMAA weight pass depends on exact texel values: any compression, sRGB decode, mip, or wrap
// would corrupt the lookup. Enforced here so a fresh clone imports them correctly with no manual
// inspector step (headless batchmode builds included).
public class SMAATexImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("SMAAAreaTex") && !assetPath.Contains("SMAASearchTex")) return;
        TextureImporter ti = (TextureImporter)assetImporter;
        ti.textureType        = TextureImporterType.Default;
        ti.sRGBTexture        = false;
        ti.mipmapEnabled      = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.wrapMode           = TextureWrapMode.Clamp;
        ti.filterMode         = assetPath.Contains("SMAASearchTex") ? FilterMode.Point : FilterMode.Bilinear;
        ti.npotScale          = TextureImporterNPOTScale.None;
    }
}
