using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Platform.Domain.Reference;

/// <summary>
/// A row of a reference table (countries, currencies, languages, time zones): platform-global, read-only for the
/// runtime roles and written only by the REFERENCE seeders. Entries are never deleted: one dropped from the data is
/// deactivated, and comes back active if the data lists it again.
/// </summary>
public abstract class ReferenceEntry : Entity
{
    protected ReferenceEntry(Guid id, string code)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
        IsActive = true;
    }

    protected ReferenceEntry()
    {
    }

    /// <summary>The natural key (ISO or IANA code); never changes.</summary>
    public string Code { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    /// <summary>Returns whether anything changed.</summary>
    public bool Deactivate()
    {
        if (!IsActive)
        {
            return false;
        }

        IsActive = false;
        return true;
    }

    /// <summary>Marks the entry active again; returns whether it was inactive.</summary>
    protected bool Reactivate()
    {
        if (IsActive)
        {
            return false;
        }

        IsActive = true;
        return true;
    }

    /// <summary>Sets <paramref name="field"/> and returns whether its value changed.</summary>
    protected static bool Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        return true;
    }
}
