using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV
{
    internal abstract class ModifyDisplayText : VaultType<ModifyDisplayText>, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";
        protected sealed override void VaultRegister() {
            Instances.Add(this);
        }
        public sealed override void VaultSetup() {
            SetStaticDefaults();
        }
        public virtual bool Handle(ref string key, ref Color color) {
            return true;
        }
    }
}
