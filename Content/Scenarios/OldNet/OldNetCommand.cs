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
    /// /oldnet noise：直接设置本机会话噪音（封锁闸 T2/T3 全链路验收用）<br/>
    /// /oldnet report：合成假战报直接弹战报屏（视觉验收用，不深潜）
    /// </summary>
    internal class OldNetCommand : ModCommand
    {
        public override string Command => "oldnet";
        public override CommandType Type => CommandType.Chat;
        public override string Description => "旧网子世界调试：进入 / 离开 / 设噪音 / 弹战报";
        public override string Usage => "/oldnet enter\n/oldnet exit\n/oldnet noise <0-100>\n/oldnet report [safe|burn|death]";

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
                case "report":
                    //战报屏视觉验收：不深潜直接弹，主世界/旧网内均可用
                    UI.OldNetExitKind kind = args.Length >= 2 ? args[1] switch {
                        "burn" => UI.OldNetExitKind.RamBurnout,
                        "death" => UI.OldNetExitKind.Death,
                        _ => UI.OldNetExitKind.SafeLogout,
                    } : UI.OldNetExitKind.RamBurnout;
                    UI.OldNetDebriefPanel.ShowPreview(kind);
                    caller.Reply("战报屏预览已弹出", Color.LightGray);
                    break;
                default:
                    caller.Reply(Usage, Color.LightGray);
                    break;
            }
        }
    }
}
#endif
