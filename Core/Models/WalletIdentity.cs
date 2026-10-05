namespace Plemu.Core.Models;

public record WalletIdentity(string PublicKey, string? Label = null)
{
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(PublicKey) &&
        PublicKey.Length >= 32 &&
        PublicKey.Length <= 44;
}
