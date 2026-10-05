using System;
using System.IO;
using System.Xml.Linq;
using Xunit;

namespace Plemu.Solana.Tests;

public class ArchitecturalIsolationTests
{
    private static string FindPlemuRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Plemu.slnx")) ||
                (Directory.Exists(Path.Combine(dir.FullName, "Core")) &&
                 Directory.Exists(Path.Combine(dir.FullName, "src"))))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        var cwd = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (cwd != null)
        {
            if (File.Exists(Path.Combine(cwd.FullName, "plemu", "Plemu.slnx")))
            {
                return Path.Combine(cwd.FullName, "plemu");
            }
            if (File.Exists(Path.Combine(cwd.FullName, "Plemu.slnx")))
            {
                return cwd.FullName;
            }
            cwd = cwd.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate plemu repository root directory.");
    }

    [Fact]
    public void PlemuRuntimeCsproj_DoesNotReference_PlemuSolanaCsproj()
    {
        var plemuRoot = FindPlemuRoot();
        var runtimeCsprojPath = Path.Combine(plemuRoot, "src", "Plemu.Runtime", "Plemu.Runtime.csproj");

        Assert.True(File.Exists(runtimeCsprojPath),
            $"Plemu.Runtime.csproj could not be located at '{runtimeCsprojPath}'.");

        var xml = XDocument.Load(runtimeCsprojPath);
        var projectReferences = xml.Descendants("ProjectReference");

        foreach (var pr in projectReferences)
        {
            var includeAttr = pr.Attribute("Include")?.Value ?? string.Empty;
            Assert.DoesNotContain("Plemu.Solana", includeAttr, StringComparison.OrdinalIgnoreCase);
        }

        var packageReferences = xml.Descendants("PackageReference");
        foreach (var pkg in packageReferences)
        {
            var includeAttr = pkg.Attribute("Include")?.Value ?? string.Empty;
            Assert.DoesNotContain("Solana", includeAttr, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PlemuCoreCsproj_DoesNotReference_PlemuSolanaCsproj()
    {
        var plemuRoot = FindPlemuRoot();
        var coreCsprojPath = Path.Combine(plemuRoot, "Core", "Plemu.Core.csproj");

        Assert.True(File.Exists(coreCsprojPath),
            $"Plemu.Core.csproj could not be located at '{coreCsprojPath}'.");

        var xml = XDocument.Load(coreCsprojPath);
        var projectReferences = xml.Descendants("ProjectReference");

        foreach (var pr in projectReferences)
        {
            var includeAttr = pr.Attribute("Include")?.Value ?? string.Empty;
            Assert.DoesNotContain("Plemu.Solana", includeAttr, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void CompositionRoot_ContainsSurgicalDev2Registrations()
    {
        var plemuRoot = FindPlemuRoot();
        var compRootPath = Path.Combine(plemuRoot, "src", "Plemu.App", "CompositionRoot.cs");

        Assert.True(File.Exists(compRootPath),
            $"CompositionRoot.cs could not be located at '{compRootPath}'.");

        var content = File.ReadAllText(compRootPath);
        Assert.Contains("services.AddSingleton<IWalletService, PlaySolanaWalletService>();", content);
        Assert.Contains("services.AddSingleton<IEntitlementService, SolanaEntitlementService>();", content);
    }
}
