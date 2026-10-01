import { Connection, Keypair, LAMPORTS_PER_SOL, clusterApiUrl } from '@solana/web3.js';
import * as fs from 'fs';
import * as path from 'path';

async function main() {
  const homeDir = process.env.HOME || process.env.USERPROFILE || '';
  const secretsDir = path.join(homeDir, 'plemu-secrets');
  const keypairPath = process.env.SOLANA_DEPLOYER_PATH || path.join(secretsDir, 'plemu-deployer.json');

  if (fs.existsSync(keypairPath)) {
    console.log(`Deployer keypair already exists at: ${keypairPath}`);
    const raw = fs.readFileSync(keypairPath, 'utf-8');
    const secretKey = Uint8Array.from(JSON.parse(raw));
    const deployer = Keypair.fromSecretKey(secretKey);
    console.log(`Public Key: ${deployer.publicKey.toBase58()}`);

    const rpcUrl = process.env.SOLANA_DEVNET_RPC_URL || clusterApiUrl('devnet');
    const connection = new Connection(rpcUrl, 'confirmed');
    const balance = await connection.getBalance(deployer.publicKey);
    console.log(`Devnet Balance: ${(balance / LAMPORTS_PER_SOL).toFixed(4)} SOL`);
    return;
  }

  if (!fs.existsSync(secretsDir)) {
    fs.mkdirSync(secretsDir, { recursive: true });
  }

  const deployer = Keypair.generate();
  const secretKeyArray = Array.from(deployer.secretKey);

  fs.writeFileSync(keypairPath, JSON.stringify(secretKeyArray));
  console.log(`New deployer keypair generated and saved to: ${keypairPath}`);
  console.log(`Public Key: ${deployer.publicKey.toBase58()}`);

  const rpcUrl = process.env.SOLANA_DEVNET_RPC_URL || clusterApiUrl('devnet');
  console.log(`Connecting to Devnet at ${rpcUrl} for airdrop...`);
  const connection = new Connection(rpcUrl, 'confirmed');

  try {
    const airdropSig = await connection.requestAirdrop(deployer.publicKey, 2 * LAMPORTS_PER_SOL);
    console.log(`Airdrop requested: ${airdropSig}`);
    const latestBlockhash = await connection.getLatestBlockhash();
    await connection.confirmTransaction({
      signature: airdropSig,
      blockhash: latestBlockhash.blockhash,
      lastValidBlockHeight: latestBlockhash.lastValidBlockHeight,
    });
    console.log('Airdrop confirmed! Balance: 2.0 SOL');
  } catch (err: any) {
    console.warn(`Airdrop request encountered an issue (Devnet faucet may be rate-limited): ${err.message || err}`);
    console.warn(`You can manually fund this address on Devnet: ${deployer.publicKey.toBase58()}`);
  }
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
