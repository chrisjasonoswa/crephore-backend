using Crephore.Api.Features.EventGroups.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.EventGroups.Services;

public class ListEventGroupsTests(DatabaseFixture fixture)
    : EventGroupsBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldReturnOnlyTheOwnersGroups_WithTheirEventsByStartDate()
    {
        var owner = await AppUserFactory.CreateAsync();
        var otherUser = await AppUserFactory.CreateAsync();
        var group = await EventGroupFactory.CreateAsync(owner.Id, "Family");
        var later = await EventFactory.CreateAsync(owner.Id, "Later", eventGroupId: group.Id, startsAt: new DateTimeOffset(2027, 12, 1, 0, 0, 0, TimeSpan.Zero));
        var sooner = await EventFactory.CreateAsync(owner.Id, "Sooner", eventGroupId: group.Id, startsAt: new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await EventGroupFactory.CreateAsync(otherUser.Id, "Not mine");

        var result = await EventGroupsService.ListAsync(owner.Id, new ListEventGroupsRequestDto());

        var dto = result.Items.Single();
        dto.Name.Should().Be("Family");
        dto.EventCount.Should().Be(2);
        dto.Events.Select(e => e.Id).Should().Equal(sooner.Id, later.Id);
    }

    [Fact]
    public async Task ShouldSortByNameByDefault()
    {
        var owner = await AppUserFactory.CreateAsync();
        await EventGroupFactory.CreateAsync(owner.Id, "Work");
        await EventGroupFactory.CreateAsync(owner.Id, "Family");

        var result = await EventGroupsService.ListAsync(owner.Id, new ListEventGroupsRequestDto());

        result.Items.Select(g => g.Name).Should().Equal("Family", "Work");
    }

    [Fact]
    public async Task ShouldFilterBySearchKey()
    {
        var owner = await AppUserFactory.CreateAsync();
        await EventGroupFactory.CreateAsync(owner.Id, "Family Reunions");
        await EventGroupFactory.CreateAsync(owner.Id, "Work");

        var result = await EventGroupsService.ListAsync(owner.Id, new ListEventGroupsRequestDto { SearchKey = "family" });

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Family Reunions");
    }

    [Fact]
    public async Task ShouldApplyPaginationAndSortByEventCount()
    {
        var owner = await AppUserFactory.CreateAsync();
        var busy = await EventGroupFactory.CreateAsync(owner.Id, "Busy");
        await EventGroupFactory.CreateAsync(owner.Id, "Empty");
        await EventFactory.CreateAsync(owner.Id, eventGroupId: busy.Id);
        await EventFactory.CreateAsync(owner.Id, eventGroupId: busy.Id);

        var result = await EventGroupsService.ListAsync(owner.Id, new ListEventGroupsRequestDto
        {
            Page = 1,
            PageSize = 1,
            Sort = ["eventCount:desc"],
        });

        result.TotalCount.Should().Be(2);
        result.Items.Single().Name.Should().Be("Busy");
        result.HasNextPage.Should().BeTrue();
    }
}
