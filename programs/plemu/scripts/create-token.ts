import { Connection, Keypair, PublicKey, clusterApiUrl } from '@solana/web3.js';
import { createMint, getOrCreateAssociatedTokenAccount, mintTo } from '@solana/spl-token';
import * as fs from 'fs';
import * as path from 'path';

function loadDeployerKeypair(): Keypair {
  // Option 1: Direct JSON string from environment variable (e.g. CI/CD secret)
  if (process.env.SOLANA_DEVNET_DEPLOYER_KEY) {
    try {
      const secretKey = Uint8Array.from(JSON.parse(process.env.SOLANA_DEVNET_DEPLOYER_KEY));
      return Keypair.fromSecretKey(secretKey);
    } catch (err) {
      console.error('Failed to parse SOLANA_DEVNET_DEPLOYER_KEY environment variable as JSON byte array.');
      process.exit(1);
    }
  }

  // Option 2: Path from environment or default secure directory (~/plemu-secrets/plemu-deployer.json)
  const homeDir = process.env.HOME || process.env.USERPROFILE || '';
  const keypairPath = process.env.SOLANA_DEPLOYER_PATH || path.join(homeDir, 'plemu-secrets', 'plemu-deployer.json');

  if (!fs.existsSync(keypairPath)) {
    console.error(`Deployer keypair not found at: ${keypairPath}`);
    console.error('Please generate one using: npm run generate-deployer');
    console.error('Or provide SOLANA_DEVNET_DEPLOYER_KEY in environment.');
    process.exit(1);
  }

  try {
    const raw = fs.readFileSync(keypairPath, 'utf-8');
    const secretKey = Uint8Array.from(JSON.parse(raw));
    return Keypair.fromSecretKey(secretKey);
  } catch (err) {
    console.error(`Failed to read keypair from ${keypairPath}:`, err);
    process.exit(1);
  }
}

async function main() {
  const rpcUrl = process.env.SOLANA_DEVNET_RPC_URL || clusterApiUrl('devnet');
  console.log(`Connecting to Solana Devnet RPC: ${rpcUrl}`);
  const connection = new Connection(rpcUrl, 'confirmed');

  const deployer = loadDeployerKeypair();
  console.log(`Deployer Public Key: ${deployer.publicKey.toBase58()}`);

  const balanceLamports = await connection.getBalance(deployer.publicKey);
  const balanceSol = balanceLamports / 1e9;
  console.log(`Deployer balance: ${balanceSol.toFixed(4)} SOL`);

  if (balanceLamports < 0.05 * 1e9) {
    console.warn('Warning: Deployer has less than 0.05 SOL. Mint creation may fail due to insufficient rent/fees.');
    console.warn(`Request a devnet airdrop for: ${deployer.publicKey.toBase58()}`);
  }

  const addressesFile = path.join(__dirname, '..', 'devnet-addresses.json');
  let addresses: Record<string, any> = {};
  if (fs.existsSync(addressesFile)) {
    try {
      addresses = JSON.parse(fs.readFileSync(addressesFile, 'utf-8'));
    } catch {
      addresses = {};
    }
  }

  if (addresses.plemuMint) {
    console.log(`$PLEMU mint already exists at: ${addresses.plemuMint}`);
    console.log(`Associated Token Account: ${addresses.plemuDeployerAta || 'unknown'}`);
    return;
  }

  console.log('Creating SPL Classic Mint for $PLEMU (6 decimals)...');
  const decimals = 6;
  const mint = await createMint(
    connection,
    deployer,
    deployer.publicKey,
    deployer.publicKey,
    decimals
  );

  const mintPubkey = mint.toBase58();
  console.log(`$PLEMU Mint created successfully: ${mintPubkey}`);

  // Create initial token account for treasury/deployer and mint 1,000,000,000 tokens
  console.log('Resolving Associated Token Account for deployer/treasury...');
  const tokenAccount = await getOrCreateAssociatedTokenAccount(
    connection,
    deployer,
    mint,
    deployer.publicKey
  );

  const tokenAccountPubkey = tokenAccount.address.toBase58();
  console.log(`Deployer ATA: ${tokenAccountPubkey}`);

  const initialSupplyTokens = 1_000_000_000n;
  const initialSupplyUnits = initialSupplyTokens * BigInt(10 ** decimals); // 1,000,000,000 * 10^6
  console.log(`Minting 1,000,000,000 $PLEMU (initial supply) to ${tokenAccountPubkey}...`);

  await mintTo(
    connection,
    deployer,
    mint,
    tokenAccount.address,
    deployer,
    initialSupplyUnits
  );

  addresses.network = 'devnet';
  addresses.plemuMint = mintPubkey;
  addresses.plemuDeployerAta = tokenAccountPubkey;
  addresses.deployerWallet = deployer.publicKey.toBase58();
  addresses.decimals = decimals;
  addresses.initialSupply = initialSupplyTokens.toString();
  addresses.mintAuthority = deployer.publicKey.toBase58();
  addresses.createdAt = new Date().toISOString();

  fs.writeFileSync(addressesFile, JSON.stringify(addresses, null, 2) + '\n');
  console.log(`Saved deployment records to ${addressesFile}`);
  console.log('Done.');
}

main().catch(err => {
  console.error('Error during token creation:', err);
  process.exit(1);
});
