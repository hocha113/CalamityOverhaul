using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV
{
    internal class ADVPlayer : ModPlayer
    {
        public static int AccPlayerCount { get; set; } = 1;
        public override void OnEnterWorld() {
            //进入世界时发送'永恒燃烧的现在'数据
            if (Player.TryGetADVSave(out var save) && save.EternalBlazingNow) {
                save.SendEbnData(Player);
            }
            OldDukeCampsite.RequestOldDukeCampsiteData();
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            int num = 0;
            foreach (var player in Main.player) {
                if (player.active) {
                    num++;
                }
            }

            if (AccPlayerCount != num) {
                if (Player.TryGetADVSave(out var save) && save.EternalBlazingNow) {
                    save.SendEbnData(Player);//如果玩家队列有所变动，同步数据
                }
            }

            AccPlayerCount = num;
        }
    }
}
