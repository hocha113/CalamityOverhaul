using CalamityMod.Items;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.EarthenProj;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 地炎长矛
    /// </summary>
    internal class EarthenPikeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "EarthenPike";
        public new string LocalizationCategory => "Items.Weapons.Melee";
        public static int maxCharge = 60;
        public override void SetStaticDefaults() {
            ItemID.Sets.Spears[Item.type] = true;
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 60;
            Item.damage = 90;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.useTurn = true;
            Item.noUseGraphic = true;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 25;
            Item.knockBack = 7f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 60;
            Item.value = CalamityGlobalItem.RarityPinkBuyPrice;
            Item.rare = ItemRarityID.Pink;
            Item.shoot = ModContent.ProjectileType<REarthenPikeSpear>();
            Item.shootSpeed = 8f;

        }

        public override bool AltFunctionUse(Player player) {
            Item.initialize();
            return Item.CWR().ai[0] <= 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Item.initialize();
            if (player.altFunctionUse == 2) {
                Item.CWR().ai[0] += maxCharge;
                Projectile proj = Projectile.NewProjectileDirect(source, position, velocity, ModContent.ProjectileType<EarthenPikeThrowProj>(), damage * 2, knockback);
                EarthenPikeThrowProj earthenPikeThrowProj = (EarthenPikeThrowProj)proj.ModProjectile;
                if (earthenPikeThrowProj != null) {
                    earthenPikeThrowProj.earthenPike = Item.Clone();
                    Item.TurnToAir();
                }
                else {
                    proj.Kill();
                }
                return false;
            }
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }

        public override void HoldItem(Player player) {
            Item.initialize();
            if (Item.CWR().ai[0] > 0) {
                Item.CWR().ai[0]--;
            }
        }

        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            Item.initialize();
            if (!(Item.CWR().ai[0] <= 0f)) {//这是一个通用的进度条绘制，用于判断充能进度
                Texture2D barBG = ModContent.Request<Texture2D>("CalamityMod/UI/MiscTextures/GenericBarBack", (AssetRequestMode)2).Value;
                Texture2D barFG = ModContent.Request<Texture2D>("CalamityMod/UI/MiscTextures/GenericBarFront", (AssetRequestMode)2).Value;
                float barScale = 2f;
                Vector2 barOrigin = barBG.Size() * 0.5f;
                float yOffset = 50f;
                Vector2 drawPos = position + Vector2.UnitY * scale * (frame.Height - yOffset);
                Rectangle frameCrop = new Rectangle(0, 0, (int)(Item.CWR().ai[0] / maxCharge * barFG.Width), barFG.Height);
                Color color = Main.hslToRgb(Main.GlobalTimeWrappedHourly * 0.6f % 1f, 1f, 0.75f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.1f);
                spriteBatch.Draw(barBG, drawPos, null, color, 0f, barOrigin, scale * barScale, 0, 0f);
                spriteBatch.Draw(barFG, drawPos, frameCrop, color * 0.8f, 0f, barOrigin, scale * barScale, 0, 0f);
            }
        }

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;// && player.ownedProjectileCounts[ModContent.ProjectileType<EarthenPikeThrowProj>()] <= 0;一般来讲是不需要判断投掷弹幕的数量的，因为这从使用情景上来讲没必要
    }
}
