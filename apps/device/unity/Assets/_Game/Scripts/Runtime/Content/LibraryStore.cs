using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Plemu.Content
{
    /// <summary>
    /// Minimal install-state snapshot: a map of title id → <see cref="LibraryEntry"/> persisted
    /// as JSON. Saves are atomic (write a sibling <c>.tmp</c> then replace/move) so a crash mid-write
    /// cannot corrupt the live file. Loads never throw: a missing file yields an empty store, and an
    /// unparseable file yields an empty store with <see cref="RecoveredCorrupt"/> set.
    /// </summary>
    public sealed class LibraryStore
    {
        [JsonProperty("entries")]
        public Dictionary<string, LibraryEntry> Entries { get; set; } = new Dictionary<string, LibraryEntry>();

        /// <summary>
        /// True when this store was produced by recovering from an unparseable file on
        /// <see cref="Load"/>. Not serialized.
        /// </summary>
        [JsonIgnore]
        public bool RecoveredCorrupt { get; private set; }

        /// <summary>
        /// Writes the store to <paramref name="path"/> atomically. Serializes to a
        /// <c>&lt;path&gt;.tmp</c> file, flushes, then replaces (or moves, on first write) the
        /// destination. On success no <c>.tmp</c> file remains.
        /// </summary>
        public void Save(string path)
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            string tempPath = path + ".tmp";

            File.WriteAllText(tempPath, json);

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }

        /// <summary>
        /// Loads a store from <paramref name="path"/>. Returns an empty store if the file does
        /// not exist, or an empty store with <see cref="RecoveredCorrupt"/> set if the file
        /// cannot be parsed. Never throws for missing/corrupt input.
        /// </summary>
        public static LibraryStore Load(string path)
        {
            if (!File.Exists(path))
                return new LibraryStore();

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (IOException)
            {
                return new LibraryStore { RecoveredCorrupt = true };
            }

            try
            {
                LibraryStore loaded = JsonConvert.DeserializeObject<LibraryStore>(json);
                if (loaded == null)
                    return new LibraryStore { RecoveredCorrupt = true };

                loaded.Entries ??= new Dictionary<string, LibraryEntry>();
                return loaded;
            }
            catch (JsonException)
            {
                return new LibraryStore { RecoveredCorrupt = true };
            }
        }
    }
}
