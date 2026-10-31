using Crephore.Api.Common.Exceptions;
using Crephore.Api.Features.EventGroups.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.EventGroups.Services;

public class UpdateEventGroupTests(DatabaseFixture fixture)
    : EventGroupsBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldRenameTheGroupAndKeepItsEvents()
    {
        var owner = await AppUserFactory.CreateAsync();
        var group = await EventGroupFactory.CreateAsync(owner.Id, "Family");
        await EventFactory.CreateAsync(owner.Id, eventGroupId: group.Id);

        var result = await EventGroupsService.UpdateAsync(group.Id, owner.Id, new UpdateEventGroupRequestDto { Name = "Relatives" });

        result.Name.Should().Be("Relatives");
        result.EventCount.Should().Be(1);
        (await Context.EventGroups.AsNoTracking().SingleAsync()).Name.Should().Be("Relatives");
    }

    [Fact]
    public async Task ShouldAllowKeepingTheSameName()
    {
        var owner = await AppUserFactory.CreateAsync();
        var group = await EventGroupFactory.CreateAsync(owner.Id, "Family");

        var result = await EventGroupsService.UpdateAsync(group.Id, owner.Id, new UpdateEventGroupRequestDto { Name = "Family" });

        result.Name.Should().Be("Family");
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenAnotherGroupHasThatName()
    {
        var owner = await AppUserFactory.CreateAsync();
        await EventGroupFactory.CreateAsync(owner.Id, "Family");
        var work = await EventGroupFactory.CreateAsync(owner.Id, "Work");

        var act = async () => await EventGroupsService.UpdateAsync(work.Id, owner.Id, new UpdateEventGroupRequestDto { Name = "Family" });

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors.Should().ContainKey("name");
    }

    [Fact]
    public async Task ShouldThrowNotFoundException_WhenGroupBelongsToAnotherUser()
    {
        var owner = await AppUserFactory.CreateAsync();
        var otherUser = await AppUserFactory.CreateAsync();
        var group = await EventGroupFactory.CreateAsync(owner.Id);

        var act = async () => await EventGroupsService.UpdateAsync(group.Id, otherUser.Id, new UpdateEventGroupRequestDto { Name = "Mine" });

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Message.Should().Be("Event group not found.");
    }

    [Fact]
    public async Task ShouldMakeTheGroupContainExactlyTheSelectedEvents()
    {
        var owner = await AppUserFactory.CreateAsync();
        var group = await EventGroupFactory.CreateAsync(owner.Id, "Family");
        var kept = await EventFactory.CreateAsync(owner.Id, "Kept", eventGroupId: group.Id);
        var removed = await EventFactory.CreateAsync(owner.Id, "Removed", eventGroupId: group.Id);
        var added = await EventFactory.CreateAsync(owner.Id, "Added");

        var result = await EventGroupsService.UpdateAsync(group.Id, owner.Id, new UpdateEventGroupRequestDto
        {
            Name = "Family",
            EventIds = [kept.Id, added.Id],
        });

        result.Events.Select(e => e.Name).Should().BeEquivalentTo(["Kept", "Added"]);
        // Removed from the group, not deleted
        (await Context.EventUsers.AsNoTracking().SingleAsync(eu => eu.EventId == removed.Id)).EventGroupId.Should().BeNull();
    }

    [Fact]
    public async Task ShouldKeepTheGroupsEvents_WhenEventIdsIsNotSent()
    {
        var owner = await AppUserFactory.CreateAsync();
        var group = await EventGroupFactory.CreateAsync(owner.Id, "Family");
        await EventFactory.CreateAsync(owner.Id, eventGroupId: group.Id);

        var result = await EventGroupsService.UpdateAsync(group.Id, owner.Id, new UpdateEventGroupRequestDto { Name = "Relatives" });

        result.Name.Should().Be("Relatives");
        result.EventCount.Should().Be(1);
    }

    [Fact]
    public async Task ShouldEmptyTheGroup_WhenEventIdsIsEmpty()
    {
        var owner = await AppUserFactory.CreateAsync();
        var group = await EventGroupFactory.CreateAsync(owner.Id, "Family");
        await EventFactory.CreateAsync(owner.Id, eventGroupId: group.Id);

        var result = await EventGroupsService.UpdateAsync(group.Id, owner.Id, new UpdateEventGroupRequestDto { Name = "Family", EventIds = [] });

        result.EventCount.Should().Be(0);
    }
}
