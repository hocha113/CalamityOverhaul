using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.HackTimes.SelfRigs;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 役鬼强驱：十秒内役鬼出手不花代价（复苏与侵蚀涨账当帧退回），
    /// 到期一次性侵蚀 +0.12 并强制休眠该鬼一分钟。<br/>
    /// 资格链原样走 <see cref="WraithAbilityService.TryResolve"/>：持鬼切这一条不动，
    /// 协议只免代价不绕资格。免费实现为权威端逐帧退款（<see cref="WraithDriveShim"/>），
    /// 不拦 <c>TryCommitUse</c> 本身；入场另要求复苏余量
    /// （当前复苏 + 该鬼单次涨幅 &lt; 1），封住"窗口内一记役使直接夺身"的缝。<br/>
    /// 账单与休眠由 <see cref="SelfRigPlayer"/> 权威侧结算，施术者死亡不豁免；
    /// 休眠位在役鬼框架 v2 已废弃，此处以强制卸下 + 看门狗降级实现
    /// </summary>
    internal class WraithForceDrive : QuickHackDef
    {
        private static readonly Color Ghost = new(140, 120, 165);

        public override void SetDefaults() {
            UploadTime = 150;
            RamCost = 6;
            Category = QuickHackCategory.Paranormal;
            SupportedTargets = HackTargetKind.SelfRig;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => SelfRigPlayer.DriveDuration;

        public override bool CanApplyTo(IHackTarget target) {
            //反射垫片不健康就整条禁用，宁可不可用，不要半生效
            if (!WraithDriveShim.Available || !base.CanApplyTo(target)
                || !SelfRigPlayer.TryGet(target, out Player player, out SelfRigPlayer rig)) {
                return false;
            }
            if (rig.DriveActive || rig.DrivePendingSettle) return false;
            if (!player.TryGetModPlayer(out WraithPlayer wraith)) return false;

            //三槽制下强驱只押得住一只：挑盘上离夺身最近的那只，
            //被它催醒的另外两只照样在爬，这是协议压不住的部分
            string key = wraith.HighestRevivalKey;
            //资格链：存活 + 手持鬼切 + 已装备该鬼 + 目录可用 + 未被夺身
            if (!WraithAbilityService.TryResolve(player, key,
                out WraithAbilityContext context)) {
                return false;
            }
            //休眠中的鬼不能再驱
            if (rig.DormantFrames > 0 && rig.DormantKey == key) return false;
            //复苏余量：窗口内单笔涨账（退款前的瞬时值）不得触顶
            return wraith.GetRevival(key) + context.Definition.RevivalCost < 1f;
        }

        public override bool CanApplyTo(IHackTarget target, Player caster) {
            return CanApplyTo(target)
                && target is SelfRigScannable rig && caster?.whoAmI == rig.PlayerIndex;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!SelfRigPlayer.TryGet(target, out Player player, out SelfRigPlayer rig)
                || !player.TryGetModPlayer(out WraithPlayer wraith)) {
                return false;
            }
            string key = wraith.HighestRevivalKey;
            if (string.IsNullOrEmpty(key)) return false;

            //权威端立账：记基线并挂待结算，退款循环从下一帧起跑
            rig.BeginDrive(key, wraith.GetRevival(key), wraith.Erosion);
            if (Main.netMode != NetmodeID.Server) EmitApply(player);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (!SelfRigPlayer.TryGet(target, out Player player, out SelfRigPlayer rig)
                || !player.TryGetModPlayer(out WraithPlayer wraith)) {
                return;
            }
            rig.MirrorDrive(wraith.HighestRevivalKey, GetDuration() - elapsed);
            EmitApply(player);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (!SelfRigPlayer.TryGet(target, out Player player, out SelfRigPlayer rig)) {
                return true;
            }
            rig.DriveFrames = Math.Max(GetDuration() - elapsed, 1);
            if (Main.netMode != NetmodeID.Server) EmitTick(player, elapsed);
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (!SelfRigPlayer.TryGet(target, out Player player, out SelfRigPlayer rig)) {
                return;
            }
            if (rig.DriveFrames > 0) {
                rig.DriveFrames = Math.Max(GetDuration() - elapsed, 1);
            }
            EmitTick(player, elapsed);
        }

        public override void OnRemove(IHackTarget target) {
            if (!SelfRigPlayer.TryGet(target, out Player player, out SelfRigPlayer rig)) {
                return;
            }
            //权威端一次性结算：侵蚀 +0.12 + 强制休眠；Pending 标记保证只结一次。
            //施术者死亡时追踪器跳过 OnRemove，账单由 SelfRigPlayer 的倒计时兜底
            rig.SettleDrive();
            if (Main.netMode != NetmodeID.Server) EmitRemove(player);
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            if (!SelfRigPlayer.TryGet(target, out Player player, out SelfRigPlayer rig)) {
                return;
            }
            //拥有者镜像休眠倒计时供扫描行显示；权威侧看门狗才是执行者。
            //先取 key 再清场：本地倒计时可能恰好先于移除包归零，不能拿 DriveFrames 判窗口
            string key = rig.DriveKey;
            rig.DriveFrames = 0;
            rig.DriveKey = string.Empty;
            if (player.whoAmI == Main.myPlayer && !string.IsNullOrEmpty(key)) {
                rig.BeginDormancy(key);
                CombatText.NewText(player.getRect(), Ghost,
                    SelfRigScanText.DriveSettleText.Value);
            }
            EmitRemove(player);
        }

        private static void EmitApply(Player player) {
            SoundEngine.PlaySound(SoundID.Zombie103 with { Volume = 0.45f, Pitch = -0.4f },
                player.Center);
            //鬼影自脚下升起绕身一圈
            for (int i = 0; i < 12; i++) {
                float ang = MathHelper.TwoPi * i / 12f;
                Vector2 pos = player.Center + ang.ToRotationVector2() * 26f;
                Vector2 vel = (ang + MathHelper.PiOver2).ToRotationVector2() * 1.6f;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, Ghost, 0.7f)
                    ?.Configure(false, 24);
            }
        }

        private static void EmitTick(Player player, int elapsed) {
            if (elapsed % 15 != 0) return;
            //役使的绳：身后一缕幽紫上飘
            Vector2 pos = player.Center + new Vector2(
                -player.direction * Main.rand.NextFloat(10f, 22f),
                Main.rand.NextFloat(-14f, 14f));
            PRTLoader.NewParticle<PRT_Spark>(pos,
                new Vector2(0f, Main.rand.NextFloat(-1.0f, -0.4f)), Ghost, 0.5f)
                ?.Configure(false, 18);
        }

        private static void EmitRemove(Player player) {
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.35f, Pitch = -0.6f },
                player.Center);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(1.8f, 1.8f)
                    - new Vector2(0f, 1.2f);
                PRTLoader.NewParticle<PRT_Spark>(player.Center, vel,
                    new Color(96, 82, 118), 0.55f)?.Configure(false, 20);
            }
        }
    }
}
