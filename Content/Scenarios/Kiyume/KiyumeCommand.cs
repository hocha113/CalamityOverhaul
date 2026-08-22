#if DEBUG
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume
{
    /// <summary>
    /// 鬼梦调试入口，仅调试构建存在（正式入口等玩法定下来再接）：<br/>
    /// /kiyume enter：坠入鬼梦<br/>
    /// /kiyume exit：醒来
    /// </summary>
    internal class KiyumeCommand : ModCommand
    {
        public override string Command => "kiyume";
        public override CommandType Type => CommandType.Chat;
        public override string Description => "鬼梦子世界调试：进入 / 离开";
        public override string Usage => "/kiyume enter\n/kiyume exit";

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
                    if (KiyumeWorld.Active) {
                        caller.Reply("已经在梦里了", Color.IndianRed);
                        return;
                    }
                    KiyumeWorld.EnterWorld();
                    break;
                case "exit":
                    if (!KiyumeWorld.Active) {
                        caller.Reply("不在梦里", Color.IndianRed);
                        return;
                    }
                    KiyumeWorld.ExitWorld();
                    break;
                default:
                    caller.Reply(Usage, Color.LightGray);
                    break;
            }
        }
    }
}
#endif
