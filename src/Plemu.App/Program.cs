using Microsoft.Extensions.DependencyInjection;

namespace Plemu.App;

public static class Program
{
    public static void Main(string[] args)
    {
        var services = new ServiceCollection();
        CompositionRoot.ConfigureServices(services);
        using var provider = services.BuildServiceProvider();
    }
}
