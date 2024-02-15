using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Summon.Whips;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Summon.Extras
{
    internal class GhostFireWhip : ModItem
    {
        public override string Texture => CWRConstant.Item_Summon + "GhostFireWhip";

        public override void SetDefaults()
        {
            Item.DefaultToWhip(ModContent.ProjectileType<GhostFireWhipProjectile>(), 522, 1, 12, 30);
            Item.rare = ItemRarityID.Purple;
        }

        public override bool MeleePrefix()
        {
            return true;
        }
    }
}
