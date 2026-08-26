using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Sporeshine.Projectiles
{
    /// <summary>
    /// 迷醉孢雾：孢子团落地绽开的短暂雾区（「巨菇喷发」的落地段）。
    /// ai[0]=档位（只调雾浓度视觉，机制形状不变）。
    /// 生长→驻留→消散；雾内微量伤害+短暂中毒，浓雾期供 <see cref="SporeshinePlayer"/> 累积孢醉；
    /// 消散段浮出荧光残点作余韵。Boss 在场时判定暂停（视觉保留）。
    /// 与腐化瘴柱划清：这里是抛物孢子团落地成雾，小半径、无角向缺口，逃离方式是走出雾区
    /// </summary>
    internal class SporeshineSporeFogProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //====== 具名数值块 ======
        /// <summary>生长期帧数（半径由 0 缓扩到满）</summary>
        private const int GrowFrames = 40;
        /// <summary>驻留期帧数</summary>
        private const int HoldFrames = 190;
        /// <summary>消散期帧数（消散过 35% 即失去判定）</summary>
        private const int DryFrames = 70;
        private const int TotalFrames = GrowFrames + HoldFrames + DryFrames;
        /// <summary>满雾半径（档位不改半径，只改浓度）</summary>
        private const float MaxRadius = 118f;
        /// <summary>判定半径 = 可见半径 × 此系数（判定略窄，偏袒玩家）</summary>
        internal const float CollideRadiusFrac = 0.85f;
        /// <summary>雾团数量</summary>
        private const int PuffCount = 10;
        /// <summary>中毒时长（固定，不随档位）</summary>
        private const int PoisonFrames = 240;
        /// <summary>消散段荧光残点数量</summary>
        private const int EmberCount = 6;
        /// <summary>档位→雾浓度系数（只作用于粉尘频率与雾层透明度）</summary>
        private static readonly float[] DensityByTier = [0.8f, 1f, 1.25f];

        private static readonly Color DeepSpore = new(24, 46, 88);
        private static readonly Color BrightSpore = new(96, 205, 255);

        private int Tier => Math.Clamp((int)Projectile.ai[0], 1, 3);
        private float Density => DensityByTier[Tier - 1];
        private int Elapsed => TotalFrames - Projectile.timeLeft;

        /// <summary>0 浓郁 → 1 散尽（最后 DryFrames 帧）</summary>
        private float DryProgress => MathHelper.Clamp((DryFrames - Projectile.timeLeft) / (float)DryFrames, 0f, 1f);

        /// <summary>当前可见半径（二次缓出）</summary>
        internal float CurrentRadius {
            get {
                float t = MathHelper.Clamp(Elapsed / (float)GrowFrames, 0f, 1f);
                return MaxRadius * (1f - (1f - t) * (1f - t));
            }
        }

        /// <summary>浓雾窗口：孢醉计量只在此口径内累积（半程成形后、未开始明显消散）</summary>
        internal bool DenseNow => Elapsed >= GrowFrames / 2 && DryProgress <= 0.35f;

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
                //撑大命中盒仅为照明/剔除服务，真判定在 Colliding
                Projectile.Resize((int)(MaxRadius * 2f * CollideRadiusFrac), (int)(MaxRadius * 2f * CollideRadiusFrac));
            }

            //判定窗=浓雾窗；Boss 在场时机制暂停（各端从同一世界状态各自判断）
            Projectile.hostile = DryProgress <= 0.35f && !CWRWorld.HasBoss;

            //雾内孢尘（客户端；频率随档位浓度走，屏远剔除）
            if (!VaultUtils.isServer && CurrentRadius > 30f
                && Vector2.DistanceSquared(Projectile.Center, Main.LocalPlayer.Center) < 1400f * 1400f) {
                float freshness = 1f - DryProgress;
                if (Main.rand.NextFloat() < (0.08f + 0.07f * Density) * freshness) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    float r = CurrentRadius * MathF.Sqrt(Main.rand.NextFloat());
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * r,
                        DustID.GlowingMushroom, new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.2f, 0.7f)),
                        140, default, 0.8f + 0.4f * freshness);
                    dust.noGravity = true;
                }
            }
            Lighting.AddLight(Projectile.Center, BrightSpore.ToVector3() * 0.28f * (1f - DryProgress));
        }

        /// <summary>圆盘判定（可见雾=判定雾，判定略窄）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            float radius = CurrentRadius * CollideRadiusFrac;
            Vector2 center = Projectile.Center;
            Vector2 closest = new(
                MathHelper.Clamp(center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(closest, center) <= radius * radius;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //短暂原版中毒（命中方本机结算，原生同步；固定时长不随档位）
            target.AddBuff(BuffID.Poisoned, PoisonFrames);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D fog = CWRAsset.Fog.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture_White.Value;
            Vector2 fogOrigin = fog.Size() * 0.5f;
            Vector2 glowOrigin = glow.Size() * 0.5f;
            Vector2 starOrigin = star.Size() * 0.5f;
            Vector2 center = Projectile.Center - Main.screenPosition;

            float radius = CurrentRadius;
            float dry = DryProgress;
            float alphaIn = MathHelper.Clamp(Elapsed / 30f, 0f, 1f);
            float fade = alphaIn * (1f - dry);

            //消散段荧光残点：雾体退去时浮出，活到实体终点（余韵）
            if (dry > 0.1f) {
                float emberIn = MathHelper.Clamp((dry - 0.1f) / 0.25f, 0f, 1f);
                float emberOut = MathHelper.Clamp((1f - dry) / 0.12f, 0f, 1f);
                float twinkleTime = Main.GlobalTimeWrappedHourly * 5f;
                for (int i = 0; i < EmberCount; i++) {
                    float hA = Hash(i, 1);
                    float hR = Hash(i, 2);
                    Vector2 pos = center + (hA * MathHelper.TwoPi).ToRotationVector2() * (radius * (0.25f + 0.6f * hR))
                        - new Vector2(0f, dry * 14f);//残点缓缓上浮
                    float twinkle = 0.6f + 0.4f * MathF.Sin(twinkleTime + i * 2.3f);
                    Color ember = BrightSpore with { A = 0 } * (0.5f * emberIn * emberOut * twinkle);
                    Main.EntitySpriteDraw(star, pos, null, ember, hA * 3f, starOrigin, 0.05f + 0.03f * hR, SpriteEffects.None, 0);
                }
            }

            if (fade <= 0.01f || radius < 8f) {
                return false;
            }

            //浓度只抬透明度与粉尘，几何不变；透明度封顶防糊死
            float deepA = MathF.Min(0.5f * Density, 0.62f) * fade;
            float coreA = 0.2f * Density * fade;

            //中央雾体（真 alpha 层，承担实体感）
            float bodyScale = radius * 1.5f / fog.Width;
            Main.EntitySpriteDraw(fog, center, null, DeepSpore * (0.75f * deepA),
                Projectile.identity * 0.7f, fogOrigin, bodyScale, SpriteEffects.None, 0);

            //环布雾团：暗层承轮廓 + 加色亮芯
            for (int i = 0; i < PuffCount; i++) {
                float hA = Hash(i, 1);
                float hR = Hash(i, 2);
                float hS = Hash(i, 3);
                float swirl = MathF.Sin(Main.GlobalTimeWrappedHourly * (0.5f + hS * 0.5f) + i * 1.7f) * 0.1f;
                float ang = hA * MathHelper.TwoPi + swirl;
                float r = radius * (0.2f + 0.72f * MathF.Sqrt(hR));
                Vector2 pos = center + ang.ToRotationVector2() * r;
                float puffScale = (0.22f + 0.16f * hS) * (radius / MaxRadius);
                float rot = hA * MathHelper.TwoPi + Main.GlobalTimeWrappedHourly * (hS - 0.5f) * 0.6f;
                SpriteEffects flip = hS > 0.5f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                Main.EntitySpriteDraw(fog, pos, null, DeepSpore * deepA, rot, fogOrigin, puffScale, flip, 0);
                Main.EntitySpriteDraw(glow, pos, null, BrightSpore with { A = 0 } * coreA,
                    0f, glowOrigin, puffScale * 2.4f, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //雾散后残留几粒慢飘荧光尘，接住残点的余韵
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(MaxRadius * 0.5f, MaxRadius * 0.4f),
                    DustID.GlowingMushroom, new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.6f)),
                    150, default, Main.rand.NextFloat(0.7f, 1f));
                dust.noGravity = true;
            }
        }

        /// <summary>确定性散列（各端一致，不触碰 Main.rand）</summary>
        private float Hash(int i, int salt) => (Projectile.identity * 137 + i * 61 + salt * 23) % 97 / 97f;
    }
}
