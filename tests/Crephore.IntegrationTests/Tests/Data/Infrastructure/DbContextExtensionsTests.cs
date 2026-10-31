using Crephore.Api.Common.Exceptions;
using Crephore.Api.Data;
using Crephore.Api.Data.Entities;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.Data.Infrastructure;

// Saves straight through the context, skipping the services' own uniqueness checks,
// so the database's unique index is what rejects the duplicate.
public class DbContextExtensionsTests(DatabaseFixture fixture)
    : DataBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldSave_WhenNoUniqueIndexIsViolated()
    {
        var owner = await AppUserFactory.CreateAsync();
        Context.EventGroups.Add(new EventGroup { Name = "Family", OwnerId = owner.Id });

        var saved = await Context.SaveChangesOrThrowIfDuplicateAsync(UniqueIndexes.EventGroupOwnerName, "name", "Taken.");

        saved.Should().Be(1);
    }

    [Fact]
    public async Task ShouldThrowValidationExceptionOnTheField_WhenTheIndexIsViolated()
    {
        var owner = await AppUserFactory.CreateAsync();
        await EventGroupFactory.CreateAsync(owner.Id, "Family");
        Context.EventGroups.Add(new EventGroup { Name = "Family", OwnerId = owner.Id });

        var act = async () => await Context.SaveChangesOrThrowIfDuplicateAsync(UniqueIndexes.EventGroupOwnerName, "name", "Taken.");

        var exception = await act.Should().ThrowAsync<AppValidationException>();
        exception.Which.Message.Should().Be("Taken.");
        exception.Which.Errors.Should().ContainKey("name").WhoseValue.Should().Equal("Taken.");
    }

    [Fact]
    public async Task ShouldRethrowDbUpdateException_WhenADifferentUniqueIndexIsViolated()
    {
        var owner = await AppUserFactory.CreateAsync();
        await EventGroupFactory.CreateAsync(owner.Id, "Family");
        Context.EventGroups.Add(new EventGroup { Name = "Family", OwnerId = owner.Id });

        var act = async () => await Context.SaveChangesOrThrowIfDuplicateAsync(UniqueIndexes.EventSubdomain, "subdomain", "Taken.");

        (await act.Should().ThrowAsync<DbUpdateException>()).Which.IsUniqueViolation(UniqueIndexes.EventGroupOwnerName)
            .Should().BeTrue();
    }
}
