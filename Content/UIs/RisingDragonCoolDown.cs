using CalamityOverhaul.Content.UIs.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria.Graphics.Shaders;

namespace CalamityOverhaul.Content.UIs
{
    internal class RisingDragonCoolDown : CWRUIPanel
    {
        public bool Alive {
            get {
                if (player.HeldItem.type != CWRIDs.MurasamaItem && player.HeldItem.type != CWRIDs.MurasamaItem2) {
                    return false;
                }
                return player.CWR().RisingDragonCoolDownTime > 0;
            }
        }

        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/RisingDragonCoolDown");

        public Asset<Texture2D> BarBase => CWRUtils.GetT2DAsset("CalamityOverhaul/Assets/UIs/BarBase");

        public float Completion;

        public float opacity;

        public override void Initialize() {
            base.Initialize();
        }

        public override void Update(GameTime gameTime) {
            base.Update(gameTime);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            GameShaders.Misc["CalamityMod:CircularBarSpriteShader"].SetShaderTexture(BarBase);
            GameShaders.Misc["CalamityMod:CircularBarSpriteShader"].UseOpacity(opacity);
            GameShaders.Misc["CalamityMod:CircularBarSpriteShader"].UseSaturation(1 - Completion);
            GameShaders.Misc["CalamityMod:CircularBarSpriteShader"].Apply();
            spriteBatch.Draw(BarBase.Value, DrawPos, null, Color.White * opacity, 0, CWRUtils.GetOrig(BarBase.Value), 1, SpriteEffects.None, 0f);
            int lostHeight = (int)Math.Ceiling(Texture.Height * (1 - Completion));
            Rectangle crop = new Rectangle(0, lostHeight, Texture.Width, Texture.Height - lostHeight);
            spriteBatch.Draw(Texture, DrawPos + Vector2.UnitY * lostHeight, crop, Color.White * opacity * 0.9f, 0, CWRUtils.GetOrig(Texture), 1, SpriteEffects.None, 0f);
        }
    }
}
