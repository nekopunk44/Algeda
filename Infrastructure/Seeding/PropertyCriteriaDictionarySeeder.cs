using Application.Interfaces;
using Application.Seeding;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Seeding;

public sealed class PropertyCriteriaDictionarySeeder(
    AppDbContext dbContext,
    ILogger<PropertyCriteriaDictionarySeeder> logger) : IPropertyCriteriaDictionarySeeder
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly ILogger<PropertyCriteriaDictionarySeeder> _logger = logger;

    public async Task<PropertyCriteriaSeedResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        var dictionary = BuildDictionary();
        var createdDefinitions = 0;
        var updatedDefinitions = 0;

        foreach (var item in dictionary)
        {
            var existing = await _dbContext.PropertyCriterionDefinitions
                .Include(x => x.Options)
                .FirstOrDefaultAsync(x => x.Code == item.Code, cancellationToken);

            if (existing is null)
            {
                var newDefinition = new PropertyCriterionDefinition(
                    item.Code,
                    item.DisplayName,
                    item.ValueType,
                    item.Category,
                    item.Description,
                    isHidden: false,
                    options: item.Options.Select(x => (x.Value, x.Label, x.SortOrder)));

                await _dbContext.PropertyCriterionDefinitions.AddAsync(newDefinition, cancellationToken);
                createdDefinitions++;
                continue;
            }

            if (!RequiresUpdate(existing, item))
            {
                continue;
            }

            existing.Update(
                item.Code,
                item.DisplayName,
                item.ValueType,
                item.Category,
                item.Description,
                existing.IsHidden,
                item.Options.Select(x => (x.Value, x.Label, x.SortOrder)));

            updatedDefinitions++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = new PropertyCriteriaSeedResult(
            TotalDefinitionsInDictionary: dictionary.Count,
            TotalOptionsInDictionary: dictionary.Sum(x => x.Options.Count),
            CreatedDefinitions: createdDefinitions,
            UpdatedDefinitions: updatedDefinitions);

        _logger.LogInformation(
            "Справочник критериев объектов заполнен. TotalDefinitions={Definitions}; TotalOptions={Options}; Created={Created}; Updated={Updated}",
            result.TotalDefinitionsInDictionary,
            result.TotalOptionsInDictionary,
            result.CreatedDefinitions,
            result.UpdatedDefinitions);

        return result;
    }

    private static bool RequiresUpdate(PropertyCriterionDefinition existing, SeedDefinition target)
    {
        if (!string.Equals(existing.DisplayName, target.DisplayName, StringComparison.Ordinal)
            || !string.Equals(existing.Category, target.Category, StringComparison.Ordinal)
            || !string.Equals(existing.Description, target.Description, StringComparison.Ordinal)
            || existing.ValueType != target.ValueType)
        {
            return true;
        }

        var existingOptions = existing.Options
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
            .Select(x => new SeedOption(x.Value, x.Label, x.SortOrder))
            .ToList();

        var targetOptions = target.Options
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (existingOptions.Count != targetOptions.Count)
        {
            return true;
        }

        for (var i = 0; i < existingOptions.Count; i++)
        {
            var current = existingOptions[i];
            var desired = targetOptions[i];

            if (!string.Equals(current.Value, desired.Value, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(current.Label, desired.Label, StringComparison.Ordinal)
                || current.SortOrder != desired.SortOrder)
            {
                return true;
            }
        }

        return false;
    }

    private static List<SeedDefinition> BuildDictionary()
    {
        return
        [
            // 1. Конструктивно-технические
            new(
                "build_year_category",
                "Возраст здания",
                PropertyCriterionValueType.SingleSelect,
                "Конструктивно-технические",
                "Категория возраста здания.",
                [
                    new("<10", "До 10 лет", 1),
                    new("10-25", "10-25 лет", 2),
                    new("25-50", "25-50 лет", 3),
                    new(">50", "Более 50 лет", 4)
                ]),
            new(
                "wall_material",
                "Материал стен",
                PropertyCriterionValueType.SingleSelect,
                "Конструктивно-технические",
                "Основной материал стен здания.",
                [
                    new("brick", "Кирпич", 1),
                    new("monolith", "Монолит", 2),
                    new("panel", "Панель", 3)
                ]),
            new(
                "floor_material",
                "Материал перекрытий",
                PropertyCriterionValueType.SingleSelect,
                "Конструктивно-технические",
                "Тип межэтажных перекрытий.",
                [
                    new("concrete", "Железобетон", 1),
                    new("wood", "Деревянные", 2)
                ]),
            new(
                "elevator_type",
                "Тип лифта",
                PropertyCriterionValueType.SingleSelect,
                "Конструктивно-технические",
                "Наличие и тип лифта.",
                [
                    new("none", "Нет лифта", 1),
                    new("standard", "Пассажирский", 2),
                    new("cargo_included", "Пассажирский + грузовой", 3)
                ]),

            // 2. Инфраструктура/локация/экология
            new(
                "distance_to_center",
                "Близость к центру",
                PropertyCriterionValueType.SingleSelect,
                "Инфраструктура/локация/экология",
                "Расположение относительно центра города.",
                [
                    new("center", "Центр", 1),
                    new("adjacent", "Прилегающий район", 2),
                    new("remote", "Удаленный район", 3)
                ]),
            new(
                "social_infra",
                "Социальная инфраструктура",
                PropertyCriterionValueType.SingleSelect,
                "Инфраструктура/локация/экология",
                "Доступность школ, садов, поликлиник и сервисов.",
                [
                    new("full", "Полная", 1),
                    new("partial", "Частичная", 2),
                    new("remote", "Удаленная", 3)
                ]),
            new(
                "transport_distance",
                "Транспортная доступность",
                PropertyCriterionValueType.SingleSelect,
                "Инфраструктура/локация/экология",
                "Расстояние до остановки общественного транспорта.",
                [
                    new("<500m", "До 500 м", 1),
                    new("500-1000m", "500-1000 м", 2),
                    new(">1000m", "Более 1000 м", 3)
                ]),
            new(
                "air_quality",
                "Качество воздуха",
                PropertyCriterionValueType.SingleSelect,
                "Инфраструктура/локация/экология",
                "Оценка экологического состояния района.",
                [
                    new("good", "Хорошее", 1),
                    new("average", "Среднее", 2),
                    new("bad", "Плохое", 3)
                ]),
            new(
                "river_nearby",
                "Рядом река",
                PropertyCriterionValueType.Boolean,
                "Инфраструктура/локация/экология",
                "Есть ли река или набережная в пешей доступности.",
                []),

            // 3. Внутренний комфорт
            new(
                "kitchen_area",
                "Площадь кухни",
                PropertyCriterionValueType.Number,
                "Внутренний комфорт",
                "Площадь кухни в квадратных метрах.",
                []),
            new(
                "ceiling_height",
                "Высота потолков",
                PropertyCriterionValueType.Number,
                "Внутренний комфорт",
                "Высота потолков в метрах.",
                []),
            new(
                "bathroom_type",
                "Тип санузла",
                PropertyCriterionValueType.SingleSelect,
                "Внутренний комфорт",
                "Раздельный или совмещенный санузел.",
                [
                    new("separated", "Раздельный", 1),
                    new("combined", "Совмещенный", 2)
                ]),
            new(
                "balcony_type",
                "Балкон/лоджия",
                PropertyCriterionValueType.SingleSelect,
                "Внутренний комфорт",
                "Наличие и тип балкона.",
                [
                    new("none", "Нет", 1),
                    new("balcony", "Балкон", 2),
                    new("loggia", "Лоджия", 3)
                ]),
            new(
                "heating_system",
                "Система отопления",
                PropertyCriterionValueType.SingleSelect,
                "Внутренний комфорт",
                "Тип отопления квартиры.",
                [
                    new("central", "Центральное", 1),
                    new("individual", "Индивидуальное", 2)
                ]),

            // 4. Дополнительные удобства
            new(
                "parking_type",
                "Парковка",
                PropertyCriterionValueType.SingleSelect,
                "Доп. удобства",
                "Тип парковки для жильцов.",
                [
                    new("none", "Нет", 1),
                    new("open", "Открытая", 2),
                    new("underground", "Подземная", 3)
                ]),
            new(
                "roof_access",
                "Доступ к крыше",
                PropertyCriterionValueType.Boolean,
                "Доп. удобства",
                "Есть ли доступ на эксплуатируемую крышу.",
                []),
            new(
                "internet_quality",
                "Качество интернета",
                PropertyCriterionValueType.SingleSelect,
                "Доп. удобства",
                "Доступная технология подключения интернета.",
                [
                    new("fiber", "Оптика", 1),
                    new("cable", "Кабель", 2),
                    new("poor", "Слабый/нестабильный", 3)
                ])
        ];
    }

    private sealed record SeedDefinition(
        string Code,
        string DisplayName,
        PropertyCriterionValueType ValueType,
        string Category,
        string Description,
        IReadOnlyCollection<SeedOption> Options);

    private sealed record SeedOption(string Value, string Label, int SortOrder);
}
