using InnoVault.GameSystem;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon
{
    /// <summary>跨世界升级确认队列，仅本地客户端；UI 只读 <see cref="Current"/></summary>
    internal static class LegendUpgradeManager
    {
        /// <summary>挂起中的升级请求</summary>
        public sealed class PendingRequest
        {
            public LegendData Data;
            public Item Item;
            public int TargetLevel;
            public int OwnerWhoAmI;
            public int ItemType;
            public string WorldFullName;

            /// <summary>物品/数据/世界仍有效且仍待确认</summary>
            public bool IsStillValid() {
                if (Data == null) {
                    return false;
                }
                if (Item == null || !Item.Alives() || Item.type != ItemType) {
                    return false;
                }
                if (Item.CWR()?.LegendData != Data) {
                    return false;
                }
                if (WorldFullName != SaveWorld.WorldFullName) {
                    return false;
                }
                if (!Data.NeedUpgrade() || !Data.NeedCrossWorldConfirm()) {
                    return false;
                }
                return true;
            }
        }

        //待处理队列
        private static readonly Queue<PendingRequest> queue = new();
        //当前展示
        private static PendingRequest current;

        /// <summary>当前展示请求，可 null</summary>
        public static PendingRequest Current => current;

        /// <summary>有弹窗待展示</summary>
        public static bool HasPending => current != null;

        /// <summary>排队数(不含当前)</summary>
        public static int QueuedCount => queue.Count;

        /// <summary>入队跨世界确认；dedServ/非 myPlayer/同 Data 引用去重</summary>
        public static void Request(LegendData data, Item item, int targetLevel, Player owner) {
            //dedServ 无 UI
            if (Main.netMode == NetmodeID.Server) {
                return;
            }

            //仅 owner==myPlayer
            if (owner == null || owner.whoAmI != Main.myPlayer) {
                return;
            }

            if (data == null || item == null || item.type <= ItemID.None) {
                return;
            }

            //同 LegendData 引用已在队/展示中则忽略(不看 ItemType)
            if (current != null && ReferenceEquals(current.Data, data)) {
                return;
            }
            foreach (var pending in queue) {
                if (ReferenceEquals(pending.Data, data)) {
                    return;
                }
            }

            var req = new PendingRequest {
                Data = data,
                Item = item,
                TargetLevel = targetLevel,
                OwnerWhoAmI = owner.whoAmI,
                ItemType = item.type,
                WorldFullName = SaveWorld.WorldFullName,
            };

            if (current == null) {
                current = req;
                PlayOpenSound();
            }
            else {
                queue.Enqueue(req);
            }
        }

        /// <summary>确认并升级，推进队列</summary>
        public static void ConfirmCurrent() {
            if (current == null) {
                return;
            }
            var req = current;
            current = null;

            if (req.IsStillValid()) {
                req.Data.PerformUpgrade();
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.7f, Pitch = 0.4f });
            }

            AdvanceQueue();
        }

        /// <summary>本会话跳过，下次进世界再问</summary>
        public static void SkipCurrent() {
            if (current == null) {
                return;
            }
            var req = current;
            current = null;

            if (req.Data != null) {
                req.Data.MarkSkippedInCurrentWorld();
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.5f });
            }

            AdvanceQueue();
        }

        /// <summary>信任此世界并升级，之后该世界静默同步</summary>
        public static void TrustCurrentAndConfirm() {
            if (current == null) {
                return;
            }
            var req = current;
            current = null;

            if (req.IsStillValid()) {
                req.Data.TrustCurrentWorld();
                req.Data.PerformUpgrade();
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.7f, Pitch = 0.6f });
            }

            AdvanceQueue();
        }

        /// <summary>清空队列，不写数据</summary>
        public static void CancelAll() {
            current = null;
            queue.Clear();
        }

        /// <summary>弹出下一个有效请求</summary>
        private static void AdvanceQueue() {
            while (queue.Count > 0) {
                var next = queue.Dequeue();
                if (!next.IsStillValid()) {
                    continue;
                }
                current = next;
                PlayOpenSound();
                return;
            }
        }

        private static void PlayOpenSound() {
            if (Main.netMode == NetmodeID.Server) {
                return;
            }
            SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.5f, Pitch = 0.2f });
        }

        /// <summary>每帧剔除失效当前请求</summary>
        internal static void TickValidate() {
            if (current != null && !current.IsStillValid()) {
                current = null;
                AdvanceQueue();
            }
        }
    }

    /// <summary>世界进出时 <see cref="LegendUpgradeManager.CancelAll"/></summary>
    internal class LegendUpgradeManagerSystem : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "Legend";

        public static LocalizedText QuestManagerHint { get; private set; }
        public static LocalizedText TrialPassed { get; private set; }

        public override void SetStaticDefaults() {
            QuestManagerHint = this.GetLocalization(nameof(QuestManagerHint), () => "按下[{KEY}]打开任务列表");
            TrialPassed = this.GetLocalization(nameof(TrialPassed), () => "已通过");
        }

        public override void OnWorldUnload() => LegendUpgradeManager.CancelAll();

        public override void OnWorldLoad() => LegendUpgradeManager.CancelAll();

        public override void ClearWorld() => LegendUpgradeManager.CancelAll();
    }
}
