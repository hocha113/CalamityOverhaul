using System;
using System.Collections.Generic;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV
{
    /// <summary>
    /// 对话场景接口
    /// </summary>
    internal interface IADVScenario
    {
        string Key { get; }
        bool CanRepeat { get; }
        bool IsCompleted { get; }
        void Start();
        void Reset();
    }

    internal abstract class ADVScenarioBase : VaultType<ADVScenarioBase>, IADVScenario
    {
        public abstract string Key { get; }
        public virtual bool CanRepeat => false;
        public bool IsCompleted { get; private set; }

        private bool built = false;
        private readonly List<(string speaker, string content)> lines = new();

        protected abstract void Build();

        protected override void VaultRegister() => ScenarioManager.Register(this);
        public override void VaultSetup() {
            DialogueBoxBase.OnPreProcessSegment += PreProcessSegment;
            SetStaticDefaults();
        }
        public override void Unload() => DialogueBoxBase.OnPreProcessSegment -= PreProcessSegment;

        public virtual void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) { }

        protected void Add(string speaker, string content) => lines.Add((speaker, content));

        public void Start() {
            if (IsCompleted && !CanRepeat) return;
            if (!built) { Build(); built = true; }
            if (lines.Count == 0) { Complete(); return; }
            var box = DialogueUIRegistry.Current;
            for (int i = 0; i < lines.Count; i++) {
                var l = lines[i]; bool last = i == lines.Count - 1;
                if (last) box.EnqueueDialogue(l.speaker, l.content, Complete);
                else box.EnqueueDialogue(l.speaker, l.content);
            }
        }

        private void Complete() { if (!IsCompleted) { IsCompleted = true; OnComplete(); } }
        protected virtual void OnComplete() { }
        public void Reset() => IsCompleted = false;
    }

    internal static class ScenarioManager
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
            if (active != null && DialogueUIRegistry.Current.Active) return false;
            active = sc; sc.Start(); return true;
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

        public static bool Start<T>() where T : IADVScenario, new() { var temp = new T(); if (!scenarios.ContainsKey(temp.Key)) Register(temp); return Start(temp.Key); }
        public static void Reset<T>() where T : IADVScenario, new() { var temp = new T(); if (scenarios.TryGetValue(temp.Key, out var sc)) sc.Reset(); }
        public static bool IsActive(string key) => active != null && active.Key == key && DialogueUIRegistry.Current.Active;
        public static void ResetAll() { foreach (var sc in scenarios.Values) sc.Reset(); active = null; }
    }
}
