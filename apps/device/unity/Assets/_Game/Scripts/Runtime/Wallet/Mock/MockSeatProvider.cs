using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Plemu.Wallet.Mock
{
    /// <summary>
    /// File-backed mock seat provider reading from mock-seats.json.
    /// Shape: {"packs":["speedrun-club","translation-club"]}.
    /// If the file is missing or invalid, returns empty seats (allowing Stream A to test the 402 no-seat path).
    /// </summary>
    public sealed class MockSeatProvider : ISeatProvider
    {
        private readonly string _filePath;

        [Serializable]
        private class MockSeatsData
        {
            [JsonProperty("packs")]
            public List<string> packs { get; set; } = new List<string>();
        }

        public MockSeatProvider() : this(Path.Combine(Application.persistentDataPath, "mock-seats.json"))
        {
        }

        public MockSeatProvider(string filePath)
        {
            _filePath = filePath;
        }

        /// <inheritdoc/>
        public Task<bool> HasSeat(string packId)
        {
            if (string.IsNullOrEmpty(packId))
                return Task.FromResult(false);

            IReadOnlyList<string> packs = ReadPacksFromFile();
            for (int i = 0; i < packs.Count; i++)
            {
                if (string.Equals(packs[i], packId, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<string>> MyPacks()
        {
            return Task.FromResult(ReadPacksFromFile());
        }

        private IReadOnlyList<string> ReadPacksFromFile()
        {
            if (!File.Exists(_filePath))
                return Array.Empty<string>();

            try
            {
                string json = File.ReadAllText(_filePath);
                var data = JsonConvert.DeserializeObject<MockSeatsData>(json);
                if (data?.packs != null)
                {
                    return data.packs;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MockSeatProvider] Failed to read mock seats at {_filePath}: {ex.Message}");
            }

            return Array.Empty<string>();
        }
    }
}
