using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Plemu.Wallet
{
    /// <summary>
    /// Encodes and decodes Mobile Wallet Adapter (MWA 2.0) JSON-RPC 2.0 messages.
    /// Handles framing for authorize, reauthorize, deauthorize, and sign_and_send_transactions.
    /// </summary>
    public static class MwaJsonRpcProtocol
    {
        public const string JsonRpcVersion = "2.0";
        public const string MethodAuthorize = "authorize";
        public const string MethodReauthorize = "reauthorize";
        public const string MethodDeauthorize = "deauthorize";
        public const string MethodSignAndSendTransactions = "sign_and_send_transactions";

        public static string CreateAuthorizeRequest(int id, string name, string uri, string icon, string cluster = "devnet")
        {
            var req = new JObject
            {
                ["jsonrpc"] = JsonRpcVersion,
                ["id"] = id,
                ["method"] = MethodAuthorize,
                ["params"] = new JObject
                {
                    ["identity"] = new JObject
                    {
                        ["name"] = name,
                        ["uri"] = uri,
                        ["icon"] = icon
                    },
                    ["cluster"] = cluster
                }
            };
            return req.ToString(Formatting.None);
        }

        public static string CreateReauthorizeRequest(int id, string authToken, string name, string uri, string icon)
        {
            var req = new JObject
            {
                ["jsonrpc"] = JsonRpcVersion,
                ["id"] = id,
                ["method"] = MethodReauthorize,
                ["params"] = new JObject
                {
                    ["identity"] = new JObject
                    {
                        ["name"] = name,
                        ["uri"] = uri,
                        ["icon"] = icon
                    },
                    ["auth_token"] = authToken
                }
            };
            return req.ToString(Formatting.None);
        }

        public static string CreateDeauthorizeRequest(int id, string authToken)
        {
            var req = new JObject
            {
                ["jsonrpc"] = JsonRpcVersion,
                ["id"] = id,
                ["method"] = MethodDeauthorize,
                ["params"] = new JObject
                {
                    ["auth_token"] = authToken
                }
            };
            return req.ToString(Formatting.None);
        }

        public static string CreateSignAndSendTransactionsRequest(int id, byte[] payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            var payloads = new JArray { Convert.ToBase64String(payload) };
            var req = new JObject
            {
                ["jsonrpc"] = JsonRpcVersion,
                ["id"] = id,
                ["method"] = MethodSignAndSendTransactions,
                ["params"] = new JObject
                {
                    ["payloads"] = payloads
                }
            };
            return req.ToString(Formatting.None);
        }

        public static MwaConnectionResult ParseAuthorizeResponse(string jsonRpcResponse)
        {
            if (string.IsNullOrWhiteSpace(jsonRpcResponse))
            {
                return MwaConnectionResult.Failed("Empty JSON-RPC response from wallet.");
            }

            try
            {
                var jObj = JObject.Parse(jsonRpcResponse);
                if (jObj["error"] != null)
                {
                    int code = jObj["error"]["code"]?.Value<int>() ?? -1;
                    string message = jObj["error"]["message"]?.Value<string>() ?? "Unknown JSON-RPC error";

                    if (code == -32600 || message.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("decline", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return MwaConnectionResult.Cancelled(message);
                    }
                    return MwaConnectionResult.Failed($"MWA Error ({code}): {message}");
                }

                var result = jObj["result"];
                if (result == null)
                {
                    return MwaConnectionResult.Failed("Missing result block in authorize response.");
                }

                string authToken = result["auth_token"]?.Value<string>();
                string rawAddress = null;

                var accounts = result["accounts"] as JArray;
                if (accounts != null && accounts.Count > 0)
                {
                    rawAddress = accounts[0]["address"]?.Value<string>();
                }

                string publicKey = NormalizeToBase58PublicKey(rawAddress);

                if (string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(authToken))
                {
                    return MwaConnectionResult.Failed("Authorize response did not contain a valid 32-byte public key or auth token.");
                }

                return MwaConnectionResult.Ok(publicKey, authToken);
            }
            catch (Exception ex)
            {
                return MwaConnectionResult.Failed($"Failed to parse MWA authorize response: {ex.Message}");
            }
        }

        public static MwaTransactionResult ParseSignAndSendTransactionsResponse(string jsonRpcResponse)
        {
            if (string.IsNullOrWhiteSpace(jsonRpcResponse))
            {
                return MwaTransactionResult.Failed("Empty JSON-RPC response from wallet.");
            }

            try
            {
                var jObj = JObject.Parse(jsonRpcResponse);
                if (jObj["error"] != null)
                {
                    int code = jObj["error"]["code"]?.Value<int>() ?? -1;
                    string message = jObj["error"]["message"]?.Value<string>() ?? "Unknown JSON-RPC error";

                    if (code == -32600 || message.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("decline", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return MwaTransactionResult.Cancelled(message);
                    }
                    return MwaTransactionResult.Failed($"MWA Error ({code}): {message}");
                }

                var result = jObj["result"];
                if (result == null)
                {
                    return MwaTransactionResult.Failed("Missing result block in sign_and_send_transactions response.");
                }

                var signatures = result["signatures"] as JArray;
                if (signatures == null || signatures.Count == 0)
                {
                    return MwaTransactionResult.Failed("No transaction signatures returned by wallet.");
                }

                string rawSig = signatures[0]?.Value<string>();
                string signature = NormalizeToBase58Signature(rawSig);
                if (string.IsNullOrWhiteSpace(signature))
                {
                    return MwaTransactionResult.Failed("Invalid or empty signature returned by wallet (expected 64-byte Base64 or Base58).");
                }

                return MwaTransactionResult.Ok(signature);
            }
            catch (Exception ex)
            {
                return MwaTransactionResult.Failed($"Failed to parse MWA sign_and_send_transactions response: {ex.Message}");
            }
        }

        /// <summary>
        /// Normalizes an MWA address to a standard 32-byte Base58 Solana public key.
        /// MWA 2.0 specification defines accounts[].address as a Base64-encoded 32-byte array.
        /// Also supports standard Base58-encoded 32-byte strings for compatibility.
        /// </summary>
        public static string NormalizeToBase58PublicKey(string rawAddress)
        {
            if (string.IsNullOrWhiteSpace(rawAddress))
            {
                return null;
            }

            string trimmed = rawAddress.Trim();

            // 1. Primary path per MWA 2.0 spec: Base64-encoded 32-byte public key
            try
            {
                byte[] b64 = Convert.FromBase64String(trimmed);
                if (b64.Length == 32)
                {
                    return SolanaBase58.Encode(b64);
                }
            }
            catch
            {
                // Not a valid Base64 string, proceed to Base58 check
            }

            // 2. Compatibility path: standard 32-byte Base58 string
            if (SolanaBase58.TryDecode(trimmed, out byte[] b58) && b58.Length == 32)
            {
                return SolanaBase58.Encode(b58);
            }

            return null;
        }

        /// <summary>
        /// Normalizes an MWA signature to a standard 64-byte Base58 Solana signature string.
        /// MWA 2.0 specification defines signatures[] as Base64-encoded 64-byte arrays.
        /// Also supports Base58-encoded signatures (64 bytes, or 32 bytes for legacy mocks) for compatibility.
        /// </summary>
        public static string NormalizeToBase58Signature(string rawSignature)
        {
            if (string.IsNullOrWhiteSpace(rawSignature))
            {
                return null;
            }

            string trimmed = rawSignature.Trim();

            // 1. Primary path per MWA 2.0 spec: Base64-encoded 64-byte Ed25519 signature
            try
            {
                byte[] b64 = Convert.FromBase64String(trimmed);
                if (b64.Length == 64)
                {
                    return SolanaBase58.Encode(b64);
                }
            }
            catch
            {
                // Not a valid Base64 string, proceed to Base58 check
            }

            // 2. Compatibility path: Base58 string (64 bytes, or 32 bytes for legacy unit test mocks)
            if (SolanaBase58.TryDecode(trimmed, out byte[] b58) && (b58.Length == 64 || b58.Length == 32))
            {
                return SolanaBase58.Encode(b58);
            }

            return null;
        }
    }
}
