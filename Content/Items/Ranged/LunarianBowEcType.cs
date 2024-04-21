using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class LunarianBowEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "LunarianBow";
        public override void SetDefaults() {
            Item.SetCalamitySD<LunarianBow>();
            Item.damage = 15;
            Item.SetHeldProj<LunarianBowHeldProj>();
        }
    }
}
