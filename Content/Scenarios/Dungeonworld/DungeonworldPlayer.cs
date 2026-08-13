using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld
{
    internal class DungeonworldPlayer : ModPlayer
    {
        public override void OnEnterWorld() {
            //子世界内不消费,回主世界才恢复快照
            if (Dungeonworld.Active) {
                return;
            }
            DungeonworldGuard.RestoreOnReturn();
        }
    }
}
