namespace Plemu.Solana;

public interface IReceiptPdaDeriver
{
    string DeriveReceiptAddress(string programId, byte[] artifactSha256, string walletPubkey);
    string DeriveReceiptAddress(string programId, string entrySha256Hex, string walletPubkey);
}
