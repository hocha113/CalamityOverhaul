using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Starfall.Projectiles
{
    /// <summary>
    /// 「余爆」小型爆燃。ai[0]=体型。生成位置即锁定爆点（预告即承诺，环境驱动无宿主怪）：
    /// 陨石瓦片红亮渐盛 + 滋滋声升调 48 帧 → 短促火柱窜起 16 帧（仅此窗口有判定，微量伤害）
    /// → 火星余韵飘散 26 帧。全程状态由 timeLeft 确定性推导，各端一致，无补包依赖
    /// </summary>
    internal class StarfallAfterburstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>预告帧数（公平契约 ≥45，各档位一律不缩短）</summary>
        private const int TelegraphFrames = 48;
        /// <summary>火柱喷发帧数（判定窗=可见喷发窗）</summary>
        private const int EruptFrames = 16;
        /// <summary>余韵帧数（火星飘散，无判定）</summary>
        private const int LingerFrames = 26;
        /// <summary>火柱窜起用时（帧）</summary>
        private const int RiseFrames = 5;
        /// <summary>余韵开头火柱塌缩用时（帧）</summary>
        private const int CollapseFrames = 6;
        /// <summary>柱高（×体型）</summary>
        private const float BaseHeight = 122f;
        /// <summary>柱半宽（×体型）</summary>
        private const float BaseHalfWidth = 15f;
        /// <summary>预告期滋滋声间隔（音调逐次升高）</summary>
        private const int SizzleGap = 12;

        private float Scale => Projectile.ai[0];
        private static int TotalLife => TelegraphFrames + EruptFrames + LingerFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        /// <summary>窜起程度 0~1（立方缓出，猛地窜起）</summary>
        private float EruptProgress {
            get {
                int t = Elapsed - TelegraphFrames;
                if (t <= 0) {
                    return 0f;
                }
                if (t >= RiseFrames) {
                    return 1f;
                }
                float x = t / (float)RiseFrames;
                return 1f - (1f - x) * (1f - x) * (1f - x);
            }
        }

        /// <summary>余韵期柱体塌缩 1→0（判定早已结束，只收视觉）</summary>
        private float CollapseFactor {
            get {
                int t = Elapsed - TelegraphFrames - EruptFrames;
                if (t <= 0) {
                    return 1f;
                }
                return MathHelper.Clamp(1f - t / (float)CollapseFrames, 0f, 1f);
            }
        }

        /// <summary>余韵进度 0~1</summary>
        private float LingerProgress {
            get {
                int t = Elapsed - TelegraphFrames - EruptFrames;
                return t <= 0 ? 0f : MathHelper.Clamp(t / (float)LingerFrames, 0f, 1f);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 240;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = false;//喷发窗口内才置真
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;
            //判定窗=可见喷发窗，各端从 timeLeft 推同一结论
            Projectile.hostile = elapsed >= TelegraphFrames && elapsed < TelegraphFrames + EruptFrames;

            if (Main.dedServ) {
                return;//以下全是本地演出
            }

            if (elapsed < TelegraphFrames) {
                TelegraphVisuals(elapsed);
                return;
            }
            if (elapsed == TelegraphFrames) {
                CommitBeat();
            }
            if (elapsed < TelegraphFrames + EruptFrames) {
                EruptVisuals();
                return;
            }
            LingerVisuals(elapsed);
        }

        /// <summary>预告期：瓦片红亮渐盛，火星渐密，滋滋声逐次升调</summary>
        private void TelegraphVisuals(int elapsed) {
            float progress = elapsed / (float)TelegraphFrames;
            if (elapsed % SizzleGap == 0) {
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with {
                    Volume = 0.24f + 0.2f * progress,
                    Pitch = -0.35f + 0.75f * progress,
                    MaxInstances = 5,
                }, Projectile.Center);
            }
            //预热火星 ≤1 粒/帧（命中率随进度 1/3→2/3）
            if (Main.rand.NextBool(3) || (progress > 0.6f && Main.rand.NextBool(2))) {
                Dust spark = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-BaseHalfWidth, BaseHalfWidth) * Scale, -2f),
                    DustID.Torch, new Vector2(Main.rand.NextFloat(-0.3f, 0.3f),
                        -Main.rand.NextFloat(0.6f, 1.4f + 2f * progress)),
                    0, default, 0.8f + progress * 0.6f);
                spark.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.55f, 0.20f, 0.06f) * (0.25f + 0.75f * progress));
        }

        /// <summary>破土帧：火柱窜起的一拍（喷焰声+闷响+近距轻震+爆点粉尘）</summary>
        private void CommitBeat() {
            SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.7f, Pitch = 0.1f, MaxInstances = 5 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.42f, Pitch = 0.35f, MaxInstances = 5 }, Projectile.Center);
            if (CWRClientConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center,
                    Vector2.UnitY, 2.2f, 4f, 14, 420f, "CWRStarfallBurst"));
            }
            for (int i = 0; i < 8; i++) {
                Dust burst = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    new Vector2(Main.rand.NextFloat(-1.8f, 1.8f), -Main.rand.NextFloat(3.5f, 8f)) * Scale,
                    0, default, Main.rand.NextFloat(1.1f, 1.7f));
                burst.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>喷发期：柱内持续上涌的火尘（判定窗口内的实体感来源之一）</summary>
        private void EruptVisuals() {
            for (int i = 0; i < 4; i++) {
                Dust flame = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-BaseHalfWidth, BaseHalfWidth) * Scale * 0.8f, 0f),
                    DustID.Torch, new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(4f, 8.5f)) * Scale,
                    0, default, Main.rand.NextFloat(1f, 1.6f));
                flame.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center - Vector2.UnitY * BaseHeight * 0.5f * Scale,
                new Vector3(1.0f, 0.55f, 0.2f) * EruptProgress);
        }

        /// <summary>余韵期：第一帧撒出带重力的火星弧线，此后碳烟缓升，地面余温减光</summary>
        private void LingerVisuals(int elapsed) {
            if (elapsed == TelegraphFrames + EruptFrames) {
                for (int i = 0; i < 10; i++) {
                    Dust spark = Dust.NewDustPerfect(
                        Projectile.Center - new Vector2(0f, Main.rand.NextFloat(10f, BaseHeight * 0.6f) * Scale),
                        DustID.Torch, new Vector2(Main.rand.NextFloat(-2.6f, 2.6f), -Main.rand.NextFloat(1f, 4f)),
                        0, default, Main.rand.NextFloat(0.9f, 1.4f));
                    spark.noGravity = false;//重力弧线，读作真火星
                }
            }
            else if (Main.rand.NextBool(4)) {
                Dust smoke = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-BaseHalfWidth, BaseHalfWidth) * Scale, -4f),
                    DustID.Smoke, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.1f)),
                    150, default, Main.rand.NextFloat(0.8f, 1.2f));
                smoke.noGravity = true;
            }
            float cooling = 1f - LingerProgress;
            if (cooling > 0.05f) {
                Lighting.AddLight(Projectile.Center, new Vector3(0.6f, 0.24f, 0.08f) * cooling);
            }
        }

        /// <summary>柱形判定：沿柱轴分两段取样（判定窗已由 hostile 门控）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            float erupt = EruptProgress;
            if (erupt < 0.25f) {
                return false;
            }
            float height = BaseHeight * Scale * erupt;
            float halfWidth = BaseHalfWidth * Scale;
            for (int i = 0; i < 2; i++) {
                Vector2 point = Projectile.Center - new Vector2(0f, height * (0.25f + 0.5f * i));
                Rectangle sample = Utils.CenteredRectangle(point, new Vector2(halfWidth * 2f, height * 0.5f));
                if (sample.Intersects(targetHitbox)) {
                    return true;
                }
            }
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //灼热余烬地的类型风味：命中方本机结算，原生同步
            target.AddBuff(BuffID.OnFire, 60);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }

            if (elapsed < TelegraphFrames) {
                DrawTelegraph(elapsed, glow);
                return false;
            }

            float columnVis = EruptProgress * CollapseFactor;
            if (columnVis > 0.02f) {
                DrawColumn(columnVis, glow);
            }
            float cooling = 1f - LingerProgress;
            if (LingerProgress > 0f && cooling > 0.02f) {
                //地面余温：随余韵冷却收尾（弹幕死前最后的存在痕迹）
                Vector2 basePos = Projectile.Center + new Vector2(0f, 2f) - Main.screenPosition;
                Color emberRest = new Color(255, 100, 44, 0) * (0.38f * cooling);
                Main.EntitySpriteDraw(glow, basePos, null, emberRest, 0f, glow.Size() / 2f,
                    new Vector2(1.4f * Scale, 0.4f), SpriteEffects.None, 0);
            }
            return false;
        }

        /// <summary>预告绘制：地表警示光斑红亮渐盛，脉动频率随进度加急，内芯后半程点亮</summary>
        private void DrawTelegraph(int elapsed, Texture2D glow) {
            float progress = elapsed / (float)TelegraphFrames;
            float pulse = 0.7f + 0.3f * MathF.Sin(
                Main.GlobalTimeWrappedHourly * (10f + 16f * progress) + Projectile.identity);
            Vector2 basePos = Projectile.Center + new Vector2(0f, 2f) - Main.screenPosition;

            //外圈暗红渐盛
            Color warn = new Color(255, 84, 36, 0) * (0.5f * MathF.Pow(progress, 1.3f) * pulse);
            Main.EntitySpriteDraw(glow, basePos, null, warn, 0f, glow.Size() / 2f,
                new Vector2((1.3f + 0.6f * progress) * Scale, 0.4f + 0.15f * progress), SpriteEffects.None, 0);

            //内芯后半程点亮：临爆的高温白热前兆（金橙，不用纯白）
            if (progress > 0.45f) {
                float inner = (progress - 0.45f) / 0.55f;
                Color hot = new Color(255, 172, 80, 0) * (0.55f * inner * pulse);
                Main.EntitySpriteDraw(glow, basePos, null, hot, 0f, glow.Size() / 2f,
                    new Vector2((0.5f + 0.4f * inner) * Scale, 0.28f), SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// 火柱绘制：Extra_98 真 alpha 暗焦烟衬底给轮廓，同图三层热边错高错宽
        /// （外层带 A，内层 A=0），每层独立抖动，柱顶加冠光
        /// </summary>
        private void DrawColumn(float columnVis, Texture2D glow) {
            float height = BaseHeight * Scale * columnVis;
            Vector2 basePos = Projectile.Center - Main.screenPosition;

            Texture2D under = CWRAsset.Extra_98?.Value;
            if (under != null) {
                Vector2 underOrigin = under.Size() / 2f;
                //暗焦烟衬底（真 alpha 才能压出暗轮廓）
                Vector2 underScale = new(BaseHalfWidth * 2.6f * Scale / under.Width, height * 1.18f / under.Height);
                Color charcoal = new Color(52, 30, 22) * (0.55f * columnVis);
                Main.EntitySpriteDraw(under, basePos - new Vector2(0f, height * 0.5f), null,
                    charcoal, 0f, underOrigin, underScale, SpriteEffects.None, 0);

                //热层：竖直短柱，外层焦暗带 A，内层橙金 A=0；判定半宽藏在外宽内
                const float SootVisFrac = 0.65f;
                float visW = under.Width * SootVisFrac;
                float visH = under.Height * SootVisFrac;
                ReadOnlySpan<float> layerH = [1f, 0.78f, 0.52f];
                ReadOnlySpan<float> layerW = [1f, 0.72f, 0.48f];
                Span<Color> layerC = [
                    new Color(150, 36, 20),
                    new Color(255, 150, 40),
                    new Color(255, 210, 110),
                ];
                for (int i = 0; i < 3; i++) {
                    float jitter = MathF.Sin(Main.GlobalTimeWrappedHourly * 34f
                        + Projectile.identity * 2.3f + i * 2.1f);
                    float hPx = height * layerH[i] * (0.92f + 0.1f * jitter);
                    float w = BaseHalfWidth * 2f * Scale * layerW[i] / visW;
                    float h = hPx / visH;
                    Color col = layerC[i];
                    if (i == 0) {
                        col *= 0.55f * columnVis;
                    }
                    else {
                        col = col with { A = 0 };
                        col *= (i == 1 ? 0.75f : 0.8f) * columnVis;
                    }
                    Vector2 pos = basePos + new Vector2(jitter * 2.5f, 0f) - new Vector2(0f, hPx * 0.5f);
                    Main.EntitySpriteDraw(under, pos, null, col, 0f, underOrigin,
                        new Vector2(w, h), SpriteEffects.None, 0);
                }
            }

            //柱顶冠光 + 柱根底光（敷料层，占比小）
            Color crown = new Color(255, 200, 110, 0) * (0.45f * columnVis);
            Main.EntitySpriteDraw(glow, basePos - new Vector2(0f, height), null, crown, 0f,
                glow.Size() / 2f, new Vector2(0.8f * Scale, 0.55f), SpriteEffects.None, 0);
            Color root = new Color(255, 120, 40, 0) * (0.5f * columnVis);
            Main.EntitySpriteDraw(glow, basePos + new Vector2(0f, 2f), null, root, 0f,
                glow.Size() / 2f, new Vector2(1.5f * Scale, 0.42f), SpriteEffects.None, 0);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //最后一缕碳烟
            for (int i = 0; i < 3; i++) {
                Dust smoke = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), -4f),
                    DustID.Smoke, new Vector2(0f, -Main.rand.NextFloat(0.4f, 0.9f)),
                    160, default, Main.rand.NextFloat(0.7f, 1f));
                smoke.noGravity = true;
            }
        }
    }
}
