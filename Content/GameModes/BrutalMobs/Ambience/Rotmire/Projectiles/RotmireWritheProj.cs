using CalamityOverhaul.Content.GameModes.BrutalMobs.EvilBiome;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Rotmire.Projectiles
{
    /// <summary>
    /// 「邪土蠕动」土包。ai[0]=会爆旗标（出生时权威端掷定，随生成包同步）ai[1]=体型。
    /// 时间线：拱起 36 帧 → 蠕动 76~123 帧（时长由 identity 哈希取定，各端一致）→
    /// 会爆者膨大变色 50 帧（充分预告：鼓大 1.5 倍+转亮色+湿气与加速气泡声）→ 爆孢 8 帧微伤 → 余孢消散；
    /// 不会爆者静默消退。多数静默、少数爆开，制造"哪个会爆"的紧张感。
    /// 纯视觉阶段无任何判定；全程由 timeLeft 确定性推演，无追加同步
    /// </summary>
    internal class RotmireWritheProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int RiseFrames = 36;
        /// <summary>蠕动基础时长；实际 = 基础 + identity 哈希 0~47（各端一致的确定性变奏）</summary>
        private const int WritheBaseFrames = 76;
        /// <summary>膨大变色预告（公平契约 ≥45）</summary>
        private const int SwellFrames = 50;
        /// <summary>爆孢判定窗</summary>
        private const int BurstFrames = 8;
        /// <summary>余孢消散</summary>
        private const int LingerFrames = 32;
        /// <summary>静默消退</summary>
        private const int SubsideFrames = 26;
        /// <summary>寿命预算上限（覆盖最长路径 36+123+50+8+32=249）</summary>
        private const int MaxLife = 250;
        /// <summary>爆孢判定半径（×体型）</summary>
        private const float BurstRadius = 78f;

        private static readonly Color SoilDeep = new(74, 56, 66);
        private static readonly Color GasDeep = EvilBiomeFX.Deep(EvilBiomeFX.FlavorCorrupt);
        private static readonly Color GasBright = EvilBiomeFX.Bright(EvilBiomeFX.FlavorCorrupt);

        private bool WillBurst => Projectile.ai[0] == 1f;
        private float Scale => Projectile.ai[1];
        private int Elapsed => MaxLife - Projectile.timeLeft;

        /// <summary>蠕动时长：identity 哈希变奏，跨端一致</summary>
        private int WritheFrames => WritheBaseFrames + (int)(Projectile.identity * 2654435761u % 48u);

        private int WritheEnd => RiseFrames + WritheFrames;
        private int SwellEnd => WritheEnd + SwellFrames;
        private int BurstEnd => SwellEnd + BurstFrames;

        /// <summary>拱起 0~1</summary>
        private float RiseProgress {
            get {
                int t = Elapsed;
                if (t >= RiseFrames) {
                    return 1f;
                }
                float x = t / (float)RiseFrames;
                return x * x * (3f - 2f * x);
            }
        }

        /// <summary>膨大预告 0~1（仅会爆者）</summary>
        private float SwellT {
            get {
                int t = Elapsed - WritheEnd;
                return t <= 0 ? 0f : MathHelper.Clamp(t / (float)SwellFrames, 0f, 1f);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 200;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;//仅爆孢窗置真
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLife;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;

            //阶段收尾：各端由同步的 ai/identity 确定性同帧收场
            if (WillBurst) {
                if (elapsed >= BurstEnd + LingerFrames) {
                    Projectile.Kill();
                    return;
                }
            }
            else if (elapsed >= WritheEnd + SubsideFrames) {
                Projectile.Kill();
                return;
            }

            //判定窗：仅会爆者的爆孢 8 帧；Boss 登场瞬间已在场土包一并缴械（视觉走完）
            Projectile.hostile = GameModeSystem.BrutalActive && !CWRWorld.HasBoss
                && WillBurst && elapsed >= SwellEnd && elapsed < BurstEnd;

            if (Main.dedServ) {
                return;
            }

            if (elapsed == 1) {
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.28f, Pitch = -0.6f, MaxInstances = 4 },
                    Projectile.Center);
            }

            if (elapsed < RiseFrames) {
                //拱起：土屑被顶开
                if (Main.rand.NextBool(2)) {
                    Dust dirt = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-30f, 30f) * Scale, 0f),
                        DustID.Dirt, new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(0.8f, 2f)),
                        60, default, Main.rand.NextFloat(0.9f, 1.4f));
                    dirt.noGravity = false;
                }
                return;
            }

            if (elapsed < WritheEnd) {
                //蠕动：低频土屑与闷闷的翻土声
                if ((elapsed + Projectile.identity * 7) % 52 == 0) {
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.16f, Pitch = -0.72f, MaxInstances = 4 },
                        Projectile.Center);
                }
                if (Main.rand.NextBool(4)) {
                    Dust crumb = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-26f, 26f) * Scale, -4f),
                        DustID.CorruptGibs, new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.9f)),
                        130, default, Main.rand.NextFloat(0.6f, 1f));
                    crumb.noGravity = true;
                }
                return;
            }

            if (!WillBurst) {
                //静默消退：土屑塌落
                if (Main.rand.NextBool(3)) {
                    Dust settle = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-24f, 24f) * Scale, -4f),
                        DustID.Dirt, new Vector2(0f, Main.rand.NextFloat(0.4f, 1.1f)),
                        80, default, Main.rand.NextFloat(0.7f, 1.1f));
                    settle.noGravity = false;
                }
                if (elapsed == WritheEnd + 1) {
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.18f, Pitch = -0.75f, MaxInstances = 4 },
                        Projectile.Center);
                }
                return;
            }

            //==== 会爆者 ====
            if (elapsed < SwellEnd) {
                int t = elapsed - WritheEnd;
                if (t == 1) {
                    //膨大起手：湿气翻涌（听觉预告通道）。禁 Zombie104，那是死光起手
                    SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.45f, Pitch = -0.4f, MaxInstances = 3 },
                        Projectile.Center);
                }
                //加速的气泡声：预告后半拍点越来越密
                int interval = 16 - (int)(SwellT * 10f);
                if (t % Math.Max(interval, 5) == 0) {
                    SoundEngine.PlaySound(SoundID.NPCHit1 with {
                        Volume = 0.2f + 0.12f * SwellT,
                        Pitch = -0.5f + 0.2f * SwellT,
                        MaxInstances = 4
                    }, Projectile.Center);
                }
                if (Main.rand.NextBool(2)) {
                    Dust spore = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-30f, 30f) * Scale,
                            -Main.rand.NextFloat(4f, 18f) * Scale),
                        EvilBiomeFX.DustFor(EvilBiomeFX.FlavorCorrupt),
                        new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.6f)),
                        120, default, Main.rand.NextFloat(0.8f, 1.2f));
                    spore.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center - new Vector2(0f, 10f),
                    new Vector3(0.14f, 0.2f, 0.08f) * SwellT);
                return;
            }

            if (elapsed == SwellEnd) {
                //爆孢帧
                SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.65f, Pitch = -0.35f, MaxInstances = 3 },
                    Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.4f, Pitch = -0.3f, MaxInstances = 3 },
                    Projectile.Center);
                for (int i = 0; i < 16; i++) {
                    float ang = MathHelper.TwoPi * i / 16f;
                    int dustType = i % 5 == 0 ? DustID.Shadowflame
                        : i % 4 == 0 ? DustID.CorruptGibs
                        : EvilBiomeFX.DustFor(EvilBiomeFX.FlavorCorrupt);
                    Dust burst = Dust.NewDustPerfect(Projectile.Center - new Vector2(0f, 10f * Scale),
                        dustType, ang.ToRotationVector2() * Main.rand.NextFloat(1.5f, 4.5f)
                            - new Vector2(0f, 1.5f),
                        110, default, Main.rand.NextFloat(1f, 1.6f));
                    burst.noGravity = true;
                }
                return;
            }

            //余孢消散：残雾缓升
            if (elapsed >= BurstEnd && Main.rand.NextBool(3)) {
                Dust drift = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-40f, 40f) * Scale,
                        -Main.rand.NextFloat(4f, 30f)),
                    EvilBiomeFX.DustFor(EvilBiomeFX.FlavorCorrupt),
                    new Vector2(Main.windSpeedCurrent * 0.8f, -Main.rand.NextFloat(0.5f, 1.2f)),
                    150, default, Main.rand.NextFloat(0.7f, 1f));
                drift.noGravity = true;
            }
        }

        /// <summary>爆孢判定：土包心为圆心的短促放射（窗口由 hostile 门控）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            Vector2 center = Projectile.Center - new Vector2(0f, 14f * Scale);
            float radius = BurstRadius * Scale;
            Vector2 closest = new(
                MathHelper.Clamp(center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(center, closest) <= radius * radius;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D fog = CWRAsset.Fog?.Value;
            if (fog == null || fog.IsDisposed) {
                return false;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Vector2 fogOrigin = fog.Size() * 0.5f;
            int elapsed = Elapsed;
            float time = Main.GlobalTimeWrappedHourly;
            float seed = Projectile.identity * 2.13f;

            float rise = RiseProgress;
            float swell = WillBurst ? SwellT : 0f;
            bool burstDone = WillBurst && elapsed >= SwellEnd;

            //消退/余孢期的整体淡出
            float fadeOut = 1f;
            if (!WillBurst && elapsed > WritheEnd) {
                fadeOut = 1f - MathHelper.Clamp((elapsed - WritheEnd) / (float)SubsideFrames, 0f, 1f);
            }
            else if (burstDone) {
                fadeOut = 1f - MathHelper.Clamp((elapsed - SwellEnd) / (float)(BurstFrames + LingerFrames), 0f, 1f);
            }
            if (fadeOut <= 0.01f) {
                return false;
            }

            if (!burstDone) {
                //蠕动呼吸：膨大期频率与幅度同步上抬（视觉预告通道）
                float freq = 4.5f + swell * 9f;
                float breath = MathF.Sin(time * freq + seed) * (0.1f + 0.1f * swell);
                float bulge = 1f + swell * 0.5f;
                float widthPx = 92f * Scale * bulge;
                float heightPx = 34f * Scale * rise * bulge * (1f + breath) * fadeOut;

                //双瓣土包错相蠕动，读作皮下有东西在爬
                for (int i = 0; i < 2; i++) {
                    float sway = MathF.Sin(time * freq * 0.8f + seed + i * 2.7f) * 4f;
                    float side = i == 0 ? -12f : 12f;
                    Vector2 pos = Projectile.Center
                        + new Vector2(side * Scale + sway, -heightPx * 0.4f) - Main.screenPosition;
                    //土色向病态亮色渐变 = 变色预告
                    Color soil = Color.Lerp(Color.Lerp(lightColor, SoilDeep, 0.6f),
                        Color.Lerp(GasDeep, GasBright, 0.45f), swell * 0.55f);
                    Main.EntitySpriteDraw(fog, pos, null, soil * (0.72f * rise * fadeOut),
                        time * 0.1f + i * 1.3f + seed,
                        fogOrigin, new Vector2(widthPx * 0.62f / fog.Width, heightPx / fog.Width),
                        SpriteEffects.None, 0);
                }
                //膨大期的孢光边缘（A=0 加色，脉动加快）
                if (swell > 0.01f && glow != null && !glow.IsDisposed) {
                    float pulse = 0.55f + 0.45f * MathF.Sin(time * (8f + swell * 10f) + seed);
                    Color rim = new Color(GasBright.R, GasBright.G, GasBright.B, (byte)0)
                        * (0.3f * swell * pulse);
                    Main.EntitySpriteDraw(glow, Projectile.Center - new Vector2(0f, heightPx * 0.4f)
                        - Main.screenPosition, null, rim, 0f, glow.Size() * 0.5f,
                        new Vector2(1.1f * Scale * bulge, 0.55f * bulge), SpriteEffects.None, 0);
                }
                return false;
            }

            //爆孢与余孢：扩张孢雾环 + 闪帧
            float boom = MathHelper.Clamp((elapsed - SwellEnd) / (float)(BurstFrames + LingerFrames), 0f, 1f);
            float ringPx = (40f + 90f * boom) * Scale;
            Color cloud = Color.Lerp(GasDeep, Color.Black, 0.2f) * (0.5f * fadeOut);
            for (int i = 0; i < 3; i++) {
                Vector2 pos = Projectile.Center + new Vector2(
                    MathF.Sin(seed + i * 2.1f) * ringPx * 0.5f,
                    -10f * Scale - boom * (26f + i * 12f)) - Main.screenPosition;
                Main.EntitySpriteDraw(fog, pos, null, cloud, time * 0.4f + i, fogOrigin,
                    (ringPx * 0.9f + i * 8f) / fog.Width, SpriteEffects.None, 0);
            }
            if (glow != null && !glow.IsDisposed && elapsed < SwellEnd + 6) {
                //爆帧闪光（≤6 帧的过曝脉冲）
                float flash = 1f - (elapsed - SwellEnd) / 6f;
                Color flashColor = new Color(GasBright.R, GasBright.G, GasBright.B, (byte)0) * (0.55f * flash);
                Main.EntitySpriteDraw(glow, Projectile.Center - new Vector2(0f, 12f * Scale)
                    - Main.screenPosition, null, flashColor, 0f, glow.Size() * 0.5f,
                    1.6f * Scale, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
