using Terraria;

namespace CalamityOverhaul.Content.GunCustomization
{
    public abstract class GlobalFeederGun
    {
        public virtual Item ChooseAmmo(Item weapon) {
            return null;
        }
    }
}
