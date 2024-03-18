using CalamityMod.Items;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 风之刃
    /// </summary>
    internal class WindBladeEcType : EctypeItem
    {
        public new string LocalizationCategory => "Items.Weapons.Melee";

        public override string Texture => CWRConstant.Cay_Wap_Melee + "WindBlade";

        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 58;
            Item.damage = 41;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 20;
            Item.useStyle = 1;
            Item.useTime = 20;
            Item.useTurn = true;
            Item.knockBack = 5f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 58;
            Item.value = CalamityGlobalItem.Rarity3BuyPrice;
            Item.rare = ItemRarityID.Orange;
            Item.shoot = ModContent.ProjectileType<Cyclones>();
            Item.shootSpeed = 3f;
            
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int proj = Projectile.NewProjectile(source, position, velocity, type, damage / 2, knockback, player.whoAmI);
            if (player.altFunctionUse == 2) {
                Main.projectile[proj].ai[0] = 1;
                Main.projectile[proj].timeLeft = 360;
                Main.projectile[proj].damage = damage / 3;

                for (int i = 0; i <= 360; i += 3) {
                    Vector2 vr = new Vector2(3f, 3f).RotatedBy(MathHelper.ToRadians(i));
                    int num = Dust.NewDust(player.Center, player.width, player.height, DustID.Smoke, vr.X, vr.Y, 200, new Color(232, 251, 250, 200), 1.4f);
                    Main.dust[num].noGravity = true;
                    Main.dust[num].position = player.Center;
                    Main.dust[num].velocity = vr;
                }
            }
            return false;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override bool? UseItem(Player player) {
            if (player.altFunctionUse == 2) {
                Item.noMelee = true;
            }
            else {
                Item.noMelee = false;
            }
            return null;
        }

        public override void UseAnimation(Player player) {
            Item.noUseGraphic = false;
            Item.UseSound = SoundID.Item1;
            if (player.altFunctionUse == 2) {
                Item.noUseGraphic = true;
                Item.UseSound = SoundID.Item60;
            }
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, DustID.BlueTorch);
            }
        }
    }
}
