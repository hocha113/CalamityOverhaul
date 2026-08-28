using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Sporeshine.Projectiles
{
    /// <summary>
    /// 迷醉孢雾：孢子团落地绽开的悬浮孢子云（「巨菇喷发」的落地段）。
    /// ai[0]=档位（只调雾浓度视觉，机制形状不变）。
    /// 材质身份：半透蓝澜雾体（真 alpha 乘环境光）内悬浮发光孢子点（加法点缀），
    /// 整团缓慢呼吸脉动，边缘稀薄可读。
    /// 预告自持：成形 <see cref="ArmFrame"/>（≥45）帧后判定才开启，不依赖上游菌盖；
    /// 成形期孢点渐亮+咕噜升调三拍双通道。判定开启后浓雾供 <see cref="SporeshinePlayer"/> 累积孢醉。
    /// 逃逸声明：走出圆即免；外缘 <see cref="EdgeGraceFrac"/> 宽限带只有稀雾，不咬人不积醉。
    /// Boss 在场时判定暂停（视觉保留）
    /// </summary>
    internal class SporeshineSporeFogProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //====== 具名数值块 ======
        /// <summary>成形期帧数（半径由 0 缓扩到满）</summary>
        private const int GrowFrames = 50;
        /// <summary>判定开启帧：预告由雾自身持有（可见起算 ≥45 公平契约），上游被打断或中途进场都不吃亏</summary>
        private const int ArmFrame = GrowFrames;
        /// <summary>驻留期帧数</summary>
        private const int HoldFrames = 190;
        /// <summary>消散期帧数（消散过 35% 即失去判定）</summary>
        private const int DryFrames = 70;
        private const int TotalFrames = GrowFrames + HoldFrames + DryFrames;
        /// <summary>满雾半径（档位不改半径，只改浓度）</summary>
        private const float MaxRadius = 118f;
        /// <summary>判定半径 = 可见半径 × 此系数（判定略窄，偏袒玩家）</summary>
        private const float CollideRadiusFrac = 0.85f;
        /// <summary>逃逸宽限带：判定圈再向内让出的比例，雾缘稀薄区不叠伤不积醉（判定循环真读）</summary>
        internal const float EdgeGraceFrac = 0.2f;
        /// <summary>呼吸脉动幅度（只作用于可见半径；判定用不呼吸的基准半径，只小不大）</summary>
        private const float BreathAmp = 0.045f;
        /// <summary>雾团数量</summary>
        private const int PuffCount = 10;
        /// <summary>悬浮发光孢子点数量（加法点缀）</summary>
        private const int SporeDotCount = 14;
        /// <summary>中毒时长（固定，不随档位）</summary>
        private const int PoisonFrames = 240;
        /// <summary>消散段荧光残点数量</summary>
        private const int EmberCount = 6;
        /// <summary>环境光乘算下限（孢子云自带微光，防无光处判定隐形）</summary>
        private const float LightFloor = 0.3f;
        /// <summary>档位→雾浓度系数（只作用于粉尘频率与雾层透明度）</summary>
        private static readonly float[] DensityByTier = [0.8f, 1f, 1.25f];

        private static readonly Color DeepSpore = new(24, 46, 88);
        private static readonly Color BrightSpore = new(96, 205, 255);

        private int Tier => Math.Clamp((int)Projectile.ai[0], 1, 3);
        private float Density => DensityByTier[Tier - 1];
        private int Elapsed => TotalFrames - Projectile.timeLeft;

        /// <summary>0 浓郁 → 1 散尽（最后 DryFrames 帧）</summary>
        private float DryProgress => MathHelper.Clamp((DryFrames - Projectile.timeLeft) / (float)DryFrames, 0f, 1f);

        /// <summary>自持预告进度 0..1（孢点亮度与判定开启同源，伤害窗=视觉窗）</summary>
        private float ArmProgress => MathHelper.Clamp(Elapsed / (float)ArmFrame, 0f, 1f);

        /// <summary>当前可见半径（二次缓出）</summary>
        private float CurrentRadius {
            get {
                float t = MathHelper.Clamp(Elapsed / (float)GrowFrames, 0f, 1f);
                return MaxRadius * (1f - (1f - t) * (1f - t));
            }
        }

        /// <summary>判定半径：宽限带已让出（判定循环与孢醉计量都读这里）</summary>
        internal float HurtRadius => CurrentRadius * CollideRadiusFrac * (1f - EdgeGraceFrac);

        /// <summary>判定窗：自持预告走完且未散逸过 35%</summary>
        private bool Armed => Elapsed >= ArmFrame && DryProgress <= 0.35f;

        /// <summary>浓雾窗口：孢醉计量只在判定窗内累积（与判定同源）</summary>
        internal bool DenseNow => Armed;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.hostile = false;//自持预告期无判定，成形后由 AI 开启
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

            //判定窗=自持预告完成后的浓雾窗；Boss 在场时机制暂停（各端从同一世界状态各自判断）
            Projectile.hostile = Armed && !CWRWorld.HasBoss;

            //成形期咕噜升调三拍：雾自己的听觉预告通道（避开出生帧，防生成同步竞速漏拍）
            if (!Main.dedServ) {
                if (Elapsed == 6) {
                    SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.28f, Pitch = -0.2f, MaxInstances = 4 }, Projectile.Center);
                }
                else if (Elapsed == 28) {
                    SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.32f, Pitch = 0.05f, MaxInstances = 4 }, Projectile.Center);
                }
                else if (Elapsed == ArmFrame) {
                    SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.36f, Pitch = 0.3f, MaxInstances = 4 }, Projectile.Center);
                }
            }

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

        /// <summary>圆盘判定：判定圈=可见雾内圈（宽限带让出雾缘），走出圆即免</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            float radius = HurtRadius;
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

            float dry = DryProgress;
            float alphaIn = MathHelper.Clamp(Elapsed / 30f, 0f, 1f);
            float fade = alphaIn * (1f - dry);
            float time = Main.GlobalTimeWrappedHourly;

            //呼吸脉动：整团缓胀缓缩的悬浮感
            float breath = 1f + BreathAmp * MathF.Sin(time * 1.9f + Projectile.identity * 0.9f);
            float radius = CurrentRadius * breath;

            //消散段荧光残点：雾体退去时浮出，活到实体终点（余韵）
            if (dry > 0.1f) {
                float emberIn = MathHelper.Clamp((dry - 0.1f) / 0.25f, 0f, 1f);
                float emberOut = MathHelper.Clamp((1f - dry) / 0.12f, 0f, 1f);
                float twinkleTime = time * 5f;
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

            //中央雾体（真 alpha 乘环境光，承担遮挡感）
            float bodyScale = radius * 1.5f / fog.Width;
            Main.EntitySpriteDraw(fog, center, null, DeepSpore * (0.75f * deepA * LightK(lightColor)),
                Projectile.identity * 0.7f, fogOrigin, bodyScale, SpriteEffects.None, 0);

            //雾团：黄金角螺旋铺排（内密外稀），内圈回旋快外圈慢，逐团乘环境光；
            //边缘稀薄可读，对应判定宽限带
            for (int i = 0; i < PuffCount; i++) {
                float frac = (i + 0.5f) / PuffCount;
                float hS = Hash(i, 3);
                float ang = i * 2.39996f + time * (0.08f + 0.18f * (1f - frac));
                float r = radius * 0.86f * MathF.Sqrt(frac);
                Vector2 world = Projectile.Center + ang.ToRotationVector2() * r;
                float edgeThin = 1f - 0.55f * MathHelper.Clamp((r / radius - 0.5f) / 0.5f, 0f, 1f);
                float puffScale = (0.24f + 0.14f * hS) * (radius / MaxRadius);
                float rot = hS * MathHelper.TwoPi + time * (hS - 0.5f) * 0.5f;
                Color lit = Lighting.GetColor((int)(world.X / 16f), (int)(world.Y / 16f));
                Main.EntitySpriteDraw(fog, world - Main.screenPosition, null,
                    DeepSpore * (deepA * edgeThin * LightK(lit)), rot, fogOrigin, puffScale,
                    hS > 0.5f ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            }

            //悬浮发光孢子点（加法点缀，光本体）：成形期渐亮=自持预告的视觉通道
            float dotA = (0.3f + 0.7f * ArmProgress) * fade;
            for (int i = 0; i < SporeDotCount; i++) {
                float hA = Hash(i, 5);
                float hR = Hash(i, 6);
                float hB = Hash(i, 7);
                float orbit = hA * MathHelper.TwoPi + time * (0.14f + 0.22f * hB) * (hB > 0.5f ? 1f : -1f);
                float rr = radius * (0.12f + 0.7f * hR);
                float bob = MathF.Sin(time * (1.1f + hB) + i * 1.9f) * radius * 0.05f;
                Vector2 pos = center + orbit.ToRotationVector2() * rr + new Vector2(0f, bob);
                float twinkle = 0.55f + 0.45f * MathF.Sin(time * (2.3f + hB * 1.4f) + i * 2.7f);
                Main.EntitySpriteDraw(glow, pos, null, BrightSpore with { A = 0 } * (0.4f * dotA * twinkle),
                    0f, glowOrigin, 0.12f + 0.08f * hB, SpriteEffects.None, 0);
                if (twinkle > 0.9f) {
                    //孢子点偶发星芒眨眼
                    Main.EntitySpriteDraw(star, pos, null,
                        BrightSpore with { A = 0 } * (0.5f * dotA * (twinkle - 0.9f) * 10f),
                        hA * 4f, starOrigin, 0.03f + 0.02f * hB, SpriteEffects.None, 0);
                }
            }
            return false;
        }

        /// <summary>环境光系数（乘环境光；微弱孢光下限防全黑）</summary>
        private static float LightK(Color lit)
            => LightFloor + (1f - LightFloor) * ((lit.R + lit.G + lit.B) / 765f);

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
