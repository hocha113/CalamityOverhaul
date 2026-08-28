#if DEBUG
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiame
{
    /// <summary>
    /// 鬼雨子世界调试入口，仅调试构建存在（正式入口是游走鬼伞）：<br/>
    /// /kiame enter：撑伞入雨<br/>
    /// /kiame exit：收伞<br/>
    /// /kiame gate：门伞立即换位
    /// </summary>
    internal class KiameCommand : ModCommand
    {
        public override string Command => "kiame";
        public override CommandType Type => CommandType.Chat;
        public override string Description => "鬼雨子世界调试：进入 / 离开 / 门伞换位";
        public override string Usage => "/kiame enter\n/kiame exit\n/kiame gate";

        public override void Action(CommandCaller caller, string input, string[] args) {
            if (Main.netMode != NetmodeID.SinglePlayer) {
                caller.Reply("仅单人模式可用", Color.IndianRed);
                return;
            }
            if (args.Length == 0) {
                caller.Reply(Usage, Color.LightGray);
                return;
            }

            switch (args[0]) {
                case "enter":
                    if (KiameWorld.Active) {
                        caller.Reply("已经在雨里了", Color.IndianRed);
                        return;
                    }
                    KiameWorld.EnterWorld();
                    break;
                case "exit":
                    if (!KiameWorld.Active) {
                        caller.Reply("不在雨里", Color.IndianRed);
                        return;
                    }
                    KiameWorld.ExitWorld();
                    break;
                case "gate":
                    if (KiameWorld.Active) {
                        caller.Reply("门伞是主世界地标，先出雨", Color.IndianRed);
                        return;
                    }
                    Gate.KiameGateSpawn.Relocate();
                    caller.Reply("门伞已择新址", Color.LightGray);
                    break;
                default:
                    caller.Reply(Usage, Color.LightGray);
                    break;
            }
        }
    }
}
#endif
