using System;
using System.Collections.Generic;
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
