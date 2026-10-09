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
        /// Creates an IWallet for Android devices. Detects MWA/Seed Vault presence and returns MwaWallet if available;
        /// falls back to MockWallet if MWA is not detected or unavailable.
        /// </summary>
        public static IWallet CreateAndroidWallet(IAndroidJniBridge bridge = null)
        {
            var adapter = new AndroidMwaAdapter(bridge);
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
    }
}
