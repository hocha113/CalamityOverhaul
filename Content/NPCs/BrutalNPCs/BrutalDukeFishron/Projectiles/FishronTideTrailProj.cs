using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Projectiles
{
    /// <summary>
    /// 潮汐冲刺的水迹残响：跟录冲刺路径成一条持留的水墙，
    /// 前段时间带判定，随后化作泡沫消散。ai[0]=Boss whoAmI，ai[1]=跟录帧数
    /// </summary>
    internal class FishronTideTrailProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int TrailDamage = 38;
        /// <summary>路径点上限</summary>
        private const int MaxPoints = 40;
        /// <summary>判定窗口：跟录结束后水墙仍然"活"的帧数</summary>
        private const int HitWindow = 86;
        /// <summary>判定关闭后的纯视觉消散帧数</summary>
        private const int FadeTail = 44;
        /// <summary>水墙半宽（判定与视觉一致）</summary>
        private const float HalfWidth = 44f;

        [VaultLoaden(CWRConstant.Masking + "PerlinNoise")]
        private static Asset<Texture2D> noiseTex = null;
        [VaultLoaden(CWRConstant.Masking + "WavyNoise")]
        private static Asset<Texture2D> flowTex = null;

        //共享 Trail：固定点数，绘制前重写位置（GPU 缓冲复用，见 DestroyerMotionFX 注释）
        private static Trail sharedTrail;
        private static readonly Vector2[] renderPositions = new Vector2[MaxPoints];
        private static readonly Vector2[] resampleSource = new Vector2[MaxPoints + 1];
        private static float renderAlpha;
        private static float renderWidthScale;
        /// <summary>尾先蚀退前沿：>1 全显，向 0 推进时从尾端吃掉条带</summary>
        private static float renderErodeFront;

        private readonly Vector2[] points = new Vector2[MaxPoints];
        private int pointCount;
        private int extendTimer = -1;

        private int BossIndex => (int)Projectile.ai[0];
        private int ExtendFrames => (int)Projectile.ai[1];
        /// <summary>判定是否仍激活</summary>
        private bool HitActive => Projectile.timeLeft > FadeTail;

        public override void SetStaticDefaults() {
            //条带最长≈死亡模式+离海激怒冲刺 68px/帧 × 30 跟录帧 ≈ 2040px，
            //本体只有 16px 且焊在头部：余量必须盖满全尾，否则头部出屏整条瞬灭
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2200;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //首帧初始化跟录窗口
            if (extendTimer < 0) {
                extendTimer = Math.Max(ExtendFrames, 6);
                Projectile.timeLeft = extendTimer + HitWindow + FadeTail;
            }

            //跟录 Boss 路径
            if (extendTimer > 0) {
                extendTimer--;
                if (BossIndex.TryGetNPC(out NPC boss) && boss.active) {
                    AppendPoint(boss.Center);
                    Projectile.Center = boss.Center;
                }
                else {
                    extendTimer = 0;
                }
            }

            //水墙冒泡与湿光
            if (!VaultUtils.isServer && pointCount > 1) {
                if (Main.rand.NextBool(3)) {
                    int idx = Main.rand.Next(pointCount);
                    Vector2 pos = points[idx] + Main.rand.NextVector2Circular(HalfWidth * 0.7f, HalfWidth * 0.7f);
                    if (HitActive) {
                        FishronMotionFX.SpawnSprayCone(pos, -Vector2.UnitY, 1, 0.5f, 2f, 0.6f, 0.6f);
                    }
                    else if (Main.rand.NextBool(2)) {
                        InnoVault.PRT.PRTLoader.NewParticle<PRT_FishronFoam>(pos,
                            -Vector2.UnitY * 0.5f, FishronMotionFX.FoamWhite * 0.3f,
                            Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(20, 34), 0.01f);
                    }
                }
                int lightIdx = (int)(Main.GameUpdateCount % pointCount);
                Lighting.AddLight(points[lightIdx], FishronMotionFX.SeaGreen.ToVector3() * 0.35f);
            }
        }

        private void AppendPoint(Vector2 pos) {
            if (pointCount > 0 && Vector2.DistanceSquared(points[pointCount - 1], pos) < 12f * 12f) {
                return;
            }
            if (pointCount < MaxPoints) {
                points[pointCount++] = pos;
                return;
            }
            //满员后滚动，丢最旧
            for (int i = 0; i < MaxPoints - 1; i++) {
                points[i] = points[i + 1];
            }
            points[MaxPoints - 1] = pos;
        }

        public override bool CanHitPlayer(Player target) => HitActive && pointCount > 1;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!HitActive || pointCount < 2) {
                return false;
            }
            float collisionPoint = 0f;
            for (int i = 0; i < pointCount - 1; i++) {
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    points[i], points[i + 1], HalfWidth * 2f, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (pointCount < 2) {
                return false;
            }

            //生命包络：判定期满强，随后指数塌缩
            float fade = HitActive
                ? 1f
                : MathHelper.Clamp(Projectile.timeLeft / (float)FadeTail, 0f, 1f);

            Effect effect = EffectLoader.OceanCurrentTrail?.Value;
            if (effect == null || noiseTex == null || flowTex == null) {
                DrawFoamFallback(fade);
                return false;
            }

            //弧长重采样：40 个渲染点均匀铺满真实路径。
            //旧做法用最旧点垫尾，着色器的尾端渐隐区(along 0.72~1)全落在零长度的
            //垫点上，真实尾端得不到任何淡出——方形硬切口的根源
            if (!BuildRenderPositions()) {
                return false;
            }
            renderAlpha = 0.4f + 0.6f * fade;
            renderWidthScale = 0.7f + 0.3f * fade;
            //消散期尾先蚀退：前沿从尾端一路推向头部，几何上"吃掉"条带
            renderErodeFront = HitActive ? 1.2f : -0.18f + 1.38f * fade;

            sharedTrail ??= new Trail(new Vector2[MaxPoints],
                f => {
                    //尾端几何收针到零宽 + 蚀退前沿软边——两种包络都在顶点层
                    float tip = MathHelper.Clamp((1f - f) / 0.2f, 0f, 1f);
                    tip = tip * tip * (3f - 2f * tip);
                    float erode = MathHelper.Clamp((renderErodeFront - f) / 0.16f, 0f, 1f);
                    return HalfWidth * renderWidthScale * (1f - f * 0.25f) * tip * erode;
                },
                texCoord => Color.White * renderAlpha);
            sharedTrail.TrailPositions = renderPositions;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly * 0.9f);
            //消散主要由顶点层蚀退承担，像素层只轻降——避免"整条变淡"的塑料退场
            effect.Parameters["fadeAlpha"]?.SetValue(0.45f + 0.55f * fade);
            effect.Parameters["pulse"]?.SetValue(0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f + Projectile.whoAmI));
            effect.Parameters["speedRatio"]?.SetValue(extendTimer > 0 ? 1f : 0.35f);
            effect.Parameters["foamDensity"]?.SetValue(HitActive ? 0.55f : 0.95f);
            effect.Parameters["deepColor"]?.SetValue(FishronMotionFX.DeepSea.ToVector3());
            effect.Parameters["shallowColor"]?.SetValue(FishronMotionFX.SeaGreen.ToVector3());
            effect.Parameters["foamColor"]?.SetValue(FishronMotionFX.FoamWhite.ToVector3());
            effect.Parameters["bioColor"]?.SetValue(new Vector3(0.35f, 0.85f, 0.9f));
            effect.Parameters["uNoiseTex"]?.SetValue(noiseTex.Value);
            effect.Parameters["uFlowTex"]?.SetValue(flowTex.Value);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.BlendState = BlendState.Additive;
            sharedTrail.DrawTrail(effect);
            gd.BlendState = BlendState.AlphaBlend;

            return false;
        }

        /// <summary>
        /// 把已录路径按弧长均匀重采样进 renderPositions（index0=头部）。
        /// 跟录期以本体实时位置作头顶点，保证条带根部始终焊在 Boss 身上。
        /// 返回 false 表示几何退化（总长过短），跳过绘制
        /// </summary>
        private bool BuildRenderPositions() {
            //源折线：头→尾（最新→最旧）
            int srcCount = 0;
            if (extendTimer > 0 && Vector2.DistanceSquared(Projectile.Center, points[pointCount - 1]) > 1f) {
                resampleSource[srcCount++] = Projectile.Center;
            }
            for (int i = pointCount - 1; i >= 0; i--) {
                resampleSource[srcCount++] = points[i];
            }
            if (srcCount < 2) {
                return false;
            }

            float totalLen = 0f;
            for (int i = 0; i < srcCount - 1; i++) {
                totalLen += Vector2.Distance(resampleSource[i], resampleSource[i + 1]);
            }
            if (totalLen < 24f) {
                return false;
            }

            //双指针等距行走
            float step = totalLen / (MaxPoints - 1);
            int seg = 0;
            float segStart = 0f;
            float segLen = Vector2.Distance(resampleSource[0], resampleSource[1]);
            renderPositions[0] = resampleSource[0];
            for (int i = 1; i < MaxPoints; i++) {
                float target = step * i;
                while (segStart + segLen < target && seg < srcCount - 2) {
                    segStart += segLen;
                    seg++;
                    segLen = Vector2.Distance(resampleSource[seg], resampleSource[seg + 1]);
                }
                float t = segLen > 0.001f ? MathHelper.Clamp((target - segStart) / segLen, 0f, 1f) : 0f;
                renderPositions[i] = Vector2.Lerp(resampleSource[seg], resampleSource[seg + 1], t);
            }
            return true;
        }

        /// <summary>着色器缺失时的泡沫贴图兜底</summary>
        private void DrawFoamFallback(float fade) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            for (int i = 0; i < pointCount; i++) {
                float t = i / (float)Math.Max(pointCount - 1, 1);
                Color c = Color.Lerp(FishronMotionFX.DeepSea, FishronMotionFX.SeaGreen, t);
                c = new Color(c.R, c.G, c.B, 0) * (fade * 0.5f);
                Main.EntitySpriteDraw(glow, points[i] - Main.screenPosition, null, c,
                    0f, glow.Size() * 0.5f, new Vector2(0.5f, 0.4f) * (0.6f + 0.4f * fade), SpriteEffects.None, 0);
            }
        }

        internal static void UnloadTrails() {
            sharedTrail?.Dispose();
            sharedTrail = null;
        }
    }
}
