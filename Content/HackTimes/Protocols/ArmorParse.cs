using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 装甲解析：把目标的防御压到零。<br/>
    /// 每帧覆写而不是记一次原值——不少 AI 会在自己的帧里改回 defense，
    /// 只在 OnApply 改一次撑不过一秒
    /// </summary>
    internal class ArmorParse : QuickHackDef
    {
        private static readonly Color Scan = new(200, 230, 255);

        public override void SetDefaults() {
            UploadTime = 90;
            RamCost = 3;
            Category = QuickHackCategory.Covert;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 60 * 6;

        public override bool CanApplyTo(IHackTarget target) {
            //防御本来就是零的目标，解析了也没有收益
            return base.CanApplyTo(target)
                && HackTargets.TryNpc(target, out NPC npc) && npc.defDefense > 0;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;
            if (Main.netMode != NetmodeID.Server) EmitApply(npc);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitApply(npc);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return true;
            npc.defense = 0;
            if (Main.netMode != NetmodeID.Server) EmitTick(npc, elapsed);
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitTick(npc, elapsed);
        }

        public override void OnRemove(IHackTarget target) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return;
            npc.defense = npc.defDefense;
            if (Main.netMode != NetmodeID.Server) EmitRemove(npc);
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitRemove(npc);
        }

        private static void EmitApply(NPC npc) {
            //一道自上而下的扫描格栅，读作"正在拆解外壳"
            for (int i = 0; i < 10; i++) {
                float t = i / 9f;
                Vector2 pos = new(npc.Center.X, npc.position.Y + npc.height * t);
                PRTLoader.NewParticle<PRT_Spark>(pos,
                    new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), 0f), Scan, 0.8f)
                    ?.Configure(false, 20);
            }
        }

        private static void EmitTick(NPC npc, int elapsed) {
            if (elapsed % 18 != 0) return;
            float t = elapsed % 72 / 72f;
            Vector2 pos = new(
                npc.Center.X + Main.rand.NextFloat(-npc.width * 0.5f, npc.width * 0.5f),
                npc.position.Y + npc.height * t);
            PRTLoader.NewParticle<PRT_Spark>(pos, Vector2.Zero, Scan, 0.5f)
                ?.Configure(false, 16);
        }

        private static void EmitRemove(NPC npc) {
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2f, 2f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel,
                    new Color(140, 170, 190), 0.6f)?.Configure(false, 14);
            }
        }
    }
}
