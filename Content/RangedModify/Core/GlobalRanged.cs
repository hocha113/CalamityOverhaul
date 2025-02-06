using Terraria;

namespace CalamityOverhaul.Content.RangedModify.Core
{
    public abstract class GlobalRanged
    {
        public virtual Item ChooseAmmo(Item weapon) {
            return null;
        }

        public virtual void PostModifyBow(BaseBow bow) {

        }

        public virtual bool? CanUpdateMagazine(BaseFeederGun baseFeederGun) {
            return null;
        }
    }
}
