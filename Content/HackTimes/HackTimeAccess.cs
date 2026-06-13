using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>骇客时间使用条件提供者</summary>
    public interface IHackTimeAccessCondition
    {
        /// <summary>判断玩家当前帧是否满足条件</summary>
        bool IsSatisfied(Player player);
    }

    /// <summary>骇客时间使用权限管理，已注册条件按逻辑或求值</summary>
    public static class HackTimeAccess
    {
        private static readonly List<IHackTimeAccessCondition> conditions = new();

        /// <summary>注册骇客时间使用条件</summary>
        /// <param name="condition">null 或重复注册会被忽略</param>
        public static void Register(IHackTimeAccessCondition condition) {
            if (condition == null) {
                return;
            }
            if (!conditions.Contains(condition)) {
                conditions.Add(condition);
            }
        }

        /// <summary>以委托注册骇客时间使用条件</summary>
        /// <param name="predicate">传入玩家并返回是否满足</param>
        /// <param name="description">可选描述，仅调试用</param>
        /// <returns>包装实例，可用于<see cref="Unregister"/></returns>
        public static IHackTimeAccessCondition Register(Func<Player, bool> predicate, string description = null) {
            if (predicate == null) {
                return null;
            }
            var wrapper = new DelegateCondition(predicate, description);
            conditions.Add(wrapper);
            return wrapper;
        }

        /// <summary>移除已注册条件</summary>
        public static bool Unregister(IHackTimeAccessCondition condition) {
            if (condition == null) {
                return false;
            }
            return conditions.Remove(condition);
        }

        /// <summary>清空全部已注册条件，模组卸载时调用</summary>
        internal static void Reset() => conditions.Clear();

        /// <summary>判断玩家是否满足任意已注册条件</summary>
        /// <remarks>无条件注册时返回 false；默认条件由<see cref="HackTimeAccessDefaults"/>在 PostSetupContent 注册</remarks>
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
                    //单一条件抛错不影响整体判定
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
