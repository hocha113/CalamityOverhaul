using CalamityOverhaul.Content.Items.Stones;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Stonewake.Projectiles
{
    /// <summary>
    /// 共振脉冲环（花岗岩厅）。生成位置即晶簇锚点，无 ai 参数。<br/>
    /// 预告：晶簇自地表长出充能发亮+蜂鸣逐拍升调（52 帧，公平契约 ≥45）；<br/>
    /// 落地：可见能量环自晶簇外扩，环过身且玩家接地则施加短暂原版缓速。
    /// 电流走地：脚离地便不成回路，跳跃时机可跨环（具名逃生阀门）；<br/>
    /// 余韵：环至最大半径消散，晶簇余辉冷却。纯控制领域，恒无伤害
    /// </summary>
    internal class StonewakeResonanceRingProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>充能预告帧数（公平契约 ≥45，各档位一律不缩短）</summary>
        private const int ChargeFrames = 52;
        /// <summary>环扩速度 px/帧</summary>
        private const float ExpandSpeed = 6.5f;
        /// <summary>环最大半径（档位只调频率，不调形状）</summary>
        private const float MaxRadius = 440f;
        private const int ExpandFrames = 68;
        private const int FadeFrames = 14;
        /// <summary>判定带半宽：可见环缘即判定带</summary>
        private const float BandHalf = 13f;
        /// <summary>缓速时长（短暂，原版 Slow）</summary>
        private const int SlowFrames = 75;
        /// <summary>升调蜂鸣拍间隔</summary>
        private const int ZapBeat = 13;

        private int TotalLife => ChargeFrames + ExpandFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        /// <summary>当前环半径（扩张期线性外推，消散期钉在最大值）</summary>
        private float Radius {
            get {
                int t = Elapsed - ChargeFrames;
                if (t <= 0) {
                    return 0f;
                }
                return MathF.Min(t * ExpandSpeed, MaxRadius);
            }
        }

        /// <summary>消散淡出 1→0</summary>
        private float FadeFactor {
            get {
                int t = Elapsed - ChargeFrames - ExpandFrames;
                if (t <= 0) {
                    return 1f;
                }
                return MathHelper.Clamp(1f - t / (float)FadeFrames, 0f, 1f);
            }
        }

        /// <summary>缓速反馈的本地节流（纯客户端演出量）</summary>
        private ref float SlowFxGate => ref Projectile.localAI[0];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 520;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = false;//纯控制领域，恒无伤害
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = ChargeFrames + ExpandFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;
            if (SlowFxGate > 0f) {
                SlowFxGate--;
            }

            //==== 判定：环带扫过接地玩家（各端只裁决本机玩家，AddBuff 原生同步） ====
            if (!Main.dedServ && elapsed >= ChargeFrames && FadeFactor >= 1f && !CWRWorld.HasBoss) {
                Player local = Main.LocalPlayer;
                if (local.active && !local.dead
                    && Math.Abs(local.Distance(Projectile.Center) - Radius) < BandHalf + 20f
                    && GraniteMarbleVFX.IsGrounded(local)) {
                    local.AddBuff(BuffID.Slow, SlowFrames);
                    if (SlowFxGate <= 0f) {
                        SlowFxGate = 30f;
                        SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with {
                            Volume = 0.55f,
                            Pitch = -0.1f,
                            MaxInstances = 3,
                        }, local.Center);
                        for (int i = 0; i < 3; i++) {
                            PRTLoader.NewParticle<PRT_GraniteVolt>(local.Bottom + new Vector2(Main.rand.NextFloat(-12f, 12f), -4f),
                                Main.rand.NextVector2Unit() * 1.6f, StonewakeFX.GraniteSpark,
                                Main.rand.NextFloat(0.26f, 0.4f)).Configure(Main.rand.Next(3, 6));
                        }
                    }
                }
            }

            if (Main.dedServ) {
                return;
            }

            //==== 预告期：晶簇充能，蜂鸣逐拍升调 ====
            if (elapsed < ChargeFrames) {
                float progress = elapsed / (float)ChargeFrames;
                if (elapsed % ZapBeat == 0) {
                    int beat = elapsed / ZapBeat;
                    SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with {
                        Volume = 0.30f + 0.09f * beat,
                        Pitch = -0.45f + 0.28f * beat,
                        MaxInstances = 4,
                    }, Projectile.Center);
                }
                //晶簇周身电火花（≤1 粒/帧）
                if (Main.rand.NextBool(2)) {
                    Dust spark = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-16f, 16f), -Main.rand.NextFloat(0f, 22f) * progress),
                        DustID.Electric, new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.2f)), 120, default, 0.7f);
                    spark.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center - Vector2.UnitY * 10f,
                    StonewakeFX.GraniteCore.ToVector3() * (0.25f + 0.75f * progress));
                return;
            }

            //==== 释放帧：脉冲成环 ====
            if (elapsed == ChargeFrames) {
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Volume = 0.9f, Pitch = -0.15f, MaxInstances = 4 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 4 }, Projectile.Center);
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center - Vector2.UnitY * 8f, Vector2.Zero,
                    StonewakeFX.GraniteSpark, 0.3f).Configure(14, 0.9f);
                for (int i = 0; i < 6; i++) {
                    float ang = MathHelper.TwoPi * i / 6f;
                    PRTLoader.NewParticle<PRT_GraniteVolt>(Projectile.Center + ang.ToRotationVector2() * 10f,
                        ang.ToRotationVector2() * 2.6f, StonewakeFX.GraniteCore,
                        Main.rand.NextFloat(0.3f, 0.45f)).Configure(Main.rand.Next(4, 6));
                }
            }

            //==== 扩张期：环缘噼啪，贴地处电流更旺（教会玩家"电流走地"） ====
            if (elapsed < ChargeFrames + ExpandFrames) {
                if (elapsed % 3 == 0) {
                    for (int i = 0; i < 3; i++) {
                        float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                        Vector2 rim = Projectile.Center + ang.ToRotationVector2() * Radius;
                        bool nearGround = Collision.SolidCollision(rim - new Vector2(12f), 24, 24);
                        if (!nearGround && !Main.rand.NextBool(4)) {
                            continue;//离地环段只留零星火花，贴地环段火花更旺
                        }
                        PRTLoader.NewParticle<PRT_GraniteVolt>(rim, ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 1.8f,
                            nearGround ? StonewakeFX.GraniteSpark : StonewakeFX.GraniteDeep,
                            Main.rand.NextFloat(0.24f, nearGround ? 0.42f : 0.3f)).Configure(Main.rand.Next(3, 6));
                        if (i >= 1) {
                            break;//每 3 帧至多 2 粒，控预算
                        }
                    }
                }
                if (elapsed % 16 == 0) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with {
                        Volume = 0.26f,
                        Pitch = Main.rand.NextFloat(-0.1f, 0.3f),
                        MaxInstances = 3,
                    }, Projectile.Center + ang.ToRotationVector2() * Radius);
                }
            }
            Lighting.AddLight(Projectile.Center, StonewakeFX.GraniteCore.ToVector3() * 0.5f * FadeFactor);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D ring = CWRAsset.DiffusionCircle.Value;
            Texture2D line = CWRAsset.Line.Value;
            Vector2 center = Projectile.Center - Main.screenPosition;

            //加色敷料染色（A=0）：只给扩散环、缘光、芯光这类"本身是光"的层
            Color deep = StonewakeFX.GraniteDeep; deep.A = 0;
            Color core = StonewakeFX.GraniteCore; core.A = 0;
            Color sparkTint = StonewakeFX.GraniteSpark; sparkTint.A = 0;
            //晶体实体层染色（A>0 真 alpha）：花岗岩色系暗底
            Color graniteDark = new(44, 50, 82);

            //==== 晶簇：花岗岩暗底晶体+电蓝亮缘，充能期长出，释放后随扩张回落（余韵冷却） ====
            float chargeProgress = MathHelper.Clamp(elapsed / (float)ChargeFrames, 0f, 1f);
            float crystalScale = elapsed < ChargeFrames
                ? 0.35f + 0.65f * chargeProgress
                : MathHelper.Clamp(1f - (elapsed - ChargeFrames) / (float)(ExpandFrames + FadeFrames), 0.25f, 1f);
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * (6f + 14f * chargeProgress) + Projectile.identity);
            float crystalGlowK = elapsed < ChargeFrames ? chargeProgress * pulse : 0.5f * FadeFactor * pulse;
            //实体不随脉冲呼吸：充能早期即凝实，尾段随消散淡出
            float crystalBodyK = elapsed < ChargeFrames
                ? MathF.Min(1f, 0.3f + chargeProgress * 1.1f) : FadeFactor;

            //原版巨鹿冰刺贴图作晶柱体（换构图：五根扇排自地面立起，花岗岩暗色重染）
            Main.instance.LoadProjectile(ProjectileID.DeerclopsIceSpike);
            Texture2D spike = TextureAssets.Projectile[ProjectileID.DeerclopsIceSpike].Value;

            //接地暗座（真 alpha）：晶簇底部的落地压暗
            Main.EntitySpriteDraw(ring, center + new Vector2(0f, 2f), null,
                graniteDark * (0.45f * crystalBodyK), 0f, ring.Size() / 2f,
                new Vector2(0.17f, 0.05f) * crystalScale, SpriteEffects.None, 0);
            //底部辉光垫
            Main.EntitySpriteDraw(glow, center, null, deep * (0.5f * crystalGlowK), 0f,
                glow.Size() / 2f, new Vector2(1.1f, 0.5f) * crystalScale, SpriteEffects.None, 0);

            //五根扇排晶柱：中间最高向两侧递减外倾，逐根取不同帧异形（确定性微颤）
            ReadOnlySpan<float> fan = [-0.52f, -0.26f, 0f, 0.26f, 0.52f];
            ReadOnlySpan<float> tall = [0.55f, 0.78f, 1f, 0.78f, 0.55f];
            for (int i = 0; i < 5; i++) {
                Rectangle rect = spike.Frame(1, 5, 0, (Projectile.identity + i) % 5);
                float axisLen = MathF.Max(rect.Width - 18f, 40f);
                float wob = MathF.Sin(Projectile.identity * 1.7f + i * 2.3f) * 0.06f;
                float rot = -MathHelper.PiOver2 + fan[i] * 0.8f + wob;
                float len = (18f + 30f * tall[i]) * crystalScale;
                Vector2 rootPos = center + new Vector2(fan[i] * 42f * crystalScale, 2f);
                Vector2 scale = new(len / axisLen, 0.55f * crystalScale);
                Vector2 orig = new(16f, rect.Height / 2f);
                SpriteEffects flip = ((Projectile.identity + i) & 1) == 0
                    ? SpriteEffects.None : SpriteEffects.FlipVertically;
                //电蓝亮缘：略大一号垫底（A=0 加色，只露边缘）
                Main.EntitySpriteDraw(spike, rootPos, rect, core * ((0.3f + 0.6f * crystalGlowK) * crystalBodyK),
                    rot, orig, scale * 1.16f, flip, 0);
                //花岗岩暗底晶体（A>0 实体，撑起剪影与遮挡）
                Main.EntitySpriteDraw(spike, rootPos, rect, graniteDark * (0.92f * crystalBodyK),
                    rot, orig, scale, flip, 0);
                //晶面电光（A=0，充能越满越亮）
                Main.EntitySpriteDraw(spike, rootPos, rect, sparkTint * (0.45f * crystalGlowK),
                    rot, orig, scale * 0.82f, flip, 0);
            }
            //充能芯光
            Main.EntitySpriteDraw(glow, center - new Vector2(0f, 8f * crystalScale), null,
                sparkTint * (0.75f * crystalGlowK), 0f, glow.Size() / 2f, 0.5f * crystalScale * pulse, SpriteEffects.None, 0);

            //==== 能量环：可见环缘=判定带 ====
            float radius = Radius;
            if (radius > 4f) {
                float fade = FadeFactor;
                float ringScale = radius / (ring.Width * 0.5f);
                //双层反向慢旋：能量环的旋转运动线索
                float ringSpin = Main.GlobalTimeWrappedHourly * 0.9f + Projectile.identity;
                Main.EntitySpriteDraw(ring, center, null, core * (0.6f * fade), ringSpin,
                    ring.Size() / 2f, ringScale, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(ring, center, null, sparkTint * (0.35f * fade), -ringSpin * 0.6f,
                    ring.Size() / 2f, ringScale * 0.96f, SpriteEffects.None, 0);

                //环缘游走的切向电痕
                float spin = Main.GlobalTimeWrappedHourly * 1.4f + Projectile.identity;
                const int ticks = 12;
                for (int i = 0; i < ticks; i++) {
                    float ang = spin + MathHelper.TwoPi * i / ticks;
                    Vector2 rim = center + ang.ToRotationVector2() * radius;
                    float tickLen = 26f / line.Height;
                    Main.EntitySpriteDraw(line, rim, null, core * (0.7f * fade), ang, line.Size() / 2f,
                        new Vector2(0.05f, tickLen), SpriteEffects.None, 0);
                    //Line 是黑底贴图：染色 A 必须为 0，A>0 会把黑底当半透明暗矩形画上屏
                    Main.EntitySpriteDraw(line, rim, null, new Color(255, 255, 255, 0) * (0.35f * fade), ang, line.Size() / 2f,
                        new Vector2(0.025f, tickLen * 0.7f), SpriteEffects.None, 0);
                }
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //晶簇熄灭的最后几粒火花
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_GraniteShard>(Projectile.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), -4f),
                    new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1f, 2.5f)),
                    StonewakeFX.GraniteCore, Main.rand.NextFloat(0.35f, 0.55f)).Configure(Main.rand.Next(20, 30));
            }
        }
    }
}
