using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>
    /// 骇客时间使用条件提供者接口
    /// <br/>实现该接口后通过<see cref="HackTimeAccess.Register(IHackTimeAccessCondition)"/>注册，
    /// 即可参与"玩家是否被允许开启骇客时间"的判定（任一条件返回 true 即算满足）
    /// </summary>
    public interface IHackTimeAccessCondition
    {
        /// <summary>
        /// 判断指定玩家在当前帧是否满足该条件
        /// </summary>
        bool IsSatisfied(Player player);
    }

    /// <summary>
    /// 骇客时间使用权限管理中心
    /// <br/>外部模块可通过<see cref="Register(IHackTimeAccessCondition)"/>或<see cref="Register(Func{Player, bool}, string)"/>
    /// 注册自定义条件（智能武器连接、网络义体植入等），实现可拓展的开启权限校验
    /// <br/>所有已注册条件按"逻辑或"求值——任何一条返回 true 即视为玩家具备骇客时间使用资格
    /// </summary>
    public static class HackTimeAccess
    {
        private static readonly List<IHackTimeAccessCondition> conditions = new();

        /// <summary>
        /// 注册一个骇客时间使用条件提供者
        /// </summary>
        /// <param name="condition">条件实例，为 null 或重复注册会被忽略</param>
        public static void Register(IHackTimeAccessCondition condition) {
            if (condition == null) {
                return;
            }
            if (!conditions.Contains(condition)) {
                conditions.Add(condition);
            }
        }

        /// <summary>
        /// 以委托形式快速注册一个骇客时间使用条件
        /// </summary>
        /// <param name="predicate">判定委托，传入玩家并返回是否满足</param>
        /// <param name="description">可选的描述文本，仅用于调试与排查</param>
        /// <returns>包装后的条件实例，可用于后续<see cref="Unregister"/></returns>
        public static IHackTimeAccessCondition Register(Func<Player, bool> predicate, string description = null) {
            if (predicate == null) {
                return null;
            }
            var wrapper = new DelegateCondition(predicate, description);
            conditions.Add(wrapper);
            return wrapper;
        }

        /// <summary>
        /// 移除一个已注册的条件
        /// </summary>
        public static bool Unregister(IHackTimeAccessCondition condition) {
            if (condition == null) {
                return false;
            }
            return conditions.Remove(condition);
        }

        /// <summary>
        /// 清空全部已注册条件，仅供模组卸载时调用
        /// </summary>
        internal static void Reset() => conditions.Clear();

        /// <summary>
        /// 判断指定玩家是否满足任意一条已注册的使用条件
        /// </summary>
        /// <remarks>
        /// 没有任何条件被注册时返回 false，避免"无门槛开启"造成的设计倒退；
        /// 默认条件由<see cref="HackTimeAccessDefaults"/>在<see cref="ModSystem.PostSetupContent"/>阶段注册
        /// </remarks>
        public static bool CanUse(Player player) {
            if (player == null || !player.active || player.dead) {
                return false;
            }

            for (int i = 0; i < conditions.Count; i++) {
                var c = conditions[i];
                if (c == null) {
                    continue;
                }
                bool ok;
                try {
                    ok = c.IsSatisfied(player);
                } catch (Exception ex) {
                    //单一条件抛错不应影响整体判定，记录后跳过
                    CWRMod.Instance.Logger.Warn($"HackTimeAccess condition threw: {ex}");
                    continue;
                }
                if (ok) {
                    return true;
                }
            }
            return false;
        }

        private sealed class DelegateCondition : IHackTimeAccessCondition
        {
            private readonly Func<Player, bool> predicate;
            public string Description { get; }

            public DelegateCondition(Func<Player, bool> predicate, string description) {
                this.predicate = predicate;
                Description = description;
            }

            public bool IsSatisfied(Player player) => predicate.Invoke(player);

            public override string ToString() => Description ?? base.ToString();
        }
    }
}
