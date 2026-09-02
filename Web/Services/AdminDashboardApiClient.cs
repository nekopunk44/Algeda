using System.Net;
using Web.Models.Api;
using Web.Models.Dashboard;

namespace Web.Services;

public sealed record CriterionOptionInput(string Value, string Label, int SortOrder);

public class AdminDashboardApiClient
{
    private readonly HttpClient _httpClient;

    public AdminDashboardApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<ApiClientResult<IReadOnlyList<DashboardClientViewModel>>> GetClientsAsync(
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        return GetListAsync<ClientDto, DashboardClientViewModel>(
            $"api/clients?limit={Math.Clamp(limit, 1, 500)}",
            dto => new DashboardClientViewModel
            {
                Id = dto.Id,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                MiddleName = dto.MiddleName,
                PhoneNumber = dto.PhoneNumber,
                Email = dto.Email,
                CreatedDate = dto.CreatedDate
            },
            "API вернул пустой список клиентов.",
            cancellationToken);
    }

    public Task<ApiOperationResult> CreateClientAsync(
        string firstName,
        string lastName,
        string? middleName,
        string phoneNumber,
        string? email,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Post, "api/clients", new
        {
            firstName,
            lastName,
            middleName,
            phoneNumber,
            email
        }, cancellationToken);
    }

    public Task<ApiOperationResult> UpdateClientAsync(
        Guid clientId,
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Put, $"api/clients/{clientId}", new
        {
            id = clientId,
            phoneNumber
        }, cancellationToken);
    }

    public Task<ApiOperationResult> DeleteClientAsync(
        Guid clientId,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Delete, $"api/clients/{clientId}", null, cancellationToken);
    }

    public async Task<ApiClientResult<DashboardCriteriaPageViewModel>> GetCriteriaPagedAsync(
        int page,
        int pageSize,
        bool includeHidden,
        string? search,
        string? sort,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"page={Math.Max(page, 1)}",
            $"pageSize={Math.Clamp(pageSize, 1, 100)}",
            $"includeHidden={(includeHidden ? "true" : "false")}"
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(sort))
        {
            query.Add($"sort={Uri.EscapeDataString(sort.Trim())}");
        }

        var relativeUrl = $"api/property-criteria/paged?{string.Join("&", query)}";

        try
        {
            var response = await _httpClient.GetAsync(relativeUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<DashboardCriteriaPageViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<PagedCriteriaDto>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<DashboardCriteriaPageViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API вернул пустую страницу критериев."));
            }

            var items = (payload.Items ?? [])
                .Select(dto => new DashboardCriterionViewModel
                {
                    Id = dto.Id,
                    Code = dto.Code,
                    DisplayName = dto.DisplayName,
                    ValueType = dto.ValueType,
                    Category = dto.Category,
                    Description = dto.Description,
                    IsHidden = dto.IsHidden,
                    Options = (dto.Options ?? [])
                        .Select(o => new DashboardCriterionOptionViewModel
                        {
                            Value = o.Value,
                            Label = o.Label,
                            SortOrder = o.SortOrder
                        })
                        .OrderBy(o => o.SortOrder)
                        .ThenBy(o => o.Value)
                        .ToList(),
                    CreatedDate = dto.CreatedDate
                })
                .ToList();

            return ApiClientResult<DashboardCriteriaPageViewModel>.Success(new DashboardCriteriaPageViewModel
            {
                Page = payload.Page,
                PageSize = payload.PageSize,
                TotalCount = payload.TotalCount,
                Items = items
            });
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<DashboardCriteriaPageViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<DashboardCriteriaPageViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<IReadOnlyList<DashboardCriterionViewModel>>> GetCriteriaAsync(
        int limit = 500,
        bool includeHidden = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var safeLimit = Math.Clamp(limit, 1, 500);
            var includeHiddenValue = includeHidden ? "true" : "false";

            var response = await _httpClient.GetAsync(
                $"api/property-criteria?limit={safeLimit}&includeHidden={includeHiddenValue}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<DashboardCriterionViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<CriterionDto>>(
                ApiClientSupport.JsonOptions,
                cancellationToken);

            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<DashboardCriterionViewModel>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API вернул пустой список критериев."));
            }

            var items = payload
                .Select(dto => new DashboardCriterionViewModel
                {
                    Id = dto.Id,
                    Code = dto.Code,
                    DisplayName = dto.DisplayName,
                    ValueType = dto.ValueType,
                    Category = dto.Category,
                    Description = dto.Description,
                    IsHidden = dto.IsHidden,
                    Options = (dto.Options ?? [])
                        .Select(o => new DashboardCriterionOptionViewModel
                        {
                            Value = o.Value,
                            Label = o.Label,
                            SortOrder = o.SortOrder
                        })
                        .OrderBy(o => o.SortOrder)
                        .ThenBy(o => o.Value)
                        .ToList(),
                    CreatedDate = dto.CreatedDate
                })
                .ToList();

            return ApiClientResult<IReadOnlyList<DashboardCriterionViewModel>>.Success(items);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<DashboardCriterionViewModel>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<DashboardCriterionViewModel>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public Task<ApiOperationResult> CreateCriterionAsync(
        string code,
        string displayName,
        string valueType,
        string? category,
        string? description,
        bool isHidden,
        IReadOnlyList<CriterionOptionInput>? options,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Post, "api/property-criteria", new
        {
            code,
            displayName,
            valueType,
            category,
            description,
            isHidden,
            options = MapOptions(options)
        }, cancellationToken);
    }

    public Task<ApiOperationResult> UpdateCriterionAsync(
        Guid criterionId,
        string code,
        string displayName,
        string valueType,
        string? category,
        string? description,
        bool isHidden,
        IReadOnlyList<CriterionOptionInput>? options,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Put, $"api/property-criteria/{criterionId}", new
        {
            code,
            displayName,
            valueType,
            category,
            description,
            isHidden,
            options = MapOptions(options)
        }, cancellationToken);
    }

    public Task<ApiOperationResult> SetCriterionHiddenAsync(
        Guid criterionId,
        bool isHidden,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Patch, $"api/property-criteria/{criterionId}/hidden", new
        {
            isHidden
        }, cancellationToken);
    }

    public Task<ApiOperationResult> DeleteCriterionAsync(
        Guid criterionId,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Delete, $"api/property-criteria/{criterionId}", null, cancellationToken);
    }

    public async Task<ApiClientResult<IReadOnlyList<DashboardCurrencyViewModel>>> GetCurrenciesAsync(
        int limit = 300,
        bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"api/currencies?limit={Math.Clamp(limit, 1, 500)}&includeInactive={(includeInactive ? "true" : "false")}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<DashboardCurrencyViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<CurrencyDto>>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<DashboardCurrencyViewModel>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API вернул пустой список валют."));
            }

            var mapped = payload
                .OrderBy(x => x.Code)
                .Select(x => new DashboardCurrencyViewModel
                {
                    Id = x.Id,
                    Code = x.Code,
                    Name = x.Name,
                    Symbol = x.Symbol,
                    RateToBase = x.RateToBase,
                    IsActive = x.IsActive,
                    UpdatedAtUtc = x.UpdatedAtUtc,
                    CreatedDate = x.CreatedDate
                })
                .ToList();

            return ApiClientResult<IReadOnlyList<DashboardCurrencyViewModel>>.Success(mapped);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<DashboardCurrencyViewModel>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<DashboardCurrencyViewModel>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public Task<ApiOperationResult> CreateCurrencyAsync(
        string code,
        string name,
        string symbol,
        decimal rateToBase,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Post, "api/currencies", new
        {
            code,
            name,
            symbol,
            rateToBase,
            isActive
        }, cancellationToken);
    }

    public Task<ApiOperationResult> UpdateCurrencyAsync(
        Guid currencyId,
        string code,
        string name,
        string symbol,
        decimal rateToBase,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Put, $"api/currencies/{currencyId}", new
        {
            code,
            name,
            symbol,
            rateToBase,
            isActive
        }, cancellationToken);
    }

    public Task<ApiOperationResult> SetCurrencyActiveAsync(
        Guid currencyId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Patch, $"api/currencies/{currencyId}/active", new
        {
            isActive
        }, cancellationToken);
    }

    public Task<ApiOperationResult> DeleteCurrencyAsync(
        Guid currencyId,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Delete, $"api/currencies/{currencyId}", null, cancellationToken);
    }

    public async Task<ApiClientResult<IReadOnlyList<DashboardIdentityUserViewModel>>> GetUserAccessAsync(
        string? search,
        string? role,
        int limit = 200,
        bool excludeClients = true,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"limit={Math.Clamp(limit, 1, 500)}",
            $"excludeClients={(excludeClients ? "true" : "false")}"
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            query.Add($"role={Uri.EscapeDataString(role.Trim())}");
        }

        return await GetListAsync<UserAccessDto, DashboardIdentityUserViewModel>(
            $"api/access-management/users?{string.Join("&", query)}",
            dto => new DashboardIdentityUserViewModel
            {
                UserId = dto.UserId,
                Email = dto.Email,
                DisplayName = dto.DisplayName,
                EmailConfirmed = dto.EmailConfirmed,
                IsFrozen = dto.IsFrozen,
                Roles = dto.Roles.OrderBy(x => x).ToList()
            },
            "API вернул пустой список пользователей.",
            cancellationToken);
    }

    public Task<ApiOperationResult> AssignRoleAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Post, $"api/access-management/users/{userId}/roles/{Uri.EscapeDataString(role)}", null, cancellationToken);
    }

    public Task<ApiOperationResult> RemoveRoleAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Delete, $"api/access-management/users/{userId}/roles/{Uri.EscapeDataString(role)}", null, cancellationToken);
    }

    public Task<ApiOperationResult> TransferSuperAdminAsync(
        Guid userId,
        string password,
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Post, $"api/access-management/users/{userId}/transfer-super-admin", new
        {
            targetUserId = userId,
            password,
            confirmed
        }, cancellationToken);
    }

    public Task<ApiOperationResult> FreezeAccountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Post, $"api/access-management/users/{userId}/freeze", null, cancellationToken);
    }

    public Task<ApiOperationResult> UnfreezeAccountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Delete, $"api/access-management/users/{userId}/freeze", null, cancellationToken);
    }

    public async Task<ApiClientResult<IReadOnlyList<DashboardRealtorRegistrationRequestViewModel>>> GetRealtorRequestsAsync(
        string? status,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"limit={Math.Clamp(limit, 1, 500)}"
        };

        if (!string.IsNullOrWhiteSpace(status)
            && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            query.Add($"status={Uri.EscapeDataString(status.Trim())}");
        }

        return await GetListAsync<RealtorRequestDto, DashboardRealtorRegistrationRequestViewModel>(
            $"api/realtor-registration-requests?{string.Join("&", query)}",
            dto => new DashboardRealtorRegistrationRequestViewModel
            {
                Id = dto.Id,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                MiddleName = dto.MiddleName,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                IdentityUserId = dto.IdentityUserId,
                Status = dto.Status,
                ReviewComment = dto.ReviewComment,
                ReviewedAt = dto.ReviewedAt,
                CreatedDate = dto.CreatedDate
            },
            "API вернул пустой список заявок риелторов.",
            cancellationToken);
    }

    public Task<ApiOperationResult> ApproveRealtorRequestAsync(
        Guid requestId,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Patch, $"api/realtor-registration-requests/{requestId}/approve", new
        {
            comment
        }, cancellationToken);
    }

    public Task<ApiOperationResult> RejectRealtorRequestAsync(
        Guid requestId,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Patch, $"api/realtor-registration-requests/{requestId}/reject", new
        {
            comment
        }, cancellationToken);
    }

    public Task<ApiClientResult<IReadOnlyList<DashboardComplaintViewModel>>> GetComplaintsAsync(
        int limit = 200,
        string? status = null,
        string? category = null,
        bool? dealLinked = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"limit={Math.Clamp(limit, 1, 500)}"
        };

        if (!string.IsNullOrWhiteSpace(status)
            && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            query.Add($"status={Uri.EscapeDataString(status.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query.Add($"category={Uri.EscapeDataString(category.Trim())}");
        }

        if (dealLinked.HasValue)
        {
            query.Add($"dealLinked={(dealLinked.Value ? "true" : "false")}");
        }

        return GetListAsync<ComplaintDto, DashboardComplaintViewModel>(
            $"api/complaints?{string.Join("&", query)}",
            MapComplaint,
            "API вернул пустой список жалоб.",
            cancellationToken);
    }

    public Task<ApiClientResult<IReadOnlyList<DashboardComplaintViewModel>>> GetOpenComplaintsAsync(
        CancellationToken cancellationToken = default)
    {
        return GetListAsync<ComplaintDto, DashboardComplaintViewModel>(
            "api/complaints/open",
            MapComplaint,
            "API вернул пустой список открытых жалоб.",
            cancellationToken);
    }

    public async Task<ApiClientResult<DashboardComplaintViewModel>> GetComplaintByIdAsync(
        Guid complaintId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/complaints/{complaintId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<DashboardComplaintViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<ComplaintDto>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<DashboardComplaintViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API вернул пустую жалобу."));
            }

            return ApiClientResult<DashboardComplaintViewModel>.Success(MapComplaint(payload));
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<DashboardComplaintViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<DashboardComplaintViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public Task<ApiOperationResult> MarkComplaintInProgressAsync(
        Guid complaintId,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Patch, $"api/complaints/{complaintId}/in-progress", null, cancellationToken);
    }

    public Task<ApiOperationResult> MarkComplaintOpenedAsync(
        Guid complaintId,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Patch, $"api/complaints/{complaintId}/opened", null, cancellationToken);
    }

    public Task<ApiOperationResult> ResolveComplaintAsync(
        Guid complaintId,
        string verdict,
        string resolution,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Patch, $"api/complaints/{complaintId}/resolve", new
        {
            complaintId,
            verdict,
            resolution
        }, cancellationToken);
    }

    public async Task<ApiClientResult<DashboardHealthViewModel>> GetDbHealthAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await GetHealthAsync("api/health/db", cancellationToken);
        if (result.Data is null)
        {
            return ApiClientResult<DashboardHealthViewModel>.Failure(
                result.Error ?? ApiClientSupport.BuildEmptyPayloadError("API вернул пустой health payload."));
        }

        var data = result.Data;
        var detailsParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(data.Database))
        {
            detailsParts.Add($"База: {data.Database}");
        }

        if (data.PendingMigrationsCount.HasValue)
        {
            detailsParts.Add($"Ожидающих миграций: {data.PendingMigrationsCount.Value}");
        }

        if (!string.IsNullOrWhiteSpace(data.Error))
        {
            detailsParts.Add(data.Error);
        }

        return ApiClientResult<DashboardHealthViewModel>.Success(new DashboardHealthViewModel
        {
            Status = data.Status,
            Alive = string.Equals(data.Status, "ok", StringComparison.OrdinalIgnoreCase),
            TimestampUtc = data.TimestampUtc,
            Details = detailsParts.Count == 0
                ? "Дополнительных деталей нет."
                : string.Join(" | ", detailsParts)
        });
    }

    public async Task<ApiClientResult<DashboardHealthViewModel>> GetAutoMatchingHealthAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await GetHealthAsync("api/health/auto-matching", cancellationToken);
        if (result.Data is null)
        {
            return ApiClientResult<DashboardHealthViewModel>.Failure(
                result.Error ?? ApiClientSupport.BuildEmptyPayloadError("API вернул пустой health payload."));
        }

        var data = result.Data;
        var detailsParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(data.Worker))
        {
            detailsParts.Add($"Воркер: {data.Worker}");
        }

        if (data.Enabled.HasValue)
        {
            detailsParts.Add($"Включен: {(data.Enabled.Value ? "да" : "нет")}");
        }

        if (data.Iteration.HasValue)
        {
            detailsParts.Add($"Итерация: {data.Iteration.Value}");
        }

        if (data.IsRunningIteration.HasValue)
        {
            detailsParts.Add($"Сейчас выполняется: {(data.IsRunningIteration.Value ? "да" : "нет")}");
        }

        if (!string.IsNullOrWhiteSpace(data.Error))
        {
            detailsParts.Add(data.Error);
        }

        return ApiClientResult<DashboardHealthViewModel>.Success(new DashboardHealthViewModel
        {
            Status = data.Status,
            Alive = data.Alive,
            TimestampUtc = data.TimestampUtc,
            Details = detailsParts.Count == 0
                ? "Дополнительных деталей нет."
                : string.Join(" | ", detailsParts)
        });
    }

    private static IReadOnlyList<object>? MapOptions(IReadOnlyList<CriterionOptionInput>? options)
    {
        if (options is null || options.Count == 0)
        {
            return null;
        }

        return options
            .Select(x => (object)new
            {
                value = x.Value,
                label = x.Label,
                sortOrder = x.SortOrder
            })
            .ToList();
    }

    private async Task<ApiClientResult<IReadOnlyList<TView>>> GetListAsync<TDto, TView>(
        string relativeUrl,
        Func<TDto, TView> mapper,
        string emptyMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(relativeUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<TView>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<TDto>>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<TView>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError(emptyMessage));
            }

            var mapped = payload.Select(mapper).ToList();
            return ApiClientResult<IReadOnlyList<TView>>.Success(mapped);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<TView>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<TView>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    private async Task<ApiClientResult<HealthDto>> GetHealthAsync(
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(relativeUrl, cancellationToken);
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.ServiceUnavailable)
            {
                return ApiClientResult<HealthDto>.Failure(await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<HealthDto>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<HealthDto>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API вернул пустой health payload."));
            }

            return ApiClientResult<HealthDto>.Success(payload);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<HealthDto>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<HealthDto>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    private static DashboardComplaintViewModel MapComplaint(ComplaintDto dto)
    {
        return new DashboardComplaintViewModel
        {
            Id = dto.Id,
            ClientId = dto.ClientId,
            TargetRealtorId = dto.TargetRealtorId,
            DealId = dto.DealId,
            PropertyId = dto.PropertyId,
            Category = dto.Category,
            Subject = dto.Subject,
            Description = dto.Description,
            Status = dto.Status,
            ModerationVerdict = dto.ModerationVerdict,
            AdminResolution = dto.AdminResolution,
            ResolvedAt = dto.ResolvedAt,
            CreatedDate = dto.CreatedDate
        };
    }

    private sealed class ClientDto
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    private sealed class PagedCriteriaDto
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public List<CriterionDto>? Items { get; set; }
    }

    private sealed class CriterionDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ValueType { get; set; } = "Undefined";
        public string? Category { get; set; }
        public string? Description { get; set; }
        public bool IsHidden { get; set; }
        public List<CriterionOptionDto>? Options { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    private sealed class CriterionOptionDto
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    private sealed class CurrencyDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "USD";
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public decimal RateToBase { get; set; }
        public bool IsActive { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    private sealed class UserAccessDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public bool EmailConfirmed { get; set; }
        public bool IsFrozen { get; set; }
        public List<string> Roles { get; set; } = [];
    }

    private sealed class RealtorRequestDto
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public Guid? IdentityUserId { get; set; }
        public string Status { get; set; } = "Pending";
        public string? ReviewComment { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    private sealed class ComplaintDto
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Guid? TargetRealtorId { get; set; }
        public Guid? DealId { get; set; }
        public Guid? PropertyId { get; set; }
        public string Category { get; set; } = "Undefined";
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "Undefined";
        public string ModerationVerdict { get; set; } = "Undefined";
        public string? AdminResolution { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    private sealed class HealthDto
    {
        public string Status { get; set; } = "unknown";
        public bool? Alive { get; set; }
        public DateTime? TimestampUtc { get; set; }
        public string? Database { get; set; }
        public int? PendingMigrationsCount { get; set; }
        public string? Worker { get; set; }
        public bool? Enabled { get; set; }
        public int? Iteration { get; set; }
        public bool? IsRunningIteration { get; set; }
        public string? Error { get; set; }
    }
}
