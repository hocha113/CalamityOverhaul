using CalamityOverhaul.Content.ADV.ADVChoices;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using System;
using System.Collections.Generic;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.ADV
{
    /// <summary>
    /// 对话场景接口
    /// </summary>
    public interface IADVScenario
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
    public class DialogueLine
    {
        public string Speaker { get; set; }
        public string Content { get; set; }
        public Action OnStart { get; set; }
        public Action OnComplete { get; set; }
        public Func<DialogueBoxBase> StyleOverride { get; set; }
        public List<Choice> Choices { get; set; }//选项列表
        public ADVChoiceBox.ChoiceBoxStyle ChoiceBoxStyle { get; set; } = ADVChoiceBox.ChoiceBoxStyle.Default;//选项框样式

        public DialogueLine(string speaker, string content) {
            Speaker = speaker;
            Content = content;
        }
    }

    public abstract class ADVScenarioBase : VaultType<ADVScenarioBase>, IADVScenario, ILocalizedModType
    {
        public virtual string LocalizationCategory => "ADV";
        /// <summary>
        /// 场景唯一标识符
        /// </summary>
        public virtual string Key => Name;
        /// <summary>
        /// 场景是否可以重复触发
        /// </summary>
        public virtual bool CanRepeat => false;
        /// <summary>
        /// 场景是否已完成
        /// </summary>
        public bool IsCompleted { get; private set; }
        /// <summary>
        /// 对话行列表
        /// </summary>
        private readonly List<DialogueLine> lines = new();
        /// <summary>
        /// 本场景使用的本地化文本字典
        /// </summary>
        protected Dictionary<string, LocalizedText> LocalizedTextDic { get; private set; } = [];
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

        protected LocalizedText Localized(string key, string text) {
            if (LocalizedTextDic.TryGetValue(key, out var localizedText)) {
                return localizedText;
            }
            localizedText = this.GetLocalization(key, () => text);
            LocalizedTextDic[key] = localizedText;
            return localizedText;
        }

        protected abstract void Build();

        protected override void VaultRegister() {
            Instances.Add(this);
            TypeToInstance[GetType()] = this;
            ScenarioManager.Register(this);
        }

        public override void VaultSetup() {
            SetStaticDefaults();
        }

        public override void Unload() { }

        public virtual void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) { }

        /// <summary>
        /// 启动场景
        /// </summary>
        public bool StartScenario() {
            ScenarioManager.Reset(Key);
            return ScenarioManager.Start(Key);
        }

        /// <summary>
        /// 添加一条简单对话
        /// </summary>
        public void AddLineFromKey(string speakerKey, string key) {
            lines.Add(new DialogueLine(LocalizedTextDic[speakerKey].Value, LocalizedTextDic[key].Value));
        }

        /// <summary>
        /// 添加一条简单对话
        /// </summary>
        public void Add(string speaker, string content) {
            lines.Add(new DialogueLine(speaker, content));
        }

        /// <summary>
        /// 添加带完成回调的对话
        /// </summary>
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
        /// 添加带选项的对话
        /// </summary>
        /// <param name="speaker">说话者名称</param>
        /// <param name="content">对话内容</param>
        /// <param name="choices">选项列表</param>
        /// <param name="onStart">对话开始时的回调</param>
        /// <param name="styleOverride">对话框样式重写</param>
        /// <param name="choiceBoxStyle">选项框样式</param>
        public void AddWithChoices(string speaker, string content, List<Choice> choices, Action onStart = null, Func<DialogueBoxBase> styleOverride = null, ADVChoiceBox.ChoiceBoxStyle choiceBoxStyle = ADVChoiceBox.ChoiceBoxStyle.Default) {
            var line = new DialogueLine(speaker, content) {
                OnStart = onStart,
                StyleOverride = styleOverride,
                Choices = choices,
                ChoiceBoxStyle = choiceBoxStyle,
                OnComplete = null//选项对话的完成由选项选择触发
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
            lines.Clear();//清空旧对话
            Build();//每次开始都重新构建对话内容，方便自定义内容
            if (lines.Count == 0) { Complete(); return; }

            OnScenarioStart();

            //确定初始对话框
            DialogueBoxBase initialBox = null;

            if (DefaultDialogueStyle != null) {
                initialBox = DefaultDialogueStyle.Invoke();
                if (initialBox != null) {
                    DialogueUIRegistry.SwitchDialogueBox(initialBox, transferQueue: false);
                }
                //设置解析器为默认样式
                DialogueUIRegistry.SetResolver(DefaultDialogueStyle);
            }

            initialBox ??= DialogueUIRegistry.Current;
            initialBox.PreProcessor = PreProcessSegment;

            //逐条处理对话，支持中途切换样式
            for (int i = 0; i < lines.Count; i++) {
                var line = lines[i];
                bool isLast = i == lines.Count - 1;

                Action completeCallback = null;

                //如果有选项，设置显示选项框的回调
                if (line.Choices != null && line.Choices.Count > 0) {
                    //捕获当前行的选项框样式
                    ADVChoiceBox.ChoiceBoxStyle capturedStyle = line.ChoiceBoxStyle;

                    completeCallback = () => {
                        //显示选项框，传递样式参数
                        ADVChoiceBox.Show(line.Choices, null, capturedStyle);//使用捕获的样式
                        //暂停对话推进，等待选择
                    };
                }
                else if (line.OnComplete != null || isLast) {
                    completeCallback = () => {
                        line.OnComplete?.Invoke();
                        if (isLast) {
                            Complete();
                        }
                    };
                }

                //构建对话开始回调
                Action startCallback = null;

                //如果这条对话有自定义样式，在播放前切换样式
                if (line.StyleOverride != null) {
                    var styleBox = line.StyleOverride.Invoke();
                    if (styleBox != null) {
                        startCallback = () => {
                            //切换对话框样式并迁移状态
                            var oldBox = DialogueUIRegistry.Current;
                            if (oldBox != styleBox) {
                                DialogueUIRegistry.SwitchDialogueBox(styleBox, transferQueue: true);
                                //确保新对话框也设置预处理器
                                styleBox.PreProcessor = PreProcessSegment;
                            }
                            //触发用户定义的 OnStart
                            line.OnStart?.Invoke();
                        };
                    }
                }
                else if (line.OnStart != null) {
                    startCallback = line.OnStart;
                }

                //获取当前实际使用的对话框来入队
                initialBox.EnqueueDialogue(line.Speaker, line.Content, completeCallback, startCallback);
            }
        }

        internal void Complete() {
            if (!IsCompleted) {
                IsCompleted = true;
                OnComplete();
                OnScenarioComplete();
            }

            var box = DialogueUIRegistry.Current;
            if (box != null && box.PreProcessor == PreProcessSegment) {
                box.PreProcessor = null;
            }
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
    public class DialogueLineBuilder
    {
        private readonly ADVScenarioBase scenario;
        private readonly DialogueLine line;

        public DialogueLineBuilder(ADVScenarioBase scenario, string speaker, string content) {
            this.scenario = scenario;
            line = new DialogueLine(speaker, content);
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
        /// 添加选项
        /// </summary>
        public DialogueLineBuilder WithChoices(params Choice[] choices) {
            line.Choices = [.. choices];
            return this;
        }

        /// <summary>
        /// 添加选项（使用列表）
        /// </summary>
        public DialogueLineBuilder WithChoices(List<Choice> choices) {
            line.Choices = choices;
            return this;
        }

        /// <summary>
        /// 设置选项框样式
        /// </summary>
        public DialogueLineBuilder WithChoiceBoxStyle(ADVChoiceBox.ChoiceBoxStyle style) {
            line.ChoiceBoxStyle = style;
            return this;
        }

        /// <summary>
        /// 完成构建并添加到场景
        /// </summary>
        public void Build() {
            scenario.Add(line);
        }
    }

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
