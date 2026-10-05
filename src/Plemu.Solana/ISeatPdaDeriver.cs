namespace Plemu.Solana;

public interface ISeatPdaDeriver
{
    string DeriveSeatAddress(string programId, string packPubkey, string walletPubkey);
}
