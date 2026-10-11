using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Plemu.Wallet
{
    public static class PlemuProgramInstructions
    {
        public const string DefaultSeatsProgramId = "SEAT111111111111111111111111111111111111111";
        public const string DefaultMarketProgramId = "MRKT111111111111111111111111111111111111111";
        public const string DefaultSystemProgramId = "11111111111111111111111111111111";

        private static string _seatsProgramId = DefaultSeatsProgramId;
        private static string _marketProgramId = DefaultMarketProgramId;

        public static string SeatsProgramId
        {
            get => _seatsProgramId;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("Seats program ID cannot be null or empty.", nameof(value));
                }

                byte[] decoded;
                try
                {
                    decoded = SolanaBase58.Decode(value);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Invalid Base58 string for seats program ID: {ex.Message}", nameof(value), ex);
                }

                if (decoded.Length != 32)
                {
                    throw new ArgumentException("Seats program ID must decode to 32 bytes.", nameof(value));
                }
                _seatsProgramId = value;
            }
        }

        public static string MarketProgramId
        {
            get => _marketProgramId;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("Market program ID cannot be null or empty.", nameof(value));
                }

                byte[] decoded;
                try
                {
                    decoded = SolanaBase58.Decode(value);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Invalid Base58 string for market program ID: {ex.Message}", nameof(value), ex);
                }

                if (decoded.Length != 32)
                {
                    throw new ArgumentException("Market program ID must decode to 32 bytes.", nameof(value));
                }
                _marketProgramId = value;
            }
        }

        public static void Configure(string seatsProgramId, string marketProgramId)
        {
            SeatsProgramId = seatsProgramId;
            MarketProgramId = marketProgramId;
        }

        public static void ConfigureFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Addresses JSON cannot be null or empty.", nameof(json));
            }

            JObject obj;
            try
            {
                obj = JObject.Parse(json);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to parse addresses JSON: {ex.Message}", nameof(json), ex);
            }

            string seats = (string)obj["seatsProgramId"] ?? (string)obj["seatsProgram"] ?? (string)obj["seats"];
            string market = (string)obj["marketProgramId"] ?? (string)obj["marketProgram"] ?? (string)obj["market"];

            if (!string.IsNullOrWhiteSpace(seats))
            {
                SeatsProgramId = seats;
            }

            if (!string.IsNullOrWhiteSpace(market))
            {
                MarketProgramId = market;
            }
        }

        public static void ResetToDefaults()
        {
            _seatsProgramId = DefaultSeatsProgramId;
            _marketProgramId = DefaultMarketProgramId;
        }

        public static byte[] GetAnchorDiscriminator(string instructionName)
        {
            if (string.IsNullOrWhiteSpace(instructionName))
            {
                throw new ArgumentException("Instruction name cannot be null or empty.", nameof(instructionName));
            }

            string preimage = $"global:{instructionName}";
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(preimage));
            byte[] discriminator = new byte[8];
            Array.Copy(hash, 0, discriminator, 0, 8);
            return discriminator;
        }

        public static SolanaInstruction PurchaseSeat(
            string buyerPublicKey,
            string packId,
            ulong maxPriceBaseUnits,
            string programId = null)
        {
            if (string.IsNullOrWhiteSpace(buyerPublicKey))
            {
                throw new ArgumentException("Buyer public key cannot be null or empty.", nameof(buyerPublicKey));
            }

            if (string.IsNullOrWhiteSpace(packId))
            {
                throw new ArgumentException("Pack ID cannot be null or empty.", nameof(packId));
            }

            string targetProgramId = !string.IsNullOrWhiteSpace(programId) ? programId : SeatsProgramId;

            byte[] buyerKeyBytes = SolanaBase58.Decode(buyerPublicKey);
            byte[] packIdBytes = Encoding.UTF8.GetBytes(packId);

            string seatPda = SolanaPda.FindProgramAddress(
                new[] { Encoding.UTF8.GetBytes("seat"), packIdBytes, buyerKeyBytes },
                targetProgramId,
                out _);

            string packPda = SolanaPda.FindProgramAddress(
                new[] { Encoding.UTF8.GetBytes("pack"), packIdBytes },
                targetProgramId,
                out _);

            var keys = new List<SolanaAccountMeta>
            {
                new SolanaAccountMeta(buyerPublicKey, isSigner: true, isWritable: true),
                new SolanaAccountMeta(seatPda, isSigner: false, isWritable: true),
                new SolanaAccountMeta(packPda, isSigner: false, isWritable: true),
                new SolanaAccountMeta(DefaultSystemProgramId, isSigner: false, isWritable: false)
            };

            using var ms = new MemoryStream();
            byte[] discriminator = GetAnchorDiscriminator("purchase_seat");
            ms.Write(discriminator, 0, 8);

            byte[] lenBytes = BitConverter.GetBytes((uint)packIdBytes.Length);
            ms.Write(lenBytes, 0, 4);
            ms.Write(packIdBytes, 0, packIdBytes.Length);

            byte[] priceBytes = BitConverter.GetBytes(maxPriceBaseUnits);
            ms.Write(priceBytes, 0, 8);

            return new SolanaInstruction(targetProgramId, keys, ms.ToArray());
        }

        public static SolanaInstruction PurchaseListing(
            string buyerPublicKey,
            string entryId,
            ulong maxPriceBaseUnits,
            string programId = null)
        {
            if (string.IsNullOrWhiteSpace(buyerPublicKey))
            {
                throw new ArgumentException("Buyer public key cannot be null or empty.", nameof(buyerPublicKey));
            }

            if (string.IsNullOrWhiteSpace(entryId))
            {
                throw new ArgumentException("Entry ID cannot be null or empty.", nameof(entryId));
            }

            string targetProgramId = !string.IsNullOrWhiteSpace(programId) ? programId : MarketProgramId;

            byte[] buyerKeyBytes = SolanaBase58.Decode(buyerPublicKey);
            byte[] entryIdBytes = Encoding.UTF8.GetBytes(entryId);

            string listingPda = SolanaPda.FindProgramAddress(
                new[] { Encoding.UTF8.GetBytes("listing"), entryIdBytes },
                targetProgramId,
                out _);

            string receiptPda = SolanaPda.FindProgramAddress(
                new[] { Encoding.UTF8.GetBytes("receipt"), entryIdBytes, buyerKeyBytes },
                targetProgramId,
                out _);

            var keys = new List<SolanaAccountMeta>
            {
                new SolanaAccountMeta(buyerPublicKey, isSigner: true, isWritable: true),
                new SolanaAccountMeta(listingPda, isSigner: false, isWritable: true),
                new SolanaAccountMeta(receiptPda, isSigner: false, isWritable: true),
                new SolanaAccountMeta(DefaultSystemProgramId, isSigner: false, isWritable: false)
            };

            using var ms = new MemoryStream();
            byte[] discriminator = GetAnchorDiscriminator("purchase_listing");
            ms.Write(discriminator, 0, 8);

            byte[] lenBytes = BitConverter.GetBytes((uint)entryIdBytes.Length);
            ms.Write(lenBytes, 0, 4);
            ms.Write(entryIdBytes, 0, entryIdBytes.Length);

            byte[] priceBytes = BitConverter.GetBytes(maxPriceBaseUnits);
            ms.Write(priceBytes, 0, 8);

            return new SolanaInstruction(targetProgramId, keys, ms.ToArray());
        }
    }
}
