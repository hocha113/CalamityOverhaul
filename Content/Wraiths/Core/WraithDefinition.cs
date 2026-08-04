using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    internal enum WraithCatalogState : byte
    {
        Usable,
        Archive,
        SealedArchive,
    }

    internal enum WraithAbilityKind : byte
    {
        None,
        ScapeGhost,
        HeadlessShade,
        GhostHand,
    }

    /// <summary>点鬼簿静态目录项。</summary>
    public abstract class WraithDefinition : ILocalizedModType
    {
        public Mod Mod => CWRMod.Instance;
        public string Name => GetType().Name;
        public string FullName => Mod.Name + "/" + Name;
        public string LocalizationCategory => "Wraiths";

        public virtual string Key => GetType().Name;
        public virtual int SortOrder => 0;
        internal virtual ushort NetworkId => ushort.MaxValue;
        internal virtual WraithCatalogState CatalogState => WraithCatalogState.Archive;
        internal virtual WraithAbilityKind AbilityKind => WraithAbilityKind.None;
        internal virtual float MasteryCost => 0f;
        internal virtual float ErosionCost => 0f;
        internal bool CanEquip => CatalogState == WraithCatalogState.Usable;

        public LocalizedText DisplayName { get; private set; }
        public LocalizedText Origin { get; private set; }
        public LocalizedText Power { get; private set; }
        public LocalizedText DeathReason { get; private set; }

        private WraithPassiveAbility ability;
        private bool abilityCreated;

        internal WraithPassiveAbility Ability {
            get {
                if (!abilityCreated) {
                    abilityCreated = true;
                    ability = CreateAbility();
                    if (ability != null) {
                        ability.Definition = this;
                    }
                }
                return ability;
            }
        }

        internal virtual WraithPassiveAbility CreateAbility() => null;

        internal void LoadLocalization() {
            DisplayName = this.GetLocalization("DisplayName", () => "???");
            Origin = this.GetLocalization("Origin", () => "...");
            Power = this.GetLocalization("Power", () => "...");
            DeathReason = this.GetLocalization("DeathReason", () => "{0}触犯了不可触犯之物");
        }
    }
}
