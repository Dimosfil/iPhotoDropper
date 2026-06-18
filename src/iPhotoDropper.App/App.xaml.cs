using iPhotoDropper.Infrastructure.Services;
using iPhotoDropper.App.Logging;
using iPhotoDropper.App.ViewModels;
using iPhotoDropper.App.Views;
using iPhotoDropper.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace iPhotoDropper.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = default!;
    private IHost? _host;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();

        await _host.StartAsync();
        Services = _host.Services;

        var window = Services.GetRequiredService<MainWindow>();
        window.Closed += (_, _) =>
        {
            StopServicesAsync();
        };
        window.Activate();

        var usbService = Services.GetRequiredService<IUsbDeviceService>();
        _ = StartUsbServiceAsync(usbService);
    }

    private static async Task StartUsbServiceAsync(IUsbDeviceService usbService)
    {
        try
        {
            await Task.Run(() => usbService.StartAsync());
        }
        catch
        {
            // Keep the window responsive even if Windows MTP discovery fails.
        }
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        var appPaths = new AppPaths();
        services.AddSingleton(appPaths);

        services.AddLogging(builder => builder.AddSimpleConsole(opts =>
        {
            opts.SingleLine = true;
            opts.TimestampFormat = "HH:mm:ss ";
        }).AddProvider(new DailyFileLoggerProvider(appPaths.LogFolder)));

        var fallbackMockRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "iPhotoDropperMockDevice");
        var mockDeviceRoot = string.IsNullOrWhiteSpace(context.Configuration["MockDevice:RootPath"])
            ? fallbackMockRoot
            : context.Configuration["MockDevice:RootPath"]!;
        var enableMockDevice = string.Equals(
            context.Configuration["MockDevice:Enabled"],
            "true",
            StringComparison.OrdinalIgnoreCase);

        services.AddSingleton<IPhoneMtpDeviceService>();
        services.AddSingleton<MockUsbDeviceService>();
        services.AddSingleton<IUsbDeviceService>(sp => new HybridUsbDeviceService(
            sp.GetRequiredService<IPhoneMtpDeviceService>(),
            sp.GetRequiredService<MockUsbDeviceService>(),
            enableMockDevice));
        services.AddSingleton<IPhoneMtpPhotoLibraryService>();
        services.AddSingleton(sp => new MockPhotoLibraryService(mockDeviceRoot));
        services.AddSingleton<IPhotoLibraryService, HybridPhotoLibraryService>();
        services.AddSingleton<IHashService, HashService>();
        services.AddSingleton<ITransferStateStore>(sp =>
        {
            var paths = sp.GetRequiredService<AppPaths>();
            return new JsonTransferStateStore(Path.Combine(paths.StateFolder, "transfer-state.json"));
        });
        services.AddSingleton<ITransferService, TransferService>();

        services.AddSingleton<TransferViewModel>();
        services.AddSingleton<MainWindow>();
    }

    private async void StopServicesAsync()
    {
        try
        {
            await Services.GetRequiredService<IUsbDeviceService>().StopAsync();
        }
        catch
        {
            // Ignore shutdown errors to avoid blocking app close.
        }

        if (_host is null)
        {
            return;
        }

        try
        {
            await _host.StopAsync();
        }
        catch
        {
            // Ignore shutdown errors during process teardown.
        }
        finally
        {
            _host.Dispose();
            _host = null;
        }
    }
}
