using System.IO;
using UnityEngine;

namespace Plemu.Tests
{
    /// <summary>
    /// Reads frozen fixtures via raw filesystem I/O (not Unity asset loading) so the signed
    /// bytes are exact. Path is resolved under <c>Application.dataPath/_Game/Tests/EditMode/Fixtures</c>.
    /// </summary>
    internal static class FixtureLoader
    {
        private static string FixturesDir =>
            Path.Combine(Application.dataPath, "_Game", "Tests", "EditMode", "Fixtures");

        internal static byte[] ReadBytes(string fileName)
        {
            return File.ReadAllBytes(Path.Combine(FixturesDir, fileName));
        }

        internal static string ReadText(string fileName)
        {
            return File.ReadAllText(Path.Combine(FixturesDir, fileName));
        }
    }
}
