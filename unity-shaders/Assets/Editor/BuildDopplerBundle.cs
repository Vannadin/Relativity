// DopplerVisual.shader를 KSP 런타임이 읽을 단일 Windows AssetBundle로 굽는 Unity 에디터 빌드 스크립트
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// KSP cannot compile ShaderLab at runtime, so the shader ships pre-compiled inside an
// AssetBundle built with the game's Unity version (2019.4.18f1 LTS). One Windows64 bundle
// serves DX11 *and* OpenGLCore — the script forces both graphics APIs below, so a headless
// batchmode build needs no GUI Player-settings step.
//
// Run from the editor menu (Relativity ▸ Build Doppler Bundle) or headless:
//   Unity.exe -batchmode -quit -projectPath <this folder> -executeMethod BuildDopplerBundle.Build
// Output: AssetBundles/relativityvisual.bundle  → copy to GameData/Relativity/Shaders/.
public static class BuildDopplerBundle
{
    const string OutDir      = "AssetBundles";
    const string BundleName  = "relativityvisual.bundle";  // neutral extension: loaded by path, not by GameDatabase/Shabby
    const string ShaderAsset = "Assets/Relativity/DopplerVisual.shader";
    const string AberrAsset  = "Assets/Relativity/GalaxyAberration.shader";  // star-bunching warp (galaxy camera)
    const string DepthAsset  = "Assets/Relativity/DepthBlack.shader";        // mask-cam hull depth-only replacement
    const string AreaAsset   = "Assets/Relativity/SMAAAreaTex.png";          // SMAA weight LUT (160×560 RG)
    const string SearchAsset = "Assets/Relativity/SMAASearchTex.png";        // SMAA search LUT (64×16 R)

    [MenuItem("Relativity/Build Doppler Bundle")]
    public static void Build()
    {
        // One bundle for DX11 + OpenGL players: force both APIs so the shader is compiled for
        // each (skipping this is the usual cause of a pink shader on OpenGL-forced installs).
        // Apply ONLY when it differs — re-setting graphics APIs schedules an editor restart that
        // aborts the rest of a batchmode run, so on a warm project (already set) we must skip it.
        var want = new[] { GraphicsDeviceType.Direct3D11, GraphicsDeviceType.OpenGLCore };
        var have = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
        bool same = !PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64)
                    && have.Length == want.Length && have[0] == want[0] && have[1] == want[1];
        if (!same)
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, want);
        }

        Directory.CreateDirectory(OutDir);

        var build = new AssetBundleBuild
        {
            assetBundleName = BundleName,
            assetNames      = new[] { ShaderAsset, AberrAsset, DepthAsset, AreaAsset, SearchAsset },
        };

        BuildPipeline.BuildAssetBundles(
            OutDir,
            new[] { build },
            BuildAssetBundleOptions.ForceRebuildAssetBundle | BuildAssetBundleOptions.DeterministicAssetBundle,
            BuildTarget.StandaloneWindows64);

        AssetDatabase.Refresh();
        Debug.Log("[Relativity] built " + Path.Combine(OutDir, BundleName));
    }
}
