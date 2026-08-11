using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using InnoVault.Narrative.Presentation.Dialogue;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Kikasa
{
    /// <summary>装饰住边框带,正文湿墨静场</summary>
    internal sealed class KikasaDialogueSkin : StoryDialogueSkin
    {
        private readonly KikasaPanelState _state = new();

        public override float PortraitSize => 100;
        public override float Padding => 10;
        /// <summary>水蚀边缘,正文再收</summary>
        public override float TextWrapInset => 12f;

        public override Color TextColor => KikasaPanelState.Text;
        public override Color SpeakerColor => KikasaPanelState.Moon;
        public override Color HintColor => KikasaPanelState.TextDim;
        public override Color SilhouetteColor => new Color(12, 18, 22) * 0.9f;

        public override void Update(DialogueLayoutContext context) {
            _state.Update(context.PanelRect, context.Alpha > 0.01f);
            _state.TrackTypewriter(context.VisibleChars);
        }

        public override void Reset() => _state.Reset();

        public override void DrawBackground(SpriteBatch spriteBatch, DialogueLayoutContext context)
            => KikasaPanelDraw.DrawShaderBackground(spriteBatch, context.PanelRect, context.Alpha, _state);

        public override void DrawBackgroundDecorations(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            //Alpha 后段再浮现檐滴
            float decoAlpha = MathHelper.Clamp((context.Alpha - 0.55f) / 0.45f, 0f, 1f);
            if (decoAlpha > 0.01f) {
                KikasaPanelDraw.DrawEaveDrips(spriteBatch, context.PanelRect, decoAlpha, _state.SwayTimer);
            }
            _state.DrawRain(spriteBatch, context.Alpha);
        }

        public override void DrawPortraitFrame(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Rectangle frame = context.PortraitRect;
            float alpha = context.Alpha;

            //雨窗装裱:冷底 + 青框 + 上下窗轨
            spriteBatch.Draw(pixel, frame, src, new Color(8, 12, 15) * (alpha * 0.92f), 0f, Vector2.Zero, SpriteEffects.None, 0f);
            Common.SkinDrawUtil.DrawRectBorder(spriteBatch, frame, KikasaPanelState.Deep * (alpha * 0.85f), 2);

            Color rail = new Color(40, 52, 56) * alpha;
            spriteBatch.Draw(pixel, new Rectangle(frame.X - 4, frame.Y - 3, frame.Width + 8, 3), src, rail, 0f, Vector2.Zero, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, new Rectangle(frame.X - 4, frame.Bottom, frame.Width + 8, 3), src, rail * 0.85f, 0f, Vector2.Zero, SpriteEffects.None, 0f);
            //窗檐下一滴
            KikasaPanelDraw.DrawDrip(spriteBatch, new Vector2(frame.Center.X, frame.Bottom + 3f), 6f, alpha * 0.60f);
        }

        public override void DrawSpeakerName(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            if (string.IsNullOrEmpty(context.SpeakerName)) {
                return;
            }

            float ease = context.SpeakerSwitchEase;
            float nameAlpha = context.ContentAlpha * ease;
            Vector2 basePos = context.SpeakerRect.Location.ToVector2();

            //伞章按落
            Vector2 sealCenter = basePos + new Vector2(8f, context.SpeakerRect.Height * 0.5f - 4f);
            float stampScale = 1f + (1f - ease) * 0.55f;
            float stampRot = (1f - ease) * 0.20f;
            KikasaPanelDraw.DrawUmbrellaGlyph(spriteBatch, sealCenter, 15f * stampScale, context.ContentAlpha * ease, stampRot);

            //名字自水面浮起 + 溺月辉环
            Vector2 namePos = basePos + new Vector2(21f, 0f);
            namePos.Y += (1f - ease) * 5f;
            Color glow = KikasaPanelState.Moon * (nameAlpha * 0.30f);
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4f;
                Utils.DrawBorderString(spriteBatch, context.SpeakerName, namePos + ang.ToRotationVector2() * 1.3f * ease, glow, NameScale);
            }
            Utils.DrawBorderString(spriteBatch, context.SpeakerName, namePos, SpeakerColor * nameAlpha, NameScale);
        }

        public override void DrawDivider(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            Vector2 start = new(context.SpeakerRect.X, context.SpeakerRect.Bottom - 3);
            Vector2 end = new(context.SpeakerRect.Right, context.SpeakerRect.Bottom - 3);
            KikasaPanelDraw.DrawWaterline(spriteBatch, start, end, 1.3f, context.ContentAlpha * 0.85f, _state.SwayTimer * 2f);
        }

        public override void DrawText(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            int remaining = context.VisibleChars;
            float wetStrength = _state.WetStrength;

            for (int i = 0; i < context.WrappedLines.Length; i++) {
                string fullLine = context.WrappedLines[i];
                bool isRevealLine = remaining < fullLine.Length;
                string draw = isRevealLine ? fullLine[..Math.Max(0, remaining)] : fullLine;

                if (draw.Length > 0) {
                    Vector2 pos = new(context.TextRect.X, context.TextRect.Y + i * context.LineHeight);
                    Utils.DrawBorderString(spriteBatch, draw, pos, TextColor * context.ContentAlpha, TextScale);

                    //水光未干,尾 1~2 字浸成冷青
                    if (isRevealLine && wetStrength > 0.02f) {
                        int tail = Math.Min(2, draw.Length);
                        string prefix = draw[..^tail];
                        string tailStr = draw[^tail..];
                        float prefixW = context.Font.MeasureString(prefix).X * TextScale;
                        Utils.DrawBorderString(spriteBatch, tailStr, pos + new Vector2(prefixW, 0f),
                            KikasaPanelState.WetInk * (context.ContentAlpha * 0.85f * wetStrength), TextScale);
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
