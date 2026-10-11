using System;
using UnityEngine;

namespace Plemu.Platform
{
    /// <summary>
    /// JNI bridge to Android's Storage Access Framework (SAF) using <c>ACTION_OPEN_DOCUMENT</c>.
    /// Declares no storage permissions in AndroidManifest.xml: each file selection is an
    /// explicit, isolated user consent act.
    /// </summary>
    public static class SafPicker
    {
        public const int RequestCode = 0x5AF1;

        /// <summary>
        /// Initiates an Android system file picker dialog for selecting ROM files.
        /// </summary>
        public static void OpenDocumentPicker(Action<string> onFilePicked = null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.OPEN_DOCUMENT");
                
                intent.Call<AndroidJavaObject>("addCategory", "android.intent.category.OPENABLE");
                intent.Call<AndroidJavaObject>("setType", "*/*");

                currentActivity.Call("startActivityForResult", intent, RequestCode);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SafPicker] Failed to start ACTION_OPEN_DOCUMENT: {ex.Message}");
            }
#else
            Debug.Log("[SafPicker] SAF picker invoked (running in Editor/Standalone fallback mode).");
            onFilePicked?.Invoke(null);
#endif
        }
    }
}
