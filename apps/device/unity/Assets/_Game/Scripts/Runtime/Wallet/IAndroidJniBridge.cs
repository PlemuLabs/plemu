using System.Threading;
using System.Threading.Tasks;

namespace Plemu.Wallet
{
    public interface IAndroidJniBridge
    {
        bool IsIntentAvailable(string schemeUri);

        Task<MwaConnectionResult> LaunchAssociateIntentAsync(
            string uri,
            string dappName,
            string dappUri,
            string dappIcon,
            string cluster,
            CancellationToken cancellationToken);

        Task<MwaTransactionResult> LaunchTransactionIntentAsync(
            string uri,
            string authToken,
            byte[] payload,
            CancellationToken cancellationToken);
    }
}
