using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class GrandGuardianEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "GrandGuardian";
        public override void SetDefaults() {
            Item.SetItemCopySD<GrandGuardian>();
            Item.DamageType = DamageClass.Melee;
            Item.shoot = ModContent.ProjectileType<GrandGuardianRapier>();
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3.5f;
            Item.shootSpeed = 5f;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.channel = true;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[Item.shoot] <= 0;
        }
    }
}
