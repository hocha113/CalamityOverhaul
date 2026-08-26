using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>骇客时间 RenderHandle，后处理与光圈</summary>
    internal class HackTimeRender : RenderHandle
    {
        //上传进度覆盖层
        internal static HackUploadOverlay UploadOverlay { get; private set; } = new();

        public override float Weight => 1.15f;

        public override void UpdateBySystem(int index) {
            if (Main.gameMenu) {
                HackTime.Reset();
                return;
            }

            HackTime.Update();
            UploadOverlay.Update();
            HackTimeTileDraw.Update();
        }

        public override void EndCaptureDraw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu) return;
            if (!HackTime.Active && HackTime.Intensity < 0.001f) return;

            ApplyScreenShader(spriteBatch, graphicsDevice, screenSwap);
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (Main.gameMenu) return;

            bool hackTimeVisible = HackTime.Active || HackTime.Intensity >= 0.001f;

            //光圈与上传环，加法混合
            if (hackTimeVisible) {
                //物块赛博滤镜，独立 Begin/End
                HackTimeTileCyberPass.Draw(spriteBatch, Main.instance.GraphicsDevice);

                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);

                DrawHoveredReticle(spriteBatch);
                DrawSelectedReticle(spriteBatch);
                HackTimeTileDraw.DrawAdditive(spriteBatch);

                var queue = HackTimeUI.Instance?.Queue;
                if (queue != null && !queue.IsEmpty && HackTime.SelectedTargetIndex >= 0) {
                    //仅当前选中目标活跃条目
                    if (queue.TryGetActiveEntry(HackTime.CurrentScanTarget, out float headProgress, out bool headCompleted)) {
                        UploadOverlay.Draw(spriteBatch, HackTime.SelectedTargetIndex,
                            headProgress, headCompleted, HackTime.Intensity);
                    }
                }

                spriteBatch.End();
            }

            //状态卡片与物块边框，AlphaBlend
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            HackStatusDisplay.Draw(spriteBatch);
            HackTimeTileDraw.DrawAlphaBlend(spriteBatch);
            spriteBatch.End();
        }

        private static void ApplyScreenShader(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            Effect shader = HackTimeAssets.HackTimeScreen;
            if (shader == null) return;
            if (screenSwap == null || screenSwap.IsDisposed) return;
            if (Main.screenTarget == null || Main.screenTarget.IsDisposed) return;

            //低光照 RT 异常时跳过着色器
            if (RenderQualitySafety.ScreenTargetUnavailable()) return;
            if (!RenderQualitySafety.IsScreenTargetActive(graphicsDevice)) return;

            //保存/还原 RT 绑定
            RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();

            //中途异常必须还原 RT 绑定,防错绑遗留到后续绘制(反馈十四·#64)
            try {
                //拷屏到交换缓冲
                graphicsDevice.SetRenderTarget(screenSwap);
                graphicsDevice.Clear(Color.Transparent);
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
                spriteBatch.End();

                //着色器参数
                shader.Parameters["intensity"]?.SetValue(HackTime.Intensity);
                shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                shader.Parameters["vignetteStrength"]?.SetValue(0.6f);
                shader.Parameters["tintStrength"]?.SetValue(1.0f);
                shader.Parameters["uScreenSize"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));

                //着色器回写主屏
                graphicsDevice.SetRenderTarget(Main.screenTarget);
                graphicsDevice.Clear(Color.Transparent);
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
                shader.CurrentTechnique.Passes[0].Apply();
                spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
                spriteBatch.End();
            } finally {
                //还原 RT 绑定
                if (previousTargets != null && previousTargets.Length > 0
                    && previousTargets[0].RenderTarget != Main.screenTarget) {
                    graphicsDevice.SetRenderTargets(previousTargets);
                }
            }
        }

        /// <summary>悬停目标预选光圈</summary>
        private static void DrawHoveredReticle(SpriteBatch spriteBatch) {
            int hoverIdx = HackTime.HoveredTargetIndex;
            if (hoverIdx < 0 || hoverIdx >= Main.maxNPCs) return;

            NPC npc = Main.npc[hoverIdx];
            if (!npc.active) return;

            //已选中则跳过
            if (hoverIdx == HackTime.SelectedTargetIndex) return;

            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex == null) return;

            Vector2 screenPos = npc.Center - Main.screenPosition;
            float baseRadius = Math.Max(npc.width, npc.height) * 0.7f + 16f;
            float time = HackTime.ReticleTimer;
            float effectStr = HackTime.Intensity;

            //预选光圈
            DrawReticleRing(spriteBatch, glowTex, screenPos, baseRadius, time,
                effectStr * 0.5f, new Color(0.1f, 0.6f, 0.65f, 0f), 8);
        }

        /// <summary>选中目标骇入光圈</summary>
        private static void DrawSelectedReticle(SpriteBatch spriteBatch) {
            int selIdx = HackTime.SelectedTargetIndex;
            if (selIdx < 0 || selIdx >= Main.maxNPCs) return;

            NPC npc = Main.npc[selIdx];
            if (!npc.active) return;

            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex == null) return;

            Vector2 screenPos = npc.Center - Main.screenPosition;
            float baseRadius = Math.Max(npc.width, npc.height) * 0.8f + 20f;
            float time = HackTime.ReticleTimer;
            float effectStr = HackTime.Intensity;

            //外环
            DrawReticleRing(spriteBatch, glowTex, screenPos, baseRadius + 8f, time,
                effectStr * 0.7f, new Color(0.15f, 0.8f, 0.85f, 0f), 12);

            //内环脉冲
            float innerPulse = 0.8f + 0.2f * MathF.Sin(time * 4f);
            DrawReticleRing(spriteBatch, glowTex, screenPos, baseRadius * innerPulse, time * 1.5f,
                effectStr * 0.45f, new Color(0.2f, 0.9f, 0.7f, 0f), 12);

            //中心十字准星
            DrawCrosshair(spriteBatch, glowTex, screenPos, time, effectStr);
        }

        /// <summary>旋转分段光圈环</summary>
        private static void DrawReticleRing(SpriteBatch spriteBatch, Texture2D glowTex,
            Vector2 center, float radius, float time, float alpha, Color baseColor, int segments) {

            float glowScale = 12f / glowTex.Width;
            Vector2 origin = new(glowTex.Width * 0.5f, glowTex.Height * 0.5f);
            float rotationOffset = time * 0.8f;

            //分段间隔排列
            float gapRatio = 0.3f;
            float segmentArc = MathHelper.TwoPi / segments;
            float gapArc = segmentArc * gapRatio;
            float drawArc = segmentArc - gapArc;

            for (int seg = 0; seg < segments; seg++) {
                float segStart = seg * segmentArc + rotationOffset;
                int pointsPerSeg = 6;

                for (int p = 0; p < pointsPerSeg; p++) {
                    float t = (float)p / (pointsPerSeg - 1);
                    float angle = segStart + drawArc * t;
                    float cos = MathF.Cos(angle);
                    float sin = MathF.Sin(angle);

                    Vector2 pos = center + new Vector2(cos, sin) * radius;

                    //线段两端渐隐
                    float edgeFade = 1f - MathF.Abs(t - 0.5f) * 2f;
                    edgeFade = MathF.Pow(edgeFade, 0.5f);
                    float pointAlpha = alpha * edgeFade;

                    Color color = baseColor * pointAlpha;
                    spriteBatch.Draw(glowTex, pos, null, color, 0f, origin, glowScale, SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>中心十字准星</summary>
        private static void DrawCrosshair(SpriteBatch spriteBatch, Texture2D glowTex,
            Vector2 center, float time, float effectStr) {

            Vector2 origin = new(glowTex.Width * 0.5f, glowTex.Height * 0.5f);
            float glowScale = 8f / glowTex.Width;
            float armLength = 14f;
            float pulse = 0.7f + 0.3f * MathF.Sin(time * 3f);
            Color color = new Color(0.15f, 0.85f, 0.8f, 0f) * (effectStr * 0.6f * pulse);

            //四向短臂
            Vector2[] dirs = {
                new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
            };

            foreach (var dir in dirs) {
                for (int i = 0; i < 3; i++) {
                    float dist = 6f + i * (armLength / 3f);
                    Vector2 pos = center + dir * dist;
                    float fade = 1f - i / 3f;
                    spriteBatch.Draw(glowTex, pos, null, color * fade, 0f, origin,
                        glowScale * (1f - i * 0.15f), SpriteEffects.None, 0f);
                }
            }
        }
    }
}
