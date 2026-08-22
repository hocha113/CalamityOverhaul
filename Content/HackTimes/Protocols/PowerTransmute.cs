using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.HackTimes.SelfRigs;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.RAMSystems;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 电能折算：把手持储电物品的 UE 按 500:1 折成 RAM，随即放空整管电。<br/>
    /// 全套唯一一条跨资源兑换协议。三道经济闸：单次上限 6 RAM、自身冷却 3600f、
    /// 只读手持物（换手/换背包都要重新亮出来）。放空是整管而不是按需
    /// 大容量电池的多余存电全部作废，罐越大越亏，这是对"拿发电农场白嫖 RAM"的主要惩罚。<br/>
    /// 结算落点拆两半：RAM 归权威端直接写；UE 在手持物上、物品归拥有者客户端，
    /// 服务端写不进（SYSTEMS.md "RAM request bus contract"），
    /// 所以权威端只按自己的镜像定额入账，放空由拥有者本机在复制回执里执行
    /// </summary>
    internal class PowerTransmute : QuickHackDef
    {
        private static readonly Color Spark = new(255, 230, 120);

        public override void SetDefaults() {
            UploadTime = 60;
            RamCost = 2;
            Category = QuickHackCategory.Covert;
            SupportedTargets = HackTargetKind.SelfRig;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 0;

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)
                || !SelfRigPlayer.TryGet(target, out Player player, out SelfRigPlayer rig)) {
                return false;
            }
            if (rig.TransmuteCooldown > 0) return false;
            //RAM 锁定期 Restore 必败，先拒掉别收上传费
            if (player.GetModPlayer<RAMPlayer>().IsLocked) return false;
            Item held = player.HeldItem;
            return held?.IsAir == false && held.CWR()?.StorageUE == true
                && held.CWR().UEValue >= SelfRigPlayer.TransmuteUEPerRam;
        }

        public override bool CanApplyTo(IHackTarget target, Player caster) {
            //自我目标恒等：目标只能是施术者本人
            return CanApplyTo(target)
                && target is SelfRigScannable rig && caster?.whoAmI == rig.PlayerIndex;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!SelfRigPlayer.TryGet(target, out Player player, out SelfRigPlayer rig)) {
                return false;
            }
            CWRItem cwr = player.HeldItem?.CWR();
            if (cwr?.StorageUE != true) return false;

            //权威端按自己的物品镜像定额；镜像与拥有者的差最多一个同步周期，误差只体现在折算额
            int ram = Math.Min((int)(cwr.UEValue / SelfRigPlayer.TransmuteUEPerRam),
                SelfRigPlayer.TransmuteMaxRam);
            if (ram < 1 || !RamSystem.Restore(player, ram, out _)) return false;

            rig.TransmuteCooldown = SelfRigPlayer.TransmuteCooldownFrames;
            //单人/自建主机：权威端即拥有者，直接放空
            if (player.whoAmI == Main.myPlayer && !Main.dedServ) {
                SettleOwner(player, rig);
            }
            if (Main.netMode != NetmodeID.Server) EmitCue(player);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (!SelfRigPlayer.TryGet(target, out Player player, out SelfRigPlayer rig)) {
                return;
            }
            //拥有者在复制回执里放空手上的电并镜像冷却；旁观者只看表现
            if (player.whoAmI == Main.myPlayer) {
                SettleOwner(player, rig);
            }
            EmitCue(player);
        }

        /// <summary>拥有者本机结算：放空手持 UE、镜像冷却、飘字</summary>
        private static void SettleOwner(Player player, SelfRigPlayer rig) {
            rig.TransmuteCooldown = SelfRigPlayer.TransmuteCooldownFrames;
            CWRItem cwr = player.HeldItem?.CWR();
            if (cwr?.StorageUE != true || cwr.UEValue <= 0f) return;

            int gained = Math.Min((int)(cwr.UEValue / SelfRigPlayer.TransmuteUEPerRam),
                SelfRigPlayer.TransmuteMaxRam);
            cwr.UEValue = 0f;
            if (gained > 0) {
                CombatText.NewText(player.getRect(), HackTheme.ProgressGlow,
                    SelfRigScanText.TransmuteGainFormat.Format(gained));
            }
        }

        private static void EmitCue(Player player) {
            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.6f, Pitch = 0.35f },
                player.Center);
            //电荷自躯干向头顶抽走的一串火花，读作"电被抽走了"
            for (int i = 0; i < 14; i++) {
                float t = i / 13f;
                Vector2 pos = player.Bottom + new Vector2(
                    Main.rand.NextFloat(-player.width * 0.6f, player.width * 0.6f),
                    -player.height * t);
                Vector2 vel = new(Main.rand.NextFloat(-0.6f, 0.6f),
                    Main.rand.NextFloat(-2.8f, -1.2f));
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, Spark, 0.7f)
                    ?.Configure(false, 22);
            }
        }
    }
}
