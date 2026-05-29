using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Application.Rooms;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Common;
using Ambiquality.Evidence.Api.Domain.Rooms;
using Ambiquality.Evidence.Api.Tests.TestSupport;
using NSubstitute;

namespace Ambiquality.Evidence.Api.Tests.Application.Rooms;

public class RegisterRoomHandlerTests
{
    private static readonly Guid BuildingId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private static IBuildingRepository BuildingRepoOwnedBy(Guid ownerId)
    {
        var building = Building.Register(
            slug: UriSlug.Create("owned-building"),
            ownerId: ownerId,
            createdBy: ownerId,
            name: "Owned Building",
            address: Address.Create("Street 1", "City", "12345", "CZ"),
            buildingTypeCode: "office",
            coordinates: null,
            anonymization: AnonymizationLevel.Precise,
            yearBuilt: null,
            yearRenovated: null,
            now: Now);

        var repo = Substitute.For<IBuildingRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(building);
        return repo;
    }

    [Fact]
    public async Task Handle_RegistersRoomAndSavesChanges()
    {
        var command = new RegisterRoomCommand(
            BuildingId: BuildingId,
            Name: "Room 101",
            Floor: 1,
            FunctionCode: "office",
            ExposureCode: null,
            AreaM2: 25.0,
            CeilingHeightM: 2.8,
            VentilationType: "vzt",
            PollutionSources: []);

        var mockClock = Substitute.For<IClock>();
        mockClock.UtcNow.Returns(Now);

        var mockCurrentUser = Substitute.For<ICurrentUser>();
        mockCurrentUser.ProjectionId.Returns(UserId);

        var mockRepo = Substitute.For<IRoomRepository>();

        var handler = new RegisterRoomHandler(mockClock, mockCurrentUser, mockRepo, BuildingRepoOwnedBy(UserId), new StubSlugGenerator());
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.RoomId);
        Assert.NotEmpty(result.UriSlug);

        mockRepo.Received(1).Add(Arg.Any<Room>());
        await mockRepo.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WithPollutionSources_AddsSourcesToRoom()
    {
        var command = new RegisterRoomCommand(
            BuildingId: BuildingId,
            Name: "Room 101",
            Floor: 1,
            FunctionCode: "kitchen",
            ExposureCode: null,
            AreaM2: null,
            CeilingHeightM: null,
            VentilationType: null,
            PollutionSources: ["cooking", "traffic"]);

        var mockClock = Substitute.For<IClock>();
        mockClock.UtcNow.Returns(Now);

        var mockCurrentUser = Substitute.For<ICurrentUser>();
        mockCurrentUser.ProjectionId.Returns(UserId);

        Room? capturedRoom = null;
        var mockRepo = Substitute.For<IRoomRepository>();
        mockRepo.When(r => r.Add(Arg.Any<Room>()))
            .Do(info => capturedRoom = info.Arg<Room>());

        var handler = new RegisterRoomHandler(mockClock, mockCurrentUser, mockRepo, BuildingRepoOwnedBy(UserId), new StubSlugGenerator());
        await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(capturedRoom);
        Assert.Equal(2, capturedRoom.PollutionSourceHistory.Count);
    }
}
