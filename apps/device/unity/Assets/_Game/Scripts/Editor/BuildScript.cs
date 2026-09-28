using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Plemu.Editor
{
    /// <summary>
    /// Batch-mode build entry points for the Plemu device app. <see cref="BuildPSG1"/> produces the
    /// PlaySolana PSG1 (Android arm64-v8a) APK with IL2CPP, packaging the native shim + SameBoy core
    /// staged under <c>Assets/Plugins/Android/libs/arm64-v8a</c> by <c>stage-android.sh</c>.
    /// </summary>
    /// <remarks>
    /// Invoke from CI/CLI (see <c>apps/device/unity/stage-android.sh</c> for the .so staging that
    /// must run first):
    /// <code>
    /// Unity -batchmode -quit -projectPath apps/device/unity -buildTarget Android \
    ///   -executeMethod Plemu.Editor.BuildScript.BuildPSG1 -logFile &lt;log&gt; \
    ///   -androidSdkPath  &lt;AndroidPlayer&gt;/SDK \
    ///   -androidNdkPath  &lt;AndroidPlayer&gt;/NDK \
    ///   -jdkPath         &lt;AndroidPlayer&gt;/OpenJDK
    /// </code>
    /// This validates the IL2CPP AOT conversion of the P/Invoke boundary (the SK.Libretro failure
    /// mode): our shim keeps every libretro callback native-side and only blittable structs / value
    /// types cross the managed boundary, so IL2CPP should convert cleanly. On-hardware execution is
    /// DevKit/RK3588-gated (no PSG1 in CI) — this build proves it COMPILES + PACKAGES for arm64.
    /// </remarks>
    public static class BuildScript
    {
        // PSG1 target parameters (per M5 brief).
        private const string ApplicationId = "com.playsolana.games.plemu";
        private const string ProductName = "Plemu";
        private const string CompanyName = "PlaySolana";
        private const int MinSdk = 30;
        private const int TargetSdk = 34;
        private const string ScriptingDefine = "PLAYSOLANA_PSG1";
        private const string BootScene = "Assets/_Game/Scenes/Boot.unity";

        // Output: apps/device/build/android/Plemu.apk. projectRoot below is the Unity project dir
        // (apps/device/unity), so "../build/android/Plemu.apk" resolves up one level to apps/device
        // then into build/android — matching the M5 brief's required output path.
        private const string OutputRelative = "../build/android/Plemu.apk";

        /// <summary>
        /// Configures PSG1 player settings and builds the arm64-v8a APK with IL2CPP. Throws on build
        /// failure so <c>-quit</c> in batch mode exits nonzero (CI-friendly).
        /// </summary>
        public static void BuildPSG1()
        {
            ConfigurePSG1PlayerSettings();

            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outputPath = Path.GetFullPath(Path.Combine(projectRoot, OutputRelative));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { BootScene },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };

            Debug.Log($"[BuildPSG1] building APK -> {outputPath}");
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            UnityEditor.Build.Reporting.BuildSummary summary = report.summary;

            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"[BuildPSG1] build FAILED: result={summary.result}, " +
                    $"errors={summary.totalErrors}, size={summary.totalSize} bytes");
            }

            Debug.Log(
                $"[BuildPSG1] build SUCCEEDED: {summary.totalSize} bytes, " +
                $"time={summary.totalTime}, output={outputPath}");
        }

        /// <summary>
        /// Applies the PSG1 player settings: IL2CPP, ARM64-only, minSdk 30 / targetSdk 34, portrait,
        /// OpenGLES3-only (no Vulkan), the <c>PLAYSOLANA_PSG1</c> scripting define, the lowercase
        /// applicationId, native libs extracted (not compressed in the APK), and product identity.
        /// </summary>
        public static void ConfigurePSG1PlayerSettings()
        {
            var android = NamedBuildTarget.Android;

            // --- identity ---
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(android, ApplicationId);

            // --- scripting backend: IL2CPP (the whole point — validate AOT P/Invoke marshalling) ---
            // API compatibility level is intentionally left at the project default (NET_Unity_4_8):
            // the P/Invoke surface (blittable structs + IntPtr) needs nothing from a specific profile,
            // and changing it here would be an unrelated project-wide edit.
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);

            // --- architecture: ARM64 only ---
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // --- SDK levels ---
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)MinSdk;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)TargetSdk;

            // --- orientation: portrait ---
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // --- graphics: OpenGLES3 only (drop Vulkan; PSG1/RK3588 Mali path is GLES3) ---
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });

            // --- signing: debug keystore for this slice (production signing is a later task) ---
            PlayerSettings.Android.useCustomKeystore = false;

            // --- native libs must be extracted onto the filesystem so the shim can dlopen the core
            //     from nativeLibraryDir (Android >=10 forbids dlopen of a lib mapped inside a
            //     compressed APK entry). In Unity 6000.3 the old PlayerSettings.Android
            //     .useLegacyPackaging / extractNativeLibs toggles were REMOVED from the scripting
            //     API; APK builds extract native libs by default (android:extractNativeLibs="true"
            //     in the generated manifest), which is exactly what we need — so there is nothing to
            //     set here. (For an .aab this would need a bundletool/Gradle-template override; we
            //     build an .apk.) See the report for the on-device dlopen-path note. ---

            // --- scripting define (additive; preserve any existing) ---
            AddScriptingDefine(android, ScriptingDefine);

            Debug.Log(
                $"[BuildPSG1] player settings: IL2CPP, ARM64, minSdk={MinSdk}, targetSdk={TargetSdk}, " +
                $"portrait, GLES3-only, define={ScriptingDefine}, id={ApplicationId}");
        }

        private static void AddScriptingDefine(NamedBuildTarget target, string define)
        {
            string existing = PlayerSettings.GetScriptingDefineSymbols(target);
            if (string.IsNullOrEmpty(existing))
            {
                PlayerSettings.SetScriptingDefineSymbols(target, define);
                return;
            }

            foreach (string d in existing.Split(';'))
            {
                if (string.Equals(d.Trim(), define, StringComparison.Ordinal))
                    return; // already present
            }

            PlayerSettings.SetScriptingDefineSymbols(target, existing + ";" + define);
        }
    }
}
