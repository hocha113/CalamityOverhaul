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
            if (scenarios.ContainsKey(scenario.Key)) {
                if (!overwrite) return;
                scenarios[scenario.Key] = scenario;
            }
            else scenarios.Add(scenario.Key, scenario);
        }

        public static bool Start(string key) {
            if (!scenarios.TryGetValue(key, out var sc)) {
                sc = TryCreate(key);
                if (sc == null) return false;
                Register(sc);
            }
            if (active != null && DialogueUIRegistry.Current.Active) {
                return false;
            }
            active = sc;
            sc.Start();
            return true;
        }

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

        public static bool Start<T>() where T : IADVScenario, new() {
            var temp = new T();
            if (!scenarios.ContainsKey(temp.Key))
                Register(temp);
            return Start(temp.Key);
        }
        public static void Reset<T>() where T : IADVScenario, new() { var temp = new T(); if (scenarios.TryGetValue(temp.Key, out var sc)) sc.Reset(); }
        public static bool IsActive(string key) => active != null && active.Key == key && DialogueUIRegistry.Current.Active;
        public static void ResetAll() { foreach (var sc in scenarios.Values) sc.Reset(); active = null; }
    }
}
