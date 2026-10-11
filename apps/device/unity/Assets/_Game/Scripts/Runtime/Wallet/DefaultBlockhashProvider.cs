using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Plemu.Wallet
{
    /// <summary>
    /// Default Solana blockhash provider with Devnet RPC integration operating strictly fail-closed.
    /// </summary>
    public sealed class DefaultBlockhashProvider : IBlockhashProvider, IDisposable
    {
        public const string DefaultRpcUrl = "https://api.devnet.solana.com";

        private readonly string _rpcUrl;
        private readonly HttpClient _httpClient;
        private readonly bool _disposeClient;

        public DefaultBlockhashProvider(string rpcUrl = DefaultRpcUrl, HttpClient httpClient = null)
        {
            _rpcUrl = string.IsNullOrWhiteSpace(rpcUrl) ? DefaultRpcUrl : rpcUrl;
            if (httpClient != null)
            {
                _httpClient = httpClient;
                _disposeClient = false;
            }
            else
            {
                _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                _disposeClient = true;
            }
        }

        public void Dispose()
        {
            if (_disposeClient)
            {
                _httpClient?.Dispose();
            }
        }

        public async Task<string> GetRecentBlockhashAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string requestJson = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"getLatestBlockhash\",\"params\":[{\"commitment\":\"confirmed\"}]}";
                using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                using var response = await _httpClient.PostAsync(_rpcUrl, content, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"Solana RPC returned status {(int)response.StatusCode}: {response.ReasonPhrase}");
                }

                string body = await response.Content.ReadAsStringAsync();
                var jObj = JObject.Parse(body);
                string blockhash = jObj["result"]?["value"]?["blockhash"]?.ToString();
                if (string.IsNullOrWhiteSpace(blockhash))
                {
                    throw new InvalidOperationException("Solana RPC returned empty or missing blockhash");
                }

                return blockhash;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException($"Failed to retrieve recent blockhash from Solana RPC ({_rpcUrl}): {ex.Message}", ex);
            }
        }
    }
}
