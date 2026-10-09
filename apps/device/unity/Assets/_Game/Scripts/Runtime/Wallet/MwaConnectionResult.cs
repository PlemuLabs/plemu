namespace Plemu.Wallet
{
    /// <summary>
    /// Immutable result representing the outcome of an MWA authorization connection attempt.
    /// </summary>
    public sealed class MwaConnectionResult
    {
        public bool IsSuccess { get; }
        public string PublicKey { get; }
        public string AuthToken { get; }
        public string ErrorMessage { get; }
        public bool IsCancelled { get; }

        private MwaConnectionResult(bool isSuccess, string publicKey, string authToken, string errorMessage, bool isCancelled)
        {
            IsSuccess = isSuccess;
            PublicKey = publicKey;
            AuthToken = authToken;
            ErrorMessage = errorMessage;
            IsCancelled = isCancelled;
        }

        public static MwaConnectionResult Ok(string publicKey, string authToken)
        {
            return new MwaConnectionResult(true, publicKey, authToken, null, false);
        }

        public static MwaConnectionResult Failed(string errorMessage)
        {
            return new MwaConnectionResult(false, null, null, errorMessage, false);
        }

        public static MwaConnectionResult Cancelled(string reason = "User cancelled authorization")
        {
            return new MwaConnectionResult(false, null, null, reason, true);
        }

        public override string ToString()
        {
            if (IsSuccess)
            {
                return $"[MwaConnectionResult: IsSuccess=True, PublicKey={PublicKey}, AuthToken=[REDACTED]]";
            }
            if (IsCancelled)
            {
                return $"[MwaConnectionResult: IsSuccess=False, IsCancelled=True, Error={ErrorMessage}]";
            }
            return $"[MwaConnectionResult: IsSuccess=False, Error={ErrorMessage}]";
        }
    }
}
