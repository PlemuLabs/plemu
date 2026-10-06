using System;
using System.Collections.Generic;

namespace Plemu.Solana;

public class SolanaEntitlementOptions
{
    public const string DefaultProgramId = "11111111111111111111111111111111";
    public const string DefaultSystemProgramId = "11111111111111111111111111111111";
    public const string DefaultMarketProgramId = "11111111111111111111111111111111";
    public const string DefaultTokenProgramId = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";
    public const string DefaultAssociatedTokenProgramId = "ATokenGPvbdGVxr1b2hvZbsiqW5xWH25efTNsLJA8knL";
    public const string DefaultMetaplexCoreProgramId = "CoREENxT6tW1HoK8ypY1SxRMZTcVPm7R94rH4P8JaNx4";
    public const string DefaultRpcUrl = "https://api.devnet.solana.com";

    public string ProgramId { get; set; } = DefaultProgramId;
    public string MarketProgramId { get; set; } = DefaultMarketProgramId;
    public string MetaplexCoreProgramId { get; set; } = DefaultMetaplexCoreProgramId;
    public string RpcUrl { get; set; } = DefaultRpcUrl;
    public string? FallbackRpcUrl { get; set; }
    public IDictionary<string, string> EntryToAssetMap { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
