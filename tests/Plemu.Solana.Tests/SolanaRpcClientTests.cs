using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Plemu.Solana.Tests;

public class SolanaRpcClientTests
{
    private class TestHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage>? ResponseFactory { get; set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            if (ResponseFactory != null)
            {
                return ResponseFactory(request);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task GetAccountInfoAsync_WhenAccountExists_ReturnsParsedAccountInfo()
    {
        var handler = new TestHttpMessageHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                        "jsonrpc": "2.0",
                        "result": {
                            "context": { "slot": 100 },
                            "value": {
                                "data": ["VGVzdERhdGE=", "base64"],
                                "executable": false,
                                "lamports": 5000,
                                "owner": "11111111111111111111111111111111",
                                "rentEpoch": 20
                            }
                        },
                        "id": 1
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8899") };
        var client = new SolanaRpcClient(httpClient);

        var info = await client.GetAccountInfoAsync("FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm");

        Assert.NotNull(info);
        Assert.Equal("11111111111111111111111111111111", info.Owner);
        Assert.Equal(Encoding.UTF8.GetBytes("TestData"), info.Data);
        Assert.Equal(5000UL, info.Lamports);
        Assert.False(info.Executable);
        Assert.Equal(20UL, info.RentEpoch);
        Assert.Contains("getAccountInfo", handler.LastRequestBody);
    }

    [Fact]
    public async Task GetAccountInfoAsync_WhenAccountNull_ReturnsNull()
    {
        var handler = new TestHttpMessageHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                        "jsonrpc": "2.0",
                        "result": {
                            "context": { "slot": 100 },
                            "value": null
                        },
                        "id": 1
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8899") };
        var client = new SolanaRpcClient(httpClient);

        var info = await client.GetAccountInfoAsync("FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm");

        Assert.Null(info);
    }

    [Fact]
    public async Task GetMultipleAccountsAsync_WhenAccountsExist_ReturnsListWithAccountsAndNulls()
    {
        var handler = new TestHttpMessageHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                        "jsonrpc": "2.0",
                        "result": {
                            "context": { "slot": 100 },
                            "value": [
                                {
                                    "data": ["VGVzdERhdGE=", "base64"],
                                    "executable": false,
                                    "lamports": 1234,
                                    "owner": "11111111111111111111111111111111",
                                    "rentEpoch": 10
                                },
                                null
                            ]
                        },
                        "id": 1
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8899") };
        var client = new SolanaRpcClient(httpClient);

        var accounts = await client.GetMultipleAccountsAsync(new[]
        {
            "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm",
            "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL"
        });

        Assert.NotNull(accounts);
        Assert.Equal(2, accounts.Count);
        Assert.NotNull(accounts[0]);
        Assert.Equal("11111111111111111111111111111111", accounts[0]!.Owner);
        Assert.Null(accounts[1]);
        Assert.Contains("getMultipleAccounts", handler.LastRequestBody);
    }

    [Fact]
    public async Task GetAccountInfoAsync_WhenRpcReturnsJsonRpcError_ThrowsInvalidOperationException()
    {
        var handler = new TestHttpMessageHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                        "jsonrpc": "2.0",
                        "error": {
                            "code": -32600,
                            "message": "Invalid Request"
                        },
                        "id": 1
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8899") };
        var client = new SolanaRpcClient(httpClient);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetAccountInfoAsync("FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm"));
    }

    [Fact]
    public async Task GetAccountInfoAsync_WhenHttp500_ThrowsHttpRequestException()
    {
        var handler = new TestHttpMessageHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Server Error")
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8899") };
        var client = new SolanaRpcClient(httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetAccountInfoAsync("FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm"));
    }

    [Fact]
    public async Task GetAccountInfoAsync_WhenMalformedJson_ThrowsJsonException()
    {
        var handler = new TestHttpMessageHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not a valid json", Encoding.UTF8, "application/json")
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8899") };
        var client = new SolanaRpcClient(httpClient);

        await Assert.ThrowsAnyAsync<JsonException>(() =>
            client.GetAccountInfoAsync("FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm"));
    }

    [Fact]
    public async Task GetAccountInfoAsync_NullOrEmptyPubkey_ThrowsArgumentException()
    {
        var client = new SolanaRpcClient("http://localhost:8899");

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetAccountInfoAsync(string.Empty));
        await Assert.ThrowsAsync<ArgumentException>(() => client.GetAccountInfoAsync(null!));
    }

    [Fact]
    public async Task GetAccountInfoAsync_WhenResultMissingValue_ReturnsNull()
    {
        var handler = new TestHttpMessageHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                        "jsonrpc": "2.0",
                        "result": {},
                        "id": 1
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8899") };
        var client = new SolanaRpcClient(httpClient);

        var info = await client.GetAccountInfoAsync("FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm");

        Assert.Null(info);
    }

    [Fact]
    public async Task GetMultipleAccountsAsync_WhenResultNotArray_ReturnsNull()
    {
        var handler = new TestHttpMessageHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                        "jsonrpc": "2.0",
                        "result": { "value": 12345 },
                        "id": 1
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8899") };
        var client = new SolanaRpcClient(httpClient);

        var accounts = await client.GetMultipleAccountsAsync(new[] { "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm" });

        Assert.Null(accounts);
    }

    [Fact]
    public async Task GetMultipleAccountsAsync_WhenResultMissingValue_ReturnsNull()
    {
        var handler = new TestHttpMessageHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                        "jsonrpc": "2.0",
                        "result": {},
                        "id": 1
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8899") };
        var client = new SolanaRpcClient(httpClient);

        var accounts = await client.GetMultipleAccountsAsync(new[] { "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm" });

        Assert.Null(accounts);
    }

    [Fact]
    public async Task GetMultipleAccountsAsync_NullOrEmptyList_ReturnsEmpty()
    {
        var client = new SolanaRpcClient("http://localhost:8899");

        var emptyList = await client.GetMultipleAccountsAsync(Array.Empty<string>());
        Assert.NotNull(emptyList);
        Assert.Empty(emptyList);

        var nullList = await client.GetMultipleAccountsAsync(null!);
        Assert.NotNull(nullList);
        Assert.Empty(nullList);
    }

    [Fact]
    public void Constructor_NullArguments_ThrowsExpectedExceptions()
    {
        Assert.Throws<ArgumentNullException>(() => new SolanaRpcClient(httpClient: null!));
        Assert.Throws<ArgumentException>(() => new SolanaRpcClient(rpcUrl: null!));
        Assert.Throws<ArgumentException>(() => new SolanaRpcClient(rpcUrl: "   "));
    }

    [Fact]
    public void Dispose_WhenOwned_DisposesSuccessfully()
    {
        var client = new SolanaRpcClient("http://localhost:8899");
        client.Dispose();
        // Multiple dispose should not throw
        client.Dispose();
    }
}
