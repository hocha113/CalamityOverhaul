#if DEBUG
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Hadalworld
{
    /// <summary>
    /// 深渊海沟调试入口,仅调试构建存在(正式入口留待后续里程碑):<br/>
    /// /hadal:不在沟内则下潜,已在沟内则上浮<br/>
    /// /hadal enter:下潜进入<br/>
    /// /hadal exit:上浮离开
    /// </summary>
    internal class HadalworldCommand : ModCommand
    {
        public override string Command => "hadal";
        public override CommandType Type => CommandType.Chat;
        public override string Description => "深渊海沟子世界调试:进入 / 离开";
        public override string Usage => "/hadal\n/hadal enter\n/hadal exit";

        public override void Action(CommandCaller caller, string input, string[] args) {
            //第一期单人优先;Chat 指令不会在 dedServ 执行,这里再拦一道联机
            if (Main.netMode != NetmodeID.SinglePlayer) {
                caller.Reply("仅单人模式可用", Color.IndianRed);
                return;
            }
            bool active = Hadalworld.Active;
            //无参=开关语义:沟外下潜,沟内上浮
            string sub = args.Length > 0 ? args[0] : (active ? "exit" : "enter");
            switch (sub) {
                case "enter":
                    if (active) {
                        caller.Reply("已在深渊海沟内", Color.IndianRed);
                        return;
                    }
                    Hadalworld.EnterWorld();
                    break;
                case "exit":
                    if (!active) {
                        caller.Reply("不在深渊海沟内", Color.IndianRed);
                        return;
                    }
                    Hadalworld.ExitWorld();
                    break;
                default:
                    caller.Reply(Usage, Color.LightGray);
                    break;
            }
        }
    }
}
#endif
