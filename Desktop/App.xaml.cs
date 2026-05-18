using Microsoft.Extensions.Configuration;
using System.Windows;

namespace Desktop;

public partial class App : Application
{
    public static string ApiUrl { get; private set; } = string.Empty;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var config = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
        ApiUrl = config["ApiUrl"]!;
    }
}