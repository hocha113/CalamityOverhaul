using CalamityOverhaul.Content.HackTimes.BossParts;
using CalamityOverhaul.Content.HackTimes.Protocols;
using CalamityOverhaul.Content.HackTimes.Scannables;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Targets
{
    /// <summary>
    /// Boss 部件目标：悬停对象是「已注册 Boss 群组的非本体部件」时命中。<br/>
    /// 优先级高于 <see cref="NpcTargetType"/>，部件优先于整体被选中。<br/>
    /// 探测带持有闸：本机玩家一条部件协议都没有时不出手，
    /// 未入坑的玩家在虫节上看到的仍是原来的 NPC 面板，零行为变化
    /// </summary>
    internal class BossPartTargetType : HackTargetType
    {
        public override HackTargetKind Kind => HackTargetKind.BossPart;

        //高于 NpcTargetType 的 100，低于将来的 SelfRig
        public override int HoverPriority => 120;

        #region 扫描面板与锁定框文案

        internal static LocalizedText AnchorLabel { get; private set; }
        internal static LocalizedText RoleLabel { get; private set; }
        internal static LocalizedText PartHpLabel { get; private set; }
        internal static LocalizedText AnchorHpLabel { get; private set; }
        internal static LocalizedText InvulnLabel { get; private set; }
        internal static LocalizedText LinkLabel { get; private set; }
        internal static LocalizedText RoleSegmentFormat { get; private set; }
        internal static LocalizedText RoleLimb { get; private set; }
        internal static LocalizedText SharedLifePool { get; private set; }
        internal static LocalizedText InvulnYes { get; private set; }
        internal static LocalizedText InvulnNo { get; private set; }
        internal static LocalizedText LinkOnline { get; private set; }
        internal static LocalizedText LinkSevered { get; private set; }
        internal static LocalizedText LinkDelinked { get; private set; }
        internal static LocalizedText AnchorHpFormat { get; private set; }
        internal static LocalizedText DelinkTallyFormat { get; private set; }

        public override void SetStaticDefaults() {
            AnchorLabel = this.GetLocalization(nameof(AnchorLabel), () => "Anchor");
            RoleLabel = this.GetLocalization(nameof(RoleLabel), () => "Role");
            PartHpLabel = this.GetLocalization(nameof(PartHpLabel), () => "Part HP");
            AnchorHpLabel = this.GetLocalization(nameof(AnchorHpLabel), () => "Anchor HP");
            InvulnLabel = this.GetLocalization(nameof(InvulnLabel), () => "Shield");
            LinkLabel = this.GetLocalization(nameof(LinkLabel), () => "Link");
            RoleSegmentFormat = this.GetLocalization(nameof(RoleSegmentFormat),
                () => "Segment {0}/{1}");
            RoleLimb = this.GetLocalization(nameof(RoleLimb), () => "Limb Unit");
            SharedLifePool = this.GetLocalization(nameof(SharedLifePool),
                () => "Shared With Anchor");
            InvulnYes = this.GetLocalization(nameof(InvulnYes), () => "Immune");
            InvulnNo = this.GetLocalization(nameof(InvulnNo), () => "Breakable");
            LinkOnline = this.GetLocalization(nameof(LinkOnline), () => "Online");
            LinkSevered = this.GetLocalization(nameof(LinkSevered), () => "Severed");
            LinkDelinked = this.GetLocalization(nameof(LinkDelinked), () => "Delinked");
            AnchorHpFormat = this.GetLocalization(nameof(AnchorHpFormat),
                () => "Anchor {0}%");
            DelinkTallyFormat = this.GetLocalization(nameof(DelinkTallyFormat),
                () => "Logged {0}");
        }

        #endregion

        public override IHackTarget TryDetectHovered(Vector2 mouseWorld) {
            //持有闸：三条部件协议都没有就不参与探测
            Player local = Main.LocalPlayer;
            if (local?.active != true || !OwnsAnyPartProtocol(local)) {
                return null;
            }

            int bestIndex = -1;
            float bestDistSq = float.MaxValue;
            const float expandMargin = 16f;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!HackTime.IsHackableTarget(npc)) continue;
                if (!BossPartResolver.TryGetPart(npc, out _)) continue;

                float left = npc.position.X - expandMargin;
                float top = npc.position.Y - expandMargin;
                float right = npc.position.X + npc.width + expandMargin;
                float bottom = npc.position.Y + npc.height + expandMargin;

                if (mouseWorld.X < left || mouseWorld.X > right) continue;
                if (mouseWorld.Y < top || mouseWorld.Y > bottom) continue;

                float dx = mouseWorld.X - npc.Center.X;
                float dy = mouseWorld.Y - npc.Center.Y;
                float distSq = dx * dx + dy * dy;
                if (distSq < bestDistSq) {
                    bestDistSq = distSq;
                    bestIndex = i;
                }
            }

            return bestIndex < 0 ? null : new BossPartScannable(bestIndex);
        }

        private static bool OwnsAnyPartProtocol(Player player) {
            return Owns<SegmentDelink>(player)
                || Owns<LimbSeizure>(player)
                || Owns<CommandLinkCut>(player);
        }

        private static bool Owns<T>(Player player) where T : QuickHackDef {
            QuickHackDef hack = QuickHackDef.Get<T>();
            return hack != null && HackProtocolOwned.Owns(player, hack);
        }
    }
}
