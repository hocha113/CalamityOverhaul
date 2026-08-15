using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.HackTimes.SelfRigs;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 神经超频：八秒内攻速 +40%、Sandevistan 消耗折半，代价是 RAM 停止回复
    /// 且每秒掉 3% 最大生命（真实伤害，最低留 1 HP）。血线跌破 25% 时安全阀提前掐断。<br/>
    /// 状态本体在 <see cref="SelfRigPlayer"/>：本类各钩子只负责把窗口计时
    /// 对到每个端上（权威 OnApply/OnTick 写真值，客户端 OnReplicated* 镜像），
    /// 攻速/掉血/抑制各自读本端计时，归属见 SelfRigPlayer 头注释
    /// </summary>
    internal class NeuralOverclock : QuickHackDef
    {
        private static readonly Color Surge = new(255, 120, 90);

        public override void SetDefaults() {
            UploadTime = 90;
            RamCost = 4;
            Category = QuickHackCategory.Lethal;
            SupportedTargets = HackTargetKind.SelfRig;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => SelfRigPlayer.OverclockDuration;

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)
                || !SelfRigPlayer.TryGet(target, out Player player, out SelfRigPlayer rig)) {
                return false;
            }
            //已在超频不叠加；血线已在安全阀死区里的开了也立刻被掐，直接拒
            return !rig.OverclockActive
                && player.statLife >= player.statLifeMax2
                    * (SelfRigPlayer.OverclockCutoffRatio + 0.05f);
        }

        public override bool CanApplyTo(IHackTarget target, Player caster) {
            return CanApplyTo(target)
                && target is SelfRigScannable rig && caster?.whoAmI == rig.PlayerIndex;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!SelfRigPlayer.TryGet(target, out Player player, out SelfRigPlayer rig)) {
                return false;
            }
            rig.OverclockFrames = SelfRigPlayer.OverclockDuration;
            if (Main.netMode != NetmodeID.Server) EmitApply(player);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (!SelfRigPlayer.TryGet(target, out Player player, out SelfRigPlayer rig)) {
                return;
            }
            rig.OverclockFrames = RemainingFrames(elapsed);
            EmitApply(player);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (!SelfRigPlayer.TryGet(target, out Player player, out SelfRigPlayer rig)) {
                return true;
            }
            //权威端安全阀真值：血线跌破 25% 提前结束（本机预测在 SelfRigPlayer 里）
            if (player.statLife < player.statLifeMax2 * SelfRigPlayer.OverclockCutoffRatio) {
                return false;
            }
            //按 elapsed 对表，丢包/漂移一帧内自愈
            rig.OverclockFrames = RemainingFrames(elapsed);
            if (Main.netMode != NetmodeID.Server) EmitTick(player, elapsed);
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (!SelfRigPlayer.TryGet(target, out Player player, out SelfRigPlayer rig)) {
                return;
            }
            //拥有者的安全阀预测已经掐掉时不要把计时刷回去，等权威端的移除包收尾；
            //否则预测在下一次复制 Tick 就被复活，血线上下抖动
            bool ownerCutoff = player.whoAmI == Main.myPlayer
                && player.statLife < player.statLifeMax2
                    * SelfRigPlayer.OverclockCutoffRatio;
            if (!ownerCutoff) {
                rig.OverclockFrames = RemainingFrames(elapsed);
            }
            EmitTick(player, elapsed);
        }

        public override void OnRemove(IHackTarget target) {
            if (!SelfRigPlayer.TryGet(target, out Player player, out SelfRigPlayer rig)) {
                return;
            }
            rig.OverclockFrames = 0;
            if (Main.netMode != NetmodeID.Server) EmitRemove(player);
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            if (!SelfRigPlayer.TryGet(target, out Player player, out SelfRigPlayer rig)) {
                return;
            }
            rig.OverclockFrames = 0;
            EmitRemove(player);
        }

        private int RemainingFrames(int elapsed)
            => Math.Max(GetDuration() - elapsed, 1);

        private static void EmitApply(Player player) {
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.8f, Pitch = 0.5f },
                player.Center);
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2.4f, 2.4f);
                PRTLoader.NewParticle<PRT_Spark>(player.Center, vel, Surge, 0.8f)
                    ?.Configure(false, 20);
            }
        }

        private static void EmitTick(Player player, int elapsed) {
            //过热的神经在头部位置间歇冒火花
            if (elapsed % 12 != 0) return;
            Vector2 pos = player.Top + new Vector2(
                Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(0f, 10f));
            PRTLoader.NewParticle<PRT_Spark>(pos,
                new Vector2(0f, Main.rand.NextFloat(-1.2f, -0.4f)), Surge, 0.45f)
                ?.Configure(false, 14);
        }

        private static void EmitRemove(Player player) {
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(1.4f, 1.4f);
                PRTLoader.NewParticle<PRT_Spark>(player.Center, vel,
                    new Color(150, 90, 80), 0.5f)?.Configure(false, 12);
            }
        }
    }
}
