#if DEBUG
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.DevTools.SoundProbe
{
    /// <summary>
    /// 音效探针命令，仅调试构建存在：<br/>
    /// /sprobe：按默认窗口重新开一段监听<br/>
    /// /sprobe &lt;帧数&gt;：指定窗口长度<br/>
    /// /sprobe stop：立即收工
    /// </summary>
    internal class SoundProbeCommand : ModCommand
    {
        public override string Command => "sprobe";
        public override CommandType Type => CommandType.Chat;
        public override string Description => "音效探针:把播放的音效与调用栈写进 client.log";
        public override string Usage => "/sprobe\n/sprobe <帧数>\n/sprobe stop";

        public override void Action(CommandCaller caller, string input, string[] args) {
            if (args.Length > 0 && args[0] == "stop") {
                SoundProbe.Disarm();
                caller.Reply("音效探针已停", Color.LightGray);
                return;
            }
            int frames = SoundProbe.DefaultWindow;
            if (args.Length > 0 && !int.TryParse(args[0], out frames)) {
                caller.Reply(Usage, Color.LightGray);
                return;
            }
            SoundProbe.Arm(frames);
            caller.Reply($"音效探针开始监听 {frames} 帧,结果写在 client.log 的 [SoundProbe] 行", Color.LightGreen);
        }
    }
}
#endif
