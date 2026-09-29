using System.Threading.Tasks;

namespace Plemu.Wallet.Mock
{
    /// <summary>
    /// Deterministic mock wallet for editor and stream A testing without requiring live Solana chain connection.
    /// </summary>
    public sealed class MockWallet : IWallet
    {
        private bool _isConnected;
        public bool IsConnected => _isConnected;
        public string PublicKey => _isConnected ? "MockWa11etDoNotUse11111111111111111111111111" : null;

        public Task<bool> Connect()
        {
            _isConnected = true;
            return Task.FromResult(true);
        }

        public void Disconnect()
        {
            _isConnected = false;
        }

        public Task<string> SendTip(string recipientWallet, ulong amountBaseUnits) => Sig("tip");

        public Task<string> PurchaseSeat(string packId, ulong maxPriceBaseUnits) => Sig("seat");

        public Task<string> PurchaseListing(string entryId, ulong maxPriceBaseUnits) => Sig("listing");

        private static Task<string> Sig(string kind) =>
            Task.FromResult($"MockSig-{kind}-1111111111111111111111111111");
    }
}
