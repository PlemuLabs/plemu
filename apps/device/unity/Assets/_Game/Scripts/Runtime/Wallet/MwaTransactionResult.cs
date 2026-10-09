namespace Plemu.Wallet
{
    /// <summary>
    /// Immutable result representing the outcome of an MWA transaction signing and submission.
    /// </summary>
    public sealed class MwaTransactionResult
    {
        public bool IsSuccess { get; }
        public string Signature { get; }
        public string ErrorMessage { get; }
        public bool IsCancelled { get; }

        private MwaTransactionResult(bool isSuccess, string signature, string errorMessage, bool isCancelled)
        {
            IsSuccess = isSuccess;
            Signature = signature;
            ErrorMessage = errorMessage;
            IsCancelled = isCancelled;
        }

        public static MwaTransactionResult Ok(string signature)
        {
            return new MwaTransactionResult(true, signature, null, false);
        }

        public static MwaTransactionResult Failed(string errorMessage)
        {
            return new MwaTransactionResult(false, null, errorMessage, false);
        }

        public static MwaTransactionResult Cancelled(string reason = "User cancelled signing")
        {
            return new MwaTransactionResult(false, null, reason, true);
        }

        public override string ToString()
        {
            if (IsSuccess)
            {
                return $"[MwaTransactionResult: IsSuccess=True, Signature={Signature}]";
            }
            if (IsCancelled)
            {
                return $"[MwaTransactionResult: IsSuccess=False, IsCancelled=True, Error={ErrorMessage}]";
            }
            return $"[MwaTransactionResult: IsSuccess=False, Error={ErrorMessage}]";
        }
    }
}
