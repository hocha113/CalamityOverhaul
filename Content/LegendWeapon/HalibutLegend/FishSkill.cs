using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal abstract class FishSkill : VaultType<FishSkill>, ILocalizedModType
    {
        public string LocalizationCategory => "FishSkill";
        public LocalizedText DisplayName => this.GetLocalization(nameof(DisplayName), () => "");
        public LocalizedText Tooltip => this.GetLocalization(nameof(Tooltip), () => "");
        public readonly static Dictionary<Type, int> TypeToID = [];
        public int ID => TypeToID[GetType()];
        protected override void VaultRegister() {
            TypeToID[GetType()] = Instances.Count;
            Instances.Add(this);
        }

        public override void VaultSetup() {
            _ = DisplayName;
            _ = Tooltip;
            SetStaticDefaults();
            SetDefaults(true);
        }

        public virtual void SetDefaults(bool create = false) {

        }

        public virtual void Use(Item item, Player player) {

        }
    }
}
