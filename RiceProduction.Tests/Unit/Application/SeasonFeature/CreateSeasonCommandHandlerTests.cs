using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RiceProduction.Application.SeasonFeature.Commands.CreateSeason;
using RiceProduction.Domain.Entities;
using RiceProduction.Tests.Fixtures;
using Xunit;

namespace RiceProduction.Tests.Unit.Application.SeasonFeature;

/// <summary>
/// Tests for CreateSeasonCommandHandler - validates season creation
/// </summary>
public class CreateSeasonCommandHandlerTests
{
    private static CreateSeasonCommandHandler CreateHandler(
        TestApplicationDbContext context,
        Mock<IMediator>? mockMediator = null) =>
        new(
            context,
            (mockMediator ?? new Mock<IMediator>()).Object,
            Mock.Of<ILogger<CreateSeasonCommandHandler>>());

    [Fact]
    public async Task Handle_ValidSeason_CreatesSuccessfully()
    {
        await using var context = TestApplicationDbContext.Create();
        var handler = CreateHandler(context);
        var command = new CreateSeasonCommand
        {
            SeasonName = "Winter-Spring",
            StartDate = "1/1",
            EndDate = "5/31"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);
        var created = await context.Seasons.SingleOrDefaultAsync(s => s.Id == result.Data);
        created.Should().NotBeNull();
        created!.SeasonName.Should().Be("Winter-Spring");
        created.StartDate.Should().Be("1/1");
        created.EndDate.Should().Be("5/31");
    }

    [Fact]
    public async Task Handle_SummerAutumnSeason_CreatesSuccessfully()
    {
        await using var context = TestApplicationDbContext.Create();
        var handler = CreateHandler(context);
        var command = new CreateSeasonCommand
        {
            SeasonName = "Summer-Autumn",
            StartDate = "6/1",
            EndDate = "9/30",
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AutumnWinterSeason_CreatesSuccessfully()
    {
        await using var context = TestApplicationDbContext.Create();
        var handler = CreateHandler(context);
        var command = new CreateSeasonCommand
        {
            SeasonName = "Autumn-Winter",
            StartDate = "10/1",
            EndDate = "12/31"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DuplicateSeasonName_ReturnsFailure()
    {
        await using var context = TestApplicationDbContext.Create();
        context.Seasons.Add(new Season
        {
            Id = Guid.NewGuid(),
            SeasonName = "Winter-Spring",
            StartDate = "1/1",
            EndDate = "5/31"
        });
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);
        var command = new CreateSeasonCommand
        {
            SeasonName = "Winter-Spring",
            StartDate = "1/1",
            EndDate = "5/31"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("already exists") || e.Contains("duplicate"));
    }

    [Fact]
    public async Task Handle_SeasonWithSeasonType_StoresSeasonType()
    {
        await using var context = TestApplicationDbContext.Create();
        var handler = CreateHandler(context);
        var command = new CreateSeasonCommand
        {
            SeasonName = "Detailed Season",
            StartDate = "3/1",
            EndDate = "7/31",
            SeasonType = "Main Crop"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var created = await context.Seasons.SingleOrDefaultAsync(s => s.Id == result.Data);
        created!.SeasonType.Should().Be("Main Crop");
    }

    [Fact]
    public async Task Handle_SeasonCrossYearBoundary_CreatesSuccessfully()
    {
        await using var context = TestApplicationDbContext.Create();
        var handler = CreateHandler(context);
        var command = new CreateSeasonCommand
        {
            SeasonName = "Year-End Season",
            StartDate = "11/1",
            EndDate = "2/28"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DatabaseError_ReturnsFailure()
    {
        await using var context = TestApplicationDbContext.Create();
        context.SaveChangesException = new Exception("Database connection error");
        var handler = CreateHandler(context);
        var command = new CreateSeasonCommand
        {
            SeasonName = "Test Season",
            StartDate = "1/1",
            EndDate = "3/31"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Failed to create season"));
    }

    [Fact]
    public async Task Handle_VietnameseSeasonName_CreatesCorrectly()
    {
        await using var context = TestApplicationDbContext.Create();
        var handler = CreateHandler(context);
        var command = new CreateSeasonCommand
        {
            SeasonName = "Vụ Đông Xuân",
            StartDate = "12/1",
            EndDate = "4/30"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SeasonWithoutDescription_CreatesSuccessfully()
    {
        await using var context = TestApplicationDbContext.Create();
        var handler = CreateHandler(context);
        var command = new CreateSeasonCommand
        {
            SeasonName = "Quick Season",
            StartDate = "5/1",
            EndDate = "7/31"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_CreatedSeason_SetsIsActiveToTrue()
    {
        await using var context = TestApplicationDbContext.Create();
        var handler = CreateHandler(context);
        var command = new CreateSeasonCommand
        {
            SeasonName = "Active Season",
            StartDate = "1/1",
            EndDate = "4/30"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var created = await context.Seasons.SingleOrDefaultAsync(s => s.Id == result.Data);
        created.Should().NotBeNull();
        created!.IsActive.Should().BeTrue();
    }
}
