using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Plemu.Tests
{
    [TestFixture]
    public class LinkXmlTests
    {
        private string _linkXmlPath;

        [SetUp]
        public void SetUp()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            // Locate plemu/apps/device/unity/Assets/link.xml from current directory or project root
            string candidate1 = Path.Combine(baseDir, "plemu", "apps", "device", "unity", "Assets", "link.xml");
            string candidate2 = Path.Combine(Directory.GetCurrentDirectory(), "plemu", "apps", "device", "unity", "Assets", "link.xml");
            string candidate3 = Path.Combine(Directory.GetCurrentDirectory(), "apps", "device", "unity", "Assets", "link.xml");

            if (File.Exists(candidate1))
            {
                _linkXmlPath = candidate1;
            }
            else if (File.Exists(candidate2))
            {
                _linkXmlPath = candidate2;
            }
            else if (File.Exists(candidate3))
            {
                _linkXmlPath = candidate3;
            }
            else
            {
                // Set expected location for assertion
                _linkXmlPath = candidate2;
            }
        }

        [Test]
        public void LinkXml_FileExistsAtExpectedPath()
        {
            Assert.IsTrue(File.Exists(_linkXmlPath), $"link.xml file should exist at '{_linkXmlPath}'");
        }

        [Test]
        public void LinkXml_IsValidXml_WithLinkerRootElement()
        {
            Assert.IsTrue(File.Exists(_linkXmlPath), "link.xml must exist before parsing.");
            string xmlContent = File.ReadAllText(_linkXmlPath);
            XDocument doc = XDocument.Parse(xmlContent);

            Assert.IsNotNull(doc.Root, "Root element should not be null.");
            Assert.AreEqual("linker", doc.Root.Name.LocalName, "Root element must be <linker>");
        }

        [Test]
        public void LinkXml_PreservesNewtonsoftJsonAssembly()
        {
            Assert.IsTrue(File.Exists(_linkXmlPath), "link.xml must exist before parsing.");
            XDocument doc = XDocument.Parse(File.ReadAllText(_linkXmlPath));

            var assemblyElem = doc.Root.Elements("assembly")
                .FirstOrDefault(e => (string)e.Attribute("fullname") == "Newtonsoft.Json");

            Assert.IsNotNull(assemblyElem, "link.xml must declare <assembly fullname=\"Newtonsoft.Json\">");
            Assert.AreEqual("all", (string)assemblyElem.Attribute("preserve"), "Newtonsoft.Json assembly must have preserve=\"all\"");
        }

        [Test]
        public void LinkXml_PreservesPlemuWalletAssembly()
        {
            Assert.IsTrue(File.Exists(_linkXmlPath), "link.xml must exist before parsing.");
            XDocument doc = XDocument.Parse(File.ReadAllText(_linkXmlPath));

            var assemblyElem = doc.Root.Elements("assembly")
                .FirstOrDefault(e => (string)e.Attribute("fullname") == "Plemu.Wallet");

            Assert.IsNotNull(assemblyElem, "link.xml must declare <assembly fullname=\"Plemu.Wallet\">");
            Assert.AreEqual("all", (string)assemblyElem.Attribute("preserve"), "Plemu.Wallet assembly must have preserve=\"all\"");
        }

        [Test]
        public void LinkXml_PreservesSolanaUnitySdkAssemblies()
        {
            Assert.IsTrue(File.Exists(_linkXmlPath), "link.xml must exist before parsing.");
            XDocument doc = XDocument.Parse(File.ReadAllText(_linkXmlPath));

            string[] expectedAssemblies = new[]
            {
                "Solana.Unity.SDK",
                "Solana.Unity.Rpc",
                "Solana.Unity.Wallet",
                "Solana.Unity.Programs"
            };

            foreach (var asm in expectedAssemblies)
            {
                var assemblyElem = doc.Root.Elements("assembly")
                    .FirstOrDefault(e => (string)e.Attribute("fullname") == asm);

                Assert.IsNotNull(assemblyElem, $"link.xml must declare <assembly fullname=\"{asm}\">");
                Assert.AreEqual("all", (string)assemblyElem.Attribute("preserve"), $"{asm} assembly must have preserve=\"all\"");
            }
        }
    }
}
