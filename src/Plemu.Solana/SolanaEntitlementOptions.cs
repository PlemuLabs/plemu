namespace Plemu.Solana;

public class SolanaEntitlementOptions
{
    public const string DefaultProgramId = "11111111111111111111111111111111";
    public const string DefaultRpcUrl = "https://api.devnet.solana.com";

    public string ProgramId { get; set; } = DefaultProgramId;
    public string RpcUrl { get; set; } = DefaultRpcUrl;
    public string? FallbackRpcUrl { get; set; }
}
