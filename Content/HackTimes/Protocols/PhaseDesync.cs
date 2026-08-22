using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 相位偏移：把目标的躯体表现和真实判定分开。<br/>
    /// 不改数值、不夺 AI：鲜活的躯体画在偏移处（见
    /// <see cref="HackNpcProtocolNPC"/> 的幽灵重绘），真身压暗并套一圈判定线框；
    /// 该 NPC 射出的弹幕出生点也套同一偏移（权威端在生成包发出前改，
    /// 各端靠首包拿到同一落点），于是它的攻击也从"看起来的位置"打出。<br/>
    /// 代价对称：你读不准它，它也打不准你
    /// </summary>
    internal class PhaseDesync : QuickHackDef
    {
        private static readonly Color Ghost = new(120, 220, 255);

        public override void SetDefaults() {
            UploadTime = 120;
            RamCost = 5;
            Category = QuickHackCategory.Covert;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 420;

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;
            //一个 NPC 只有一条偏移通道
            return !HackEffectTracker.HasEffect<PhaseDesync>(npc.whoAmI);
        }

        /// <summary>
        /// 偏移函数，各端用各自效果的 Elapsed 确定性地算同一条漂移轨迹。<br/>
        /// 头 40 帧做包络爬升，免得挂上瞬间躯体直接跳走 64px
        /// </summary>
        internal static Vector2 GetOffset(int elapsed) {
            float ramp = Math.Min(1f, elapsed / 40f);
            return new Vector2(
                MathF.Sin(elapsed / 40f) * 96f,
                MathF.Cos(elapsed / 31f) * 64f) * ramp;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;
            if (Main.netMode != NetmodeID.Server) EmitSplit(npc);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitSplit(npc);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (Main.netMode != NetmodeID.Server
                && HackTargets.TryNpc(target, out NPC npc)) {
                EmitDrift(npc, elapsed);
            }
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitDrift(npc, elapsed);
        }

        public override void OnRemove(IHackTarget target) {
            if (Main.netMode != NetmodeID.Server
                && HackTargets.TryNpc(target, out NPC npc)) {
                EmitMerge(npc);
            }
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitMerge(npc);
        }

        #region 表现

        //挂载：躯体"撕开"的一瞬，横向拉出两串错位闪点
        private static void EmitSplit(NPC npc) {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-4.5f, 4.5f),
                    Main.rand.NextFloat(-1f, 1f));
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, Ghost, 0.9f)
                    ?.Configure(false, 18);
            }
            PRTLoader.NewParticle<PRT_TBUGGlitch>(npc.Center,
                Main.rand.NextVector2Circular(1.5f, 1.5f), Ghost, 1.2f)?.Configure(26);
        }

        //持续期：幻影位置冒故障块，真身脚下留一点残点，两处都要有存在感
        private static void EmitDrift(NPC npc, int elapsed) {
            if (elapsed % 9 == 0) {
                Vector2 ghostPos = npc.Center + GetOffset(elapsed)
                    + Main.rand.NextVector2Circular(npc.width * 0.35f, npc.height * 0.35f);
                PRTLoader.NewParticle<PRT_TBUGGlitch>(ghostPos,
                    Main.rand.NextVector2Circular(0.8f, 0.8f), Ghost, 0.9f)?.Configure(20);
            }
            if (elapsed % 16 == 0) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(
                    npc.width * 0.4f, npc.height * 0.4f);
                PRTLoader.NewParticle<PRT_Spark>(pos, Vector2.Zero, Ghost, 0.4f)
                    ?.Configure(false, 12);
            }
        }

        //解除：幻影缩回真身
        private static void EmitMerge(NPC npc) {
            for (int i = 0; i < 8; i++) {
                Vector2 edge = npc.Center + Main.rand.NextVector2CircularEdge(60f, 44f);
                Vector2 vel = (npc.Center - edge).SafeNormalize(Vector2.UnitX)
                    * Main.rand.NextFloat(2.5f, 5f);
                PRTLoader.NewParticle<PRT_Spark>(edge, vel, Ghost, 0.7f)
                    ?.Configure(false, 14);
            }
        }

        #endregion
    }
}
