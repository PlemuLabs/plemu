using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Plemu.EditorTools
{
    /// <summary>
    /// Editor convenience menu for the native-plugin + content staging a fresh clone needs before
    /// the emulator can run in the editor. Wraps the two shell scripts so a developer can open the
    /// project and stage with one click instead of dropping to a terminal.
    /// </summary>
    /// <remarks>
    /// The scripts build the native shim + core (gitignored dylibs) and build+sign the beta manifest
    /// and stage the game into StreamingAssets. macOS/Linux only (they are bash + clang); on Windows
    /// run them under WSL/Git Bash instead.
    /// </remarks>
    public static class StageMenu
    {
        private static string UnityProjectRoot => Directory.GetParent(Application.dataPath)!.FullName;

        [MenuItem("Plemu/Stage Native + Content", priority = 0)]
        public static void StageAll()
        {
            if (RunScript("stage-editor-plugins.sh"))
                RunScript("stage-content.sh");
            AssetDatabase.Refresh();
        }

        [MenuItem("Plemu/Stage/Native Plugins (shim + core)", priority = 20)]
        public static void StageNative()
        {
            RunScript("stage-editor-plugins.sh");
            AssetDatabase.Refresh();
        }

        [MenuItem("Plemu/Stage/Content (manifest + game)", priority = 21)]
        public static void StageContent()
        {
            RunScript("stage-content.sh");
            AssetDatabase.Refresh();
        }

        [MenuItem("Plemu/Build PSG1 APK", priority = 40)]
        public static void BuildApk()
        {
            // Delegates to the batch build entry so the menu and CI share one code path.
            Plemu.Editor.BuildScript.BuildPSG1();
        }

        private static bool RunScript(string scriptName)
        {
            string path = Path.Combine(UnityProjectRoot, scriptName);
            if (!File.Exists(path))
            {
                Debug.LogError($"[Plemu] staging script not found: {path}");
                return false;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"\"{path}\"",
                    WorkingDirectory = UnityProjectRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    Debug.LogError($"[Plemu] failed to start {scriptName}");
                    return false;
                }

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(stdout))
                    Debug.Log($"[Plemu] {scriptName}\n{stdout}");

                if (process.ExitCode != 0)
                {
                    Debug.LogError($"[Plemu] {scriptName} exited {process.ExitCode}\n{stderr}");
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(stderr))
                    Debug.LogWarning($"[Plemu] {scriptName} (stderr)\n{stderr}");

                Debug.Log($"[Plemu] {scriptName} OK");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Plemu] {scriptName} threw: {e.Message}");
                return false;
            }
        }
    }
}
