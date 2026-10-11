using System;
using UnityEngine;

namespace Plemu.Wallet
{
    /// <summary>
    /// Default implementation of IMwaDeepLinkListener hooked to Unity's Application.deepLinkActivated event.
    /// </summary>
    public class UnityDeepLinkListener : IMwaDeepLinkListener
    {
        public event Action<string> DeepLinkReceived;
        private bool _isListening;
        private string _lastProcessedColdStartUrl;

        public void StartListening()
        {
            if (!_isListening)
            {
                Application.deepLinkActivated += OnDeepLinkActivated;
                _isListening = true;

                string initialUrl = Application.absoluteURL;
                if (!string.IsNullOrEmpty(initialUrl) && !string.Equals(initialUrl, _lastProcessedColdStartUrl, StringComparison.Ordinal))
                {
                    _lastProcessedColdStartUrl = initialUrl;
                    OnDeepLinkActivated(initialUrl);
                }
            }
        }

        public void StopListening()
        {
            if (_isListening)
            {
                Application.deepLinkActivated -= OnDeepLinkActivated;
                _isListening = false;
            }
        }

        private void OnDeepLinkActivated(string url)
        {
            DeepLinkReceived?.Invoke(url);
        }
    }
}
