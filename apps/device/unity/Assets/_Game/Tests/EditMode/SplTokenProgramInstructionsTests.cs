using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Plemu.Wallet;
using Plemu.Wallet.Mock;

namespace Plemu.Tests
{
    [TestFixture]
    public class SplTokenProgramInstructionsTests
    {
        private const string TestDeployerWallet = "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm";
        private const string TestDeployerAta = "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL";
        private const string TestPlemuMint = "xNTPbgKARXhiC2Nn5FRiLdkv2SpD2tViiSEhW2qWruu";
        private const string TestRecipientWallet = "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL";

        private class CapturingMwaAdapter : IMobileWalletAdapter
        {
            public bool IsAvailable => true;
            public byte[] LastSerializedTransaction { get; private set; }

            public bool IsMwaAvailable() => IsAvailable;

            public Task<MwaConnectionResult> ConnectAsync(System.Threading.CancellationToken cancellationToken = default)
            {
                return Task.FromResult(MwaConnectionResult.Ok(TestDeployerWallet, "auth-token-test"));
            }

            public Task<MwaTransactionResult> SignAndSendTransactionAsync(byte[] serializedTransaction, System.Threading.CancellationToken cancellationToken = default)
            {
                LastSerializedTransaction = serializedTransaction;
                return Task.FromResult(MwaTransactionResult.Ok("MwaSig-devnet-spl-test"));
            }

            public Task DisconnectAsync()
            {
                return Task.CompletedTask;
            }
        }

        [Test]
        public void DeriveAssociatedTokenAccount_MatchesOfficialDevnetDeployerAta()
        {
            string ata = SplTokenProgramInstructions.DeriveAssociatedTokenAccount(TestDeployerWallet, TestPlemuMint);
            Assert.AreEqual(TestDeployerAta, ata);
        }

        [Test]
        public void DeriveAssociatedTokenAccount_ThrowsOnNullOrEmptyInputs()
        {
            Assert.Throws<ArgumentException>(() => SplTokenProgramInstructions.DeriveAssociatedTokenAccount(null, TestPlemuMint));
            Assert.Throws<ArgumentException>(() => SplTokenProgramInstructions.DeriveAssociatedTokenAccount("", TestPlemuMint));
            Assert.Throws<ArgumentException>(() => SplTokenProgramInstructions.DeriveAssociatedTokenAccount(TestDeployerWallet, null));
            Assert.Throws<ArgumentException>(() => SplTokenProgramInstructions.DeriveAssociatedTokenAccount(TestDeployerWallet, ""));
        }

        [Test]
        public void DeriveAssociatedTokenAccount_ThrowsOnInvalidBase58Inputs()
        {
            Assert.Throws<ArgumentException>(() => SplTokenProgramInstructions.DeriveAssociatedTokenAccount("invalid-base-58!?", TestPlemuMint));
            Assert.Throws<ArgumentException>(() => SplTokenProgramInstructions.DeriveAssociatedTokenAccount(TestDeployerWallet, "invalid-base-58!?"));
        }

        [Test]
        public void TransferChecked_GeneratesValidSolanaInstruction()
        {
            string sourceAta = TestDeployerAta;
            string mint = TestPlemuMint;
            string destAta = "7vA1111111111111111111111111111111111111111";
            string owner = TestDeployerWallet;
            ulong amount = 10_000_000;
            byte decimals = 6;

            var ix = SplTokenProgramInstructions.TransferChecked(sourceAta, mint, destAta, owner, amount, decimals);

            Assert.AreEqual(SplTokenProgramInstructions.TokenProgramId, ix.ProgramId);
            Assert.AreEqual(4, ix.Keys.Count);

            Assert.AreEqual(sourceAta, ix.Keys[0].PublicKey);
            Assert.IsFalse(ix.Keys[0].IsSigner);
            Assert.IsTrue(ix.Keys[0].IsWritable);

            Assert.AreEqual(mint, ix.Keys[1].PublicKey);
            Assert.IsFalse(ix.Keys[1].IsSigner);
            Assert.IsFalse(ix.Keys[1].IsWritable);

            Assert.AreEqual(destAta, ix.Keys[2].PublicKey);
            Assert.IsFalse(ix.Keys[2].IsSigner);
            Assert.IsTrue(ix.Keys[2].IsWritable);

            Assert.AreEqual(owner, ix.Keys[3].PublicKey);
            Assert.IsTrue(ix.Keys[3].IsSigner);
            Assert.IsFalse(ix.Keys[3].IsWritable);

            Assert.AreEqual(10, ix.Data.Length);
            Assert.AreEqual(12, ix.Data[0]); // TransferChecked opcode
            Assert.AreEqual(amount, BitConverter.ToUInt64(ix.Data, 1));
            Assert.AreEqual(decimals, ix.Data[9]);
        }

