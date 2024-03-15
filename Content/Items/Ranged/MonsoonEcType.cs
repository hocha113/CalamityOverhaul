using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;
using CalamityMod.Items.Weapons.Ranged;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class MonsoonEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Monsoon";
        public override void SetDefaults() {
            Item.SetCalamitySD<Monsoon>();
            Item.SetHeldProj<MonsoonHeldProj>();
        }
    }
}
