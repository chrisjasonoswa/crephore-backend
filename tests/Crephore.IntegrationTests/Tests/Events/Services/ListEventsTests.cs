using Crephore.Api.Features.Events.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.Events.Services;

public class ListEventsTests(DatabaseFixture fixture)
    : EventsBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldReturnOnlyTheOwnersEvents_WithGroupAndTimelineInOrder()
    {
        var owner = await AppUserFactory.CreateAsync();
        var otherUser = await AppUserFactory.CreateAsync();
        var group = await EventGroupFactory.CreateAsync(owner.Id, "Family");
        var birthday = await EventFactory.CreateAsync(owner.Id, "Birthday", eventGroupId: group.Id, timeline: ["Opening", "Dinner", "First dance"]);
        await EventFactory.CreateAsync(otherUser.Id, "Not mine");

        var result = await EventsService.ListAsync(owner.Id, new ListEventsRequestDto());

        result.TotalCount.Should().Be(1);
        var dto = result.Items.Single();
        dto.Id.Should().Be(birthday.Id);
        dto.EventGroup!.Name.Should().Be("Family");
        dto.Timeline.Should().Equal("Opening", "Dinner", "First dance");
    }

    [Fact]
    public async Task ShouldIncludeEventsSharedWithTheUser()
    {
        var owner = await AppUserFactory.CreateAsync();
        var member = await AppUserFactory.CreateAsync();
        var shared = await EventFactory.CreateAsync(owner.Id, "Shared");
        await EventFactory.CreateAsync(owner.Id, "Not shared");
        await EventUserFactory.CreateAsync(shared.Id, member.Id);

        var result = await EventsService.ListAsync(member.Id, new ListEventsRequestDto());

        result.Items.Select(e => e.Name).Should().Equal("Shared");
    }

    [Fact]
    public async Task ShouldSortByStartDateByDefault()
    {
        var owner = await AppUserFactory.CreateAsync();
        var later = await EventFactory.CreateAsync(owner.Id, "Later", startsAt: new DateTimeOffset(2027, 12, 1, 0, 0, 0, TimeSpan.Zero));
        var sooner = await EventFactory.CreateAsync(owner.Id, "Sooner", startsAt: new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var result = await EventsService.ListAsync(owner.Id, new ListEventsRequestDto());

        result.Items.Select(e => e.Id).Should().Equal(sooner.Id, later.Id);
    }

    [Fact]
    public async Task ShouldFilterBySearchKey_OnNameOrSubdomain()
    {
        var owner = await AppUserFactory.CreateAsync();
        await EventFactory.CreateAsync(owner.Id, "Ana's Wedding", subdomain: "ana-and-marco");
        await EventFactory.CreateAsync(owner.Id, "Office Party", subdomain: "year-end-2027");
        await EventFactory.CreateAsync(owner.Id, "Reunion", subdomain: "wedding-anniversary");

        var result = await EventsService.ListAsync(owner.Id, new ListEventsRequestDto { SearchKey = "wedding" });

        result.Items.Select(e => e.Name).Should().BeEquivalentTo(["Ana's Wedding", "Reunion"]);
    }

    [Fact]
    public async Task ShouldFilterByEventGroup_WhenEventGroupIdIsProvided()
    {
        var owner = await AppUserFactory.CreateAsync();
        var group = await EventGroupFactory.CreateAsync(owner.Id);
        var grouped = await EventFactory.CreateAsync(owner.Id, eventGroupId: group.Id);
        await EventFactory.CreateAsync(owner.Id);

        var result = await EventsService.ListAsync(owner.Id, new ListEventsRequestDto { EventGroupId = group.Id });

        result.Items.Select(e => e.Id).Should().Equal(grouped.Id);
    }

    [Fact]
    public async Task ShouldApplyPaginationAndSorting_WhenRequestHasPageAndSort()
    {
        var owner = await AppUserFactory.CreateAsync();
        await EventFactory.CreateAsync(owner.Id, "Beta");
        await EventFactory.CreateAsync(owner.Id, "Alpha");
        await EventFactory.CreateAsync(owner.Id, "Gamma");

        var result = await EventsService.ListAsync(owner.Id, new ListEventsRequestDto
        {
            Page = 2,
            PageSize = 1,
            Sort = ["name:desc"],
        });

        result.TotalCount.Should().Be(3);
        result.Items.Single().Name.Should().Be("Beta");
        result.HasNextPage.Should().BeTrue();
        result.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public async Task ShouldSortByType_WhenRequested()
    {
        var owner = await AppUserFactory.CreateAsync();
        await EventFactory.CreateAsync(owner.Id, "Wedding", type: Api.Common.Constants.EventType.Wedding);
        await EventFactory.CreateAsync(owner.Id, "Birthday", type: Api.Common.Constants.EventType.Birthday);

        var result = await EventsService.ListAsync(owner.Id, new ListEventsRequestDto { Sort = ["type:asc"] });

        // Stored as names, so sorted alphabetically: Birthday before Wedding
        result.Items.Select(e => e.Name).Should().Equal("Birthday", "Wedding");
    }

    [Fact]
    public async Task ShouldReturnEmptyList_WhenOwnerHasNoEvents()
    {
        var owner = await AppUserFactory.CreateAsync();

        var result = await EventsService.ListAsync(owner.Id, new ListEventsRequestDto());

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }
}
