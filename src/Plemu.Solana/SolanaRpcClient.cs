using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Plemu.Solana;

public class SolanaRpcClient : ISolanaRpcClient, IDisposable
{
    private const string JsonRpcVersion = "2.0";
    private const string JsonMediaType = "application/json";
    private const string EncodingBase64 = "base64";
    private const string EncodingJsonParsed = "jsonParsed";
    private const string CommitmentConfirmed = "confirmed";
    private const string DefaultDevnetUrl = "https://api.devnet.solana.com";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly ILogger<SolanaRpcClient>? _logger;

    public SolanaRpcClient(HttpClient httpClient, ILogger<SolanaRpcClient>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
        _ownsHttpClient = false;
    }

    public SolanaRpcClient(string rpcUrl = DefaultDevnetUrl, ILogger<SolanaRpcClient>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(rpcUrl))
        {
            throw new ArgumentException("RPC URL cannot be null or empty.", nameof(rpcUrl));
        }

        _httpClient = new HttpClient { BaseAddress = new Uri(rpcUrl) };
        _logger = logger;
        _ownsHttpClient = true;
    }

    public async Task<ulong> GetBalanceAsync(string pubkey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pubkey))
        {
            throw new ArgumentException("Public key cannot be null or empty.", nameof(pubkey));
        }

        var requestPayload = new
        {
            jsonrpc = JsonRpcVersion,
            id = 1,
            method = "getBalance",
            @params = new object[]
            {
                pubkey,
                new { commitment = CommitmentConfirmed }
            }
        };

        using var jsonDoc = await SendRpcRequestAsync(requestPayload, cancellationToken).ConfigureAwait(false);
        var root = jsonDoc.RootElement;

        CheckRpcError(root);

        if (!root.TryGetProperty("result", out var resultEl) || !resultEl.TryGetProperty("value", out var valueEl))
        {
            return 0UL;
        }

        if (valueEl.ValueKind == JsonValueKind.Number)
        {
            return valueEl.GetUInt64();
        }

        return 0UL;
    }

    public async Task<decimal> GetTokenBalanceAsync(string walletPubkey, string tokenMint, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(walletPubkey))
        {
            throw new ArgumentException("Wallet public key cannot be null or empty.", nameof(walletPubkey));
        }

        if (string.IsNullOrWhiteSpace(tokenMint))
        {
            throw new ArgumentException("Mint public key cannot be null or empty.", nameof(tokenMint));
        }

        var requestPayload = new
        {
            jsonrpc = JsonRpcVersion,
            id = 1,
            method = "getTokenAccountsByOwner",
            @params = new object[]
            {
                walletPubkey,
                new { mint = tokenMint },
                new { encoding = EncodingJsonParsed, commitment = CommitmentConfirmed }
            }
        };

        using var jsonDoc = await SendRpcRequestAsync(requestPayload, cancellationToken).ConfigureAwait(false);
        var root = jsonDoc.RootElement;

        CheckRpcError(root);

        if (!root.TryGetProperty("result", out var resultEl) || !resultEl.TryGetProperty("value", out var valueEl))
        {
            return 0.0m;
        }

        if (valueEl.ValueKind != JsonValueKind.Array)
        {
            return 0.0m;
        }

        decimal totalBalance = 0.0m;
        foreach (var item in valueEl.EnumerateArray())
        {
            if (item.TryGetProperty("account", out var accountEl) &&
                accountEl.TryGetProperty("data", out var dataEl) &&
                dataEl.TryGetProperty("parsed", out var parsedEl) &&
                parsedEl.TryGetProperty("info", out var infoEl) &&
                infoEl.TryGetProperty("tokenAmount", out var tokenAmountEl))
            {
                if (tokenAmountEl.TryGetProperty("uiAmountString", out var uiAmountStrEl) &&
                    decimal.TryParse(uiAmountStrEl.GetString(), CultureInfo.InvariantCulture, out var parsedBalance))
                {
                    totalBalance += parsedBalance;
                }
                else if (tokenAmountEl.TryGetProperty("uiAmount", out var uiAmountEl) &&
                         uiAmountEl.ValueKind == JsonValueKind.Number)
                {
                    totalBalance += (decimal)uiAmountEl.GetDouble();
                }
            }
        }

        return totalBalance;
    }

    public async Task<SolanaAccountInfo?> GetAccountInfoAsync(string pubkey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pubkey))
        {
            throw new ArgumentException("Public key cannot be null or empty.", nameof(pubkey));
        }

        var requestPayload = new
        {
            jsonrpc = JsonRpcVersion,
            id = 1,
            method = "getAccountInfo",
            @params = new object[]
            {
                pubkey,
                new { encoding = EncodingBase64, commitment = CommitmentConfirmed }
            }
        };

        using var jsonDoc = await SendRpcRequestAsync(requestPayload, cancellationToken).ConfigureAwait(false);
        var root = jsonDoc.RootElement;

        CheckRpcError(root);

        if (!root.TryGetProperty("result", out var resultEl) || !resultEl.TryGetProperty("value", out var valueEl))
        {
            return null;
        }

        if (valueEl.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return ParseAccountInfo(valueEl);
    }

    public async Task<IReadOnlyList<SolanaAccountInfo?>?> GetMultipleAccountsAsync(IReadOnlyList<string> pubkeys, CancellationToken cancellationToken = default)
    {
        if (pubkeys == null || pubkeys.Count == 0)
        {
            return Array.Empty<SolanaAccountInfo?>();
        }

        var requestPayload = new
        {
            jsonrpc = JsonRpcVersion,
            id = 1,
            method = "getMultipleAccounts",
            @params = new object[]
            {
                pubkeys,
                new { encoding = EncodingBase64, commitment = CommitmentConfirmed }
            }
        };

        using var jsonDoc = await SendRpcRequestAsync(requestPayload, cancellationToken).ConfigureAwait(false);
        var root = jsonDoc.RootElement;

        CheckRpcError(root);

        if (!root.TryGetProperty("result", out var resultEl) || !resultEl.TryGetProperty("value", out var valueEl))
        {
            return null;
        }

        if (valueEl.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var list = new List<SolanaAccountInfo?>(valueEl.GetArrayLength());
        foreach (var element in valueEl.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                list.Add(null);
            }
            else
            {
                list.Add(ParseAccountInfo(element));
            }
        }

        return list;
    }

    private async Task<JsonDocument> SendRpcRequestAsync(object payload, CancellationToken cancellationToken)
    {
        string jsonBody = JsonSerializer.Serialize(payload);
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, string.Empty)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, JsonMediaType)
        };

        var response = await _httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static void CheckRpcError(JsonElement root)
    {
        if (root.TryGetProperty("error", out var errorEl))
        {
            int code = errorEl.TryGetProperty("code", out var codeEl) ? codeEl.GetInt32() : -1;
            string message = errorEl.TryGetProperty("message", out var msgEl) ? msgEl.GetString() ?? "Unknown RPC error" : "Unknown RPC error";
            throw new InvalidOperationException($"Solana RPC returned error {code}: {message}");
        }
    }

    private static SolanaAccountInfo ParseAccountInfo(JsonElement element)
    {
        string owner = element.GetProperty("owner").GetString() ?? string.Empty;
        ulong lamports = element.GetProperty("lamports").GetUInt64();

        bool executable = element.TryGetProperty("executable", out var execEl) && execEl.GetBoolean();
        ulong rentEpoch = element.TryGetProperty("rentEpoch", out var rentEl) ? rentEl.GetUInt64() : 0;

        byte[] data = Array.Empty<byte>();
        if (element.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
        {
            string? base64 = dataEl[0].GetString();
            if (!string.IsNullOrEmpty(base64))
            {
                data = Convert.FromBase64String(base64);
            }
        }

        return new SolanaAccountInfo(owner, data, lamports, executable, rentEpoch);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
