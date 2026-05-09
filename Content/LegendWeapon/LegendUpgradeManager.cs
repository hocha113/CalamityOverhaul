using InnoVault.GameSystem;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon
{
    /// <summary>
    /// 传奇武器升级请求的全局管理器
    /// <para>
    /// 集中管理跨世界升级提示的待处理请求，UI 仅作为该管理器的渲染层
    /// </para>
    /// <para>
    /// 设计要点：
    /// <list type="bullet">
    /// <item>所有状态严格限定在本地客户端，服务端永远不会持有任何请求</item>
    /// <item>请求按物品所有者绑定，只有当物品归属<see cref="Main.myPlayer"/>时才会入队</item>
    /// <item>世界进入/离开/重载时自动清空所有挂起请求，避免静态状态跨世界泄漏</item>
    /// <item>支持队列排队，避免单帧内多个传奇同时请求时丢失提示</item>
    /// <item>去重严格按 <see cref="LegendData"/> 引用：不同传奇/不同实例都会获得各自的弹窗</item>
    /// </list>
    /// </para>
    /// </summary>
    internal static class LegendUpgradeManager
    {
        /// <summary>
        /// 一个待处理的传奇升级请求
        /// </summary>
        public sealed class PendingRequest
        {
            public LegendData Data;
            public Item Item;
            public int TargetLevel;
            public int OwnerWhoAmI;
            public int ItemType;
            public string WorldFullName;

            /// <summary>
            /// 检查请求是否仍然有效（物品仍存在、数据仍归属、世界未变、仍需要升级）
            /// </summary>
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

        //待处理请求队列
        private static readonly Queue<PendingRequest> queue = new();
        //当前正在展示的请求
        private static PendingRequest current;

        /// <summary>
        /// 当前正在展示的请求，可能为 null
        /// </summary>
        public static PendingRequest Current => current;

        /// <summary>
        /// 当前是否有挂起的请求需要 UI 展示
        /// </summary>
        public static bool HasPending => current != null;

        /// <summary>
        /// 队列中等待的请求数量(不含正在展示的)
        /// </summary>
        public static int QueuedCount => queue.Count;

        /// <summary>
        /// 请求显示一次跨世界升级确认
        /// <para>
        /// 该方法会自动过滤：服务端、非本地玩家、同一<see cref="LegendData"/>实例的重复请求
        /// </para>
        /// <para>
        /// **去重规则只看<see cref="LegendData"/>引用**：不同传奇(Halibut / Murasama / SHPC)
        /// 和不同物品实例都会被视为不同的请求，每个都会获得独立的弹窗
        /// </para>
        /// </summary>
        public static void Request(LegendData data, Item item, int targetLevel, Player owner) {
            //服务端永远不持有 UI 请求
            if (Main.netMode == NetmodeID.Server) {
                return;
            }

            //本地玩家校验：只有物品归属的本地玩家才会被打扰
            if (owner == null || owner.whoAmI != Main.myPlayer) {
                return;
            }

            //基础合法性
            if (data == null || item == null || item.type <= ItemID.None) {
                return;
            }

            //去重：同一个 LegendData 已经在展示或排队中 -> 忽略
            //(注意只看 Data 引用，不看 ItemType。因为两件同型号的传奇是各自独立的实例)
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

        /// <summary>
        /// 用户确认当前请求：执行实际升级并推进队列
        /// </summary>
        public static void ConfirmCurrent() {
            if (current == null) {
                return;
            }
            var req = current;
            current = null;

            //再次校验有效性后再写入数据，避免对已失效物品执行升级
            if (req.IsStillValid()) {
                req.Data.PerformUpgrade();
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.7f, Pitch = 0.4f });
            }

            AdvanceQueue();
        }

        /// <summary>
        /// 用户跳过当前请求：仅在当前世界标记为忽略，下一次进入世界仍会再次询问
        /// </summary>
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

        /// <summary>
        /// 用户选择"信任此世界"：把当前世界加入信任列表(持久化到磁盘)，同时执行升级
        /// <para>之后再进入这个世界时，该传奇会直接静默同步，不再弹出确认</para>
        /// </summary>
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

        /// <summary>
        /// 取消所有挂起的请求(不写入任何数据)
        /// </summary>
        public static void CancelAll() {
            current = null;
            queue.Clear();
        }

        /// <summary>
        /// 推进队列：丢弃失效请求，弹出下一个有效请求作为当前
        /// </summary>
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

        /// <summary>
        /// 每帧自检，剔除变成无效的请求(物品被丢弃、被替换、世界已切换等)
        /// </summary>
        internal static void TickValidate() {
            if (current != null && !current.IsStillValid()) {
                current = null;
                AdvanceQueue();
            }
        }
    }

    /// <summary>
    /// 负责在世界生命周期事件中清理<see cref="LegendUpgradeManager"/>的全局静态状态
    /// </summary>
    internal class LegendUpgradeManagerSystem : ModSystem
    {
        public override void OnWorldUnload() => LegendUpgradeManager.CancelAll();

        public override void OnWorldLoad() => LegendUpgradeManager.CancelAll();

        public override void ClearWorld() => LegendUpgradeManager.CancelAll();
    }
}
