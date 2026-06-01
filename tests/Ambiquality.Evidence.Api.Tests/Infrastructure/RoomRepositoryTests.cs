using Ambiquality.Evidence.Api.Application;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Common;
using Ambiquality.Evidence.Api.Domain.Rooms;
using Ambiquality.Evidence.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Ambiquality.Evidence.Api.Tests.Infrastructure;

[Collection("Database")]
public sealed class RoomRepositoryTests(PostgresFixture postgres)
{
    private async Task<(EvidenceDbContext context, IRoomRepository repo, Building building)> SetupAsync()
    {
        var services = new ServiceCollection();
        services.AddDbContext<EvidenceDbContext>(options =>
            options.UseNpgsql(postgres.ConnectionString,
                o => o.MigrationsHistoryTable("__EFMigrationsHistory", "evidence")));

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EvidenceDbContext>();

        var repository = new RoomRepository(context);

        // Seed a building for Room tests (using unique slug per test)
        var uniqueSlug = $"test-building-{Guid.NewGuid().ToString().Substring(0, 8)}";
        var building = Building.Register(
            slug: UriSlug.Create(uniqueSlug),
            ownerId: Guid.NewGuid(),
            createdBy: Guid.NewGuid(),
            name: "Test Building",
            address: Address.Create("Test St 1", "Test City", "12345", "CZ"),
            buildingTypeCode: "office",
            coordinates: null,
            anonymization: AnonymizationLevel.Precise,
            yearBuilt: 2020,
            yearRenovated: null,
            now: DateTime.UtcNow);

        context.Buildings.Add(building);
        await context.SaveChangesAsync();

        return (context, repository, building);
    }

