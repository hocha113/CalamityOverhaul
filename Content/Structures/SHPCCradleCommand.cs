#if DEBUG
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Structures
{
    /// <summary>
    /// SHPC 坠舱空岛的调试命令，仅调试构建存在：<br/>
    /// /shpccradle gen —— 走一遍真实的世界生成流程（天空寻位，失败仅 Warn 不放置）<br/>
    /// /shpccradle here —— 以光标为箱体左上角直接构建，便于反复迭代观感（会清空该区域）
    /// </summary>
    internal class SHPCCradleCommand : ModCommand
    {
        public override string Command => "shpccradle";
        public override CommandType Type => CommandType.Chat;
        public override string Description => "SHPC 坠舱空岛调试：生成测试 / 光标处构建";
        public override string Usage => "/shpccradle gen\n/shpccradle here";

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
                case "gen":
                    caller.Reply(SHPCCradleGen.Generate()
                        ? "已按世界生成流程放置，位置见日志"
                        : "寻位失败，未放置（详见日志）",
                        Color.LightGreen);
                    break;
                case "here": {
                    Point16 at = new((int)(Main.MouseWorld.X / 16f), (int)(Main.MouseWorld.Y / 16f));
                    SHPCCradleGen.Build(at);
                    caller.Reply($"已构建于 {at.X}, {at.Y}", Color.LightGreen);
                    break;
                }
                default:
                    caller.Reply(Usage, Color.LightGray);
                    break;
            }
        }
    }
}
#endif
