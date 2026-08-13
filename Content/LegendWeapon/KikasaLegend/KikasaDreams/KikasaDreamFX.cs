using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams
{
    /// <summary>
    /// 鬼梦演出的节拍音、震屏与延迟雷。全是观看者本机的表现量，
    /// 节拍点是 PhaseTimer 的确定性函数，远端凭快照对齐到一两帧内
    /// </summary>
    internal static class KikasaDreamFX
    {
        //光先于声：闪由 NotifyThunder 先行，雷声隔十几帧才砸下来
        private static int thunderSoundDelay;

        /// <summary>每帧泵，由 <see cref="KikasaDreamSystem"/> 驱动</summary>
        internal static void Update() {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            if (thunderSoundDelay > 0 && --thunderSoundDelay == 0) {
                Player viewer = Main.LocalPlayer;
                if (viewer?.active == true) {
                    SoundEngine.PlaySound(SoundID.Thunder with {
                        Pitch = Main.rand.NextFloat(-1f, -0.8f),
                        Volume = Main.rand.NextFloat(0.3f, 0.45f),
                        MaxInstances = 3,
                    }, viewer.Center + new Vector2(Main.rand.NextFloat(-900f, 900f), -400f));
                }
            }
        }

        internal static void Clear() => thunderSoundDelay = 0;

        /// <summary>拉入节拍表，仅观看端调用</summary>
        internal static void PullBeat(KikasaDomainPlayer domain) {
            Vector2 lakeAt = new(domain.Player.Center.X, domain.LakeWorldY);
            switch (domain.PhaseTimer) {
                case 1:
                    //受理凶兆：天幕先无声地闪，湖面荡开大涟漪，远处有什么低低地应了一声
                    KikasaDomainSky.NotifyThunder();
                    thunderSoundDelay = Main.rand.Next(12, 22);
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.9f, Volume = 0.5f, MaxInstances = 2 }, lakeAt);
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.92f, Volume = 0.26f, MaxInstances = 2 }, lakeAt + new Vector2(0f, 300f));
                    KikasaDomainDeco.RippleAt(lakeAt, 1.6f);
                    ShakeViewer(2f);
                    break;
                case 20:
                    //湖底翻起来的第一记涌拍
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.7f, Volume = 0.5f, MaxInstances = 2 }, lakeAt);
                    break;
                case 52:
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.4f, Volume = 0.6f, MaxInstances = 2 }, lakeAt);
                    KikasaDomainDeco.RippleAt(lakeAt, 1.2f);
                    ShakeViewer(2.5f);
                    break;
                case 84:
                    //沸腾顶点，整面湖都在滚
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.1f, Volume = 0.7f, MaxInstances = 2 }, lakeAt);
                    ShakeViewer(3.5f);
                    break;
                case KikasaDream.PullBoilEnd + 24:
                    //凝视拍：镜里那双眼睛亮起来，喉底的低吼贴着水皮传上来
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -1f, Volume = 0.34f, MaxInstances = 2 }, lakeAt);
                    ShakeViewer(1.5f);
                    break;
                case KikasaDream.PullDwellEnd:
                    //倒转起势
                    SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Pitch = -0.75f, Volume = 0.55f, MaxInstances = 2 }, lakeAt);
                    break;
                case 196:
                    //世界滚动的极低闷响
                    SoundEngine.PlaySound(SoundID.Thunder with { Pitch = -1f, Volume = 0.36f, MaxInstances = 3 }, domain.Player.Center);
                    break;
                case KikasaDream.PullCommitFrame:
                    //血红闪结算：世界被咬进梦里
                    SoundEngine.PlaySound(SoundID.Thunder with { Pitch = -0.55f, Volume = 0.9f, MaxInstances = 3 }, domain.Player.Center);
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.7f, Volume = 0.6f, MaxInstances = 2 }, domain.Player.Center);
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.7f, Volume = 0.42f, MaxInstances = 2 }, domain.Player.Center);
                    ShakeViewer(10f);
                    break;
                case KikasaDream.PullRollEnd:
                    //落进梦侧：没有水花，只有远处又一声应和
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f, Volume = 0.3f, MaxInstances = 2 }, domain.Player.Center + new Vector2(600f, -200f));
                    ShakeViewer(3f);
                    break;
            }
        }

        /// <summary>归返节拍表，仅观看端调用</summary>
        internal static void ReturnBeat(KikasaDomainPlayer domain) {
            Vector2 lakeAt = new(domain.Player.Center.X, domain.LakeWorldY);
            switch (domain.PhaseTimer) {
                case 1:
                    //湖水从屏底涌回来
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.8f, Volume = 0.5f, MaxInstances = 2 }, lakeAt);
                    break;
                case 34:
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.55f, Volume = 0.55f, MaxInstances = 2 }, lakeAt);
                    break;
                case KikasaDream.ReturnSurgeEnd:
                    //水面触脚确认拍
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.15f, Volume = 0.7f, MaxInstances = 2 }, lakeAt);
                    KikasaDomainDeco.SplashAt(lakeAt, 10);
                    KikasaDomainDeco.RippleAt(lakeAt, 1.3f);
                    ShakeViewer(3.5f);
                    break;
                case KikasaDream.ReturnDwellEnd:
                    SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Pitch = -0.7f, Volume = 0.5f, MaxInstances = 2 }, lakeAt);
                    break;
                case 132:
                    SoundEngine.PlaySound(SoundID.Thunder with { Pitch = -1f, Volume = 0.32f, MaxInstances = 3 }, domain.Player.Center);
                    break;
                case KikasaDream.ReturnCommitFrame:
                    //暖白闪：吐回真实
                    SoundEngine.PlaySound(SoundID.Thunder with { Pitch = -0.6f, Volume = 0.8f, MaxInstances = 3 }, domain.Player.Center);
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.65f, Volume = 0.55f, MaxInstances = 2 }, domain.Player.Center);
                    ShakeViewer(9f);
                    break;
                case KikasaDream.ReturnRollEnd:
                    //落定闷锣
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.9f, Volume = 0.4f, MaxInstances = 1 }, domain.Player.Center);
                    ShakeViewer(4f);
                    break;
            }
        }

        /// <summary>屏震落在观看者身上而非施术者</summary>
        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);
    }
}
