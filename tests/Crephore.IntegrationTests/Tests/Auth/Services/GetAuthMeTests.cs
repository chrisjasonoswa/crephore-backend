using Crephore.Api.Common.Exceptions;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.Auth.Services;

public class GetAuthMeTests(DatabaseFixture fixture)
    : AuthBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldReturnCurrentUser_WhenUserExists()
    {
        var user = await AppUserFactory.CreateAsync(
            firstName: "Chris",
            lastName: "Oswa",
            phoneNumber: "+639171234567",
            timezone: "Asia/Manila",
            isSuperAdmin: true);

        var result = await AuthService.GetCurrentUserAsync(user.Id);

        result.Id.Should().Be(user.Id);
        result.FirstName.Should().Be("Chris");
        result.LastName.Should().Be("Oswa");
        result.Email.Should().Be(user.Email);
        result.PhoneNumber.Should().Be("+639171234567");
        result.Timezone.Should().Be("Asia/Manila");
        result.IsSuperAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task ShouldThrowNotFoundException_WhenUserDoesNotExist()
    {
        var act = async () => await AuthService.GetCurrentUserAsync(int.MaxValue);

        var exception = await act.Should().ThrowAsync<NotFoundException>();
        exception.Which.Message.Should().Be("User not found.");
    }
}
