using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 固件回滚：让 Boss 用上一阶段的招式表打你，到期后暴走五秒。<br/>
    /// 实现是"在 AI 通道里撒谎"：<see cref="HackNpcProtocolNPC"/> 在 PreAI 前
    /// 把 <c>npc.life</c> 顶到 95% lifeMax，PostAI 无条件还原（tML 里 PostAI
    /// 不会被 PreAI 的 false 跳过，已对上游源码核过）。真实血量不变、伤害照算。<br/>
    /// 只对相位每帧从 lifeRatio 现算的 Boss 有效，把相位闩进 newAI 的
    /// （Thanatos / Twins / AstrumDeus 等）骗不动，所以走逐 Boss 白名单，
    /// 白名单之外 <see cref="CanApplyTo(IHackTarget)"/> 直接拒绝，不收白花的 RAM。<br/>
    /// 灾厄在场时 QueenBee / Plantera 的招式表跑在灾厄自家 GlobalNPC.PreAI 里
    /// （加载序在本模组之前，伪装来不及生效），这两位只在无灾厄环境列入
    /// </summary>
    internal class FirmwareRollback : QuickHackDef
    {
        /// <summary>AI 通道里伪装的血量比例；取 Max 不取覆写，满血时不反向压血</summary>
        internal const float SpoofLifeRatio = 0.95f;
        /// <summary>到期后的暴走帧数</summary>
        internal const int FrenzyFrames = 300;

        private static readonly Color Rewind = new(120, 190, 255);
        private static readonly Color Rage = new(255, 90, 50);

        public override void SetDefaults() {
            UploadTime = 200;
            RamCost = 7;
            Category = QuickHackCategory.Control;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 600;

        /// <summary>
        /// 逐 Boss 白名单：只收"相位纯血量派生"的固件（对灾厄源码逐个核过：
        /// Yharon.phase2Check、Providence.phase2、QueenBeeAI/PlanteraAI.phase2..N
        /// 都是每帧从 lifeRatio 现算的局部布尔）
        /// </summary>
        internal static bool IsRollbackable(int npcType) {
            if (CWRRef.Has) {
                return npcType == CWRID.NPC_Yharon || npcType == CWRID.NPC_Providence;
            }
            return npcType == NPCID.QueenBee || npcType == NPCID.Plantera;
        }

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;
            return npc.boss && IsRollbackable(npc.type)
                && !HackEffectTracker.HasEffect<FirmwareRollback>(npc.whoAmI);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;
            //伪装本身由钩子每帧做（各端都跑，客户端的预测 AI 同样要被骗），这里只放表现
            if (Main.netMode != NetmodeID.Server) EmitRewind(npc);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitRewind(npc);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (Main.netMode != NetmodeID.Server
                && HackTargets.TryNpc(target, out NPC npc)) {
                EmitOldFirmware(npc, elapsed);
            }
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitOldFirmware(npc, elapsed);
        }

        public override void OnRemove(IHackTarget target) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return;
            IgniteFrenzy(npc);
            if (Main.netMode != NetmodeID.Server) EmitFrenzy(npc);
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return;
            //暴走计时是 GlobalNPC 实例字段、不进任何同步包，各端从各自的移除钩点燃
            IgniteFrenzy(npc);
            EmitFrenzy(npc);
        }

        private static void IgniteFrenzy(NPC npc) {
            if (npc.active && npc.life > 0) {
                npc.GetGlobalNPC<HackNpcProtocolNPC>().FirmwareFrenzy = FrenzyFrames;
            }
        }

        #region 表现

        //上载完成：一圈逆时针收拢的回退蓝 + 版本闪
        private static void EmitRewind(NPC npc) {
            CombatText.NewText(npc.Hitbox, Rewind,
                QuickHackDef.Get<FirmwareRollback>()?.DisplayName.Value ?? "", true);
            for (int i = 0; i < 12; i++) {
                float ang = MathHelper.TwoPi * i / 12f;
                Vector2 edge = npc.Center + ang.ToRotationVector2()
                    * (npc.width * 0.6f + 14f);
                Vector2 vel = (ang - MathHelper.PiOver2).ToRotationVector2() * -2.6f;
                PRTLoader.NewParticle<PRT_Spark>(edge, vel, Rewind, 0.9f)
                    ?.Configure(false, 22);
            }
        }

        //旧固件运转期：稳定节拍的故障块，读作"这台机器在跑不属于它的版本"
        private static void EmitOldFirmware(NPC npc, int elapsed) {
            if (elapsed % 14 != 0) return;
            Vector2 pos = npc.Center + Main.rand.NextVector2Circular(
                npc.width * 0.45f, npc.height * 0.45f);
            PRTLoader.NewParticle<PRT_TBUGGlitch>(pos,
                Main.rand.NextVector2Circular(1f, 1f), Rewind, 1.0f)?.Configure(22);
        }

        //回滚失效：过热暴走
        private static void EmitFrenzy(NPC npc) {
            for (int i = 0; i < 16; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f)
                    * Main.rand.NextFloat(0.4f, 1f);
                PRTLoader.NewParticle<PRT_SHPCThermalEmber>(npc.Center, vel, Rage, 1.1f)
                    ?.Configure(new Color(90, 18, 10), 30);
            }
        }

        #endregion
    }
}
