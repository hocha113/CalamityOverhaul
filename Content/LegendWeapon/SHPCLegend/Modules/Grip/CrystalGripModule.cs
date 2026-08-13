using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    /// <summary>水晶握把，暴击折射棱片</summary>
    internal sealed class CrystalGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //水晶幻紫
        public override Color TintColor => new(200, 130, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.ManaCostMul += -0.10f;
            ctx.CritAdd += 6;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.Projectile.owner != Main.myPlayer || !hit.Crit) return;
            SpawnShards(beam.Projectile, target, Math.Max((int)(damageDone * 0.35f), 1), 3);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            if (laser.Projectile.owner != Main.myPlayer || !hit.Crit) return;
            if (!Main.rand.NextBool(2)) return; //激光命中频繁，暴击只折半触发
            SpawnShards(laser.Projectile, target, Math.Max((int)(damageDone * 0.30f), 1), 1);
        }

        private static void SpawnShards(Projectile source, NPC target, int damage, int count) {
            for (int i = 0; i < count; i++) {
                //扇形上抛
                Vector2 vel = new(Main.rand.NextFloat(-4.5f, 4.5f), Main.rand.NextFloat(-9.5f, -5.5f));
                Projectile.NewProjectile(source.GetSource_FromThis(),
                    target.Center + Main.rand.NextVector2Circular(target.width * 0.3f, target.height * 0.3f),
                    vel,
                    ModContent.ProjectileType<SHPCCrystalShardProj>(),
                    damage, 1.5f, source.owner);
            }
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = 0.4f }, target.Center);
            }
        }
    }

    /// <summary>水晶棱片，重力+220px引力，穿透2</summary>
    internal sealed class SHPCCrystalShardProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private static readonly Color GemCore = new(255, 170, 235);
        private static readonly Color GemGlow = new(210, 110, 255);
        private static readonly Color GemAura = new(95, 35, 150);

        private float fadeAlpha;
        private float spin;

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 80;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            //初始朝向去同相，同帧多片不叠影
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                spin = Projectile.whoAmI * 0.73f;
            }

            //重力+末速
            Projectile.velocity.Y += 0.24f;
            if (Projectile.velocity.Y > 12f) Projectile.velocity.Y = 12f;

            //下坠引力捕获
            if (Projectile.velocity.Y > 0f) {
                NPC target = Projectile.Center.FindClosestNPC(220f, false, true);
                if (target != null) {
                    Vector2 pull = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity += pull * 0.45f;
                    if (Projectile.velocity.Length() > 13f) {
                        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 13f;
                    }
                }
            }

            spin += 0.28f;
            Projectile.rotation = spin;
            fadeAlpha = MathHelper.Clamp(Projectile.timeLeft / 14f, 0f, 1f);
            Lighting.AddLight(Projectile.Center, GemGlow.ToVector3() * 0.3f * fadeAlpha);

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center, -Projectile.velocity * 0.1f,
                    GemCore, Main.rand.NextFloat(0.25f, 0.5f)).Configure(GemAura, Main.rand.Next(8, 16), Main.rand.NextFloat(-0.2f, 0.2f), 0.6f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.4f, Pitch = 0.7f }, target.Center);
            BurstDust(target.Center, 6);
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;
            BurstDust(Projectile.Center, 5);
        }

        private static void BurstDust(Vector2 center, int count) {
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3.2f, 3.2f);
                PRTLoader.NewParticle<PRT_Sparkle>(center, vel, GemCore, Main.rand.NextFloat(0.35f, 0.7f)).Configure(GemGlow, Main.rand.Next(10, 20), Main.rand.NextFloat(-0.3f, 0.3f), 0.8f);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (fadeAlpha < 0.01f) return;
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture_White?.Value;
            if (star == null) return;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 starOrigin = star.Size() * 0.5f;
            float t = (float)Main.timeForVisualEffects;

            //底晕
            if (glow != null) {
                spriteBatch.Draw(glow, drawPos, null, GemAura * (fadeAlpha * 0.5f), 0f,
                    glow.Size() * 0.5f, 0.45f, SpriteEffects.None, 0f);
            }

            //高速速度模糊条，锚在尾侧
            float speedVal = Projectile.velocity.Length();
            if (pixel != null && speedVal > 6.5f) {
                float speedT = MathHelper.Clamp((speedVal - 6.5f) / 7f, 0f, 1f);
                spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), GemGlow * (fadeAlpha * speedT * 0.5f),
                    Projectile.velocity.ToRotation(), new Vector2(1f, 0.5f),
                    new Vector2(speedVal * 1.7f, 2.2f), SpriteEffects.None, 0f);
            }

            //旋转拖影，逆自旋向两道
            spriteBatch.Draw(star, drawPos, null, GemGlow * (fadeAlpha * 0.16f),
                spin - 0.62f, starOrigin, new Vector2(0.105f, 0.038f), SpriteEffects.None, 0f);
            spriteBatch.Draw(star, drawPos, null, GemGlow * (fadeAlpha * 0.34f),
                spin - 0.31f, starOrigin, new Vector2(0.108f, 0.04f), SpriteEffects.None, 0f);

            //马眼形分面剪影双层
            spriteBatch.Draw(star, drawPos, null, GemGlow * (fadeAlpha * 0.95f),
                spin, starOrigin, new Vector2(0.112f, 0.042f), SpriteEffects.None, 0f);
            spriteBatch.Draw(star, drawPos, null, GemCore * fadeAlpha,
                spin, starOrigin, new Vector2(0.066f, 0.027f), SpriteEffects.None, 0f);

            //周期折射爆闪，尖峰时垂直交叉闪+点状高光
            float glint = MathF.Pow(MathF.Abs(MathF.Sin(t * 0.23f + Projectile.whoAmI * 1.37f)), 6f);
            if (glint > 0.12f) {
                Color flash = Color.Lerp(GemCore, Color.White, 0.55f) * (fadeAlpha * glint);
                spriteBatch.Draw(star, drawPos, null, flash,
                    spin + MathHelper.PiOver2, starOrigin, new Vector2(0.075f, 0.018f), SpriteEffects.None, 0f);
                spriteBatch.Draw(star, drawPos, null, flash * 0.8f,
                    -spin * 0.5f, starOrigin, 0.045f, SpriteEffects.None, 0f);
            }
        }
    }
}
