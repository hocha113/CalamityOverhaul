using System;
using System.Collections.Generic;
using Terraria.ModLoader.IO;

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

    /// <summary>
    /// 单条对话数据，支持绑定事件和自定义样式
    /// </summary>
    internal class DialogueLine
    {
        public string Speaker { get; set; }
        public string Content { get; set; }
        public Action OnStart { get; set; }
        public Action OnComplete { get; set; }
        public Func<DialogueBoxBase> StyleOverride { get; set; }

        public DialogueLine(string speaker, string content) {
            Speaker = speaker;
            Content = content;
        }
    }

    internal abstract class ADVScenarioBase : VaultType<ADVScenarioBase>, IADVScenario
    {
        public abstract string Key { get; }
        public virtual bool CanRepeat => false;
        public bool IsCompleted { get; private set; }

        private bool built = false;
        private readonly List<DialogueLine> lines = new();

        /// <summary>
        /// 场景级别的默认对话框样式，如果不设置则使用全局默认
        /// </summary>
        protected virtual Func<DialogueBoxBase> DefaultDialogueStyle => null;

        /// <summary>
        /// 场景开始时触发
        /// </summary>
        protected virtual void OnScenarioStart() { }

        /// <summary>
        /// 场景完成时触发
        /// </summary>
        protected virtual void OnScenarioComplete() { }

        protected abstract void Build();

        protected override void VaultRegister() {
            Instances.Add(this);
            ScenarioManager.Register(this);
        }

        public override void VaultSetup() {
            SetStaticDefaults();
        }

        public override void Unload() { }

        public virtual void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) { }

        /// <summary>
        /// 添加一条简单对话
        /// </summary>
        public void Add(string speaker, string content) {
            lines.Add(new DialogueLine(speaker, content));
        }

        /// <summary>
        /// 添加带完成回调的对话
        /// /// </summary>
        public void Add(string speaker, string content, Action onComplete) {
            var line = new DialogueLine(speaker, content) { OnComplete = onComplete };
            lines.Add(line);
        }

        /// <summary>
        /// 添加完整配置的对话
        /// </summary>
        public void Add(string speaker, string content, Action onStart = null, Action onComplete = null, Func<DialogueBoxBase> styleOverride = null) {
            var line = new DialogueLine(speaker, content) {
                OnStart = onStart,
                OnComplete = onComplete,
                StyleOverride = styleOverride
            };
            lines.Add(line);
        }

        /// <summary>
        /// 使用 DialogueLine 对象添加对话
        /// </summary>
        public void Add(DialogueLine line) {
            if (line != null) {
                lines.Add(line);
            }
        }

        /// <summary>
        /// 链式构建器：创建一条对话
        /// </summary>
        public DialogueLineBuilder Line(string speaker, string content) => new DialogueLineBuilder(this, speaker, content);

        public void Start() {
            if (IsCompleted && !CanRepeat) return;
            if (!built) { Build(); built = true; }
            if (lines.Count == 0) { Complete(); return; }

            OnScenarioStart();

            //确定使用的对话框
            DialogueBoxBase targetBox = null;

            if (DefaultDialogueStyle != null) {
                targetBox = DefaultDialogueStyle.Invoke();
                if (targetBox != null) {
                    DialogueUIRegistry.SwitchDialogueBox(targetBox, transferQueue: false);
                }
            }

            targetBox ??= DialogueUIRegistry.Current;

            targetBox.PreProcessor = PreProcessSegment;

            for (int i = 0; i < lines.Count; i++) {
                var line = lines[i];
                bool isLast = i == lines.Count - 1;

                Action completeCallback = null;
                if (line.OnComplete != null || isLast) {
                    completeCallback = () => {
                        line.OnComplete?.Invoke();
                        if (isLast) {
                            Complete();
                        }
                    };
                }

                // 构建对话开始回调
                Action startCallback = null;
                
                // 如果这条对话有自定义样式，在播放前切换样式
                if (line.StyleOverride != null) {
                    var styleBox = line.StyleOverride.Invoke();
                    if (styleBox != null) {
                        startCallback = () => {
                            // 切换对话框样式并迁移状态
                            DialogueUIRegistry.SwitchDialogueBox(styleBox, transferQueue: true);
                            styleBox.PreProcessor = PreProcessSegment;
                            // 触发用户定义的 OnStart
                            line.OnStart?.Invoke();
                        };
                    }
                }
                else if (line.OnStart != null) {
                    startCallback = line.OnStart;
                }

                targetBox.EnqueueDialogue(line.Speaker, line.Content, completeCallback, startCallback);
            }
        }

        private void Complete() {
            if (!IsCompleted) {
                IsCompleted = true;
                OnComplete();
                OnScenarioComplete();
            }

            var box = DialogueUIRegistry.Current;
            if (box != null && box.PreProcessor == PreProcessSegment) {
                box.PreProcessor = null;
            }

            //他妈的别在这里调用解析器的恢复，会让对话框结束时卡住不动
            //DialogueUIRegistry.SetResolver(null);
        }

        public virtual void SaveData(TagCompound tag) { }

        public virtual void LoadData(TagCompound tag) { }

        public virtual void Update(ADVSave save, HalibutPlayer halibutPlayer) { }

        protected virtual void OnComplete() { }
        public void Reset() => IsCompleted = false;
    }

    /// <summary>
    /// 对话行链式构建器
    /// </summary>
    internal class DialogueLineBuilder
    {
        private readonly ADVScenarioBase scenario;
        private readonly DialogueLine line;

        internal DialogueLineBuilder(ADVScenarioBase scenario, string speaker, string content) {
            this.scenario = scenario;
            this.line = new DialogueLine(speaker, content);
        }

        /// <summary>
        /// 设置开始事件
        /// </summary>
        public DialogueLineBuilder OnStart(Action action) {
            line.OnStart = action;
            return this;
        }

        /// <summary>
        /// 设置完成事件
        /// </summary>
        public DialogueLineBuilder OnComplete(Action action) {
            line.OnComplete = action;
            return this;
        }

        /// <summary>
        /// 设置对话框样式
        /// </summary>
        public DialogueLineBuilder WithStyle(Func<DialogueBoxBase> styleProvider) {
            line.StyleOverride = styleProvider;
            return this;
        }

        /// <summary>
        /// 使用深海风格
        /// </summary>
        public DialogueLineBuilder WithSeaStyle() {
            line.StyleOverride = () => SeaDialogueBox.Instance;
            return this;
        }

        /// <summary>
        /// 使用硫磺火风格
        /// </summary>
        public DialogueLineBuilder WithBrimstoneStyle() {
            line.StyleOverride = () => BrimstoneDialogueBox.Instance;
            return this;
        }

        /// <summary>
        /// 完成构建并添加到场景
        /// </summary>
        public void Build() {
            scenario.Add(line);
        }
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
