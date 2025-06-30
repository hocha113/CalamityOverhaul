using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RLucrecia : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<Lucrecia>();
        public override void SetDefaults(Item item) {
            item.shoot = ModContent.ProjectileType<LucreciaRapier>();
            item.useTime = 60;
            item.useAnimation = 60;
            item.autoReuse = true;
            item.UseSound = null;
            item.useStyle = ItemUseStyleID.Shoot;
            item.knockBack = 3.5f;
            item.shootSpeed = 5f;
            item.noUseGraphic = true;
            item.noMelee = true;
            item.channel = true;
        }
        public override bool? On_CanUseItem(Item item, Player player)
            => player.ownedProjectileCounts[item.shoot] <= 0;
    }
}
