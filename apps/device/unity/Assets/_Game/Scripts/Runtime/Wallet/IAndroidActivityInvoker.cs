using System.Collections.Generic;

namespace Plemu.Wallet
{
    /// <summary>
    /// Abstraction for invoking Android Activity intents, enabling deterministic unit testing
    /// without native Android JNI runtime dependencies.
    /// </summary>
    public interface IAndroidActivityInvoker
    {
        bool IsIntentAvailable(string schemeUri);

        void LaunchIntent(string uri, Dictionary<string, string> extras);
    }
}
