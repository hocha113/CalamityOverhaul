using CalamityMod.Dusts;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Pandemoniums
{
    ///<summary>
    ///硫磺血雨 - 从天而降的硫磺火球
    ///</summary>
    internal class PandemoniumRainDrop : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        
        private ref float Timer => ref Projectile.localAI[0];
        private bool hasHit = false;
        
        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.alpha = 255;
        }

        public override void AI() {
            Timer++;
            
            //淡入
            if (Projectile.alpha > 0) {
                Projectile.alpha -= 10;
                if (Projectile.alpha < 0) Projectile.alpha = 0;
            }

            //重力加速
            Projectile.velocity.Y += 0.3f;
            if (Projectile.velocity.Y > 20f) {
                Projectile.velocity.Y = 20f;
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //硫磺火拖尾
            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + Projectile.velocity * -0.3f, 
                    (int)CalamityDusts.Brimstone, 
                    Projectile.velocity * -0.2f, 
                    100, 
                    default, 
                    Main.rand.NextFloat(1.2f, 2.0f)
                );
                d.noGravity = true;
                d.fadeIn = 1.2f;
            }

            //火焰粒子
            if (Main.rand.NextBool(3)) {
                Dust fire = Dust.NewDustPerfect(
                    Projectile.Center, 
                    DustID.Torch, 
                    Main.rand.NextVector2Circular(2f, 2f), 
                    100, 
                    Color.OrangeRed, 
                    1.0f
                );
                fire.noGravity = true;
            }

            //硫磺火光芒
            float lightIntensity = (255 - Projectile.alpha) / 255f;
            Lighting.AddLight(Projectile.Center, 1.5f * lightIntensity, 0.6f * lightIntensity, 0.3f * lightIntensity);
        }

        public override void OnKill(int timeLeft) {
            if (hasHit) return;
            CreateImpact();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            CreateImpact();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            hasHit = true;
            target.AddBuff(BuffID.OnFire3, 180);
            CreateImpact();
        }

        private void CreateImpact() {
            if (hasHit) return;
            hasHit = true;

            //冲击音效
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.5f, Pitch = 0.2f }, Projectile.Center);

            //硫磺火爆发
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center, 
                    (int)CalamityDusts.Brimstone, 
                    vel, 
                    100, 
                    default, 
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                d.noGravity = true;
            }

            //火焰环
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel, 100, Color.OrangeRed, 1.5f);
                d.noGravity = true;
            }

            //地面燃烧效果 - 生成小型火焰柱
            if (Projectile.owner == Main.myPlayer && Main.rand.NextBool(3)) {
                Vector2 spawnPos = Projectile.Center;
                int flameProj = Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    spawnPos,
                    Vector2.Zero,
                    ModContent.ProjectileType<PandemoniumGroundFlame>(),
                    (int)(Projectile.damage * 0.5f),
                    Projectile.knockBack * 0.3f,
                    Projectile.owner
                );
            }

            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.alpha >= 255) return false;

            Texture2D glow = CWRAsset.SoftGlow.Value;
            float time = Main.GlobalTimeWrappedHourly;
            float pulse = (float)Math.Sin(time * 20f + Projectile.whoAmI) * 0.3f + 0.7f;

            Color c1 = new Color(255, 200, 100, 0);
            Color c2 = new Color(255, 120, 50, 0);
            Color c3 = new Color(200, 60, 30, 0);

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float alpha = (255 - Projectile.alpha) / 255f;

            //拉长的雨滴形状
            Vector2 scale = new Vector2(0.6f, 1.2f + Projectile.velocity.Y * 0.02f);

            Main.spriteBatch.Draw(glow, drawPos, null, c3 * 0.6f * alpha, Projectile.rotation, glow.Size() / 2, scale * Projectile.scale * 1.5f * pulse, 0, 0);
            Main.spriteBatch.Draw(glow, drawPos, null, c2 * 0.8f * alpha, Projectile.rotation, glow.Size() / 2, scale * Projectile.scale * 1.2f * pulse, 0, 0);
            Main.spriteBatch.Draw(glow, drawPos, null, c1 * alpha, Projectile.rotation, glow.Size() / 2, scale * Projectile.scale * pulse, 0, 0);

            return false;
        }
    }

    ///<summary>
    ///地面火焰 - 雨滴落地后产生的持续燃烧效果
    ///</summary>
    internal class PandemoniumGroundFlame : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        
        private ref float Timer => ref Projectile.ai[0];
        private const int MaxLifetime = 120;
        
        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLifetime;
            Projectile.alpha = 0;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            Timer++;
            
            //火焰粒子
            if (Main.rand.NextBool(2)) {
                Vector2 spawnPos = Projectile.position + new Vector2(Main.rand.Next(Projectile.width), Main.rand.Next(Projectile.height));
                Vector2 vel = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-3f, -1f));
                
                Dust d = Dust.NewDustPerfect(spawnPos, DustID.Torch, vel, 100, Color.OrangeRed, Main.rand.NextFloat(1.0f, 1.8f));
                d.noGravity = true;
            }

            //硫磺火粒子
            if (Main.rand.NextBool(3)) {
                Vector2 spawnPos = Projectile.position + new Vector2(Main.rand.Next(Projectile.width), Main.rand.Next(Projectile.height));
                Dust d = Dust.NewDustPerfect(spawnPos, (int)CalamityDusts.Brimstone, Vector2.UnitY * -2f, 100, default, 1.2f);
                d.noGravity = true;
            }

            //逐渐消散
            float fadeProgress = Timer / MaxLifetime;
            Projectile.alpha = (int)(fadeProgress * 255);

            Lighting.AddLight(Projectile.Center, 1.0f * (1 - fadeProgress), 0.5f * (1 - fadeProgress), 0.2f * (1 - fadeProgress));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 120);
        }

        public override bool PreDraw(ref Color lightColor) {
            return false; //完全由粒子表现
        }
    }
}
