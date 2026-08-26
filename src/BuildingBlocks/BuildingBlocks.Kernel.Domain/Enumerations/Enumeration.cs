using System.Reflection;

namespace BuildingBlocks.Kernel.Domain.Enumerations;

/// <summary>
/// Smart-enum base class: an enum with behavior and persistence-friendly identity.
/// </summary>
public abstract class Enumeration<TEnum>(int id, string name) : IComparable<TEnum>
    where TEnum : Enumeration<TEnum>
{
    private static readonly Lazy<Dictionary<int, TEnum>> AllById = new(() =>
        typeof(TEnum)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => typeof(TEnum).IsAssignableFrom(f.FieldType))
            .Select(f => (TEnum)f.GetValue(null)!)
            .ToDictionary(e => e.Id));

    public int Id { get; } = id;

    public string Name { get; } = name;

    public static IReadOnlyCollection<TEnum> GetAll() => AllById.Value.Values;

    public static TEnum FromId(int id) =>
        AllById.Value.TryGetValue(id, out var value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(id), id, $"No {typeof(TEnum).Name} with id {id}.");

    public static TEnum FromName(string name) =>
        AllById.Value.Values.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentOutOfRangeException(nameof(name), name, $"No {typeof(TEnum).Name} named '{name}'.");

    public int CompareTo(TEnum? other) => other is null ? 1 : Id.CompareTo(other.Id);

    public override bool Equals(object? obj) => obj is Enumeration<TEnum> other && GetType() == other.GetType() && Id == other.Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public override string ToString() => Name;
}
