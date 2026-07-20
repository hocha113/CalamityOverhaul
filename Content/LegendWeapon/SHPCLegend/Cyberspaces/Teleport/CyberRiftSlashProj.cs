using CalamityOverhaul.Common;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Teleport
{
    /// <summary>瞬移数据走廊弹幕，Trail + CyberRiftSlash.fx</summary>
    internal class CyberRiftSlashProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //总寿命：从延伸到收尾约 0.5 秒
        private const int MaxLife = 30;
        //命中目标的归一化时间点：在此前完成"延伸"，到达后触发冲击脉冲
        private const float ImpactT = 0.32f;

        private Vector2 startPos;
        private Vector2 endPos;
        private Vector2[] points;
        private int pointCount;
        private bool pathReady;
        private float glitchSeed;
        private Trail trail;

        private float visibleStart;
        private float visibleEnd;
        private float fadeAlpha;
        private float impactPulse;
        private float corridorLength;

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
            if (!pathReady) {
                startPos = Projectile.Center;
                endPos = new Vector2(Projectile.ai[0], Projectile.ai[1]);
                glitchSeed = Main.rand.NextFloat();
                GeneratePath();
                pathReady = true;
            }

            float t = 1f - (float)Projectile.timeLeft / MaxLife;
            ComputeAnimation(t);
        }

        private void ComputeAnimation(float t) {
            //三段式：延伸（0~ImpactT）→ 命中提亮（ImpactT~0.5）→ 急速尾收（0.5~1）
            if (t < ImpactT) {
                float ext = t / ImpactT;
                visibleEnd = 1f - MathF.Pow(1f - ext, 2.4f);
                visibleStart = 0f;
                fadeAlpha = MathHelper.SmoothStep(0.55f, 1f, ext);
                impactPulse = 0f;
            }
            else if (t < 0.5f) {
                visibleEnd = 1f;
                visibleStart = 0f;
                float phase = (t - ImpactT) / (0.5f - ImpactT);
                fadeAlpha = 1f;
                impactPulse = MathF.Sin(phase * MathF.PI);
            }
            else {
                float retract = (t - 0.5f) / 0.5f;
                visibleEnd = 1f;
                visibleStart = retract;
                fadeAlpha = 1f - retract;
                impactPulse = MathF.Max(0f, 0.35f - retract * 0.6f);
            }
            fadeAlpha = MathHelper.Clamp(fadeAlpha, 0f, 1.4f);
            impactPulse = MathHelper.Clamp(impactPulse, 0f, 1f);
        }

        private void GeneratePath() {
            //笔直主轴 + 极轻微正弦"呼吸"摇摆，让走廊看起来是稳定的传输管道
            //刻意避免大幅弧线/锯齿，因为像素格走廊一旦弯曲就会被拉成菱形丑像素
            Vector2 axis = endPos - startPos;
            float length = axis.Length();
            corridorLength = length;
            if (length < 1f) {
                points = new Vector2[] { startPos, endPos };
                pointCount = 2;
                return;
            }
            Vector2 dir = axis / length;
            Vector2 perp = new(-dir.Y, dir.X);

            //段数适中，过多会扭弯像素格
            int segs = (int)MathHelper.Clamp(length / 60f, 8f, 18f);
            pointCount = segs + 1;
            points = new Vector2[pointCount];

            //极小幅度的整体弧度（最大 8px 内），仅为避免一根直线的纯几何感
            float arcSign = Main.rand.NextBool() ? 1f : -1f;
            float arcMag = MathHelper.Clamp(length * 0.012f, 2f, 8f);

            for (int i = 0; i < pointCount; i++) {
                float k = i / (float)(pointCount - 1);
                Vector2 basePos = startPos + axis * k;
                float arc = MathF.Sin(k * MathF.PI) * arcMag * arcSign;
                //端点压平
                float endpointDamp = MathHelper.SmoothStep(0f, 1f, MathF.Min(k, 1f - k) * 4f);
                points[i] = basePos + perp * arc * endpointDamp;
            }
        }

        private float WidthFunction(float progress) {
            //走廊宽度：两端微收，中段保持，避免梭形（梭形会让网格被拉伸）
            float taper = MathF.Sin(progress * MathF.PI);
            taper = MathF.Pow(MathF.Max(taper, 0.18f), 0.45f);
            float boost = 1f + impactPulse * 0.30f;
            //命中冲击时整条走廊轻微加粗
            return 56f * taper * boost;
        }

        private Color ColorFunction(Vector2 _) => Color.White;

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (!pathReady || points == null || fadeAlpha < 0.01f) {
                return;
            }

            Effect shader = EffectLoader.CyberRiftSlash?.Value;
            if (shader == null) return;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (noise == null) return;

            trail ??= new Trail(points, WidthFunction, ColorFunction);
            trail.TrailPositions = points;

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            //取主人玩家的领域时间，避免远端客户端读 Local 造成裂缝走廊节奏不一致
            CyberspacePlayer ownerCp = Cyberspace.For(Projectile.owner);
            float ownerTime = ownerCp?.EffectTime ?? Cyberspace.EffectTime;
            shader.Parameters["uTime"]?.SetValue(ownerTime);
            shader.Parameters["fadeAlpha"]?.SetValue(MathHelper.Clamp(fadeAlpha, 0f, 1f));
            shader.Parameters["visibleStart"]?.SetValue(visibleStart);
            shader.Parameters["visibleEnd"]?.SetValue(visibleEnd);
            shader.Parameters["glitchSeed"]?.SetValue(glitchSeed);
            shader.Parameters["impactPulse"]?.SetValue(impactPulse);
            shader.Parameters["corridorLength"]?.SetValue(corridorLength);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            trail.DrawTrail(shader);
            device.BlendState = BlendState.AlphaBlend;
        }

        public override bool ShouldUpdatePosition() => false;
    }
}
