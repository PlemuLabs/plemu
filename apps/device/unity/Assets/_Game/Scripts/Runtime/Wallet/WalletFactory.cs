using System;
using Plemu.Wallet.Mock;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;
#endif

namespace Plemu.Wallet
{
    /// <summary>
    /// Factory for resolving and instantiating IWallet implementations based on the runtime platform and hardware availability.
    /// In Android environments, detects Seed Vault/MWA capability via AndroidMwaAdapter, falling back gracefully to MockWallet if absent.
    /// In Editor or desktop development environments, resolves MwaWallet with MockMwaAdapter for fast iteration.
    /// </summary>
    public static class WalletFactory
    {
        /// <summary>
        /// Creates an IWallet instance based on the active runtime platform.
        /// </summary>
        public static IWallet CreateWallet()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return CreateAndroidWallet();
#else
            return CreateEditorWallet();
#endif
        }

        /// <summary>
        /// Creates an IWallet instance wrapping a custom or explicit adapter.
        /// Falls back to default resolution if the explicit adapter is null.
        /// </summary>
        public static IWallet CreateWallet(IMobileWalletAdapter explicitAdapter)
        {
            if (explicitAdapter == null)
            {
                return CreateWallet();
            }

            return new MwaWallet(explicitAdapter);
        }

        /// <summary>
        /// Creates an IWallet for editor and desktop environments using MockMwaAdapter.
        /// </summary>
        public static IWallet CreateEditorWallet()
        {
            return new MwaWallet(new MockMwaAdapter());
        }

        /// <summary>
        /// Creates an IWallet for Android devices using the official Solana Unity SDK transport (WebSocket MWA 2.0).
        /// Prioritizes SolanaSdkMwaAdapter when available, falling back to AndroidMwaAdapter (native JNI Intent),
        /// or MockWallet if no hardware wallet adapter is detected.
        /// </summary>
        public static IWallet CreateAndroidWallet(IAndroidJniBridge bridge = null, ISolanaSdkBridge sdkBridge = null)
        {
            if (sdkBridge != null)
            {
                var sdkAdapter = new SolanaSdkMwaAdapter(sdkBridge);
                if (sdkAdapter.IsMwaAvailable())
                {
                    return new MwaWallet(sdkAdapter);
                }
            }

            if (sdkBridge == null && bridge == null)
            {
                var defaultSdkAdapter = new SolanaSdkMwaAdapter();
                if (defaultSdkAdapter.IsMwaAvailable())
                {
                    return new MwaWallet(defaultSdkAdapter);
                }
            }

            var jniAdapter = new AndroidMwaAdapter(bridge);
            if (jniAdapter.IsMwaAvailable())
            {
                return new MwaWallet(jniAdapter);
            }

            return new MockWallet();
        }

        /// <summary>
        /// Creates an IWallet for Android devices using the official Solana Unity SDK transport.
        /// </summary>
        public static IWallet CreateSolanaSdkWallet(ISolanaSdkBridge bridge = null)
        {
            var adapter = new SolanaSdkMwaAdapter(bridge);
            if (adapter.IsMwaAvailable())
            {
                return new MwaWallet(adapter);
            }

            return new MockWallet();
        }

        /// <summary>
        /// Resolves an IWallet simulating the specified platform and bridge configuration.
        /// </summary>
        public static IWallet ResolveWallet(bool isAndroidPlatform, IAndroidJniBridge bridge = null)
        {
            if (isAndroidPlatform)
            {
                return CreateAndroidWallet(bridge);
            }

            return CreateEditorWallet();
        }

        /// <summary>
        /// Resolves an IWallet using either native JNI or the official Solana Unity SDK transport.
        /// </summary>
        public static IWallet ResolveWallet(bool isAndroidPlatform, bool useSolanaSdk, ISolanaSdkBridge sdkBridge = null, IAndroidJniBridge jniBridge = null)
        {
            if (isAndroidPlatform)
            {
                return useSolanaSdk ? CreateSolanaSdkWallet(sdkBridge) : CreateAndroidWallet(jniBridge);
            }

            return CreateEditorWallet();
        }
    }
}