        [Test]
        public void TransferChecked_ThrowsOnNullOrEmptyArguments()
        {
            Assert.Throws<ArgumentException>(() => SplTokenProgramInstructions.TransferChecked(null, TestPlemuMint, "dest", "owner", 100));
            Assert.Throws<ArgumentException>(() => SplTokenProgramInstructions.TransferChecked("src", null, "dest", "owner", 100));
            Assert.Throws<ArgumentException>(() => SplTokenProgramInstructions.TransferChecked("src", TestPlemuMint, null, "owner", 100));
            Assert.Throws<ArgumentException>(() => SplTokenProgramInstructions.TransferChecked("src", TestPlemuMint, "dest", null, 100));
        }

        [Test]
        public void Transfer_GeneratesValidSolanaInstruction()
        {
            string sourceAta = TestDeployerAta;
            string destAta = "7vA1111111111111111111111111111111111111111";
            string owner = TestDeployerWallet;
            ulong amount = 5_000_000;

            var ix = SplTokenProgramInstructions.Transfer(sourceAta, destAta, owner, amount);

            Assert.AreEqual(SplTokenProgramInstructions.TokenProgramId, ix.ProgramId);
            Assert.AreEqual(3, ix.Keys.Count);

            Assert.AreEqual(sourceAta, ix.Keys[0].PublicKey);
            Assert.IsFalse(ix.Keys[0].IsSigner);
            Assert.IsTrue(ix.Keys[0].IsWritable);

            Assert.AreEqual(destAta, ix.Keys[1].PublicKey);
            Assert.IsFalse(ix.Keys[1].IsSigner);
            Assert.IsTrue(ix.Keys[1].IsWritable);

            Assert.AreEqual(owner, ix.Keys[2].PublicKey);
            Assert.IsTrue(ix.Keys[2].IsSigner);
            Assert.IsFalse(ix.Keys[2].IsWritable);

            Assert.AreEqual(9, ix.Data.Length);
            Assert.AreEqual(3, ix.Data[0]); // Transfer opcode
            Assert.AreEqual(amount, BitConverter.ToUInt64(ix.Data, 1));
        }

        [Test]
        public void Transfer_ThrowsOnNullOrEmptyArguments()
        {
            Assert.Throws<ArgumentException>(() => SplTokenProgramInstructions.Transfer(null, "dest", "owner", 100));
            Assert.Throws<ArgumentException>(() => SplTokenProgramInstructions.Transfer("src", null, "owner", 100));
            Assert.Throws<ArgumentException>(() => SplTokenProgramInstructions.Transfer("src", "dest", null, 100));
        }

        [Test]
        public void SolanaPda_FindProgramAddress_ThrowsOnInvalidInputs()
        {
            Assert.Throws<ArgumentNullException>(() => SolanaPda.FindProgramAddress(null, TestPlemuMint, out _));
            Assert.Throws<ArgumentException>(() => SolanaPda.FindProgramAddress(new byte[0][], TestPlemuMint, out _));
            Assert.Throws<ArgumentException>(() => SolanaPda.FindProgramAddress(new byte[][] { new byte[10] }, null, out _));
        }

        [Test]
        public void CreateAssociatedTokenAccountIdempotent_GeneratesValidSolanaInstruction()
        {
            string payer = TestDeployerWallet;
            string recipientWallet = "7vA1111111111111111111111111111111111111111";
            string recipientAta = SplTokenProgramInstructions.DeriveAssociatedTokenAccount(recipientWallet, TestPlemuMint);

            var ix = SplTokenProgramInstructions.CreateAssociatedTokenAccountIdempotent(
                fundingAccount: payer,
                associatedTokenAccount: recipientAta,
                walletAddress: recipientWallet,
                tokenMintAddress: TestPlemuMint);

            Assert.AreEqual(SplTokenProgramInstructions.AssociatedTokenProgramId, ix.ProgramId);
            Assert.AreEqual(6, ix.Keys.Count);

            // 0. Funding account (signer, writable)
            Assert.AreEqual(payer, ix.Keys[0].PublicKey);
            Assert.IsTrue(ix.Keys[0].IsSigner);
            Assert.IsTrue(ix.Keys[0].IsWritable);

            // 1. Associated token account (writable)
            Assert.AreEqual(recipientAta, ix.Keys[1].PublicKey);
            Assert.IsFalse(ix.Keys[1].IsSigner);
            Assert.IsTrue(ix.Keys[1].IsWritable);

            // 2. Wallet address (readonly)
            Assert.AreEqual(recipientWallet, ix.Keys[2].PublicKey);
            Assert.IsFalse(ix.Keys[2].IsSigner);
            Assert.IsFalse(ix.Keys[2].IsWritable);

            // 3. Token mint (readonly)
            Assert.AreEqual(TestPlemuMint, ix.Keys[3].PublicKey);
            Assert.IsFalse(ix.Keys[3].IsSigner);
            Assert.IsFalse(ix.Keys[3].IsWritable);

            // 4. System Program (readonly)
            Assert.AreEqual(SplTokenProgramInstructions.SystemProgramId, ix.Keys[4].PublicKey);
            Assert.IsFalse(ix.Keys[4].IsSigner);
            Assert.IsFalse(ix.Keys[4].IsWritable);

            // 5. Token Program (readonly)
            Assert.AreEqual(SplTokenProgramInstructions.TokenProgramId, ix.Keys[5].PublicKey);
            Assert.IsFalse(ix.Keys[5].IsSigner);
            Assert.IsFalse(ix.Keys[5].IsWritable);

            // Instruction Data: Discriminator 1 for CreateIdempotent
            Assert.AreEqual(1, ix.Data.Length);
            Assert.AreEqual(1, ix.Data[0]);
        }

