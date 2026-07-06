using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using InnoVault.Narrative.Presentation.Dialogue;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Onikiri
{
    /// <summary>
    /// 鬼切对话框:墨染和纸面板 + 顶沿注连墨绸/纸垂/绯月(shader 边沿带) + 朱印名牌 +
    /// 刀痕分隔线 + 墨迹未干打字机。装饰全部住在边框带,正文区保持墨黑静场
    /// </summary>
    internal sealed class OnikiriDialogueSkin : StoryDialogueSkin
    {
        private readonly OnikiriPanelState _state = new();

        public override float PortraitSize => 100;
        public override float Padding => 10;
        /// <summary>墨缘侵蚀 ±4px + 边缘吃墨晕染,正文再收一档</summary>
        public override float TextWrapInset => 12f;

        public override Color TextColor => OnikiriPanelState.Paper;
        public override Color SpeakerColor => OnikiriPanelState.HotWhite;
        public override Color HintColor => new(224, 122, 100);
        public override Color SilhouetteColor => new Color(34, 7, 12) * 0.9f;

        public override void Update(DialogueLayoutContext context) {
            _state.Update(context.PanelRect, context.Alpha > 0.01f);
            _state.TrackTypewriter(context.VisibleChars);
        }

        public override void Reset() => _state.Reset();

        public override void DrawBackground(SpriteBatch spriteBatch, DialogueLayoutContext context)
            => OnikiriPanelDraw.DrawShaderBackground(spriteBatch, context.PanelRect, context.Alpha, _state);

        public override void DrawBackgroundDecorations(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            //纸垂等面板基本展开(Alpha 后段)再浮现,避免拔刀揭示期悬空
            float decoAlpha = MathHelper.Clamp((context.Alpha - 0.55f) / 0.45f, 0f, 1f);
            if (decoAlpha > 0.01f) {
                OnikiriPanelDraw.DrawShide(spriteBatch, context.PanelRect, decoAlpha, _state.SwayTimer);
            }
            _state.DrawPetals(spriteBatch, context.Alpha);
        }

        public override void DrawPortraitFrame(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Rectangle frame = context.PortraitRect;
            float alpha = context.Alpha;

            //挂轴式装裱:墨底 + 深红框线 + 上下轴杆
            spriteBatch.Draw(pixel, frame, src, new Color(12, 5, 8) * (alpha * 0.92f), 0f, Vector2.Zero, SpriteEffects.None, 0f);
            Common.SkinDrawUtil.DrawRectBorder(spriteBatch, frame, OnikiriPanelState.Deep * (alpha * 0.75f), 2);

            Color rail = new Color(58, 10, 14) * alpha;
            spriteBatch.Draw(pixel, new Rectangle(frame.X - 4, frame.Y - 3, frame.Width + 8, 3), src, rail, 0f, Vector2.Zero, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, new Rectangle(frame.X - 4, frame.Bottom, frame.Width + 8, 3), src, rail * 0.85f, 0f, Vector2.Zero, SpriteEffects.None, 0f);
            //下轴中央一缕小流苏
            spriteBatch.Draw(pixel, new Rectangle(frame.Center.X - 1, frame.Bottom + 3, 2, 5), src, OnikiriPanelState.Bright * (alpha * 0.55f), 0f, Vector2.Zero, SpriteEffects.None, 0f);
        }

        public override void DrawSpeakerName(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            if (string.IsNullOrEmpty(context.SpeakerName)) {
                return;
            }

            float ease = context.SpeakerSwitchEase;
            float nameAlpha = context.ContentAlpha * ease;
            Vector2 basePos = context.SpeakerRect.Location.ToVector2();

            //朱印盖章:缩放砸落 + 回正的余旋
            Vector2 sealCenter = basePos + new Vector2(8f, context.SpeakerRect.Height * 0.5f - 4f);
            float stampScale = 1f + (1f - ease) * 0.55f;
            float stampRot = (1f - ease) * 0.20f;
            OnikiriPanelDraw.DrawSealGlyph(spriteBatch, sealCenter, 15f * stampScale, context.ContentAlpha * ease, stampRot);

            //名字带极淡绯红辉环
            Vector2 namePos = basePos + new Vector2(21f, 0f);
            namePos.Y -= (1f - ease) * 6f;
            Color glow = OnikiriPanelState.Bright * (nameAlpha * 0.34f);
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4f;
                Utils.DrawBorderString(spriteBatch, context.SpeakerName, namePos + ang.ToRotationVector2() * 1.3f * ease, glow, NameScale);
            }
            Utils.DrawBorderString(spriteBatch, context.SpeakerName, namePos, SpeakerColor * nameAlpha, NameScale);
        }

        public override void DrawDivider(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            Vector2 start = new(context.SpeakerRect.X, context.SpeakerRect.Bottom - 3);
            Vector2 end = new(context.SpeakerRect.Right, context.SpeakerRect.Bottom - 3);
            OnikiriPanelDraw.DrawTaperedSlash(spriteBatch, start, end, 2.1f, 1.6f, context.ContentAlpha * 0.9f);
        }

        public override void DrawText(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            int remaining = context.VisibleChars;
            float inkStrength = _state.InkStrength;

            for (int i = 0; i < context.WrappedLines.Length; i++) {
                string fullLine = context.WrappedLines[i];
                bool isRevealLine = remaining < fullLine.Length;
                string draw = isRevealLine ? fullLine[..Math.Max(0, remaining)] : fullLine;

                if (draw.Length > 0) {
                    Vector2 pos = new(context.TextRect.X, context.TextRect.Y + i * context.LineHeight);
                    Utils.DrawBorderString(spriteBatch, draw, pos, TextColor * context.ContentAlpha, TextScale);

                    //墨迹未干:最新 1~2 个字符叠一层随时间褪去的绯红
                    if (isRevealLine && inkStrength > 0.02f) {
                        int tail = Math.Min(2, draw.Length);
                        string prefix = draw[..^tail];
                        string tailStr = draw[^tail..];
                        float prefixW = context.Font.MeasureString(prefix).X * TextScale;
                        Utils.DrawBorderString(spriteBatch, tailStr, pos + new Vector2(prefixW, 0f),
                            OnikiriPanelState.Bright * (context.ContentAlpha * 0.8f * inkStrength), TextScale);
                    }
                }

                remaining -= fullLine.Length;
                if (remaining <= 0) {
                    break;
                }
            }
        }
    }
}
