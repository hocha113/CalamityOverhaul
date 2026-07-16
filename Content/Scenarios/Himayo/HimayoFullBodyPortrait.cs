using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Presentation;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Himayo
{
    internal class HimayoFullBodyPortrait : FullBodyPortraitBase
    {
        private static readonly Vector2 FaceOffset = new(82f, 0f);

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
            scale = 1.2f;
            currentFace = Face.None;
        }

        protected override void OnUpdate() {
            scale = 1.2f;
            drawColor = Color.White;
        }

        protected override void OnDraw(SpriteBatch spriteBatch, float alpha) {
            Texture2D portrait = ADVAsset.Himayo;
            if (portrait == null || portrait.IsDisposed || OwnerDialogue == null) {
                return;
            }

            position = OwnerDialogue.GetPanelRect().Top() + new Vector2(-160f, -portrait.Height + 100f) * scale;
            Color color = drawColor * alpha;
            spriteBatch.Draw(portrait, position, null, color, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);

            Texture2D faceTexture = currentFace switch {
                Face.Doubt => ADVAsset.Himayo_doubt,
                Face.Grin => ADVAsset.Himayo_grin,
                Face.Forsmile => ADVAsset.Himayo_forsmile,
                Face.Ruminate => ADVAsset.Himayo_ruminate,
                _ => null,
            };

            if (faceTexture != null && !faceTexture.IsDisposed) {
                spriteBatch.Draw(faceTexture, position + FaceOffset * scale, null, color, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }
    }
}
