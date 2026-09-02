using Web.Models.Api;
using Web.Models.Dashboard;
using Web.Models.DealRequests;
using Web.Models.SellProperty;

namespace Web.Services;

public class DealRequestsApiClient
{
    private readonly HttpClient _httpClient;

    public DealRequestsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<ApiClientResult<IReadOnlyList<DealRequestListItemViewModel>>> GetIncomingAsync(
        int limit = 500,
        string? search = null,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"limit={Math.Clamp(limit, 1, 500)}" };
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            query.Add($"source={Uri.EscapeDataString(source.Trim())}");
        }

        return GetDealsCoreAsync($"api/deals/incoming?{string.Join("&", query)}", cancellationToken);
    }

    public Task<ApiClientResult<IReadOnlyList<DealRequestListItemViewModel>>> GetMineAsync(
        int limit = 500,
        string? search = null,
        string? status = null,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"limit={Math.Clamp(limit, 1, 500)}" };
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add($"status={Uri.EscapeDataString(status.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            query.Add($"source={Uri.EscapeDataString(source.Trim())}");
        }

        return GetDealsCoreAsync($"api/deals/mine?{string.Join("&", query)}", cancellationToken);
    }

    public Task<ApiClientResult<IReadOnlyList<DealRequestListItemViewModel>>> GetAllAsync(
        int limit = 500,
        string? search = null,
        string? status = null,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"limit={Math.Clamp(limit, 1, 500)}" };
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add($"status={Uri.EscapeDataString(status.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            query.Add($"source={Uri.EscapeDataString(source.Trim())}");
        }

        return GetDealsCoreAsync($"api/deals/all?{string.Join("&", query)}", cancellationToken);
    }

    public async Task<ApiClientResult<DealRequestListItemViewModel>> GetByIdAsync(
        Guid dealId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/deals/{dealId}/workflow", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<DealRequestListItemViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<DealWorkflowDto>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<DealRequestListItemViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API вернул пустую карточку заявки."));
            }

            return ApiClientResult<DealRequestListItemViewModel>.Success(MapDeal(payload));
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<DealRequestListItemViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<DealRequestListItemViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<IReadOnlyList<DealRequestStatusCountViewModel>>> GetStatusCountsAsync(
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var relativeUrl = "api/deals/status-counts";
            if (!string.IsNullOrWhiteSpace(source))
            {
                relativeUrl += $"?source={Uri.EscapeDataString(source.Trim())}";
            }

            var response = await _httpClient.GetAsync(relativeUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<DealRequestStatusCountViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<StatusCountDto>>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<DealRequestStatusCountViewModel>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API вернул пустой список счетчиков."));
            }

            var mapped = payload
                .Select(x => new DealRequestStatusCountViewModel
                {
                    Status = x.Status,
                    Count = x.Count
                })
                .ToList();

            return ApiClientResult<IReadOnlyList<DealRequestStatusCountViewModel>>.Success(mapped);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<DealRequestStatusCountViewModel>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<DealRequestStatusCountViewModel>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public Task<ApiOperationResult> CreateMyRequestAsync(
        Guid? propertyId,
        Guid? clientRequirementId,
        string source = "Home",
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        var safeSource = source?.Trim() switch
        {
            "Matching" => "Matching",
            "Manual" => "Manual",
            _ => "Home"
        };

        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Post, "api/deals/me/request", new
        {
            source = safeSource,
            propertyId,
            clientRequirementId,
            message
        }, cancellationToken);
    }

    public Task<ApiClientResult<IReadOnlyList<DealRequestListItemViewModel>>> GetMySaleRequestsAsync(
        int limit = 500,
        string? search = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"limit={Math.Clamp(limit, 1, 500)}" };
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add($"status={Uri.EscapeDataString(status.Trim())}");
        }

        return GetDealsCoreAsync($"api/deals/me/sale-requests?{string.Join("&", query)}", cancellationToken);
    }

    public Task<ApiClientResult<IReadOnlyList<DealRequestListItemViewModel>>> GetMyClientDealsAsync(
        int limit = 500,
        string? search = null,
        string? status = null,
        string? scope = "all",
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"limit={Math.Clamp(limit, 1, 500)}",
            $"scope={Uri.EscapeDataString(NormalizeClientDealScope(scope))}"
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add($"status={Uri.EscapeDataString(status.Trim())}");
        }

        return GetDealsCoreAsync($"api/client-deals/mine?{string.Join("&", query)}", cancellationToken);
    }

    public async Task<ApiClientResult<DealRequestListItemViewModel>> GetMyClientDealByIdAsync(
        Guid dealId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/client-deals/mine/{dealId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<DealRequestListItemViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<DealWorkflowDto>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<DealRequestListItemViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API вернул пустую карточку сделки клиента."));
            }

            return ApiClientResult<DealRequestListItemViewModel>.Success(MapDeal(payload));
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<DealRequestListItemViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<DealRequestListItemViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<SaleRequestDetailsViewModel>> GetMySaleRequestByIdAsync(
        Guid dealId,
        CancellationToken cancellationToken = default)
    {
        return await GetSaleRequestDetailsCoreAsync($"api/deals/me/sale-requests/{dealId}", cancellationToken);
    }

    public async Task<ApiClientResult<SaleRequestDetailsViewModel>> GetSaleRequestByIdAsync(
        Guid dealId,
        CancellationToken cancellationToken = default)
    {
        return await GetSaleRequestDetailsCoreAsync($"api/deals/{dealId}/sale-request", cancellationToken);
    }

    private async Task<ApiClientResult<SaleRequestDetailsViewModel>> GetSaleRequestDetailsCoreAsync(
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(relativeUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<SaleRequestDetailsViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<SaleRequestDetailsDto>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null || payload.Deal is null || payload.Property is null)
            {
                return ApiClientResult<SaleRequestDetailsViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API вернул пустую карточку заявки на продажу."));
            }

            return ApiClientResult<SaleRequestDetailsViewModel>.Success(MapSaleRequestDetails(payload));
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<SaleRequestDetailsViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<SaleRequestDetailsViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public Task<ApiOperationResult> CreateMySaleRequestAsync(
        SaleRequestSubmitPayload payload,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Post, "api/deals/me/sale-requests", new
        {
            property = new
            {
                title = payload.Title,
                address = payload.Address,
                price = payload.Price,
                priceCurrency = payload.PriceCurrency,
                area = payload.Area,
                roomsCount = payload.RoomsCount,
                latitude = payload.Latitude,
                longitude = payload.Longitude,
                type = payload.Type,
                photoPaths = payload.PhotoPaths,
                ownerFullName = payload.OwnerFullName,
                ownerEmail = payload.OwnerEmail,
                ownerPhoneNumber = payload.OwnerPhoneNumber
            },
            criteria = payload.Criteria?.Select(x => new
            {
                criterionDefinitionId = x.CriterionDefinitionId,
                value = x.Value,
                values = x.Values
            }),
            message = payload.Comment
        }, cancellationToken);
    }

    public Task<ApiOperationResult> UpdateMySaleRequestAsync(
        Guid dealId,
        SaleRequestSubmitPayload payload,
        CancellationToken cancellationToken = default)
    {
        return UpdateSaleRequestCoreAsync($"api/deals/me/sale-requests/{dealId}", payload, cancellationToken);
    }

    public Task<ApiOperationResult> UpdateSaleRequestAsync(
        Guid dealId,
        SaleRequestSubmitPayload payload,
        CancellationToken cancellationToken = default)
    {
        return UpdateSaleRequestCoreAsync($"api/deals/{dealId}/sale-request", payload, cancellationToken);
    }

    private Task<ApiOperationResult> UpdateSaleRequestCoreAsync(
        string relativeUrl,
        SaleRequestSubmitPayload payload,
        CancellationToken cancellationToken)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Put, relativeUrl, new
        {
            property = new
            {
                title = payload.Title,
                address = payload.Address,
                price = payload.Price,
                priceCurrency = payload.PriceCurrency,
                area = payload.Area,
                roomsCount = payload.RoomsCount,
                latitude = payload.Latitude,
                longitude = payload.Longitude,
                type = payload.Type,
                photoPaths = payload.PhotoPaths,
                ownerFullName = payload.OwnerFullName,
                ownerEmail = payload.OwnerEmail,
                ownerPhoneNumber = payload.OwnerPhoneNumber
            },
            criteria = payload.Criteria?.Select(x => new
            {
                criterionDefinitionId = x.CriterionDefinitionId,
                value = x.Value,
                values = x.Values
            }),
            message = payload.Comment
        }, cancellationToken);
    }

    public Task<ApiOperationResult> CancelMySaleRequestAsync(
        Guid dealId,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Patch, $"api/deals/me/sale-requests/{dealId}/cancel", null, cancellationToken);
    }

    public Task<ApiOperationResult> AcceptAsync(Guid dealId, CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Patch, $"api/deals/{dealId}/accept", null, cancellationToken);
    }

    public Task<ApiOperationResult> RejectAsync(Guid dealId, CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Patch, $"api/deals/{dealId}/reject", null, cancellationToken);
    }

    public Task<ApiOperationResult> ReleaseAsync(Guid dealId, CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Patch, $"api/deals/{dealId}/release", null, cancellationToken);
    }

    public Task<ApiOperationResult> CompleteAsync(
        Guid dealId,
        decimal commissionAmount,
        string commissionCurrency,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Patch, $"api/deals/{dealId}/complete", new
        {
            dealId,
            commissionAmount,
            commissionCurrency
        }, cancellationToken);
    }

    public Task<ApiOperationResult> CancelAsync(Guid dealId, CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Patch, $"api/deals/{dealId}/cancel", null, cancellationToken);
    }

    public Task<ApiOperationResult> AssignRealtorAsync(
        Guid dealId,
        Guid realtorId,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Patch, $"api/deals/{dealId}/assign", new
        {
            realtorId
        }, cancellationToken);
    }

    public Task<ApiOperationResult> PublishSalePropertyAsync(
        Guid dealId,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Patch, $"api/deals/{dealId}/publish-property", null, cancellationToken);
    }

    public async Task<ApiClientResult<IReadOnlyList<DealRequestRealtorCandidateViewModel>>> SearchRealtorsAsync(
        string? search,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"limit={Math.Clamp(limit, 1, 100)}" };
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }

        try
        {
            var response = await _httpClient.GetAsync($"api/deals/realtors/search?{string.Join("&", query)}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<DealRequestRealtorCandidateViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<RealtorCandidateDto>>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<DealRequestRealtorCandidateViewModel>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API вернул пустой список риелторов."));
            }

            var mapped = payload.Select(x => new DealRequestRealtorCandidateViewModel
            {
                RealtorId = x.RealtorId,
                FullName = x.FullName,
                PhoneNumber = x.PhoneNumber,
                Email = x.Email
            }).ToList();

            return ApiClientResult<IReadOnlyList<DealRequestRealtorCandidateViewModel>>.Success(mapped);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<DealRequestRealtorCandidateViewModel>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<DealRequestRealtorCandidateViewModel>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<DealRequestNoteViewModel>> AddNoteAsync(
        Guid dealId,
        string text,
        CancellationToken cancellationToken = default)
    {
        return await UpsertNoteAsync(HttpMethod.Post, $"api/deals/{dealId}/notes", new { text }, cancellationToken);
    }

    public async Task<ApiClientResult<DealRequestNoteViewModel>> UpdateNoteAsync(
        Guid dealId,
        Guid noteId,
        string text,
        CancellationToken cancellationToken = default)
    {
        return await UpsertNoteAsync(HttpMethod.Put, $"api/deals/{dealId}/notes/{noteId}", new { text }, cancellationToken);
    }

    public Task<ApiOperationResult> DeleteNoteAsync(
        Guid dealId,
        Guid noteId,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Delete, $"api/deals/{dealId}/notes/{noteId}", null, cancellationToken);
    }

    public async Task<ApiClientResult<IReadOnlyList<DealDocumentViewModel>>> GetDocumentsAsync(
        Guid dealId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/deals/{dealId}/documents", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<DealDocumentViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<DealDocumentDto>>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<DealDocumentViewModel>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API вернул пустой список документов."));
            }

            return ApiClientResult<IReadOnlyList<DealDocumentViewModel>>.Success(
                payload.Select(MapDocument).ToList());
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<DealDocumentViewModel>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<DealDocumentViewModel>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<DealDocumentViewModel>> UploadDocumentAsync(
        Guid dealId,
        string title,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(title), "title");
            await using var stream = file.OpenReadStream();
            content.Add(new StreamContent(stream), "file", file.FileName);

            var response = await _httpClient.PostAsync($"api/deals/{dealId}/documents", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<DealDocumentViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<DealDocumentDto>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<DealDocumentViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API вернул пустой документ."));
            }

            return ApiClientResult<DealDocumentViewModel>.Success(MapDocument(payload));
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<DealDocumentViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<DealDocumentViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<DealDocumentFileViewModel>> DownloadDocumentAsync(
        Guid dealId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"api/deals/{dealId}/documents/{documentId}/download",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<DealDocumentFileViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName
                ?? "document";

            return ApiClientResult<DealDocumentFileViewModel>.Success(new DealDocumentFileViewModel
            {
                FileName = fileName.Trim('"'),
                ContentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
                Content = content
            });
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<DealDocumentFileViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<DealDocumentFileViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public Task<ApiOperationResult> DeleteDocumentAsync(
        Guid dealId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(
            _httpClient,
            HttpMethod.Delete,
            $"api/deals/{dealId}/documents/{documentId}",
            null,
            cancellationToken);
    }

    public async Task<ApiClientResult<IReadOnlyList<DealDocumentAccessLogItemViewModel>>> GetDocumentAccessLogAsync(
        string? dateFrom,
        string? dateTo,
        string? action,
        string? dealId,
        string? search,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"limit={Math.Clamp(limit, 1, 500)}" };
        AddQuery(query, "dateFrom", dateFrom);
        AddQuery(query, "dateTo", dateTo);
        AddQuery(query, "action", action);
        AddQuery(query, "dealId", dealId);
        AddQuery(query, "search", search);

        try
        {
            var response = await _httpClient.GetAsync($"api/deal-documents/access-log?{string.Join("&", query)}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<DealDocumentAccessLogItemViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<DealDocumentAccessLogDto>>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<DealDocumentAccessLogItemViewModel>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API вернул пустой журнал документов."));
            }

            return ApiClientResult<IReadOnlyList<DealDocumentAccessLogItemViewModel>>.Success(
                payload.Select(MapDocumentLog).ToList());
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<DealDocumentAccessLogItemViewModel>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<DealDocumentAccessLogItemViewModel>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<DashboardRequirementViewModel>> GetRequirementByIdAsync(
        Guid requirementId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/client-requirements/{requirementId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<DashboardRequirementViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<RequirementDto>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<DashboardRequirementViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API вернул пустое требование клиента."));
            }

            return ApiClientResult<DashboardRequirementViewModel>.Success(MapRequirement(payload));
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<DashboardRequirementViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<DashboardRequirementViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    private async Task<ApiClientResult<IReadOnlyList<DealRequestListItemViewModel>>> GetDealsCoreAsync(
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(relativeUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<DealRequestListItemViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<DealWorkflowDto>>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<DealRequestListItemViewModel>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API вернул пустой список заявок."));
            }

            var mapped = payload.Select(MapDeal).ToList();
            return ApiClientResult<IReadOnlyList<DealRequestListItemViewModel>>.Success(mapped);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<DealRequestListItemViewModel>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<DealRequestListItemViewModel>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    private async Task<ApiClientResult<DealRequestNoteViewModel>> UpsertNoteAsync(
        HttpMethod method,
        string relativeUrl,
        object payload,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, relativeUrl)
            {
                Content = JsonContent.Create(payload)
            };

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<DealRequestNoteViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var dto = await response.Content.ReadFromJsonAsync<DealNoteDto>(ApiClientSupport.JsonOptions, cancellationToken);
            if (dto is null)
            {
                return ApiClientResult<DealRequestNoteViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API вернул пустой ответ заметки."));
            }

            return ApiClientResult<DealRequestNoteViewModel>.Success(MapNote(dto));
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<DealRequestNoteViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<DealRequestNoteViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    private static DealRequestListItemViewModel MapDeal(DealWorkflowDto item)
    {
        return new DealRequestListItemViewModel
        {
            Id = item.Id,
            ClientId = item.ClientId,
            ClientFullName = item.ClientFullName,
            ClientPhoneNumber = item.ClientPhoneNumber,
            ClientEmail = item.ClientEmail,
            PropertyId = item.PropertyId,
            PropertyTitle = item.PropertyTitle,
            ClientRequirementId = item.ClientRequirementId,
            Source = item.Source,
            Status = item.Status,
            IsIncoming = item.IsIncoming,
            RealtorId = item.RealtorId,
            RealtorFullName = item.RealtorFullName,
            RealtorPhoneNumber = item.RealtorPhoneNumber,
            RealtorEmail = item.RealtorEmail,
            RequestMessage = item.RequestMessage,
            AcceptedAtUtc = item.AcceptedAtUtc,
            RejectedAtUtc = item.RejectedAtUtc,
            PriorityRealtorId = item.PriorityRealtorId,
            PriorityUntilUtc = item.PriorityUntilUtc,
            CompletedAtUtc = item.CompletedAtUtc,
            Notes = (item.Notes ?? [])
                .Select(MapNote)
                .OrderByDescending(x => x.CreatedDate)
                .ToList(),
            CreatedDate = item.CreatedDate
        };
    }

    private static DealDocumentViewModel MapDocument(DealDocumentDto item)
    {
        return new DealDocumentViewModel
        {
            Id = item.Id,
            DealId = item.DealId,
            Title = item.Title,
            OriginalFileName = item.OriginalFileName,
            ContentType = item.ContentType,
            FileSizeBytes = item.FileSizeBytes,
            UploadedByUserId = item.UploadedByUserId,
            UploadedByDisplayName = item.UploadedByDisplayName,
            UploadedByEmail = item.UploadedByEmail,
            CreatedDate = item.CreatedDate
        };
    }

    private static DealDocumentAccessLogItemViewModel MapDocumentLog(DealDocumentAccessLogDto item)
    {
        return new DealDocumentAccessLogItemViewModel
        {
            Id = item.Id,
            DealId = item.DealId,
            DealDocumentId = item.DealDocumentId,
            DocumentTitle = item.DocumentTitle,
            DocumentFileName = item.DocumentFileName,
            Action = item.Action,
            ActorUserId = item.ActorUserId,
            ActorDisplayName = item.ActorDisplayName,
            ActorEmail = item.ActorEmail,
            ActorRole = item.ActorRole,
            IpAddress = item.IpAddress,
            UserAgent = item.UserAgent,
            CreatedDate = item.CreatedDate,
            DealSummary = item.DealSummary,
            ClientSummary = item.ClientSummary,
            RealtorSummary = item.RealtorSummary
        };
    }

    private static SaleRequestDetailsViewModel MapSaleRequestDetails(SaleRequestDetailsDto payload)
    {
        return new SaleRequestDetailsViewModel
        {
            Deal = MapDeal(payload.Deal!),
            Property = new SaleRequestPropertyViewModel
            {
                Id = payload.Property!.Id,
                Title = payload.Property.Title,
                Address = payload.Property.Address,
                Price = payload.Property.Price,
                OriginalPriceAmount = payload.Property.OriginalPriceAmount,
                OriginalPriceCurrency = payload.Property.OriginalPriceCurrency,
                Area = payload.Property.Area,
                RoomsCount = payload.Property.RoomsCount,
                Latitude = payload.Property.Latitude,
                Longitude = payload.Property.Longitude,
                Type = payload.Property.Type,
                Status = payload.Property.Status,
                OwnerFullName = payload.Property.OwnerFullName,
                OwnerEmail = payload.Property.OwnerEmail,
                OwnerPhoneNumber = payload.Property.OwnerPhoneNumber,
                PhotoPaths = payload.Property.PhotoPaths ?? [],
                Criteria = (payload.Property.Criteria ?? [])
                    .Select(x => new SaleRequestPropertyCriterionViewModel
                    {
                        CriterionDefinitionId = x.CriterionDefinitionId,
                        DisplayName = x.CriterionDisplayName,
                        ValueType = x.CriterionValueType,
                        RawValue = x.Value,
                        DisplayValue = x.DisplayValue
                    })
                    .ToList()
            },
            Lifecycle = payload.Lifecycle is null
                ? null
                : new SaleRequestLifecycleViewModel
                {
                    Stage = payload.Lifecycle.Stage,
                    Description = payload.Lifecycle.Description,
                    BuyerDeal = payload.Lifecycle.BuyerDeal is null
                        ? null
                        : new SaleRequestBuyerDealViewModel
                        {
                            DealId = payload.Lifecycle.BuyerDeal.DealId,
                            BuyerClientId = payload.Lifecycle.BuyerDeal.BuyerClientId,
                            BuyerFullName = payload.Lifecycle.BuyerDeal.BuyerFullName,
                            BuyerPhoneNumber = payload.Lifecycle.BuyerDeal.BuyerPhoneNumber,
                            BuyerEmail = payload.Lifecycle.BuyerDeal.BuyerEmail,
                            Status = payload.Lifecycle.BuyerDeal.Status,
                            CreatedDate = payload.Lifecycle.BuyerDeal.CreatedDate
                        }
                }
        };
    }

    private static DealRequestNoteViewModel MapNote(DealNoteDto note)
    {
        return new DealRequestNoteViewModel
        {
            Id = note.Id,
            AuthorRealtorId = note.AuthorRealtorId,
            Text = note.Text,
            UpdatedAtUtc = note.UpdatedAtUtc,
            CreatedDate = note.CreatedDate
        };
    }

    private static DashboardRequirementViewModel MapRequirement(RequirementDto payload)
    {
        return new DashboardRequirementViewModel
        {
            Id = payload.Id,
            ClientId = payload.ClientId,
            DesiredType = payload.DesiredType,
            DesiredTypes = payload.DesiredTypes ?? [],
            Latitude = payload.Latitude,
            Longitude = payload.Longitude,
            SearchRadiusMeters = payload.SearchRadiusMeters,
            IgnoreArea = payload.IgnoreArea,
            MinPrice = payload.MinPrice,
            MaxPrice = payload.MaxPrice,
            MinArea = payload.MinArea,
            MaxArea = payload.MaxArea,
            AddressQuery = payload.AddressQuery,
            MinMatchPercentage = payload.MinMatchPercentage,
            PriceWeight = payload.PriceWeight,
            AreaWeight = payload.AreaWeight,
            IsActive = payload.IsActive,
            Criteria = payload.Criteria?.Select(item => new DashboardRequirementCriterionViewModel
            {
                CriterionDefinitionId = item.CriterionDefinitionId,
                Priority = item.Priority,
                Value = item.Value,
                Values = item.Values ?? []
            }).ToList() ?? []
        };
    }

    private static string NormalizeClientDealScope(string? scope)
    {
        if (string.Equals(scope, "sale", StringComparison.OrdinalIgnoreCase))
            return "sale";

        if (string.Equals(scope, "purchase", StringComparison.OrdinalIgnoreCase))
            return "purchase";

        return "all";
    }

    private static void AddQuery(List<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
        }
    }

    public sealed class SaleRequestSubmitPayload
    {
        public string Title { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string PriceCurrency { get; set; } = "USD";
        public double Area { get; set; }
        public int RoomsCount { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Type { get; set; } = "Apartment";
        public IReadOnlyList<string> PhotoPaths { get; set; } = [];
        public string? OwnerFullName { get; set; }
        public string? OwnerEmail { get; set; }
        public string? OwnerPhoneNumber { get; set; }
        public string? Comment { get; set; }
        public IReadOnlyList<SaleRequestSubmitCriterionPayload>? Criteria { get; set; }
    }

    public sealed class SaleRequestSubmitCriterionPayload
    {
        public Guid CriterionDefinitionId { get; set; }
        public string? Value { get; set; }
        public IReadOnlyList<string>? Values { get; set; }
    }

    private sealed class DealWorkflowDto
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public string ClientFullName { get; set; } = string.Empty;
        public string ClientPhoneNumber { get; set; } = string.Empty;
        public string? ClientEmail { get; set; }
        public Guid? PropertyId { get; set; }
        public string? PropertyTitle { get; set; }
        public Guid? ClientRequirementId { get; set; }
        public string Source { get; set; } = "Undefined";
        public string Status { get; set; } = "Undefined";
        public bool IsIncoming { get; set; }
        public Guid? RealtorId { get; set; }
        public string? RealtorFullName { get; set; }
        public string? RealtorPhoneNumber { get; set; }
        public string? RealtorEmail { get; set; }
        public string? RequestMessage { get; set; }
        public DateTime? AcceptedAtUtc { get; set; }
        public DateTime? RejectedAtUtc { get; set; }
        public Guid? PriorityRealtorId { get; set; }
        public DateTime? PriorityUntilUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public List<DealNoteDto>? Notes { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    private sealed class SaleRequestDetailsDto
    {
        public DealWorkflowDto? Deal { get; set; }
        public SaleRequestPropertyDto? Property { get; set; }
        public SaleRequestLifecycleDto? Lifecycle { get; set; }
    }

    private sealed class SaleRequestLifecycleDto
    {
        public string Stage { get; set; } = "Submitted";
        public string Description { get; set; } = string.Empty;
        public SaleRequestBuyerDealDto? BuyerDeal { get; set; }
    }

    private sealed class SaleRequestBuyerDealDto
    {
        public Guid DealId { get; set; }
        public Guid BuyerClientId { get; set; }
        public string BuyerFullName { get; set; } = string.Empty;
        public string BuyerPhoneNumber { get; set; } = string.Empty;
        public string? BuyerEmail { get; set; }
        public string Status { get; set; } = "Undefined";
        public DateTime CreatedDate { get; set; }
    }

    private sealed class SaleRequestPropertyDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal OriginalPriceAmount { get; set; }
        public string OriginalPriceCurrency { get; set; } = "USD";
        public double Area { get; set; }
        public int RoomsCount { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Type { get; set; } = "Undefined";
        public string Status { get; set; } = "Undefined";
        public string? OwnerFullName { get; set; }
        public string? OwnerEmail { get; set; }
        public string? OwnerPhoneNumber { get; set; }
        public List<string>? PhotoPaths { get; set; }
        public List<SaleRequestPropertyCriterionDto>? Criteria { get; set; }
    }

    private sealed class SaleRequestPropertyCriterionDto
    {
        public Guid CriterionDefinitionId { get; set; }
        public string CriterionDisplayName { get; set; } = string.Empty;
        public string CriterionValueType { get; set; } = "Undefined";
        public string Value { get; set; } = string.Empty;
        public string DisplayValue { get; set; } = string.Empty;
    }

    private sealed class DealNoteDto
    {
        public Guid Id { get; set; }
        public Guid? AuthorRealtorId { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime? UpdatedAtUtc { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    private sealed class StatusCountDto
    {
        public string Status { get; set; } = "Undefined";
        public int Count { get; set; }
    }

    private sealed class DealDocumentDto
    {
        public Guid Id { get; set; }
        public Guid DealId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public Guid? UploadedByUserId { get; set; }
        public string UploadedByDisplayName { get; set; } = string.Empty;
        public string? UploadedByEmail { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    private sealed class DealDocumentAccessLogDto
    {
        public Guid Id { get; set; }
        public Guid DealId { get; set; }
        public Guid DealDocumentId { get; set; }
        public string DocumentTitle { get; set; } = string.Empty;
        public string DocumentFileName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public Guid? ActorUserId { get; set; }
        public string ActorDisplayName { get; set; } = string.Empty;
        public string? ActorEmail { get; set; }
        public string ActorRole { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public DateTime CreatedDate { get; set; }
        public string DealSummary { get; set; } = string.Empty;
        public string ClientSummary { get; set; } = string.Empty;
        public string RealtorSummary { get; set; } = string.Empty;
    }

    private sealed class RealtorCandidateDto
    {
        public Guid RealtorId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
    }

    private sealed class RequirementDto
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public string DesiredType { get; set; } = "Undefined";
        public List<string>? DesiredTypes { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double SearchRadiusMeters { get; set; }
        public bool IgnoreArea { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public double MinArea { get; set; }
        public double? MaxArea { get; set; }
        public string? AddressQuery { get; set; }
        public double MinMatchPercentage { get; set; }
        public double PriceWeight { get; set; }
        public double AreaWeight { get; set; }
        public bool IsActive { get; set; }
        public List<RequirementCriterionDto>? Criteria { get; set; }
    }

    private sealed class RequirementCriterionDto
    {
        public Guid CriterionDefinitionId { get; set; }
        public string Priority { get; set; } = "Undefined";
        public string? Value { get; set; }
        public List<string>? Values { get; set; }
    }
}
