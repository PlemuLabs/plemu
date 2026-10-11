using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Plemu.Wallet;

namespace Plemu.Tests
{
    [TestFixture]
    public class BufferOptimizationAndAllocationTests
    {
        private const string TestSender = "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm";
        private const string TestRecipient = "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL";
        private const string TestMint = "xNTPbgKARXhiC2Nn5FRiLdkv2SpD2tViiSEhW2qWruu";
        private const string TestBlockhash = "4vJ9JU1bJJE96FCSDJvffWN8iCG3qUCo33pPtxcLd4FX";

        [Test]
        public void Shortvec_EncodeByteArray_ReturnsExpectedBytePatterns()
        {
            byte[] encodedZero = SolanaShortvec.Encode(0);
            CollectionAssert.AreEqual(new byte[] { 0x00 }, encodedZero);

            byte[] encodedOne = SolanaShortvec.Encode(1);
            CollectionAssert.AreEqual(new byte[] { 0x01 }, encodedOne);

            byte[] encoded127 = SolanaShortvec.Encode(127);
            CollectionAssert.AreEqual(new byte[] { 0x7F }, encoded127);

            byte[] encoded128 = SolanaShortvec.Encode(128);
            CollectionAssert.AreEqual(new byte[] { 0x80, 0x01 }, encoded128);

            byte[] encoded16383 = SolanaShortvec.Encode(16383);
            CollectionAssert.AreEqual(new byte[] { 0xFF, 0x7F }, encoded16383);

            byte[] encoded16384 = SolanaShortvec.Encode(16384);
            CollectionAssert.AreEqual(new byte[] { 0x80, 0x80, 0x01 }, encoded16384);
        }

        [Test]
        public void Shortvec_EncodeToSpan_AndDecodeFromSpan_RoundTripsAllRanges()
        {
            int[] testValues = new int[] { 0, 1, 64, 127, 128, 255, 300, 16383, 16384, 65535 };
            byte[] buffer = new byte[4];

            foreach (int val in testValues)
            {
                int written = SolanaShortvec.Encode(val, buffer.AsSpan());
                Assert.IsTrue(written >= 1 && written <= 3, $"Written count {written} invalid for value {val}");

                int decoded = SolanaShortvec.Decode(buffer.AsSpan(0, written), out int readCount);
                Assert.AreEqual(val, decoded, $"Mismatch on value {val}");
                Assert.AreEqual(written, readCount, $"Mismatch on bytes read for value {val}");
            }
        }

        [Test]
        public void Shortvec_EncodeToSpan_DestinationTooSmall_ThrowsArgumentException()
        {
            byte[] buffer = new byte[1];
            bool caught = false;
            try
            {
                SolanaShortvec.Encode(128, buffer.AsSpan());
            }
            catch (ArgumentException)
            {
                caught = true;
            }
            Assert.IsTrue(caught, "Expected ArgumentException when destination span is too small.");
        }

        [Test]
        public void Shortvec_DecodeFromSpan_Truncated_ThrowsEndOfStreamException()
        {
            bool caughtEmpty = false;
            try
            {
                SolanaShortvec.Decode(ReadOnlySpan<byte>.Empty, out _);
            }
            catch (EndOfStreamException)
            {
                caughtEmpty = true;
            }
            Assert.IsTrue(caughtEmpty, "Expected EndOfStreamException for empty span.");

            byte[] truncated = new byte[] { 0x80 }; // Needs more bytes
            bool caughtTrunc = false;
            try
            {
                SolanaShortvec.Decode(truncated.AsSpan(), out _);
            }
            catch (EndOfStreamException)
            {
                caughtTrunc = true;
            }
            Assert.IsTrue(caughtTrunc, "Expected EndOfStreamException for truncated multi-byte span.");
        }

        [Test]
        public void Shortvec_DecodeFromSpan_Corrupt_ThrowsFormatException()
        {
            byte[] corrupt = new byte[] { 0x80, 0x80, 0x80, 0x80 };
            bool caught = false;
            try
            {
                SolanaShortvec.Decode(corrupt.AsSpan(), out _);
            }
            catch (FormatException)
            {
                caught = true;
            }
            Assert.IsTrue(caught, "Expected FormatException for corrupt multi-byte span.");
        }

        [Test]
        public void Base58_DecodeToSpan_AccurateAndMatchesDecodeByteArray()
        {
            string[] testAddresses = new string[] { TestSender, TestRecipient, TestMint, TestBlockhash };
            byte[] buffer = new byte[32];

            foreach (string addr in testAddresses)
            {
                byte[] expected = SolanaBase58.Decode(addr);
                Assert.AreEqual(32, expected.Length);

                int written = SolanaBase58.Decode(addr, buffer.AsSpan());
                Assert.AreEqual(32, written);
                CollectionAssert.AreEqual(expected, buffer);
            }
        }

        [Test]
        public void Base58_DecodeToSpan_DestinationTooSmall_ThrowsArgumentException()
        {
            byte[] smallBuffer = new byte[16];
            bool caught = false;
            try
            {
                SolanaBase58.Decode(TestSender, smallBuffer.AsSpan());
            }
            catch (ArgumentException)
            {
                caught = true;
            }
            Assert.IsTrue(caught, "Expected ArgumentException when Base58 destination span is too small.");
        }

