using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Particles.Core
{
    internal class PeSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            if (!Main.dedServ)
                CWRParticleHandler.Update();
        }

        public static void CWRDrawForegroundParticles(Terraria.On_Main.orig_DrawInfernoRings orig, Main self) {
            CWRParticleHandler.DrawAll(Main.spriteBatch);
            orig(self);
        }
    }
}
