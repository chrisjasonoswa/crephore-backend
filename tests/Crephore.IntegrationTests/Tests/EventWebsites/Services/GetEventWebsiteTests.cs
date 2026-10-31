using Crephore.Api.Common.Exceptions;
using Crephore.IntegrationTests.Factories.Entities;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.EventWebsites.Services;

public class GetEventWebsiteTests(DatabaseFixture fixture)
    : EventWebsitesBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldReturnTheThemeAndSectionsInPageOrder_WhenTheEventHasAWebsite()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id,
        [
            EventWebsiteFactory.BuildSection("hero", "split", "First"),
            EventWebsiteFactory.BuildSection("countdown", "boxes", "Second"),
        ]);

        var website = await EventWebsitesService.GetAsync(@event.Id, owner.Id);

        website!.EventId.Should().Be(@event.Id);
        website.Theme.PrimaryColor.Should().Be("#1E2A44");
        website.Theme.CornerStyle.Should().Be("soft");
        website.Sections.Select(s => s.Type).Should().Equal("hero", "countdown");
        website.Sections[0].Options.Should().Contain("photoSide", "right");
        website.Sections[0].Content.GetProperty("title").GetString().Should().Be("First");
    }

    [Fact]
    public async Task ShouldReturnNull_WhenTheEventHasNoWebsiteYet()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var website = await EventWebsitesService.GetAsync(@event.Id, owner.Id);

        website.Should().BeNull();
    }

    [Fact]
    public async Task ShouldReturnTheWebsite_WhenUserHasAccessWithoutOwning()
    {
        var owner = await AppUserFactory.CreateAsync();
        var member = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventUserFactory.CreateAsync(@event.Id, member.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id);

        var website = await EventWebsitesService.GetAsync(@event.Id, member.Id);

        website.Should().NotBeNull();
    }

    [Fact]
    public async Task ShouldThrowNotFoundException_WhenEventBelongsToAnotherUser()
    {
        var owner = await AppUserFactory.CreateAsync();
        var otherUser = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id);

        var act = async () => await EventWebsitesService.GetAsync(@event.Id, otherUser.Id);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Message.Should().Be("Event not found.");
    }
}
