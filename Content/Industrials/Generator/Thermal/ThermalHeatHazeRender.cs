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
    /// 火力发电机热浪 RenderHandle——EndCapture 扫描高温 TP，<see cref="EffectLoader.ThermalHeatHaze"/> 批量扭曲
    /// </summary>
    internal class ThermalHeatHazeRender : RenderHandle
    {
        /// <summary>着色器最大支持热源数，需与 fx 中的 MAX_SOURCES 保持一致</summary>
        private const int MaxSources = 8;
        /// <summary>低于此温度的发电机不会产生热浪</summary>
        private const float MinTemperature = 60f;
        /// <summary>边界外扩像素，让靠近屏幕边缘的发电机也能影响画面</summary>
        private const int ScreenMargin = 240;

        private static readonly Vector4[] _sources = new Vector4[MaxSources];
        private static int _sourceCount;
        private static readonly List<(float weight, Vector4 data)> _candidates = new(32);

        public override float Weight => 1.06f;

        /// <summary>收集热源，世界坐标→归一化屏幕坐标</summary>
        private static void CollectSources() {
            _sourceCount = 0;
            _candidates.Clear();

            if (Main.dedServ || !TileProcessorLoader.LoadenTP) {
                return;
            }

            float screenW = Main.screenWidth;
            float screenH = Main.screenHeight;
            //GameViewMatrix.Zoom 是以屏幕中心为锚点的画面缩放，世界点投影到屏幕需要按此换算
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            if (zoom.X <= 0f) zoom.X = 1f;
            if (zoom.Y <= 0f) zoom.Y = 1f;
            Vector2 screenCenterPx = new(screenW * 0.5f, screenH * 0.5f);
            //缩放后玩家实际能看到的世界范围（以世界坐标计），用于剔除离屏发电机
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

                //温度比例驱动的强度，正在燃烧时给一个底值，避免冷却阶段戛然而止
                float maxTemp = data.MaxTemperature > 0 ? data.MaxTemperature : 600f;
                float tempRatio = MathHelper.Clamp(data.Temperature / maxTemp, 0f, 1f);
                float intensity = MathHelper.Clamp((tempRatio - 0.05f) / 0.95f, 0f, 1f);
                if (data.IsBurning) {
                    intensity = MathF.Max(intensity, 0.3f);
                }
                if (intensity <= 0.02f) {
                    continue;
                }

                //热源中心：取碰撞箱中心略上偏，符合"热气向上"的视觉直觉
                Vector2 worldCenter = hb.Center.ToVector2() + new Vector2(0f, -hb.Height * 0.25f);

                //世界坐标→屏幕像素，需考虑 GameViewMatrix.Zoom 的中心缩放
                Vector2 worldOffset = worldCenter - viewWorldCenter;
                Vector2 screenPx = screenCenterPx + worldOffset * zoom;
                Vector2 normalized = new(screenPx.X / screenW, screenPx.Y / screenH);

                //影响半径：随强度从 140px 增长到 320px，再按缩放放大并归一化到 Y 方向尺度
                float radiusPx = MathHelper.Lerp(140f, 320f, intensity) * zoom.Y;
                float radiusNorm = radiusPx / screenH;

                //权重：强度优先，距离次之，让玩家附近的热源优先入选
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

            //把当前屏幕拷贝到交换 RT
            gd.SetRenderTarget(screenSwap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            //写入着色器参数
            shader.Parameters["screenSize"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
            shader.Parameters["sources"]?.SetValue(_sources);
            shader.Parameters["sourceCount"]?.SetValue(_sourceCount);
            shader.Parameters["globalTime"]?.SetValue((float)Main.timeForVisualEffects * 0.018f);
            shader.Parameters["uNoise"]?.SetValue(CWRAsset.Extra_193.Value);

            //把扭曲后的画面写回主屏 RT
            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(screenSwap, Vector2.Zero, Color.White);
            sb.End();
        }
    }
}
