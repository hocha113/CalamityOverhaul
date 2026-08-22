#if DEBUG
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet
{
    /// <summary>
    /// 旧网调试入口，仅调试构建存在（正式入口在 M1：坠舱终端 / L3 下潜）：<br/>
    /// /oldnet enter：越墙深潜<br/>
    /// /oldnet exit：直接断链（视同非登出途径离开，账本按机制作废）
    /// </summary>
    internal class OldNetCommand : ModCommand
    {
        public override string Command => "oldnet";
        public override CommandType Type => CommandType.Chat;
        public override string Description => "旧网子世界调试：进入 / 离开";
        public override string Usage => "/oldnet enter\n/oldnet exit";

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
                    if (OldNetWorld.Active) {
                        caller.Reply("已在旧网内", Color.IndianRed);
                        return;
                    }
                    OldNetWorld.EnterWorld();
                    break;
                case "exit":
                    if (!OldNetWorld.Active) {
                        caller.Reply("不在旧网内", Color.IndianRed);
                        return;
                    }
                    OldNetWorld.ExitWorld();
                    break;
                default:
                    caller.Reply(Usage, Color.LightGray);
                    break;
            }
        }
    }
}
#endif
