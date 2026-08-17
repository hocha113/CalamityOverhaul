using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume
{
    internal class KiyumePlayer : ModPlayer
    {
        public override void OnEnterWorld() {
            //梦里不消费，回主世界才恢复快照
            if (KiyumeWorld.Active) {
                return;
            }
            KiyumeGuard.RestoreOnReturn();
        }
    }
}
