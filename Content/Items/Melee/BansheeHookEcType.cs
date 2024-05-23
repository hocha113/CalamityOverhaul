using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Armor.Bloodflare;
using CalamityMod.Rarities;
using CalamityMod.Sounds;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 女妖之爪
    /// </summary>
    internal class BansheeHookEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "BansheeHook";

        public override void SetStaticDefaults() {
            ItemID.Sets.Spears[Type] = true;
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 120;
            Item.damage = 220;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 21;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 21;
            Item.knockBack = 8.5f;
            Item.UseSound = SoundID.DD2_GhastlyGlaivePierce;
            Item.autoReuse = true;
            Item.height = 108;
            Item.shoot = ModContent.ProjectileType<RBansheeHookProj>();
            Item.shootSpeed = 42f;
            Item.value = CalamityGlobalItem.RarityPureGreenBuyPrice;
            Item.rare = ModContent.RarityType<PureGreen>();

        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI) {
            base.Item.DrawItemGlowmaskSingleFrame(spriteBatch, rotation, ModContent.Request<Texture2D>(CWRConstant.Cay_Wap_Melee + "BansheeHookGlow", (AssetRequestMode)2).Value);
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[base.Item.shoot] <= 0;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            float num82 = Main.mouseX + Main.screenPosition.X - position.X;
            float num83 = Main.mouseY + Main.screenPosition.Y - position.Y;
            if (player.gravDir == -1f) {
                num83 = Main.screenPosition.Y + Main.screenHeight - Main.mouseY - position.Y;
            }
            float num84 = (float)Math.Sqrt(num82 * num82 + num83 * num83);
            if ((float.IsNaN(num82) && float.IsNaN(num83)) || (num82 == 0f && num83 == 0f)) {
                num82 = player.direction;
                num83 = 0f;
                num84 = base.Item.shootSpeed;
            }
            else {
                num84 = base.Item.shootSpeed / num84;
            }
            num82 *= num84;
            num83 *= num84;
            float ai4 = Main.rand.NextFloat() * base.Item.shootSpeed * 0.75f * player.direction;
            velocity = new Vector2(num82, num83);
            int proj = Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, ai4);
            if (player.altFunctionUse == 2) {
                SoundEngine.PlaySound(in CommonCalamitySounds.MeatySlashSound, player.Center);
                SoundEngine.PlaySound(in BloodflareHeadRanged.ActivationSound, player.Center);
                Item.CWR().MeleeCharge = 0;
                Main.projectile[proj].ai[1] = 1;
            }
            return false;
        }
    }
}
