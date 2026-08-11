using CalamityOverhaul.Content.HackTimes.BossParts;
using CalamityOverhaul.Content.HackTimes.Protocols;
using CalamityOverhaul.Content.HackTimes.Targets;
using System;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes.Scannables
{
    /// <summary>
    /// Boss 部件扫描 + IHackTarget。<br/>
    /// 继承 <see cref="NpcScannable"/> 并重新实现接口：部件本身就是 NPC，
    /// 追踪器与网络层所有 <c>is NpcScannable</c> 分支照走（身份捕获、效果表归类、
    /// 线上 Npc 身份序列化都免改），接口分派则落到这里的部件专属成员上。<br/>
    /// 注意：升级到独立 BossPart 线上格式的补丁里，<c>TryCapture</c> 的类型判定
    /// 必须先查本类再查基类，否则子类永远被基类分支截走
    /// </summary>
    internal class BossPartScannable : NpcScannable, IHackTarget
    {
        public BossPartScannable(int npcIndex) : base(npcIndex) { }

        /// <summary>接口重映射：目标种类落到部件工厂上</summary>
        public new HackTargetType TargetType => HackTargetType.Get<BossPartTargetType>();

        public new int ScanRowCount => 6;

        public new void BuildScanData(string[] labels, string[] values, Color[] colors) {
            if (NpcIndex < 0 || NpcIndex >= Main.maxNPCs) return;
            NPC npc = Main.npc[NpcIndex];
            if (!npc.active) return;
            if (!BossPartResolver.TryGetPart(npc, out BossPartInfo info) || !info.IsPart) {
                //部件关系已失效（本体刚死等），退回普通 NPC 行，面板不至于空白
                base.BuildScanData(labels, values, colors);
                return;
            }
            NPC anchor = Main.npc[info.AnchorIndex];

            labels[0] = BossPartTargetType.AnchorLabel.Value;
            values[0] = anchor.FullName;
            colors[0] = HackTheme.Danger;

            labels[1] = BossPartTargetType.RoleLabel.Value;
            if (info.Role == BossPartRole.Segment) {
                BossPartResolver.GetSegmentOrdinal(npc, info.AnchorIndex,
                    out int ordinal, out int total);
                values[1] = BossPartTargetType.RoleSegmentFormat.Format(ordinal, total);
            }
            else {
                values[1] = BossPartTargetType.RoleLimb.Value;
            }
            colors[1] = HackTheme.TextBright;

            //realLife 部件的 life 是本体池的镜像，报数字会被读成部件自己的血
            labels[2] = BossPartTargetType.PartHpLabel.Value;
            if (npc.realLife >= 0) {
                values[2] = BossPartTargetType.SharedLifePool.Value;
                colors[2] = HackTheme.TextDim;
            }
            else {
                values[2] = $"{npc.life:N0} / {npc.lifeMax:N0}";
                float partPct = (float)npc.life / Math.Max(npc.lifeMax, 1);
                colors[2] = partPct > 0.5f ? HackTheme.Accent
                    : partPct > 0.25f ? HackTheme.Uploading : HackTheme.Danger;
            }

            labels[3] = BossPartTargetType.AnchorHpLabel.Value;
            values[3] = $"{anchor.life:N0} / {anchor.lifeMax:N0}";
            float hpPct = (float)anchor.life / Math.Max(anchor.lifeMax, 1);
            colors[3] = hpPct > 0.5f ? HackTheme.Accent
                : hpPct > 0.25f ? HackTheme.Uploading : HackTheme.Danger;

            labels[4] = BossPartTargetType.InvulnLabel.Value;
            bool immune = npc.dontTakeDamage || npc.immortal;
            values[4] = immune
                ? BossPartTargetType.InvulnYes.Value
                : BossPartTargetType.InvulnNo.Value;
            colors[4] = immune ? HackTheme.Danger : HackTheme.Accent;

            labels[5] = BossPartTargetType.LinkLabel.Value;
            if (HackEffectTracker.HasEffect<SegmentDelink>(NpcIndex)) {
                values[5] = BossPartTargetType.LinkDelinked.Value;
                colors[5] = HackTheme.Uploading;
            }
            else if (CommandLinkCut.CoversNpc(npc)) {
                values[5] = BossPartTargetType.LinkSevered.Value;
                colors[5] = HackTheme.AccentAlt;
            }
            else {
                values[5] = BossPartTargetType.LinkOnline.Value;
                colors[5] = HackTheme.TextDim;
            }
        }

        /// <summary>锁定框副状态：离网中报记账数，其余时候报本体血线</summary>
        public new bool TryGetLockFrameStatus(out string text, out Color color) {
            text = null;
            color = default;
            if (!IsValid) return false;
            NPC npc = Main.npc[NpcIndex];

            if (SegmentDelink.TryGetTally(NpcIndex, out long tally)) {
                text = BossPartTargetType.DelinkTallyFormat.Format(tally);
                color = HackTheme.Uploading;
                return true;
            }

            if (!BossPartResolver.TryGetPart(npc, out BossPartInfo info) || !info.IsPart) {
                return base.TryGetLockFrameStatus(out text, out color);
            }
            NPC anchor = Main.npc[info.AnchorIndex];
            if (anchor.lifeMax <= 0) return false;
            float hpPct = (float)anchor.life / anchor.lifeMax;
            text = BossPartTargetType.AnchorHpFormat.Format((int)(hpPct * 100));
            color = hpPct > 0.5f ? HackTheme.AccentAlt
                : hpPct > 0.25f ? HackTheme.Uploading : HackTheme.Danger;
            return true;
        }
    }
}
