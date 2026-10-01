import { constants } from 'node:fs';
import { lstat, mkdir, open } from 'node:fs/promises';
import { dirname } from 'node:path';
import {
  createKeyPairSignerFromBytes, createSignableMessage, generateKeyPair,
  getBase58Decoder, verifySignature,
} from '@solana/kit';
import { DevnetError } from './errors.js';

async function checkDirectory(path: string) {
  const info = await lstat(dirname(path));
  if (!info.isDirectory() || info.isSymbolicLink()) {
    throw new DevnetError('UnsafeWalletFile', 'The test wallet directory must be a real directory.');
  }
}

export async function createWallet(path: string) {
  await mkdir(dirname(path), { recursive: true, mode: 0o700 });
  await checkDirectory(path);
  const keyPair = await generateKeyPair(true);
  const privateDer = new Uint8Array(await crypto.subtle.exportKey('pkcs8', keyPair.privateKey));
  const publicBytes = new Uint8Array(await crypto.subtle.exportKey('raw', keyPair.publicKey));
  const bytes = new Uint8Array([...privateDer.slice(-32), ...publicBytes]);
  const signer = await createKeyPairSignerFromBytes(bytes);
  let file;
  try {
    file = await open(path, constants.O_WRONLY | constants.O_CREAT | constants.O_EXCL, 0o600);
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'EEXIST') {
      throw new DevnetError('WalletExists', 'A test wallet already exists. It was not overwritten.');
    }
    throw new DevnetError('UnsafeWalletFile', 'Could not create the private test wallet file.');
  }
  try {
    await file.writeFile(JSON.stringify(Array.from(bytes)) + '\n');
  } finally {
    bytes.fill(0);
    privateDer.fill(0);
    await file.close();
  }
  return signer.address;
}

export async function loadWallet(path: string) {
  let file;
  try {
    await checkDirectory(path);
    file = await open(path, constants.O_RDONLY | constants.O_NOFOLLOW);
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'ENOENT') {
      throw new DevnetError('WalletMissing', 'Create the dedicated test wallet with wallet:create first.');
    }
    throw new DevnetError('UnsafeWalletFile', 'The private wallet file could not be opened safely.');
  }
  try {
    const info = await file.stat();
    if (!info.isFile() || (info.mode & 0o077) !== 0 || info.size > 1024) {
      throw new DevnetError('UnsafeWalletFile', 'The wallet must be a small regular file with owner-only permissions (0600).');
    }
    const values: unknown = JSON.parse(await file.readFile('utf8'));
    if (!Array.isArray(values) || values.length !== 64 ||
        !values.every(value => Number.isInteger(value) && value >= 0 && value <= 255)) {
      throw new DevnetError('InvalidWallet', 'The wallet must contain a 64-byte Solana keypair.');
    }
    const bytes = Uint8Array.from(values);
    try {
      return await createKeyPairSignerFromBytes(bytes);
    } finally {
      bytes.fill(0);
    }
  } catch (error) {
    if (error instanceof DevnetError) throw error;
    throw new DevnetError('InvalidWallet', 'The test wallet is malformed or its public key does not match its private key.');
  } finally {
    await file.close();
  }
}

export async function proveWallet(signer: Awaited<ReturnType<typeof loadWallet>>) {
  const message = createSignableMessage(
    `Plemu Devnet Diagnostic v1\n\nWallet: ${signer.address}\nNonce: ${crypto.randomUUID()}`,
  );
  const [signatures] = await signer.signMessages([message]);
  const signature = signatures?.[signer.address];
  if (!signature || !await verifySignature(signer.keyPair.publicKey, signature, message.content)) {
    throw new DevnetError('SignatureInvalid', 'The wallet signature could not be verified.');
  }
  return { address: signer.address, verified: true, signature: getBase58Decoder().decode(signature) };
}
