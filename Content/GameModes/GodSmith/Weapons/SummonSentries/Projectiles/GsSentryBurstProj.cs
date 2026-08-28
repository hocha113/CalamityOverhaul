using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Projectiles
{
    /// <summary>
    /// 哨兵族通用一次性爆发判定：owner 生成的真弹幕承载额外伤害与跨端视觉。<br/>
    /// ai[0]=样式（0 交叉火力钉刺 / 1 高爆芯 / 2 火焰溅射环带）ai[1]=判定半径。<br/>
    /// 前 5 帧判定窗（每敌一次），其后纯视觉衰减；环带样式只打外带，不与原爆炸区重复结算
    /// </summary>
    internal class GsSentryBurstProj : ModProjectile
    {
        internal const int StyleCrossSpike = 0;
        internal const int StyleHighExplosive = 1;
        internal const int StyleFlameSplash = 2;

        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonSentries";

        private ref float Style => ref Projectile.ai[0];
        private ref float Radius => ref Projectile.ai[1];
        private ref float Age => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 20;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => Age <= 5f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float dist = DistRectPoint(targetHitbox, Projectile.Center);
            if ((int)Style == StyleFlameSplash) {
                //环带：内圈让位原版爆炸判定
                return dist <= Radius && dist >= Radius * 0.55f;
            }
            return dist <= Radius;
        }

        internal static float DistRectPoint(Rectangle rect, Vector2 point) {
            float dx = MathHelper.Clamp(point.X, rect.Left, rect.Right) - point.X;
            float dy = MathHelper.Clamp(point.Y, rect.Top, rect.Bottom) - point.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        public override void AI() {
            Age++;
            if (Age != 1f || VaultUtils.isServer) {
                return;
            }
            //出生帧：按样式一次性演出（各端都跑，粒子本地）
            switch ((int)Style) {
                case StyleCrossSpike:
                    SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.45f, Pitch = 0.5f, MaxInstances = 3 }, Projectile.Center);
                    for (int i = 0; i < 6; i++) {
                        PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                            Main.rand.NextBool() ? new Color(255, 236, 190) : new Color(226, 178, 96),
                            Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(12, 20));
                    }
                    break;
                case StyleHighExplosive:
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
                    PRTLoader.NewParticle<PRT_MechExplosion>(Projectile.Center, Vector2.Zero,
                        Color.White, 1.1f)?.Configure(24, new Color(255, 140, 46));
                    break;
                case StyleFlameSplash:
                    SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.4f, Pitch = 0.1f, MaxInstances = 3 }, Projectile.Center);
                    for (int i = 0; i < 8; i++) {
                        float ang = MathHelper.TwoPi * i / 8f + Projectile.identity * 0.37f;
                        PRTLoader.NewParticle<PRT_HellFire>(
                            Projectile.Center + ang.ToRotationVector2() * Radius * 0.8f,
                            ang.ToRotationVector2() * 1.4f + new Vector2(0f, -0.5f),
                            Color.White, Main.rand.NextFloat(0.5f, 0.8f));
                    }
                    break;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float fade = 1f - MathHelper.Clamp(Age / 18f, 0f, 1f);
            if (fade <= 0.01f) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            switch ((int)Style) {
                case StyleCrossSpike: {
                    Texture2D cross = CWRAsset.RayCross01?.Value;
                    if (cross == null) {
                        return false;
                    }
                    //十字钉光：出生急胀后收，identity 定摆角
                    float t = MathHelper.Clamp(Age / 6f, 0f, 1f);
                    float scale = (0.28f + 0.22f * t) * (Radius / 80f + 0.6f);
                    Color c = new Color(255, 232, 160) * (0.9f * fade);
                    c.A = 0;
                    float rot = Projectile.identity * 0.51f;
                    Main.EntitySpriteDraw(cross, pos, null, c, rot, cross.Size() * 0.5f, scale, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(cross, pos, null, c * 0.6f, rot + MathHelper.PiOver4, cross.Size() * 0.5f, scale * 0.7f, SpriteEffects.None, 0);
                    break;
                }
                case StyleHighExplosive: {
                    //爆芯冲击环 + 白闪
                    float t = MathHelper.Clamp(Age / 14f, 0f, 1f);
                    ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center,
                        Radius * (0.3f + 0.7f * t), 9f,
                        new Color(255, 236, 190), new Color(255, 140, 46), new Color(120, 46, 16),
                        (1f - t) * 0.8f, timeSeed: Projectile.identity * 0.7f);
                    break;
                }
                case StyleFlameSplash: {
                    Texture2D glow = CWRAsset.SoftGlow?.Value;
                    if (glow == null) {
                        return false;
                    }
                    //环带热浪：贴地压扁光环
                    Color c = new Color(255, 120, 40) * (0.35f * fade);
                    c.A = 0;
                    Main.EntitySpriteDraw(glow, pos, null, c, 0f, glow.Size() * 0.5f,
                        new Vector2(Radius / 26f, Radius / 42f), SpriteEffects.None, 0);
                    break;
                }
            }
            return false;
        }
    }
}
