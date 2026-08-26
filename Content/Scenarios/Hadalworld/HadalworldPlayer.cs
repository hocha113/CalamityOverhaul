using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Hadalworld
{
    internal class HadalworldPlayer : ModPlayer
    {
        public override void OnEnterWorld() {
            //子世界内不消费,回主世界才恢复快照
            if (Hadalworld.Active) {
                return;
            }
            HadalworldGuard.RestoreOnReturn();
        }
    }
}
