﻿using CalamityMod.CalPlayer;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.RemakeItems.Core;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RBalefulHarvester : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<BalefulHarvester>();
        public static int maxCharge = 160;
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public override bool? CanUseItem(Item item, Player player) => player.ownedProjectileCounts[ModContent.ProjectileType<BalefulHarvesterHeldThrow>()] == 0;
        public override bool? AltFunctionUse(Item item, Player player) => AltFunctionUseFunc(item);
        public override void HoldItem(Item item, Player player) => HoldItemFunc(item);
        public override void PostDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position
            , Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
            => PostDrawInInventoryFunc(item, spriteBatch, position, frame, scale);
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => ShootFunc(item, player, source, position, velocity, type, damage, knockback);

        public static void SetDefaultsFunc(Item Item) {
            Item.damage = 90;
            Item.width = 74;
            Item.height = 86;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 22;
            Item.useTurn = true;
            Item.knockBack = 8f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.value = CalamityGlobalItem.RarityPurpleBuyPrice;
            Item.rare = ItemRarityID.Red;
            Item.shootSpeed = 15;
            Item.SetKnifeHeld<BalefulHarvesterHeld>();
        }

        public static bool AltFunctionUseFunc(Item Item) {
            Item.Initialize();
            return Item.CWR().ai[0] <= 0;
        }

        public static bool ShootFunc(Item Item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Item.CWR().ai[0] += maxCharge;
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<BalefulHarvesterHeldThrow>(), damage, knockback, player.whoAmI);
                return false;
            }
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }

        public static void HoldItemFunc(Item Item) {
            Item.Initialize();
            if (Item.CWR().ai[0] > 0) {
                Item.CWR().ai[0]--;
            }
        }

        public static void PostDrawInInventoryFunc(Item Item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, float scale) {
            Item.Initialize();
            if (!(Item.CWR().ai[0] <= 0f)) {//这是一个通用的进度条绘制，用于判断充能进度
                Texture2D barBG = CWRAsset.GenericBarBack.Value;
                Texture2D barFG = CWRAsset.GenericBarFront.Value;
                float barScale = 3f;
                Vector2 barOrigin = barBG.Size() * 0.5f;
                float yOffset = 50f;
                Vector2 drawPos = position + Vector2.UnitY * scale * (frame.Height - yOffset);
                Rectangle frameCrop = new Rectangle(0, 0, (int)(Item.CWR().ai[0] / maxCharge * barFG.Width), barFG.Height);
                Color color = Main.hslToRgb(Main.GlobalTimeWrappedHourly * 0.6f % 1f, 1f, 0.75f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.1f);
                spriteBatch.Draw(barBG, drawPos, null, color, 0f, barOrigin, scale * barScale, 0, 0f);
                spriteBatch.Draw(barFG, drawPos, frameCrop, color * 0.8f, 0f, barOrigin, scale * barScale, 0, 0f);
            }
        }

        public static void SpanDust(Vector2 origPos, float maxSengNum, float minScale, float maxScale) {
            float randomRot = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int i = 0; i < 4; i++) {
                float rot = MathHelper.PiOver2 * i + randomRot;
                Vector2 vr = rot.ToRotationVector2();
                for (int j = 0; j < maxSengNum; j++) {
                    PRT_HeavenfallStar spark = new PRT_HeavenfallStar(origPos, vr * (0.1f + i * 0.1f)
                        , false, 37, Main.rand.NextFloat(minScale, maxScale), Color.DarkGoldenrod);
                    PRTLoader.AddParticle(spark);
                }
            }
        }
    }

    internal class BalefulHarvesterHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<BalefulHarvester>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "Aftershock_Bar";
        public override void SetKnifeProperty() {
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            drawTrailCount = 80;
            drawTrailTopWidth = 36;
            distanceToOwner = 10;
            drawTrailBtommWidth = 20;
            SwingData.baseSwingSpeed = 4f;
            Projectile.width = Projectile.height = 46;
            unitOffsetDrawZkMode = 2;
            Length = 66;
            shootSengs = 0.8f;
            autoSetShoot = true;
        }

        public override void SwingModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.CritDamage *= 0.5f;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            int type = Main.rand.NextBool() ? ModContent.ProjectileType<BalefulHarvesterProjectile>() : ProjectileID.FlamingJack;
            CalamityPlayer.HorsemansBladeOnHit(Owner, -1, (int)(Item.damage * 1.5f), Item.knockBack, 0, type);
            target.AddBuff(BuffID.OnFire3, 300);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            int type = Main.rand.NextBool() ? ModContent.ProjectileType<BalefulHarvesterProjectile>() : ProjectileID.FlamingJack;
            CalamityPlayer.HorsemansBladeOnHit(Owner, target.whoAmI, (int)(Item.damage * 1.5f), Item.knockBack, 0, type);
            target.AddBuff(BuffID.OnFire3, 300);
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(initialMeleeSize: 1, phase0SwingSpeed: 0.3f
                , phase1SwingSpeed: 6.2f, phase2SwingSpeed: 3f, swingSound: SoundID.Item71);
            return base.PreInOwner();
        }

        public override void MeleeEffect() {
            if (Main.rand.NextBool(3)) {
                Vector2 spanPos = Projectile.Hitbox.TopLeft();
                spanPos.X += Main.rand.Next(Projectile.Hitbox.Width);
                spanPos.Y += Main.rand.Next(Projectile.Hitbox.Height);
                RBalefulHarvester.SpanDust(spanPos, 6, 0.2f, 0.4f);
            }
        }

        public override void Shoot() {
            for (int i = 0; i < 2; i++) {
                Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.2f, 0.2f))
                    , ModContent.ProjectileType<BalefulSickle>(), Projectile.damage, Projectile.knockBack, Owner.whoAmI);
            }
        }
    }
}
