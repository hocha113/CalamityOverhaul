using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Structures
{
    public class WorldGenTutorialWorld : ModSystem
    {
        public static bool JustPressed(Keys key) {
            return Main.keyState.IsKeyDown(key) && !Main.oldKeyState.IsKeyDown(key);
        }

        public override void PostUpdateWorld() {
            //if (JustPressed(Keys.D1)) {
            //    IndustrializationGen.SpawnWindGrivenGenerator();
            //}
        }
    }
}