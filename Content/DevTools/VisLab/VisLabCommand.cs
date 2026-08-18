#if DEBUG
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.DevTools.VisLab
{
    /// <summary>
    /// 游戏内快照台命令,仅调试构建存在,且要求本机有 .vissandbox 目录(开发机门控):<br/>
    /// /vlab list —— 列出可用 job<br/>
    /// /vlab run &lt;名字&gt; —— 执行快照会话<br/>
    /// /vlab stop —— 中止当前会话
    /// </summary>
    internal class VisLabCommand : ModCommand
    {
        public override string Command => "vlab";
        public override CommandType Type => CommandType.Chat;
        public override string Description => "视觉快照台:弹幕/PRT/UI 抓帧导出";
        public override string Usage => "/vlab list\n/vlab run <job名>\n/vlab stop";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (!VisLabSystem.DevMachine) {
                caller.Reply("非开发机(.vissandbox 不存在),快照台不可用", Color.IndianRed);
                return;
            }
            if (Main.netMode != NetmodeID.SinglePlayer) {
                caller.Reply("仅单人模式可用", Color.IndianRed);
                return;
            }
            if (args.Length == 0) {
                caller.Reply(Usage, Color.LightGray);
                return;
            }

            switch (args[0]) {
                case "list": {
                    string jobsDir = Path.Combine(VisLabSystem.Root, "jobs");
                    if (!Directory.Exists(jobsDir)) {
                        caller.Reply("jobs 目录为空", Color.LightGray);
                        return;
                    }
                    string[] names = Directory.GetFiles(jobsDir, "*.json")
                        .Select(Path.GetFileNameWithoutExtension).ToArray();
                    caller.Reply(names.Length == 0 ? "没有 job" : string.Join(", ", names), Color.LightGray);
                    return;
                }
                case "run": {
                    if (args.Length < 2) {
                        caller.Reply("用法: /vlab run <job名>", Color.LightGray);
                        return;
                    }
                    if (VisLabSystem.TryStart(args[1], out string error)) {
                        caller.Reply("会话开始: " + args[1], Color.LightGreen);
                    }
                    else {
                        caller.Reply(error, Color.IndianRed);
                    }
                    return;
                }
                case "stop":
                    VisLabSystem.Stop("手动中止");
                    return;
                default:
                    caller.Reply(Usage, Color.LightGray);
                    return;
            }
        }
    }
}
#endif
