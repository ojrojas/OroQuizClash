namespace QuizArena.Admin.Client.Models.Rewards;

public sealed record RewardForm(
    string Name,
    string? Description,
    RewardType Type,
    int Cost,
    int Stock,
    DateTimeOffset? AvailableFrom,
    DateTimeOffset? AvailableTo)
{
    public IReadOnlyDictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        var name = Name?.Trim() ?? string.Empty;
        if (name.Length is < 3 or > 100)
            errors[nameof(Name)] = ["El nombre debe tener 3-100 caracteres."];

        if (Description is not null && Description.Length > 500)
            errors[nameof(Description)] = ["La descripción debe tener como máximo 500 caracteres."];

        if (!Enum.IsDefined(Type))
            errors[nameof(Type)] = ["Tipo de premio no válido. Debe ser uno de los 6 valores."];

        if (Cost is < 1 or > 100000)
            errors[nameof(Cost)] = ["El costo debe estar entre 1 y 100000 puntos."];

        if (Stock < 0)
            errors[nameof(Stock)] = ["El stock debe ser 0 o positivo."];

        if (AvailableFrom.HasValue && AvailableTo.HasValue && AvailableFrom.Value >= AvailableTo.Value)
            errors["Availability"] = ["La fecha de inicio debe ser anterior a la fecha de fin."];

        return errors;
    }

    public bool IsValid => Validate().Count == 0;
}

public sealed record CreateRewardRequest(
    string Name,
    string? Description,
    RewardType Type,
    int Cost,
    int Stock,
    DateTimeOffset? AvailableFrom,
    DateTimeOffset? AvailableTo);

public sealed record UpdateRewardRequest(
    string Name,
    string? Description,
    RewardType Type,
    int Cost,
    int Stock,
    DateTimeOffset? AvailableFrom,
    DateTimeOffset? AvailableTo,
    string RowVersion);
