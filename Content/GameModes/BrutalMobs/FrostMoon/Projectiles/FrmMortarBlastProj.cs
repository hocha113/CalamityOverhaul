using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.FrostMoon.Projectiles
{
    /// <summary>
    /// 礼盒迫击炮落点：ai[0]=引信帧（=炮弹飞行时长，即标记环可见时长） ai[1]=半径比例。
    /// 生成位置即锁定弹着点（预告即承诺）：地面标记环亮起 → 引信归零轰爆 → 余烬消散。
    /// 弹着半径与标记环半径共用 <see cref="BlastRadius"/> 同一常量（×同一比例），
    /// 判定窗=可见爆窗；标记环静止不旋转，只做透明度脉动
    /// </summary>
    internal class FrmMortarBlastProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>弹着半径基准（像素）。Colliding 判定与标记环绘制读取同一常量</summary>
        internal const float BlastRadius = 110f;
        /// <summary>轰爆判定帧（判定窗=可见爆窗）</summary>
        internal const int BurstFrames = 14;
        /// <summary>余烬帧（无判定的消散尾）</summary>
        private const int LingerFrames = 18;
        /// <summary>引信下限（公平契约：标记可见 ≥30 帧）</summary>
        private const int MinFuseFrames = 30;
        /// <summary>标记环缘的光点数（静态方位，不旋转）</summary>
        private const int RimDotCount = 12;

        private static readonly Color WarnGold = new Color(255, 208, 96, 0);
        private static readonly Color WarnRed = new Color(255, 96, 72, 0);
        private static readonly Color BurstSmoke = new Color(96, 62, 40);

        private int FuseFrames => Math.Max((int)Projectile.ai[0], MinFuseFrames);
        private float RadiusScale => Projectile.ai[1] <= 0f ? 1f : Projectile.ai[1];
        private float Radius => BlastRadius * RadiusScale;
        private int TotalLife => FuseFrames + BurstFrames + LingerFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 420;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;//轰爆窗内才置真
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
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.4f, Pitch = -0.35f, MaxInstances = 6 }, Projectile.Center);
                }
            }

            int elapsed = Elapsed;
            //判定窗=可见爆窗（各端由同步的 ai[0] 确定性推得同一时刻）
            Projectile.hostile = elapsed >= FuseFrames && elapsed < FuseFrames + BurstFrames;

            if (elapsed == FuseFrames) {
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.62f, Pitch = 0.15f, MaxInstances = 6 }, Projectile.Center);
                    //爆帧碎屑（一次性）
                    for (int i = 0; i < 6; i++) {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center,
                            Main.rand.NextBool() ? DustID.Snow : DustID.Smoke,
                            Main.rand.NextVector2Circular(1f, 0.5f) * Main.rand.NextFloat(2f, 6f) * RadiusScale
                            + new Vector2(0f, -Main.rand.NextFloat(2f, 5f)),
                            90, default, Main.rand.NextFloat(1.1f, 1.7f));
                        dust.noGravity = Main.rand.NextBool();
                    }
                }
            }
            else if (elapsed < FuseFrames && !Main.dedServ && Main.rand.NextBool(4)) {
                //预告期：环缘零星雪尘（≤1 粒/帧）
                float ang = Main.rand.Next(RimDotCount) / (float)RimDotCount * MathHelper.TwoPi;
                Dust dust = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * Radius,
                    DustID.Snow, new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.2f)), 130, default, 0.9f);
                dust.noGravity = true;
            }

            float burstVis = BurstVisual();
            if (burstVis > 0.05f) {
                Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.72f, 0.4f) * 0.7f * burstVis);
            }
        }

        /// <summary>轰爆可见强度 1→0（爆帧起衰减到余烬结束）</summary>
        private float BurstVisual() {
            int t = Elapsed - FuseFrames;
            if (t < 0) {
                return 0f;
            }
            return MathHelper.Clamp(1f - t / (float)(BurstFrames + LingerFrames), 0f, 1f);
        }

        /// <summary>圆形弹着判定：目标碰撞箱到弹着点最近距离 ≤ 半径（与标记环同一常量）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            float radius = Radius;
            Vector2 nearest = new Vector2(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(Projectile.Center, nearest) <= radius * radius;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 glowOrig = glow.Size() / 2f;
            Vector2 center = Projectile.Center - Main.screenPosition;
            int elapsed = Elapsed;
            float radius = Radius;

            if (elapsed < FuseFrames) {
                //预告期：静止标记环（环缘光点+扁平地面光晕），只做脉动不旋转
                float grow = MathHelper.Clamp(elapsed / 10f, 0f, 1f);
                float urgency = MathHelper.Clamp(1f - (FuseFrames - elapsed) / (float)FuseFrames, 0f, 1f);
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + Projectile.identity);
                Color warn = Color.Lerp(WarnGold, WarnRed, urgency);

                //地面扁光晕（中心提示）
                Main.EntitySpriteDraw(glow, center, null, warn * (0.3f * grow * pulse), 0f, glowOrig,
                    new Vector2(radius / glow.Width * 1.6f, 0.4f), SpriteEffects.None, 0);

                //环缘光点：方位固定（差异化契约：标记环不旋转）
                for (int i = 0; i < RimDotCount; i++) {
                    float ang = i / (float)RimDotCount * MathHelper.TwoPi;
                    Vector2 dotPos = center + ang.ToRotationVector2() * radius * grow;
                    float dotPulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + i * 1.7f);
                    Main.EntitySpriteDraw(glow, dotPos, null, warn * (0.55f * grow * dotPulse), 0f, glowOrig,
                        0.11f + 0.05f * urgency, SpriteEffects.None, 0);
                }
                return false;
            }

            float vis = BurstVisual();
            if (vis <= 0.01f) {
                return false;
            }

            //轰爆：真 alpha 烟障轮廓 + 加色闪芯（暗层不走加色）
            Texture2D smoke = CWRAsset.Extra_98.Value;
            float expand = MathHelper.Clamp((Elapsed - FuseFrames + 1) / 6f, 0.3f, 1f);
            Vector2 smokeScale = new Vector2(radius * 2.3f / smoke.Width, radius * 2f / smoke.Height) * expand;
            Main.EntitySpriteDraw(smoke, center, null, BurstSmoke * (0.72f * vis), Projectile.identity * 1.3f,
                smoke.Size() / 2f, smokeScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(smoke, center, null, BurstSmoke * (0.5f * vis), -Projectile.identity * 0.9f,
                smoke.Size() / 2f, smokeScale * 0.72f, SpriteEffects.None, 0);
            //闪芯（爆即光，加色仅作辉光敷料）
            Main.EntitySpriteDraw(glow, center, null, WarnGold * (0.85f * vis), 0f, glowOrig,
                radius * 2f / glow.Width * expand, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, center, null, Color.White with { A = 0 } * (0.55f * vis * vis), 0f, glowOrig,
                radius * 1.1f / glow.Width * expand, SpriteEffects.None, 0);
            return false;
        }
    }
}
