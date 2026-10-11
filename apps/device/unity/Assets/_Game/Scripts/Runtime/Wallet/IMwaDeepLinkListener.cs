using System;

namespace Plemu.Wallet
{
    /// <summary>
    /// Abstraction for listening to inbound deep link callbacks from external wallet applications.
    /// </summary>
    public interface IMwaDeepLinkListener
    {
        event Action<string> DeepLinkReceived;

        void StartListening();

        void StopListening();
    }
}
