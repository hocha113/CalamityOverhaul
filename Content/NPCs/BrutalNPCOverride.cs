using CalamityOverhaul.Content.GameModes;
using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs
{
    /// <summary>
    /// BrutalNPCs 的 AI 覆盖基类：残酷模式世界旗标（<see cref="GameModeSystem.BrutalActive"/>）是默认门控。
    /// <see cref="DisabledReworkTypes"/> 里的类型恒不接管（重制未完成），残酷模式下走原版 AI + <c>GameModeNPC</c> 通用增强。
    /// 覆盖器在 NPC 生成时绑定，模式切换只影响此后生成的个体。
    /// 子类可用 <see cref="CanBrutalOverride"/> 越过旗标门（返回非 null 时以其为准；拒绝名单仍优先）
    /// </summary>
    internal abstract class BrutalNPCOverride : NPCOverride
    {
        /// <summary>
        /// 重制未完成、默认不接管的 NPC 类型。加 ID 即禁用，从集合移除即重新启用。
        /// </summary>
        internal static readonly HashSet<int> DisabledReworkTypes = [
            NPCID.HallowBoss,
        ];

        public sealed override bool CanOverride() {
            if (DisabledReworkTypes.Contains(TargetID)) {
                return false;
            }
            bool? result = CanBrutalOverride();
            if (result.HasValue) {
                return result.Value;
            }
            return GameModeSystem.BrutalActive;
        }

        public virtual bool? CanBrutalOverride() {
            return null;
        }
    }

    /// <summary>
    /// 绑定期接触伤基线守卫（#57 冲刺穿身无伤的真因修复）。
    /// tML 在 <c>NPCLoader.SetDefaults</c>（模组钩子）之后才执行 <c>defDamage = damage</c> 快照
    /// （末尾 <c>ScaleStats</c> 内还有二次快照），而 InnoVault 恰在钩子期绑定覆盖类：
    /// 各 Boss 的 SetProperty 初始化状态机时，SetInitialState 会立即执行入场态 OnEnter，
    /// 其中的 <c>npc.damage = 0</c> 便抢在快照之前生效，把 0 烙进 defDamage。
    /// 全舰队伤害窗口（defDamage × 系数）从出生起恒为 0：玩家接触链与敌对撞友好 NPC 链
    /// 共用 damage&gt;0 闸门而同时哑火，大师锚定也因 damage&gt;0 门槛静默跳过。
    /// 此处绑定前暂存出生伤害、绑定后发现被清零则原样交还——快照与锚定拿到真实基线，
    /// 入场态的无伤窗由其 OnUpdate 每帧重申，行为不变。两端 SetDefaults 各自确定性执行，无需同步
    /// </summary>
    internal class BrutalBindDamageGuard : ICWRLoader
    {
        /// <summary>SetDefaults 可能嵌套，配对暂存；条目带 NPC 引用，弹栈时校验防错位</summary>
        private static readonly Stack<(NPC npc, int damage)> spawnDamage = new();

        void ICWRLoader.LoadData() {
            NPCRebuildLoader.PreSetDefaultsEvent += StashSpawnDamage;
            NPCRebuildLoader.PostSetDefaultsEvent += RestoreSpawnDamage;
        }

        void ICWRLoader.UnLoadData() {
            NPCRebuildLoader.PreSetDefaultsEvent -= StashSpawnDamage;
            NPCRebuildLoader.PostSetDefaultsEvent -= RestoreSpawnDamage;
            spawnDamage.Clear();
        }

        private static void StashSpawnDamage(NPC npc) => spawnDamage.Push((npc, npc.damage));

        private static void RestoreSpawnDamage(NPC npc) {
            //嵌套是后进先出：栈顶若残留“前事件跑了、后事件被跳过”的孤儿条目，走位丢弃自愈
            int stashed = -1;
            while (spawnDamage.Count > 0) {
                (NPC owner, int damage) = spawnDamage.Pop();
                if (ReferenceEquals(owner, npc)) {
                    stashed = damage;
                    break;
                }
            }

            if (stashed <= 0 || npc.damage != 0) {
                return;
            }

            //只收口 Brutal 覆盖绑定的个体：绑定期不属于任何伤害窗口，出生基线必须交还快照
            if (!npc.TryGetOverride(out Dictionary<Type, NPCOverride> overrides)) {
                return;
            }
            foreach (NPCOverride inds in overrides.Values) {
                if (inds is BrutalNPCOverride) {
                    npc.damage = stashed;
                    return;
                }
            }
        }
    }
}
