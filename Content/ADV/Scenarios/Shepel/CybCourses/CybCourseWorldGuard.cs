using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.OtherMods.ImproveGame;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.CybCourses
{
    //进出超梦教程子世界时的主世界数据防护层
    //职责：快照灾厄Boss击杀进度 + 城镇NPC列表 + SHPC 持有总数，回到主世界后补全丢失数据并剥离教程兜底产生的多余SHPC
    //快照时机：CybCourse.Enter() 在切换世界前调用 Snapshot()
    //恢复时机：CybCoursePlayer.OnEnterWorld() 确认已回到主世界后调用 RestoreOnReturn()
    internal static class CybCourseWorldGuard
    {
        //记录进入子世界前灾厄Boss击杀标志的快照
        private static Dictionary<string, bool> _calBossFlags;

        private readonly record struct NpcEntry(int Type, int X, int Y);
        //记录进入子世界前存活的城镇NPC
        private static List<NpcEntry> _npcSnapshot;

        //进入子世界前玩家持有 SHPC 的总数快照（覆盖主背包/各类银行/垃圾槽/QoT大背包）
        //设计要点：基于"前后差量"判定教程产生的多余 SHPC，而不是为兜底物品打标签
        //1.不需要侵入 SHPC / CWRItem 的数据结构，零耦合
        //2.玩家在子世界中自由移动/堆叠/塞进银行均不影响判定
        //3.进入前已合法持有的高等级 SHPC 永远不会被误删（按 Level 升序剔除）
        //_shpcSnapshotValid 用作"一次性消费"标记，避免静态状态被错位应用
        private static int _shpcOwnedSnapshot;
        private static bool _shpcSnapshotValid;

        //进入子世界前调用，同时拍摄Boss进度、城镇NPC、SHPC 持有量三类快照
        internal static void Snapshot() {
            SnapshotCalamityFlags();
            SnapshotTownNPCs();
            SnapshotSHPCOwnership();
        }

        //拍摄灾厄Boss击杀标志（仅在灾厄Mod存在时生效）
        private static void SnapshotCalamityFlags() {
            _calBossFlags = new Dictionary<string, bool>(36);
            CWRRef.BulkCopyCalamityFlags((k, v) => _calBossFlags[k] = v);
        }

        //拍摄当前所有活跃城镇NPC的类型与坐标
        private static void SnapshotTownNPCs() {
            _npcSnapshot = new List<NpcEntry>(32);
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n.active && n.townNPC)
                    _npcSnapshot.Add(new NpcEntry(n.type, (int)n.Center.X, (int)n.Center.Y));
            }
        }

        //回到主世界后统一恢复，在CybCoursePlayer.OnEnterWorld()中调用
        internal static void RestoreOnReturn() {
            RestoreCalamityFlags();
            RestoreTownNPCs();
            PruneTutorialSHPC();
        }

        //以OR方式将快照标志补写回灾厄系统（只补true，绝不清除已有的true）
        private static void RestoreCalamityFlags() {
            if (_calBossFlags is null) return;
            CWRRef.BulkRestoreCalamityFlagsOr(k => _calBossFlags.TryGetValue(k, out bool v) && v);
            _calBossFlags = null;
        }

        //补全快照中存在、但当前世界里已消失的城镇NPC
        private static void RestoreTownNPCs() {
            if (_npcSnapshot is null) return;
            //收集当前已存在的城镇NPC类型
            HashSet<int> present = new(64);
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n.active && n.townNPC)
                    present.Add(n.type);
            }
            //补生缺失的城镇NPC，位置尽量还原到快照坐标
            foreach (NpcEntry e in _npcSnapshot) {
                if (!present.Contains(e.Type))
                    NPC.NewNPC(new EntitySource_Misc("CybCourse_NPCRestore"), e.X, e.Y, e.Type);
            }
            _npcSnapshot = null;
        }

        //拍摄玩家进入子世界前持有的 SHPC 总数
        //失败时（无效玩家）将快照标记置否，确保 RestoreOnReturn 不会拿过期数据干预
        private static void SnapshotSHPCOwnership() {
            _shpcSnapshotValid = false;
            Player p = Main.LocalPlayer;
            if (p == null || !p.active) return;
            _shpcOwnedSnapshot = CountSHPCOnPlayer(p);
            _shpcSnapshotValid = true;
        }

        //回到主世界后剔除教程兜底产生的多余 SHPC
        //核心保证：当前持有数 ≤ 进入前持有数；且按等级升序剔除，永远不会动到玩家已升级的合法武器
        private static void PruneTutorialSHPC() {
            if (!_shpcSnapshotValid) return;
            //一次性消费：无论后续是否真的修剪，都把快照标记拉低，避免静态状态被错位应用
            _shpcSnapshotValid = false;

            Player p = Main.LocalPlayer;
            if (p == null || !p.active) return;

            int currentCount = CountSHPCOnPlayer(p);
            int excess = currentCount - _shpcOwnedSnapshot;
            if (excess <= 0) return;

            RemoveLowestLevelSHPC(p, excess);
        }

        //汇总玩家所有可达存储位置中的 SHPC 数量
        //含：主背包(50格，含热键栏/钱币槽/弹药槽)、猪猪储蓄罐、保险箱、防御者熔炉、虚空袋、垃圾槽、QoT大背包
        private static int CountSHPCOnPlayer(Player p) {
            int total = 0;
            foreach (Item[] arr in EnumeratePlayerContainers(p)) {
                total += CountSHPCInArray(arr);
            }
            if (IsSHPC(p.trashItem)) total++;
            return total;
        }

        //按等级升序剔除多余 SHPC：先动 Level=0 的兜底货，最后才会触及玩家精心练度过的高级版本
        //剔除时使用 TurnToAir 原地清空，保留槽位结构
        private static void RemoveLowestLevelSHPC(Player p, int toRemove) {
            List<Item> shpcs = new(8);
            foreach (Item[] arr in EnumeratePlayerContainers(p)) {
                CollectSHPC(arr, shpcs);
            }
            if (IsSHPC(p.trashItem)) shpcs.Add(p.trashItem);

            shpcs.Sort((a, b) => SHPCOverride.GetLevel(a).CompareTo(SHPCOverride.GetLevel(b)));

            int removed = 0;
            for (int i = 0; i < shpcs.Count && removed < toRemove; i++) {
                shpcs[i].TurnToAir();
                removed++;
            }
        }

        //统一枚举玩家身上的容器数组，避免在两个方法里重复维护"包含哪些容器"
        private static IEnumerable<Item[]> EnumeratePlayerContainers(Player p) {
            yield return p.inventory;
            if (p.bank?.item != null) yield return p.bank.item;
            if (p.bank2?.item != null) yield return p.bank2.item;
            if (p.bank3?.item != null) yield return p.bank3.item;
            if (p.bank4?.item != null) yield return p.bank4.item;
            //QoT (ImproveGame) 大背包：未启用 Mod 时 GetBigBagItems 返回 null，跳过即可
            List<Item> bigBag = p.GetBigBagItems();
            if (bigBag != null && bigBag.Count > 0) {
                //取出底层数组以便后续 TurnToAir 直接落在原引用上
                yield return bigBag.ToArray();
            }
        }

        private static int CountSHPCInArray(Item[] arr) {
            if (arr == null) return 0;
            int c = 0;
            for (int i = 0; i < arr.Length; i++) {
                if (IsSHPC(arr[i])) c++;
            }
            return c;
        }

        private static void CollectSHPC(Item[] arr, List<Item> sink) {
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++) {
                if (IsSHPC(arr[i])) sink.Add(arr[i]);
            }
        }

        private static bool IsSHPC(Item item)
            => item != null && !item.IsAir && item.type == SHPCOverride.ID;
    }
}
