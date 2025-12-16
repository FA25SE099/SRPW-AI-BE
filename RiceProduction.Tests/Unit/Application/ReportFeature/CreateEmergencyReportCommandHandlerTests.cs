using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Interfaces.External;
using RiceProduction.Application.EmergencyReportFeature.Commands.CreateEmergencyReport;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System.Linq.Expressions;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.ReportFeature;

/// <summary>
/// Tests for CreateEmergencyReportCommandHandler - validates emergency report creation
/// </summary>
public class CreateEmergencyReportCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IGenericRepository<EmergencyReport>> _mockEmergencyReportRepo;
    private readonly Mock<IGenericRepository<PlotCultivation>> _mockPlotCultivationRepo;
    private readonly Mock<IGenericRepository<Group>> _mockGroupRepo;
    private readonly Mock<IClusterRepository> _mockClusterRepo;
    private readonly Mock<IUser> _mockUser;
    private readonly Mock<IStorageService> _mockStorageService;
    private readonly Mock<ILogger<CreateEmergencyReportCommandHandler>> _mockLogger;
    private readonly CreateEmergencyReportCommandHandler _handler;

    public CreateEmergencyReportCommandHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockEmergencyReportRepo = new Mock<IGenericRepository<EmergencyReport>>();
        _mockPlotCultivationRepo = new Mock<IGenericRepository<PlotCultivation>>();
        _mockGroupRepo = new Mock<IGenericRepository<Group>>();
        _mockClusterRepo = new Mock<IClusterRepository>();
        _mockUser = new Mock<IUser>();
        _mockStorageService = new Mock<IStorageService>();
        _mockLogger = new Mock<ILogger<CreateEmergencyReportCommandHandler>>();

        _mockUnitOfWork.Setup(u => u.Repository<EmergencyReport>()).Returns(_mockEmergencyReportRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<PlotCultivation>()).Returns(_mockPlotCultivationRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<Group>()).Returns(_mockGroupRepo.Object);
        _mockUnitOfWork.Setup(u => u.ClusterRepository).Returns(_mockClusterRepo.Object);

        // Setup default authenticated user
        var userId = Guid.NewGuid();
        _mockUser.Setup(u => u.Id).Returns(userId);
        _mockUser.Setup(u => u.Roles).Returns(new List<string> { "Farmer" });

        _handler = new CreateEmergencyReportCommandHandler(
            _mockUnitOfWork.Object,
            _mockUser.Object,
            _mockStorageService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidPestReport_CreatesSuccessfully()
    {
        // Arrange
        var plotCultivationId = Guid.NewGuid();

        var command = new CreateEmergencyReportCommand
        {
            PlotCultivationId = plotCultivationId,
            AlertType = "Pest",
            Title = "Brown Planthopper Infestation",
            Description = "Brown planthopper infestation detected",
            Severity = AlertSeverity.High
        };

        _mockPlotCultivationRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<PlotCultivation, bool>>>()))
            .ReturnsAsync(true);

        _mockEmergencyReportRepo.Setup(r => r.AddAsync(It.IsAny<EmergencyReport>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);
        _mockEmergencyReportRepo.Verify(r => r.AddAsync(It.Is<EmergencyReport>(
            report => report.AlertType == "Pest" &&
                     report.Severity == AlertSeverity.High &&
                     report.PlotCultivationId == plotCultivationId)), Times.Once);
    }

    [Fact]
    public async Task Handle_DiseaseReport_CreatesWithCorrectType()
    {
        // Arrange
        var plotCultivationId = Guid.NewGuid();

        var command = new CreateEmergencyReportCommand
        {
            PlotCultivationId = plotCultivationId,
            AlertType = "Disease",
            Title = "Rice Blast Disease",
            Description = "Rice blast disease spreading rapidly",
            Severity = AlertSeverity.Critical
        };

        _mockPlotCultivationRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<PlotCultivation, bool>>>()))
            .ReturnsAsync(true);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockEmergencyReportRepo.Verify(r => r.AddAsync(It.Is<EmergencyReport>(
            report => report.AlertType == "Disease")), Times.Once);
    }

    [Fact]
    public async Task Handle_WeatherDamageReport_CreatesSuccessfully()
    {
        // Arrange
        var groupId = Guid.NewGuid();

        var command = new CreateEmergencyReportCommand
        {
            GroupId = groupId,
            AlertType = "Weather",
            Title = "Severe Flooding",
            Description = "Severe flooding due to heavy rainfall",
            Severity = AlertSeverity.Critical
        };

        _mockGroupRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<Group, bool>>>()))
            .ReturnsAsync(true);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PlotCultivationNotFound_ReturnsFailure()
    {
        // Arrange
        var command = new CreateEmergencyReportCommand
        {
            PlotCultivationId = Guid.NewGuid(),
            AlertType = "Pest",
            Title = "Test Report",
            Description = "Test report",
            Severity = AlertSeverity.Medium
        };

        _mockPlotCultivationRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<PlotCultivation, bool>>>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("PlotCultivation") && e.Contains("not found"));
        _mockEmergencyReportRepo.Verify(r => r.AddAsync(It.IsAny<EmergencyReport>()), Times.Never);
    }

    [Fact]
    public async Task Handle_LowSeverityReport_CreatesSuccessfully()
    {
        // Arrange
        var plotCultivationId = Guid.NewGuid();

        var command = new CreateEmergencyReportCommand
        {
            PlotCultivationId = plotCultivationId,
            AlertType = "Pest",
            Title = "Minor Pest Observation",
            Description = "Minor pest observation",
            Severity = AlertSeverity.Low
        };

        _mockPlotCultivationRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<PlotCultivation, bool>>>()))
            .ReturnsAsync(true);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockEmergencyReportRepo.Verify(r => r.AddAsync(It.Is<EmergencyReport>(
            report => report.Severity == AlertSeverity.Low)), Times.Once);
    }

    [Fact]
    public async Task Handle_ReportWithLongDescription_StoresFullDescription()
    {
        // Arrange
        var plotCultivationId = Guid.NewGuid();

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

        _mockPlotCultivationRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<PlotCultivation, bool>>>()))
            .ReturnsAsync(true);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockEmergencyReportRepo.Verify(r => r.AddAsync(It.Is<EmergencyReport>(
            report => report.Description.Contains("Detailed observation of pest infestation"))), Times.Once);
    }

    [Fact]
    public async Task Handle_MultipleReportsForSamePlotCultivation_AllowsCreation()
    {
        // Arrange
        var plotCultivationId = Guid.NewGuid();

        var command = new CreateEmergencyReportCommand
        {
            PlotCultivationId = plotCultivationId,
            AlertType = "Disease",
            Title = "Second Report",
            Description = "Second report for same plot cultivation",
            Severity = AlertSeverity.High
        };

        _mockPlotCultivationRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<PlotCultivation, bool>>>()))
            .ReturnsAsync(true);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DatabaseSaveFails_ReturnsFailure()
    {
        // Arrange
        var plotCultivationId = Guid.NewGuid();

        var command = new CreateEmergencyReportCommand
        {
            PlotCultivationId = plotCultivationId,
            AlertType = "Pest",
            Title = "Test Report",
            Description = "Test report",
            Severity = AlertSeverity.Medium
        };

        _mockPlotCultivationRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<PlotCultivation, bool>>>()))
            .ReturnsAsync(true);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("error"));
    }

    [Fact]
    public async Task Handle_OtherEmergencyType_CreatesSuccessfully()
    {
        // Arrange
        var clusterId = Guid.NewGuid();

        var command = new CreateEmergencyReportCommand
        {
            ClusterId = clusterId,
            AlertType = "Other",
            Title = "Unexpected Issue",
            Description = "Unexpected issue requiring attention",
            Severity = AlertSeverity.Medium
        };

        _mockClusterRepo.Setup(r => r.ExistClusterAsync(clusterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockEmergencyReportRepo.Verify(r => r.AddAsync(It.Is<EmergencyReport>(
            report => report.AlertType == "Other")), Times.Once);
    }

    [Fact]
    public async Task Handle_CriticalSeverityReport_CreatesWithHighPriority()
    {
        // Arrange
        var plotCultivationId = Guid.NewGuid();

        var command = new CreateEmergencyReportCommand
        {
            PlotCultivationId = plotCultivationId,
            AlertType = "Disease",
            Title = "Critical Rice Blast Outbreak",
            Description = "Critical rice blast outbreak affecting entire field",
            Severity = AlertSeverity.Critical
        };

        _mockPlotCultivationRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<PlotCultivation, bool>>>()))
            .ReturnsAsync(true);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockEmergencyReportRepo.Verify(r => r.AddAsync(It.Is<EmergencyReport>(
            report => report.Severity == AlertSeverity.Critical)), Times.Once);
    }

    [Fact]
    public async Task Handle_UserNotAuthenticated_ReturnsFailure()
    {
        // Arrange
        var unauthenticatedUser = new Mock<IUser>();
        unauthenticatedUser.Setup(u => u.Id).Returns((Guid?)null);

        var handler = new CreateEmergencyReportCommandHandler(
            _mockUnitOfWork.Object,
            unauthenticatedUser.Object,
            _mockStorageService.Object,
            _mockLogger.Object);

        var command = new CreateEmergencyReportCommand
        {
            PlotCultivationId = Guid.NewGuid(),
            AlertType = "Pest",
            Title = "Test Report",
            Description = "Test report",
            Severity = AlertSeverity.Medium
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not authenticated") || e.Contains("Unauthorized"));
    }

    [Fact]
    public async Task Handle_NoAffectedEntitySpecified_ReturnsFailure()
    {
        // Arrange
        var command = new CreateEmergencyReportCommand
        {
            // No PlotCultivationId, GroupId, or ClusterId specified
            AlertType = "Pest",
            Title = "Test Report",
            Description = "Test report",
            Severity = AlertSeverity.Medium
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("At least one affected entity"));
    }

    [Fact]
    public async Task Handle_InvalidAlertType_ReturnsFailure()
    {
        // Arrange
        var command = new CreateEmergencyReportCommand
        {
            PlotCultivationId = Guid.NewGuid(),
            AlertType = "InvalidType",
            Title = "Test Report",
            Description = "Test report",
            Severity = AlertSeverity.Medium
        };

        _mockPlotCultivationRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<PlotCultivation, bool>>>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Alert type must be"));
    }

    [Fact]
    public async Task Handle_SupervisorUser_SetsCorrectSource()
    {
        // Arrange
        var supervisorUser = new Mock<IUser>();
        supervisorUser.Setup(u => u.Id).Returns(Guid.NewGuid());
        supervisorUser.Setup(u => u.Roles).Returns(new List<string> { "Supervisor" });

        var handler = new CreateEmergencyReportCommandHandler(
            _mockUnitOfWork.Object,
            supervisorUser.Object,
            _mockStorageService.Object,
            _mockLogger.Object);

        var plotCultivationId = Guid.NewGuid();
        var command = new CreateEmergencyReportCommand
        {
            PlotCultivationId = plotCultivationId,
            AlertType = "Pest",
            Title = "Supervisor Inspection Report",
            Description = "Inspection report",
            Severity = AlertSeverity.High
        };

        _mockPlotCultivationRepo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<PlotCultivation, bool>>>()))
            .ReturnsAsync(true);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockEmergencyReportRepo.Verify(r => r.AddAsync(It.Is<EmergencyReport>(
            report => report.Source == AlertSource.SupervisorInspection)), Times.Once);
    }
}
