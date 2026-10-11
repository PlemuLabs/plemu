using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Plemu.Tests
{
    [TestFixture]
    public class AndroidManifestTests
    {
        private static readonly XNamespace AndroidNs = "http://schemas.android.com/apk/res/android";
        private string _manifestPath;
        private XDocument _document;

        [SetUp]
        public void SetUp()
        {
            _manifestPath = ResolveManifestPath();
            if (File.Exists(_manifestPath))
            {
                _document = XDocument.Load(_manifestPath);
            }
        }

        private static string ResolveManifestPath()
        {
            string[] candidates = new[]
            {
                Path.Combine("apps", "device", "unity", "Assets", "Plugins", "Android", "AndroidManifest.xml"),
                Path.Combine("plemu", "apps", "device", "unity", "Assets", "Plugins", "Android", "AndroidManifest.xml"),
                Path.Combine(Directory.GetCurrentDirectory(), "plemu", "apps", "device", "unity", "Assets", "Plugins", "Android", "AndroidManifest.xml"),
                Path.Combine(Directory.GetCurrentDirectory(), "apps", "device", "unity", "Assets", "Plugins", "Android", "AndroidManifest.xml")
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }

            return Path.Combine("plemu", "apps", "device", "unity", "Assets", "Plugins", "Android", "AndroidManifest.xml");
        }

        [Test]
        public void ManifestFile_ExistsAtExpectedPath()
        {
            Assert.IsTrue(File.Exists(_manifestPath), $"AndroidManifest.xml was not found at '{_manifestPath}'.");
        }

        [Test]
        public void Manifest_IsValidXml()
        {
            Assert.IsNotNull(_document, "AndroidManifest.xml must be valid XML and loadable into XDocument.");
            Assert.IsNotNull(_document.Root, "Root element of AndroidManifest.xml must not be null.");
            Assert.AreEqual("manifest", _document.Root.Name.LocalName);
        }

        [Test]
        public void Manifest_RootElement_DeclaresAndroidNamespace()
        {
            Assert.IsNotNull(_document, "Document not loaded.");
            XAttribute androidAttr = _document.Root.Attributes().FirstOrDefault(a => a.Name.LocalName == "android");
            Assert.IsNotNull(androidAttr, "manifest must declare xmlns:android attribute.");
            Assert.AreEqual("http://schemas.android.com/apk/res/android", androidAttr.Value);
        }

        [Test]
        public void Manifest_DeclaresInternetPermission()
        {
            Assert.IsNotNull(_document, "Document not loaded.");
            var permissions = _document.Root.Elements("uses-permission")
                .Select(e => (string)e.Attribute(AndroidNs + "name"))
                .ToList();

            Assert.IsTrue(permissions.Contains("android.permission.INTERNET"),
                "AndroidManifest.xml must declare <uses-permission android:name=\"android.permission.INTERNET\" />.");
        }

        [Test]
        public void Manifest_DeclaresQueriesElement()
        {
            Assert.IsNotNull(_document, "Document not loaded.");
            XElement queries = _document.Root.Element("queries");
            Assert.IsNotNull(queries, "AndroidManifest.xml must contain a top-level <queries> element for Android 11+ package visibility.");
        }

        [Test]
        public void Manifest_QueriesElement_ContainsSolanaWalletIntent()
        {
            Assert.IsNotNull(_document, "Document not loaded.");
            XElement queries = _document.Root.Element("queries");
            Assert.IsNotNull(queries, "<queries> element is required.");

            var matchingIntent = queries.Elements("intent").FirstOrDefault(intent =>
            {
                var action = (string)intent.Element("action")?.Attribute(AndroidNs + "name");
                var category = (string)intent.Element("category")?.Attribute(AndroidNs + "name");
                var dataScheme = (string)intent.Element("data")?.Attribute(AndroidNs + "scheme");

                return action == "android.intent.action.VIEW" &&
                       category == "android.intent.category.BROWSABLE" &&
                       dataScheme == "solana-wallet";
            });

            Assert.IsNotNull(matchingIntent,
                "The <queries> tag must contain an <intent> with action 'android.intent.action.VIEW', category 'android.intent.category.BROWSABLE', and data scheme 'solana-wallet'.");
        }

        [Test]
        public void Manifest_ApplicationElement_Exists()
        {
            Assert.IsNotNull(_document, "Document not loaded.");
            XElement application = _document.Root.Element("application");
            Assert.IsNotNull(application, "AndroidManifest.xml must contain an <application> element.");
        }

        [Test]
        public void Manifest_ApplicationElement_DisablesBackupForSecurity()
        {
            Assert.IsNotNull(_document, "Document not loaded.");
            XElement application = _document.Root.Element("application");
            Assert.IsNotNull(application, "<application> element required.");

            string allowBackup = (string)application.Attribute(AndroidNs + "allowBackup");
            Assert.AreEqual("false", allowBackup, "<application> must set android:allowBackup=\"false\" per security policies.");
        }

        [Test]
        public void Manifest_ApplicationElement_HasUnityPlayerActivity()
        {
            Assert.IsNotNull(_document, "Document not loaded.");
            XElement application = _document.Root.Element("application");
            Assert.IsNotNull(application, "<application> element required.");

            var unityActivity = application.Elements("activity").FirstOrDefault(act =>
                (string)act.Attribute(AndroidNs + "name") == "com.unity3d.player.UnityPlayerActivity");

            Assert.IsNotNull(unityActivity, "AndroidManifest.xml must declare an <activity> for 'com.unity3d.player.UnityPlayerActivity'.");
        }

        [Test]
        public void Manifest_UnityPlayerActivity_HasMainLauncherIntentFilter()
        {
            Assert.IsNotNull(_document, "Document not loaded.");
            XElement application = _document.Root.Element("application");
            var unityActivity = application?.Elements("activity").FirstOrDefault(act =>
                (string)act.Attribute(AndroidNs + "name") == "com.unity3d.player.UnityPlayerActivity");

            Assert.IsNotNull(unityActivity, "UnityPlayerActivity required.");

            var intentFilters = unityActivity.Elements("intent-filter");
            bool hasMain = intentFilters.Any(filter =>
                filter.Elements("action").Any(a => (string)a.Attribute(AndroidNs + "name") == "android.intent.action.MAIN"));
            bool hasLauncher = intentFilters.Any(filter =>
                filter.Elements("category").Any(c => (string)c.Attribute(AndroidNs + "name") == "android.intent.category.LAUNCHER"));

            Assert.IsTrue(hasMain, "UnityPlayerActivity must declare action 'android.intent.action.MAIN'.");
            Assert.IsTrue(hasLauncher, "UnityPlayerActivity must declare category 'android.intent.category.LAUNCHER'.");
        }

        [Test]
        public void Manifest_UnityPlayerActivity_HasUnityActivityMetaData()
        {
            Assert.IsNotNull(_document, "Document not loaded.");
            XElement application = _document.Root.Element("application");
            var unityActivity = application?.Elements("activity").FirstOrDefault(act =>
                (string)act.Attribute(AndroidNs + "name") == "com.unity3d.player.UnityPlayerActivity");

            Assert.IsNotNull(unityActivity, "UnityPlayerActivity required.");

            var metaData = unityActivity.Elements("meta-data").FirstOrDefault(m =>
                (string)m.Attribute(AndroidNs + "name") == "unityplayer.UnityActivity");

            Assert.IsNotNull(metaData, "UnityPlayerActivity must declare meta-data 'unityplayer.UnityActivity'.");
            Assert.AreEqual("true", (string)metaData.Attribute(AndroidNs + "value"), "meta-data 'unityplayer.UnityActivity' must have value 'true'.");
        }

        [Test]
        public void Manifest_UnityPlayerActivity_HasDeepLinkIntentFilterForPlemuScheme()
        {
            Assert.IsNotNull(_document, "Document not loaded.");
            XElement application = _document.Root.Element("application");
            var unityActivity = application?.Elements("activity").FirstOrDefault(act =>
                (string)act.Attribute(AndroidNs + "name") == "com.unity3d.player.UnityPlayerActivity");

            Assert.IsNotNull(unityActivity, "UnityPlayerActivity required.");

            var intentFilters = unityActivity.Elements("intent-filter");
            bool hasDeepLink = intentFilters.Any(filter =>
            {
                var action = (string)filter.Element("action")?.Attribute(AndroidNs + "name");
                var categoryDefault = filter.Elements("category").Any(c => (string)c.Attribute(AndroidNs + "name") == "android.intent.category.DEFAULT");
                var categoryBrowsable = filter.Elements("category").Any(c => (string)c.Attribute(AndroidNs + "name") == "android.intent.category.BROWSABLE");
                var dataScheme = (string)filter.Element("data")?.Attribute(AndroidNs + "scheme");
                var dataHost = (string)filter.Element("data")?.Attribute(AndroidNs + "host");

                return action == "android.intent.action.VIEW" &&
                       categoryDefault &&
                       categoryBrowsable &&
                       dataScheme == "plemu" &&
                       dataHost == "mwa-callback";
            });

            Assert.IsTrue(hasDeepLink, "UnityPlayerActivity must declare an <intent-filter> for 'plemu://mwa-callback' deep link returns.");
        }
    }
}
