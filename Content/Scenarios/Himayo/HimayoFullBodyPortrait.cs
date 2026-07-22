using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Presentation;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Himayo
{
    internal class HimayoFullBodyPortrait : FullBodyPortraitBase
    {
        private static readonly Vector2 FaceOffset = new(82f, 0f);
        private readonly HimayoPortraitAssemblyRenderer assemblyRenderer = new();

        public override string PortraitKey => "HimayoFullBody";

        protected override float FadeInDuration => 20f;

        internal Face currentFace;

        internal enum Face
        {
            None,
            Doubt,
            Grin,
            Forsmile,
            Ruminate,
        }

        protected override void OnInitialize() {
            assemblyRenderer.Stop();
            scale = 1.2f;
            currentFace = Face.None;
        }

        internal void StartPetalAssembly() {
            SkipFadeIn();
            assemblyRenderer.Start(ADVAsset.Himayo);
            BlockDialogueAdvance = true;
        }

        protected override void OnUpdate() {
            scale = 1.2f;
            drawColor = Color.White;
            if (assemblyRenderer.Update((OwnerDialogue?.ShowProgress ?? 0f) >= 0.92f)) {
                BlockDialogueAdvance = false;
            }
        }

        protected override void OnDraw(SpriteBatch spriteBatch, float alpha) {
            Texture2D portrait = ADVAsset.Himayo;
            if (portrait == null || portrait.IsDisposed || OwnerDialogue == null) {
                return;
            }

            position = OwnerDialogue.GetPanelRect().Top() + new Vector2(-160f, -portrait.Height + 100f) * scale;
            Texture2D faceTexture = currentFace switch {
                Face.Doubt => ADVAsset.Himayo_doubt,
                Face.Grin => ADVAsset.Himayo_grin,
                Face.Forsmile => ADVAsset.Himayo_forsmile,
                Face.Ruminate => ADVAsset.Himayo_ruminate,
                _ => null,
            };

            if (assemblyRenderer.Draw(spriteBatch, portrait, faceTexture, FaceOffset,
                position, scale, rotation, drawColor, alpha)) {
                return;
            }

            Color color = drawColor * alpha;
            spriteBatch.Draw(portrait, position, null, color, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
            if (faceTexture != null && !faceTexture.IsDisposed) {
                Vector2 facePosition = position + FaceOffset.RotatedBy(rotation) * scale;
                spriteBatch.Draw(faceTexture, facePosition, null, color, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        protected override void OnDeactivate() {
            assemblyRenderer.Stop();
            BlockDialogueAdvance = false;
        }
    }
}
