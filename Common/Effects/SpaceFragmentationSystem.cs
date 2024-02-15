using CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace CalamityOverhaul.Common.Effects
{
    internal class SpaceFragmentationSystem : ModSystem
    {
        public static SpaceFragmentationSystem Instance;
        public static RenderTarget2D render;
        public float twistStrength = 0f;
        public override void Load() {
            Instance = this;
            On_FilterManager.EndCapture += FilterManager_EndCapture;
            Main.OnResolutionChanged += Main_OnResolutionChanged;
        }

        public override void Unload() {
            Instance = null;
            On_FilterManager.EndCapture -= FilterManager_EndCapture;
            Main.OnResolutionChanged -= Main_OnResolutionChanged;
        }

        //在分辨率更改时，重建render防止某些bug
        private void Main_OnResolutionChanged(Vector2 obj) => render = new RenderTarget2D(Main.graphics.GraphicsDevice, Main.screenWidth, Main.screenHeight);

        private void FilterManager_EndCapture(On_FilterManager.orig_EndCapture orig, FilterManager self, RenderTarget2D finalTexture, RenderTarget2D screenTarget1, RenderTarget2D screenTarget2, Color clearColor) {
            render ??= new RenderTarget2D(Main.graphics.GraphicsDevice, Main.screenWidth, Main.screenHeight);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            SpriteBatch sb = Main.spriteBatch;

            gd.SetRenderTarget(Main.screenTargetSwap);//
            gd.Clear(Color.Transparent);//用透明清除
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            gd.SetRenderTarget(render);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.Transform);

            PowerSpaceFragmentation.DrawPowerProjEff(sb, ref twistStrength);

            sb.End();

            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            EffectsRegistry.PowerSFShader.Parameters["tex0"].SetValue(render);
            EffectsRegistry.PowerSFShader.Parameters["i"].SetValue(twistStrength);
            EffectsRegistry.PowerSFShader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(Main.screenTargetSwap, Vector2.Zero, Color.White);
            sb.End();

            orig(self, finalTexture, screenTarget1, screenTarget2, clearColor);
        }
    }
}
