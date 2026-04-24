
using Bunit;
using HotelChain.Web.Pages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HotelChain.Tests.Frontend;

public class CheckoutTests : BunitContext
{
    [Fact]
    public void Checkout_Should_Render_Main_Title()
    {
        // Arrange
        Services.AddSingleton(new HttpClient
        {
            BaseAddress = new Uri("http://localhost")
        });

        // Act
        var cut = Render<Checkout>();

        // Assert
        Assert.Contains("checkout-premium-page", cut.Markup);
    }

    [Fact]
    public void Checkout_Should_Render_Checkout_Button()
    {
        // Arrange
        Services.AddSingleton(new HttpClient
        {
            BaseAddress = new Uri("http://localhost")
        });

        // Act
        var cut = Render<Checkout>();

        // Assert
        Assert.Contains("Pagar y confirmar", cut.Markup);
    }
}