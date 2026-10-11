using System;
using System.Collections.Generic;

namespace Plemu.Wallet
{
    /// <summary>
    /// Program instructions and address derivation for SPL Token and Associated Token Accounts.
    /// </summary>
    public static class SplTokenProgramInstructions
    {
        public const string TokenProgramId = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";
        public const string AssociatedTokenProgramId = "ATokenGPvbdGVxr1b2hvZbsiqW5xWH25efTNsLJA8knL";
        public const string SystemProgramId = "11111111111111111111111111111111";
        public const string PlemuMintAddress = "xNTPbgKARXhiC2Nn5FRiLdkv2SpD2tViiSEhW2qWruu";
        public const byte PlemuDecimals = 6;

        /// <summary>
        /// Derives the Associated Token Account (ATA) PDA for a given wallet owner and token mint.
        /// </summary>
        public static string DeriveAssociatedTokenAccount(
            string walletPublicKey,
            string mintPublicKey = PlemuMintAddress,
            string tokenProgramId = TokenProgramId,
            string associatedTokenProgramId = AssociatedTokenProgramId)
        {
            if (string.IsNullOrWhiteSpace(walletPublicKey))
            {
                throw new ArgumentException("Wallet public key cannot be null or empty.", nameof(walletPublicKey));
            }

            if (string.IsNullOrWhiteSpace(mintPublicKey))
            {
                throw new ArgumentException("Mint public key cannot be null or empty.", nameof(mintPublicKey));
            }

            if (string.IsNullOrWhiteSpace(tokenProgramId))
            {
                throw new ArgumentException("Token program ID cannot be null or empty.", nameof(tokenProgramId));
            }

            if (string.IsNullOrWhiteSpace(associatedTokenProgramId))
            {
                throw new ArgumentException("Associated token program ID cannot be null or empty.", nameof(associatedTokenProgramId));
            }

            byte[] walletBytes = NormalizeKey(walletPublicKey, nameof(walletPublicKey));
            byte[] tokenProgramBytes = NormalizeKey(tokenProgramId, nameof(tokenProgramId));
            byte[] mintBytes = NormalizeKey(mintPublicKey, nameof(mintPublicKey));

            byte[][] seeds = new byte[][]
            {
                walletBytes,
                tokenProgramBytes,
                mintBytes
            };

            return SolanaPda.FindProgramAddress(seeds, associatedTokenProgramId, out _);
        }

        /// <summary>
        /// Constructs an SPL Associated Token Account CreateIdempotent instruction.
        /// Ensures the recipient's ATA exists without failing if it was already created.
        /// Accounts layout: [fundingAccount (signer, writable), associatedTokenAccount (writable), walletAddress (readonly), tokenMintAddress (readonly), systemProgram (readonly), tokenProgram (readonly)].
        /// </summary>
        public static SolanaInstruction CreateAssociatedTokenAccountIdempotent(
            string fundingAccount,
            string associatedTokenAccount,
            string walletAddress,
            string tokenMintAddress,
            string systemProgramId = SystemProgramId,
            string tokenProgramId = TokenProgramId,
            string associatedTokenProgramId = AssociatedTokenProgramId)
        {
            if (string.IsNullOrWhiteSpace(fundingAccount))
            {
                throw new ArgumentException("Funding account cannot be null or empty.", nameof(fundingAccount));
            }

            if (string.IsNullOrWhiteSpace(associatedTokenAccount))
            {
                throw new ArgumentException("Associated token account cannot be null or empty.", nameof(associatedTokenAccount));
            }

            if (string.IsNullOrWhiteSpace(walletAddress))
            {
                throw new ArgumentException("Wallet address cannot be null or empty.", nameof(walletAddress));
            }

            if (string.IsNullOrWhiteSpace(tokenMintAddress))
            {
                throw new ArgumentException("Token mint address cannot be null or empty.", nameof(tokenMintAddress));
            }

            if (string.IsNullOrWhiteSpace(systemProgramId))
            {
                throw new ArgumentException("System program ID cannot be null or empty.", nameof(systemProgramId));
            }

            if (string.IsNullOrWhiteSpace(tokenProgramId))
            {
                throw new ArgumentException("Token program ID cannot be null or empty.", nameof(tokenProgramId));
            }

            if (string.IsNullOrWhiteSpace(associatedTokenProgramId))
            {
                throw new ArgumentException("Associated token program ID cannot be null or empty.", nameof(associatedTokenProgramId));
            }

            var keys = new List<SolanaAccountMeta>
            {
                new SolanaAccountMeta(fundingAccount, isSigner: true, isWritable: true),
                new SolanaAccountMeta(associatedTokenAccount, isSigner: false, isWritable: true),
                new SolanaAccountMeta(walletAddress, isSigner: false, isWritable: false),
                new SolanaAccountMeta(tokenMintAddress, isSigner: false, isWritable: false),
                new SolanaAccountMeta(systemProgramId, isSigner: false, isWritable: false),
                new SolanaAccountMeta(tokenProgramId, isSigner: false, isWritable: false)
            };

            byte[] data = new byte[] { 1 }; // Opcode 1: CreateIdempotent in Associated Token Account program

            return new SolanaInstruction(associatedTokenProgramId, keys, data);
        }

