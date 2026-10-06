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

    [Fact]
    public void DenialReason_Contains_EntitlementMissing()
    {
        Assert.True(Enum.IsDefined(typeof(Plemu.Content.DenialReason), Plemu.Content.DenialReason.EntitlementMissing));
        Assert.Equal("EntitlementMissing", Plemu.Content.DenialReason.EntitlementMissing.ToString());
    }

    [Fact]
    public void PlemuRuntimeSourceFiles_DoNotReference_PlemuSolana()
    {
        var plemuRoot = FindPlemuRoot();
        var runtimeSourceDir = Path.Combine(plemuRoot, "src", "Plemu.Runtime");

        var csFiles = Directory.GetFiles(runtimeSourceDir, "*.cs", SearchOption.AllDirectories);
        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file);
            Assert.DoesNotContain("using Plemu.Solana;", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Plemu.Solana", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PlemuRuntimeAssembly_DoesNotReference_PlemuSolanaAssembly()
    {
        var runtimeAssembly = typeof(Plemu.Content.LaunchGate).Assembly;
        var referencedAssemblies = runtimeAssembly.GetReferencedAssemblies();

        foreach (var assemblyName in referencedAssemblies)
        {
            Assert.DoesNotContain("Plemu.Solana", assemblyName.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Solana", assemblyName.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PlemuCoreAssembly_DoesNotReference_PlemuSolanaAssembly()
    {
        var coreAssembly = typeof(Plemu.Core.Abstractions.IWalletService).Assembly;
        var referencedAssemblies = coreAssembly.GetReferencedAssemblies();

        foreach (var assemblyName in referencedAssemblies)
        {
            Assert.DoesNotContain("Plemu.Solana", assemblyName.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Solana", assemblyName.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PlemuSolanaCsproj_DoesNotReference_PlemuRuntime_Or_PlemuApp()
    {
        var plemuRoot = FindPlemuRoot();
        var solanaCsprojPath = Path.Combine(plemuRoot, "src", "Plemu.Solana", "Plemu.Solana.csproj");

        Assert.True(File.Exists(solanaCsprojPath),
            $"Plemu.Solana.csproj could not be located at '{solanaCsprojPath}'.");

        var xml = XDocument.Load(solanaCsprojPath);
        var projectReferences = xml.Descendants("ProjectReference");

        foreach (var pr in projectReferences)
        {
            var includeAttr = pr.Attribute("Include")?.Value ?? string.Empty;
            Assert.DoesNotContain("Plemu.Runtime", includeAttr, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Plemu.App", includeAttr, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PlemuSolanaAssembly_DoesNotReference_PlemuRuntime_Or_PlemuApp()
    {
        var solanaAssembly = typeof(PlaySolanaWalletService).Assembly;
        var referencedAssemblies = solanaAssembly.GetReferencedAssemblies();

        foreach (var assemblyName in referencedAssemblies)
        {
            Assert.DoesNotContain("Plemu.Runtime", assemblyName.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Plemu.App", assemblyName.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void UnityRuntimeContentSourceFiles_DoNotReference_PlemuSolana()
    {
        var plemuRoot = FindPlemuRoot();
        var unityContentDir = Path.Combine(plemuRoot, "apps", "device", "unity", "Assets", "_Game", "Scripts", "Runtime", "Content");

        if (!Directory.Exists(unityContentDir))
        {
            return;
        }

        var csFiles = Directory.GetFiles(unityContentDir, "*.cs", SearchOption.AllDirectories);
        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file);
            Assert.DoesNotContain("using Plemu.Solana;", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Plemu.Solana", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PlemuCoreAbstractions_Types_DoNotExpose_PlemuSolanaTypes()
    {
        var typesToCheck = new[]
        {
            typeof(Plemu.Core.Abstractions.IWalletService),
            typeof(Plemu.Core.Abstractions.IEntitlementService),
            typeof(Plemu.Core.Abstractions.IEntitlementStore),
            typeof(Plemu.Core.Models.WalletIdentity),
            typeof(Plemu.Core.Models.WalletConnectionState)
        };

        foreach (var type in typesToCheck)
        {
            // Check properties
            foreach (var prop in type.GetProperties())
            {
                Assert.DoesNotContain("Plemu.Solana", prop.PropertyType.FullName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            // Check methods
            foreach (var method in type.GetMethods())
            {
                Assert.DoesNotContain("Plemu.Solana", method.ReturnType.FullName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                foreach (var param in method.GetParameters())
                {
                    Assert.DoesNotContain("Plemu.Solana", param.ParameterType.FullName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }
}
