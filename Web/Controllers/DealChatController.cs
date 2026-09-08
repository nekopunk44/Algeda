using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Models.DealChat;
using Web.Services;

namespace Web.Controllers;

[Authorize(Policy = "RequireAnyAuthorized")]
public sealed class DealChatController : Controller
{
    private const string ErrorKey = "DealChatError";

    private readonly DealChatApiClient _dealChatApiClient;

    public DealChatController(DealChatApiClient dealChatApiClient)
    {
        _dealChatApiClient = dealChatApiClient;
    }

    [HttpGet("deal-chat/deals/{dealId:guid}")]
    public async Task<IActionResult> Deal(
        Guid dealId,
        string? returnUrl = null,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        var dialogResult = await _dealChatApiClient.GetDealDialogAsync(dealId, Math.Clamp(limit, 1, 1000), cancellationToken);

        if (dialogResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToLoginCurrent();
        }

        return View(new DealChatPageViewModel
        {
            ApiError = GetTempApiError() ?? dialogResult.Error,
            DealId = dealId,
            ReturnUrl = NormalizeReturnUrl(returnUrl),
            Dialog = dialogResult.Data
        });
    }

    [HttpGet("deal-chat/deals/{dealId:guid}/dialog")]
    public async Task<IActionResult> DealDialog(
        Guid dealId,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        var dialogResult = await _dealChatApiClient.GetDealDialogAsync(
            dealId,
            Math.Clamp(limit, 1, 1000),
            cancellationToken);

        if (dialogResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return Unauthorized();
        }

        if (!dialogResult.IsSuccess || dialogResult.Data is null)
        {
            var status = dialogResult.Error?.StatusCode ?? StatusCodes.Status400BadRequest;
            var message = dialogResult.Error?.Message ?? "Не удалось загрузить чат.";
            return StatusCode(status, new { message });
        }

        return Json(new
        {
            dealId = dialogResult.Data.DealId,
            canWrite = dialogResult.Data.CanWrite,
            blockReason = dialogResult.Data.BlockReason,
            messages = dialogResult.Data.Messages.Select(message => new
            {
                id = message.Id,
                senderId = message.SenderId,
                content = message.Content,
                isOutgoing = message.IsOutgoing,
                createdDate = message.CreatedDate
            })
        });
    }

    [HttpPost("deal-chat/deals/{dealId:guid}/send")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(
        Guid dealId,
        string content,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        var isAjax = string.Equals(
            Request.Headers["X-Requested-With"],
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);

        if (dealId == Guid.Empty)
        {
            if (isAjax)
            {
                return BadRequest(new { success = false, message = "Сделка для чата не указана." });
            }

            TempData[ErrorKey] = "Сделка для чата не указана.";
            return RedirectToAction(nameof(Deal), new { dealId, returnUrl });
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            if (isAjax)
            {
                return BadRequest(new { success = false, message = "Введите текст сообщения." });
            }

            TempData[ErrorKey] = "Введите текст сообщения.";
            return RedirectToAction(nameof(Deal), new { dealId, returnUrl });
        }

        var result = await _dealChatApiClient.SendMessageAsync(dealId, content.Trim(), cancellationToken);

        if (result.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            if (isAjax)
            {
                return Unauthorized(new { success = false, message = "Требуется повторная авторизация." });
            }

            return RedirectToLoginCurrent();
        }

        if (result.IsSuccess)
        {
            if (isAjax)
            {
                return Ok(new { success = true });
            }
        }
        else
        {
            if (isAjax)
            {
                return BadRequest(new { success = false, message = result.Error?.Message ?? "Не удалось отправить сообщение." });
            }

            TempData[ErrorKey] = result.Error?.Message ?? "Не удалось отправить сообщение.";
        }

        return RedirectToAction(nameof(Deal), new { dealId, returnUrl });
    }

    private IActionResult RedirectToLoginCurrent()
    {
        var current = $"{Request.Path}{Request.QueryString}";
        return RedirectToAction("Login", "Auth", new { returnUrl = current });
    }

    private static string? NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return null;

        return returnUrl.Trim();
    }

    private Web.Models.Api.ApiErrorViewModel? GetTempApiError()
    {
        var message = TempData[ErrorKey]?.ToString();
        if (string.IsNullOrWhiteSpace(message))
            return null;

        return Web.Models.Api.ApiErrorFactory.Create(StatusCodes.Status400BadRequest, message);
    }
}
