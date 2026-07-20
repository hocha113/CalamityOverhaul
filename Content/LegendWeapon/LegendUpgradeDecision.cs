using CalamityOverhaul.Content.UIs.EntryDecisions;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon
{
    /// <summary>
    /// 传奇武器跨世界升级的入世决策，整条 <see cref="LegendUpgradeManager"/> 队列共用一个条目，
    /// 卡片内容随 <see cref="LegendUpgradeManager.Current"/> 推进而更新
    /// <br/>队列失效/清空后条目自动移除；缓存展示数据以支撑退场动画
    /// </summary>
    internal sealed class LegendUpgradeDecision : EntryDecision
    {
        public static LegendUpgradeDecision Instance { get; } = new();

        //退场动画期间 Current 已空，读缓存
        private string cachedName = string.Empty;
        private int cachedLevel;
        private int cachedItemType;

        /// <summary>刷新展示缓存，Current 为空时保持旧值</summary>
        private void Sync() {
            var current = LegendUpgradeManager.Current;
            if (current == null) {
                return;
            }
            cachedName = current.Item?.Name ?? string.Empty;
            cachedLevel = current.TargetLevel;
            cachedItemType = current.ItemType;
        }

        public override Color Accent => new(255, 200, 110);

        public override string PillText => EntryDecisionUI.LegendPill.Value;

        public override int PendingCount
            => LegendUpgradeManager.HasPending ? 1 + LegendUpgradeManager.QueuedCount : 0;

        public override string CardTitle => EntryDecisionUI.LegendTitle.Value;

        public override string CardDesc {
            get {
                Sync();
                return string.Format(EntryDecisionUI.LegendDesc.Value, cachedName, cachedLevel);
            }
        }

        public override string CardFooter {
            get {
                int queued = LegendUpgradeManager.QueuedCount;
                return queued > 0 ? string.Format(EntryDecisionUI.LegendQueue.Value, queued) : null;
            }
        }

        public override string ConfirmLabel => EntryDecisionUI.LegendConfirm.Value;
        public override string SkipLabel => EntryDecisionUI.LegendSkip.Value;
        public override string TrustLabel => EntryDecisionUI.LegendTrust.Value;

        public override bool StillValid {
            get {
                LegendUpgradeManager.TickValidate();
                return LegendUpgradeManager.HasPending;
            }
        }

        public override void Confirm() {
            Sync();
            string name = cachedName;
            int level = cachedLevel;

            LegendUpgradeManager.ConfirmCurrent();

            if (!string.IsNullOrEmpty(name)) {
                CombatText.NewText(Main.LocalPlayer.Hitbox, Color.Gold,
                    string.Format(EntryDecisionUI.LegendSuccess.Value, name, level), true);
            }
            Sync();
        }

        public override void Skip() {
            LegendUpgradeManager.SkipCurrent();
            Sync();
        }

        public override void Trust() {
            Sync();
            string name = cachedName;

            LegendUpgradeManager.TrustCurrentAndConfirm();

            if (!string.IsNullOrEmpty(name)) {
                CombatText.NewText(Main.LocalPlayer.Hitbox, new Color(150, 220, 255),
                    string.Format(EntryDecisionUI.LegendTrustSuccess.Value, name), true);
            }
            Sync();
        }

        public override void DrawIcon(SpriteBatch sb, Vector2 center, float size, float alpha) {
            Sync();
            if (cachedItemType > ItemID.None) {
                VaultUtils.SimpleDrawItem(sb, cachedItemType, center, (int)size, 0f, 0f, Color.White * alpha);
            }
        }
    }
}
