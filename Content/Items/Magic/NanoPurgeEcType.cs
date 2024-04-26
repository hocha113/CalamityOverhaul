using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class NanoPurgeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "NanoPurge";
        public override void SetDefaults() {
            Item.SetCalamitySD<NanoPurge>();
            Item.SetHeldProj<NanoPurgeHeldProj>();
        }
    }
}
