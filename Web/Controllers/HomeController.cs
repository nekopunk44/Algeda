using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Web.Models;
using Web.Models.Properties;
using Web.Models.RealEstate;
using Web.Services;

namespace Web.Controllers;

public class HomeController : Controller
{
    private readonly PropertiesApiClient _propertiesApiClient;
    private readonly RealEstateApiClient _realEstateApiClient;

    public HomeController(
        PropertiesApiClient propertiesApiClient,
        RealEstateApiClient realEstateApiClient)
    {
        _propertiesApiClient = propertiesApiClient;
        _realEstateApiClient = realEstateApiClient;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var isAuthenticated = User.Identity?.IsAuthenticated == true;
        var availableTask = _propertiesApiClient.GetAvailableAsync(cancellationToken);
        var currenciesTask = _realEstateApiClient.GetCurrenciesAsync(200, includeInactive: false, cancellationToken);
        await Task.WhenAll(availableTask, currenciesTask);

        var availableResult = availableTask.Result;
        var currenciesResult = currenciesTask.Result;
        var pageError = availableResult.Error ?? currenciesResult.Error;

        if (IsServiceUnavailable(pageError))
        {
            return View("Error", BuildTemporaryUnavailableError(pageError));
        }

        if (isAuthenticated && availableResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToAction("Login", "Auth", new
            {
                returnUrl = Url.Action("Index", "Home")
            });
        }

        return View(new HomeMapViewModel
        {
            IsAuthenticated = isAuthenticated,
            Properties = availableResult.Data ?? [],
            Currencies = EnsureBaseCurrency(currenciesResult.Data ?? []),
            ApiError = pageError
        });
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private static IReadOnlyList<CurrencyRateViewModel> EnsureBaseCurrency(
        IReadOnlyList<CurrencyRateViewModel> currencies)
    {
        if (currencies.Any(x => string.Equals(x.Code, "USD", StringComparison.OrdinalIgnoreCase)))
        {
            return currencies;
        }

        return
        [
            new CurrencyRateViewModel
            {
                Code = "USD",
                Name = "US Dollar",
                Symbol = "$",
                RateToBase = 1m,
                IsActive = true
            },
            .. currencies
        ];
    }

    private static bool IsServiceUnavailable(Web.Models.Api.ApiErrorViewModel? error)
    {
        return error?.StatusCode is StatusCodes.Status503ServiceUnavailable
            or StatusCodes.Status504GatewayTimeout;
    }

    private ErrorViewModel BuildTemporaryUnavailableError(Web.Models.Api.ApiErrorViewModel? error)
    {
        return new ErrorViewModel
        {
            RequestId = HttpContext.TraceIdentifier,
            Title = error?.Title ?? "Сервис временно недоступен",
            Heading = "Данные временно недоступны.",
            Message = error?.Message ?? "Попробуйте обновить страницу позже."
        };
    }
}
