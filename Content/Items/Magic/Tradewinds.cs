using CalamityMod.Items;
using CalamityMod.Projectiles.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    /// <summary>
    /// 信风
    /// </summary>
    internal class Tradewinds : EctypeItem
    {
        public new string LocalizationCategory => "Items.Weapons.Magic";

        public override string Texture => CWRConstant.Cay_Wap_Magic + "Tradewinds";

        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.damage = 25;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 5;
            Item.width = 28;
            Item.height = 30;
            Item.useTime = 12;
            Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 5f;
            Item.value = CalamityGlobalItem.Rarity3BuyPrice;
            Item.rare = ItemRarityID.Orange;
            Item.UseSound = SoundID.Item7;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<TradewindsProjectile>();
            Item.shootSpeed = 20f;
            
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int proj = Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            if (player.altFunctionUse == 2) {
                Main.projectile[proj].ai[0] = 2;
                for (int i = 0; i <= 360; i += 3) {
                    Vector2 vr = new Vector2(3f, 3f).RotatedBy(MathHelper.ToRadians(i));
                    int num = Dust.NewDust(player.Center, player.width, player.height, DustID.Smoke, vr.X, vr.Y, 200, new Color(232, 251, 250, 200), 1.4f);
                    Main.dust[num].noGravity = true;
                    Main.dust[num].position = player.Center + velocity.UnitVector() * 23;
                    Main.dust[num].velocity = vr;
                }
            }
            else {
                Main.projectile[proj].penetrate = 13;
            }
            return false;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            Item.useTime = 12;
            Item.useAnimation = 12;
            if (player.altFunctionUse == 2) {
                Item.useTime = 18;
                Item.useAnimation = 18;
                type = ModContent.ProjectileType<Feathers>();
            }
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }
    }
}
