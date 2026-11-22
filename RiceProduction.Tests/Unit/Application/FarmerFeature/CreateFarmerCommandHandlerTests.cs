using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.FarmerFeature.Command.CreateFarmer;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Tests.Fixtures;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.FarmerFeature;

/// <summary>
/// Tests for CreateFarmerCommandHandler - validates farmer creation logic
/// </summary>
public class CreateFarmerCommandHandlerTests
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IMediator> _mockMediator;

    public CreateFarmerCommandHandlerTests()
    {
        _mockMediator = new Mock<IMediator>();

        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private CreateFarmerCommandHandler CreateHandler(TestApplicationDbContext context) =>
        new(
            _mockUserManager.Object,
            context,
            Mock.Of<ILogger<CreateFarmerCommandHandler>>(),
            _mockMediator.Object);

    private void SetupUserManagerSuccess()
    {
        _mockUserManager.Setup(um => um.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<Farmer>(), It.IsAny<string>()))
            .Callback<ApplicationUser, string>((e, _) => { if (e.Id == Guid.Empty) e.Id = Guid.NewGuid(); })
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(um => um.AddToRoleAsync(It.IsAny<Farmer>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
    }

    [Fact]
    public async Task Handle_ValidFarmerData_CreatesSuccessfully()
    {
        await using var context = TestApplicationDbContext.Create();
        SetupUserManagerSuccess();
        var handler = CreateHandler(context);

        var command = new CreateFarmerCommand
        {
            FullName = "Nguyen Van A",
            FarmCode = "FARM001",
            PhoneNumber = "0901234567",
            Address = "123 Main St, District 1, HCMC"
        };

        var result = await handler.Handle(command, CancellationToken.None);

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
        await using var context = TestApplicationDbContext.Create();
        context.Farmers.Add(new Farmer
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "0901234567",
            FullName = "Existing Farmer"
        });
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);
        var command = new CreateFarmerCommand
        {
            FullName = "New Farmer",
            FarmCode = "FARM001",
            PhoneNumber = "0901234567"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("already exists"));
        _mockUserManager.Verify(um => um.CreateAsync(It.IsAny<Farmer>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MissingFullName_ReturnsFailure()
    {
        await using var context = TestApplicationDbContext.Create();
        var handler = CreateHandler(context);

        var command = new CreateFarmerCommand
        {
            FullName = "",
            FarmCode = "FARM002",
            PhoneNumber = "0901234567"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Full name is required"));
    }

    [Fact]
    public async Task Handle_MissingPhoneNumber_ReturnsFailure()
    {
        await using var context = TestApplicationDbContext.Create();
        var handler = CreateHandler(context);

        var command = new CreateFarmerCommand
        {
            FullName = "Test Farmer",
            FarmCode = "FARM002",
            PhoneNumber = ""
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Phone number is required"));
    }

    [Fact]
    public async Task Handle_WithOptionalFields_StoresAllData()
    {
        await using var context = TestApplicationDbContext.Create();
        SetupUserManagerSuccess();
        var handler = CreateHandler(context);

        var command = new CreateFarmerCommand
        {
            FullName = "Tran Van B",
            FarmCode = "FARM003",
            PhoneNumber = "0909876543",
            Address = "456 Rural Road, District 9"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _mockUserManager.Verify(um => um.CreateAsync(It.Is<Farmer>(
            f => f.Address == "456 Rural Road, District 9"), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UserManagerCreateFails_ReturnsFailure()
    {
        await using var context = TestApplicationDbContext.Create();
        _mockUserManager.Setup(um => um.FindByNameAsync("0901111111"))
            .ReturnsAsync((ApplicationUser?)null);
        _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<Farmer>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "User creation failed" }));

        var handler = CreateHandler(context);
        var command = new CreateFarmerCommand
        {
            FullName = "Test Farmer",
            FarmCode = "FARM004",
            PhoneNumber = "0901111111"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Failed to create farmer"));
    }

    [Fact]
    public async Task Handle_ValidPhoneNumberFormat_AcceptsVariousFormats()
    {
        await using var context = TestApplicationDbContext.Create();
        SetupUserManagerSuccess();
        var handler = CreateHandler(context);

        var command = new CreateFarmerCommand
        {
            FullName = "Le Thi C",
            FarmCode = "FARM005",
            PhoneNumber = "+84901234567"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _mockUserManager.Verify(um => um.CreateAsync(It.Is<Farmer>(
            f => f.PhoneNumber == "+84901234567"), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Handle_LongAddress_StoresFullAddress()
    {
        await using var context = TestApplicationDbContext.Create();
        SetupUserManagerSuccess();
        var handler = CreateHandler(context);

        var longAddress = "Building 5, Apartment 12B, 789 Long Street Name, Ward 15, District 10, Ho Chi Minh City, Vietnam";
        var command = new CreateFarmerCommand
        {
            FullName = "Vo Thi E",
            FarmCode = "FARM007",
            PhoneNumber = "0903333333",
            Address = longAddress
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _mockUserManager.Verify(um => um.CreateAsync(It.Is<Farmer>(
            f => f.Address == longAddress), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SpecialCharactersInName_StoresCorrectly()
    {
        await using var context = TestApplicationDbContext.Create();
        SetupUserManagerSuccess();
        var handler = CreateHandler(context);

        var command = new CreateFarmerCommand
        {
            FullName = "Nguyễn Văn Đức",
            FarmCode = "FARM008",
            PhoneNumber = "0904444444"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _mockUserManager.Verify(um => um.CreateAsync(It.Is<Farmer>(
            f => f.FullName == "Nguyễn Văn Đức"), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithPlots_CreatesPlotsToo()
    {
        await using var context = TestApplicationDbContext.Create();
        SetupUserManagerSuccess();
        var handler = CreateHandler(context);

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

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var plots = await context.Plots.ToListAsync();
        plots.Should().HaveCount(2);
        plots.Should().AllSatisfy(p => p.FarmerId.Should().Be(result.Data));
    }

    [Fact]
    public async Task Handle_WithClusterManagerId_AssignsClusterId()
    {
        await using var context = TestApplicationDbContext.Create();
        var clusterManagerId = Guid.NewGuid();
        var clusterId = Guid.NewGuid();
        context.ClusterManagers.Add(new ClusterManager { Id = clusterManagerId, ClusterId = clusterId });
        await context.SaveChangesAsync();

        SetupUserManagerSuccess();
        var handler = CreateHandler(context);

        var command = new CreateFarmerCommand
        {
            FullName = "Farmer in Cluster",
            FarmCode = "FARM010",
            PhoneNumber = "0906666666",
            ClusterManagerId = clusterManagerId
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _mockUserManager.Verify(um => um.CreateAsync(It.Is<Farmer>(
            f => f.ClusterId == clusterId), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RoleAssignmentFails_DeletesUserAndReturnsFailure()
    {
        await using var context = TestApplicationDbContext.Create();
        _mockUserManager.Setup(um => um.FindByNameAsync("0907777777"))
            .ReturnsAsync((ApplicationUser?)null);
        _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<Farmer>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(um => um.AddToRoleAsync(It.IsAny<Farmer>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Role assignment failed" }));
        _mockUserManager.Setup(um => um.DeleteAsync(It.IsAny<Farmer>()))
            .ReturnsAsync(IdentityResult.Success);

        var handler = CreateHandler(context);
        var command = new CreateFarmerCommand
        {
            FullName = "Test Farmer",
            FarmCode = "FARM011",
            PhoneNumber = "0907777777"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Failed to assign role"));
        _mockUserManager.Verify(um => um.DeleteAsync(It.IsAny<Farmer>()), Times.Once);
    }
}
