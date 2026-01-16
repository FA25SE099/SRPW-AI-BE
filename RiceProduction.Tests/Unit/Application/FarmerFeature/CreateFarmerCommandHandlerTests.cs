using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.FarmerFeature.Command.CreateFarmer;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using Xunit;
using MediatR;
using RiceProduction.Infrastructure.Repository;

namespace RiceProduction.Tests.Unit.Application.FarmerFeature;

/// <summary>
/// Tests for CreateFarmerCommandHandler - validates farmer creation logic
/// </summary>
public class CreateFarmerCommandHandlerTests
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IFarmerRepository> _mockFarmerRepo;
    private readonly Mock<IClusterManagerRepository> _mockClusterManagerRepo;
    private readonly Mock<IGenericRepository<Plot>> _mockPlotRepo;
    private readonly Mock<ILogger<CreateFarmerCommandHandler>> _mockLogger;
    private readonly Mock<IMediator> _mockMediator;
    private readonly CreateFarmerCommandHandler _handler;

    public CreateFarmerCommandHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockFarmerRepo = new Mock<IFarmerRepository>();
        _mockClusterManagerRepo = new Mock<IClusterManagerRepository>();
        _mockPlotRepo = new Mock<IGenericRepository<Plot>>();
        _mockLogger = new Mock<ILogger<CreateFarmerCommandHandler>>();
        _mockMediator = new Mock<IMediator>();

        // Setup UserManager mock (requires special setup for constructor)
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);

        _mockUnitOfWork.Setup(u => u.FarmerRepository).Returns(_mockFarmerRepo.Object);
        _mockUnitOfWork.Setup(u => u.ClusterManagerRepository).Returns(_mockClusterManagerRepo.Object);
        _mockUnitOfWork.Setup(u => u.Repository<Plot>()).Returns(_mockPlotRepo.Object);

        _handler = new CreateFarmerCommandHandler(
            _mockUserManager.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object,
            _mockMediator.Object);
    }

    [Fact]
    public async Task Handle_ValidFarmerData_CreatesSuccessfully()
    {
        // Arrange
        var command = new CreateFarmerCommand
        {
            FullName = "Nguyen Van A",
            FarmCode = "FARM001",
            PhoneNumber = "0901234567",
            Address = "123 Main St, District 1, HCMC"
        };

        _mockFarmerRepo.Setup(r => r.GetFarmerByPhoneNumber(command.PhoneNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Farmer?)null);

        _mockUserManager.Setup(um => um.FindByNameAsync(command.PhoneNumber))
            .ReturnsAsync((ApplicationUser?)null);

        _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<Farmer>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(um => um.AddToRoleAsync(It.IsAny<Farmer>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);
        _mockUserManager.Verify(um => um.CreateAsync(It.Is<Farmer>(
            f => f.FullName == "Nguyen Van A" && 
                 f.FarmCode == "FARM001" &&
                 f.PhoneNumber == command.PhoneNumber), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicatePhoneNumber_ReturnsFailure()
    {
        // Arrange
        var existingFarmer = new Farmer
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "0901234567",
            FullName = "Existing Farmer"
        };

        _mockFarmerRepo.Setup(r => r.GetFarmerByPhoneNumber("0901234567", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFarmer);

        var command = new CreateFarmerCommand
        {
            FullName = "New Farmer",
            FarmCode = "FARM001",
            PhoneNumber = "0901234567"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("already exists"));
        _mockUserManager.Verify(um => um.CreateAsync(It.IsAny<Farmer>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MissingFullName_ReturnsFailure()
    {
        // Arrange
        var command = new CreateFarmerCommand
        {
            FullName = "",
            FarmCode = "FARM002",
            PhoneNumber = "0901234567"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Full name is required"));
    }

    [Fact]
    public async Task Handle_MissingPhoneNumber_ReturnsFailure()
    {
        // Arrange
        var command = new CreateFarmerCommand
        {
            FullName = "Test Farmer",
            FarmCode = "FARM002",
            PhoneNumber = ""
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Phone number is required"));
    }

    [Fact]
    public async Task Handle_WithOptionalFields_StoresAllData()
    {
        // Arrange
        var command = new CreateFarmerCommand
        {
            FullName = "Tran Van B",
            FarmCode = "FARM003",
            PhoneNumber = "0909876543",
            Address = "456 Rural Road, District 9"
        };

        _mockFarmerRepo.Setup(r => r.GetFarmerByPhoneNumber(command.PhoneNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Farmer?)null);

        _mockUserManager.Setup(um => um.FindByNameAsync(command.PhoneNumber))
            .ReturnsAsync((ApplicationUser?)null);

        _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<Farmer>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(um => um.AddToRoleAsync(It.IsAny<Farmer>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockUserManager.Verify(um => um.CreateAsync(It.Is<Farmer>(
            f => f.Address == "456 Rural Road, District 9"), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UserManagerCreateFails_ReturnsFailure()
    {
        // Arrange
        var command = new CreateFarmerCommand
        {
            FullName = "Test Farmer",
            FarmCode = "FARM004",
            PhoneNumber = "0901111111"
        };

        _mockFarmerRepo.Setup(r => r.GetFarmerByPhoneNumber(command.PhoneNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Farmer?)null);

        _mockUserManager.Setup(um => um.FindByNameAsync(command.PhoneNumber))
            .ReturnsAsync((ApplicationUser?)null);

        _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<Farmer>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "User creation failed" }));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Failed to create farmer"));
    }

    [Fact]
    public async Task Handle_ValidPhoneNumberFormat_AcceptsVariousFormats()
    {
        // Arrange
        var command = new CreateFarmerCommand
        {
            FullName = "Le Thi C",
            FarmCode = "FARM005",
            PhoneNumber = "+84901234567"
        };

        _mockFarmerRepo.Setup(r => r.GetFarmerByPhoneNumber(command.PhoneNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Farmer?)null);

        _mockUserManager.Setup(um => um.FindByNameAsync(command.PhoneNumber))
            .ReturnsAsync((ApplicationUser?)null);

        _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<Farmer>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(um => um.AddToRoleAsync(It.IsAny<Farmer>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockUserManager.Verify(um => um.CreateAsync(It.Is<Farmer>(
            f => f.PhoneNumber == "+84901234567"), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Handle_LongAddress_StoresFullAddress()
    {
        // Arrange
        var longAddress = "Building 5, Apartment 12B, 789 Long Street Name, Ward 15, District 10, Ho Chi Minh City, Vietnam";
        var command = new CreateFarmerCommand
        {
            FullName = "Vo Thi E",
            FarmCode = "FARM007",
            PhoneNumber = "0903333333",
            Address = longAddress
        };

        _mockFarmerRepo.Setup(r => r.GetFarmerByPhoneNumber(command.PhoneNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Farmer?)null);

        _mockUserManager.Setup(um => um.FindByNameAsync(command.PhoneNumber))
            .ReturnsAsync((ApplicationUser?)null);

        _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<Farmer>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(um => um.AddToRoleAsync(It.IsAny<Farmer>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockUserManager.Verify(um => um.CreateAsync(It.Is<Farmer>(
            f => f.Address == longAddress), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SpecialCharactersInName_StoresCorrectly()
    {
        // Arrange
        var command = new CreateFarmerCommand
        {
            FullName = "Nguyễn Văn Đức",
            FarmCode = "FARM008",
            PhoneNumber = "0904444444"
        };

        _mockFarmerRepo.Setup(r => r.GetFarmerByPhoneNumber(command.PhoneNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Farmer?)null);

        _mockUserManager.Setup(um => um.FindByNameAsync(command.PhoneNumber))
            .ReturnsAsync((ApplicationUser?)null);

        _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<Farmer>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(um => um.AddToRoleAsync(It.IsAny<Farmer>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockUserManager.Verify(um => um.CreateAsync(It.Is<Farmer>(
            f => f.FullName == "Nguyễn Văn Đức"), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithPlots_CreatesPlotsToo()
    {
        // Arrange
        var command = new CreateFarmerCommand
        {
            FullName = "Farmer with Plots",
            FarmCode = "FARM009",
            PhoneNumber = "0905555555",
            ClusterManagerId = Guid.NewGuid(),
            Plots = new List<PlotCreationData>
            {
                new PlotCreationData { SoThua = 1, SoTo = 1, PlotArea = 1000m, SoilType = "Clay" },
                new PlotCreationData { SoThua = 2, SoTo = 1, PlotArea = 1500m, SoilType = "Loam" }
            }
        };

        _mockFarmerRepo.Setup(r => r.GetFarmerByPhoneNumber(command.PhoneNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Farmer?)null);

        _mockUserManager.Setup(um => um.FindByNameAsync(command.PhoneNumber))
            .ReturnsAsync((ApplicationUser?)null);

        _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<Farmer>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(um => um.AddToRoleAsync(It.IsAny<Farmer>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockPlotRepo.Setup(r => r.AddAsync(It.IsAny<Plot>()))
            .Returns(Task.CompletedTask);

        _mockPlotRepo.Setup(r => r.ListAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Plot, bool>>>(), null, null))
            .ReturnsAsync(new List<Plot>());

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockPlotRepo.Verify(r => r.AddAsync(It.IsAny<Plot>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_WithClusterManagerId_AssignsClusterId()
    {
        // Arrange
        var clusterManagerId = Guid.NewGuid();
        var clusterId = Guid.NewGuid();
        var clusterManager = new ClusterManager { Id = clusterManagerId, ClusterId = clusterId };

        var command = new CreateFarmerCommand
        {
            FullName = "Farmer in Cluster",
            FarmCode = "FARM010",
            PhoneNumber = "0906666666",
            ClusterManagerId = clusterManagerId
        };

        _mockFarmerRepo.Setup(r => r.GetFarmerByPhoneNumber(command.PhoneNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Farmer?)null);

        _mockUserManager.Setup(um => um.FindByNameAsync(command.PhoneNumber))
            .ReturnsAsync((ApplicationUser?)null);

        _mockClusterManagerRepo.Setup(r => r.GetClusterManagerByIdAsync(clusterManagerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clusterManager);

        _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<Farmer>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(um => um.AddToRoleAsync(It.IsAny<Farmer>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockUserManager.Verify(um => um.CreateAsync(It.Is<Farmer>(
            f => f.ClusterId == clusterId), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RoleAssignmentFails_DeletesUserAndReturnsFailure()
    {
        // Arrange
        var command = new CreateFarmerCommand
        {
            FullName = "Test Farmer",
            FarmCode = "FARM011",
            PhoneNumber = "0907777777"
        };

        _mockFarmerRepo.Setup(r => r.GetFarmerByPhoneNumber(command.PhoneNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Farmer?)null);

        _mockUserManager.Setup(um => um.FindByNameAsync(command.PhoneNumber))
            .ReturnsAsync((ApplicationUser?)null);

        _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<Farmer>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(um => um.AddToRoleAsync(It.IsAny<Farmer>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Role assignment failed" }));

        _mockUserManager.Setup(um => um.DeleteAsync(It.IsAny<Farmer>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Failed to assign role"));
        _mockUserManager.Verify(um => um.DeleteAsync(It.IsAny<Farmer>()), Times.Once);
    }
}
