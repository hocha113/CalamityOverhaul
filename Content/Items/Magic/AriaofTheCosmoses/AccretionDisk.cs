using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses
{
    /// 黑洞本体：恒星→坍缩→内爆→视界诞生→稳态蓄力→掷出吞噬→蒸发终曲
    /// 蓄力计时各端在 AI 内自走(确定性)，掷出经 ai[1]+netUpdate 同步
    internal class AccretionDisk : ModProjectile, IPrimitiveDrawable, IWarpDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        /// <summary>蓄力帧计时(掷出后冻结)</summary>
        public ref float ChargeTime => ref Projectile.ai[0];
        /// <summary>0=蓄力中 1=已掷出</summary>
        public ref float ThrownState => ref Projectile.ai[1];
        private bool Thrown => ThrownState >= 1f;

        //演出时间轴(与 AriaofTheCosmosHeld.MaxChargeTime=180 对齐)
        internal const int StarEnd = 40;
        internal const int CollapseEnd = 50;
        internal const int BirthEnd = 80;
        internal const int FullCharge = 180;
        /// <summary>视界诞生前松手只算失手，恒星溃散不掷出</summary>
        internal const int MinThrowCharge = 52;
        /// <summary>掷出后寿命末尾的蒸发终曲窗口</summary>
        private const int EvapFrames = 36;

        //色板：白热→金橙→洋红→紫外(与 AriaBlackHole.fx 一致)
        internal static readonly Color ColHot = new(255, 248, 232);
        internal static readonly Color ColGold = new(255, 179, 71);
        internal static readonly Color ColRose = new(255, 94, 122);
        internal static readonly Color ColUV = new(107, 47, 168);
        internal static readonly Color ColIce = new(184, 219, 255);

        //本帧形态参数(AI 计算,DrawPrimitives/Warp 消费)
        private float fade;
        private float starR;
        private float starBright;
        private float collapse;
        private float horizonR;
        private float ringBright;
        private float diskIn;
        private float diskOut;
        private float diskFlat;
        private float diskBright;
        private float arc;
        private float doppler;
        private float inflow;
        private float blueshift;
        private float flash;
        private float jet;
        private float lensIntensity;
        private float spinPhase;
        private float visTime;
        /// <summary>吞噬弹幕后的光子环短促增亮</summary>
        private float ringBoost;
        /// <summary>内爆冲击波扭曲窗口计时</summary>
        private int implodeWarpTimer;

        private float Seed => Projectile.whoAmI * 0.137f % 1f;
        /// <summary>绘制quad边长(世界px)：内容直径的2.6倍给辉光留余量</summary>
        private float QuadSide => Projectile.width * Projectile.scale * 2.6f;
        /// <summary>盘外缘世界半径,亦作碰撞半径</summary>
        private float WorldDiskOut => QuadSide * diskOut;

        public override void SetDefaults() {
            Projectile.width = 400;
            Projectile.height = 400;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 2;
            Projectile.scale = 0.45f;
        }

        public override bool ShouldUpdatePosition() => Thrown;

        public override void AI() {
            visTime += 1f / 60f;

            if (!Thrown) {
                //蓄力计时各端确定性自走；位置由手持弹幕主人端锚定
                ChargeTime = Math.Min(ChargeTime + 1, FullCharge);
                Projectile.scale = MathHelper.Lerp(0.45f, 1.3f, EaseOut(Charge01(0, FullCharge)));
            }
            else {
                ThrownBehavior();
            }

            ComputeVisualState();
            EmitParticles();
            PlayStageSounds();

            //自旋相位积分：转速随蓄力增长而不跳帧
            float spinSpeed = MathHelper.Lerp(0.8f, 2.6f, Charge01(0, FullCharge)) + (Thrown ? 0.6f : 0f);
            spinPhase += spinSpeed / 60f;

            Lighting.AddLight(Projectile.Center, ColGold.ToVector3() * (diskBright + starBright) * 0.9f * fade);

            if (implodeWarpTimer > 0) {
                implodeWarpTimer--;
            }
            if (ringBoost > 0f) {
                ringBoost -= 0.12f;
            }
        }

        private float Charge01(float from, float to) => MathHelper.Clamp((ChargeTime - from) / (to - from), 0f, 1f);
        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
        private static float EaseIn(float t) => t * t;

        /// <summary>从 ChargeTime/掷出/蒸发推导本帧全部 shader 参数</summary>
        private void ComputeVisualState() {
            float t = ChargeTime;
            fade = MathHelper.Clamp(t / 10f, 0f, 1f);

            //2.5Hz 引力呼吸
            float breath = (float)Math.Sin(visTime * MathHelper.TwoPi * 2.5f);

            //---- 恒星段 ----
            if (t < StarEnd) {
                float p = t / StarEnd;
                starR = MathHelper.Lerp(0.09f, 0.125f, p) * (1f + breath * 0.02f);
                starBright = MathHelper.Clamp(t / 12f, 0f, 1f);
                collapse = 0f;
            }
            //---- 坍缩段：10帧内压到1/3,白热化 ----
            else if (t < CollapseEnd) {
                float p = (t - StarEnd) / (CollapseEnd - StarEnd);
                starR = MathHelper.Lerp(0.125f, 0.04f, EaseIn(p));
                starBright = 1f + p * 1.3f;
                collapse = p;
            }
            else {
                starR = 0f;
                starBright = 0f;
                collapse = 0f;
            }

            //---- 内爆白闪：48→58帧三角波 ----
            flash = 0f;
            if (t >= CollapseEnd - 2 && t < CollapseEnd + 8) {
                float ft = t - (CollapseEnd - 2);
                flash = ft < 2 ? ft / 2f : 1f - (ft - 2f) / 8f;
            }

            //---- 视界诞生 ----
            float birth = Charge01(CollapseEnd, BirthEnd - 10);
            horizonR = 0.085f * EaseOut(birth);
            //光子环点亮带过冲：诞生瞬间1.6倍随后回落
            float ignite = Charge01(CollapseEnd + 2, CollapseEnd + 14);
            float settle = Charge01(CollapseEnd + 14, BirthEnd);
            ringBright = ignite * MathHelper.Lerp(1.6f, 1.05f, settle);

            //---- 吸积盘甩出：x先扩y后扩 ----
            float spread = Charge01(CollapseEnd + 6, FullCharge - 20);
            diskIn = 0.115f;
            diskOut = MathHelper.Lerp(0.16f, 0.40f, EaseOut(spread));
            diskFlat = MathHelper.Lerp(0.9f, 0.42f, Charge01(CollapseEnd + 6, BirthEnd + 30));
            diskBright = MathHelper.Clamp(spread * 2.2f, 0f, 1.1f);

            //---- 稳态修饰 ----
            arc = Charge01(100, 170);
            doppler = MathHelper.Lerp(0.25f, 0.5f, Charge01(BirthEnd, FullCharge));
            inflow = birth * (0.55f + 0.2f * breath);
            float full = Charge01(FullCharge - 12, FullCharge);
            blueshift = full * (0.65f + 0.25f * breath);
            ringBright += full * 0.15f * breath + MathHelper.Clamp(ringBoost, 0f, 0.9f);

            //---- 透镜扭曲强度 ----
            lensIntensity = birth * MathHelper.Lerp(0.14f, 0.5f, Charge01(BirthEnd, FullCharge)) * (1f + breath * 0.1f);

            jet = 0f;

            //---- 掷出后的蒸发终曲覆写 ----
            if (Thrown && Projectile.timeLeft <= EvapFrames) {
                float e = 1f - Projectile.timeLeft / (float)EvapFrames;
                //盘先被吃光
                diskOut = MathHelper.Lerp(diskOut, diskIn + 0.015f, EaseIn(MathHelper.Clamp(e * 1.6f, 0f, 1f)));
                diskBright *= 1f - e * 0.75f;
                arc *= 1f - e;
                inflow = 1.2f * (1f - e * 0.5f);
                //视界随后急缩
                float shrink = MathHelper.Clamp((e - 0.55f) / 0.45f, 0f, 1f);
                horizonR *= 1f - EaseIn(shrink) * 0.92f;
                ringBright *= 1f + e * 0.8f;
                //两极喷流中段最盛
                jet = (float)Math.Sin(MathHelper.Clamp(e, 0f, 1f) * MathHelper.Pi) * 1.1f;
                //最后6帧霍金白闪
                if (Projectile.timeLeft <= 6) {
                    flash = 1f - Projectile.timeLeft / 6f;
                }
                lensIntensity *= 1f + e * 0.6f;
            }
        }

        private void ThrownBehavior() {
            //轻度追踪 + 缓速漂移
            HomeInOnNearestEnemy();
            Projectile.velocity *= 0.985f;

            //引力拉拽敌人
            float pullR = WorldDiskOut * 1.2f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile) || npc.boss || npc.knockBackResist <= 0f) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < pullR && dist > 20f) {
                    float factor = 1f - dist / pullR;
                    npc.velocity += (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero)
                        * 5.5f * factor * factor * npc.knockBackResist;
                }
            }

            //吞噬敌方弹幕：入视界即毁,光子环闪烁回应
            float eatR = Math.Max(horizonR * QuadSide * 2.2f, 60f);
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (!proj.hostile || proj.damage <= 0) {
                    continue;
                }
                float dist = Vector2.Distance(proj.Center, Projectile.Center);
                if (dist < eatR * 2.5f) {
                    proj.velocity += (Projectile.Center - proj.Center).SafeNormalize(Vector2.Zero) * 2.5f;
                }
                if (dist < eatR) {
                    proj.Kill();
                    ringBoost = 0.9f;
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 5; i++) {
                            PRTLoader.NewParticle<PRT_Spark>(proj.Center,
                                (Projectile.Center - proj.Center).SafeNormalize(Vector2.Zero).RotatedByRandom(0.4f) * Main.rand.NextFloat(4f, 9f),
                                ColIce, Main.rand.NextFloat(0.8f, 1.3f))?.Configure(false, Main.rand.Next(10, 16));
                        }
                    }
                }
            }
        }

        private void HomeInOnNearestEnemy() {
            NPC closest = null;
            float minDist = 700f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < minDist) {
                    minDist = dist;
                    closest = npc;
                }
            }
            if (closest != null) {
                Vector2 desired = Projectile.DirectionTo(closest.Center) * Projectile.velocity.Length();
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.12f);
            }
        }

        private void EmitParticles() {
            if (VaultUtils.isServer) {
                return;
            }

            float t = ChargeTime;

            //恒星段：光尘螺旋吸入
            if (t < StarEnd && t > 8 && Projectile.timeLeft % 5 == 0) {
                PRTLoader.NewParticle<PRT_GravityVortex>(Projectile.Center, Vector2.Zero,
                    Color.Lerp(ColGold, ColHot, Main.rand.NextFloat()), Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.NextFloat(MathHelper.TwoPi), Main.rand.NextFloat(70f, 150f) * Projectile.scale, Main.rand.Next(35, 55));
            }

            //坍缩段：外围物质急速塌入
            if (t >= StarEnd && t < CollapseEnd && Projectile.timeLeft % 2 == 0) {
                Vector2 spawn = Projectile.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(90f, 160f) * Projectile.scale;
                PRTLoader.NewParticle<PRT_Spark>(spawn,
                    (Projectile.Center - spawn).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(9f, 16f),
                    Color.Lerp(ColHot, ColIce, Main.rand.NextFloat(0.4f)), Main.rand.NextFloat(0.9f, 1.5f))
                    ?.Configure(false, Main.rand.Next(8, 14));
            }

            //稳态：意面化坠入流(切向拉长的裂隙贴着视界游走)
            if (horizonR > 0.01f && diskBright > 0.2f && Projectile.timeLeft % 4 == 0) {
                float hr = horizonR * QuadSide;
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * hr * Main.rand.NextFloat(1.35f, 2.1f);
                Vector2 tangential = ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(3f, 6f)
                    + (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1.5f, 3f);
                PRTLoader.NewParticle<PRT_SpaceFracture>(pos, tangential,
                    Color.Lerp(ColRose, ColUV, Main.rand.NextFloat()) * 0.9f, Main.rand.NextFloat(0.35f, 0.65f))
                    ?.Configure(Main.rand.Next(14, 24), Main.rand.NextFloat(-0.4f, 0.4f));

                //少量吸入光点
                if (Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_GravityVortex>(Projectile.Center, Vector2.Zero,
                        Color.Lerp(ColGold, ColIce, Main.rand.NextFloat()), Main.rand.NextFloat(0.4f, 0.7f))
                        ?.Configure(Main.rand.NextFloat(MathHelper.TwoPi), WorldDiskOut * Main.rand.NextFloat(0.7f, 1.05f), Main.rand.Next(40, 65));
                }
            }

            //掷出：后方拖出撕碎的盘物质
            if (Thrown && Projectile.velocity.LengthSquared() > 1f && Main.rand.NextBool(2)) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(1f, 1f) * WorldDiskOut * 0.5f;
                PRTLoader.NewParticle<PRT_SpaceFracture>(pos,
                    -Projectile.velocity * Main.rand.NextFloat(0.25f, 0.5f) + Main.rand.NextVector2Circular(1.2f, 1.2f),
                    Color.Lerp(ColGold, ColUV, Main.rand.NextFloat()) * 0.85f, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(12, 20), Main.rand.NextFloat(-0.3f, 0.3f));
            }

            //蒸发终曲：两极喷流粒子
            if (jet > 0.15f && Projectile.timeLeft % 2 == 0) {
                for (int s = -1; s <= 1; s += 2) {
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), s * Main.rand.NextFloat(10f, 20f) * jet);
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + new Vector2(Main.rand.NextFloat(-6f, 6f), 0f), vel,
                        Color.Lerp(ColIce, ColHot, Main.rand.NextFloat(0.5f)), Main.rand.NextFloat(0.9f, 1.5f))
                        ?.Configure(false, Main.rand.Next(10, 18));
                }
            }
        }

        private void PlayStageSounds() {
            if (VaultUtils.isServer) {
                return;
            }
            float t = ChargeTime;
            if (t == 2) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.55f, Pitch = -0.4f }, Projectile.Center);
            }
            else if (t == StarEnd) {
                //坍缩吸气
                SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with { Volume = 0.9f, Pitch = -0.6f }, Projectile.Center);
            }
            else if (t == CollapseEnd) {
                //内爆重锤：视界诞生
                SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 1f, Pitch = -0.6f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.8f, Pitch = -0.85f }, Projectile.Center);
                implodeWarpTimer = 14;
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Main.LocalPlayer.CWR().GetScreenShake(6f);
                }
            }
            else if (t == FullCharge - 12) {
                //满蓄提示
                SoundEngine.PlaySound(SoundID.DD2_DarkMageHealImpact with { Volume = 0.8f, Pitch = 0.15f }, Projectile.Center);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Thrown) {
                return false;
            }
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, Math.Max(WorldDiskOut, 60f), targetHitbox);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.4f, Pitch = 0.2f }, Projectile.Center);

            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_SpaceFracture>(target.Center + Main.rand.NextVector2Circular(14f, 14f),
                        Main.rand.NextVector2Circular(5f, 5f),
                        Color.Lerp(ColGold, ColUV, Main.rand.NextFloat()), Main.rand.NextFloat(0.4f, 0.8f))
                        ?.Configure(Main.rand.Next(14, 24), Main.rand.NextFloat(-0.5f, 0.5f));
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }

            if (!Thrown) {
                //失手溃散：恒星/雏形内爆成一撮火花
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = 0.5f }, Projectile.Center);
                for (int i = 0; i < 14; i++) {
                    Vector2 spawn = Projectile.Center + Main.rand.NextVector2Circular(60f, 60f);
                    PRTLoader.NewParticle<PRT_Spark>(spawn,
                        (Projectile.Center - spawn).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(5f, 10f),
                        ColGold, Main.rand.NextFloat(0.7f, 1.2f))?.Configure(false, Main.rand.Next(10, 18));
                }
                return;
            }

            //霍金蒸发白闪终曲
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.85f, Pitch = -0.3f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.6f, Pitch = 0.4f }, Projectile.Center);

            for (int i = 0; i < 26; i++) {
                float ang = MathHelper.TwoPi * i / 26f;
                PRTLoader.NewParticle<PRT_SpaceFracture>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    ang.ToRotationVector2() * Main.rand.NextFloat(5f, 15f),
                    Color.Lerp(ColHot, ColUV, Main.rand.NextFloat()), Main.rand.NextFloat(0.5f, 1.1f))
                    ?.Configure(Main.rand.Next(20, 38), Main.rand.NextFloat(-0.5f, 0.5f));
            }
            for (int i = 0; i < 16; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Circular(14f, 14f), ColIce, Main.rand.NextFloat(1f, 1.7f))
                    ?.Configure(false, Main.rand.Next(14, 24));
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                Main.LocalPlayer.CWR().GetScreenShake(7f);
            }
        }

        //=================== 绘制 ===================

        public override bool PreDraw(ref Color lightColor) => false;

        public bool CanDrawCustom() => false;
        public void DrawCustom(SpriteBatch spriteBatch) { }

        /// <summary>扭曲采样源：稳态引力透镜 / 内爆与蒸发时的冲击波环</summary>
        public void Warp() {
            float size = MathHelper.Clamp(QuadSide * 1.25f, 200f, 2400f);

            if (Thrown && Projectile.timeLeft <= 8) {
                //终曲外爆冲击波
                float p = 1f - Projectile.timeLeft / 8f;
                NeutronWarpHelper.DrawWarp(Projectile.Center, size * 1.4f, size * 1.4f,
                    0.55f, p, 0f, "ShockwaveRing");
                return;
            }

            if (implodeWarpTimer > 0) {
                //内爆：冲击波环反向收缩(progress 由 1 走向 0)
                float p = implodeWarpTimer / 14f;
                NeutronWarpHelper.DrawWarp(Projectile.Center, size, size,
                    0.45f, p, 0f, "ShockwaveRing");
            }

            if (lensIntensity > 0.01f) {
                NeutronWarpHelper.DrawWarp(Projectile.Center, size, size,
                    lensIntensity, 1f, 0f, "GravitationalLens");
            }
        }

        public void DrawPrimitives() {
            if (VaultUtils.isServer || fade <= 0.01f) {
                return;
            }

            Effect effect = EffectLoader.AriaBlackHole?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Texture2D white = CWRAsset.Placeholder_White?.Value;
            if (effect == null || noise == null || white == null) {
                return;
            }

            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float side = QuadSide;
            Vector2 texHalf = white.Size() * 0.5f;
            Vector2 quadScale = new(side / white.Width, side / white.Height);

            Matrix finalMatrix = Main.GameViewMatrix.TransformationMatrix
                * Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1);

            effect.Parameters["transformMatrix"]?.SetValue(finalMatrix);
            effect.Parameters["uTime"]?.SetValue(visTime);
            effect.Parameters["uSpinPhase"]?.SetValue(spinPhase);
            effect.Parameters["uSeed"]?.SetValue(Seed);
            effect.Parameters["uFade"]?.SetValue(fade);
            effect.Parameters["uStretch"]?.SetValue(Thrown ? 1f + MathHelper.Clamp(Projectile.velocity.Length() / 26f, 0f, 0.45f) : 1f);
            effect.Parameters["uMotAngle"]?.SetValue(Projectile.velocity.ToRotation());
            effect.Parameters["uStarR"]?.SetValue(starR);
            effect.Parameters["uStarBright"]?.SetValue(starBright);
            effect.Parameters["uCollapse"]?.SetValue(collapse);
            effect.Parameters["uHorizonR"]?.SetValue(horizonR);
            effect.Parameters["uRingBright"]?.SetValue(ringBright);
            effect.Parameters["uDiskIn"]?.SetValue(diskIn);
            effect.Parameters["uDiskOut"]?.SetValue(diskOut);
            effect.Parameters["uDiskFlat"]?.SetValue(diskFlat);
            effect.Parameters["uDiskBright"]?.SetValue(diskBright);
            effect.Parameters["uArc"]?.SetValue(arc);
            effect.Parameters["uDoppler"]?.SetValue(doppler);
            effect.Parameters["uInflow"]?.SetValue(inflow);
            effect.Parameters["uBlueshift"]?.SetValue(blueshift);
            effect.Parameters["uFlash"]?.SetValue(flash);
            effect.Parameters["uJet"]?.SetValue(jet);
            effect.Parameters["uJetAsym"]?.SetValue(0f);
            effect.Parameters["uPalShift"]?.SetValue(0f);
            effect.Parameters["noiseTexture"]?.SetValue(noise);

            //Pass1：暗背板+不透明视界(AlphaBlend 压出白天对比度)
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique = effect.Techniques["Backdrop"];
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(white, drawPos, null, Color.White, 0f, texHalf, quadScale, SpriteEffects.None, 0);
            sb.End();

            //Pass2：恒星/光子环/吸积盘/透镜弧/喷流(Additive)
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique = effect.Techniques["Glow"];
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(white, drawPos, null, Color.White, 0f, texHalf, quadScale, SpriteEffects.None, 0);
            sb.End();
        }
    }
}
