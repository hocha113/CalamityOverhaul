#if DEBUG
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet
{
    /// <summary>
    /// 旧网调试入口，仅调试构建存在（正式入口在 M1：坠舱终端 / L3 下潜）：<br/>
    /// /oldnet enter：越墙深潜<br/>
    /// /oldnet exit：直接断链（视同非登出途径离开，账本按机制作废）<br/>
    /// /oldnet noise：直接设置本机会话噪音（封锁闸 T2/T3 全链路验收用）
    /// </summary>
    internal class OldNetCommand : ModCommand
    {
        public override string Command => "oldnet";
        public override CommandType Type => CommandType.Chat;
        public override string Description => "旧网子世界调试：进入 / 离开 / 设噪音";
        public override string Usage => "/oldnet enter\n/oldnet exit\n/oldnet noise <0-100>";

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
                case "noise":
                    if (!OldNetWorld.Active) {
                        caller.Reply("不在旧网内", Color.IndianRed);
                        return;
                    }
                    if (args.Length < 2 || !float.TryParse(args[1], out float noise)) {
                        caller.Reply("/oldnet noise <0-100>", Color.LightGray);
                        return;
                    }
                    OldNetPlayer session = OldNetPlayer.Get(caller.Player);
                    session.Noise = MathHelper.Clamp(noise, 0f, 100f);
                    caller.Reply($"噪音已设为 {session.Noise:0}（档位下帧刷新）", Color.LightGray);
                    break;
                default:
                    caller.Reply(Usage, Color.LightGray);
                    break;
            }
        }
    }
}
#endif
