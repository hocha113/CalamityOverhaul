using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.PumpkinMoon.Projectiles
{
    /// <summary>
    /// 祭火落点标记与迸燃：ai[0]=引爆倒计时帧（=炮弹飞行帧）。生成位置即锁定落点（预告即承诺），
    /// 预告期画地面标记环（环径=迸燃判定半径 BlastRadius，环即真相），引爆帧转迸燃，
    /// 判定窗=迸燃可见窗；相邻落点由发射端按 MortarSpacing 布点，走廊=安全带
    /// </summary>
    internal class PmkMortarMarkerProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int BlastFrames = 14;
        internal const int FadeFrames = 14;
        /// <summary>迸燃判定半径（判定与标记环共用，公平阀门）</summary>
        internal const float BlastRadius = 60f;
        /// <summary>迸燃撑满半径的帧数</summary>
        private const int RiseFrames = 6;
        /// <summary>标记环圆周光点数</summary>
        private const int RingDots = 10;

        private static readonly Color MarkWarn = new Color(255, 156, 48, 0);
        private static readonly Color BlastDeep = new Color(126, 42, 12);
        private static readonly Color BlastHot = new Color(255, 196, 90, 0);

        private int Telegraph => Math.Max((int)Projectile.ai[0], 30);
        private int TotalLife => Telegraph + BlastFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;
        private bool Blasting => Elapsed >= Telegraph && Elapsed < Telegraph + BlastFrames;

        /// <summary>迸燃撑开度 0~1</summary>
        private float RiseProgress {
            get {
                int t = Elapsed - Telegraph;
                if (t <= 0) {
                    return 0f;
                }
                if (t >= RiseFrames) {
                    return 1f;
                }
                float x = t / (float)RiseFrames;
                return 1f - (1f - x) * (1f - x);
            }
        }

        /// <summary>收场衰减 1→0</summary>
        private float FadeFactor {
            get {
                int t = Elapsed - Telegraph - BlastFrames;
                if (t <= 0) {
                    return 1f;
                }
                return MathHelper.Clamp(1f - t / (float)FadeFrames, 0f, 1f);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 280;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TotalLife;
            }

            int elapsed = Elapsed;
            //判定窗=迸燃可见窗
            Projectile.hostile = Blasting;

            if (elapsed == Telegraph && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.62f, Pitch = 0.25f, MaxInstances = 4 }, Projectile.Center);
                //爆发帧粉尘 ≤6 粒（性能红线）
                for (int i = 0; i < 4; i++) {
                    Dust burst = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                        (-Vector2.UnitY).RotatedByRandom(1.1f) * Main.rand.NextFloat(2f, 7f), 80, default,
                        Main.rand.NextFloat(1.2f, 1.9f));
                    burst.noGravity = Main.rand.NextBool();
                }
                for (int i = 0; i < 2; i++) {
                    Dust smoke = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke,
                        new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1f, 3f)), 150, default, 1.3f);
                    smoke.noGravity = true;
                }
            }

            if (Main.dedServ) {
                return;
            }

            if (elapsed < Telegraph) {
                //预告期：环内零星火星（≤1 粒/帧）
                if (Main.rand.NextBool(4)) {
                    Dust spark = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-BlastRadius, BlastRadius) * 0.8f, 0f),
                        DustID.Torch, new Vector2(0f, -Main.rand.NextFloat(0.3f, 1f)), 120, default, 0.8f);
                    spark.noGravity = true;
                }
                float progress = elapsed / (float)Telegraph;
                Lighting.AddLight(Projectile.Center, MarkWarn.R / 255f * 0.2f * progress,
                    MarkWarn.G / 255f * 0.2f * progress, MarkWarn.B / 255f * 0.2f * progress);
            }
            else {
                float glow = RiseProgress * FadeFactor;
                Lighting.AddLight(Projectile.Center, 0.75f * glow, 0.45f * glow, 0.15f * glow);
            }
        }

        /// <summary>圆域判定：环径即真相（判定半径与标记环同一常量）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            float radius = BlastRadius * RiseProgress;
            Vector2 center = Projectile.Center;
            float cx = MathHelper.Clamp(center.X, targetHitbox.Left, targetHitbox.Right);
            float cy = MathHelper.Clamp(center.Y, targetHitbox.Top, targetHitbox.Bottom);
            return new Vector2(cx - center.X, cy - center.Y).LengthSquared() <= radius * radius;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire, 150);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            Vector2 groundPos = Projectile.Center - Main.screenPosition;
            Texture2D glow = CWRAsset.SoftGlow.Value;

            if (elapsed < Telegraph) {
                //预告期：圆周光点环（椭圆透视贴地）+ 中心随引爆临近增亮的火漩
                float progress = elapsed / (float)Telegraph;
                float fadeIn = MathHelper.Clamp(elapsed / 10f, 0f, 1f);
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 13f + Projectile.identity);
                float spin = Main.GlobalTimeWrappedHourly * 1.4f + Projectile.identity;
                for (int i = 0; i < RingDots; i++) {
                    float ang = spin + i * (MathHelper.TwoPi / RingDots);
                    Vector2 dotPos = groundPos + new Vector2(MathF.Cos(ang) * BlastRadius, MathF.Sin(ang) * BlastRadius * 0.38f);
                    Main.EntitySpriteDraw(glow, dotPos, null, MarkWarn * (0.55f * fadeIn * pulse), 0f,
                        glow.Size() / 2f, 0.14f, SpriteEffects.None, 0);
                }
                //中心火漩：越临近引爆越亮越大（进度即读秒）
                Main.EntitySpriteDraw(glow, groundPos, null, MarkWarn * (0.3f + 0.5f * progress) * fadeIn, 0f,
                    glow.Size() / 2f, new Vector2(0.35f + 0.3f * progress, 0.22f + 0.18f * progress), SpriteEffects.None, 0);
                return false;
            }

            //迸燃：暗焰穹衬（真 alpha，火团轮廓）+ 加色热芯 + 首帧白闪
            float rise = RiseProgress;
            float fade = FadeFactor;
            if (rise <= 0.01f || fade <= 0.01f) {
                return false;
            }
            Texture2D under = CWRAsset.Extra_98.Value;
            float radius = BlastRadius * rise;
            Vector2 domePos = groundPos - new Vector2(0f, radius * 0.35f);
            Color deep = BlastDeep * (0.8f * fade);
            Main.EntitySpriteDraw(under, domePos, null, deep, 0f, under.Size() / 2f,
                new Vector2(radius * 2.3f / under.Width, radius * 1.7f / under.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, domePos, null, BlastHot * (0.85f * fade), 0f, glow.Size() / 2f,
                new Vector2(radius * 1.5f / 100f, radius * 1.1f / 100f), SpriteEffects.None, 0);
            float flash = MathHelper.Clamp(1f - (elapsed - Telegraph) / 5f, 0f, 1f);
            if (flash > 0f) {
                Main.EntitySpriteDraw(glow, domePos, null, (Color.White with { A = 0 }) * (0.7f * flash), 0f,
                    glow.Size() / 2f, radius * 1.1f / 100f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
