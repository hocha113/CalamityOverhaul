using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;

using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;

using InnoVault.Narrative.Presentation.Dialogue;

using Microsoft.Xna.Framework.Graphics;

using System;

using Terraria;



namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.SHPC

{

    /// <summary>SHPC 对话框——布局/字号与 <see cref="Sea.SeaDialogueSkin"/> 一致，仅替换赛博特效面板。</summary>

    internal sealed class SHPCDialogueSkin : StoryDialogueSkin

    {

        private readonly SHPCPanelState _state = new();



        public override float Padding => 10;

        /// <summary>对齐 ADV SHPCDialogueBox 与 CyberPanel shader 内缘。</summary>

        public override float TextWrapInset => 24f;



        public override Color TextColor => new(210, 240, 255);

        public override Color SpeakerColor => new(200, 195, 240);

        public override Color HintColor => SHPCPanelState.NeonBlueColor;

        public override Color SilhouetteColor => new Color(12, 8, 24) * 0.9f;



        public override void Update(DialogueLayoutContext context)

            => _state.Update(context.PanelRect, context.Alpha > 0.01f, dialogueDecorations: true);



        public override void Reset() => _state.Reset();



        public override void DrawBackground(SpriteBatch spriteBatch, DialogueLayoutContext context) {

            SHPCPanelDraw.DrawCyberBackground(spriteBatch, context.PanelRect, context.Alpha, _state, layeredShadow: true);

            SHPCPanelDraw.DrawDialogueDecorations(spriteBatch, context.PanelRect, context.Alpha, _state);

        }



        public override void DrawBackgroundDecorations(SpriteBatch spriteBatch, DialogueLayoutContext context)

            => _state.DrawParticles(spriteBatch, context.Alpha, dialogueDecorations: true);



        public override void DrawPortraitFrame(SpriteBatch spriteBatch, DialogueLayoutContext context) {

            float pulse = MathF.Sin(_state.NeonPulse * 1.2f) * 0.15f + 0.85f;

            SHPCPanelDraw.DrawPortraitFrame(spriteBatch, context.PortraitRect, context.Alpha, pulse);

        }



        public override void DrawSpeakerName(SpriteBatch spriteBatch, DialogueLayoutContext context) {

            if (string.IsNullOrEmpty(context.SpeakerName)) {

                return;

            }



            float nameAlpha = context.ContentAlpha * context.SpeakerSwitchEase;

            Vector2 pos = context.SpeakerRect.Location.ToVector2();

            pos.Y -= (1f - context.SpeakerSwitchEase) * 6f;

            Color glow = SHPCPanelState.NeonBlueColor * (nameAlpha * 0.65f);

            for (int i = 0; i < 6; i++) {

                float angle = MathHelper.TwoPi * i / 6f;

                Utils.DrawBorderString(spriteBatch, context.SpeakerName, pos + angle.ToRotationVector2() * 2.2f * context.SpeakerSwitchEase, glow * 0.5f, NameScale);

            }

            Utils.DrawBorderString(spriteBatch, context.SpeakerName, pos, SpeakerColor * nameAlpha, NameScale);

        }



        public override void DrawDivider(SpriteBatch spriteBatch, DialogueLayoutContext context) {

            Vector2 start = new(context.SpeakerRect.X, context.SpeakerRect.Bottom - 3);

            Vector2 end = new(context.SpeakerRect.Right, context.SpeakerRect.Bottom - 3);

            SkinDrawUtil.DrawGradientLine(spriteBatch, start, end,

                SHPCPanelState.NeonBlueColor * (context.ContentAlpha * 0.85f),

                SHPCPanelState.NeonBlueDimColor * (context.ContentAlpha * 0.08f),

                1.3f);

        }



        public override void DrawText(SpriteBatch spriteBatch, DialogueLayoutContext context) {

            int remaining = context.VisibleChars;

            for (int i = 0; i < context.WrappedLines.Length; i++) {

                string fullLine = context.WrappedLines[i];

                string draw = remaining < fullLine.Length ? fullLine[..Math.Max(0, remaining)] : fullLine;

                if (draw.Length > 0) {

                    Vector2 pos = new(context.TextRect.X, context.TextRect.Y + i * context.LineHeight);

                    Color lineColor = Color.Lerp(new Color(200, 195, 240), Color.White, 0.15f) * context.ContentAlpha;

                    Utils.DrawBorderString(spriteBatch, draw, pos, lineColor, TextScale);

                }

                remaining -= fullLine.Length;

                if (remaining <= 0) {

                    break;

                }

            }

        }

    }

}


