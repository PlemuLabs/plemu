using System;
using NUnit.Framework;
using Plemu.Content;

namespace Plemu.Tests
{
    [TestFixture]
    public sealed class FreshnessPolicyTests
    {
        private static readonly DateTimeOffset ExpiresAt =
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

        [Test]
        public void Evaluate_NowBeforeExpiry_Fresh()
        {
            // Arrange
            DateTimeOffset now = ExpiresAt - TimeSpan.FromDays(1);

            // Act
            Freshness result = FreshnessPolicy.Evaluate(ExpiresAt, now);

            // Assert
            Assert.That(result, Is.EqualTo(Freshness.Fresh));
        }

        [Test]
        public void Evaluate_NowExactlyAtExpiry_Fresh()
        {
            // Arrange: the expiry instant itself is inclusive on the Fresh side.
            DateTimeOffset now = ExpiresAt;

            // Act
            Freshness result = FreshnessPolicy.Evaluate(ExpiresAt, now);

            // Assert
            Assert.That(result, Is.EqualTo(Freshness.Fresh));
        }

        [Test]
        public void Evaluate_NowOneSecondPastExpiry_Grace()
        {
            // Arrange
            DateTimeOffset now = ExpiresAt + TimeSpan.FromSeconds(1);

            // Act
            Freshness result = FreshnessPolicy.Evaluate(ExpiresAt, now);

            // Assert
            Assert.That(result, Is.EqualTo(Freshness.Grace));
        }

        [Test]
        public void Evaluate_NowExactlyAtGraceBoundary_Grace()
        {
            // Arrange: exactly expiresAt + 7 days is inclusive (still Grace).
            DateTimeOffset now = ExpiresAt + TimeSpan.FromDays(7);

            // Act
            Freshness result = FreshnessPolicy.Evaluate(ExpiresAt, now);

            // Assert
            Assert.That(result, Is.EqualTo(Freshness.Grace));
        }

        [Test]
        public void Evaluate_NowOneSecondPastGrace_Expired()
        {
            // Arrange
            DateTimeOffset now = ExpiresAt + TimeSpan.FromDays(7) + TimeSpan.FromSeconds(1);

            // Act
            Freshness result = FreshnessPolicy.Evaluate(ExpiresAt, now);

            // Assert
            Assert.That(result, Is.EqualTo(Freshness.Expired));
        }
    }
}
