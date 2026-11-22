using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Interfaces.External;
using RiceProduction.Application.EmergencyReportFeature.Commands.CreateEmergencyReport;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Tests.Fixtures;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.ReportFeature;

/// <summary>
/// Tests for CreateEmergencyReportCommandHandler - validates emergency report creation
/// </summary>
public class CreateEmergencyReportCommandHandlerTests
{
    private readonly Mock<IUser> _mockUser;
    private readonly Mock<IStorageService> _mockStorageService;

    public CreateEmergencyReportCommandHandlerTests()
    {
        _mockUser = new Mock<IUser>();
        _mockStorageService = new Mock<IStorageService>();

        _mockUser.Setup(u => u.Id).Returns(Guid.NewGuid());
        _mockUser.Setup(u => u.Roles).Returns(new List<string> { "Farmer" });
    }

    private CreateEmergencyReportCommandHandler CreateHandler(
        TestApplicationDbContext context,
        IUser? user = null) =>
        new(
            context,
            user ?? _mockUser.Object,
            _mockStorageService.Object,
            Mock.Of<ILogger<CreateEmergencyReportCommandHandler>>());

    private static async Task<Guid> SeedPlotCultivationAsync(TestApplicationDbContext context)
    {
        var pc = new PlotCultivation { Id = Guid.NewGuid() };
        context.PlotCultivations.Add(pc);
        await context.SaveChangesAsync();
        return pc.Id;
    }

    [Fact]
    public async Task Handle_ValidPestReport_CreatesSuccessfully()
    {
        await using var context = TestApplicationDbContext.Create();
        var plotCultivationId = await SeedPlotCultivationAsync(context);
        var handler = CreateHandler(context);

        var command = new CreateEmergencyReportCommand
        {
            PlotCultivationId = plotCultivationId,
            AlertType = "Pest",
            Title = "Brown Planthopper Infestation",
            Description = "Brown planthopper infestation detected",
            Severity = AlertSeverity.High
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);
        var report = await context.EmergencyReports.SingleOrDefaultAsync(r => r.Id == result.Data);
        report.Should().NotBeNull();
        report!.AlertType.Should().Be("Pest");
        report.Severity.Should().Be(AlertSeverity.High);
        report.PlotCultivationId.Should().Be(plotCultivationId);
    }

