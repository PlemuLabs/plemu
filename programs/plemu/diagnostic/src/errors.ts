export type DevnetErrorCode =
  | 'InvalidArguments' | 'InvalidEndpoint' | 'WrongCluster' | 'RpcUnavailable'
  | 'WalletMissing' | 'WalletExists' | 'UnsafeWalletFile' | 'InvalidWallet'
  | 'SignatureInvalid' | 'InsufficientFunds' | 'TransactionFailed'
  | 'ConfirmationTimeout' | 'TransactionExpired' | 'AirdropUnavailable';

export class DevnetError extends Error {
  constructor(readonly code: DevnetErrorCode, message: string) {
    super(message);
    this.name = 'DevnetError';
  }
}

export function publicError(error: unknown) {
  return error instanceof DevnetError
    ? { error: error.code, message: error.message }
    : { error: 'UnexpectedFailure', message: 'Operation failed. No credentials or paths were logged.' };
}
