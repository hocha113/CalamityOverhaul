using CalamityOverhaul.Content.HackTimes.BossParts;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 协同断链：十秒内 Exo Mechs 的协同图对成员 AI 读不到——
    /// 卫星不再继承队长仇恨、各自 <c>TargetClosest</c>，
    /// 队长「其他机体还活着」的记账落空，被动/免疫相位判据失效，
    /// 三机战短时间退化成各打各的单机战。<br/>
    /// 实现走 <see cref="BossPartAiSpoof"/> 的配对伪装：每个成员 AI 前把
    /// <c>CalamityGlobalNPC.draedonExoMech*</c> 四槽临时写 -1，AI 跑完按
    /// 「仍是 -1 才还原」的规则放回，绝不覆盖 AI 刚写入的新注册。
    /// 全程 <see cref="ExoLinkRef"/> 反射，任一字段缺失整条协议失活（宁可不可用，不要半生效）。<br/>
    /// 漏还兜底：这四槽 Calamity 每帧自校验、各机体 AI 每帧重新注册，
    /// 极端漏还也会在下一帧被上游自愈——没有永久坏值路径
    /// </summary>
    internal class CommandLinkCut : QuickHackDef
    {
        private static readonly Color Sever = new(122, 222, 255);

        public override void SetDefaults() {
            UploadTime = 210;
            RamCost = 7;
            Category = QuickHackCategory.Covert;
            SupportedTargets = HackTargetKind.BossPart;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 600;

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;
            //协同图字段任何一个反射不到就整条拒绝
            if (!ExoLinkRef.Ready) return false;
            //目标得是协同图成员里的部件（Ares 炮组 / Thanatos 体节）
            return BossPartResolver.TryGetPart(npc, out BossPartInfo info)
                && info.IsPart && BossPartResolver.IsExoGroupMember(npc);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;
            BossPartAiSpoof.RefreshLinkCut();
            if (Main.netMode != NetmodeID.Server) EmitSeverAll(npc);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitSeverAll(npc);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            //反射缓存中途失效（理论上只在卸载竞态出现）就立刻收摊
            if (!ExoLinkRef.Ready) return false;
            BossPartAiSpoof.RefreshLinkCut();
            if (Main.netMode != NetmodeID.Server
                && HackTargets.TryNpc(target, out NPC npc)) {
                EmitPulse(npc, elapsed);
            }
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitPulse(npc, elapsed);
        }

        public override void OnRemove(IHackTarget target) {
            //刻意不在这里关窗：窗口靠停止刷新在两帧内自然过期。
            //两条断链叠加时，先到期的那条硬关窗会给幸存者切出一帧协同恢复的缝
            if (Main.netMode != NetmodeID.Server
                && HackTargets.TryNpc(target, out NPC npc)) {
                EmitRelink(npc);
            }
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitRelink(npc);
        }

        public override void Unload() {
            base.Unload();
            BossPartAiSpoof.ClearLinkCut();
        }

        #region 状态查询

        /// <summary>
        /// 该 NPC 是否被当前断链窗口罩住。扫描面板用；
        /// 复制端的效果也在同一张表里，客户端同样能答
        /// </summary>
        internal static bool CoversNpc(NPC npc) {
            if (npc == null || !BossPartResolver.IsExoGroupMember(npc)) {
                return false;
            }
            IReadOnlyList<ActiveHackEffect> effects
                = HackEffectTracker.AllActiveEffects;
            for (int i = 0; i < effects.Count; i++) {
                ActiveHackEffect effect = effects[i];
                if (effect.Active && effect.Hack is CommandLinkCut) {
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region 表现

        //断链瞬间：从目标向每个在场协同成员拉一条断开的虚线
        private static void EmitSeverAll(NPC origin) {
            foreach (NPC other in Main.ActiveNPCs) {
                if (other.whoAmI == origin.whoAmI
                    || !BossPartResolver.IsExoGroupMember(other)) {
                    continue;
                }
                EmitSeverLine(origin.Center, other.Center);
            }
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3f, 3f);
                PRTLoader.NewParticle<PRT_Spark>(origin.Center, vel, Sever, 0.9f)
                    ?.Configure(false, 22);
            }
        }

        //虚线只画一半的点位，中段留一个「断口」
        private static void EmitSeverLine(Vector2 from, Vector2 to) {
            Vector2 delta = to - from;
            int steps = (int)MathHelper.Clamp(delta.Length() / 90f, 3f, 9f);
            for (int i = 0; i <= steps; i++) {
                float t = i / (float)steps;
                if (t > 0.38f && t < 0.62f) continue;
                PRTLoader.NewParticle<PRT_Spark>(from + delta * t, Vector2.Zero,
                    Sever, 0.55f)?.Configure(false, 18);
            }
        }

        private static void EmitPulse(NPC npc, int elapsed) {
            if (elapsed % 24 != 0) return;
            PRTLoader.NewParticle<PRT_Spark>(npc.Center, Vector2.Zero, Sever, 0.5f)
                ?.Configure(false, 14);
        }

        private static void EmitRelink(NPC npc) {
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2.4f, 2.4f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel,
                    new Color(90, 160, 190), 0.6f)?.Configure(false, 16);
            }
        }

        #endregion
    }
}
