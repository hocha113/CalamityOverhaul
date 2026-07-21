using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using InnoVault.Narrative.Presentation.Dialogue;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Onikiri
{
    /// <summary>装饰住边框带,正文墨黑静场</summary>
    internal sealed class OnikiriDialogueSkin : StoryDialogueSkin
    {
        private readonly OnikiriPanelState _state = new();

        public override float PortraitSize => 100;
        public override float Padding => 10;
        /// <summary>墨缘侵蚀,正文再收</summary>
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
            //Alpha 后段再浮现纸垂
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

            //挂轴装裱
            spriteBatch.Draw(pixel, frame, src, new Color(12, 5, 8) * (alpha * 0.92f), 0f, Vector2.Zero, SpriteEffects.None, 0f);
            Common.SkinDrawUtil.DrawRectBorder(spriteBatch, frame, OnikiriPanelState.Deep * (alpha * 0.75f), 2);

            Color rail = new Color(58, 10, 14) * alpha;
            spriteBatch.Draw(pixel, new Rectangle(frame.X - 4, frame.Y - 3, frame.Width + 8, 3), src, rail, 0f, Vector2.Zero, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, new Rectangle(frame.X - 4, frame.Bottom, frame.Width + 8, 3), src, rail * 0.85f, 0f, Vector2.Zero, SpriteEffects.None, 0f);
            //下轴小流苏
            spriteBatch.Draw(pixel, new Rectangle(frame.Center.X - 1, frame.Bottom + 3, 2, 5), src, OnikiriPanelState.Bright * (alpha * 0.55f), 0f, Vector2.Zero, SpriteEffects.None, 0f);
        }

        public override void DrawSpeakerName(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            if (string.IsNullOrEmpty(context.SpeakerName)) {
                return;
            }

            float ease = context.SpeakerSwitchEase;
            float nameAlpha = context.ContentAlpha * ease;
            Vector2 basePos = context.SpeakerRect.Location.ToVector2();

            //朱印砸落
            Vector2 sealCenter = basePos + new Vector2(8f, context.SpeakerRect.Height * 0.5f - 4f);
            float stampScale = 1f + (1f - ease) * 0.55f;
            float stampRot = (1f - ease) * 0.20f;
            OnikiriPanelDraw.DrawSealGlyph(spriteBatch, sealCenter, 15f * stampScale, context.ContentAlpha * ease, stampRot);

            //绯红辉环
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

                    //墨迹未干,尾 1~2 字
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