    [Fact]
    public async Task RoundTripRoom_WithAllAttributes_PersistsAndRetrieves()
    {
        var (context, repository, building) = await SetupAsync();
        try
        {
            // Arrange
            var roomId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var recordedBy = Guid.NewGuid();

            var room = Room.Register(
                slug: UriSlug.Create("room-101"),
                buildingId: building.Id,
                createdBy: recordedBy,
                name: "Room 101",
                floor: FloorNumber.Create(1),
                functionCode: "office",
                exposureCode: "medium",
                areaM2: 25.5,
                ceilingHeightM: 3.2,
                ventilationType: "natural",
                pollutionSources: new[] { "traffic" },
                now: now);

            // Act
            repository.Add(room);
            await repository.SaveChangesAsync();

            // Assert - Retrieve by ID
            var retrieved = await repository.GetByIdAsync(room.Id);
            Assert.NotNull(retrieved);
            Assert.Equal(room.Id, retrieved.Id);
            var snapshot = retrieved.SnapshotAt(now);
            Assert.Equal("Room 101", snapshot.Name);
            Assert.Equal(1, snapshot.Floor);
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsRoom_WithinBuilding()
    {
        var (context, repository, building) = await SetupAsync();
        try
        {
            // Arrange
            var slug = UriSlug.Create("lab-202");
            var recordedBy = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var room = Room.Register(
                slug: slug,
                buildingId: building.Id,
                createdBy: recordedBy,
                name: "Lab 202",
                floor: FloorNumber.Create(1),
                functionCode: null,
                exposureCode: null,
                areaM2: null,
                ceilingHeightM: null,
                ventilationType: null,
                pollutionSources: Array.Empty<string>(),
                now: now);

            repository.Add(room);
            await repository.SaveChangesAsync();

            // Act
            var retrieved = await repository.GetBySlugAsync(building.Id, slug);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal("Lab 202", retrieved.SnapshotAt(now).Name);
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task DuplicateUriSlug_SameBuilding_ThrowsException()
    {
        var (context, repository, building) = await SetupAsync();
        try
        {
            // Arrange
            var slug = UriSlug.Create("conference-room");
            var recordedBy = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var room1 = Room.Register(
                slug: slug,
                buildingId: building.Id,
                createdBy: recordedBy,
                name: "Conference Room",
                floor: FloorNumber.Create(1),
                functionCode: null,
                exposureCode: null,
                areaM2: null,
                ceilingHeightM: null,
                ventilationType: null,
                pollutionSources: Array.Empty<string>(),
                now: now);

            repository.Add(room1);
            await repository.SaveChangesAsync();

            var room2 = Room.Register(
                slug: slug,
                buildingId: building.Id,
                createdBy: recordedBy,
                name: "Conference Room 2",
                floor: FloorNumber.Create(1),
                functionCode: null,
                exposureCode: null,
                areaM2: null,
                ceilingHeightM: null,
                ventilationType: null,
                pollutionSources: Array.Empty<string>(),
                now: DateTime.UtcNow.AddHours(1));

            repository.Add(room2);

            // Act & Assert
            await Assert.ThrowsAsync<DuplicateUriSlugException>(
                async () => await repository.SaveChangesAsync());
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task DuplicateUriSlug_AcrossBuildings_ThrowsException()
    {
        // Slugs are now globally unique, so the same slug in a *different*
        // building is also rejected (previously allowed under the per-building index).
        var (context, repository, building) = await SetupAsync();
        try
        {
            var otherBuilding = Building.Register(
                slug: UriSlug.Create($"other-building-{Guid.NewGuid().ToString()[..8]}"),
                ownerId: Guid.NewGuid(),
                createdBy: Guid.NewGuid(),
                name: "Other Building",
                address: Address.Create("Test St 2", "Test City", "12345", "CZ"),
                buildingTypeCode: "office",
                coordinates: null,
                anonymization: AnonymizationLevel.Precise,
                yearBuilt: 2020,
                yearRenovated: null,
                now: DateTime.UtcNow);
            context.Buildings.Add(otherBuilding);
            await context.SaveChangesAsync();

            var slug = UriSlug.Create("shared-slug");
            var recordedBy = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var room1 = Room.Register(
                slug: slug,
                buildingId: building.Id,
                createdBy: recordedBy,
                name: "Room in building 1",
                floor: FloorNumber.Create(1),
                functionCode: null,
                exposureCode: null,
                areaM2: null,
                ceilingHeightM: null,
                ventilationType: null,
                pollutionSources: Array.Empty<string>(),
                now: now);

            repository.Add(room1);
            await repository.SaveChangesAsync();

            var room2 = Room.Register(
                slug: slug,
                buildingId: otherBuilding.Id,
                createdBy: recordedBy,
                name: "Room in building 2",
                floor: FloorNumber.Create(1),
                functionCode: null,
                exposureCode: null,
                areaM2: null,
                ceilingHeightM: null,
                ventilationType: null,
                pollutionSources: Array.Empty<string>(),
                now: now);

            repository.Add(room2);

            await Assert.ThrowsAsync<DuplicateUriSlugException>(
                async () => await repository.SaveChangesAsync());
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task SnapshotAt_AsOfQuery_ReturnsCorrectVersion()
    {
        var (context, repository, building) = await SetupAsync();
        try
        {
            // Arrange
            var time1 = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc);
            var time2 = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);
            var recordedBy = Guid.NewGuid();

            var room = Room.Register(
                slug: UriSlug.Create("room-a"),
                buildingId: building.Id,
                createdBy: recordedBy,
                name: "Original Name",
                floor: FloorNumber.Create(1),
                functionCode: null,
                exposureCode: null,
                areaM2: null,
                ceilingHeightM: null,
                ventilationType: null,
                pollutionSources: Array.Empty<string>(),
                now: time1);

            repository.Add(room);
            await repository.SaveChangesAsync();

            // Simulate time passing and change name
            room.ChangeName("Updated Name", time2, recordedBy);
            await repository.SaveChangesAsync();

            // Act - Query at specific times
            var retrieved = await repository.GetByIdAsync(room.Id);
            var snapshotAtTime1 = retrieved!.SnapshotAt(time1);
            var snapshotAtTime2 = retrieved.SnapshotAt(time2);

            // Assert
            Assert.Equal("Original Name", snapshotAtTime1.Name);
            Assert.Equal("Updated Name", snapshotAtTime2.Name);
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddPollutionSource_OpensRange()
    {
        var (context, repository, building) = await SetupAsync();
        try
        {
            // Arrange
            var validFrom = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc);
            var recordedBy = Guid.NewGuid();

            var room = Room.Register(
                slug: UriSlug.Create("room-b"),
                buildingId: building.Id,
                createdBy: recordedBy,
                name: "Room B",
                floor: FloorNumber.Create(1),
                functionCode: null,
                exposureCode: null,
                areaM2: null,
                ceilingHeightM: null,
                ventilationType: null,
                pollutionSources: Array.Empty<string>(),
                now: validFrom);

            repository.Add(room);
            await repository.SaveChangesAsync();

            // Act - Add pollution source
            room.AddPollutionSource("traffic", validFrom);
            await repository.SaveChangesAsync();

            // Assert
            var retrieved = await repository.GetByIdAsync(room.Id);
            var snapshot = retrieved!.SnapshotAt(validFrom);
            Assert.Contains("traffic", snapshot.PollutionSources);
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task RemovePollutionSource_ClosesRange()
    {
        var (context, repository, building) = await SetupAsync();
        try
        {
            // Arrange
            var addTime = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc);
            var removeTime = new DateTime(2026, 5, 20, 14, 0, 0, DateTimeKind.Utc);
            var recordedBy = Guid.NewGuid();

            var room = Room.Register(
                slug: UriSlug.Create("room-c"),
                buildingId: building.Id,
                createdBy: recordedBy,
                name: "Room C",
                floor: FloorNumber.Create(1),
                functionCode: null,
                exposureCode: null,
                areaM2: null,
                ceilingHeightM: null,
                ventilationType: null,
                pollutionSources: new[] { "traffic" },
                now: addTime);

            repository.Add(room);
            await repository.SaveChangesAsync();

            // Act - Remove pollution source
            room.RemovePollutionSource("traffic", removeTime);
            await repository.SaveChangesAsync();

            // Assert
            var retrieved = await repository.GetByIdAsync(room.Id);
            var snapshotBefore = retrieved!.SnapshotAt(addTime);
            var snapshotAfter = retrieved.SnapshotAt(removeTime.AddSeconds(1));

            Assert.Contains("traffic", snapshotBefore.PollutionSources);
            Assert.DoesNotContain("traffic", snapshotAfter.PollutionSources);
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task ChangeName_AppliesCleanlyUnderExclusionConstraint()
    {
        var (context, repository, building) = await SetupAsync();
        try
        {
            var t0 = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc);
            var t1 = new DateTime(2026, 5, 20, 11, 0, 0, DateTimeKind.Utc);
            var t2 = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);
            var recordedBy = Guid.NewGuid();

            var room = Room.Register(
                slug: UriSlug.Create("room-constraint"),
                buildingId: building.Id,
                createdBy: recordedBy,
                name: "Name 0",
                floor: FloorNumber.Create(1),
                functionCode: null,
                exposureCode: null,
                areaM2: null,
                ceilingHeightM: null,
                ventilationType: null,
                pollutionSources: Array.Empty<string>(),
                now: t0);

            repository.Add(room);
            await repository.SaveChangesAsync();

            // Each change closes the open row (UPDATE) and opens a new one (INSERT)
            // in one SaveChanges. With half-open ranges and the DEFERRABLE GiST
            // constraint this must commit without an overlap violation.
            room.ChangeName("Name 1", t1, recordedBy);
            await repository.SaveChangesAsync();
            room.ChangeName("Name 2", t2, recordedBy);
            await repository.SaveChangesAsync();

            var retrieved = await repository.GetByIdAsync(room.Id);
            Assert.Equal("Name 0", retrieved!.SnapshotAt(t0).Name);
            Assert.Equal("Name 1", retrieved.SnapshotAt(t1).Name);
            Assert.Equal("Name 2", retrieved.SnapshotAt(t2).Name);
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task OverlappingNameHistory_IsRejectedByExclusionConstraint()
    {
        var (context, repository, building) = await SetupAsync();
        try
        {
            var now = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc);
            var recordedBy = Guid.NewGuid();

            var room = Room.Register(
                slug: UriSlug.Create("room-overlap"),
                buildingId: building.Id,
                createdBy: recordedBy,
                name: "Original",
                floor: FloorNumber.Create(1),
                functionCode: null,
                exposureCode: null,
                areaM2: null,
                ceilingHeightM: null,
                ventilationType: null,
                pollutionSources: Array.Empty<string>(),
                now: now);

            repository.Add(room);
            await repository.SaveChangesAsync();

            // The room already has an open [now, +inf) name row. Inserting a second
            // open row for the same room, bypassing the aggregate, must be rejected
            // by the no-overlap exclusion constraint (SqlState 23P01).
            var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO evidence.room_name_history " +
                    "(room_id, recorded_at, validity, name, recorded_by) " +
                    "VALUES ({0}, {1}, tstzrange({2}, NULL, '[)'), {3}, {4})",
                    room.Id, now.AddMinutes(1), now.AddMinutes(1), "Overlap", recordedBy));

            Assert.Equal("23P01", ex.SqlState);
        }
        finally
        {
            await context.DisposeAsync();
        }
    }
}
