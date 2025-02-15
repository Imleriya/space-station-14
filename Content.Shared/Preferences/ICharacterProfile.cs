using Content.Shared.Humanoid;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Shared.Preferences
{
    public interface ICharacterProfile
    {
        string Name { get; }

        ICharacterAppearance CharacterAppearance { get; }

        bool MemberwiseEquals(ICharacterProfile other);

        /// <summary>
        ///     Makes this profile valid so there's no bad data like negative ages.
        /// </summary>
        void EnsureValid(IConfigurationManager configManager, IPrototypeManager prototypeManager, string[] sponsorMarkings);  // Corvax-Sponsors: Integrated filtering for sponsor markings

        /// <summary>
        /// Gets a copy of this profile that has <see cref="EnsureValid"/> applied, i.e. no invalid data.
        /// </summary>
        ICharacterProfile Validated(IConfigurationManager configManager, IPrototypeManager prototypeManager, string[] sponsorMarkings);  // Corvax-Sponsors: Integrated filtering for sponsor markings
    }
}
