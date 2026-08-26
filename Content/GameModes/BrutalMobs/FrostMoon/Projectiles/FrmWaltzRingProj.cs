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
    /// 冰雪女王·冰晶华尔兹：ai[0]=安全楔中心角（权威掷定后随生成包同步）。
    /// 生成位置即环心（预告即承诺，环心不追踪）：淡入预告 ≥40 帧（无判定）→ 冰晶环收缩
    /// （判定窗=可见收缩窗）→ 碎裂退场（纯视觉，无伤害）。环方位恒定不旋转（差异化契约）；
    /// 安全楔由 <see cref="WedgeHalfAngle"/> 表达，判定采样与绘制读取同一跳角判据，
    /// 楔缘另画两道冷色界线（可见缺口=真实缺口）
    /// </summary>
    internal class FrmWaltzRingProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>冰晶数（方位固定不旋转）</summary>
        internal const int CrystalCount = 14;
        /// <summary>起始半径</summary>
        internal const float RingStartRadius = 340f;
        /// <summary>终止半径（到此碎裂退场）</summary>
        internal const float RingEndRadius = 48f;
        /// <summary>淡入预告帧（小Boss契约 ≥40，全程无判定）</summary>
        internal const int FadeInFrames = 46;
        /// <summary>收缩帧（判定窗=此窗）</summary>
        internal const int ContractFrames = 132;
        /// <summary>安全楔半张角（弧度）：判定与绘制共用的逃生阀门</summary>
        internal const float WedgeHalfAngle = 0.58f;
        /// <summary>单枚冰晶判定半径</summary>
        internal const float CrystalHitRadius = 17f;
        private const int ShatterFrames = 18;

        private static readonly Color IceBody = new Color(150, 208, 255);
        private static readonly Color IceGlow = new Color(120, 190, 255, 0);
        private static readonly Color WedgeSafe = new Color(140, 255, 214, 0);

        private float WedgeDir => Projectile.ai[0];
        private int TotalLife => FadeInFrames + ContractFrames + ShatterFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;
        private bool Contracting => Elapsed >= FadeInFrames && Elapsed < FadeInFrames + ContractFrames;

        /// <summary>当前环半径（各端由同步的生成时刻确定性推得）</summary>
        private float CurrentRadius {
            get {
                int t = Elapsed - FadeInFrames;
                if (t <= 0) {
                    return RingStartRadius;
                }
                if (t >= ContractFrames) {
                    return RingEndRadius;
                }
                return MathHelper.Lerp(RingStartRadius, RingEndRadius, t / (float)ContractFrames);
            }
        }

        /// <summary>安全楔判据：判定采样与绘制共用（可见缺口=真实缺口）</summary>
        private bool InWedge(float angle)
            => Math.Abs(MathHelper.WrapAngle(angle - WedgeDir)) < WedgeHalfAngle;

        private static float CrystalAngle(int index) => index / (float)CrystalCount * MathHelper.TwoPi;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 720;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;//收缩窗内才置真
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
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
                }
            }

            int elapsed = Elapsed;
            //判定窗=可见收缩窗
            Projectile.hostile = Contracting;

            if (elapsed == FadeInFrames && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.6f, Pitch = 0.25f, MaxInstances = 3 }, Projectile.Center);
            }
            if (elapsed == FadeInFrames + ContractFrames && !Main.dedServ) {
                //碎裂退场（纯视觉，无伤害；非死亡机制）
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.7f, Pitch = -0.45f, MaxInstances = 3 }, Projectile.Center);
                float radius = RingEndRadius;
                for (int i = 0; i < 6; i++) {
                    float ang = CrystalAngle(Main.rand.Next(CrystalCount));
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * radius,
                        DustID.Ice, ang.ToRotationVector2() * Main.rand.NextFloat(2f, 6f), 90, default, Main.rand.NextFloat(1f, 1.6f));
                    dust.noGravity = true;
                }
            }

            //收缩期环上冰雾（≤3 粒/帧）
            if (Contracting && !Main.dedServ && Main.rand.NextBool(2)) {
                float ang = CrystalAngle(Main.rand.Next(CrystalCount));
                if (!InWedge(ang)) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * CurrentRadius,
                        DustID.Frost, -ang.ToRotationVector2() * Main.rand.NextFloat(0.4f, 1.2f), 130, default, 0.9f);
                    dust.noGravity = true;
                }
            }

            Lighting.AddLight(Projectile.Center, IceBody.ToVector3() * 0.22f);
        }

        /// <summary>逐枚冰晶圆判定：楔内恒跳过（与绘制同一判据）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            float radius = CurrentRadius;
            for (int i = 0; i < CrystalCount; i++) {
                float ang = CrystalAngle(i);
                if (InWedge(ang)) {
                    continue;
                }
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * radius;
                Vector2 nearest = new Vector2(
                    MathHelper.Clamp(pos.X, targetHitbox.Left, targetHitbox.Right),
                    MathHelper.Clamp(pos.Y, targetHitbox.Top, targetHitbox.Bottom));
                if (Vector2.DistanceSquared(pos, nearest) <= CrystalHitRadius * CrystalHitRadius) {
                    return true;
                }
            }
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Frostburn, 120);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float radius = CurrentRadius;
            float alpha;
            if (elapsed < FadeInFrames) {
                alpha = MathHelper.Clamp(elapsed / (float)FadeInFrames, 0f, 1f) * 0.55f;
            }
            else if (Contracting) {
                alpha = 1f;
            }
            else {
                alpha = MathHelper.Clamp(1f - (elapsed - FadeInFrames - ContractFrames) / (float)ShatterFrames, 0f, 1f) * 0.5f;
            }
            if (alpha <= 0.01f) {
                return false;
            }

            Main.instance.LoadProjectile(ProjectileID.DeerclopsIceSpike);
            Texture2D spike = TextureAssets.Projectile[ProjectileID.DeerclopsIceSpike].Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D line = CWRAsset.Extra_98.Value;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + Projectile.identity);

            //安全楔界线：两道冷色缺口界（楔=显式保持的逃生门，可见即真实）
            for (int s = -1; s <= 1; s += 2) {
                float edge = WedgeDir + WedgeHalfAngle * s;
                Vector2 mid = center + edge.ToRotationVector2() * radius * 0.6f;
                Main.EntitySpriteDraw(glow, mid, null, WedgeSafe * (0.32f * alpha * pulse), edge,
                    glow.Size() / 2f, new Vector2(radius * 0.8f / glow.Width, 0.08f), SpriteEffects.None, 0);
            }

            //冰晶环：方位恒定不旋转，尖端指向环心
            for (int i = 0; i < CrystalCount; i++) {
                float ang = CrystalAngle(i);
                if (InWedge(ang)) {
                    continue;//楔内不画（绘制与判定同一判据）
                }
                Vector2 pos = center + ang.ToRotationVector2() * radius;
                Rectangle rect = spike.Frame(1, 5, 0, (i + Projectile.identity) % 5);
                Vector2 orig = new Vector2(16f, rect.Height / 2f);
                float inward = ang + MathHelper.Pi;//尖端向心
                //暗底衬（真 alpha 轮廓）
                Main.EntitySpriteDraw(line, pos, null, new Color(40, 66, 104) * (0.5f * alpha), inward + MathHelper.PiOver2,
                    line.Size() / 2f, new Vector2(0.16f, 0.3f), SpriteEffects.None, 0);
                //冰晶本体（原版贴图实体层）
                Main.EntitySpriteDraw(spike, pos, rect, Color.Lerp(lightColor, IceBody, 0.6f) * alpha, inward,
                    orig, 0.62f, SpriteEffects.None, 0);
                //辉光敷料
                Main.EntitySpriteDraw(glow, pos, null, IceGlow * (0.3f * alpha * pulse), 0f,
                    glow.Size() / 2f, 0.24f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
