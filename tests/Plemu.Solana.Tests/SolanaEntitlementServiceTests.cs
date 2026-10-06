using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Plemu.Core.Abstractions;
using Plemu.Core.Models;
using Plemu.Solana.Cryptography;
using Xunit;

namespace Plemu.Solana.Tests;

public class SolanaEntitlementServiceTests
{
    private const string ProgramId = "11111111111111111111111111111111";
    private const string PackPubkey = "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm";
    private const string WalletPubkey = "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL";

    private class FakeRpcClient : ISolanaRpcClient
    {
        public SolanaAccountInfo? AccountInfoToReturn { get; set; }
        public IReadOnlyList<SolanaAccountInfo?>? MultipleAccountsToReturn { get; set; }
        public ulong BalanceToReturn { get; set; }
        public decimal TokenBalanceToReturn { get; set; }
        public Exception? ExceptionToThrow { get; set; }

        public Task<SolanaAccountInfo?> GetAccountInfoAsync(string pubkey, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow != null) throw ExceptionToThrow;
            return Task.FromResult(AccountInfoToReturn);
        }

        public Task<IReadOnlyList<SolanaAccountInfo?>?> GetMultipleAccountsAsync(IReadOnlyList<string> pubkeys, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow != null) throw ExceptionToThrow;
            return Task.FromResult(MultipleAccountsToReturn);
        }

        public Task<ulong> GetBalanceAsync(string pubkey, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow != null) throw ExceptionToThrow;
            return Task.FromResult(BalanceToReturn);
        }

        public Task<decimal> GetTokenBalanceAsync(string walletPubkey, string tokenMint, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow != null) throw ExceptionToThrow;
            return Task.FromResult(TokenBalanceToReturn);
        }
    }

    private class FakePdaDeriver : ISeatPdaDeriver, IReceiptPdaDeriver
    {
        public string PdaToReturn { get; set; } = "xNTPbgKARXhiC2Nn5FRiLdkv2SpD2tViiSEhW2qWruu";
        public Exception? ExceptionToThrow { get; set; }

        public string DeriveSeatAddress(string programId, string packPubkey, string walletPubkey)
        {
            if (ExceptionToThrow != null) throw ExceptionToThrow;
            return PdaToReturn;
        }

        public string DeriveReceiptAddress(string programId, byte[] artifactSha256, string walletPubkey)
        {
            if (ExceptionToThrow != null) throw ExceptionToThrow;
            return PdaToReturn;
        }

        public string DeriveReceiptAddress(string programId, string entrySha256Hex, string walletPubkey)
        {
            if (ExceptionToThrow != null) throw ExceptionToThrow;
            return PdaToReturn;
        }
    }

    private static (SolanaAccountInfo seat, SolanaAccountInfo pack) CreateValidAccounts(
        string programId = ProgramId,
        string packPubkey = PackPubkey,
        string walletPubkey = WalletPubkey,
        bool packRevoked = false)
    {
        byte[] packBytes = Base58.Decode(packPubkey);
        byte[] walletBytes = Base58.Decode(walletPubkey);

        byte[] seatData = new byte[80];
        // Discriminator: [90, 228, 22, 90, 162, 86, 173, 26]
        byte[] discriminator = SolanaEntitlementService.SeatDiscriminator;
        discriminator.CopyTo(seatData, 0);
        walletBytes.CopyTo(seatData, 8);
        packBytes.CopyTo(seatData, 40);

        byte[] packData = new byte[100];
        if (packRevoked)
        {
            packData[72] = 1;
        }

        var seat = new SolanaAccountInfo(programId, seatData, 10000);
        var pack = new SolanaAccountInfo(programId, packData, 10000);

        return (seat, pack);
    }

    private const string SampleArtifactSha256 = "70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd";

    private static SolanaAccountInfo CreateValidReceiptAccount(
        string programId = ProgramId,
        string sha256Hex = SampleArtifactSha256,
        string walletPubkey = WalletPubkey,
        bool isRevoked = false)
    {
        byte[] walletBytes = Base58.Decode(walletPubkey);
        byte[] sha256Bytes = Convert.FromHexString(sha256Hex);

        byte[] receiptData = new byte[91];
        byte[] discriminator = SolanaEntitlementService.ReceiptDiscriminator;
        discriminator.CopyTo(receiptData, 0);
        walletBytes.CopyTo(receiptData, 8);
        sha256Bytes.CopyTo(receiptData, 40);
        receiptData[89] = (byte)(isRevoked ? 1 : 0);
        receiptData[90] = 254; // bump

        return new SolanaAccountInfo(programId, receiptData, 1460000);
    }

    private static SolanaAccountInfo CreateValidMetaplexCoreAssetAccount(
        string programId = "CoREENxT6tW1HoK8ypY1SxRMZTcVPm7R94rH4P8JaNx4",
        string walletPubkey = WalletPubkey)
    {
        byte[] walletBytes = Base58.Decode(walletPubkey);
        byte[] data = new byte[33];
        data[0] = 1; // Key::AssetV1
        walletBytes.CopyTo(data, 1); // Owner pubkey at offset 1..32

        return new SolanaAccountInfo(programId, data, 1000000);
    }

    [Fact]
    public async Task CanAccessPackAsync_ValidSeatAndPack_ReturnsTrue()
    {
        var (seat, pack) = CreateValidAccounts();
        var fakeRpc = new FakeRpcClient
        {
            MultipleAccountsToReturn = new[] { seat, pack }
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessPackAsync(wallet, PackPubkey);

        Assert.True(canAccess);
    }

    [Fact]
    public async Task CanAccessPackAsync_SeatAccountNotFound_ReturnsFalse()
    {
        var (_, pack) = CreateValidAccounts();
        var fakeRpc = new FakeRpcClient
        {
            MultipleAccountsToReturn = new[] { (SolanaAccountInfo?)null, pack }
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessPackAsync(wallet, PackPubkey);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessPackAsync_PackAccountNotFound_ReturnsFalse()
    {
        var (seat, _) = CreateValidAccounts();
        var fakeRpc = new FakeRpcClient
        {
            MultipleAccountsToReturn = new[] { seat, (SolanaAccountInfo?)null }
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessPackAsync(wallet, PackPubkey);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessPackAsync_PackRevoked_ReturnsFalse()
    {
        var (seat, pack) = CreateValidAccounts(packRevoked: true);
        var fakeRpc = new FakeRpcClient
        {
            MultipleAccountsToReturn = new[] { seat, pack }
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessPackAsync(wallet, PackPubkey);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessPackAsync_SeatAccountDusted_WrongOwner_ReturnsFalse()
    {
        var (seat, pack) = CreateValidAccounts();
        var dustedSeat = seat with { Owner = "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm" }; // Different program owner

        var fakeRpc = new FakeRpcClient
        {
            MultipleAccountsToReturn = new[] { dustedSeat, pack }
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessPackAsync(wallet, PackPubkey);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessPackAsync_PackAccountWrongOwner_ReturnsFalse()
    {
        var (seat, pack) = CreateValidAccounts();
        var invalidPack = pack with { Owner = "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm" };

        var fakeRpc = new FakeRpcClient
        {
            MultipleAccountsToReturn = new[] { seat, invalidPack }
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessPackAsync(wallet, PackPubkey);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessPackAsync_SeatAccountDataTooShort_ReturnsFalse()
    {
        var (seat, pack) = CreateValidAccounts();
        var shortSeat = seat with { Data = new byte[71] };

        var fakeRpc = new FakeRpcClient
        {
            MultipleAccountsToReturn = new[] { shortSeat, pack }
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessPackAsync(wallet, PackPubkey);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessPackAsync_SeatAccountInvalidDiscriminator_ReturnsFalse()
    {
        var (seat, pack) = CreateValidAccounts();
        byte[] corruptedData = (byte[])seat.Data.Clone();
        corruptedData[0] ^= 0xFF; // Invalidate discriminator byte
        var corruptedSeat = seat with { Data = corruptedData };

        var fakeRpc = new FakeRpcClient
        {
            MultipleAccountsToReturn = new[] { corruptedSeat, pack }
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessPackAsync(wallet, PackPubkey);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessPackAsync_SeatAccountMemberMismatch_ReturnsFalse()
    {
        var (seat, pack) = CreateValidAccounts(walletPubkey: "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm"); // Mismatched member

        var fakeRpc = new FakeRpcClient
        {
            MultipleAccountsToReturn = new[] { seat, pack }
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessPackAsync(wallet, PackPubkey);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessPackAsync_SeatAccountPackMismatch_ReturnsFalse()
    {
        var (seat, pack) = CreateValidAccounts(packPubkey: "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL"); // Mismatched pack

        var fakeRpc = new FakeRpcClient
        {
            MultipleAccountsToReturn = new[] { seat, pack }
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessPackAsync(wallet, PackPubkey);

        Assert.False(canAccess);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData("invalid-base58-with-0-and-O-and-I-and-l-characters")]
    public async Task CanAccessPackAsync_InvalidPackId_ReturnsFalse(string? invalidPackId)
    {
        var (seat, pack) = CreateValidAccounts();
        var fakeRpc = new FakeRpcClient
        {
            MultipleAccountsToReturn = new[] { seat, pack }
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessPackAsync(wallet, invalidPackId!);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessPackAsync_NullOrInvalidWallet_ReturnsFalse()
    {
        var (seat, pack) = CreateValidAccounts();
        var fakeRpc = new FakeRpcClient
        {
            MultipleAccountsToReturn = new[] { seat, pack }
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        Assert.False(await service.CanAccessPackAsync(null!, PackPubkey));
        Assert.False(await service.CanAccessPackAsync(new WalletIdentity("short"), PackPubkey));
        Assert.False(await service.CanAccessPackAsync(new WalletIdentity("invalid-0-O-I-l-characters-in-address-key"), PackPubkey));
    }

    [Fact]
    public async Task CanAccessPackAsync_RpcThrowsHttpRequestException_FailsClosedReturnsFalse()
    {
        var fakeRpc = new FakeRpcClient
        {
            ExceptionToThrow = new HttpRequestException("HTTP 503 Service Unavailable")
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessPackAsync(wallet, PackPubkey);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessPackAsync_RpcThrowsTimeout_FailsClosedReturnsFalse()
    {
        var fakeRpc = new FakeRpcClient
        {
            ExceptionToThrow = new TaskCanceledException("Request timed out")
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessPackAsync(wallet, PackPubkey);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessPackAsync_RpcReturnsNullOrInsufficientAccounts_ReturnsFalse()
    {
        var fakeRpc = new FakeRpcClient
        {
            MultipleAccountsToReturn = null
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        Assert.False(await service.CanAccessPackAsync(wallet, PackPubkey));

        fakeRpc.MultipleAccountsToReturn = new SolanaAccountInfo?[] { null }; // only 1 account
        Assert.False(await service.CanAccessPackAsync(wallet, PackPubkey));
    }

    [Fact]
    public async Task CanAccessPackAsync_PdaDeriverThrows_ReturnsFalse()
    {
        var fakeRpc = new FakeRpcClient();
        var fakePda = new FakePdaDeriver
        {
            ExceptionToThrow = new InvalidOperationException("Failed to find valid PDA bump")
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            fakePda,
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessPackAsync(wallet, PackPubkey);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessPack_SyncConvenienceMethod_DelegatesSuccessfully()
    {
        var (seat, pack) = CreateValidAccounts();
        var fakeRpc = new FakeRpcClient
        {
            MultipleAccountsToReturn = new[] { seat, pack }
        };

        IEntitlementService service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessPack(wallet, PackPubkey);

        Assert.True(canAccess);
    }

    [Fact]
    public void SolanaEntitlementOptions_Properties_GetAndSetExpectedValues()
    {
        var options = new SolanaEntitlementOptions
        {
            ProgramId = "CustomProgram1111111111111111111111111111",
            RpcUrl = "https://custom.rpc.com",
            FallbackRpcUrl = "https://fallback.rpc.com"
        };

        Assert.Equal("CustomProgram1111111111111111111111111111", options.ProgramId);
        Assert.Equal("https://custom.rpc.com", options.RpcUrl);
        Assert.Equal("https://fallback.rpc.com", options.FallbackRpcUrl);
    }

    [Fact]
    public async Task CanAccessPackAsync_PackIdDecodesToNon32Bytes_ReturnsFalse()
    {
        // 32-character string that decodes to fewer than 32 bytes (leading '1's missing or small integer)
        string shortDecodedKey = new string('2', 32);

        var service = new SolanaEntitlementService(
            new FakeRpcClient(),
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool result = await service.CanAccessPackAsync(wallet, shortDecodedKey);

        Assert.False(result);
    }

    [Fact]
    public async Task CanAccessPackAsync_WalletDecodesToNon32Bytes_ReturnsFalse()
    {
        string shortDecodedKey = new string('2', 32);

        var service = new SolanaEntitlementService(
            new FakeRpcClient(),
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(shortDecodedKey);
        bool result = await service.CanAccessPackAsync(wallet, PackPubkey);

        Assert.False(result);
    }

    [Fact]
    public void Dispose_WhenNotOwningRpcClient_DoesNotThrow()
    {
        var service = new SolanaEntitlementService(
            new FakeRpcClient(),
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        service.Dispose();
        service.Dispose();
    }

    [Fact]
    public void DefaultConstructor_InitializesWorkingService()
    {
        using var service = new SolanaEntitlementService();
        Assert.NotNull(service);
    }

    [Fact]
    public async Task CanAccessPackAsync_WithMockHttpMessageHandler_EndToEndVerification()
    {
        // Real SolanaRpcClient and Real DefaultSeatPdaDeriver
        var (seat, pack) = CreateValidAccounts();
        string seatDataB64 = Convert.ToBase64String(seat.Data);
        string packDataB64 = Convert.ToBase64String(pack.Data);

        var handler = new TestHttpMessageHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""
                    {
                        "jsonrpc": "2.0",
                        "result": {
                            "context": { "slot": 555 },
                            "value": [
                                {
                                    "data": ["{{seatDataB64}}", "base64"],
                                    "executable": false,
                                    "lamports": 10000,
                                    "owner": "{{ProgramId}}",
                                    "rentEpoch": 0
                                },
                                {
                                    "data": ["{{packDataB64}}", "base64"],
                                    "executable": false,
                                    "lamports": 10000,
                                    "owner": "{{ProgramId}}",
                                    "rentEpoch": 0
                                }
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
        var rpcClient = new SolanaRpcClient(httpClient);

        using var service = new SolanaEntitlementService(
            rpcClient,
            DefaultSeatPdaDeriver.Instance,
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessPackAsync(wallet, PackPubkey);

        Assert.True(canAccess);
    }

    [Fact]
    public void ReceiptDiscriminator_MatchesAnchorHash()
    {
        byte[] expected = SHA256.HashData(Encoding.UTF8.GetBytes("account:Receipt"))[..8];
        Assert.Equal(expected, SolanaEntitlementService.ReceiptDiscriminator);
    }

    [Fact]
    public async Task CanAccessEntryAsync_ValidReceipt_ReturnsTrue()
    {
        var receipt = CreateValidReceiptAccount();
        var fakeRpc = new FakeRpcClient
        {
            AccountInfoToReturn = receipt
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessEntryAsync(wallet, SampleArtifactSha256);

        Assert.True(canAccess);
    }

    [Fact]
    public async Task CanAccessEntry_SyncConvenienceMethod_DelegatesSuccessfully()
    {
        var receipt = CreateValidReceiptAccount();
        var fakeRpc = new FakeRpcClient
        {
            AccountInfoToReturn = receipt
        };

        IEntitlementService service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessEntry(wallet, SampleArtifactSha256);

        Assert.True(canAccess);
    }

    [Fact]
    public async Task CanAccessEntryAsync_RevokedReceipt_ReturnsFalse()
    {
        var receipt = CreateValidReceiptAccount(isRevoked: true);
        var fakeRpc = new FakeRpcClient
        {
            AccountInfoToReturn = receipt
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessEntryAsync(wallet, SampleArtifactSha256);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessEntryAsync_ReceiptNotFound_ReturnsFalse()
    {
        var fakeRpc = new FakeRpcClient
        {
            AccountInfoToReturn = null
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessEntryAsync(wallet, SampleArtifactSha256);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessEntryAsync_ReceiptDusted_WrongOwner_ReturnsFalse()
    {
        var receipt = CreateValidReceiptAccount();
        var dustedReceipt = receipt with { Owner = "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm" };

        var fakeRpc = new FakeRpcClient
        {
            AccountInfoToReturn = dustedReceipt
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessEntryAsync(wallet, SampleArtifactSha256);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessEntryAsync_ReceiptDataTooShort_ReturnsFalse()
    {
        var receipt = CreateValidReceiptAccount();
        var shortReceipt = receipt with { Data = new byte[89] };

        var fakeRpc = new FakeRpcClient
        {
            AccountInfoToReturn = shortReceipt
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessEntryAsync(wallet, SampleArtifactSha256);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessEntryAsync_ReceiptInvalidDiscriminator_ReturnsFalse()
    {
        var receipt = CreateValidReceiptAccount();
        byte[] corruptedData = (byte[])receipt.Data.Clone();
        corruptedData[0] ^= 0xFF;
        var corruptedReceipt = receipt with { Data = corruptedData };

        var fakeRpc = new FakeRpcClient
        {
            AccountInfoToReturn = corruptedReceipt
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessEntryAsync(wallet, SampleArtifactSha256);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessEntryAsync_ReceiptBuyerMismatch_ReturnsFalse()
    {
        var receipt = CreateValidReceiptAccount(walletPubkey: "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm");

        var fakeRpc = new FakeRpcClient
        {
            AccountInfoToReturn = receipt
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessEntryAsync(wallet, SampleArtifactSha256);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessEntryAsync_ReceiptArtifactSha256Mismatch_ReturnsFalse()
    {
        var receipt = CreateValidReceiptAccount(sha256Hex: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        var fakeRpc = new FakeRpcClient
        {
            AccountInfoToReturn = receipt
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessEntryAsync(wallet, SampleArtifactSha256);

        Assert.False(canAccess);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("not-a-valid-hex-string-and-has-invalid-characters-1234567890123456")]
    [InlineData("70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948")]
    [InlineData("70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948ddaa")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public async Task CanAccessEntryAsync_InvalidEntrySha256_ReturnsFalse(string? invalidSha256)
    {
        var receipt = CreateValidReceiptAccount();
        var fakeRpc = new FakeRpcClient
        {
            AccountInfoToReturn = receipt
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessEntryAsync(wallet, invalidSha256!);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessEntryAsync_ValidBase58Sha256_Succeeds()
    {
        byte[] rawBytes = Convert.FromHexString(SampleArtifactSha256);
        string base58Sha = Base58.Encode(rawBytes);

        var receipt = CreateValidReceiptAccount();
        var fakeRpc = new FakeRpcClient
        {
            AccountInfoToReturn = receipt
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessEntryAsync(wallet, base58Sha);

        Assert.True(canAccess);
    }

    [Fact]
    public async Task CanAccessEntryAsync_ReceiptNotFound_WithMetaplexCoreAssetAccountNull_ReturnsFalse()
    {
        const string assetPubkey = "9xQeWvG816bUx9EPjHmaT23yvVM2ZWbrrpZb9PusVFin";

        var options = new SolanaEntitlementOptions
        {
            ProgramId = ProgramId,
            EntryToAssetMap = { [SampleArtifactSha256] = assetPubkey }
        };

        var multiRpc = new MockDictionaryRpcClient(new Dictionary<string, SolanaAccountInfo?>
        {
            ["receipt_pda"] = null,
            [assetPubkey] = null
        });

        var service = new SolanaEntitlementService(
            multiRpc,
            new FakePdaDeriver { PdaToReturn = "receipt_pda" },
            options);

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessEntryAsync(wallet, SampleArtifactSha256);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessEntryAsync_NullOrInvalidWallet_ReturnsFalse()
    {
        var service = new SolanaEntitlementService(
            new FakeRpcClient(),
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        Assert.False(await service.CanAccessEntryAsync(null!, SampleArtifactSha256));
        Assert.False(await service.CanAccessEntryAsync(new WalletIdentity("short"), SampleArtifactSha256));
        Assert.False(await service.CanAccessEntryAsync(new WalletIdentity("invalid-0-O-I-l-characters-in-address-key"), SampleArtifactSha256));
    }

    [Fact]
    public async Task CanAccessEntryAsync_WalletDecodesToNon32Bytes_ReturnsFalse()
    {
        string shortDecodedKey = new string('2', 32);

        var service = new SolanaEntitlementService(
            new FakeRpcClient(),
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(shortDecodedKey);
        bool result = await service.CanAccessEntryAsync(wallet, SampleArtifactSha256);

        Assert.False(result);
    }

    [Fact]
    public async Task CanAccessEntryAsync_RpcThrowsException_FailsClosedReturnsFalse()
    {
        var fakeRpc = new FakeRpcClient
        {
            ExceptionToThrow = new HttpRequestException("RPC timeout")
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            new FakePdaDeriver(),
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessEntryAsync(wallet, SampleArtifactSha256);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessEntryAsync_PdaDeriverThrows_ReturnsFalse()
    {
        var fakeRpc = new FakeRpcClient();
        var fakePda = new FakePdaDeriver
        {
            ExceptionToThrow = new InvalidOperationException("Failed to find valid PDA bump")
        };

        var service = new SolanaEntitlementService(
            fakeRpc,
            fakePda,
            new SolanaEntitlementOptions { ProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessEntryAsync(wallet, SampleArtifactSha256);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessEntryAsync_ReceiptNotFound_WithValidMetaplexCoreCartridge_ReturnsTrue()
    {
        const string assetPubkey = "9xQeWvG816bUx9EPjHmaT23yvVM2ZWbrrpZb9PusVFin";
        var metaplexAsset = CreateValidMetaplexCoreAssetAccount(walletPubkey: WalletPubkey);

        var options = new SolanaEntitlementOptions
        {
            ProgramId = ProgramId,
            EntryToAssetMap = { [SampleArtifactSha256] = assetPubkey }
        };

        var multiRpc = new MockDictionaryRpcClient(new Dictionary<string, SolanaAccountInfo?>
        {
            ["receipt_pda"] = null,
            [assetPubkey] = metaplexAsset
        });

        var fakePda = new FakePdaDeriver { PdaToReturn = "receipt_pda" };

        var service = new SolanaEntitlementService(
            multiRpc,
            fakePda,
            options);

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessEntryAsync(wallet, SampleArtifactSha256);

        Assert.True(canAccess);
    }

    [Fact]
    public async Task CanAccessEntryAsync_ReceiptNotFound_WithMetaplexCoreCartridgeWrongOwner_ReturnsFalse()
    {
        const string assetPubkey = "9xQeWvG816bUx9EPjHmaT23yvVM2ZWbrrpZb9PusVFin";
        var metaplexAsset = CreateValidMetaplexCoreAssetAccount(walletPubkey: "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm");

        var options = new SolanaEntitlementOptions
        {
            ProgramId = ProgramId,
            EntryToAssetMap = { [SampleArtifactSha256] = assetPubkey }
        };

        var multiRpc = new MockDictionaryRpcClient(new Dictionary<string, SolanaAccountInfo?>
        {
            ["receipt_pda"] = null,
            [assetPubkey] = metaplexAsset
        });

        var service = new SolanaEntitlementService(
            multiRpc,
            new FakePdaDeriver { PdaToReturn = "receipt_pda" },
            options);

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessEntryAsync(wallet, SampleArtifactSha256);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessEntryAsync_ReceiptNotFound_WithMetaplexCoreCartridgeWrongProgram_ReturnsFalse()
    {
        const string assetPubkey = "9xQeWvG816bUx9EPjHmaT23yvVM2ZWbrrpZb9PusVFin";
        var metaplexAsset = CreateValidMetaplexCoreAssetAccount(programId: "11111111111111111111111111111111");

        var options = new SolanaEntitlementOptions
        {
            ProgramId = ProgramId,
            EntryToAssetMap = { [SampleArtifactSha256] = assetPubkey }
        };

        var multiRpc = new MockDictionaryRpcClient(new Dictionary<string, SolanaAccountInfo?>
        {
            ["receipt_pda"] = null,
            [assetPubkey] = metaplexAsset
        });

        var service = new SolanaEntitlementService(
            multiRpc,
            new FakePdaDeriver { PdaToReturn = "receipt_pda" },
            options);

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessEntryAsync(wallet, SampleArtifactSha256);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessEntryAsync_ReceiptNotFound_WithMetaplexCoreCartridgeDataTooShort_ReturnsFalse()
    {
        const string assetPubkey = "9xQeWvG816bUx9EPjHmaT23yvVM2ZWbrrpZb9PusVFin";
        var metaplexAsset = new SolanaAccountInfo("CoREENxT6tW1HoK8ypY1SxRMZTcVPm7R94rH4P8JaNx4", new byte[32], 1000);

        var options = new SolanaEntitlementOptions
        {
            ProgramId = ProgramId,
            EntryToAssetMap = { [SampleArtifactSha256] = assetPubkey }
        };

        var multiRpc = new MockDictionaryRpcClient(new Dictionary<string, SolanaAccountInfo?>
        {
            ["receipt_pda"] = null,
            [assetPubkey] = metaplexAsset
        });

        var service = new SolanaEntitlementService(
            multiRpc,
            new FakePdaDeriver { PdaToReturn = "receipt_pda" },
            options);

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessEntryAsync(wallet, SampleArtifactSha256);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessEntryAsync_ReceiptNotFound_WithMetaplexCoreRpcException_ReturnsFalse()
    {
        const string assetPubkey = "9xQeWvG816bUx9EPjHmaT23yvVM2ZWbrrpZb9PusVFin";

        var options = new SolanaEntitlementOptions
        {
            ProgramId = ProgramId,
            EntryToAssetMap = { [SampleArtifactSha256] = assetPubkey }
        };

        var multiRpc = new MockDictionaryRpcClient(new Dictionary<string, SolanaAccountInfo?>
        {
            ["receipt_pda"] = null
        })
        {
            ExceptionOnPubkey = assetPubkey
        };

        var service = new SolanaEntitlementService(
            multiRpc,
            new FakePdaDeriver { PdaToReturn = "receipt_pda" },
            options);

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessEntryAsync(wallet, SampleArtifactSha256);

        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessEntryAsync_WithMockHttpMessageHandler_EndToEndVerification()
    {
        var receipt = CreateValidReceiptAccount();
        string receiptDataB64 = Convert.ToBase64String(receipt.Data);

        var handler = new TestHttpMessageHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""
                    {
                        "jsonrpc": "2.0",
                        "result": {
                            "context": { "slot": 777 },
                            "value": {
                                "data": ["{{receiptDataB64}}", "base64"],
                                "executable": false,
                                "lamports": 1460000,
                                "owner": "{{ProgramId}}",
                                "rentEpoch": 0
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
        var rpcClient = new SolanaRpcClient(httpClient);

        using var service = new SolanaEntitlementService(
            rpcClient,
            DefaultSeatPdaDeriver.Instance,
            new SolanaEntitlementOptions { ProgramId = ProgramId, MarketProgramId = ProgramId });

        var wallet = new WalletIdentity(WalletPubkey);
        bool canAccess = await service.CanAccessEntryAsync(wallet, SampleArtifactSha256);

        Assert.True(canAccess);
    }

    private class MockDictionaryRpcClient : ISolanaRpcClient
    {
        private readonly IDictionary<string, SolanaAccountInfo?> _accounts;
        public string? ExceptionOnPubkey { get; set; }

        public MockDictionaryRpcClient(IDictionary<string, SolanaAccountInfo?> accounts)
        {
            _accounts = accounts;
        }

        public Task<SolanaAccountInfo?> GetAccountInfoAsync(string pubkey, CancellationToken cancellationToken = default)
        {
            if (string.Equals(pubkey, ExceptionOnPubkey, StringComparison.OrdinalIgnoreCase))
            {
                throw new HttpRequestException("RPC network error on pubkey: " + pubkey);
            }
            _accounts.TryGetValue(pubkey, out var info);
            return Task.FromResult(info);
        }

        public Task<IReadOnlyList<SolanaAccountInfo?>?> GetMultipleAccountsAsync(IReadOnlyList<string> pubkeys, CancellationToken cancellationToken = default)
        {
            var list = new List<SolanaAccountInfo?>();
            foreach (var key in pubkeys)
            {
                _accounts.TryGetValue(key, out var info);
                list.Add(info);
            }
            return Task.FromResult<IReadOnlyList<SolanaAccountInfo?>?>(list);
        }

        public Task<ulong> GetBalanceAsync(string pubkey, CancellationToken cancellationToken = default) => Task.FromResult(0UL);
        public Task<decimal> GetTokenBalanceAsync(string walletPubkey, string tokenMint, CancellationToken cancellationToken = default) => Task.FromResult(0m);
    }

    private class TestHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage>? ResponseFactory { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(ResponseFactory?.Invoke(request) ?? new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
