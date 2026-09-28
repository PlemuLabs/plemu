using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Plemu.App;
using Plemu.Content;
using Plemu.Core;
using Plemu.Emulation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Plemu.Tests.PlayMode
{
    /// <summary>
    /// The M4 gate: proves the SameBoy core runs Libbet in Unity reached ONLY through
    /// <see cref="LaunchGate"/>, that video really emulates (framebuffer changes over time), that a
    /// savestate genuinely rewinds execution, and that a tampered content byte makes the gate refuse
    /// with <see cref="DenialReason.HashMismatch"/>. Mirrors the native rh_realcore_smoke harness,
    /// in the editor. Requires stage-editor-plugins.sh + stage-content.sh to have run.
    /// </summary>
    [TestFixture]
    public sealed class EmulationSmokeTests
    {
        private const string LibbetTitle = "gb/libbet";
        private const string LibbetSha256 =
            "3607412031c8287cf878299ce96e581e85b852dde703806343b95576fa3ff1a9";

        private GameObject _root;
        private VideoSurface _video;
        private AudioSurface _audio;
        private EmulationSession _session;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("EmuTestRoot");

            // A canvas + RawImage so VideoSurface has a real target to upload into.
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(_root.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var imgGo = new GameObject("Video");
            imgGo.transform.SetParent(canvasGo.transform, false);
            RawImage rawImage = imgGo.AddComponent<RawImage>();
            var area = (RectTransform)canvasGo.transform;

            _video = _root.AddComponent<VideoSurface>();
            _video.Configure(rawImage, area);

            var audioGo = new GameObject("Audio");
            audioGo.transform.SetParent(_root.transform, false);
            audioGo.AddComponent<AudioSource>().playOnAwake = false;
            _audio = audioGo.AddComponent<AudioSurface>();
        }

        [TearDown]
        public void TearDown()
        {
            _session?.Dispose();
            _session = null;
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
        }

        [Test]
        public void Authorize_Libbet_IssuesTicketWithCorrectFields()
        {
            LaunchDecision decision = Authorize(ContentPaths.ContentStore());

            Assert.That(decision.IsAuthorized, Is.True,
                decision.Denial != null ? $"Denied: {decision.Denial.Reason} — {decision.Denial.Detail}" : "no denial");
            Assert.That(decision.Ticket.TitleId, Is.EqualTo(LibbetTitle));
            Assert.That(decision.Ticket.System, Is.EqualTo("gb"));
            Assert.That(decision.Ticket.CoreId, Is.EqualTo("sameboy"));
            Assert.That(decision.Ticket.Sha256, Is.EqualTo(LibbetSha256));
            Assert.That(File.Exists(decision.Ticket.ContentPath), Is.True,
                $"content path should exist: {decision.Ticket.ContentPath}");
        }

        [UnityTest]
        public IEnumerator Emulation_RunsLibbet_VideoChangesAndAudioPlays()
        {
            LaunchDecision decision = Authorize(ContentPaths.ContentStore());
            Assert.That(decision.IsAuthorized, Is.True,
                decision.Denial != null ? $"Denied: {decision.Denial.Reason}" : "no ticket");

            _session = new EmulationSession();
            _session.Load(decision.Ticket);
            _video.Bind(_session);
            _audio.Bind(_session);

            // AV info sanity (mirrors the native harness): sane geometry + GB-ish fps.
            Assert.That(_session.BaseWidth, Is.GreaterThan(0u));
            Assert.That(_session.BaseHeight, Is.GreaterThan(0u));
            Assert.That(_session.Fps, Is.GreaterThan(50.0).And.LessThan(65.0), $"fps was {_session.Fps}");
            Assert.That(_session.SampleRate, Is.GreaterThan(0.0));

            // Drive deterministically: pause the auto loop and step, so the test is fast and stable.
            _session.Pause();

            // Early frame (~60 in): capture pixels after a LateUpdate uploads them.
            _session.StepFrames(60);
            yield return null; // let VideoSurface.LateUpdate upload
            Color32[] early = ReadTexturePixels();
            Assert.That(early, Is.Not.Null, "no early texture");
            Assert.That(IsBlank(early), Is.False, "frame @60 is blank (single constant colour)");
            ulong earlyHash = HashPixels(early);

            // Late frame (~600 in): a booting game changes the screen between these points.
            _session.StepFrames(540);
            yield return null;
            Color32[] late = ReadTexturePixels();
            Assert.That(late, Is.Not.Null, "no late texture");
            Assert.That(IsBlank(late), Is.False, "frame @600 is blank");
            ulong lateHash = HashPixels(late);

            Assert.That(lateHash, Is.Not.EqualTo(earlyHash),
                "framebuffer identical at frame 60 and 600 — core is not emulating");
            Assert.That(_video.FramesUploaded, Is.GreaterThan(0));

            TestContext.WriteLine(
                $"[EMU-EVIDENCE] Libbet {_video.Texture.width}x{_video.Texture.height} " +
                $"frameHash@60=0x{earlyHash:x16} frameHash@600=0x{lateHash:x16} " +
                $"uploads={_video.FramesUploaded} (pixels changed => real emulation)");

            // Dump the rendered frame as a PNG for visual evidence (best-effort).
            TryDumpPng(_video.Texture, "libbet-frame600.png");

            // Audio: pull through the real AudioSurface path and assert non-zero samples exist.
            // (OnAudioFilterRead does not run reliably in batchmode, so pump the same code path.)
            bool anyNonZero = false;
            for (int burst = 0; burst < 8 && !anyNonZero; burst++)
            {
                _session.StepFrames(8);
                float[] samples = _audio.PumpForTest(4096);
                foreach (float s in samples)
                {
                    if (s != 0f)
                    {
                        anyNonZero = true;
                        break;
                    }
                }
            }
            Assert.That(anyNonZero, Is.True, "no non-zero audio samples — core produced silence");
        }

        [UnityTest]
        public IEnumerator SaveState_RoundTrips_RewindsExecution()
        {
            LaunchDecision decision = Authorize(ContentPaths.ContentStore());
            Assert.That(decision.IsAuthorized, Is.True);

            _session = new EmulationSession();
            _session.Load(decision.Ticket);
            _video.Bind(_session);
            _session.Pause();

            // Advance into the animated boot so a fork provably diverges (mirrors the native proof).
            _session.StepFrames(40);

            byte[] state = _session.SaveState();
            Assert.That(state, Is.Not.Null.And.Not.Empty);

            // Reference: state + 1 frame.
            _session.LoadState(state);
            _session.StepFrames(1);
            yield return null;
            ulong referenceHash = HashPixels(ReadTexturePixels());

            // Diverge: run 60 more frames from the reference point; the screen must move on.
            _session.StepFrames(60);
            yield return null;
            ulong divergedHash = HashPixels(ReadTexturePixels());
            Assert.That(divergedHash, Is.Not.EqualTo(referenceHash),
                "no forward progress after the save point — cannot prove a non-trivial rewind");

            // Rewind: restore state, run exactly 1 frame; MUST reproduce the reference. Only a real
            // state restore can reproduce referenceHash after diverging away from it.
            _session.LoadState(state);
            _session.StepFrames(1);
            yield return null;
            ulong restoredHash = HashPixels(ReadTexturePixels());
            Assert.That(restoredHash, Is.EqualTo(referenceHash),
                "savestate did NOT rewind: restored frame hash differs from the reference");
        }

        [Test]
        public void Authorize_TamperedArtifact_DeniesHashMismatch()
        {
            // Point the store at a TEMP copy of the content dir where one byte is flipped, so the
            // real staged content is untouched. LaunchGate must refuse with HashMismatch.
            string tempContentDir = Path.Combine(Path.GetTempPath(), $"plemu_tamper_{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(tempContentDir);
            try
            {
                string srcDir = ContentPaths.ContentDirectory;
                // Copy the manifest + sig so the manifest still verifies (only the artifact is bad).
                File.Copy(Path.Combine(srcDir, FileManifestSource.DefaultManifestName),
                    Path.Combine(tempContentDir, FileManifestSource.DefaultManifestName));
                File.Copy(Path.Combine(srcDir, FileManifestSource.DefaultSignatureName),
                    Path.Combine(tempContentDir, FileManifestSource.DefaultSignatureName));

                string artifactsDir = Path.Combine(tempContentDir, FileContentStore.ArtifactsSubdir);
                Directory.CreateDirectory(artifactsDir);
                byte[] bytes = File.ReadAllBytes(Path.Combine(srcDir, FileContentStore.ArtifactsSubdir, LibbetSha256));
                bytes[0] ^= 0xFF; // flip one byte
                File.WriteAllBytes(Path.Combine(artifactsDir, LibbetSha256), bytes);

                var manifests = new FileManifestSource(tempContentDir);
                var store = new FileContentStore(tempContentDir);

                // Deterministic clock (manifest is Fresh) so the ONLY reason to deny is the flipped
                // byte — a wall-clock past grace could otherwise mask HashMismatch with ManifestExpired.
                LaunchDecision decision =
                    LaunchGate.Authorize(LibbetTitle, manifests, store, FreshClockForStagedManifest());

                Assert.That(decision.IsAuthorized, Is.False, "tampered artifact must NOT be authorized");
                Assert.That(decision.Denial.Reason, Is.EqualTo(DenialReason.HashMismatch));

                // Quarantine moved the bad artifact aside (fail-closed sweep).
                Assert.That(File.Exists(Path.Combine(artifactsDir, LibbetSha256)), Is.False,
                    "mismatched artifact should have been quarantined");
            }
            finally
            {
                if (Directory.Exists(tempContentDir))
                    Directory.Delete(tempContentDir, recursive: true);
            }
        }

        [UnityTest]
        public IEnumerator AudioReadDuringStop_IsRaceSafe_NoCrash()
        {
            // Guard for the audio-thread use-after-free on teardown: hammer the real audio pull path
            // (AudioSurface.PumpForTest -> EmulationSession.TryReadAudio -> rh_read_audio) on a
            // background thread while the main thread calls Stop() (rh_unload_game + rh_destroy).
            // A true data race is nondeterministic, so this drives many start/pump/stop cycles and
            // asserts no exception and no native crash escapes; the fix is the session's teardown
            // lock + alive flag, so a read that overlaps destroy either completes first or bails to
            // silence rather than dereferencing a freed ring.
            const int cycles = 12;
            for (int i = 0; i < cycles; i++)
            {
                LaunchDecision decision = Authorize(ContentPaths.ContentStore());
                Assert.That(decision.IsAuthorized, Is.True);

                var session = new EmulationSession();
                session.Load(decision.Ticket);
                _audio.Bind(session);

                var stop = new System.Threading.ManualResetEventSlim(false);
                System.Exception threadError = null;
                var pumper = new System.Threading.Thread(() =>
                {
                    try
                    {
                        // Pump continuously until told to stop; each call goes through TryReadAudio.
                        while (!stop.IsSet)
                            _audio.PumpForTest(1024);
                    }
                    catch (System.Exception ex)
                    {
                        threadError = ex;
                    }
                })
                {
                    Name = "AudioPumpStress",
                    IsBackground = true,
                };
                pumper.Start();

                // Let some reads land, then tear the handle down WHILE the pumper is mid-flight.
                yield return null;
                session.Stop(); // rh_destroy under the lock; concurrent reads must be safe

                // Keep pumping briefly AFTER destroy: every read must now bail to silence.
                yield return null;
                stop.Set();
                pumper.Join(2000);

                _audio.Bind(null);
                session.Dispose();

                Assert.That(threadError, Is.Null,
                    threadError != null ? $"audio pump threw during/after Stop: {threadError}" : null);
                Assert.That(pumper.IsAlive, Is.False, "audio pump thread did not exit (possible deadlock)");
            }
        }

        // --- helpers -------------------------------------------------------------------------

        private static LaunchDecision Authorize(IContentStore store)
        {
            return LaunchGate.Authorize(
                LibbetTitle,
                ContentPaths.ManifestSource(),
                store,
                FreshClockForStagedManifest());
        }

        /// <summary>
        /// A deterministic clock pinned to the staged manifest's own freshness window: it parses the
        /// committed <c>manifest-beta.json</c>'s <c>generatedAt</c> and returns that instant plus a
        /// few seconds — guaranteed inside <c>[generatedAt, expiresAt]</c> (expiry is
        /// <c>generatedAt + 72h</c>, SPEC §1.2), so the freshness gate resolves to <c>Fresh</c> no
        /// matter when <c>stage-content.sh</c> last ran. This exercises the real 72h-expiry
        /// production manifest without a wall-clock dependency (no ~10-day time-bomb).
        /// </summary>
        private static IClock FreshClockForStagedManifest()
        {
            ManifestBundle bundle = ContentPaths.ManifestSource().GetVerifiedManifest();
            Assert.That(bundle, Is.Not.Null, "staged manifest-beta.json not found — run stage-content.sh");

            // Verify parses the (authenticated) manifest and returns it regardless of freshness
            // (freshness is reported, not enforced, by the verifier), so we can read generatedAt with
            // any bootstrap clock. A parse failure here means the signing chain is broken.
            VerificationResult verification = ManifestVerifier.Verify(
                bundle.ManifestBytes, bundle.SignatureBytes, bundle.Keys, DateTimeOffset.UnixEpoch);
            Assert.That(verification.IsValid, Is.True,
                $"staged manifest failed verification: {verification.Failure}");
            Assert.That(verification.Manifest, Is.Not.Null);

            Assert.That(Iso8601.TryParse(verification.Manifest.GeneratedAt, out DateTimeOffset generatedAt),
                Is.True, $"could not parse manifest generatedAt: '{verification.Manifest.GeneratedAt}'");

            return new FixedClock(generatedAt + TimeSpan.FromSeconds(5));
        }

        private Color32[] ReadTexturePixels()
        {
            Texture2D tex = _video.Texture;
            return tex == null ? null : tex.GetPixels32();
        }

        private static bool IsBlank(Color32[] pixels)
        {
            if (pixels == null || pixels.Length == 0)
                return true;
            Color32 first = pixels[0];
            for (int i = 1; i < pixels.Length; i++)
            {
                if (!pixels[i].Equals(first))
                    return false;
            }
            return true;
        }

        private static void TryDumpPng(Texture2D source, string fileName)
        {
            try
            {
                if (source == null)
                    return;
                // Copy into an RGBA32 texture so EncodeToPNG works regardless of the source format.
                var rgba = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                rgba.SetPixels32(source.GetPixels32());
                rgba.Apply(false);
                byte[] png = rgba.EncodeToPNG();
                UnityEngine.Object.Destroy(rgba);

                string dir = System.Environment.GetEnvironmentVariable("PLEMU_EVIDENCE_DIR");
                if (string.IsNullOrEmpty(dir))
                    dir = Path.GetTempPath();
                string path = Path.Combine(dir, fileName);
                File.WriteAllBytes(path, png);
                TestContext.WriteLine($"[EMU-EVIDENCE] wrote frame PNG: {path} ({png.Length} bytes)");
            }
            catch (System.Exception ex)
            {
                TestContext.WriteLine($"[EMU-EVIDENCE] PNG dump skipped: {ex.Message}");
            }
        }

        private static ulong HashPixels(Color32[] pixels)
        {
            // FNV-1a over the RGBA bytes (same spirit as the native harness's framebuffer hash).
            ulong h = 1469598103934665603UL;
            foreach (Color32 c in pixels)
            {
                h = (h ^ c.r) * 1099511628211UL;
                h = (h ^ c.g) * 1099511628211UL;
                h = (h ^ c.b) * 1099511628211UL;
                h = (h ^ c.a) * 1099511628211UL;
            }
            return h;
        }
    }
}
