using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults
{
    /// <summary>
    /// 湖藏过程化绘制工具箱（湖窗退役后仍是沉影笔/伞章/血水物品的共用家）：
    /// 沉物用 KikasaItemForm 血水材质，沉影走 KikasaSunkEffigy，亮件走加色层。
    /// DrawPanel 的撕纸窗面暂无消费者，留给后续需要小窗的场合。
    /// </summary>
    internal static class KikasaVaultRenderer
    {
        private static Texture2D Pixel => VaultAsset.placeholder2.Value;

        /// <summary>
        /// 面板底：撕开的湿纸口子里看血湖。
        /// open 驱动撕开孔径，waterY 是当前水位（开窗时湖水在窗里涨起），
        /// hoverX01 为悬停列在面板内的 uv.x（无悬停给 -1），hoverGlow 是列血光强度
        /// </summary>
        public static void DrawPanel(SpriteBatch sb, Rectangle rect, float alpha, float stir,
            float open, float waterY, float hoverX01, float hoverGlow) {
            if (rect.Width < 4 || rect.Height < 4 || alpha < 0.01f || open <= 0.002f) {
                return;
            }
            Effect effect = EffectLoader.KikasaVaultPanel?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                DrawPanelCPU(sb, rect, alpha, open, waterY);
                return;
            }
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(rect.Width, rect.Height));
            effect.Parameters["uWaterY"]?.SetValue(waterY);
            effect.Parameters["uSlitY"]?.SetValue(KikasaVaultTheme.WaterLineY);
            effect.Parameters["uOpen"]?.SetValue(MathHelper.Clamp(open, 0f, 1f));
            effect.Parameters["uStir"]?.SetValue(MathHelper.Clamp(stir, 0f, 1f));
            effect.Parameters["uHoverX"]?.SetValue(hoverX01);
            effect.Parameters["uHoverGlow"]?.SetValue(MathHelper.Clamp(hoverGlow, 0f, 1f));

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            Main.instance.GraphicsDevice.Textures[1] = noise;
            Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            sb.Draw(Pixel, rect, Color.White);
            RestoreUIBatch(sb);
        }

        //CPU 回退：按孔径裁出的平底 + 动态水线一划，不做同心放大的假羽化

        private static void DrawPanelCPU(SpriteBatch sb, Rectangle rect, float alpha,
            float open, float waterY) {
            float openE = 1f - MathF.Pow(1f - MathHelper.Clamp(open, 0f, 1f), 3f);
            int slitY = rect.Y + (int)(rect.Height * KikasaVaultTheme.WaterLineY);
            int top = (int)MathHelper.Lerp(slitY, rect.Y, MathHelper.Clamp(openE * 1.15f, 0f, 1f));
            int bottom = (int)MathHelper.Lerp(slitY, rect.Bottom,
                MathHelper.Clamp((openE - 0.12f) / 0.88f, 0f, 1f));
            Rectangle vis = new(rect.X, top, rect.Width, Math.Max(2, bottom - top));

            Rectangle shadow = vis;
            shadow.Offset(3, 4);
            sb.Draw(Pixel, shadow, Color.Black * (0.45f * alpha));
            sb.Draw(Pixel, vis, KikasaVaultTheme.PanelBg * (0.94f * alpha));
            int waterPix = rect.Y + (int)(rect.Height * MathHelper.Clamp(waterY, 0f, 1f));
            if (waterPix < vis.Bottom - 2) {
                int wy = Math.Max(waterPix, vis.Y);
                Rectangle water = new(vis.X, wy, vis.Width, vis.Bottom - wy);
                sb.Draw(Pixel, water, KikasaVaultTheme.Deep * (0.5f * alpha));
                DrawLine(sb, new Vector2(vis.Left + 4, wy), new Vector2(vis.Right - 4, wy),
                    1.6f, KikasaVaultTheme.Foam * (0.5f * alpha));
            }
            Color edge = KikasaVaultTheme.Blood * (0.4f * alpha);
            DrawLine(sb, new Vector2(vis.Left, vis.Top), new Vector2(vis.Right, vis.Top), 1.2f, edge);
            DrawLine(sb, new Vector2(vis.Left, vis.Bottom), new Vector2(vis.Right, vis.Bottom), 1.2f, edge * 0.7f);
            DrawLine(sb, new Vector2(vis.Left, vis.Top), new Vector2(vis.Left, vis.Bottom), 1.2f, edge * 0.85f);
            DrawLine(sb, new Vector2(vis.Right, vis.Top), new Vector2(vis.Right, vis.Bottom), 1.2f, edge * 0.85f);
        }

        //==================== 沉物绘制 ====================

        /// <summary>进入血水物品绘制段：Immediate + 噪声挂载；随后逐件 DrawFormItem</summary>
        public static bool BeginItemBatch(SpriteBatch sb, out Effect formEffect) {
            formEffect = EffectLoader.KikasaItemForm?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool shaderOk = formEffect != null && noise != null;
            sb.End();
            //非整数缩小适配槽位，Point 采样会闪锯齿，走 Linear
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
            if (shaderOk) {
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            }
            return shaderOk;
        }

        public static void EndItemBatch(SpriteBatch sb) => RestoreUIBatch(sb);

        /// <summary>血水态物品：form 1=全血水 0=真身，UI 用斑驳交融模式</summary>
        public static void DrawFormItem(SpriteBatch sb, Effect formEffect, bool shaderOk,
            int itemType, Vector2 center, float form, float seed, float alpha) {

            Main.instance.LoadItem(itemType);
            Texture2D tex = TextureAssets.Item[itemType]?.Value;
            if (tex == null) {
                return;
            }
            Rectangle frame = Main.itemAnimations[itemType]?.GetFrame(tex) ?? tex.Frame();
            float fit = KikasaVaultTheme.SlotFit;
            float scale = MathF.Min(1f, fit / MathF.Max(frame.Width, frame.Height));
            Vector2 origin = frame.Size() * 0.5f;

            Color color;
            if (shaderOk) {
                formEffect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                formEffect.Parameters["uSeed"]?.SetValue(seed);
                formEffect.Parameters["uForm"]?.SetValue(form);
                formEffect.Parameters["uDissolve"]?.SetValue(0f);
                formEffect.Parameters["uScanMode"]?.SetValue(0f);
                formEffect.Parameters["uUvRect"]?.SetValue(new Vector4(
                    frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                    frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
                formEffect.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                formEffect.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
                formEffect.CurrentTechnique.Passes[0].Apply();
                color = new Color(255, 255, 255, (byte)(alpha * 255f));
            }
            else {
                color = Color.Lerp(Color.White, KikasaVaultTheme.Blood, form) * alpha;
            }

            sb.Draw(tex, center, frame, color, 0f, origin, scale, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 湖底沉影（记忆/伞奴共用）：贴图只当形状模板，材质全由 KikasaSunkEffigy.fx 承担。
        /// 按 NPC 类型取首帧；细节见另一重载
        /// </summary>
        public static void DrawSunkEffigy(SpriteBatch sb, int npcType, Vector2 center,
            float fit, float alpha, float submerge, float depth, bool tamed, bool absent,
            float rain, float stir, Color fallbackTint) {
            Main.instance.LoadNPC(npcType);
            Texture2D tex = TextureAssets.Npc[npcType]?.Value;
            if (tex == null) {
                return;
            }
            int frameCount = Math.Max(Main.npcFrameCount[npcType], 1);
            Rectangle frameRect = new(0, 0, tex.Width, tex.Height / frameCount);
            DrawSunkEffigy(sb, tex, frameRect, center, fit, alpha, submerge, depth,
                tamed, absent, rain, stir, npcType * 0.173f, fallbackTint, SpriteEffects.None);
        }

        /// <summary>
        /// 湖底沉影核心：submerge 0=干湖泥痕 1=水下沉影；depth 越深折射越大越沉入水色；
        /// tamed=可驱使（形凝得住+缘线+余烬沿缘缓移），absent=鬼奴在外（负形空缺，不是消失）。
        /// 自管 Immediate 批与噪声挂载，画完复原 UI 默认批；着色器缺编退近黑剪影
        /// </summary>
        public static void DrawSunkEffigy(SpriteBatch sb, Texture2D tex, Rectangle frameRect,
            Vector2 center, float fit, float alpha, float submerge, float depth,
            bool tamed, bool absent, float rain, float stir, float seed,
            Color fallbackTint, SpriteEffects flip) {
            if (tex == null || alpha <= 0.01f) {
                return;
            }
            float scale = MathF.Min(1f, fit / MathF.Max(frameRect.Width, frameRect.Height));

            Effect effect = EffectLoader.KikasaSunkEffigy?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect != null && noise != null) {
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uSeed"]?.SetValue(seed);
                effect.Parameters["uSubmerge"]?.SetValue(MathHelper.Clamp(submerge, 0f, 1f));
                effect.Parameters["uDepth"]?.SetValue(MathHelper.Clamp(depth, 0f, 1f));
                effect.Parameters["uTamed"]?.SetValue(tamed ? 1f : 0f);
                effect.Parameters["uAbsent"]?.SetValue(absent ? 1f : 0f);
                effect.Parameters["uRain"]?.SetValue(MathHelper.Clamp(rain, 0f, 1f));
                effect.Parameters["uStir"]?.SetValue(MathHelper.Clamp(stir, 0f, 1f));
                effect.Parameters["uUvRect"]?.SetValue(new Vector4(
                    frameRect.X / (float)tex.Width, frameRect.Y / (float)tex.Height,
                    frameRect.Width / (float)tex.Width, frameRect.Height / (float)tex.Height));
                effect.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                effect.Parameters["uAspect"]?.SetValue(frameRect.Width / (float)frameRect.Height);
                effect.CurrentTechnique.Passes[0].Apply();
                Color color = new(255, 255, 255, (byte)(MathHelper.Clamp(alpha, 0f, 1f) * 235f));
                sb.Draw(tex, center, frameRect, color, 0f,
                    frameRect.Size() * 0.5f, scale, flip, 0f);
                RestoreUIBatch(sb);
            }
            else {
                //着色器缺编：近黑剪影，状态只靠透明度分档，在外最淡，未驯服次之
                float stateA = absent ? 0.35f : tamed ? 0.9f : 0.7f;
                Color ink = Color.Lerp(new Color(12, 6, 9), fallbackTint, 0.35f) * (alpha * stateA);
                sb.Draw(tex, center, frameRect, ink, 0f,
                    frameRect.Size() * 0.5f, scale, flip, 0f);
            }
        }

        /// <summary>
        /// 记忆键通用沉影：正键走 NPC 贴图重载，负键取物品贴图走纹理重载。
        /// 转盘/湖心景/湖窗共用这一支笔，别在各屏重抄
        /// </summary>
        public static void DrawEffigyByKey(SpriteBatch sb, int key, Vector2 center, float fit,
            float alpha, float submerge, bool tamed, bool absent, float rain, float stir,
            Color fallbackTint) {
            if (key > 0) {
                DrawSunkEffigy(sb, key, center, fit, alpha,
                    submerge, 0.35f, tamed, absent, rain, stir, fallbackTint);
                return;
            }
            if (key == 0) {
                return;
            }
            int itemType = -key;
            Main.instance.LoadItem(itemType);
            Texture2D tex = TextureAssets.Item[itemType]?.Value;
            if (tex == null) {
                return;
            }
            DrawSunkEffigy(sb, tex, new Rectangle(0, 0, tex.Width, tex.Height),
                center, fit, alpha, submerge, 0.35f, tamed, absent, rain, stir,
                itemType * 0.173f, fallbackTint, SpriteEffects.None);
        }

        //==================== 伞章 ====================

        //归一 [-1,1] 空间：圆拱伞盖 + 四瓣荷缘；顶针、中棒弯钩与两根斜骨

        private const string SealCanopy =
            "M -0.92 0.14 C -0.55 -0.66 0.55 -0.66 0.92 0.14 "
            + "Q 0.66 0.03 0.46 0.15 Q 0.23 0.02 0 0.15 "
            + "Q -0.23 0.02 -0.46 0.15 Q -0.66 0.03 -0.92 0.14";

        private const string SealFrame =
            "M 0 -0.62 L 0 0.88 Q 0.02 1.0 0.2 0.92 "
            + "M 0 -0.44 L -0.58 0.02 M 0 -0.44 L 0.58 0.02";

        /// <summary>
        /// 伞章：伞骨淡线垫底，伞盖粗笔带亮芯，笔序随 reveal 揭示；描完伞面一段掠光缓巡。
        /// 颜色由调用方定，湖心景传 KikasaHudTheme 双形态色随鬼雨浸染
        /// </summary>
        public static void DrawSeal(SpriteBatch sb, Vector2 center, float scale, float alpha,
            float time, float reveal, Color bone, Color canopy, Color core) {
            SvgPath canopyPath = SvgPathPen.Path(SealCanopy);
            SvgPath framePath = SvgPathPen.Path(SealFrame);
            SvgPathPen.Stroke(sb, framePath, center, scale, 0f, bone, 1.2f, alpha * 0.85f, 0f, reveal);
            SvgPathPen.Stroke(sb, canopyPath, center, scale, 0f, canopy, 2.4f, alpha, 0f, reveal, core: core);
            if (reveal >= 0.995f) {
                SvgPathPen.StrokeRunner(sb, canopyPath, center, scale, 0f, core, 2.6f, alpha * 0.5f,
                    time * 0.07f, 0.10f);
            }
        }

        //==================== 加色小件 ====================

        /// <summary>压扁的扩散环（悬停浮圈/提取旋涡）</summary>
        public static void DrawRing(SpriteBatch sb, Vector2 center, float rx, float ry, Color color) {
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            if (ring == null) {
                return;
            }
            Vector2 origin = ring.Size() * 0.5f;
            Vector2 scale = new(rx * 2f / ring.Width, ry * 2f / ring.Height);
            sb.Draw(ring, center, null, color, 0f, origin, scale, SpriteEffects.None, 0f);
        }

        /// <summary>软光点（黑底 SoftGlow：只许加色批或 A=0 用色，暗色走 <see cref="DrawDarkDisc"/>）</summary>
        public static void DrawGlowDot(SpriteBatch sb, Vector2 center, float radius, Color color) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            Vector2 origin = glow.Size() * 0.5f;
            float s = radius * 2f / glow.Width;
            sb.Draw(glow, center, null, color, 0f, origin, s, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 暗色软圆盘：真 alpha 的 Extra_98 才压得出暗形。
        /// 黑底 SoftGlow 在 AlphaBlend 批里画暗色会连黑底一起糊成方块（转盘章底实例）；
        /// ×2 补偿 Extra_98 相对 SoftGlow 更紧的径向衰减
        /// </summary>
        public static void DrawDarkDisc(SpriteBatch sb, Vector2 center, float radius, Color color) {
            Texture2D disc = CWRAsset.Extra_98?.Value;
            if (disc == null) {
                return;
            }
            Vector2 origin = disc.Size() * 0.5f;
            float s = radius * 2f / disc.Width * 2f;
            sb.Draw(disc, center, null, color, 0f, origin, s, SpriteEffects.None, 0f);
        }

        public static void DrawLine(SpriteBatch sb, Vector2 a, Vector2 b, float width, Color color) {
            Vector2 d = b - a;
            float len = d.Length();
            if (len < 0.5f) {
                return;
            }
            float rot = MathF.Atan2(d.Y, d.X);
            sb.Draw(Pixel, a, null, color, rot, new Vector2(0f, 0.5f),
                new Vector2(len / Pixel.Width, width / Pixel.Height), SpriteEffects.None, 0f);
        }

        /// <summary>恢复 UI 默认批次（Deferred + UIScaleMatrix）</summary>
        public static void RestoreUIBatch(SpriteBatch sb) {
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        /// <summary>切到加色批次画亮件，用完 RestoreUIBatch</summary>
        public static void BeginAdditive(SpriteBatch sb) {
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }
    }
}
