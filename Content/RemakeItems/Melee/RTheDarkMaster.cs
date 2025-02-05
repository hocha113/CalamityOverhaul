using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Others;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RTheDarkMaster : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<TheDarkMaster>();
        public const int maxDeBuffTime = 600;
        public override void SetDefaults(Item item) {
            item.shoot = ModContent.ProjectileType<TheDarkMasterRapier>();
            item.useTime = 45;
            item.useAnimation = 45;
            item.autoReuse = true;
            item.useStyle = ItemUseStyleID.Shoot;
            item.knockBack = 3.5f;
            item.shootSpeed = 5f;
            item.noUseGraphic = true;
            item.noMelee = true;
            item.channel = true;
        }
        public override bool? On_AltFunctionUse(Item item, Player player) => false;
        public override void PostDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame
            , Color drawColor, Color itemColor, Vector2 origin, float scale) {
            PostDrawInInventoryFunc(spriteBatch, position, frame, drawColor, itemColor, origin, scale);
        }
        public override void HoldItem(Item item, Player player) => HoldItemFunc(player);
        public override bool? On_CanUseItem(Item item, Player player) {
            return player.ownedProjectileCounts[item.shoot] <= 0;
        }
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }

        public static void PostDrawInInventoryFunc(SpriteBatch spriteBatch, Vector2 position, Rectangle frame
            , Color drawColor, Color itemColor, Vector2 origin, float scale) {
            int value = Main.LocalPlayer.CWR().DontHasSemberDarkMasterCloneTime;
            if (!(value <= 0f)) {//这是一个通用的进度条绘制，用于判断充能进度
                Texture2D barBG = CWRAsset.GenericBarBack.Value;
                Texture2D barFG = CWRAsset.GenericBarFront.Value;
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

        public static void HoldItemFunc(Player player) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<Hit>()] > 0) {
                player.AddBuff(BuffID.Darkness, maxDeBuffTime);
                player.AddBuff(BuffID.Slow, maxDeBuffTime);
                player.AddBuff(BuffID.Weak, maxDeBuffTime);
                player.AddBuff(BuffID.Silenced, maxDeBuffTime);
                player.CWR().DontHasSemberDarkMasterCloneTime = maxDeBuffTime;
            }
        }
    }
}
