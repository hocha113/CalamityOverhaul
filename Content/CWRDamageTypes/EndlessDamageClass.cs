using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.CWRDamageTypes
{
    internal class EndlessDamageClass : DamageClass
    {
        internal static EndlessDamageClass Instance;

        public override void Load() => Instance = this;
        public override void Unload() => Instance = null;

        public override string LocalizationCategory => "EndlessDamageClassTextContent";

        public override StatInheritanceData GetModifierInheritance(DamageClass damageClass) {
            return StatInheritanceData.Full;
        }

        public override bool GetEffectInheritance(DamageClass damageClass) => true;
    }
}
