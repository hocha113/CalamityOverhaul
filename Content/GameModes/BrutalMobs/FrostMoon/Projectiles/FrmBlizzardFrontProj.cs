using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.FrostMoon.Projectiles
{
    /// <summary>
    /// 冰雪女王·暴风雪锋面：ai[0]=扫掠方向（±1）。生成位置即航道起点（预告即承诺）：
    /// 预告期整条航道亮出上下界线与锋面虚影（无判定、零速度）→ 锋面沿航道匀速扫过
    /// （判定窗=可见扫掠窗，判定高与界线共用 <see cref="LaneHalfHeight"/>）→ 航道尽头消散。
    /// 迟入端以速度非零为已扫掠证据快进相位；纵向脱离航道即安全
    /// </summary>
    internal class FrmBlizzardFrontProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>预告帧（小Boss契约 ≥40）</summary>
        internal const int PreviewFrames = 50;
        /// <summary>航道半高：判定、界线绘制、锋面绘制共用（可见高度=真实威胁高度）</summary>
        internal const float LaneHalfHeight = 132f;
        /// <summary>锋面判定半宽</summary>
        internal const float WallHalfWidth = 34f;
        /// <summary>扫掠速度（像素/帧）</summary>
        internal const float SweepSpeed = 9f;
        /// <summary>航道全长：扫掠帧 = 航道长 ÷ 扫掠速度 = 1503/9 ≈ 167 帧，全程判定连续覆盖</summary>
        internal const float LaneLength = 1503f;
        internal const int SweepFrames = 167;
        private const int FadeFrames = 16;

        private static readonly Color FrostVeil = new Color(196, 226, 255, 0);
        private static readonly Color FrostDark = new Color(58, 84, 122);

        private float Dir => Projectile.ai[0] >= 0f ? 1f : -1f;
        private int TotalLife => PreviewFrames + SweepFrames + FadeFrames;
        private int Elapsed => (int)Projectile.localAI[1] - Projectile.timeLeft;
        private bool Sweeping => Elapsed >= PreviewFrames && Elapsed < PreviewFrames + SweepFrames;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;

        public override void SetDefaults() {
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.hostile = false;//扫掠窗内才置真
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TotalLife;
                Projectile.localAI[1] = Projectile.timeLeft;
                //迟入端：速度非零=服务端已在扫掠（同步证据），相位快进不重放预告
                if (Projectile.velocity.LengthSquared() > 1f) {
                    Projectile.timeLeft = SweepFrames + FadeFrames;
                }
                else if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.55f, Pitch = -0.5f, MaxInstances = 3 }, Projectile.Center);
                }
            }

            int elapsed = Elapsed;
            //相位速度：各端同帧确定性赋值，无需逐帧同步
            Projectile.velocity = Sweeping ? new Vector2(Dir * SweepSpeed, 0f) : Vector2.Zero;
            //判定窗=可见扫掠窗
            Projectile.hostile = Sweeping;

            if (elapsed == PreviewFrames && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.4f, Pitch = -0.6f, MaxInstances = 3 }, Projectile.Center);
            }

            //扫掠期锋面雪幕（≤5 粒/帧，仅此窗口）
            if (Sweeping && !Main.dedServ) {
                for (int i = 0; i < 3; i++) {
                    if (Main.rand.NextBool(2)) {
                        continue;
                    }
                    Dust dust = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-WallHalfWidth, WallHalfWidth),
                            Main.rand.NextFloat(-LaneHalfHeight, LaneHalfHeight)),
                        DustID.Snow, new Vector2(Dir * Main.rand.NextFloat(2f, 5f), Main.rand.NextFloat(-1f, 1f)),
                        100, default, Main.rand.NextFloat(1f, 1.7f));
                    dust.noGravity = true;
                }
            }

            Lighting.AddLight(Projectile.Center, 0.24f, 0.3f, 0.4f);
        }

        /// <summary>锋面矩形判定：半宽/半高与绘制共用同一常量</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            Rectangle wall = Utils.CenteredRectangle(Projectile.Center,
                new Vector2(WallHalfWidth * 2f, LaneHalfHeight * 2f));
            return wall.Intersects(targetHitbox);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Chilled, 100);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float fade = elapsed >= PreviewFrames + SweepFrames
                ? MathHelper.Clamp(1f - (elapsed - PreviewFrames - SweepFrames) / (float)FadeFrames, 0f, 1f) : 1f;
            float fadeIn = MathHelper.Clamp(elapsed / 10f, 0f, 1f);
            float strength = fadeIn * fade;
            if (strength <= 0.01f) {
                return false;
            }

            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D veil = CWRAsset.Extra_98.Value;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.identity);

            //航道上下界线：自锋面当前位置延伸到航道尽头（预告期=全长）
            float traveled = Sweeping || elapsed >= PreviewFrames ? (elapsed - PreviewFrames) * SweepSpeed : 0f;
            float remain = LaneLength - traveled;
            if (remain > 30f) {
                float laneAngle = Dir > 0f ? 0f : MathHelper.Pi;
                for (int s = -1; s <= 1; s += 2) {
                    Vector2 edgeMid = center + new Vector2(Dir * remain * 0.5f, LaneHalfHeight * s);
                    Main.EntitySpriteDraw(glow, edgeMid, null, FrostVeil * (0.3f * strength * pulse), laneAngle,
                        glow.Size() / 2f, new Vector2(remain / glow.Width, 0.09f), SpriteEffects.None, 0);
                }
            }

            //锋面雪墙：真 alpha 暗底 + 分层雪幕（预告期为虚影）
            float wallAlpha = Sweeping ? 1f : 0.4f * pulse;
            Vector2 wallScale = new Vector2(WallHalfWidth * 2.6f / veil.Width, LaneHalfHeight * 2.1f / veil.Height);
            Main.EntitySpriteDraw(veil, center, null, FrostDark * (0.62f * strength * wallAlpha), 0f,
                veil.Size() / 2f, wallScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(veil, center + new Vector2(-Dir * 10f, 0f), null,
                new Color(120, 158, 205) * (0.5f * strength * wallAlpha), 0f,
                veil.Size() / 2f, wallScale * new Vector2(0.7f, 0.92f), SpriteEffects.None, 0);
            //白亮锋缘（加色敷料，置于前进侧）
            Main.EntitySpriteDraw(glow, center + new Vector2(Dir * WallHalfWidth * 0.7f, 0f), null,
                FrostVeil * (0.5f * strength * wallAlpha), MathHelper.PiOver2,
                glow.Size() / 2f, new Vector2(LaneHalfHeight * 2f / glow.Width, 0.3f), SpriteEffects.None, 0);
            return false;
        }
    }
}
