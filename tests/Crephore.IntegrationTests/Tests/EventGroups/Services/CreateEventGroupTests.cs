using Crephore.Api.Common.Exceptions;
using Crephore.Api.Features.EventGroups;
using Crephore.Api.Features.EventGroups.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.EventGroups.Services;

public class CreateEventGroupTests(DatabaseFixture fixture)
    : EventGroupsBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldCreateAnEmptyGroupForTheOwner()
    {
        var owner = await AppUserFactory.CreateAsync();

        var result = await EventGroupsService.CreateAsync(owner.Id, new CreateEventGroupRequestDto { Name = "  Family  " });

        result.Name.Should().Be("Family");
        result.EventCount.Should().Be(0);
        result.Events.Should().BeEmpty();
        (await Context.EventGroups.AsNoTracking().SingleAsync()).OwnerId.Should().Be(owner.Id);
    }

    [Fact]
    public async Task ShouldAllowTheSameNameForDifferentOwners()
    {
        var otherUser = await AppUserFactory.CreateAsync();
        await EventGroupFactory.CreateAsync(otherUser.Id, "Family");
        var owner = await AppUserFactory.CreateAsync();

        var result = await EventGroupsService.CreateAsync(owner.Id, new CreateEventGroupRequestDto { Name = "Family" });

        result.Name.Should().Be("Family");
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenTheOwnerAlreadyHasThatName()
    {
        var owner = await AppUserFactory.CreateAsync();
        await EventGroupFactory.CreateAsync(owner.Id, "Family");

        var act = async () => await EventGroupsService.CreateAsync(owner.Id, new CreateEventGroupRequestDto { Name = "family" });

        var exception = await act.Should().ThrowAsync<AppValidationException>();
        exception.Which.Message.Should().Be("Event group name already exists.");
        exception.Which.Errors.Should().ContainKey("name");
    }

    [Fact]
    public async Task ShouldRejectTheSecond_WhenTwoRequestsUseTheSameNameAtOnce()
    {
        var owner = await AppUserFactory.CreateAsync();
        var request = new CreateEventGroupRequestDto { Name = "Family" };

        var results = await RunConcurrentlyAsync<IEventGroupsService>(2, service => service.CreateAsync(owner.Id, request));

        // Whichever request loses gets the field error, never a 500 from the unique index
        results.Should().ContainSingle(exception => exception == null);
        results.Single(exception => exception != null).Should().BeOfType<AppValidationException>()
            .Which.Errors!["name"].Should().Contain("Event group name already exists.");
        (await Context.EventGroups.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ShouldPutTheSelectedEventsInTheGroup_MovingThemFromOtherGroups()
    {
        var owner = await AppUserFactory.CreateAsync();
        var oldGroup = await EventGroupFactory.CreateAsync(owner.Id, "Old");
        var moved = await EventFactory.CreateAsync(owner.Id, "Moved", eventGroupId: oldGroup.Id);
        var ungrouped = await EventFactory.CreateAsync(owner.Id, "Ungrouped");
        await EventFactory.CreateAsync(owner.Id, "Not selected");

        var result = await EventGroupsService.CreateAsync(owner.Id, new CreateEventGroupRequestDto
        {
            Name = "Family",
            EventIds = [moved.Id, ungrouped.Id, moved.Id],
        });

        result.EventCount.Should().Be(2);
        result.Events.Select(e => e.Name).Should().BeEquivalentTo(["Moved", "Ungrouped"]);
        (await Context.EventUsers.AsNoTracking().SingleAsync(eu => eu.EventId == moved.Id)).EventGroupId.Should().Be(result.Id);
    }

    [Fact]
    public async Task ShouldThrowValidationException_AndCreateNothing_WhenAnEventIsNotTheUsers()
    {
        var owner = await AppUserFactory.CreateAsync();
        var otherUser = await AppUserFactory.CreateAsync();
        var othersEvent = await EventFactory.CreateAsync(otherUser.Id);

        var act = async () => await EventGroupsService.CreateAsync(owner.Id, new CreateEventGroupRequestDto
        {
            Name = "Family",
            EventIds = [othersEvent.Id],
        });

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors.Should().ContainKey("eventIds");
        (await Context.EventGroups.AnyAsync()).Should().BeFalse();
    }
}
