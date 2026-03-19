using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Template.Observability;

public sealed class OpenTelemetryRuntime : IDisposable
{
    private readonly TracerProvider _tracerProvider;
    private readonly MeterProvider _meterProvider;

    private OpenTelemetryRuntime(OpenTelemetryRuntimeObserver observer, TracerProvider tracerProvider, MeterProvider meterProvider)
    {
        Observer = observer;
        _tracerProvider = tracerProvider;
        _meterProvider = meterProvider;
    }

    public OpenTelemetryRuntimeObserver Observer { get; }

    public static OpenTelemetryRuntime CreateDefault(string serviceName)
    {
        var observer = new OpenTelemetryRuntimeObserver();

        var resourceBuilder = ResourceBuilder.CreateDefault().AddService(serviceName);
        var emitConsole = !string.Equals(
            Environment.GetEnvironmentVariable("OTEL_CONSOLE_EXPORTER_ENABLED"),
            "false",
            StringComparison.OrdinalIgnoreCase);

        var tracerBuilder = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddSource(OpenTelemetryRuntimeObserver.ActivitySourceName);

        var meterBuilder = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter(OpenTelemetryRuntimeObserver.MeterName);

        if (emitConsole)
        {
            tracerBuilder.AddConsoleExporter();
            meterBuilder.AddConsoleExporter();
        }

        return new OpenTelemetryRuntime(observer, tracerBuilder.Build(), meterBuilder.Build());
    }

    public void Dispose()
    {
        Observer.Dispose();
        _tracerProvider.Dispose();
        _meterProvider.Dispose();
    }
}
