using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 昼夜跳转：把时间快进到最近的日出或日落。<br/>
    /// 实现完全镜像原版日晷/月晷，权威端置
    /// <c>Main.fastForwardTimeToDawn / fastForwardTimeToDusk</c> 并广播
    /// <see cref="MessageID.WorldData"/>；60 倍速的天空扫掠、边界处的事件重掷、
    /// 到点自动清旗全部由原版 <c>UpdateTime_StartDay/StartNight</c> 兜底，
    /// 没有任何要"还"的状态。<br/>
    /// 硬闸（§5.7）：Boss 存活 / 事件进行中 / 已在快进中不可用；
    /// 联机下另有 60 秒全服冷却（世界级静态，权威端读写）
    /// </summary>
    internal class DielSkip : QuickHackDef
    {
        //全服冷却，不是个人冷却，时间是全员共享的资源
        private static ulong worldCooldownUntil;
        private const int WorldCooldownTicks = 60 * 60;

        private static readonly Color DawnGold = new(255, 210, 120);
        private static readonly Color DuskViolet = new(170, 120, 255);

        public override void SetDefaults() {
            //怪物级定价：写的是全服每个人的时间
            UploadTime = 240;
            RamCost = 8;
            Category = QuickHackCategory.Paranormal;
            SupportedTargets = HackTargetKind.World;
            UnlockedByDefault = false;
        }

        public override void Unload() {
            base.Unload();
            worldCooldownUntil = 0;
        }

        /// <summary>切世界清账：冷却时间戳属于上一个世界</summary>
        internal static void ClearWorldCooldown() => worldCooldownUntil = 0;

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (target is not WorldScannable) return false;
            //已在快进中再叠一发只是白花 RAM
            if (Main.IsFastForwardingTime()) return false;
            //Boss 存活时禁用：把"送 Boss 昼夜暴走"从事故变成设计上不可能
            if (WorldScannable.CountActiveBosses() > 0) return false;
            //事件进行中禁用：一键掐掉全服正在打的事件太脏
            if (Main.bloodMoon || Main.eclipse
                || Main.pumpkinMoon || Main.snowMoon) {
                return false;
            }
            //冷却是权威端记的账，客户端查不到就放行，由服务端复核拒绝
            if (Main.netMode != NetmodeID.MultiplayerClient
                && Main.GameUpdateCount < worldCooldownUntil) {
                return false;
            }
            return true;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not WorldScannable) return false;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                //昼→最近边界是日落，夜→日出；旗子由原版在边界自清
                if (Main.dayTime) {
                    Main.fastForwardTimeToDusk = true;
                }
                else {
                    Main.fastForwardTimeToDawn = true;
                }
                worldCooldownUntil = Main.GameUpdateCount
                    + (ulong)WorldCooldownTicks;
                if (Main.netMode == NetmodeID.Server) {
                    //旗子随 WorldData 下发，每个客户端本地跑出同一场天空扫掠
                    NetMessage.SendData(MessageID.WorldData);
                }
            }

            if (Main.netMode != NetmodeID.Server) {
                EmitSkipCue(caster?.Center ?? Main.LocalPlayer.Center);
            }
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            //时间扫掠本体由 WorldData 旗子驱动，这里只补施术瞬间的可听可见
            if (Main.LocalPlayer?.active == true) {
                EmitSkipCue(Main.LocalPlayer.Center);
            }
        }

        //向上涌的金/紫光尘 + 一声低鸣，昼夜各取一色
        private static void EmitSkipCue(Vector2 center) {
            Color tint = Main.dayTime ? DuskViolet : DawnGold;
            for (int i = 0; i < 24; i++) {
                Vector2 pos = center + new Vector2(
                    Main.rand.NextFloat(-320f, 320f),
                    Main.rand.NextFloat(-40f, 160f));
                Vector2 vel = new(Main.rand.NextFloat(-0.3f, 0.3f),
                    Main.rand.NextFloat(-3.4f, -1.2f));
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, tint, 0.9f)
                    ?.Configure(false, 40);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.6f }, center);
                SoundEngine.PlaySound(CWRSound.Hacker with { Pitch = 0.4f }, center);
            }
        }
    }
}
