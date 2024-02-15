using CalamityMod.Items;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 寒夜长枪
    /// </summary>
    internal class StarnightLance : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "StarnightLance";
        public new string LocalizationCategory => "Items.Weapons.Melee";
        public override void SetStaticDefaults() {
            ItemID.Sets.Spears[Item.type] = true;
        }

        public override void SetDefaults() {
            Item.width = 72;
            Item.damage = 110;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.useTurn = true;
            Item.noUseGraphic = true;
            Item.useAnimation = 23;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 23;
            Item.knockBack = 6;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 72;
            Item.value = CalamityGlobalItem.Rarity5BuyPrice;
            Item.rare = ItemRarityID.Pink;
            Item.shoot = ModContent.ProjectileType<RStarnightLanceProjectile>();
            Item.shootSpeed = 6f;
            
        }

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;
    }
}
