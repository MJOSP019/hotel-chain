using HotelChain.Api.Controllers;
using HotelChain.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;

namespace HotelChain.Tests.Controllers;

public class UserReservationsControllerTests
{
    [Fact]
    public async Task GetMyReservations_Should_ReturnUnauthorized_When_UserIdClaimIsMissing()
    {
        await using var db = TestDbFactory.CreateDbContext();
        var controller = new UserReservationsController(db);
        TestDbFactory.SetAnonymousUser(controller);

        var result = await controller.GetMyReservations();

        Assert.IsType<UnauthorizedResult>(result);
    }
}
