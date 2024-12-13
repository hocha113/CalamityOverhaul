using SubworldLibrary;
using System.Collections.Generic;
using Terraria;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Subworlds.FogDungeons
{
    internal class DungeonWorld : Subworld
    {
        public override int Width => 2000;

        public override int Height => 2000;

        public static bool Active => SubworldSystem.IsActive<DungeonWorld>();

        public override List<GenPass> Tasks => [new FogDungeonGen()];

        public override void OnLoad() {
            Main.dayTime = true;
            Main.time = 27000;
        }

        public override void Update() {

        }

        public override bool ChangeAudio() {
            return base.ChangeAudio();
        }

        public override void DrawMenu(GameTime gameTime) {
            base.DrawMenu(gameTime);
        }

        public override void DrawSetup(GameTime gameTime) {
            base.DrawSetup(gameTime);
        }

        public override float GetGravity(Entity entity) {
            return base.GetGravity(entity);
        }
    }
}
