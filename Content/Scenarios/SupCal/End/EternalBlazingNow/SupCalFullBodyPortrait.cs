using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Presentation;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow
{
    internal sealed class SupCalFullBodyPortrait : FullBodyPortraitBase
    {
        public override string PortraitKey => "SupremeCalamitasFullBody";

        protected override float FadeInDuration => 120f;

        private const int SmilePortraitDialogueIndex = 10;
        private const float BurnDuration = 180f;

        private bool useSmilePortrait;
        private float burnProgress;

        protected override void OnInitialize() {
            useSmilePortrait = false;
            burnProgress = 0f;
            scale = 1.4f;
        }

        public override void OnDialogueAdvance() {
            base.OnDialogueAdvance();
            if (dialogueIndex >= SmilePortraitDialogueIndex && !useSmilePortrait) {
                useSmilePortrait = true;
                StartBurningDissolve();
            }
        }

        public override void EndPerformance() {
            if (currentPhase != PerformancePhase.Custom) {
                StartBurningDissolve();
            }
        }

        private void StartBurningDissolve() {
            EnterCustomPhase();
            burnProgress = 0f;
            BlockDialogueAdvance = true;
            BlockDialogueClose = true;
        }

        protected override void OnCustomPhaseUpdate() {
            burnProgress++;
            float t = MathHelper.Clamp(burnProgress / BurnDuration, 0f, 1f);
            CurrentFade = 1f - t * t;
            if (burnProgress >= BurnDuration) {
                ForceDeactivate();
            }
        }

        protected override void OnDeactivate() {
            BlockDialogueAdvance = false;
            BlockDialogueClose = false;
        }

        protected override void OnDraw(SpriteBatch spriteBatch, float alpha) {
            Texture2D portrait = useSmilePortrait ? ADVAsset.SupCal_smileADV : ADVAsset.SupCal_closeEyesADV;
            if (portrait == null || portrait.IsDisposed || OwnerDialogue == null) {
                return;
            }

            Rectangle panel = OwnerDialogue.GetPanelRect();
            Vector2 pos = new(panel.X, panel.Bottom - portrait.Height * scale);
            spriteBatch.Draw(portrait, pos, null, Color.White * alpha, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
