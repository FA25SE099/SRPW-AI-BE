using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RiceProduction.Application.Common.Telemetry;

public static class ApplicationMetrics
{
    private static readonly Meter Meter = new("RiceProduction.Application", "1.0.0");

    // Activity Source for distributed tracing
    public static readonly ActivitySource ActivitySource = new("RiceProduction.Application", "1.0.0");

    // Counters
    public static readonly Counter<long> CommandExecutionCounter = Meter.CreateCounter<long>(
        "riceproduction.command.executions",
        description: "Number of CQRS commands executed");

    public static readonly Counter<long> QueryExecutionCounter = Meter.CreateCounter<long>(
        "riceproduction.query.executions",
        description: "Number of CQRS queries executed");

    public static readonly Counter<long> ValidationErrorCounter = Meter.CreateCounter<long>(
        "riceproduction.validation.errors",
        description: "Number of validation errors");

    public static readonly Counter<long> DomainEventCounter = Meter.CreateCounter<long>(
        "riceproduction.domain.events",
        description: "Number of domain events raised");

    public static readonly Counter<long> FarmerCreatedCounter = Meter.CreateCounter<long>(
        "riceproduction.farmer.created",
        description: "Number of farmers created");

    public static readonly Counter<long> PlotCreatedCounter = Meter.CreateCounter<long>(
        "riceproduction.plot.created",
        description: "Number of plots created");

    public static readonly Counter<long> GroupFormedCounter = Meter.CreateCounter<long>(
        "riceproduction.group.formed",
        description: "Number of groups formed");

    public static readonly Counter<long> ProductionPlanCreatedCounter = Meter.CreateCounter<long>(
        "riceproduction.productionplan.created",
        description: "Number of production plans created");

    // Histograms
    public static readonly Histogram<double> CommandExecutionDuration = Meter.CreateHistogram<double>(
        "riceproduction.command.execution.duration",
        unit: "ms",
        description: "Command execution duration in milliseconds");

    public static readonly Histogram<double> QueryExecutionDuration = Meter.CreateHistogram<double>(
        "riceproduction.query.execution.duration",
        unit: "ms",
        description: "Query execution duration in milliseconds");

    public static readonly Histogram<double> DatabaseQueryDuration = Meter.CreateHistogram<double>(
        "riceproduction.database.query.duration",
        unit: "ms",
        description: "Database query duration in milliseconds");

    public static readonly Histogram<double> ExternalApiCallDuration = Meter.CreateHistogram<double>(
        "riceproduction.external.api.duration",
        unit: "ms",
        description: "External API call duration in milliseconds");

    // Observable Gauges
    public static readonly ObservableGauge<int> ActiveFarmersGauge = Meter.CreateObservableGauge(
        "riceproduction.farmers.active",
        () => GetActiveFarmersCount(),
        description: "Number of active farmers");

    public static readonly ObservableGauge<int> ActivePlotsGauge = Meter.CreateObservableGauge(
        "riceproduction.plots.active",
        () => GetActivePlotsCount(),
        description: "Number of active plots");

    // Helper methods
    private static int _activeFarmers = 0;
    private static int _activePlots = 0;

    public static void SetActiveFarmers(int count) => _activeFarmers = count;
    public static void SetActivePlots(int count) => _activePlots = count;

    private static int GetActiveFarmersCount() => _activeFarmers;
    private static int GetActivePlotsCount() => _activePlots;

    public static void RecordCommandExecution(string commandName, double durationMs, bool success)
    {
        var tags = new TagList
        {
            { "command.name", commandName },
            { "success", success }
        };

        CommandExecutionCounter.Add(1, tags);
        CommandExecutionDuration.Record(durationMs, tags);
    }

    public static void RecordQueryExecution(string queryName, double durationMs, bool success)
    {
        var tags = new TagList
        {
            { "query.name", queryName },
            { "success", success }
        };

        QueryExecutionCounter.Add(1, tags);
        QueryExecutionDuration.Record(durationMs, tags);
    }

    public static void RecordValidationError(string validationName)
    {
        ValidationErrorCounter.Add(1, new TagList { { "validation.name", validationName } });
    }

    public static void RecordDomainEvent(string eventName)
    {
        DomainEventCounter.Add(1, new TagList { { "event.name", eventName } });
    }

    public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
    {
        return ActivitySource.StartActivity(name, kind);
    }
}