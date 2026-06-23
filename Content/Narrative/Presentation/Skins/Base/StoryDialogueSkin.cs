using CalamityOverhaul.Common;
using InnoVault.Narrative.Presentation.Dialogue;
using InnoVault.Narrative.Styling;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Audio;
using Terraria.Localization;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Base
{
    internal class StoryDialogueSkin : DialogueSkin
    {
        /// <summary>与 ADV 底栏一致：略压入 shader 内缘，消除视觉底边空隙。</summary>
        public override float HintBottomMargin => -8f;

        public override float PortraitSize => 100;
        public override float Padding => 10f;

        protected virtual Color Fill => new(16, 22, 34);
        protected virtual Color Edge => new(70, 130, 200);

        public override Color TextColor => new(235, 240, 255);
        public override Color SpeakerColor => new(180, 220, 255);
        public override Color HintColor => new(150, 190, 235);

        protected override string ResolveAutoHint() => DialogueSystem.AutoHint.Value;
        protected override string ResolveFastHint() => DialogueSystem.FastHint.Value;
        protected override string ResolveSkipHint() => DialogueSystem.SkipHint.Value;
        protected override string ResolveContinueHint(bool hover) => FormatHintLabel(DialogueSystem.ContinueHint.Value, hover);

        protected static string FormatHintLabel(string label, bool bracketed) => bracketed ? $"[{label}]" : label;

        public override void PlayToggleAutoSound(bool autoMode)
            => SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = autoMode ? 0.5f : 0.1f });

        public override void PlayToggleFastSound(bool fastMode)
            => SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = fastMode ? 0.65f : 0.1f });

        public override void PlaySkipSound()
            => SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = 0.35f });

        public override void DrawPanel(SpriteBatch spriteBatch, Rectangle panel, float alpha)
            => NarrativeSkinDraw.DrawPanel(spriteBatch, panel, Fill, Edge, alpha);
        public override void DrawDivider(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            Rectangle divider = new(context.SpeakerRect.X, context.SpeakerRect.Bottom - 2, context.SpeakerRect.Width, 1);
            NarrativeSkinDraw.FillRect(spriteBatch, divider, Edge * (context.Alpha * 0.45f));
        }
    }
}
