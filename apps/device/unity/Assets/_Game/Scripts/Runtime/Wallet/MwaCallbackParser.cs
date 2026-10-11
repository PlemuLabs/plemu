using System;
using System.Collections.Generic;

namespace Plemu.Wallet
{
    /// <summary>
    /// Utility for parsing deep link callback URIs received from external Mobile Wallet Adapter wallets (e.g. Seed Vault).
    /// </summary>
    public static class MwaCallbackParser
    {
        public static Dictionary<string, string> ExtractQueryParams(string url)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(url))
            {
                return result;
            }

            int queryIndex = url.IndexOf('?');
            if (queryIndex < 0 || queryIndex >= url.Length - 1)
            {
                return result;
            }

            string queryString = url.Substring(queryIndex + 1);
            string[] pairs = queryString.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string pair in pairs)
            {
                int eqIndex = pair.IndexOf('=');
                if (eqIndex > 0)
                {
                    string rawKey = pair.Substring(0, eqIndex).Replace('+', ' ');
                    string rawVal = eqIndex < pair.Length - 1 ? pair.Substring(eqIndex + 1).Replace('+', ' ') : string.Empty;
                    string key = Uri.UnescapeDataString(rawKey);
                    string value = Uri.UnescapeDataString(rawVal);
                    result[key] = value;
                }
                else if (pair.Length > 0)
                {
                    string rawKey = pair.Replace('+', ' ');
                    string key = Uri.UnescapeDataString(rawKey);
                    result[key] = string.Empty;
                }
            }

            return result;
        }

        public static MwaConnectionResult ParseConnectionCallback(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return MwaConnectionResult.Failed("Empty callback URL received from wallet.");
            }

            var query = ExtractQueryParams(url);

            if (IsCancellation(query, out string cancelReason))
            {
                return MwaConnectionResult.Cancelled(cancelReason);
            }

            if (IsError(query, out string errorMessage))
            {
                return MwaConnectionResult.Failed(errorMessage);
            }

            string publicKey = GetFirstValue(query, "public_key", "publicKey", "account_address", "address");
            string authToken = GetFirstValue(query, "auth_token", "authToken", "token");

            if (!string.IsNullOrEmpty(publicKey) && !string.IsNullOrEmpty(authToken))
            {
                return MwaConnectionResult.Ok(publicKey, authToken);
            }

            return MwaConnectionResult.Failed("Malformed callback URI: missing public_key or auth_token.");
        }

        public static MwaTransactionResult ParseTransactionCallback(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return MwaTransactionResult.Failed("Empty callback URL received from wallet.");
            }

            var query = ExtractQueryParams(url);

            if (IsCancellation(query, out string cancelReason))
            {
                return MwaTransactionResult.Cancelled(cancelReason);
            }

            if (IsError(query, out string errorMessage))
            {
                return MwaTransactionResult.Failed(errorMessage);
            }

            string signature = GetFirstValue(query, "signature", "sig", "tx_signature");
            if (!string.IsNullOrEmpty(signature))
            {
                return MwaTransactionResult.Ok(signature);
            }

            return MwaTransactionResult.Failed("Malformed callback URI: missing signature.");
        }

        private static bool IsCancellation(Dictionary<string, string> query, out string reason)
        {
            reason = null;

            string error = GetFirstValue(query, "error", "reason", "status", "result");
            string errorCode = GetFirstValue(query, "error_code", "code");
            string message = GetFirstValue(query, "error_message", "error_description", "message");

            bool isCancel = false;

            if (!string.IsNullOrEmpty(error))
            {
                string lower = error.ToLowerInvariant();
                if (lower.Contains("cancel") || lower.Contains("declined") || lower.Contains("reject") || lower.Contains("biometric"))
                {
                    isCancel = true;
                }
            }

            if (!string.IsNullOrEmpty(errorCode))
            {
                if (errorCode == "-32600" || errorCode == "4001")
                {
                    isCancel = true;
                }
            }

            if (!string.IsNullOrEmpty(message))
            {
                string lower = message.ToLowerInvariant();
                if (lower.Contains("cancel") || lower.Contains("declined") || lower.Contains("reject") || lower.Contains("biometric"))
                {
                    isCancel = true;
                }
            }

            if (isCancel)
            {
                reason = !string.IsNullOrEmpty(message) ? message : (!string.IsNullOrEmpty(error) ? error : "Operation cancelled by user.");
                return true;
            }

            return false;
        }

        private static bool IsError(Dictionary<string, string> query, out string message)
        {
            message = null;

            string error = GetFirstValue(query, "error");
            string errorCode = GetFirstValue(query, "error_code", "code");
            string description = GetFirstValue(query, "error_message", "error_description", "message");

            if (!string.IsNullOrEmpty(error) || !string.IsNullOrEmpty(errorCode) || !string.IsNullOrEmpty(description))
            {
                if (!string.IsNullOrEmpty(description))
                {
                    message = description;
                }
                else if (!string.IsNullOrEmpty(error))
                {
                    message = error;
                }
                else
                {
                    message = $"Wallet returned error code: {errorCode}";
                }
                return true;
            }

            return false;
        }

        private static string GetFirstValue(Dictionary<string, string> query, params string[] keys)
        {
            foreach (string key in keys)
            {
                if (query.TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
            return null;
        }
    }
}
