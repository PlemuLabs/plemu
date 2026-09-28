using System.IO;
using UnityEngine;

namespace Plemu.Emulation
{
    /// <summary>
    /// Resolves a SPEC §1.5 core id (e.g. <c>sameboy</c>) to the on-disk path of the core the shim
    /// will <c>dlopen</c>. Two platforms, one entry point (<see cref="ResolveCorePath"/>):
    /// <list type="bullet">
    /// <item>Editor (macOS): the core is staged DATA under
    /// <c>StreamingAssets/Cores/Editor/&lt;coreId&gt;_libretro.dylib</c> (see
    /// <c>stage-editor-plugins.sh</c>) — a plain file the shim opens by path, NOT a Unity plugin.</item>
    /// <item>Android (PSG1): Android ≥10 forbids <c>dlopen</c> from app data dirs, so the core must
    /// ship inside the APK's native lib dir and be loaded from <c>nativeLibraryDir</c>. It is staged
    /// as a native plugin <c>lib&lt;coreId&gt;_libretro_android.so</c> (the <c>lib…so</c> name is
    /// mandatory for the APK to extract it into <c>nativeLibraryDir</c>) and resolved there via
    /// <c>ApplicationInfo.nativeLibraryDir</c> (see <c>stage-android.sh</c>).</item>
    /// </list>
    /// Only the host shim is P/Invoked; the core is never a <c>[DllImport]</c> target.
    /// </summary>
    public static class CoreRegistry
    {
        /// <summary>The editor StreamingAssets subfolder that holds staged core dylibs.</summary>
        public const string EditorCoresSubdir = "Cores/Editor";

        /// <summary>The dylib filename for <paramref name="coreId"/> (macOS editor convention).</summary>
        public static string EditorCoreFileName(string coreId) => $"{coreId}_libretro.dylib";

        /// <summary>
        /// The Android core shared-object filename for <paramref name="coreId"/>. The <c>lib</c>
        /// prefix + <c>.so</c> suffix are REQUIRED: Unity/Gradle only extract <c>lib*.so</c> from a
        /// plugin dir into the APK's <c>nativeLibraryDir</c>. Matches the artifact name emitted by
        /// <c>cores/build_sameboy_android.sh</c> (which is then renamed with the <c>lib</c> prefix
        /// when staged as a plugin — see <c>stage-android.sh</c>).
        /// </summary>
        public static string AndroidCoreFileName(string coreId) => $"lib{coreId}_libretro_android.so";

        /// <summary>
        /// Absolute path to the core for <paramref name="coreId"/> on the current platform, or null
        /// if <paramref name="coreId"/> is empty. Existence is not checked here — the caller surfaces
        /// a missing-core error from the failed <c>rh_load_core</c>. On Android the path is the
        /// <c>lib*.so</c> under <c>nativeLibraryDir</c>; in the editor it is the staged dylib under
        /// StreamingAssets.
        /// </summary>
        public static string ResolveCorePath(string coreId)
        {
            if (string.IsNullOrEmpty(coreId))
                return null;

#if UNITY_EDITOR
            return ResolveEditorCorePath(coreId);
#elif UNITY_ANDROID
            return ResolveAndroidCorePath(coreId);
#else
            return ResolveEditorCorePath(coreId);
#endif
        }

        /// <summary>
        /// Absolute path to the staged core dylib for <paramref name="coreId"/> in the editor, or
        /// null if <paramref name="coreId"/> is empty. Kept public so editor tooling/tests can
        /// resolve the StreamingAssets path directly.
        /// </summary>
        public static string ResolveEditorCorePath(string coreId)
        {
            if (string.IsNullOrEmpty(coreId))
                return null;

            return Path.Combine(
                Application.streamingAssetsPath,
                EditorCoresSubdir,
                EditorCoreFileName(coreId));
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Absolute path to the core inside the APK's native lib dir on Android. Reads
        /// <c>ApplicationInfo.nativeLibraryDir</c> off the current activity's ApplicationInfo — the
        /// only dir Android ≥10 permits <c>dlopen</c> from for APK-shipped cores. The core lands
        /// there because it is staged as a <c>lib*.so</c> native plugin for arm64-v8a.
        /// </summary>
        private static string ResolveAndroidCorePath(string coreId)
        {
            string nativeLibraryDir;
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var appInfo = activity.Call<AndroidJavaObject>("getApplicationInfo"))
            {
                nativeLibraryDir = appInfo.Get<string>("nativeLibraryDir");
            }

            return Path.Combine(nativeLibraryDir, AndroidCoreFileName(coreId));
        }
#endif
    }
}
