using CalamityMod.Events;
using CalamityMod.Skies;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics.Shaders;

namespace CalamityOverhaul.Content.Events
{
    internal class TungstenSkyDate : ScreenShaderData
    {
        public TungstenSkyDate(string passName) : base(passName) { }

        public override void Apply() {
            UseTargetPosition(Main.LocalPlayer.Center);
            base.Apply();
        }

        public override void Update(GameTime gameTime) {
            
        }
    }
}
