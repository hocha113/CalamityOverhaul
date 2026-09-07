using CalamityOverhaul.Content.GameModes;
using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using System.IO;
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
        /// 2026-09-06 光之女皇重制完成移出，当前为空
        /// </summary>
        internal static readonly HashSet<int> DisabledReworkTypes = [];

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

        #region 联机运动（契约见 tml-mp-motion-sync 与 Doc BOSS-REWORK Contract 1b）
        /// <summary>客户端位置纠偏 + 状态计时收养的共用件</summary>
        protected readonly BossNetMotion NetMotion = new();

        /// <summary>
        /// 本覆盖是否把状态计时挂上快照。<b>必须是编译期常量</b>：
        /// 收发两端据它决定流里有没有这一段，运行期判断会让两端读写不对称
        /// </summary>
        protected virtual bool CarryStateTiming => false;

        /// <summary>当前状态（供计时过线用）；返回 null 表示状态机尚未装配</summary>
        protected virtual IBossNetTiming TimedState => null;

        /// <summary>
        /// 持久累加型的摆动相位（蛇行/摆尾），随计时同包过线。<br/>
        /// 有这种量的 Boss 代理到自己的上下文字段上；没有的不必理
        /// </summary>
        protected virtual float NetSwayPhase {
            get => 0f;
            set { }
        }

        /// <summary>
        /// 客户端 AI 开头：清原版平滑并分摊纠偏，然后收养同态快照里的计时。
        /// 权威端与单机为空操作
        /// </summary>
        protected void BeginNetFrame() {
            if (!VaultUtils.isClient) {
                return;
            }
            NetMotion.BeginFrame(npc);
            //相位要在消费它的运动函数之前收养
            if (NetMotion.TakeSway(out float sway)) {
                NetSwayPhase = sway;
            }
            IBossNetTiming state = TimedState;
            if (state != null && NetMotion.TryTakeTiming(state.StateId, out int timer, out int counter)) {
                state.AdoptNetTiming(timer, counter);
            }
        }

        /// <summary>AI 末尾：客户端记下预测位置，权威端只留慢频兜底心跳（决策点各自 netUpdate）</summary>
        protected void EndNetFrame() {
            if (VaultUtils.isClient) {
                NetMotion.EndFrame(npc);
            }
            else if (Main.GameUpdateCount % BossNetMotion.HeartbeatFrames == 0) {
                npc.netUpdate = true;
            }
        }

        /// <summary>
        /// 贴锚点部件（手/臂/拳）的整帧联机处理，放在 AI 开头一次即可，覆盖所有提前返回的分支。
        /// <para>
        /// 与本体的两段式区别在于<b>不做速度预测</b>：这类部件每帧向锚点收敛
        /// （<c>Center = Lerp(Center, 锚点, k)</c> 或阻尼弹簧），下一帧位置不是
        /// <c>position + velocity</c>，硬套预测只会跟收敛打架。收敛本身即自愈——
        /// 偏了会按比例收回去，所以清掉平滑 + 收养计时 + 慢频心跳就够
        /// </para>
        /// </summary>
        protected void RunNetFrameForAnchoredPart() {
            BeginNetFrame();
            if (!VaultUtils.isClient && Main.GameUpdateCount % BossNetMotion.HeartbeatFrames == 0) {
                npc.netUpdate = true;
            }
        }

        /// <summary>
        /// 挂到状态机 <c>OnStateChanged</c>：换态包带的是新态的计时，
        /// 而新实例的 OnEnter 刚把计时清零，必须在这里补收养
        /// </summary>
        protected void AdoptTimingOnSwap(object entered) {
            if (VaultUtils.isClient && entered is IBossNetTiming state
                && NetMotion.TryTakeTiming(state.StateId, out int timer, out int counter)) {
                state.AdoptNetTiming(timer, counter);
            }
        }

        /// <summary>
        /// 计时块追加在 <see cref="NPCOverride.ai"/> 掩码之后。走追加流而非 ai 槽：
        /// InnoVault 给每个覆盖的负载带长度前缀，多写这几字节既安全又不跟各 Boss 的槽位预算打架
        /// </summary>
        public override void NetSend(BinaryWriter writer) {
            base.NetSend(writer);
            if (!CarryStateTiming) {
                return;
            }
            IBossNetTiming state = TimedState;
            BossNetMotion.WriteTiming(writer, state?.StateId ?? -1, state?.Timer ?? 0, state?.Counter ?? 0, NetSwayPhase);
        }

        /// <summary>客户端收包：ai 槽已刷新、position/velocity 已是服务端值，据计时差纠偏</summary>
        public override void NetReceive(BinaryReader reader) {
            base.NetReceive(reader);
            if (!CarryStateTiming) {
                return;
            }
            IBossNetTiming state = TimedState;
            NetMotion.ReceiveTiming(reader, npc, state?.StateId ?? -1, state?.Timer ?? 0);
        }
        #endregion
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