        [Test]
        public void CreateAssociatedTokenAccountIdempotent_ThrowsOnNullOrEmptyInputs()
        {
            string payer = TestDeployerWallet;
            string ata = TestDeployerAta;
            string owner = "7vA1111111111111111111111111111111111111111";
            string mint = TestPlemuMint;

            Assert.Throws<ArgumentException>(() => SplTokenProgramInstructions.CreateAssociatedTokenAccountIdempotent(null, ata, owner, mint));
            Assert.Throws<ArgumentException>(() => SplTokenProgramInstructions.CreateAssociatedTokenAccountIdempotent("", ata, owner, mint));
            Assert.Throws<ArgumentException>(() => SplTokenProgramInstructions.CreateAssociatedTokenAccountIdempotent(payer, null, owner, mint));
            Assert.Throws<ArgumentException>(() => SplTokenProgramInstructions.CreateAssociatedTokenAccountIdempotent(payer, "", owner, mint));
            Assert.Throws<ArgumentException>(() => SplTokenProgramInstructions.CreateAssociatedTokenAccountIdempotent(payer, ata, null, mint));
            Assert.Throws<ArgumentException>(() => SplTokenProgramInstructions.CreateAssociatedTokenAccountIdempotent(payer, ata, "", mint));
            Assert.Throws<ArgumentException>(() => SplTokenProgramInstructions.CreateAssociatedTokenAccountIdempotent(payer, ata, owner, null));
            Assert.Throws<ArgumentException>(() => SplTokenProgramInstructions.CreateAssociatedTokenAccountIdempotent(payer, ata, owner, ""));
        }

        [Test]
        public async Task MwaWallet_SendTip_BuildsAtomicTransactionWithIdempotentAtaCreationAndTransferChecked()
        {
            var capturingAdapter = new CapturingMwaAdapter();
            var wallet = new MwaWallet(capturingAdapter);

            await wallet.Connect();
            string sig = await wallet.SendTip(TestRecipientWallet, 2_500_000);

            Assert.IsNotNull(sig);
            byte[] capturedPayload = capturingAdapter.LastSerializedTransaction;
            Assert.IsNotNull(capturedPayload);

            var tx = SolanaTransaction.Deserialize(capturedPayload);
            Assert.AreEqual(1, tx.Header.NumRequiredSignatures);
            Assert.AreEqual(2, tx.Instructions.Count, "Atomic transaction must contain 2 instructions: CreateIdempotent ATA + TransferChecked.");

            // Instruction 0: CreateIdempotent ATA
            var ataIx = tx.Instructions[0];
            string ataProgramId = tx.AccountKeys[ataIx.ProgramIdIndex];
            Assert.AreEqual(SplTokenProgramInstructions.AssociatedTokenProgramId, ataProgramId, "First instruction must target Associated Token Account Program.");
            Assert.AreEqual(1, ataIx.Data.Length);
            Assert.AreEqual(1, ataIx.Data[0]); // CreateIdempotent discriminator
            Assert.AreEqual(6, ataIx.AccountIndices.Length);

            // Instruction 1: TransferChecked
            var transferIx = tx.Instructions[1];
            string transferProgramId = tx.AccountKeys[transferIx.ProgramIdIndex];
            Assert.AreEqual(SplTokenProgramInstructions.TokenProgramId, transferProgramId, "Second instruction must target SPL Token Program.");
            Assert.AreNotEqual(SolanaSystemProgram.ProgramId, transferProgramId, "SendTip must NOT transfer native SOL.");

            Assert.AreEqual(10, transferIx.Data.Length);
            Assert.AreEqual(12, transferIx.Data[0]); // TransferChecked
            Assert.AreEqual(2_500_000ul, BitConverter.ToUInt64(transferIx.Data, 1));
            Assert.AreEqual(6, transferIx.Data[9]); // 6 decimals for $PLEMU

            // Accounts: [sourceAta, mint, destAta, owner]
            Assert.AreEqual(4, transferIx.AccountIndices.Length);
            string sourceAtaKey = tx.AccountKeys[transferIx.AccountIndices[0]];
            string mintKey = tx.AccountKeys[transferIx.AccountIndices[1]];
            string destAtaKey = tx.AccountKeys[transferIx.AccountIndices[2]];
            string ownerKey = tx.AccountKeys[transferIx.AccountIndices[3]];

            Assert.AreEqual(TestDeployerAta, sourceAtaKey);
            Assert.AreEqual(TestPlemuMint, mintKey);
            Assert.AreEqual(TestDeployerWallet, ownerKey);
            Assert.IsNotNull(destAtaKey);
        }
    }
}
