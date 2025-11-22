using CalamityOverhaul.Content.RangedModify.Core;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class CorrodedCaustibowHeld : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "CorrodedCaustibow";
        public override void SetShootAttribute() {
            if (AmmoTypes == ProjectileID.WoodenArrowFriendly) {
                AmmoTypes = CWRID.Proj_CorrodedShell;
            }
        }
    }
}