        [Test]
        public void Base58_TryDecodeToSpan_ValidAndInvalidInputs_ReturnsExpectedBool()
        {
            byte[] validBuffer = new byte[32];
            bool success = SolanaBase58.TryDecode(TestSender, validBuffer.AsSpan(), out int bytesWritten);
            Assert.IsTrue(success);
            Assert.AreEqual(32, bytesWritten);

            byte[] smallBuffer = new byte[10];
            bool smallSuccess = SolanaBase58.TryDecode(TestSender, smallBuffer.AsSpan(), out int smallWritten);
            Assert.IsFalse(smallSuccess);
            Assert.AreEqual(0, smallWritten);

            byte[] invalidBuffer = new byte[32];
            bool invalidSuccess = SolanaBase58.TryDecode("0OIl-InvalidBase58", invalidBuffer.AsSpan(), out int invWritten);
            Assert.IsFalse(invalidSuccess);
            Assert.AreEqual(0, invWritten);
        }

        [Test]
        public void Base58_NullAndEmptyInputs_SpanDecode_ReturnsZero()
        {
            byte[] buffer = new byte[32];
            Assert.AreEqual(0, SolanaBase58.Decode(null, buffer.AsSpan()));
            Assert.AreEqual(0, SolanaBase58.Decode("", buffer.AsSpan()));

            Assert.IsTrue(SolanaBase58.TryDecode("", buffer.AsSpan(), out int written));
            Assert.AreEqual(0, written);
        }

        [Test]
        public void Concurrent_Shortvec_MultiThreadedExecution_NoRaceConditionsOrCorruption()
        {
            Parallel.For(0, 500, i =>
            {
                int val = i % 65536;
                using var ms = new MemoryStream();
                SolanaShortvec.Encode(val, ms);
                ms.Position = 0;
                int decoded = SolanaShortvec.Decode(ms);
                Assert.AreEqual(val, decoded);

                byte[] encodedArr = SolanaShortvec.Encode(val);
                int decodedFromSpan = SolanaShortvec.Decode(encodedArr.AsSpan(), out int read);
                Assert.AreEqual(val, decodedFromSpan);
                Assert.AreEqual(encodedArr.Length, read);
            });
        }

        [Test]
        public void Concurrent_Base58_MultiThreadedExecution_NoRaceConditionsOrCorruption()
        {
            string[] keys = new string[] { TestSender, TestRecipient, TestMint, TestBlockhash };

            Parallel.For(0, 500, i =>
            {
                string key = keys[i % keys.Length];
                byte[] decoded = SolanaBase58.Decode(key);
                Assert.AreEqual(32, decoded.Length);

                string reEncoded = SolanaBase58.Encode(decoded);
                Assert.AreEqual(key, reEncoded);

                byte[] buffer = new byte[32];
                int written = SolanaBase58.Decode(key, buffer.AsSpan());
                Assert.AreEqual(32, written);
                Assert.AreEqual(key, SolanaBase58.Encode(buffer));
            });
        }

        [Test]
        public void Concurrent_Transaction_SerializeDeserialize_NoRaceConditionsOrCorruption()
        {
            var sender = TestSender;
            var recipient = TestRecipient;
            var blockhash = TestBlockhash;

            Parallel.For(0, 200, i =>
            {
                var ix = SolanaSystemProgram.Transfer(sender, recipient, (ulong)(1000 + i));
                var tx = SolanaTransaction.Create(sender, new List<SolanaInstruction> { ix }, blockhash);

                byte[] serialized = tx.Serialize();
                Assert.IsNotNull(serialized);
                Assert.IsTrue(serialized.Length > 0);

                var deserialized = SolanaTransaction.Deserialize(serialized);
                Assert.AreEqual(blockhash, deserialized.RecentBlockhash);
                Assert.AreEqual(sender, deserialized.AccountKeys[0]);
                Assert.AreEqual(1, deserialized.Instructions.Count);

                byte[] reserialized = deserialized.Serialize();
                CollectionAssert.AreEqual(serialized, reserialized);
            });
        }

        [Test]
        public void Transaction_SerializeAndDeserialize_ProducesIdenticalWireBytesWithPooledBuffers()
        {
            var ix1 = SplTokenProgramInstructions.TransferChecked(
                TestSender,
                TestMint,
                TestRecipient,
                TestSender,
                5_000_000,
                6
            );

            var tx = SolanaTransaction.Create(TestSender, new List<SolanaInstruction> { ix1 }, TestBlockhash);
            byte[] wireBytes = tx.Serialize();

            var roundTripped = SolanaTransaction.Deserialize(wireBytes);
            Assert.AreEqual(TestBlockhash, roundTripped.RecentBlockhash);
            Assert.AreEqual(TestSender, roundTripped.AccountKeys[0]);
            Assert.AreEqual(1, roundTripped.Instructions.Count);

            byte[] wireBytes2 = roundTripped.Serialize();
            CollectionAssert.AreEqual(wireBytes, wireBytes2);
        }
    }
}
