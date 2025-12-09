using CalamityOverhaul.Content.ADV.ADVChoices;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.ADV.Scenarios
{
    public static class ScenarioManager
    {
        private static readonly Dictionary<string, IADVScenario> scenarios = new();
        private static IADVScenario active;

        public static void Register(IADVScenario scenario, bool overwrite = false) {
            if (scenario == null) return;
            if (!scenarios.TryAdd(scenario.Key, scenario)) {
                if (!overwrite) return;
                scenarios[scenario.Key] = scenario;
            }
        }

        /// <summary>
        /// 启动指定场景
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static bool Start(string key) {
            if (!scenarios.TryGetValue(key, out var sc)) {
                sc = TryCreate(key);
                if (sc == null) return false;
                Register(sc);
            }
            //如果对话框处于激活状态或者选项框处于激活状态，则认为场景未结束
            if (IsActive()) {
                return false;
            }
            active = sc;
            sc.Start();
            return true;
        }

        /// <summary>
        /// 重置指定场景
        /// </summary>
        /// <param name="key"></param>
        public static void Reset(string key) { if (scenarios.TryGetValue(key, out var sc)) sc.Reset(); }

        private static IADVScenario TryCreate(string key) {
            var asm = typeof(ScenarioManager).Assembly;
            foreach (var t in asm.GetTypes()) {
                if (!t.IsAbstract && typeof(IADVScenario).IsAssignableFrom(t)) {
                    if (string.Equals(t.Name, key, StringComparison.Ordinal)) {
                        try { return (IADVScenario)Activator.CreateInstance(t); } catch { }
                    }
                }
            }
            return null;
        }
        /// <summary>
        /// 启动指定类型的场景
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static bool Start<T>() where T : IADVScenario, new() {
            var temp = new T();
            if (!scenarios.ContainsKey(temp.Key))
                Register(temp);
            return Start(temp.Key);
        }
        /// <summary>
        /// 重置指定类型的场景
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void Reset<T>() where T : IADVScenario, new() {
            var temp = new T();
            if (scenarios.TryGetValue(temp.Key, out var sc))
                sc.Reset();
        }
        /// <summary>
        /// 当前是否有场景处于激活状态
        /// </summary>
        /// <returns></returns>
        public static bool IsActive() => active != null && ((DialogueUIRegistry.Current?.Active ?? false) || (ADVChoiceBox.Instance?.Active ?? false));
        /// <summary>
        /// 指定场景是否处于激活状态
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static bool IsActive(string key) => IsActive() && active.Key == key;
        /// <summary>
        /// 重置所有已注册的场景
        /// </summary>
        public static void ResetAll() {
            foreach (var sc in scenarios.Values)
                sc.Reset();
            active = null;
        }
    }
}
