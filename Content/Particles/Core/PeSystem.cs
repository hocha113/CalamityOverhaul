using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Particles.Core
{
    internal class PeSystem : ModSystem
    {
        public override void Load() => On_Main.DrawInfernoRings += CWRDrawForegroundParticles;
        public override void Unload() => On_Main.DrawInfernoRings -= CWRDrawForegroundParticles;
        public override void PostUpdateEverything() => CWRParticleHandler.Update();
        public static void CWRDrawForegroundParticles(Terraria.On_Main.orig_DrawInfernoRings orig, Main self) {
            CWRParticleHandler.DrawAll(Main.spriteBatch);
            orig(self);
        }
    }
}
