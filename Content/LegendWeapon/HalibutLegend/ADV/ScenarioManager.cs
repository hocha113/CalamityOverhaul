using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV
{
    /// <summary>
    /// 对话场景接口
    /// </summary>
    internal interface IADVScenario
    {
        /// <summary>唯一键值(建议使用类名)</summary>
        string Key { get; }
        /// <summary>是否允许重复播放</summary>
        bool CanRepeat { get; }
        /// <summary>是否已经完成</summary>
        bool IsCompleted { get; }
        /// <summary>开始该场景(应当自行把需要的对话加入DialogueBox)</summary>
        void Start();
        /// <summary>重置完成状态</summary>
        void Reset();
    }

    /// <summary>
    /// 简易的场景基类, 帮助构建对话序列
    /// </summary>
    internal abstract class ADVScenarioBase : VaultType<ADVScenarioBase>, IADVScenario
    {
        public abstract string Key { get; }
        public virtual bool CanRepeat => false;
        public bool IsCompleted { get; private set; }

        private bool built = false;
        private readonly List<(string speaker, string content)> lines = new();

        protected abstract void Build(); //子类实现添加行

        protected override void VaultRegister() {
            ScenarioManager.Register(this);
        }

        protected void Add(string speaker, string content) {
            lines.Add((speaker, content));
        }

        public void Start() {
            if (IsCompleted && !CanRepeat) {
                return;
            }
            if (!built) {
                Build();
                built = true;
            }
            if (lines.Count == 0) {
                Complete();
                return;
            }
            for (int i = 0; i < lines.Count; i++) {
                var l = lines[i];
                bool last = i == lines.Count - 1;
                if (last) {
                    DialogueBox.Instance.EnqueueDialogue(l.speaker, l.content, Complete);
                }
                else {
                    DialogueBox.Instance.EnqueueDialogue(l.speaker, l.content);
                }
            }
        }

        private void Complete() {
            if (!IsCompleted) {
                IsCompleted = true;
                OnComplete();
            }
        }

        protected virtual void OnComplete() { }

        public void Reset() {
            IsCompleted = false;
        }
    }

    /// <summary>
    /// 场景管理器: 负责注册与启动
    /// </summary>
    internal static class ScenarioManager
    {
        private static readonly Dictionary<string, IADVScenario> scenarios = new();
        private static IADVScenario active;

        public static void Register(IADVScenario scenario, bool overwrite = false) {
            if (scenario == null) {
                return;
            }
            if (scenarios.ContainsKey(scenario.Key)) {
                if (!overwrite) {
                    return;
                }
                scenarios[scenario.Key] = scenario;
            }
            else {
                scenarios.Add(scenario.Key, scenario);
            }
        }

        public static bool Start(string key) {
            if (!scenarios.TryGetValue(key, out var sc)) {
                sc = TryCreate(key); //尝试自动反射创建
                if (sc == null) {
                    return false;
                }
                Register(sc);
            }
            if (active != null && DialogueBox.Instance.Active) {
                return false; //当前仍在播放其它场景
            }
            active = sc;
            sc.Start();
            return true;
        }

        private static IADVScenario TryCreate(string key) {
            var asm = typeof(ScenarioManager).Assembly;
            foreach (var t in asm.GetTypes()) {
                if (!t.IsAbstract && typeof(IADVScenario).IsAssignableFrom(t)) {
                    if (string.Equals(t.Name, key, StringComparison.Ordinal)) {
                        try {
                            return (IADVScenario)Activator.CreateInstance(t);
                        }
                        catch {
                        }
                    }
                }
            }
            return null;
        }

        public static bool Start<T>() where T : IADVScenario, new() {
            var temp = new T();
            if (!scenarios.ContainsKey(temp.Key)) {
                Register(temp);
            }
            return Start(temp.Key);
        }

        public static bool IsActive(string key) {
            return active != null && active.Key == key && DialogueBox.Instance.Active;
        }

        public static void ResetAll() {
            foreach (var sc in scenarios.Values) {
                sc.Reset();
            }
            active = null;
        }
    }
}
