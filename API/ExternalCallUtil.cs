using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Terraria;
using Terraria.Localization;

namespace CalamityOverhaul.API
{
    /// <summary>ModCall / 门面共用的参数拆箱，失败只记日志</summary>
    internal static class ExternalCallUtil
    {
        internal static void LogFailed(string command, string reason) {
            CWRMod.Instance?.Logger.Warn($"[CWR API] {command}: {reason}");
        }

        internal static bool TryAsMap(object raw, out Dictionary<string, object> map) {
            map = null;
            if (raw is Dictionary<string, object> typed) {
                map = typed;
                return true;
            }
            if (raw is IDictionary<string, object> generic) {
                map = new Dictionary<string, object>(generic, StringComparer.OrdinalIgnoreCase);
                return true;
            }
            if (raw is IDictionary legacy) {
                map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (DictionaryEntry entry in legacy) {
                    if (entry.Key != null) {
                        map[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)] = entry.Value;
                    }
                }
                return map.Count > 0;
            }
            return false;
        }

        internal static bool TryGet(IDictionary<string, object> map, string key, out object value) {
            if (map == null) {
                value = null;
                return false;
            }
            if (map.TryGetValue(key, out value) && value != null) {
                return true;
            }
            foreach (var kv in map) {
                if (kv.Value != null && string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) {
                    value = kv.Value;
                    return true;
                }
            }
            value = null;
            return false;
        }

        internal static bool TryGet(IDictionary<string, object> map, out object value, params string[] keys) {
            foreach (string key in keys) {
                if (TryGet(map, key, out value)) {
                    return true;
                }
            }
            value = null;
            return false;
        }

        internal static string ReadString(IDictionary<string, object> map, params string[] keys) {
            if (!TryGet(map, out object raw, keys) || raw == null) {
                return null;
            }
            return Convert.ToString(raw, CultureInfo.InvariantCulture);
        }

        internal static bool TryReadBool(IDictionary<string, object> map, bool fallback, out bool value, params string[] keys) {
            value = fallback;
            if (!TryGet(map, out object raw, keys) || raw == null) {
                return false;
            }
            try {
                value = Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch {
                return false;
            }
        }

        internal static bool TryReadInt(object raw, out int value) {
            value = 0;
            if (raw == null) {
                return false;
            }
            try {
                value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch {
                return false;
            }
        }

        internal static bool TryReadFloat(object raw, out float value) {
            value = 0f;
            if (raw == null) {
                return false;
            }
            try {
                value = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch {
                return false;
            }
        }

        /// <summary>
        /// 外模文案：<see cref="LocalizedText"/> 原样返回；string 字面量包成绑定文本。<br/>
        /// 不能走 <c>Language.GetOrRegister("Mods.CalamityOverhaul....")</c>：
        /// 开发机上 tML 会在 FinishSetup 把该前缀下未落文件的 key 写进本模组 hjson，
        /// 且切语言时 ReloadLanguage 会把所有值重置为 key，注册时给的默认值不会重放。<br/>
        /// <c>Language.GetText("{0}")</c> 这个 key 从未注册，取值恒为 "{0}"；
        /// <c>WithFormatArgs</c> 绑定后重算仍得字面量，也不会被写回任何 hjson
        /// </summary>
        internal static LocalizedText CoerceText(object raw) {
            if (raw is LocalizedText text) {
                return text;
            }
            if (raw is string literal) {
                if (string.IsNullOrEmpty(literal)) {
                    return LocalizedText.Empty;
                }
                return Language.GetText(LiteralFormatKey).WithFormatArgs(literal);
            }
            return LocalizedText.Empty;
        }

        private const string LiteralFormatKey = "{0}";

        internal static bool TryBindComplete(object raw, out Func<Player, bool> asBool, out Func<Player, float> asFloat) {
            asBool = null;
            asFloat = null;
            switch (raw) {
                case Func<Player, bool> fb:
                    asBool = fb;
                    return true;
                case Func<Player, float> ff:
                    asFloat = ff;
                    return true;
                case Predicate<Player> pred:
                    asBool = pred.Invoke;
                    return true;
                case Delegate del:
                    return TryAdaptDelegate(del, out asBool, out asFloat);
                default:
                    return false;
            }
        }

        private static bool TryAdaptDelegate(Delegate del, out Func<Player, bool> asBool, out Func<Player, float> asFloat) {
            asBool = null;
            asFloat = null;
            var method = del.Method;
            var pars = method.GetParameters();
            if (pars.Length != 1 || !typeof(Player).IsAssignableFrom(pars[0].ParameterType)) {
                return false;
            }
            Type ret = method.ReturnType;
            if (ret == typeof(bool)) {
                asBool = player => (bool)del.DynamicInvoke(player);
                return true;
            }
            if (ret == typeof(float) || ret == typeof(double) || ret == typeof(int)) {
                asFloat = player => Convert.ToSingle(del.DynamicInvoke(player), CultureInfo.InvariantCulture);
                return true;
            }
            return false;
        }

        internal static IEnumerable<(int item, int stack)> EnumerateRewards(object raw) {
            if (raw == null) {
                yield break;
            }
            if (raw is IDictionary<string, object> singleMap) {
                if (TryReadReward(singleMap, out var one)) {
                    yield return one;
                }
                yield break;
            }
            if (raw is IEnumerable seq && raw is not string) {
                foreach (object item in seq) {
                    if (TryReadReward(item, out var pair)) {
                        yield return pair;
                    }
                }
            }
        }

        private static bool TryReadReward(object raw, out (int item, int stack) reward) {
            reward = (0, 0);
            if (raw == null) {
                return false;
            }
            if (raw is IDictionary<string, object> map) {
                if (!TryGet(map, out object itemRaw, "item", "itemId", "type") || !TryReadInt(itemRaw, out int item)) {
                    return false;
                }
                int stack = 1;
                if (TryGet(map, out object stackRaw, "stack", "amount", "count")) {
                    TryReadInt(stackRaw, out stack);
                }
                reward = (item, stack);
                return true;
            }
            if (raw is IDictionary legacy) {
                var boxed = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (DictionaryEntry entry in legacy) {
                    if (entry.Key != null) {
                        boxed[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)] = entry.Value;
                    }
                }
                return TryReadReward(boxed, out reward);
            }
            if (raw is IList list && list.Count >= 1) {
                if (!TryReadInt(list[0], out int item)) {
                    return false;
                }
                int stack = 1;
                if (list.Count >= 2) {
                    TryReadInt(list[1], out stack);
                }
                reward = (item, stack);
                return true;
            }
            return false;
        }
    }
}
