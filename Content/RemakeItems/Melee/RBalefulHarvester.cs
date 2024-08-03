using CalamityMod.Items;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RBalefulHarvester : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.BalefulHarvester>();
        public override int ProtogenesisID => ModContent.ItemType<BalefulHarvesterEcType>();
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        }
        public override string TargetToolTipItemName => "BalefulHarvesterEcType";

        public override void SetDefaults(Item item) {
            item.damage = 90;
            item.width = 74;
            item.height = 86;
            item.noMelee = true;
            item.noUseGraphic = true;
            item.DamageType = DamageClass.Melee;
            item.useAnimation = 22;
            item.useStyle = ItemUseStyleID.Swing;
            item.useTime = 22;
            item.useTurn = true;
            item.knockBack = 8f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.value = CalamityGlobalItem.RarityPurpleBuyPrice;
            item.rare = ItemRarityID.Red;
            item.shoot = ModContent.ProjectileType<BalefulHarvesterHeldProj>();
            item.shootSpeed = 15;
        }

        public override bool? CanUseItem(Item item, Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<BalefulHarvesterHeldProj>()] == 0;

        public override bool? AltFunctionUse(Item item, Player player) {
            item.initialize();
            return item.CWR().ai[0] <= 0;
        }

        public override void ModifyShootStats(Item item, Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            item.noMelee = false;
            item.noUseGraphic = false;
            if (player.altFunctionUse == 2) {
                item.noMelee = true;
                item.noUseGraphic = true;
            }
        }

        public override void HoldItem(Item item, Player player) {
            item.initialize();
            if (item.CWR().ai[0] > 0) {
                item.CWR().ai[0]--;
            }
        }

        public override void PostDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            item.initialize();
            if (!(item.CWR().ai[0] <= 0f)) {//这是一个通用的进度条绘制，用于判断充能进度
                Texture2D barBG = ModContent.Request<Texture2D>("CalamityMod/UI/MiscTextures/GenericBarBack", (AssetRequestMode)2).Value;
                Texture2D barFG = ModContent.Request<Texture2D>("CalamityMod/UI/MiscTextures/GenericBarFront", (AssetRequestMode)2).Value;
                float barScale = 3f;
                Vector2 barOrigin = barBG.Size() * 0.5f;
                float yOffset = 50f;
                Vector2 drawPos = position + Vector2.UnitY * scale * (frame.Height - yOffset);
                Rectangle frameCrop = new Rectangle(0, 0, (int)(item.CWR().ai[0] / BalefulHarvesterEcType.maxCharge * barFG.Width), barFG.Height);
                Color color = Main.hslToRgb(Main.GlobalTimeWrappedHourly * 0.6f % 1f, 1f, 0.75f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.1f);
                spriteBatch.Draw(barBG, drawPos, null, color, 0f, barOrigin, scale * barScale, 0, 0f);
                spriteBatch.Draw(barFG, drawPos, frameCrop, color * 0.8f, 0f, barOrigin, scale * barScale, 0, 0f);
            }
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            item.initialize();
            if (player.altFunctionUse == 2) {
                item.CWR().ai[0] += BalefulHarvesterEcType.maxCharge;
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
                return false;
            }
            SoundEngine.PlaySound(SoundID.Item71, player.position);
            for (int i = 0; i < 2; i++) {
                Projectile.NewProjectile(source, position, velocity.RotatedBy(Main.rand.NextFloat(-0.2f, 0.2f)), ModContent.ProjectileType<BalefulSickle>(), damage, knockback, player.whoAmI);
            }
            return false;
        }

        public override void MeleeEffects(Item item, Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(3))
                BalefulHarvesterEcType.SpanDust(hitbox.TopLeft() + new Vector2(Main.rand.Next(hitbox.Width), Main.rand.Next(hitbox.Height)), 6, 0.3f, 0.5f);
        }
    }
}
