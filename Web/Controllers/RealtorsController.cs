using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Models.Realtors;
using Web.Services;

namespace Web.Controllers;

[Authorize(Policy = "RequireAnyAuthorized")]
public class RealtorsController : Controller
{
    private readonly RealtorsApiClient _realtorsApiClient;

    public RealtorsController(RealtorsApiClient realtorsApiClient)
    {
        _realtorsApiClient = realtorsApiClient;
    }

    public async Task<IActionResult> Index(
        string? search,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        const int pageSize = 9;

        var listResult = await _realtorsApiClient.GetAsync(500, cancellationToken);

        if (listResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToAction("Login", "Auth", new
            {
                returnUrl = Url.Action("Index", "Realtors")
            });
        }

        var allRealtors = listResult.Data ?? [];
        var topRealtors = allRealtors
            .OrderByDescending(x => x.AverageRating)
            .ThenByDescending(x => x.DealsThisMonth)
            .ThenBy(x => x.LastName)
            .Take(6)
            .ToList();

        var normalizedSearch = search?.Trim();
        var filtered = string.IsNullOrWhiteSpace(normalizedSearch)
            ? allRealtors
            : allRealtors
                .Where(x => MatchesSearch(x, normalizedSearch))
                .ToList();

        var totalCount = filtered.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var safePage = Math.Clamp(page, 1, totalPages);
        var pagedRealtors = filtered
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ThenBy(x => x.MiddleName)
            .Skip((safePage - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return View(new RealtorsIndexViewModel
        {
            TopRealtors = topRealtors,
            Realtors = pagedRealtors,
            SearchQuery = normalizedSearch,
            Page = safePage,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            ApiError = listResult.Error
        });
    }

    private static bool MatchesSearch(RealtorCardViewModel realtor, string search)
    {
        var normalizedPhoneSearch = NormalizePhone(search);

        return realtor.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)
            || realtor.FirstName.Contains(search, StringComparison.OrdinalIgnoreCase)
            || realtor.LastName.Contains(search, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(realtor.MiddleName)
                && realtor.MiddleName.Contains(search, StringComparison.OrdinalIgnoreCase))
            || realtor.PhoneNumber.Contains(search, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(normalizedPhoneSearch)
                && NormalizePhone(realtor.PhoneNumber).Contains(normalizedPhoneSearch, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePhone(string value)
    {
        return new string(value.Where(char.IsDigit).ToArray());
    }
}
