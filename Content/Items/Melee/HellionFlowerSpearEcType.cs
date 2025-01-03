﻿using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class HellionFlowerSpearEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "HellionFlowerSpear";
        public static List<GuardOfLife> guardOfLives = [];
        private static int index;
        public override void SetDefaults() {
            Item.SetItemCopySD<HellionFlowerSpear>();
            Item.SetKnifeHeld<HellionFlowerSpearHeld>();
        }
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, index);
            if (++index > 2) {
                index = 0;
            }
            return false;
        }
    }

    internal class RHellionFlowerSpear : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<HellionFlowerSpear>();
        public override int ProtogenesisID => ModContent.ItemType<HellionFlowerSpearEcType>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<HellionFlowerSpearHeld>();
        private static int index;
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, index);
            if (++index > 2) {
                index = 0;
            }
            return false;
        }
    }

    internal class GuardOfLife : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "HellionSpike";
        internal GuardOfLifeCore guardOfLifeCore;
        internal Vector2 targetPos;
        internal Vector2 ver1;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 600;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

            if (Projectile.ai[2] == 0) {
                Projectile.velocity *= 0.9f;
                if (Projectile.ai[1] == 2) {
                    Projectile.velocity *= 0.99f;
                }
            }

            if (guardOfLifeCore != null && guardOfLifeCore.Projectile.active && Projectile.ai[2] == 0) {
                ver1 = Projectile.velocity.UnitVector() * -9;
                Projectile.timeLeft = 120;
                Projectile.ai[2] = 1;
                Projectile.ai[0] = 0;
            }

            if (Projectile.ai[2] == 1) {
                if (Projectile.ai[0] > 10) {
                    Projectile.velocity = Projectile.velocity.UnitVector() * 16;
                    Projectile.ai[2] = 2;
                    Projectile.ai[0] = 0;
                    Projectile.timeLeft = 120;
                    Projectile.extraUpdates = 1;
                }
                else {
                    Projectile.position += ver1;
                }
            }

            if (Projectile.ai[2] == 2) {
                if (Projectile.timeLeft > 80) {
                    Projectile.SmoothHomingBehavior(targetPos, 1, 0.1f);
                }
                else {
                    Projectile.velocity *= 0.99f;
                }
            }
            else {
                Projectile.position += Main.player[Projectile.owner].CWR().PlayerPositionChange;
                targetPos += Main.player[Projectile.owner].CWR().PlayerPositionChange;
            }

            Projectile.ai[0]++;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle rectangle = CWRUtils.GetRec(texture);
            Vector2 drawOrigin = rectangle.Size() / 2;

            if (Projectile.ai[2] > 0 && !(Projectile.ai[2] == 1 && Projectile.ai[0] <= 30)) {
                for (int k = 0; k < Projectile.oldPos.Length; k++) {
                    Vector2 drawPos = (Projectile.oldPos[k] - Main.screenPosition) + Projectile.Size / 2;
                    Color color = Color.White * (float)(((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length) / 2);
                    Main.EntitySpriteDraw(texture, drawPos, rectangle, color, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
                }
            }
            VaultUtils.DrawRotatingMarginEffect(Main.spriteBatch, texture, (int)Projectile.ai[0], Projectile.Center - Main.screenPosition
                , null, Color.Green, Projectile.rotation, drawOrigin, Projectile.scale, 0);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, rectangle
                , Color.White, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    internal class GuardOfLifeCore : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "HellionSpike";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 600;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle rectangle = CWRUtils.GetRec(texture);
            Vector2 drawOrigin = rectangle.Size() / 2;

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = (Projectile.oldPos[k] - Main.screenPosition) + Projectile.Size / 2;
                Color color = Color.White * (float)(((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length) / 2);
                Main.EntitySpriteDraw(texture, drawPos, rectangle, color, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, rectangle, Color.White, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    internal class HellionFlowerSpearHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<HellionFlowerSpear>();
        public override Texture2D TextureValue => CWRUtils.GetT2DValue(CWRConstant.Cay_Proj_Melee + "Spears/HellionFlowerSpearProjectile");
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "Greentide_Bar";
        public List<GuardOfLife> guardOfLives => HellionFlowerSpearEcType.guardOfLives;
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 46;
            canDrawSlashTrail = true;
            drawTrailHighlight = false;
            distanceToOwner = 20;
            drawTrailBtommWidth = 50;
            drawTrailTopWidth = 40;
            drawTrailCount = 6;
            Length = 52;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            SwingDrawRotingOffset = MathHelper.PiOver2;
            ShootSpeed = 32f;
        }

        public override bool PreSwingAI() {
            if (Projectile.ai[0] == 0) {
                StabBehavior(scaleFactorDenominator: 520f, maxLength: 80);
                if (Time < 6 * updateCount) {
                    Vector2 spanSparkPos = Projectile.Center + Projectile.velocity.UnitVector() * Length / 2;
                    BasePRT spark = new PRT_Spark(spanSparkPos, Projectile.velocity, false, 4, 1.26f, Color.GreenYellow, Owner);
                    PRTLoader.AddParticle(spark);
                }

                return false;
            }

            return true;
        }

        public override void Shoot() {
            if (Projectile.ai[0] != 0) {
                for (int i = 0; i < 5; i++) {
                    GuardOfLife guardOfLife = Projectile.NewProjectileDirect(Source
                        , ShootSpanPos, ShootVelocity.RotatedBy((-2 + i) * 0.4f)
                        , ModContent.ProjectileType<GuardOfLife>(), Projectile.damage
                        , Projectile.knockBack, Owner.whoAmI, ai0: 0, ai1: Projectile.ai[0], ai2: 0)
                        .ModProjectile as GuardOfLife;
                    guardOfLife.targetPos = ShootSpanPos + ShootVelocity.UnitVector() * 900;
                    guardOfLives.Add(guardOfLife);
                }
                return;
            }

            GuardOfLifeCore core = Projectile.NewProjectileDirect(Source, ShootSpanPos, ShootVelocity
                , ModContent.ProjectileType<GuardOfLifeCore>(), Projectile.damage
                , Projectile.knockBack, Owner.whoAmI, 0f, 0).ModProjectile as GuardOfLifeCore;

            foreach (var proj in guardOfLives) {
                proj.guardOfLifeCore = core;
            }

            HellionFlowerSpearEcType.guardOfLives.Clear();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (hit.Crit) {
                Projectile petal = CalamityUtils.ProjectileBarrage(Source, Projectile.Center, target.Center
                    , Main.rand.NextBool(), 800f, 800f, 0f, 800f, 10f, ProjectileID.FlowerPetal
                    , (int)(Projectile.damage * 0.5), Projectile.knockBack * 0.5f, Projectile.owner, true);
                if (petal.whoAmI.WithinBounds(Main.maxProjectiles)) {
                    petal.DamageType = DamageClass.Melee;
                    petal.localNPCHitCooldown = -1;
                }
            }
        }
    }
}
