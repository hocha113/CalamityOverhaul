using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiame
{
    internal class KiamePlayer : ModPlayer
    {
        public override void OnEnterWorld() {
            //雨里不消费，回主世界才恢复快照
            if (KiameWorld.Active) {
                return;
            }
            KiameGuard.RestoreOnReturn();
        }

        //雨里死不了：HP 归零走惊醒送回主世界，不掉落不留墓碑
        //per-player hook 在跑到 KillMe 的各端各自拦截；锁血/演出/出雨全在 KiameWake
        public override bool PreKill(double damage, int hitDirection, bool pvp,
            ref bool playSound, ref bool genGore, ref PlayerDeathReason damageSource) {
            if (KiameWorld.Active && KiameWake.InterceptDeath(Player, damageSource)) {
                playSound = false;
                genGore = false;
                return false;
            }
            return true;
        }
    }
}
