using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.EtherRoarProj;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Extras
{
    internal class EtherRoar : ModItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "AethersWhisper";
        public override bool IsLoadingEnabled(Mod mod) => false;
        public override void SetDefaults() {
            Item.SetCalamitySD<AethersWhisper>();
            Item.useTime = 30;
            Item.SetHeldProj<EtherRoarHeldProj>();
        }
    }
}
