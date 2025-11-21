using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RGrandGuardian : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.shoot = ModContent.ProjectileType<GrandGuardianRapier>();
            item.GiveMeleeType();
            item.useTime = 30;
            item.useAnimation = 30;
            item.autoReuse = true;
            item.useStyle = ItemUseStyleID.Shoot;
            item.UseSound = null;
            item.knockBack = 3.5f;
            item.shootSpeed = 5f;
            item.noUseGraphic = true;
            item.noMelee = true;
            item.channel = true;
        }

        public override bool? On_CanUseItem(Item item, Player player) => player.ownedProjectileCounts[item.shoot] <= 0;
    }
}
