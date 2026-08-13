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
        private static float renderAlpha;
        private static float renderWidthScale;

        private readonly Vector2[] points = new Vector2[MaxPoints];
        private int pointCount;
        private int extendTimer = -1;

        private int BossIndex => (int)Projectile.ai[0];
        private int ExtendFrames => (int)Projectile.ai[1];
        /// <summary>判定是否仍激活</summary>
        private bool HitActive => Projectile.timeLeft > FadeTail;

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

            //index0=最新点=着色器头部；不足处以最旧点垫尾
            renderPositions[0] = pointCount > 0 ? points[pointCount - 1] : Projectile.Center;
            for (int i = 1; i < MaxPoints; i++) {
                int src = pointCount - 1 - i;
                renderPositions[i] = src >= 0 ? points[src] : points[0];
            }
            renderAlpha = fade;
            renderWidthScale = 0.55f + 0.45f * fade;

            sharedTrail ??= new Trail(new Vector2[MaxPoints],
                f => HalfWidth * renderWidthScale * (1f - f * 0.25f),
                texCoord => Color.White * renderAlpha);
            sharedTrail.TrailPositions = renderPositions;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly * 0.9f);
            effect.Parameters["fadeAlpha"]?.SetValue(fade);
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
