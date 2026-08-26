using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.EvilBiome.Projectiles
{
    /// <summary>
    /// 侵蚀瘴云:缓扩场地侵蚀云。由 <see cref="ErosionCloudSeed"/> 绽放,
    /// 生长→驻留→消散,带一条出生即锁定的具名角向缺口(缺口内无雾亦无判定,可见即安全)。
    /// ai[0]=风味 ai[1]=缺口中心角 ai[2]=出生档位
    /// </summary>
    internal class ErosionCloudProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //====== 具名数值块 ======
        /// <summary>生长期帧数(半径由 0 缓扩到满,期间伤害范围=可见范围同步长大)</summary>
        private const int GrowFrames = 150;
        /// <summary>驻留期帧数</summary>
        private const int HoldFrames = 336;
        /// <summary>消散期帧数(参照酸池:消散过 35% 即失去判定)</summary>
        private const int DryFrames = 66;
        private const int TotalFrames = GrowFrames + HoldFrames + DryFrames;
        /// <summary>档位 1 满半径;档位每 +1 半径 +15%(只调强度)</summary>
        private const float BaseMaxRadius = 150f;
        private const float TierRadiusStep = 0.15f;
        /// <summary>逃生缺口半角(发射与判定共用,总口径约 63°)</summary>
        public const float GapHalfAngle = 0.55f;
        /// <summary>雾团相对缺口边缘的额外留白,保证摆动不会侵入缺口</summary>
        private const float PuffMargin = 0.12f;
        /// <summary>雾团角向摆动幅度(必须小于 PuffMargin)</summary>
        private const float SwirlAmp = 0.06f;
        /// <summary>判定半径 = 可见半径 × 此系数(判定略窄,偏袒玩家)</summary>
        private const float CollideRadiusFrac = 0.9f;
        /// <summary>雾团数量</summary>
        private const int PuffCount = 12;

        private int Flavor => (int)Projectile.ai[0];
        private float GapCenter => Projectile.ai[1];
        private int Tier => Math.Clamp((int)Projectile.ai[2], 1, 3);

        private int Elapsed => TotalFrames - Projectile.timeLeft;
        private float MaxRadius => BaseMaxRadius * (1f + TierRadiusStep * (Tier - 1));

        /// <summary>当前可见半径(缓扩:二次缓出)</summary>
        private float CurrentRadius {
            get {
                float t = MathHelper.Clamp(Elapsed / (float)GrowFrames, 0f, 1f);
                return MaxRadius * (1f - (1f - t) * (1f - t));
            }
        }

        /// <summary>0 浓郁 → 1 散尽(最后 DryFrames 帧)</summary>
        private float DryProgress => MathHelper.Clamp((DryFrames - Projectile.timeLeft) / (float)DryFrames, 0f, 1f);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //撑大命中盒仅为照明/剔除服务,真判定在 Colliding
                Projectile.Resize((int)(MaxRadius * 2f * CollideRadiusFrac), (int)(MaxRadius * 2f * CollideRadiusFrac));
            }

            //消散过 35% 后失去伤害(伤害窗口=可见窗口)
            if (DryProgress > 0.35f) {
                Projectile.hostile = false;
            }

            //云缘渗雾(客户端,预算 ≤1 粒/帧)
            if (!VaultUtils.isServer && Main.rand.NextBool(3) && CurrentRadius > 30f) {
                float freshness = 1f - DryProgress;
                float band = MathHelper.TwoPi - 2f * (GapHalfAngle + PuffMargin);
                float ang = GapCenter + GapHalfAngle + PuffMargin + Main.rand.NextFloat(band);
                Vector2 p = Projectile.Center + ang.ToRotationVector2() * CurrentRadius * Main.rand.NextFloat(0.8f, 1f);
                Dust dust = Dust.NewDustPerfect(p, EvilBiomeFX.DustFor(Flavor),
                    ang.ToRotationVector2() * 0.3f, 150, default, 1f * freshness + 0.2f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, EvilBiomeFX.Bright(Flavor).ToVector3() * 0.3f * (1f - DryProgress));
        }

        /// <summary>圆盘判定 + 具名缺口豁免(缺口扇区内可见为空,判定亦为空)</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            float radius = CurrentRadius * CollideRadiusFrac;
            Vector2 center = Projectile.Center;
            Vector2 closest = new(
                MathHelper.Clamp(center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(center.Y, targetHitbox.Top, targetHitbox.Bottom));
            if (Vector2.DistanceSquared(closest, center) > radius * radius) {
                return false;
            }
            float ang = (targetHitbox.Center.ToVector2() - center).ToRotation();
            if (Math.Abs(MathHelper.WrapAngle(ang - GapCenter)) < GapHalfAngle) {
                return false;
            }
            return true;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //减益时长随档位小幅上调(4/5/6 秒)
            target.AddBuff(EvilBiomeFX.BuffFor(Flavor), (3 + Tier) * 60);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D fog = CWRAsset.Fog.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 fogOrigin = fog.Size() * 0.5f;
            Vector2 glowOrigin = glow.Size() * 0.5f;
            Vector2 center = Projectile.Center - Main.screenPosition;

            float radius = CurrentRadius;
            float alphaIn = MathHelper.Clamp(Elapsed / 40f, 0f, 1f);
            float fade = alphaIn * (1f - DryProgress);
            if (fade <= 0.01f || radius < 8f) {
                return false;
            }

            Color deep = EvilBiomeFX.Deep(Flavor);
            Color bright = EvilBiomeFX.Bright(Flavor);

            //中央雾体(真 alpha 层,承担实体感)
            float bodyScale = radius * 1.5f / fog.Width;
            Main.EntitySpriteDraw(fog, center, null, deep * (0.4f * fade),
                Projectile.identity * 0.7f, fogOrigin, bodyScale, SpriteEffects.None, 0);

            //环布雾团:角向只在缺口以外的带内取值,缺口可见为空=安全
            float band = MathHelper.TwoPi - 2f * (GapHalfAngle + PuffMargin);
            for (int i = 0; i < PuffCount; i++) {
                float hA = Hash(i, 1);
                float hR = Hash(i, 2);
                float hS = Hash(i, 3);
                float swirl = MathF.Sin(Main.GlobalTimeWrappedHourly * (0.5f + hS * 0.5f) + i * 1.7f) * SwirlAmp;
                float ang = GapCenter + GapHalfAngle + PuffMargin + band * hA + swirl;
                float r = radius * (0.2f + 0.75f * MathF.Sqrt(hR));
                Vector2 pos = center + ang.ToRotationVector2() * r;
                float puffScale = (0.24f + 0.18f * hS) * (radius / BaseMaxRadius);
                float rot = hA * MathHelper.TwoPi + Main.GlobalTimeWrappedHourly * (hS - 0.5f) * 0.6f;

                //暗层雾团(A>0)+ 亮芯(加色)
                Main.EntitySpriteDraw(fog, pos, null, deep * (0.55f * fade),
                    rot, fogOrigin, puffScale, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, pos, null, bright with { A = 0 } * (0.22f * fade),
                    0f, glowOrigin, puffScale * 2.6f, SpriteEffects.None, 0);
            }
            return false;
        }

        /// <summary>确定性散列(各端一致,不触碰 Main.rand)</summary>
        private float Hash(int i, int salt) => (Projectile.identity * 137 + i * 61 + salt * 23) % 97 / 97f;
    }
}
