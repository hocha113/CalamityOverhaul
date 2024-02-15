using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Common.Effects
{
    public class RenderTargetManager : ModSystem
    {
        internal static List<ManagedRenderTarget> ManagedTargets = new();

        public delegate void RenderTargetUpdateDelegate();

        public static event RenderTargetUpdateDelegate RenderTargetUpdateLoopEvent;

        public override void OnModLoad() {
            Main.OnPreDraw += HandleTargetUpdateLoop;
        }

        public override void OnModUnload() {
            Main.OnPreDraw -= HandleTargetUpdateLoop;
        }

        private void HandleTargetUpdateLoop(GameTime obj) => RenderTargetUpdateLoopEvent?.Invoke();
    }
}
