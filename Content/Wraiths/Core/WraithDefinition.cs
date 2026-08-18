using CalamityOverhaul.Content.Wraiths.Marks;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    internal enum WraithCatalogState : byte
    {
        Usable,
        Archive,
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
        /// <summary>每次有效结算推进的复苏量；满格即厉鬼夺身。</summary>
        internal virtual float RevivalCost => 0f;
        internal virtual float ErosionCost => 0f;
        internal bool CanEquip => CatalogState == WraithCatalogState.Usable;
        /// <summary>该鬼往猎物身上留的状态（Flags 并集）；None = 不留状态。</summary>
        internal virtual WraithMark Emits => WraithMark.None;
        /// <summary>该鬼的灵异叠加消费规则；注册表惰性收集，结印盘边名由此推导。</summary>
        internal virtual WraithSynergyRule[] BuildSynergyRules() => [];

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

        /// <summary>该鬼的夺身死亡演出；返回 null 时使用通用兜底演出。每次夺身新建实例。</summary>
        internal virtual Deaths.WraithDeathPerformance CreateDeathPerformance() => null;

        internal void LoadLocalization() {
            DisplayName = this.GetLocalization("DisplayName", () => "???");
            Origin = this.GetLocalization("Origin", () => "...");
            Power = this.GetLocalization("Power", () => "...");
            DeathReason = this.GetLocalization("DeathReason", () => "{0}触犯了不可触犯之物");
        }
    }
}