        /// <summary>
        /// Constructs an SPL Token TransferChecked instruction (Instruction index 12).
        /// Required when transferring tokens with explicit decimal validation.
        /// Accounts layout: [sourceAta (writable), mint (readonly), destinationAta (writable), owner (signer)].
        /// </summary>
        public static SolanaInstruction TransferChecked(
            string sourceAta,
            string mint,
            string destinationAta,
            string owner,
            ulong amount,
            byte decimals = PlemuDecimals,
            string tokenProgramId = TokenProgramId)
        {
            if (string.IsNullOrWhiteSpace(sourceAta))
            {
                throw new ArgumentException("Source ATA cannot be null or empty.", nameof(sourceAta));
            }

            if (string.IsNullOrWhiteSpace(mint))
            {
                throw new ArgumentException("Mint cannot be null or empty.", nameof(mint));
            }

            if (string.IsNullOrWhiteSpace(destinationAta))
            {
                throw new ArgumentException("Destination ATA cannot be null or empty.", nameof(destinationAta));
            }

            if (string.IsNullOrWhiteSpace(owner))
            {
                throw new ArgumentException("Owner public key cannot be null or empty.", nameof(owner));
            }

            if (string.IsNullOrWhiteSpace(tokenProgramId))
            {
                throw new ArgumentException("Token program ID cannot be null or empty.", nameof(tokenProgramId));
            }

            var keys = new List<SolanaAccountMeta>
            {
                new SolanaAccountMeta(sourceAta, isSigner: false, isWritable: true),
                new SolanaAccountMeta(mint, isSigner: false, isWritable: false),
                new SolanaAccountMeta(destinationAta, isSigner: false, isWritable: true),
                new SolanaAccountMeta(owner, isSigner: true, isWritable: false)
            };

            byte[] data = new byte[10];
            data[0] = 12; // TransferChecked opcode
            BitConverter.GetBytes(amount).CopyTo(data, 1);
            data[9] = decimals;

            return new SolanaInstruction(tokenProgramId, keys, data);
        }

        /// <summary>
        /// Constructs an SPL Token Transfer instruction (Instruction index 3).
        /// Accounts layout: [sourceAta (writable), destinationAta (writable), owner (signer)].
        /// </summary>
        public static SolanaInstruction Transfer(
            string sourceAta,
            string destinationAta,
            string owner,
            ulong amount,
            string tokenProgramId = TokenProgramId)
        {
            if (string.IsNullOrWhiteSpace(sourceAta))
            {
                throw new ArgumentException("Source ATA cannot be null or empty.", nameof(sourceAta));
            }

            if (string.IsNullOrWhiteSpace(destinationAta))
            {
                throw new ArgumentException("Destination ATA cannot be null or empty.", nameof(destinationAta));
            }

            if (string.IsNullOrWhiteSpace(owner))
            {
                throw new ArgumentException("Owner public key cannot be null or empty.", nameof(owner));
            }

            if (string.IsNullOrWhiteSpace(tokenProgramId))
            {
                throw new ArgumentException("Token program ID cannot be null or empty.", nameof(tokenProgramId));
            }

            var keys = new List<SolanaAccountMeta>
            {
                new SolanaAccountMeta(sourceAta, isSigner: false, isWritable: true),
                new SolanaAccountMeta(destinationAta, isSigner: false, isWritable: true),
                new SolanaAccountMeta(owner, isSigner: true, isWritable: false)
            };

            byte[] data = new byte[9];
            data[0] = 3; // Transfer opcode
            BitConverter.GetBytes(amount).CopyTo(data, 1);

            return new SolanaInstruction(tokenProgramId, keys, data);
        }

        private static byte[] NormalizeKey(string base58Key, string paramName)
        {
            if (!SolanaBase58.TryDecode(base58Key, out byte[] decoded) || decoded == null || decoded.Length == 0)
            {
                throw new ArgumentException($"Parameter '{paramName}' is not a valid Base58 string.", paramName);
            }

            if (decoded.Length == 32)
            {
                return decoded;
            }

            if (decoded.Length < 32)
            {
                byte[] padded = new byte[32];
                Array.Copy(decoded, 0, padded, 32 - decoded.Length, decoded.Length);
                return padded;
            }

            throw new ArgumentException($"Parameter '{paramName}' decoded length {decoded.Length} exceeds 32 bytes.", paramName);
        }
    }
}
