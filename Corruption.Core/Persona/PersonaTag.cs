namespace Corruption.Core;

/// The fifth SimpleHeels tag: the personality and traits by id. Unknown ids are kept, so a newer client's additions don't
/// break the tag; what shows them leaves out the ones it doesn't know.
public readonly record struct PersonaTag(string Personality, IReadOnlyList<string> Traits)
{
    public const string Key = "Corruption.Persona";

    private const string Prefix = "R1";
    private const string NoTraits = "-";

    /// Like `R1|shy|hypersexual,sensitive`, or `R1|easygoing|-`.
    public string Format() => $"{Prefix}|{Personality}|{(Traits.Count == 0 ? NoTraits : string.Join(',', Traits))}";

    /// Null for anything that isn't a well-formed tag of this version.
    public static PersonaTag? Parse(string? text)
    {
        var parts = text?.Split('|');
        if (parts is not { Length: 3 } || parts[0] != Prefix || !PersonaCatalog.IsValidId(parts[1])) return null;
        if (parts[2] == NoTraits) return new PersonaTag(parts[1], []);
        var traits = parts[2].Split(',');
        if (traits.Length > PersonaCatalog.MaxTraits || !traits.All(PersonaCatalog.IsValidId)) return null;
        return new PersonaTag(parts[1], traits);
    }

    /// What a character's persona sends: Easygoing with no traits until one is chosen.
    public static PersonaTag Of(CharacterSave save) => new(PersonaBook.Personality(save).Id, save.Setup?.Traits.ToList() ?? []);

    /// The ones this version knows.
    public PersonaDef? KnownPersonality => PersonaCatalog.FindPersonality(Personality);

    public IEnumerable<PersonaDef> KnownTraits => Traits.Select(PersonaCatalog.FindTrait).OfType<PersonaDef>();

    // Records compare lists by reference; a tag compares by what it says, so the publisher can tell when it changed.
    public bool Equals(PersonaTag other) => Personality == other.Personality && Traits.SequenceEqual(other.Traits);

    public override int GetHashCode() => HashCode.Combine(Personality, string.Join(',', Traits));
}

/// Writes the persona tag only when it changes, which only happens at setup.
public sealed class PersonaTagPublisher
{
    private PersonaTag? published;

    public void Forget() => published = null;

    public PersonaTag? Update(PersonaTag wanted)
    {
        if (published is { } last && last.Equals(wanted)) return null;
        published = wanted;
        return wanted;
    }
}
