using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Astralveil.Projectiles
{
    /// <summary>
    /// 「星辉矛」点名坠星。ai[0]=感染绽放存续帧（转交绽放实体），ai[1]=被点名者 whoAmI+1（个人预警提亮）。
    /// 生成位置即锁定圈心（预告即承诺，圈固定不追踪）：
    /// 晶尘圈+升调蜂鸣预告 70 帧 → 星辉晶矛自天纵落 14 帧（加速下坠，无判定）→
    /// 触地 6 帧（仅圈内低矮判定板有伤害，跳离或走出即免）→ 星尘飞溅、碎晶余韵渐次黯灭
    /// 并交棒「感染绽放」。一切相位由 timeLeft 确定性推导，各端自算；伤害恒满值，判定窗只走 hostile 开关
    /// </summary>
    internal class AstralveilSpearMarkProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>星矛触地伤害（经典模式基线：星辉群系怪接触伤中位 50 × 0.5，取白金星舰前口径——
        /// 灾厄源码 NPCs/Astral 各怪 SetDefaults 中位 50，击败白金星舰后才升至 ~85；困难度加成由原版结算）</summary>
        internal const int ImpactDamage = 25;

        /// <summary>预告帧数（公平契约 ≥45，从宽给足；各档位一律不缩短）</summary>
        private const int TelegraphFrames = 70;
        /// <summary>星矛下坠帧数（加速坠落）</summary>
        private const int FallFrames = 14;
        /// <summary>触地判定帧数（判定窗=触地闪光窗）</summary>
        private const int ImpactFrames = 6;
        /// <summary>星纹圈与碎晶余韵退场帧数（消散而非删除）</summary>
        private const int FadeFrames = 26;
        /// <summary>星纹圈半径（约 6 格直径，预告期内足够走出）</summary>
        private const float CircleRadius = 96f;
        /// <summary>地面透视压扁比</summary>
        private const float GroundSquash = 0.42f;
        /// <summary>星矛下坠起始高度</summary>
        private const float FallHeight = 620f;
        /// <summary>触地判定板高度（低矮：跳起可免）</summary>
        private const float HitSlabHeight = 110f;
        /// <summary>圈缘晶粒数（自点阵收敛，晶尘物象化）</summary>
        private const int RingGrains = 14;

        /// <summary>暗橙晶体底色（真 alpha 暗层用，与暗靛交替成星辉双色）</summary>
        private static readonly Color EmberDeep = new(96, 52, 22);

        private int TotalLife => TelegraphFrames + FallFrames + ImpactFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;
        private int ImpactStart => TelegraphFrames + FallFrames;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;//触地窗口内才置真
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames + FallFrames + ImpactFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>星矛当前位置（加速下坠：位移走 t² 曲线，各端从相位自算）</summary>
        private Vector2 SpearPos(float fallT)
            => Projectile.Center - new Vector2(0f, FallHeight * (1f - fallT * fallT));

        public override void AI() {
            int elapsed = Elapsed;

            //触地帧：权威端把落点交给「感染绽放」（ai[0] 携档位化存续，随生成包同步）
            if (elapsed == ImpactStart && Main.netMode != NetmodeID.MultiplayerClient) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<AstralveilBloomProj>(), AstralveilBloomProj.TickDamage, 0f,
                    Main.myPlayer, Projectile.ai[0]);
            }

            //判定窗=触地闪光窗；中途关残酷模式或 Boss 在场时伤害层让位（命中在受害者本机裁决，本地读世界旗标即可）
            Projectile.hostile = GameModeSystem.BrutalActive
                && elapsed >= ImpactStart && elapsed < ImpactStart + ImpactFrames
                && !CWRWorld.HasBoss;

            if (Main.dedServ) {
                return;
            }

            //升调蜂鸣：四记水晶震音逐步抬高音调（听觉预告通道，与星纹圈双通道并行）
            if (elapsed == 0 || elapsed == 24 || elapsed == 48 || elapsed == 66) {
                float rise = elapsed / 66f;
                SoundEngine.PlaySound(SoundID.MaxMana with {
                    Volume = 0.40f + 0.22f * rise,
                    Pitch = -0.35f + 0.9f * rise,
                    MaxInstances = 4,
                }, Projectile.Center);
            }

            if (elapsed < TelegraphFrames) {
                //预告期：圈缘星屑向圈心收拢（≤1 粒/3 帧）
                if (Main.rand.NextBool(3)) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 edge = Projectile.Center + new Vector2(
                        MathF.Cos(ang) * CircleRadius, MathF.Sin(ang) * CircleRadius * GroundSquash - 6f);
                    bool indigo = Main.rand.NextFloat() < AstralveilFX.IndigoFraction;
                    Dust dust = Dust.NewDustPerfect(edge, AstralveilFX.DustFor(indigo),
                        (Projectile.Center - edge) * 0.02f - new Vector2(0f, 0.3f), 150, default, 0.9f);
                    dust.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, new Vector3(0.14f, 0.12f, 0.30f) * (elapsed / (float)TelegraphFrames));
                return;
            }

            if (elapsed == TelegraphFrames) {
                //提交帧：坠星哨音（星星坠落的原版签名音）
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.85f, Pitch = -0.05f, MaxInstances = 4 },
                    Projectile.Center);
            }

            if (elapsed < ImpactStart) {
                //坠落期：矛身沿途甩星屑（2 粒/帧，短窗口）
                float fallT = (elapsed - TelegraphFrames) / (float)FallFrames;
                Vector2 spearPos = SpearPos(fallT);
                for (int i = 0; i < 2; i++) {
                    bool indigo = i == 0;
                    Dust dust = Dust.NewDustPerfect(
                        spearPos + new Vector2(Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(-30f, 10f)),
                        AstralveilFX.DustFor(indigo),
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.2f, 0.9f)),
                        130, default, Main.rand.NextFloat(0.9f, 1.3f));
                    dust.noGravity = true;
                }
                Lighting.AddLight(spearPos, new Vector3(0.30f, 0.24f, 0.55f));
                return;
            }

            if (elapsed == ImpactStart) {
                //触地帧：晶裂脆响 + 星尘飞溅（径向 18 + 上扬 6）
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.9f, Pitch = -0.2f, MaxInstances = 4 },
                    Projectile.Center);
                for (int i = 0; i < 18; i++) {
                    float ang = MathHelper.TwoPi * i / 18f;
                    bool indigo = (i & 1) == 0;
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, AstralveilFX.DustFor(indigo),
                        new Vector2(MathF.Cos(ang), MathF.Sin(ang) * 0.5f - 0.4f) * Main.rand.NextFloat(2.4f, 5.2f),
                        110, default, Main.rand.NextFloat(1.1f, 1.6f));
                    dust.noGravity = Main.rand.NextBool();
                }
                for (int i = 0; i < 6; i++) {
                    Dust dust = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-CircleRadius, CircleRadius) * 0.6f, -4f),
                        AstralveilFX.DustFor(Main.rand.NextBool()),
                        new Vector2(0f, -Main.rand.NextFloat(1.6f, 3.4f)), 120, default, 1.2f);
                    dust.noGravity = true;
                }
            }

            float sinceHit = elapsed - ImpactStart;
            Lighting.AddLight(Projectile.Center,
                new Vector3(0.7f, 0.55f, 1.0f) * MathHelper.Clamp(1f - sinceHit / 10f, 0f, 1f));
        }

        /// <summary>触地判定：圈内低矮判定板（宽=圈径、高 110px），跳离或走出皆可免</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            Rectangle slab = new(
                (int)(Projectile.Center.X - CircleRadius),
                (int)(Projectile.Center.Y - HitSlabHeight),
                (int)(CircleRadius * 2f), (int)HitSlabHeight + 8);
            return slab.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            //黑底四芒星只做 A=0 加色星光；暗晶体形状由真 alpha 白星与 Extra_98 梭形承担（A>0 才能遮挡）
            Texture2D star = CWRAsset.StarTexture.Value;
            Texture2D starWhite = CWRAsset.StarTexture_White.Value;
            Texture2D pad = CWRAsset.Extra_98.Value;
            if (glow == null || star == null || starWhite == null || pad == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            Vector2 glowOrig = glow.Size() * 0.5f;
            Vector2 starOrig = star.Size() * 0.5f;
            Vector2 starWhiteOrig = starWhite.Size() * 0.5f;
            Vector2 padOrig = pad.Size() * 0.5f;
            float phase = Projectile.identity * 1.37f;

            //圈的浮现与退场包络
            float ringIn = Math.Min(elapsed / 14f, 1f);
            float ringOut = elapsed <= ImpactStart + ImpactFrames ? 1f
                : MathHelper.Clamp(1f - (elapsed - ImpactStart - ImpactFrames) / (float)FadeFrames, 0f, 1f);
            float ringAlpha = ringIn * ringOut;
            float progress = Math.Min(elapsed / (float)TelegraphFrames, 1f);
            //被点名者本机看到的圈略微提亮（个人预警，纯本地表现）
            float selfBoost = (int)Projectile.ai[1] - 1 == Main.myPlayer ? 1.15f : 1f;

            //===== 晶尘圈 =====
            //地面暗垫（真 alpha 透镜形，加色批压不出暗色，只有真 alpha 能承担）
            Main.EntitySpriteDraw(pad, center, null, AstralveilFX.IndigoDeep * (0.34f * ringAlpha),
                MathHelper.PiOver2, padOrig,
                new Vector2(CircleRadius * 0.9f / 24f, CircleRadius * 2.3f / 42f), SpriteEffects.None, 0);

            //圈缘晶尘：星辉晶粒沿圈缘缓旋列阵（暗芯真 alpha 承遮挡+晶尖加色微光），
            //靛/橙交替成星辉双色，进度越深脉动越快（紧迫感读数）
            float pulse = 0.72f + 0.28f * MathF.Sin(elapsed * (0.10f + 0.16f * progress) + phase);
            float spin = phase + elapsed * 0.012f;
            for (int i = 0; i < RingGrains; i++) {
                float ang = spin + MathHelper.TwoPi * i / RingGrains;
                float h = 0.5f + 0.5f * MathF.Sin(i * 2.7f + phase * 3f);
                Vector2 pos = center + new Vector2(
                    MathF.Cos(ang) * CircleRadius, MathF.Sin(ang) * CircleRadius * GroundSquash);
                float len = (10f + 8f * h) * (0.8f + 0.2f * progress);
                float lean = MathF.Cos(ang) * 0.38f + (h - 0.5f) * 0.24f;
                bool indigo = (i & 1) == 0;
                //晶粒暗芯（真 alpha 梭形，微微外倾如自地面析出）
                Main.EntitySpriteDraw(pad, pos + new Vector2(0f, -len * 0.28f), null,
                    (indigo ? AstralveilFX.IndigoDeep : EmberDeep) * ((0.55f + 0.35f * progress) * ringAlpha),
                    lean, padOrig, new Vector2(0.28f, len / 42f), SpriteEffects.None, 0);
                //晶尖微光（加色敷料，被点名者本机略提亮）
                Main.EntitySpriteDraw(glow,
                    pos + new Vector2(MathF.Sin(lean), -MathF.Cos(lean)) * (len * 0.45f), null,
                    AstralveilFX.A0(indigo ? AstralveilFX.Indigo : AstralveilFX.Orange)
                        * ((0.30f + 0.40f * progress) * pulse * ringAlpha * selfBoost),
                    0f, glowOrig, 0.10f, SpriteEffects.None, 0);
            }

            //五颗晶星反向缓旋（星图语汇：点名的"星座"落在你脚下）——
            //真 alpha 白星承暗晶体，黑底星只做亮缘星光；半径微差错落成星座而非正五边形
            for (int i = 0; i < 5; i++) {
                float ang = -spin * 1.6f + MathHelper.TwoPi * i / 5f;
                float rFrac = 0.72f + 0.07f * MathF.Sin(i * 2.4f + phase);
                Vector2 pos = center + new Vector2(
                    MathF.Cos(ang) * CircleRadius * rFrac, MathF.Sin(ang) * CircleRadius * rFrac * GroundSquash);
                float a = (0.38f + 0.40f * progress) * ringAlpha;
                Main.EntitySpriteDraw(starWhite, pos, null, AstralveilFX.IndigoDeep * (0.92f * a),
                    ang * 0.5f, starWhiteOrig, 0.075f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(star, pos, null,
                    AstralveilFX.A0(AstralveilFX.IndigoPale) * (0.55f * a * selfBoost),
                    ang * 0.5f, starOrig, 0.05f, SpriteEffects.None, 0);
            }

            //圈心晶种：随进度凝实的暗晶星+橙芯星光（星矛将在此结晶落地）
            Main.EntitySpriteDraw(starWhite, center + new Vector2(0f, -4f), null,
                AstralveilFX.IndigoDeep * (0.88f * progress * ringAlpha),
                spin * 2f, starWhiteOrig, 0.045f + 0.035f * progress, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, center + new Vector2(0f, -4f), null,
                AstralveilFX.A0(AstralveilFX.OrangePale) * (0.5f * progress * pulse * ringAlpha),
                spin * 2f, starOrig, 0.045f, SpriteEffects.None, 0);

            //===== 坠落星辉晶矛（暗晶剪影+星辉亮缘：光鞘垫底、真 alpha 晶体压上）=====
            if (elapsed >= TelegraphFrames && elapsed < ImpactStart) {
                float fallT = (elapsed - TelegraphFrames) / (float)FallFrames;
                //残影：纯光尾迹（运动拖影走加色合法）
                for (int k = 2; k >= 1; k--) {
                    float ghostT = fallT - k * 0.05f;
                    if (ghostT < 0f) {
                        continue;
                    }
                    float ghostAlpha = k == 1 ? 0.42f : 0.22f;
                    Vector2 gpos = SpearPos(ghostT) - Main.screenPosition;
                    Main.EntitySpriteDraw(glow, gpos, null,
                        AstralveilFX.A0(AstralveilFX.Indigo) * (0.60f * ghostAlpha),
                        0f, glowOrig, new Vector2(0.30f, 2.6f), SpriteEffects.None, 0);
                }
                Vector2 spearPos = SpearPos(fallT) - Main.screenPosition;
                //亮缘光鞘（黑底加色垫底，暗体压上后只露两侧亮缘）
                Main.EntitySpriteDraw(glow, spearPos, null, AstralveilFX.A0(AstralveilFX.Indigo) * 0.85f,
                    0f, glowOrig, new Vector2(0.44f, 3.0f), SpriteEffects.None, 0);
                //晶矛暗体（真 alpha 梭形剪影：星辉暗晶，两端自带尖锥收口）
                Main.EntitySpriteDraw(pad, spearPos, null, AstralveilFX.IndigoDeep * 0.95f,
                    0f, padOrig, new Vector2(0.62f, 3.4f), SpriteEffects.None, 0);
                //晶芯橙线（晶体内部辉光，窄于暗体）
                Main.EntitySpriteDraw(glow, spearPos, null, AstralveilFX.A0(AstralveilFX.OrangePale) * 0.55f,
                    0f, glowOrig, new Vector2(0.10f, 2.2f), SpriteEffects.None, 0);
                //矛尖星芒
                Main.EntitySpriteDraw(star, spearPos + new Vector2(0f, 58f), null,
                    AstralveilFX.A0(Color.White) * 0.85f, fallT * 3f, starOrig, 0.075f, SpriteEffects.None, 0);
            }

            //===== 触地闪与碎晶余韵 =====
            if (elapsed >= ImpactStart) {
                float flash = MathHelper.Clamp(1f - (elapsed - ImpactStart) / 8f, 0f, 1f);
                if (flash > 0.01f) {
                    //触地白橙闪（过曝只住在前两帧的 flash 峰值里）
                    Main.EntitySpriteDraw(glow, center, null,
                        AstralveilFX.A0(AstralveilFX.OrangePale) * (0.85f * flash), 0f, glowOrig,
                        new Vector2(2.6f + 1.2f * (1f - flash), 1.0f), SpriteEffects.None, 0);
                    //矛身余像：自落点向上淡出的短光柱（星矛死后仍留一瞬"曾在这里"）
                    Main.EntitySpriteDraw(glow, center - new Vector2(0f, 92f), null,
                        AstralveilFX.A0(AstralveilFX.Indigo) * (0.45f * flash), 0f, glowOrig,
                        new Vector2(0.5f, 3.4f), SpriteEffects.None, 0);
                }
                //碎晶余韵：矛体碎成贴地残晶，随退场帧缩短黯灭（消散而非删除）
                for (int i = 0; i < 6; i++) {
                    float h = 0.5f + 0.5f * MathF.Sin(i * 3.1f + phase * 2f);
                    float dx = (i - 2.5f) * CircleRadius * 0.18f + (h - 0.5f) * 20f;
                    float len = (8f + 9f * h) * ringOut;
                    float lean = (i - 2.5f) * 0.20f + (h - 0.5f) * 0.5f;
                    bool indigo = (i & 1) == 0;
                    Vector2 pos = center + new Vector2(dx, -len * 0.30f);
                    Main.EntitySpriteDraw(pad, pos, null,
                        (indigo ? AstralveilFX.IndigoDeep : EmberDeep) * (0.85f * ringOut),
                        lean, padOrig, new Vector2(0.26f, len / 42f), SpriteEffects.None, 0);
                    //残晶余光：凋灭前最后一点星辉
                    Main.EntitySpriteDraw(glow,
                        pos + new Vector2(MathF.Sin(lean), -MathF.Cos(lean)) * (len * 0.4f), null,
                        AstralveilFX.A0(indigo ? AstralveilFX.IndigoPale : AstralveilFX.OrangePale)
                            * (0.28f * ringOut),
                        0f, glowOrig, 0.08f, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
