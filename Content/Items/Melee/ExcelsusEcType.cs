using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 宙宇波能刃
    /// </summary>
    internal class ExcelsusEcType : EctypeItem
    {
        public new string LocalizationCategory => "Items.Weapons.Melee";

        public override string Texture => CWRConstant.Cay_Wap_Melee + "Excelsus";

        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 78;
            Item.damage = 220;
            Item.DamageType = DamageClass.Melee;
            Item.useTime = Item.useAnimation = 14;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.knockBack = 8f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 94;
            Item.value = CalamityGlobalItem.RarityDarkBlueBuyPrice;
            Item.rare = ModContent.RarityType<DarkBlue>();
            Item.shoot = ModContent.ProjectileType<ExcelsusMain>();
            Item.shootSpeed = 12f;
            
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI) {
            Item.DrawItemGlowmaskSingleFrame(spriteBatch, rotation
                , ModContent.Request<Texture2D>("CalamityMod/Items/Weapons/Melee/ExcelsusGlow").Value);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Item.useTime = 10;
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<ExcelsusBomb>(), damage * 2, knockback, player.whoAmI);
            }
            else {
                Item.useTime = 14;
                for (int i = 0; i < 3; i++) {
                    float speedX = velocity.X + Main.rand.NextFloat(-1.5f, 1.5f);
                    float speedY = velocity.Y + Main.rand.NextFloat(-1.5f, 1.5f);
                    switch (i) {
                        case 0:
                            type = ModContent.ProjectileType<ExcelsusMain>();
                            break;
                        case 1:
                            type = ModContent.ProjectileType<ExcelsusBlue>();
                            break;
                        case 2:
                            type = ModContent.ProjectileType<ExcelsusPink>();
                            break;
                    }

                    Projectile.NewProjectile(source, position.X, position.Y, speedX, speedY, type, damage, knockback, player.whoAmI);
                }
            }
            return false;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.NewProjectile(player.GetSource_ItemUse(Item), target.Center, Vector2.Zero, ModContent.ProjectileType<LaserFountains>(), Item.damage, 0f, player.whoAmI, target.whoAmI);
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            Projectile.NewProjectile(player.GetSource_ItemUse(Item), target.Center, Vector2.Zero, ModContent.ProjectileType<LaserFountains>(), Item.damage, 0f, player.whoAmI, target.whoAmI);
        }
    }
}
