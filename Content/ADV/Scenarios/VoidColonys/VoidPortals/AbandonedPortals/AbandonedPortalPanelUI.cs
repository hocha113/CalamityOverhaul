using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.VoidColonys.VoidPortals.AbandonedPortals
{
    internal class AbandonedPortalPanelUI : UIHandle
    {
        private const int EdgePad = 14;
        private static Rectangle panelRect;

        //轻量动画状态
        private float shaderTime;
        private float glitchTimer;
        private float lastRepairProgress;
        private float repairAccel;

        public override bool Active => !Main.gameMenu
            && (AbandonedPortalSession.IsOpen || AbandonedPortalSession.OpenProgress > 0.005f);

        public override void Update() {
            shaderTime += 1f / 60f;
            if (shaderTime > 100f) shaderTime -= 100f;

            //根据状态衰减/驱动故障强度
            AbandonedPortalTP portal = AbandonedPortalSession.CurrentPortal;
            float targetGlitch;
            if (portal == null) {
                targetGlitch = 0.4f;
            }
            else {
                targetGlitch = portal.State switch {
                    AbandonedPortalTP.RepairState.Broken => 0.85f,
                    AbandonedPortalTP.RepairState.Repairing => 0.45f - portal.RepairProgress * 0.30f,
                    _ => 0.10f,
                };
            }
            glitchTimer = MathHelper.Lerp(glitchTimer, targetGlitch, 0.08f);

            float curr = portal?.RepairProgress ?? 0f;
            repairAccel = MathHelper.Lerp(repairAccel, MathHelper.Clamp((curr - lastRepairProgress) * 60f, 0f, 1f), 0.15f);
            lastRepairProgress = curr;

            UIHitBox = AbandonedPortalSession.OpenProgress > 0.05f ? panelRect : Rectangle.Empty;
        }

        public override void Draw(SpriteBatch sb) {
            AbandonedPortalTP portal = AbandonedPortalSession.CurrentPortal;
            if (portal == null) return;

            float open = AbandonedPortalSession.OpenProgress;
            float eased = 1f - (float)Math.Pow(1f - MathHelper.Clamp(open, 0f, 1f), 3);
            int width = 760;
            int height = 410;
            float scale = 0.92f + eased * 0.08f;
            int drawW = (int)(width * scale);
            int drawH = (int)(height * scale);
            Rectangle rect = new(Main.screenWidth / 2 - drawW / 2, Main.screenHeight / 2 - drawH / 2, drawW, drawH);
            panelRect = rect;
            Texture2D px = TextureAssets.MagicPixel.Value;

            //背景压暗
            sb.Draw(px, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.Black * (0.55f * eased));

            //着色器面板（含外发光范围）
            DrawShaderPanel(sb, rect, eased, portal);

            //内容
            DrawPanelContent(sb, rect, portal, eased);

            if (rect.Contains(Main.mouseX, Main.mouseY) && eased > 0.2f) {
                Main.LocalPlayer.mouseInterface = true;
                Main.LocalPlayer.cursorItemIconEnabled = false;
            }
        }

        //═══ 1. 使用 AbandonedPortalPanel 着色器渲染面板背景 ═══
        private void DrawShaderPanel(SpriteBatch sb, Rectangle rect, float alpha, AbandonedPortalTP portal) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            Asset<Effect> effectAsset = EffectLoader.AbandonedPortalPanel;

            if (effectAsset?.Value == null) {
                DrawFallbackPanel(sb, rect, alpha, portal);
                return;
            }

            Rectangle ext = rect;
            ext.Inflate(EdgePad, EdgePad);

            float repair = portal.RepairProgress;
            float state = portal.State switch {
                AbandonedPortalTP.RepairState.Repaired => 2f,
                AbandonedPortalTP.RepairState.Repairing => 1f,
                _ => 0f,
            };

            Effect effect = effectAsset.Value;
            effect.Parameters["uTime"]?.SetValue(shaderTime);
            effect.Parameters["uAlpha"]?.SetValue(alpha * 0.97f);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(ext.Width, ext.Height));
            effect.Parameters["uEdgePad"]?.SetValue((float)EdgePad);
            effect.Parameters["uRepair"]?.SetValue(repair);
            effect.Parameters["uState"]?.SetValue(state);
            effect.Parameters["uGlitch"]?.SetValue(MathHelper.Clamp(glitchTimer, 0f, 1f));

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(px, ext, Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }

        //降级面板：着色器未加载时仍能显示
        private static void DrawFallbackPanel(SpriteBatch sb, Rectangle rect, float alpha, AbandonedPortalTP portal) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            Color bg = new Color(14, 12, 9) * (0.95f * alpha);
            Color edge = new Color(190, 110, 50) * alpha;
            Color rust = new Color(140, 70, 38) * (0.7f * alpha);

            sb.Draw(px, rect, bg);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 3), edge);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), rust);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 2, rect.Height), edge * 0.85f);
            sb.Draw(px, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), rust * 0.85f);
        }

        //═══ 2. 面板内容：标题、状态、诊断、进度条、按钮 ═══
        private void DrawPanelContent(SpriteBatch sb, Rectangle rect, AbandonedPortalTP portal, float alpha) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            //── 状态色调：损毁=橙红，修复中=蓝橙，已修复=青蓝 ──
            Color accent = portal.State switch {
                AbandonedPortalTP.RepairState.Repaired => new Color(140, 230, 250),
                AbandonedPortalTP.RepairState.Repairing => Color.Lerp(new Color(245, 130, 60), new Color(150, 220, 245), portal.RepairProgress),
                _ => new Color(245, 110, 50),
            };
            Color title = new Color(240, 235, 220) * alpha;
            Color body = new Color(196, 200, 196) * alpha;
            Color dim = new Color(120, 110, 96) * alpha;
            Color warn = new Color(255, 170, 90) * alpha;

            // ── 标题 + 状态徽章 ──
            int padX = 36;
            int padY = 30;
            Vector2 titlePos = new(rect.X + padX, rect.Y + padY);
            Utils.DrawBorderString(sb, AbandonedPortalStrings.Title.Value, titlePos, title, 0.95f);

            //右上角状态徽章
            string status = portal.State switch {
                AbandonedPortalTP.RepairState.Repaired => AbandonedPortalStrings.StatusRepaired.Value,
                AbandonedPortalTP.RepairState.Repairing => AbandonedPortalStrings.StatusRepairing.Value,
                _ => AbandonedPortalStrings.StatusBroken.Value,
            };
            float blink = MathF.Sin(shaderTime * (portal.State == AbandonedPortalTP.RepairState.Broken ? 4.5f : 1.6f)) * 0.3f + 0.7f;
            DrawStatusBadge(sb, new Rectangle(rect.Right - 232, rect.Y + 26, 200, 26), status, accent * (alpha * blink));

            //── 副标题 ──
            string subtitle = portal.State switch {
                AbandonedPortalTP.RepairState.Repairing => AbandonedPortalStrings.RepairingSubtitle.Value,
                AbandonedPortalTP.RepairState.Repaired => AbandonedPortalStrings.RepairedSubtitle.Value,
                _ => AbandonedPortalStrings.BrokenSubtitle.Value,
            };
            Utils.DrawBorderString(sb, subtitle, new Vector2(rect.X + padX, rect.Y + padY + 36f), accent * alpha, 0.74f);

            //装饰：分隔线
            DrawDecoLine(sb, new Rectangle(rect.X + padX, rect.Y + padY + 64, rect.Width - padX * 2, 2), accent * (alpha * 0.6f), alpha);

            //── 正文（带终端字头） ──
            string body1 = portal.State switch {
                AbandonedPortalTP.RepairState.Repairing => AbandonedPortalStrings.RepairingBody.Value,
                AbandonedPortalTP.RepairState.Repaired => AbandonedPortalStrings.RepairedBody.Value,
                _ => AbandonedPortalStrings.BrokenBody.Value,
            };
            string[] wrapped = Utils.WordwrapString(body1, font, rect.Width - 90, 6, out _);
            float bodyY = rect.Y + padY + 78f;
            for (int i = 0; i < wrapped.Length; i++) {
                if (string.IsNullOrEmpty(wrapped[i])) continue;
                Utils.DrawBorderString(sb, wrapped[i], new Vector2(rect.X + padX, bodyY + i * 22f), body, 0.66f);
            }

            //── 诊断行（终端式 [DIAG] 标签） ──
            string diag = portal.State switch {
                AbandonedPortalTP.RepairState.Repairing => AbandonedPortalStrings.DiagnosticRepairing.Value,
                AbandonedPortalTP.RepairState.Repaired => AbandonedPortalStrings.DiagnosticRepaired.Value,
                _ => AbandonedPortalStrings.DiagnosticBroken.Value,
            };
            string diagFull = AbandonedPortalStrings.DiagnosticHeader.Value + " " + diag;
            Utils.DrawBorderString(sb, diagFull, new Vector2(rect.X + padX, rect.Bottom - 156f), dim, 0.62f);

            //── 校准进度条 ──
            Rectangle progressRect = new(rect.X + padX, rect.Bottom - 124, rect.Width - padX * 2, 24);
            DrawRepairBar(sb, progressRect, portal.RepairProgress, accent, alpha);

            int percent = (int)(portal.RepairProgress * 100f);
            string progressText = string.Format(AbandonedPortalStrings.ProgressFormat.Value, percent);
            Utils.DrawBorderString(sb, progressText,
                new Vector2(rect.X + padX, progressRect.Bottom + 4f),
                portal.CanTeleport ? accent * alpha : warn, 0.62f);

            //── 按钮区 ──
            int btnH = 38;
            int btnPadY = rect.Bottom - 60;
            Rectangle close = new(rect.X + padX, btnPadY, 130, btnH);
            Rectangle primary = new(rect.Right - 274, btnPadY, 240, btnH);

            DrawTechButton(sb, close, AbandonedPortalStrings.Close.Value, dim * 1.6f, alpha,
                AbandonedPortalSession.RequestClose, false);

            if (portal.State == AbandonedPortalTP.RepairState.Broken) {
                DrawTechButton(sb, primary, AbandonedPortalStrings.StartRepair.Value, accent, alpha,
                    () => portal.StartRepair(), true);
            }
            else if (portal.State == AbandonedPortalTP.RepairState.Repaired) {
                DrawTechButton(sb, primary, AbandonedPortalStrings.Teleport.Value, accent, alpha,
                    () => portal.StartTransport(Main.LocalPlayer), true);
            }
            else {
                //修复中：进度按钮（不可点击，呼吸动效）
                DrawProgressButton(sb, primary, accent, portal.RepairProgress, alpha);
            }
        }

        //═══ 装饰：分隔线，左端有起始符号 ═══
        private static void DrawDecoLine(SpriteBatch sb, Rectangle rect, Color c, float alpha) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            //起始三角符号 ▌
            sb.Draw(px, new Rectangle(rect.X, rect.Y - 3, 4, 8), c);
            sb.Draw(px, new Rectangle(rect.X + 6, rect.Y, rect.Width - 6, rect.Height), c * 0.85f);
            //短装饰
            sb.Draw(px, new Rectangle(rect.Right - 26, rect.Y - 3, 26, 1), c * 0.7f);
            sb.Draw(px, new Rectangle(rect.Right - 12, rect.Y - 5, 12, 1), c * 0.5f);
        }

        //═══ 状态徽章 ═══
        private static void DrawStatusBadge(SpriteBatch sb, Rectangle rect, string text, Color color) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            sb.Draw(px, rect, Color.Black * (color.A / 255f * 0.45f));
            //左端 6px 浓色条
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 4, rect.Height), color);
            sb.Draw(px, new Rectangle(rect.X + 4, rect.Y, rect.Width - 4, 1), color * 0.85f);
            sb.Draw(px, new Rectangle(rect.X + 4, rect.Bottom - 1, rect.Width - 4, 1), color * 0.6f);

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 size = font.MeasureString(text) * 0.55f;
            Utils.DrawBorderString(sb, text,
                new Vector2(rect.X + 12, rect.Center.Y - size.Y * 0.5f),
                color, 0.55f);
        }

        //═══ 修复进度条（带流光） ═══
        private void DrawRepairBar(SpriteBatch sb, Rectangle rect, float progress, Color accent, float alpha) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            //凹槽
            sb.Draw(px, rect, Color.Black * (0.65f * alpha));
            //内边框
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), accent * (alpha * 0.55f));
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), accent * (alpha * 0.4f));

            //填充
            int fillW = (int)(rect.Width * MathHelper.Clamp(progress, 0f, 1f));
            if (fillW > 0) {
                Rectangle fill = new(rect.X, rect.Y, fillW, rect.Height);
                Color fillColor = accent * (alpha * 0.85f);
                sb.Draw(px, fill, fillColor * 0.25f);
                sb.Draw(px, new Rectangle(fill.X, fill.Y, fill.Width, 2), fillColor);
                sb.Draw(px, new Rectangle(fill.X, fill.Bottom - 2, fill.Width, 2), fillColor * 0.7f);

                //流光
                float flow = shaderTime * 0.55f % 1f;
                int flowX = fill.X + (int)(flow * fill.Width);
                int beam = 28;
                for (int dx = -beam; dx <= beam; dx++) {
                    int x = flowX + dx;
                    if (x < fill.X || x >= fill.Right) continue;
                    float f = 1f - Math.Abs(dx) / (float)beam;
                    sb.Draw(px, new Rectangle(x, fill.Y, 1, fill.Height), Color.White * (alpha * 0.35f * f * f));
                }
            }

            //刻度（每 10%）
            for (int i = 1; i < 10; i++) {
                int x = rect.X + rect.Width * i / 10;
                int h = i % 5 == 0 ? rect.Height : rect.Height / 2;
                sb.Draw(px, new Rectangle(x, rect.Bottom - h, 1, h), accent * (alpha * 0.25f));
            }
        }

        //═══ 主按钮：带边框、悬停发光 ═══
        private void DrawTechButton(SpriteBatch sb, Rectangle rect, string text, Color color, float alpha,
            Action onClick, bool isPrimary) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            bool hover = rect.Contains(Main.mouseX, Main.mouseY);

            //背景
            Color bg = (hover ? color * 0.32f : Color.Black * 0.40f) * alpha;
            sb.Draw(px, rect, bg);

            //双层边框
            Color edge = color * (alpha * (hover ? 1f : 0.7f));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), edge);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), edge * 0.7f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), edge * 0.85f);
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), edge * 0.55f);

            //四角小切角
            int cs = 5;
            sb.Draw(px, new Rectangle(rect.X, rect.Y, cs, 2), color * alpha);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 2, cs), color * alpha);
            sb.Draw(px, new Rectangle(rect.Right - cs, rect.Bottom - 2, cs, 2), color * (alpha * 0.7f));
            sb.Draw(px, new Rectangle(rect.Right - 2, rect.Bottom - cs, 2, cs), color * (alpha * 0.7f));

            //主按钮悬停时左端添加流动条
            if (isPrimary && hover) {
                float w = shaderTime * 1.4f % 1f * rect.Width;
                int beam = 64;
                for (int dx = -beam; dx <= beam; dx++) {
                    int x = rect.X + (int)w + dx;
                    if (x < rect.X || x >= rect.Right) continue;
                    float f = 1f - Math.Abs(dx) / (float)beam;
                    sb.Draw(px, new Rectangle(x, rect.Y + 2, 1, rect.Height - 4), color * (alpha * 0.28f * f * f));
                }
            }

            //按钮标识（左端 ▌）
            sb.Draw(px, new Rectangle(rect.X + 10, rect.Center.Y - 7, 3, 14), color * alpha);

            //文本
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 size = font.MeasureString(text) * 0.66f;
            Utils.DrawBorderString(sb, text,
                new Vector2(rect.X + 22 + (rect.Width - 22 - size.X) * 0.5f, rect.Center.Y - size.Y * 0.5f),
                Color.White * alpha, 0.66f);

            if (hover) {
                Main.LocalPlayer.mouseInterface = true;
                if (Main.mouseLeft && Main.mouseLeftRelease) {
                    Main.mouseLeftRelease = false;
                    SoundEngine.PlaySound(SoundID.MenuTick);
                    onClick?.Invoke();
                }
            }
        }

        //═══ 进度按钮：修复进行中显示，无法点击 ═══
        private void DrawProgressButton(SpriteBatch sb, Rectangle rect, Color color, float progress, float alpha) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            //底色
            sb.Draw(px, rect, Color.Black * (alpha * 0.55f));
            int fillW = (int)(rect.Width * MathHelper.Clamp(progress, 0f, 1f));
            if (fillW > 0) {
                sb.Draw(px, new Rectangle(rect.X, rect.Y, fillW, rect.Height), color * (alpha * 0.30f));
                //顶/底高光
                sb.Draw(px, new Rectangle(rect.X, rect.Y, fillW, 2), color * (alpha * 0.85f));
                sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, fillW, 2), color * (alpha * 0.55f));
            }
            //外边框
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), color * (alpha * 0.55f));
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color * (alpha * 0.4f));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), color * (alpha * 0.55f));
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color * (alpha * 0.4f));

            //文本：CALIBRATING xx%
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string txt = string.Format(AbandonedPortalStrings.ProgressFormat.Value, (int)(progress * 100f));
            Vector2 size = font.MeasureString(txt) * 0.66f;
            float pulse = MathF.Sin(shaderTime * 3.4f) * 0.18f + 0.82f;
            Utils.DrawBorderString(sb, txt,
                new Vector2(rect.Center.X - size.X * 0.5f, rect.Center.Y - size.Y * 0.5f),
                color * (alpha * pulse), 0.66f);
        }
    }
}
