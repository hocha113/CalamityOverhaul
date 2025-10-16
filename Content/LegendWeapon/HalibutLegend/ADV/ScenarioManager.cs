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

            //如果场景有默认样式，使用它
            if (DefaultDialogueStyle != null) {
                targetBox = DefaultDialogueStyle.Invoke();
            }

            //否则使用全局默认
            targetBox ??= DialogueUIRegistry.Current;

            //设置预处理器
            targetBox.PreProcessor = PreProcessSegment;

            //将所有对话添加到对话框
            for (int i = 0; i < lines.Count; i++) {
                var line = lines[i];
                bool isLast = i == lines.Count - 1;

                //构建完成回调
                Action completeCallback = null;
                if (line.OnComplete != null || isLast) {
                    completeCallback = () => {
                        line.OnComplete?.Invoke();
                        if (isLast) {
                            Complete();
                        }
                    };
                }

                //触发开始事件
                line.OnStart?.Invoke();

                //入队对话（注意：styleOverride 暂时不支持运行时切换，因为需要重构对话框系统）
                //如果需要支持，请为每个对话框分别调用 Start
                targetBox.EnqueueDialogue(line.Speaker, line.Content, completeCallback);
            }
        }

        public virtual void SaveData(TagCompound tag) { }

        public virtual void LoadData(TagCompound tag) { }

        public virtual void Update(ADVSave save, HalibutPlayer halibutPlayer) { }

        private void Complete() {
            if (!IsCompleted) {
                IsCompleted = true;
                OnComplete();
                OnScenarioComplete();
            }

            // 清理预处理器引用
            var box = DialogueUIRegistry.Current;
            if (box != null && box.PreProcessor == PreProcessSegment) {
                box.PreProcessor = null;
            }
        }

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
