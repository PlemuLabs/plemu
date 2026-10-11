using System;
using System.Collections.Generic;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;
#endif

namespace Plemu.Wallet
{
    /// <summary>
    /// Default implementation of IAndroidActivityInvoker using Android JNI when running on Android hardware,
    /// and failing closed safely on unsupported platforms.
    /// </summary>
    public class DefaultAndroidActivityInvoker : IAndroidActivityInvoker
    {
        public bool IsIntentAvailable(string schemeUri)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var packageManager = activity.Call<AndroidJavaObject>("getPackageManager"))
                using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                using (var parsedUri = uriClass.CallStatic<AndroidJavaObject>("parse", schemeUri))
                using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW", parsedUri))
                {
                    using (var resolveInfoList = packageManager.Call<AndroidJavaObject>("queryIntentActivities", intent, 0))
                    {
                        if (resolveInfoList == null)
                        {
                            return false;
                        }
                        int count = resolveInfoList.Call<int>("size");
                        return count > 0;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
#else
            return false;
#endif
        }

        public void LaunchIntent(string uri, Dictionary<string, string> extras)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                using (var parsedUri = uriClass.CallStatic<AndroidJavaObject>("parse", uri))
                using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW", parsedUri))
                {
                    if (extras != null)
                    {
                        foreach (var kvp in extras)
                        {
                            intent.Call<AndroidJavaObject>("putExtra", kvp.Key, kvp.Value);
                        }
                    }

                    activity.Call("startActivity", intent);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to launch Android Intent: {ex.Message}", ex);
            }
#else
            throw new PlatformNotSupportedException("Android Activity Invoker is only supported on Android runtime.");
#endif
        }
    }
}
