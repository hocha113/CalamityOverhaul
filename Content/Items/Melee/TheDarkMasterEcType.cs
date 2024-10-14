using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class TheDarkMasterEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "TheDarkMaster";
        public const int maxDeBuffTime = 600;
        public override void SetDefaults() {
            Item.SetItemCopySD<TheDarkMaster>();
            Item.shoot = ModContent.ProjectileType<TheDarkMasterRapier>();
            Item.useTime = 45;
            Item.useAnimation = 45;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3.5f;
            Item.shootSpeed = 5f;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.channel = true;
        }
        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame
            , Color drawColor, Color itemColor, Vector2 origin, float scale) {
            PostDrawInInventoryFunc(spriteBatch, position, frame, drawColor, itemColor, origin, scale);
        }
        public static void PostDrawInInventoryFunc(SpriteBatch spriteBatch, Vector2 position, Rectangle frame
            , Color drawColor, Color itemColor, Vector2 origin, float scale) {
            int value = Main.LocalPlayer.CWR().DontHasSemberDarkMasterCloneTime;
            if (!(value <= 0f)) {//这是一个通用的进度条绘制，用于判断充能进度
                Texture2D barBG = ModContent.Request<Texture2D>("CalamityMod/UI/MiscTextures/GenericBarBack", (AssetRequestMode)2).Value;
                Texture2D barFG = ModContent.Request<Texture2D>("CalamityMod/UI/MiscTextures/GenericBarFront", (AssetRequestMode)2).Value;
                float barScale = 2f;
                Vector2 barOrigin = barBG.Size() * 0.5f;
                float yOffset = 50f;
                Vector2 drawPos = position + Vector2.UnitY * scale * (frame.Height - yOffset);
                Rectangle frameCrop = new Rectangle(0, 0, (int)(value / (float)maxDeBuffTime * barFG.Width), barFG.Height);
                Color color = Main.hslToRgb(Main.GlobalTimeWrappedHourly * 0.6f % 1f, 1f, 0.75f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.1f);
                spriteBatch.Draw(barBG, drawPos, null, color, 0f, barOrigin, scale * barScale, 0, 0f);
                spriteBatch.Draw(barFG, drawPos, frameCrop, color * 0.8f, 0f, barOrigin, scale * barScale, 0, 0f);
            }
        }
        public override void HoldItem(Player player) => HoldItemFunc(player);
        public static void HoldItemFunc(Player player) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<Hit>()] > 0) {
                player.AddBuff(BuffID.Darkness, maxDeBuffTime);
                player.CWR().DontHasSemberDarkMasterCloneTime = maxDeBuffTime;
            }
        }
        public override bool CanUseItem(Player player) {
            if (player.CWR().NoSemberCloneSpanTime > 0) {
                player.CWR().NoSemberCloneSpanTime--;
            }
            return player.ownedProjectileCounts[Item.shoot] <= 0;
        }
    }
}