    [Fact]
    public async Task Handle_DiseaseReport_CreatesWithCorrectType()
    {
        await using var context = TestApplicationDbContext.Create();
        var plotCultivationId = await SeedPlotCultivationAsync(context);
        var handler = CreateHandler(context);

        var command = new CreateEmergencyReportCommand
        {
            PlotCultivationId = plotCultivationId,
            AlertType = "Disease",
            Title = "Rice Blast Disease",
            Description = "Rice blast disease spreading rapidly",
            Severity = AlertSeverity.Critical
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var report = await context.EmergencyReports.SingleAsync(r => r.Id == result.Data);
        report.AlertType.Should().Be("Disease");
    }

    [Fact]
    public async Task Handle_WeatherDamageReport_CreatesSuccessfully()
    {
        await using var context = TestApplicationDbContext.Create();
        var group = new Group { Id = Guid.NewGuid() };
        context.Groups.Add(group);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);
        var command = new CreateEmergencyReportCommand
        {
            GroupId = group.Id,
            AlertType = "Weather",
            Title = "Severe Flooding",
            Description = "Severe flooding due to heavy rainfall",
            Severity = AlertSeverity.Critical
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PlotCultivationNotFound_ReturnsFailure()
    {
        await using var context = TestApplicationDbContext.Create();
        var handler = CreateHandler(context);

        var command = new CreateEmergencyReportCommand
        {
            PlotCultivationId = Guid.NewGuid(),
            AlertType = "Pest",
            Title = "Test Report",
            Description = "Test report",
            Severity = AlertSeverity.Medium
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("PlotCultivation") && e.Contains("not found"));
        (await context.EmergencyReports.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_LowSeverityReport_CreatesSuccessfully()
    {
        await using var context = TestApplicationDbContext.Create();
        var plotCultivationId = await SeedPlotCultivationAsync(context);
        var handler = CreateHandler(context);

        var command = new CreateEmergencyReportCommand
        {
            PlotCultivationId = plotCultivationId,
            AlertType = "Pest",
            Title = "Minor Pest Observation",
            Description = "Minor pest observation",
            Severity = AlertSeverity.Low
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var report = await context.EmergencyReports.SingleAsync(r => r.Id == result.Data);
        report.Severity.Should().Be(AlertSeverity.Low);
    }

    [Fact]
    public async Task Handle_ReportWithLongDescription_StoresFullDescription()
    {
        await using var context = TestApplicationDbContext.Create();
        var plotCultivationId = await SeedPlotCultivationAsync(context);
        var handler = CreateHandler(context);

        var longDescription = string.Join(" ", Enumerable.Repeat(
            "Detailed observation of pest infestation including location, extent, and damage assessment.", 20));

        var command = new CreateEmergencyReportCommand
        {
            PlotCultivationId = plotCultivationId,
            AlertType = "Pest",
            Title = "Detailed Pest Report",
            Description = longDescription,
            Severity = AlertSeverity.Medium
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var report = await context.EmergencyReports.SingleAsync(r => r.Id == result.Data);
        report.Description.Should().Contain("Detailed observation of pest infestation");
    }

    [Fact]
    public async Task Handle_MultipleReportsForSamePlotCultivation_AllowsCreation()
    {
        await using var context = TestApplicationDbContext.Create();
        var plotCultivationId = await SeedPlotCultivationAsync(context);
        var handler = CreateHandler(context);

        var command = new CreateEmergencyReportCommand
        {
            PlotCultivationId = plotCultivationId,
            AlertType = "Disease",
            Title = "Second Report",
            Description = "Second report for same plot cultivation",
            Severity = AlertSeverity.High
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DatabaseSaveFails_ReturnsFailure()
    {
        await using var context = TestApplicationDbContext.Create();
        var plotCultivationId = await SeedPlotCultivationAsync(context);
        context.SaveChangesException = new Exception("Database error");
        var handler = CreateHandler(context);

        var command = new CreateEmergencyReportCommand
        {
            PlotCultivationId = plotCultivationId,
            AlertType = "Pest",
            Title = "Test Report",
            Description = "Test report",
            Severity = AlertSeverity.Medium
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("error"));
    }

    [Fact]
    public async Task Handle_OtherEmergencyType_CreatesSuccessfully()
    {
        await using var context = TestApplicationDbContext.Create();
        var cluster = new Cluster { Id = Guid.NewGuid(), ClusterName = "Test Cluster" };
        context.Clusters.Add(cluster);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);
        var command = new CreateEmergencyReportCommand
        {
            ClusterId = cluster.Id,
            AlertType = "Other",
            Title = "Unexpected Issue",
            Description = "Unexpected issue requiring attention",
            Severity = AlertSeverity.Medium
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var report = await context.EmergencyReports.SingleAsync(r => r.Id == result.Data);
        report.AlertType.Should().Be("Other");
    }

    [Fact]
    public async Task Handle_CriticalSeverityReport_CreatesWithHighPriority()
    {
        await using var context = TestApplicationDbContext.Create();
        var plotCultivationId = await SeedPlotCultivationAsync(context);
        var handler = CreateHandler(context);

        var command = new CreateEmergencyReportCommand
        {
            PlotCultivationId = plotCultivationId,
            AlertType = "Disease",
            Title = "Critical Rice Blast Outbreak",
            Description = "Critical rice blast outbreak affecting entire field",
            Severity = AlertSeverity.Critical
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var report = await context.EmergencyReports.SingleAsync(r => r.Id == result.Data);
        report.Severity.Should().Be(AlertSeverity.Critical);
    }

    [Fact]
    public async Task Handle_UserNotAuthenticated_ReturnsFailure()
    {
        await using var context = TestApplicationDbContext.Create();
        var unauthenticatedUser = new Mock<IUser>();
        unauthenticatedUser.Setup(u => u.Id).Returns((Guid?)null);
        var handler = CreateHandler(context, unauthenticatedUser.Object);

        var command = new CreateEmergencyReportCommand
        {
            PlotCultivationId = Guid.NewGuid(),
            AlertType = "Pest",
            Title = "Test Report",
            Description = "Test report",
            Severity = AlertSeverity.Medium
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not authenticated") || e.Contains("Unauthorized"));
    }

    [Fact]
    public async Task Handle_NoAffectedEntitySpecified_ReturnsFailure()
    {
        await using var context = TestApplicationDbContext.Create();
        var handler = CreateHandler(context);

        var command = new CreateEmergencyReportCommand
        {
            AlertType = "Pest",
            Title = "Test Report",
            Description = "Test report",
            Severity = AlertSeverity.Medium
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("At least one affected entity"));
    }

    [Fact]
    public async Task Handle_InvalidAlertType_ReturnsFailure()
    {
        await using var context = TestApplicationDbContext.Create();
        var handler = CreateHandler(context);

        var command = new CreateEmergencyReportCommand
        {
            PlotCultivationId = Guid.NewGuid(),
            AlertType = "InvalidType",
            Title = "Test Report",
            Description = "Test report",
            Severity = AlertSeverity.Medium
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Alert type must be"));
    }

    [Fact]
    public async Task Handle_SupervisorUser_SetsCorrectSource()
    {
        await using var context = TestApplicationDbContext.Create();
        var plotCultivationId = await SeedPlotCultivationAsync(context);
        var supervisorUser = new Mock<IUser>();
        supervisorUser.Setup(u => u.Id).Returns(Guid.NewGuid());
        supervisorUser.Setup(u => u.Roles).Returns(new List<string> { "Supervisor" });

        var handler = CreateHandler(context, supervisorUser.Object);

        var command = new CreateEmergencyReportCommand
        {
            PlotCultivationId = plotCultivationId,
            AlertType = "Pest",
            Title = "Supervisor Inspection Report",
            Description = "Inspection report",
            Severity = AlertSeverity.High
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var report = await context.EmergencyReports.SingleAsync(r => r.Id == result.Data);
        report.Source.Should().Be(AlertSource.SupervisorInspection);
    }
}
