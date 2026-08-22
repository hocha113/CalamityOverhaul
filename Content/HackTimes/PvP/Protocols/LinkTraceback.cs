using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.PvP.Protocols
{
    /// <summary>
    /// 链路回溯（默认档，基础反制之一：掐上传）。<b>挂 SelfRig 位不挂 Player 位</b>
    /// 目标是你自己，对自己施放不过 PvP 准入门槛，被骇是唯一前置。<br/>
    /// 服务端裁决（OnApply 跑在权威端）：立即作废所有正在瞄准施术者的上传
    /// （<b>攻击方 RAM 不退</b>：被回溯的上传白丢，这是攻击方的风险成本）、
    /// 每个被作废的攻击方吃 2 RAM 烧蚀（RAM 归服务端，直写）、
    /// 攻击方位置对施术者穿墙标记 900f（坐标广播的镜像，只发施术者）。<br/>
    /// 自身冷却 900f：真值记在服务端的 <see cref="PlayerHackLedger"/> 实例上，
    /// 施术者本机在 OnReplicatedApply 里镜像一份供面板灰显
    /// </summary>
    internal class LinkTraceback : QuickHackDef
    {
        public override void SetDefaults() {
            UploadTime = 40;
            RamCost = 2;
            Category = QuickHackCategory.Covert;
            SupportedTargets = HackTargetKind.SelfRig;
            //默认档：人人在手的基础反制
            UnlockedByDefault = true;
        }

        public override int GetDuration() => 0;

        public override bool CanApplyTo(IHackTarget target) {
            if (target is not SelfRigScannable rig) return false;
            Player player = rig.ResolvePlayer();
            if (player == null
                || !player.TryGetModPlayer(out PlayerHackLedger ledger)
                || ledger.TracebackCooldown > 0) {
                return false;
            }
            //被骇是唯一前置：帐本非空或有活跃来袭上传。
            //本机端读自己的帐本；权威端帐本是空的，读授予账与上传队列
            if (Main.netMode != NetmodeID.Server) {
                return ledger.HasHostileEffects || ledger.HasActiveIncomingUpload;
            }
            return PlayerHackAuthority.HasUploadsAimedAt(player.whoAmI)
                || PlayerHackAuthority.HasGrantsOn(player.whoAmI);
        }

        public override bool CanApplyTo(IHackTarget target, Player caster) {
            //自我目标恒等：目标只能是施术者本人
            return CanApplyTo(target)
                && target is SelfRigScannable rig && caster?.whoAmI == rig.PlayerIndex;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not SelfRigScannable rig
                || caster?.whoAmI != rig.PlayerIndex) {
                return false;
            }
            //作废数为零也算施放成功（赌时机是施术者的风险），冷却照吃
            PlayerHackAuthority.ExecuteTraceback(caster);
            if (Main.netMode != NetmodeID.Server) EmitCue(caster);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (target is not SelfRigScannable rig) return;
            Player player = rig.ResolvePlayer();
            if (player == null) return;
            //施术者本机镜像冷却（面板灰显）；标记数据另经 TracebackResult 到帐本
            if (player.whoAmI == Main.myPlayer
                && player.TryGetModPlayer(out PlayerHackLedger ledger)) {
                ledger.TracebackCooldown = PlayerHackAuthority.TracebackCooldownFrames;
            }
            EmitCue(player);
        }

        /// <summary>反向追踪起手式：向外炸开一圈故障切片 + 低啸警报</summary>
        private static void EmitCue(Player player) {
            SoundEngine.PlaySound(CWRSound.Hacker with { Volume = 0.75f, Pitch = 0.4f },
                player.Center);
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20f;
                Vector2 vel = angle.ToRotationVector2()
                    * Main.rand.NextFloat(2.2f, 4.6f);
                PRTLoader.NewParticle<PRT_TBUGGlitch>(player.Center, vel, default,
                    Main.rand.NextFloat(0.6f, 1.1f))?.Configure(30);
            }
        }
    }
}
