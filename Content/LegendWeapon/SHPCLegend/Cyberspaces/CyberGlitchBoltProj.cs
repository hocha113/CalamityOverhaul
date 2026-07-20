using CalamityOverhaul.Common;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    /// <summary>领域展开故障闪电，Trail+CyberGlitchBolt.fx</summary>
    internal class CyberGlitchBoltProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int MaxLife = 30;
        private Vector2[] points;
        private int pointCount;
        private bool pathReady;
        private float glitchSeed;
        private Trail trail;

        private float visibleStart;
        private float visibleEnd;
        private float fadeAlpha;

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLife;
        }

        public override void AI() {
            //ai[0] = 主方向角度, ai[1] = 延迟帧数
            if (Projectile.ai[1] > 0) {
                Projectile.ai[1]--;
                Projectile.timeLeft = MaxLife;
                return;
            }

            if (!pathReady) {
                GeneratePath();
                glitchSeed = Main.rand.NextFloat();
                pathReady = true;
            }

            float t = 1f - (float)Projectile.timeLeft / MaxLife;
            ComputeAnimation(t);
        }

        private void ComputeAnimation(float t) {
            if (t < 0.28f) {
                //快速延伸（缓出）
                float ext = t / 0.28f;
                visibleEnd = 1f - MathF.Pow(1f - ext, 3.2f);
                visibleStart = 0f;
                fadeAlpha = MathHelper.SmoothStep(0.3f, 1f, ext);
            }
            else if (t < 0.40f) {
                //全亮+闪烁
                visibleEnd = 1f;
                visibleStart = 0f;
                float flash = MathF.Sin((t - 0.28f) / 0.12f * MathF.PI);
                fadeAlpha = 1f + flash * 0.4f;
            }
            else {
                //从尾部收缩消失
                float retract = (t - 0.40f) / 0.60f;
                visibleEnd = 1f;
                visibleStart = retract;
                fadeAlpha = 1f - retract;
            }
            fadeAlpha = MathHelper.Clamp(fadeAlpha, 0f, 1.4f);
        }

        private void GeneratePath() {
            float angle = Projectile.ai[0];
            int keyCount = Main.rand.Next(10, 17);
            Vector2[] keys = new Vector2[keyCount];
            Vector2 current = Projectile.Center;
            keys[0] = current;

            for (int i = 1; i < keyCount; i++) {
                float distFactor = (float)i / keyCount;
                float jag = Main.rand.NextFloat(-0.5f, 0.5f) * (0.6f + distFactor * 0.9f);

                //15%概率出现大偏转（数字电路般的急拐）
                if (Main.rand.NextFloat() < 0.15f)
                    jag = (Main.rand.NextBool() ? 1f : -1f) * 1.1f;

                //段长度：越远越长（加速扩张感）
                float segLen = Main.rand.NextFloat(25f, 55f) * (0.7f + distFactor * 0.6f);
                Vector2 step = (angle + jag).ToRotationVector2() * segLen;
                current += step;
                keys[i] = current;
            }

            //细分：每对关键帧间插入1个带垂直抖动的中间点
            pointCount = keyCount * 2 - 1;
            points = new Vector2[pointCount];
            for (int i = 0; i < keyCount - 1; i++) {
                points[i * 2] = keys[i];
                Vector2 mid = (keys[i] + keys[i + 1]) * 0.5f;
                Vector2 dir = keys[i + 1] - keys[i];
                Vector2 perp = new Vector2(-dir.Y, dir.X);
                if (perp.LengthSquared() > 0.01f)
                    perp.Normalize();
                mid += perp * Main.rand.NextFloat(-5f, 5f);
                points[i * 2 + 1] = mid;
            }
            points[(keyCount - 1) * 2] = keys[keyCount - 1];
        }

        private float WidthFunction(float progress) {
            //两端收窄，中间最宽
            float taper = MathF.Sin(progress * MathF.PI);
            taper = MathF.Max(taper, 0.06f);
            return 36f * taper;
        }

        private Color ColorFunction(Vector2 _) => Color.White;

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (!pathReady || points == null || fadeAlpha < 0.01f || Projectile.ai[1] > 0)
                return;

            Effect shader = EffectLoader.CyberGlitchBolt?.Value;
            if (shader == null) return;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (noise == null) return;

            trail ??= new Trail(points, WidthFunction, ColorFunction);
            trail.TrailPositions = points;

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            //取主人玩家的领域时间，避免远端客户端读 Local 造成故障雷动画节奏错位
            CyberspacePlayer ownerCp = Cyberspace.For(Projectile.owner);
            float ownerTime = ownerCp?.EffectTime ?? Cyberspace.EffectTime;
            shader.Parameters["uTime"]?.SetValue(ownerTime);
            shader.Parameters["fadeAlpha"]?.SetValue(MathHelper.Clamp(fadeAlpha, 0f, 1f));
            shader.Parameters["visibleStart"]?.SetValue(visibleStart);
            shader.Parameters["visibleEnd"]?.SetValue(visibleEnd);
            shader.Parameters["glitchSeed"]?.SetValue(glitchSeed);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            trail.DrawTrail(shader);
            device.BlendState = BlendState.AlphaBlend;
        }

        public override bool ShouldUpdatePosition() => false;
    }
}
