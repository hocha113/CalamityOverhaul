using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Industrials.Generator.Thermal
{
    /// <summary>
    /// 热力机热浪 RenderHandle，EndCapture 扫高温 TP，<see cref="EffectLoader.ThermalHeatHaze"/> 批量扭曲
    /// </summary>
    internal class ThermalHeatHazeRender : RenderHandle
    {
        /// <summary>与 fx MAX_SOURCES 对齐</summary>
        private const int MaxSources = 8;
        /// <summary>低于此温度无热浪</summary>
        private const float MinTemperature = 60f;
        /// <summary>屏外扩像素，边缘机也能影响</summary>
        private const int ScreenMargin = 240;

        private static readonly Vector4[] _sources = new Vector4[MaxSources];
        private static int _sourceCount;
        private static readonly List<(float weight, Vector4 data)> _candidates = new(32);

        public override float Weight => 1.06f;

        /// <summary>收集热源，世界→归一化屏幕坐标</summary>
        private static void CollectSources() {
            _sourceCount = 0;
            _candidates.Clear();

            if (Main.dedServ || !TileProcessorLoader.LoadenTP) {
                return;
            }

            float screenW = Main.screenWidth;
            float screenH = Main.screenHeight;
            //Zoom 以屏心为锚，投影需换算
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            if (zoom.X <= 0f) zoom.X = 1f;
            if (zoom.Y <= 0f) zoom.Y = 1f;
            Vector2 screenCenterPx = new(screenW * 0.5f, screenH * 0.5f);
            //缩放后可见世界范围，剔离屏
            Vector2 viewWorldHalf = new(screenW * 0.5f / zoom.X, screenH * 0.5f / zoom.Y);
            Vector2 viewWorldCenter = Main.screenPosition + screenCenterPx;
            Rectangle screenRect = new(
                (int)(viewWorldCenter.X - viewWorldHalf.X) - ScreenMargin,
                (int)(viewWorldCenter.Y - viewWorldHalf.Y) - ScreenMargin,
                (int)(viewWorldHalf.X * 2) + ScreenMargin * 2,
                (int)(viewWorldHalf.Y * 2) + ScreenMargin * 2);

            Vector2 playerCenter = Main.LocalPlayer?.Center ?? Main.screenPosition;

            foreach (var baseTP in TileProcessorLoader.TP_InWorld) {
                if (baseTP is not ThermalGeneratorTP tp || !tp.Active) {
                    continue;
                }
                if (tp.MachineData is not ThermalData data) {
                    continue;
                }
                if (data.Temperature < MinTemperature && !data.IsBurning) {
                    continue;
                }

                Rectangle hb = tp.HitBox;
                if (!screenRect.Intersects(hb)) {
                    continue;
                }

                //燃烧时保底强度，防冷却戛止
                float maxTemp = data.MaxTemperature > 0 ? data.MaxTemperature : 600f;
                float tempRatio = MathHelper.Clamp(data.Temperature / maxTemp, 0f, 1f);
                float intensity = MathHelper.Clamp((tempRatio - 0.05f) / 0.95f, 0f, 1f);
                if (data.IsBurning) {
                    intensity = MathF.Max(intensity, 0.3f);
                }
                if (intensity <= 0.02f) {
                    continue;
                }

                //中心略上偏
                Vector2 worldCenter = hb.Center.ToVector2() + new Vector2(0f, -hb.Height * 0.25f);

                Vector2 worldOffset = worldCenter - viewWorldCenter;
                Vector2 screenPx = screenCenterPx + worldOffset * zoom;
                Vector2 normalized = new(screenPx.X / screenW, screenPx.Y / screenH);

                //半径 140~320px，再按 Zoom 归一到 Y
                float radiusPx = MathHelper.Lerp(140f, 320f, intensity) * zoom.Y;
                float radiusNorm = radiusPx / screenH;

                //强度优先，近玩家优先
                float distSq = Vector2.DistanceSquared(worldCenter, playerCenter);
                float weight = intensity * 1000f - distSq * 0.0001f;

                _candidates.Add((weight, new Vector4(normalized.X, normalized.Y, intensity, radiusNorm)));
            }

            if (_candidates.Count == 0) {
                return;
            }

            _candidates.Sort((a, b) => b.weight.CompareTo(a.weight));
            _sourceCount = Math.Min(_candidates.Count, MaxSources);
            for (int i = 0; i < _sourceCount; i++) {
                _sources[i] = _candidates[i].data;
            }
        }

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            CollectSources();
            if (_sourceCount == 0) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                return;
            }
            if (EffectLoader.ThermalHeatHaze == null || !EffectLoader.ThermalHeatHaze.IsLoaded) {
                return;
            }

            Effect shader = EffectLoader.ThermalHeatHaze.Value;

            //屏→交换 RT
            gd.SetRenderTarget(screenSwap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            shader.Parameters["screenSize"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
            shader.Parameters["sources"]?.SetValue(_sources);
            shader.Parameters["sourceCount"]?.SetValue(_sourceCount);
            shader.Parameters["globalTime"]?.SetValue((float)Main.timeForVisualEffects * 0.018f);
            shader.Parameters["uNoise"]?.SetValue(CWRAsset.Extra_193.Value);

            //扭曲后写回主屏
            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(screenSwap, Vector2.Zero, Color.White);
            sb.End();
        }
    }
}
