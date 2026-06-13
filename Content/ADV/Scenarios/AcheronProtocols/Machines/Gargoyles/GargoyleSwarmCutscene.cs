using CalamityOverhaul.Common;
using InnoVault.Cinematics;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines.Gargoyles
{
    /// <summary>
    /// 石像鬼虫群飞越过场运镜——基于 InnoVault 演出系统。
    /// <para>运镜节奏（上摇 → 飞越缩放 + 水平跟踪集群重心 → 下摇）仍由 <see cref="GargoyleSwarmPlayer"/>
    /// 按其时间轴常量逐帧推导，本演出每帧读取其结果（焦点 = 演出起点 + 运镜偏移、缩放）并应用，
    /// 同时锁定本地玩家操作；演出收尾时的镜头平滑归位与输入解锁交由 InnoVault 在
    /// <see cref="CutsceneDirector.Stop"/> 后自动完成。</para>
    /// </summary>
    internal sealed class GargoyleSwarmCutscene : CutsceneClip
    {
        //略大于演出硬上限，确保 clip 不会先于 GargoyleSwarmPlayer 主动收尾而自停
        private const int ClipFrames = GargoyleSwarmPlayer.CutsceneHardLimit + 120;

        public override int Priority => 50;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = ClipFrames;
            timeline.Add(new DynamicCameraTrack(0, ClipFrames, DriveCamera));
            timeline.Add(new InputLockTrack(0, ClipFrames, CutsceneInputLockFlags.All));
        }

        private static void DriveCamera(CutsceneContext context) {
            //焦点/缩放已由 GargoyleSwarmPlayer.UpdateCamera 平滑推导，这里直接套用（lerpSpeed=1 即时应用）
            context.SetCameraFocus(GargoyleSwarmPlayer.CameraFocus, 1f);
            context.SetCameraZoom(GargoyleSwarmPlayer.CameraZoom, 1f);
        }
    }
}
