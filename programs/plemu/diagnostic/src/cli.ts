import { fileURLToPath } from 'node:url';
import { DevnetClient } from './devnet.js';
import { DevnetError, publicError } from './errors.js';
import { createWallet, loadWallet, proveWallet } from './wallet.js';

const walletPath = fileURLToPath(new URL('../../../../../../dev-secrets/solana-devnet-wallet.json', import.meta.url));
const output = (value: unknown) => process.stdout.write(JSON.stringify(value) + '\n');
const submitted = (signature: string) => output({
  status: 'pending', signature, explorer: `https://explorer.solana.com/tx/${signature}?cluster=devnet`,
});

async function main() {
  const [command = 'help', ...rest] = process.argv.slice(2);
  if (rest.length) throw new DevnetError('InvalidArguments', 'Commands do not accept additional arguments. Run help for usage.');
  if (command === 'help') {
    output({ commands: ['wallet:create', 'wallet:address', 'wallet:prove', 'status', 'airdrop', 'memo'],
      network: 'devnet only', note: 'airdrop requests 1 test SOL; memo submits a fixed diagnostic transaction using test SOL.' });
    return;
  }
  if (!['wallet:create', 'wallet:address', 'wallet:prove', 'status', 'airdrop', 'memo'].includes(command)) {
    throw new DevnetError('InvalidArguments', 'Unknown command. Run help for usage.');
  }
  if (command === 'wallet:create') {
    output({ address: await createWallet(walletPath), created: true });
    return;
  }
  const signer = await loadWallet(walletPath);
  if (command === 'wallet:address') { output({ address: signer.address }); return; }
  if (command === 'wallet:prove') { output(await proveWallet(signer)); return; }
  const client = DevnetClient.connect(process.env.PLEMU_DEVNET_RPC_URL);
  if (command === 'status') output(await client.status(signer.address));
  if (command === 'airdrop') output({ status: 'confirmed', signature: await client.airdrop(signer.address, submitted) });
  if (command === 'memo') output({ status: 'confirmed', signature: await client.memo(signer, submitted) });
}

main().catch(error => {
  process.stderr.write(JSON.stringify(publicError(error)) + '\n');
  process.exitCode = 1;
});
