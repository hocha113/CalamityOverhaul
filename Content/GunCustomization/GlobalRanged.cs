using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;

namespace CalamityOverhaul.Content.GunCustomization
{
    public abstract class GlobalRanged
    {
        public virtual Item ChooseAmmo(Item weapon) {
            return null;
        }

        public virtual void PostModifyBow(BaseBow bow) {

        }
    }
}
