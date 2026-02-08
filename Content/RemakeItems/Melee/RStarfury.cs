using CalamityOverhaul.Content.MeleeModify.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RStarfury : CWRItemOverride
    {
        public override int TargetID => ItemID.Starfury;
        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.useTime = 12;
            item.damage = 15;
            item.SetKnifeHeld<StarfuryHeld>();
        }
    }

    internal class StarfuryHeld : BaseKnife
    {
        public override int TargetID => ItemID.Starfury;
        public override string gradientTexturePath => CWRConstant.ColorBar + "StellarStriker_Bar";
        public override void SetKnifeProperty() {
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            drawTrailCount = 10;
            drawTrailTopWidth = 30;
            distanceToOwner = -22;
            drawTrailBtommWidth = 10;
            SwingData.baseSwingSpeed = 3f;
            Projectile.width = Projectile.height = 46;
            Length = 42;
            SwingData.starArg = 50;
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(initialMeleeSize: 1, phase0SwingSpeed: 0.3f
                , phase1SwingSpeed: 8.2f, phase2SwingSpeed: 6f
                , phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0);
            return base.PreInOwner();
        }

        public override void MeleeEffect() {
            if (Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height
                    , DustID.Enchanted_Gold, 0f, 0f, 150, default, 0.8f);
                dust.velocity *= 0.3f;
                dust.noGravity = true;
            }
        }

        public override void Shoot() {
            //从天空中降下星辰弹幕，落向挥击前方的区域
            int starCount = Main.rand.Next(1, 3);
            for (int i = 0; i < starCount; i++) {
                //生成位置在目标点上方约600到800像素处，带有水平随机偏移
                float offsetX = Main.rand.NextFloat(-120f, 120f);
                float offsetY = Main.rand.NextFloat(-800f, -600f);
                Vector2 spawnPos = Main.MouseWorld + new Vector2(offsetX, offsetY);
                //速度朝向挥击位置，带一些随机扰动
                Vector2 toTarget = (Main.MouseWorld - spawnPos).SafeNormalize(Vector2.UnitY);
                Vector2 velocity = toTarget * Main.rand.NextFloat(10f, 14f);
                velocity.X += Main.rand.NextFloat(-1.5f, 1.5f);

                Projectile.NewProjectile(Source, spawnPos, velocity
                    , ModContent.ProjectileType<StarfuryFallingStar>()
                    , (int)(Projectile.damage * 0.7f), Projectile.knockBack, Owner.whoAmI);
            }
        }
    }

    /// <summary>
    /// 星怒挥击时从天空坠落的星辰弹幕
    /// </summary>
    internal class StarfuryFallingStar : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.alpha = 50;
            Projectile.light = 0.6f;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            //星辰旋转
            Projectile.rotation += Projectile.velocity.X * 0.03f;

            //轻微的重力加速，让坠落感更强
            if (Projectile.velocity.Y < 18f) {
                Projectile.velocity.Y += 0.08f;
            }

            //发光和光照
            float pulse = (float)Math.Sin(Projectile.timeLeft * 0.15f) * 0.2f + 0.8f;
            Lighting.AddLight(Projectile.Center, 0.8f * pulse, 0.7f * pulse, 0.3f * pulse);

            //金色星尘拖尾粒子
            if (Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f)
                    , DustID.Enchanted_Gold, -Projectile.velocity * 0.2f, 100, default, Main.rand.NextFloat(0.8f, 1.4f));
                dust.noGravity = true;
            }
            if (Main.rand.NextBool(4)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center
                    , DustID.YellowStarDust, -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(1f, 1f)
                    , 150, default, Main.rand.NextFloat(0.5f, 0.9f));
                dust.noGravity = true;
                dust.fadeIn = 1.1f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中时产生星光爆发
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                Dust dust = Dust.NewDustPerfect(target.Center, DustID.Enchanted_Gold, vel, 0, default, 1.5f);
                dust.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10, Projectile.Center);
            //消亡时的星光粒子爆发
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Enchanted_Gold, vel, 50, default, Main.rand.NextFloat(1f, 1.8f));
                dust.noGravity = true;
            }
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3f, 3f);
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.YellowStarDust, vel, 100, default, Main.rand.NextFloat(0.6f, 1.2f));
                dust.noGravity = true;
                dust.fadeIn = 1.2f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //使用原版星怒弹幕的纹理
            Main.instance.LoadProjectile(ProjectileID.Starfury);
            Texture2D texture = TextureAssets.Projectile[ProjectileID.Starfury].Value;
            Vector2 origin = texture.Size() / 2f;

            //绘制拖尾残影
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                if (Projectile.oldPos[k] == Vector2.Zero) continue;
                Vector2 drawPos = Projectile.oldPos[k] + Projectile.Size / 2f - Main.screenPosition;
                float progress = k / (float)Projectile.oldPos.Length;
                float trailAlpha = (1f - progress) * (1f - Projectile.alpha / 255f);
                Color trailColor = Color.Lerp(Color.White, new Color(255, 200, 80), progress) * trailAlpha * 0.5f;
                float trailScale = Projectile.scale * (1f - progress * 0.3f);
                Main.EntitySpriteDraw(texture, drawPos, null, trailColor
                    , Projectile.oldRot[k], origin, trailScale, SpriteEffects.None, 0);
            }

            //绘制光晕层
            float alphaFactor = 1f - Projectile.alpha / 255f;
            float glowPulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f + Projectile.whoAmI) * 0.15f + 0.85f;
            Color glowColor = new Color(255, 220, 100, 0) * alphaFactor * 0.4f * glowPulse;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null
                , glowColor, Projectile.rotation, origin, Projectile.scale * 1.25f, SpriteEffects.None, 0);

            //绘制主体
            Color mainColor = Color.White * alphaFactor;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null
                , mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }
    }
}
