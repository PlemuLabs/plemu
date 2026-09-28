using System.IO;
using System.Text;
using NUnit.Framework;
using Plemu.Content;

namespace Plemu.Tests
{
    [TestFixture]
    public sealed class Sha256VerifierTests
    {
        private const string StarforgeSha256 =
            "70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd";

        [Test]
        public void ComputeHex_EmptyInput_ReturnsKnownAnswer()
        {
            // Arrange
            using MemoryStream stream = new MemoryStream(new byte[0]);

            // Act
            string hex = Sha256Verifier.ComputeHex(stream);

            // Assert
            Assert.That(hex, Is.EqualTo("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"));
        }

        [Test]
        public void ComputeHex_Abc_ReturnsKnownAnswer()
        {
            // Arrange
            using MemoryStream stream = new MemoryStream(Encoding.ASCII.GetBytes("abc"));

            // Act
            string hex = Sha256Verifier.ComputeHex(stream);

            // Assert
            Assert.That(hex, Is.EqualTo("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
        }

        [Test]
        public void ComputeHex_StarforgeFixture_MatchesSpecDigest()
        {
            // Arrange
            byte[] artifact = FixtureLoader.ReadBytes("starforge-demo.bytes");
            using MemoryStream stream = new MemoryStream(artifact);

            // Act
            string hex = Sha256Verifier.ComputeHex(stream);

            // Assert
            Assert.That(hex, Is.EqualTo(StarforgeSha256));
        }

        [Test]
        public void Matches_StarforgeFixtureAgainstSpecDigest_True()
        {
            // Arrange
            byte[] artifact = FixtureLoader.ReadBytes("starforge-demo.bytes");
            using MemoryStream stream = new MemoryStream(artifact);

            // Act
            bool matches = Sha256Verifier.Matches(stream, StarforgeSha256);

            // Assert
            Assert.That(matches, Is.True);
        }

        [Test]
        public void ComputeHex_ThreeByteChunkedStream_EqualsOneShot()
        {
            // Arrange
            byte[] artifact = FixtureLoader.ReadBytes("starforge-demo.bytes");
            string oneShot;
            using (MemoryStream whole = new MemoryStream(artifact))
            {
                oneShot = Sha256Verifier.ComputeHex(whole);
            }

            using ChunkedStream chunked = new ChunkedStream(artifact, 3);

            // Act
            string streamed = Sha256Verifier.ComputeHex(chunked);

            // Assert
            Assert.That(streamed, Is.EqualTo(oneShot));
        }

        /// <summary>A read-only stream that never returns more than <c>maxChunk</c> bytes per Read.</summary>
        private sealed class ChunkedStream : Stream
        {
            private readonly byte[] _data;
            private readonly int _maxChunk;
            private int _position;

            public ChunkedStream(byte[] data, int maxChunk)
            {
                _data = data;
                _maxChunk = maxChunk;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _data.Length;

            public override long Position
            {
                get => _position;
                set => throw new System.NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int remaining = _data.Length - _position;
                if (remaining <= 0)
                    return 0;

                int toCopy = System.Math.Min(System.Math.Min(count, _maxChunk), remaining);
                System.Array.Copy(_data, _position, buffer, offset, toCopy);
                _position += toCopy;
                return toCopy;
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new System.NotSupportedException();

            public override void SetLength(long value) => throw new System.NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new System.NotSupportedException();
        }
    }
}
