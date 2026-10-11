using System;
using System.Threading;
using System.Threading.Tasks;

namespace Plemu.Wallet.Mock
{
    /// <summary>
    /// Deterministic, offline mock blockhash provider for testing wallet transaction compilation.
    /// </summary>
    public sealed class MockBlockhashProvider : IBlockhashProvider
    {
        public const string DefaultMockBlockhash = "4vJ9JU1bJJE96FCSDJvffWN8iCG3qUCo33pPtxcLd4FX";

        public string RecentBlockhash { get; set; }
        public bool ThrowOnFetch { get; set; }
        public string ErrorMessage { get; set; } = "Mock RPC network failure";

        public MockBlockhashProvider(string recentBlockhash = DefaultMockBlockhash)
        {
            RecentBlockhash = recentBlockhash;
        }

        public Task<string> GetRecentBlockhashAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<string>(cancellationToken);
            }

            if (ThrowOnFetch)
            {
                throw new InvalidOperationException(ErrorMessage);
            }

            return Task.FromResult(RecentBlockhash);
        }
    }
}
