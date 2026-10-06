using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Plemu.Content;
using UnityEngine;
using UnityEngine.Networking;

namespace Plemu.Platform
{
    /// <summary>
    /// Staging bootstrap for on-device content. Copies packaged manifest and artifact files
    /// from StreamingAssets (which resides inside the APK on Android) to persistentDataPath
    /// so the engine-free System.IO trust chain can consume them.
    /// </summary>
    public static class AndroidContentBootstrap
    {
        private const string MarkerFileName = ".staged-v1";
        private static readonly string[] RequiredFiles =
        {
            FileManifestSource.DefaultManifestName,
            FileManifestSource.DefaultSignatureName
        };

        /// <summary>
        /// Ensures bundled content in StreamingAssets is extracted to targetRootPath.
        /// Returns the verified target root directory path on success, or null if staging fails.
        /// </summary>
        public static async Task<string> EnsureContentStagedAsync(string streamingAssetsPath, string targetRootPath)
        {
            if (string.IsNullOrEmpty(streamingAssetsPath) || string.IsNullOrEmpty(targetRootPath))
                return null;

            string markerPath = Path.Combine(targetRootPath, MarkerFileName);
            if (File.Exists(markerPath))
                return targetRootPath;

            try
            {
                Directory.CreateDirectory(targetRootPath);
                string artifactsDestDir = Path.Combine(targetRootPath, "artifacts");
                Directory.CreateDirectory(artifactsDestDir);

                bool isArchiveOrUrl = streamingAssetsPath.StartsWith("jar:", StringComparison.OrdinalIgnoreCase) ||
                                      streamingAssetsPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                      streamingAssetsPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

                // 1. Copy required manifest and signature files
                foreach (string file in RequiredFiles)
                {
                    string src = CombinePathOrUrl(streamingAssetsPath, file);
                    string dst = Path.Combine(targetRootPath, file);

                    if (isArchiveOrUrl)
                    {
                        if (!await CopyFromUrlAsync(src, dst))
                            return null;
                    }
                    else
                    {
                        if (!File.Exists(src))
                            return null;
                        File.Copy(src, dst, overwrite: true);
                    }
                }

                // 2. Read artifacts from staged manifest
                string stagedManifestPath = Path.Combine(targetRootPath, FileManifestSource.DefaultManifestName);
                IReadOnlyList<string> artifactShas = ReadArtifactList(stagedManifestPath);

                string artifactsSrcDir = CombinePathOrUrl(streamingAssetsPath, "artifacts");
                foreach (string sha in artifactShas)
                {
                    string src = CombinePathOrUrl(artifactsSrcDir, sha);
                    string dst = Path.Combine(artifactsDestDir, sha);

                    if (isArchiveOrUrl)
                    {
                        if (!await CopyFromUrlAsync(src, dst))
                            return null;
                    }
                    else
                    {
                        if (File.Exists(src))
                        {
                            File.Copy(src, dst, overwrite: true);
                        }
                    }
                }

                File.WriteAllText(markerPath, Application.version ?? "1.0");
                return targetRootPath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AndroidContentBootstrap] Staging failed: {ex.Message}");
                return null;
            }
        }

        private static string CombinePathOrUrl(string baseUri, string relativePath)
        {
            if (baseUri.Contains("://"))
                return $"{baseUri.TrimEnd('/')}/{relativePath.TrimStart('/')}";
            return Path.Combine(baseUri, relativePath);
        }

        private static async Task<bool> CopyFromUrlAsync(string srcUrl, string destPath)
        {
            using UnityWebRequest req = UnityWebRequest.Get(srcUrl);
            UnityWebRequestAsyncOperation op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[AndroidContentBootstrap] Failed to download {srcUrl}: {req.error}");
                return false;
            }

            byte[] data = req.downloadHandler.data;
            if (data == null || data.Length == 0)
                return false;

            File.WriteAllBytes(destPath, data);
            return true;
        }

        private static IReadOnlyList<string> ReadArtifactList(string manifestPath)
        {
            var shas = new List<string>();
            if (!File.Exists(manifestPath))
                return shas;

            string json = File.ReadAllText(manifestPath);
            var manifest = JsonConvert.DeserializeObject<Manifest>(json);
            if (manifest?.Entries == null)
                return shas;

            foreach (var entry in manifest.Entries)
            {
                if (!string.IsNullOrEmpty(entry?.Sha256) && IsHex64(entry.Sha256))
                {
                    shas.Add(entry.Sha256);
                }
            }

            return shas;
        }

        private static bool IsHex64(string str)
        {
            if (str == null || str.Length != 64)
                return false;

            for (int i = 0; i < 64; i++)
            {
                char c = str[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    return false;
            }
            return true;
        }
    }
}
