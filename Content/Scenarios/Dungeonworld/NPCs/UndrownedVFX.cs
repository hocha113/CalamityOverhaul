using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>
    /// 不溺者水系表现枢纽（纯客户端，零网络包）。
    /// 三类职责：躯体材质 shader 的统一上参口（Boss 与蛰伏体共用同一身皮）、
    /// 一次性水幕/立管水柱事件池、锚涡盘与泄洪拽流的逐帧喂参口。
    /// 事件全部由各端从同步状态节拍确定性自派——旁观者端天然看得到，不需要广播。
    /// 着色器缺编时：躯体口返回 false 由调用方走旧染色路径，事件层静默不画。
    /// 共置类型：UndrownedVFX（静态枢纽）、UndrownedWaterRender（RenderHandle，Weight 1.621）。
    /// </summary>
    internal static class UndrownedVFX
    {
        private static Texture2D Noise => CWRAsset.PerlinNoise?.Value;

        //==================== 躯体材质 ====================

        /// <summary>
        /// 用 UndrownedBody.fx 画一层躯体。返回 false = 着色器/噪声缺编，调用方走旧染色回退。
        /// 源矩形四边内缩 1px + shader 侧 uUvRect 钳制，双通道防帧表渗色。
        /// waterWorldY 传 float.MaxValue = 全干。
        /// </summary>
        internal static bool DrawBody(SpriteBatch sb, Texture2D tex, Rectangle frame, Vector2 pos,
            float rotation, float scale, SpriteEffects fx, Color lightTint, float alpha,
            float waterWorldY, float seed, float wet, float flash) {
            Effect effect = EffectLoader.UndrownedBody?.Value;
            Texture2D noise = Noise;
            if (effect == null || noise == null || alpha <= 0.01f) {
                return false;
            }

            Rectangle inset = new(frame.X + 1, frame.Y + 1, frame.Width - 2, frame.Height - 2);
            Vector2 origin = new(inset.Width * 0.5f, inset.Height * 0.5f);

            //水线映射进帧内 v（忽略旋转的近似：Boss 倾角 ≤0.5rad，缝口随身姿轻晃可接受）
            float drawTop = pos.Y - origin.Y * scale;
            float waterV = waterWorldY >= float.MaxValue * 0.5f
                ? 2f
                : MathHelper.Clamp((waterWorldY - drawTop) / (inset.Height * scale), -0.2f, 2f);

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSeed"]?.SetValue(seed);
            effect.Parameters["uUvRect"]?.SetValue(new Vector4(
                inset.X / (float)tex.Width, inset.Y / (float)tex.Height,
                inset.Width / (float)tex.Width, inset.Height / (float)tex.Height));
            effect.Parameters["uWaterV"]?.SetValue(waterV);
            effect.Parameters["uWet"]?.SetValue(wet);
            effect.Parameters["uFlash"]?.SetValue(flash);
            effect.Parameters["uColDeep"]?.SetValue(Undrowned.CorpseDeep.ToVector3());
            effect.Parameters["uColTeal"]?.SetValue(Undrowned.CorpseTeal.ToVector3());
            effect.Parameters["uColPale"]?.SetValue(Undrowned.FoamWhite.ToVector3());
            effect.Parameters["uColRust"]?.SetValue(Undrowned.RustOrange.ToVector3());

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            //透明度只进 A：shader 里 rgb 会再乘 a 做预乘，整色乘 alpha 会让 rgb 吃 alpha 平方
            Color tint = new(lightTint.R, lightTint.G, lightTint.B, (byte)(255f * MathHelper.Clamp(alpha, 0f, 1f)));
            sb.Draw(tex, pos - Main.screenPosition, inset, tint, rotation, origin, scale, fx, 0f);

            sb.End();
            gd.Textures[1] = null;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return true;
        }

        //==================== 一次性事件池（水幕 / 水柱）====================

        private struct Veil
        {
            internal Vector2 SurfacePos;   //幕底中点（水面线）
            internal float WidthPx;
            internal float HeightPx;
            internal float Seed;
            internal int Life;
            internal int MaxLife;
        }

        private struct Column
        {
            internal Vector2 TopPos;       //管口
            internal float HeightPx;       //管口到落点水面
            internal float WidthPx;        //可见柱宽
            internal float Seed;
            internal int Life;
            internal int MaxLife;
        }

        private static readonly List<Veil> veils = [];
        private static readonly List<Column> columns = [];

        /// <summary>破水/落水/砸浪的水幕事件（surfacePos=幕底水面中点）</summary>
        internal static void PushVeil(Vector2 surfacePos, float widthPx, float heightPx, int frames) {
            if (Main.dedServ || veils.Count > 16) {
                return;
            }
            veils.Add(new Veil {
                SurfacePos = surfacePos,
                WidthPx = widthPx,
                HeightPx = heightPx,
                Seed = (surfacePos.X * 0.013f + surfacePos.Y * 0.007f) % 3.1f,
                Life = 0,
                MaxLife = Math.Max(frames, 10),
            });
        }

        /// <summary>立管泄洪柱事件（涨水仪式；topPos=管口，heightPx 落到水面）</summary>
        internal static void PushColumn(Vector2 topPos, float heightPx, float widthPx, int frames) {
            if (Main.dedServ || columns.Count > 8) {
                return;
            }
            columns.Add(new Column {
                TopPos = topPos,
                HeightPx = heightPx,
                WidthPx = widthPx,
                Seed = topPos.X * 0.017f % 3.1f,
                Life = 0,
                MaxLife = Math.Max(frames, 20),
            });
        }

        //==================== 逐帧喂参口（锚涡盘 / 泄洪拽流层）====================

        private static bool whirlFedThisTick;
        private static Vector2 whirlCenter;
        private static float whirlSurfaceY;
        private static float whirlRadiusPx;
        private static float whirlTrackPx;
        private static float whirlSpin;
        private static float whirlIntensity;

        /// <summary>锚涡盘：Boss 锚涡态每帧喂；停喂即快速熄灭</summary>
        internal static void FeedWhirl(Vector2 center, float surfaceY, float radiusPx,
            float trackRadiusPx, float spin, float intensity) {
            if (Main.dedServ) {
                return;
            }
            whirlFedThisTick = true;
            whirlCenter = center;
            whirlSurfaceY = surfaceY;
            whirlRadiusPx = radiusPx;
            whirlTrackPx = trackRadiusPx;
            whirlSpin = spin;
            whirlIntensity = intensity;
        }

        private static bool drainFedThisTick;
        private static Rectangle drainRect;
        private static Vector2 drainFocus;
        private static float drainStrength;
        /// <summary>池推进的逻辑帧闩（绘制帧可能多于逻辑帧）</summary>
        private static uint lastAdvanceTick;

        /// <summary>泄洪拽流层：死亡演出每帧喂（rect=水域世界矩形，focus=格栅口）</summary>
        internal static void FeedDrain(Rectangle worldRect, Vector2 focusWorld, float strength) {
            if (Main.dedServ) {
                return;
            }
            drainFedThisTick = true;
            drainRect = worldRect;
            drainFocus = focusWorld;
            drainStrength = strength;
        }

        //==================== 池维护与绘制（UndrownedWaterRender 每帧驱动）====================

        internal static void ClearAll() {
            veils.Clear();
            columns.Clear();
            whirlIntensity = 0f;
            drainStrength = 0f;
            whirlFedThisTick = false;
            drainFedThisTick = false;
        }

        internal static void DrawAndAdvance(SpriteBatch sb) {
            Texture2D noise = Noise;
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            Effect flood = EffectLoader.UndrownedFloodFlow?.Value;
            Effect whirl = EffectLoader.UndrownedWhirl?.Value;
            Effect veilFx = EffectLoader.UndrownedBreachVeil?.Value;
            if (noise == null || pixel == null) {
                ClearAll();
                return;
            }

            bool anyDrain = flood != null && drainStrength > 0.02f;
            bool anyWhirl = whirl != null && whirlIntensity > 0.02f;
            bool anyCol = flood != null && columns.Count > 0;
            bool anyVeil = veilFx != null && veils.Count > 0;
            if (anyDrain || anyWhirl || anyCol || anyVeil) {
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;

                if (anyDrain) {
                    DrawDrain(sb, pixel, flood);
                }
                if (anyWhirl) {
                    DrawWhirl(sb, pixel, whirl);
                }
                if (anyCol) {
                    DrawColumns(sb, pixel, flood);
                }
                if (anyVeil) {
                    DrawVeils(sb, pixel, veilFx);
                }

                sb.End();
                gd.Textures[1] = null;
            }

            //池推进按逻辑帧走：高刷新率下绘制帧多于逻辑帧，
            //按绘制帧衰减会让喂参层在两个逻辑帧之间抖闪
            if (Main.gamePaused || Main.GameUpdateCount == lastAdvanceTick) {
                return;
            }
            lastAdvanceTick = Main.GameUpdateCount;
            //包络推进与停喂衰减（喂参口每逻辑帧续票，绘制端消费）
            if (whirlFedThisTick) {
                whirlFedThisTick = false;
            }
            else {
                whirlIntensity *= 0.82f;
            }
            if (drainFedThisTick) {
                drainFedThisTick = false;
            }
            else {
                drainStrength *= 0.82f;
            }
            for (int i = veils.Count - 1; i >= 0; i--) {
                Veil v = veils[i];
                if (++v.Life >= v.MaxLife) {
                    veils.RemoveAt(i);
                }
                else {
                    veils[i] = v;
                }
            }
            for (int i = columns.Count - 1; i >= 0; i--) {
                Column c = columns[i];
                if (++c.Life >= c.MaxLife) {
                    columns.RemoveAt(i);
                }
                else {
                    columns[i] = c;
                }
            }
        }

        private static bool OnScreen(Rectangle worldRect) {
            Rectangle view = new((int)Main.screenPosition.X - 200, (int)Main.screenPosition.Y - 200,
                Main.screenWidth + 400, Main.screenHeight + 400);
            return worldRect.Intersects(view);
        }

        /// <summary>共用调色上载（uniform 是设备全局态：每个调用点全参数重设）</summary>
        private static void SetPalette(Effect effect) {
            effect.Parameters["uDeepColor"]?.SetValue(Undrowned.BogDeep.ToVector3());
            effect.Parameters["uSeaColor"]?.SetValue(Undrowned.BogWater.ToVector3());
            effect.Parameters["uFoamColor"]?.SetValue(Undrowned.FoamWhite.ToVector3());
        }

        private static void DrawDrain(SpriteBatch sb, Texture2D pixel, Effect flood) {
            if (!OnScreen(drainRect)) {
                return;
            }
            SetPalette(flood);
            flood.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            flood.Parameters["uLife"]?.SetValue(MathHelper.Clamp(drainStrength, 0f, 1f));
            flood.Parameters["uDrain"]?.SetValue(0f);
            flood.Parameters["uSeed"]?.SetValue(0.47f);
            flood.Parameters["uFocus"]?.SetValue(new Vector2(
                (drainFocus.X - drainRect.X) / drainRect.Width,
                (drainFocus.Y - drainRect.Y) / drainRect.Height));
            flood.Parameters["uAspect"]?.SetValue(drainRect.Width / (float)drainRect.Height);
            flood.CurrentTechnique = flood.Techniques["TechDrain"];
            flood.CurrentTechnique.Passes[0].Apply();
            sb.Draw(pixel, new Vector2(drainRect.X, drainRect.Y) - Main.screenPosition, null, Color.White,
                0f, Vector2.Zero, new Vector2(drainRect.Width / (float)pixel.Width, drainRect.Height / (float)pixel.Height),
                SpriteEffects.None, 0f);
        }

        private static void DrawWhirl(SpriteBatch sb, Texture2D pixel, Effect whirl) {
            //画布半宽=轨道半径×1.5，透视压扁 0.5 贴在水面线上
            float halfW = whirlRadiusPx * 1.5f;
            float halfH = halfW * 0.5f;
            Vector2 center = new(whirlCenter.X, whirlSurfaceY);
            Rectangle worldRect = new((int)(center.X - halfW), (int)(center.Y - halfH), (int)(halfW * 2f), (int)(halfH * 2f));
            if (!OnScreen(worldRect)) {
                return;
            }
            SetPalette(whirl);
            whirl.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            whirl.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(whirlIntensity, 0f, 1f));
            whirl.Parameters["uSpin"]?.SetValue(whirlSpin);
            whirl.Parameters["uTrackR"]?.SetValue(MathHelper.Clamp(whirlTrackPx / halfW, 0.1f, 0.95f));
            whirl.Parameters["uSeed"]?.SetValue(0.29f);
            whirl.CurrentTechnique.Passes[0].Apply();
            sb.Draw(pixel, center - Main.screenPosition, null, Color.White, 0f,
                pixel.Size() * 0.5f, new Vector2(halfW * 2f / pixel.Width, halfH * 2f / pixel.Height),
                SpriteEffects.None, 0f);
        }

        private static void DrawColumns(SpriteBatch sb, Texture2D pixel, Effect flood) {
            foreach (Column c in columns) {
                //可见柱宽≈画布 0.35：画布宽按折算放大（画布契约随 shader 剖面走）
                float quadW = c.WidthPx / 0.35f;
                Rectangle worldRect = new((int)(c.TopPos.X - quadW * 0.5f), (int)c.TopPos.Y, (int)quadW, (int)c.HeightPx);
                if (!OnScreen(worldRect)) {
                    continue;
                }
                float life = MathHelper.Clamp(c.Life / 18f, 0f, 1f);
                float drain = MathHelper.Clamp((c.Life - (c.MaxLife - 26)) / 26f, 0f, 1f);
                SetPalette(flood);
                flood.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + c.Seed);
                flood.Parameters["uLife"]?.SetValue(life);
                flood.Parameters["uDrain"]?.SetValue(drain);
                flood.Parameters["uSeed"]?.SetValue(c.Seed);
                flood.Parameters["uFocus"]?.SetValue(Vector2.Zero);
                flood.Parameters["uAspect"]?.SetValue(1f);
                flood.CurrentTechnique = flood.Techniques["TechColumn"];
                flood.CurrentTechnique.Passes[0].Apply();
                sb.Draw(pixel, new Vector2(worldRect.X, worldRect.Y) - Main.screenPosition, null, Color.White,
                    0f, Vector2.Zero, new Vector2(quadW / pixel.Width, c.HeightPx / pixel.Height),
                    SpriteEffects.None, 0f);
            }
        }

        private static void DrawVeils(SpriteBatch sb, Texture2D pixel, Effect veilFx) {
            foreach (Veil v in veils) {
                Rectangle worldRect = new((int)(v.SurfacePos.X - v.WidthPx * 0.5f),
                    (int)(v.SurfacePos.Y - v.HeightPx), (int)v.WidthPx, (int)v.HeightPx);
                if (!OnScreen(worldRect)) {
                    continue;
                }
                SetPalette(veilFx);
                veilFx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + v.Seed);
                veilFx.Parameters["uLife"]?.SetValue(MathHelper.Clamp(v.Life / (float)v.MaxLife, 0f, 1f));
                veilFx.Parameters["uSeed"]?.SetValue(v.Seed);
                veilFx.CurrentTechnique.Passes[0].Apply();
                sb.Draw(pixel, new Vector2(worldRect.X, worldRect.Y) - Main.screenPosition, null, Color.White,
                    0f, Vector2.Zero, new Vector2(v.WidthPx / pixel.Width, v.HeightPx / pixel.Height),
                    SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 不溺者水系事件的屏幕层（Weight 1.621，A2 频段）。
    /// EndEntityDraw 进场时没有活动批：自开 Immediate 批逐体上参，画完交还设备状态。
    /// 层序：拽流层 → 锚涡盘 → 立管水柱 → 破水水幕。
    /// </summary>
    internal sealed class UndrownedWaterRender : InnoVault.RenderHandles.RenderHandle
    {
        public override float Weight => 1.621f;

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (Main.gameMenu || Main.dedServ) {
                UndrownedVFX.ClearAll();
                return;
            }
            UndrownedVFX.DrawAndAdvance(spriteBatch);
        }
    }
}
