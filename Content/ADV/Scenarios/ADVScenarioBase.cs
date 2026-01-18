using CalamityOverhaul.Content.ADV.ADVChoices;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using System;
using System.Collections.Generic;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.ADV.Scenarios
{
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

        public virtual void PreProcessSegment(DialoguePreProcessArgs args) { }

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
        /// 添加对话(角色名和立绘分离)
        /// </summary>
        /// <param name="speaker">显示的说话者名称</param>
        /// <param name="portraitKey">用于查找立绘的键</param>
        /// <param name="content">对话内容</param>
        public void Add(string speaker, string portraitKey, string content) {
            lines.Add(new DialogueLine(speaker, portraitKey, content));
        }

        /// <summary>
        /// 添加完整配置的对话(角色名和立绘分离)
        /// </summary>
        /// <param name="speaker">显示的说话者名称</param>
        /// <param name="portraitKey">用于查找立绘的键</param>
        /// <param name="content">对话内容</param>
        /// <param name="onStart">开始回调</param>
        /// <param name="onComplete">完成回调</param>
        /// <param name="styleOverride">样式重写</param>
        public void Add(string speaker, string portraitKey, string content, Action onStart = null, Action onComplete = null, Func<DialogueBoxBase> styleOverride = null) {
            var line = new DialogueLine(speaker, portraitKey, content) {
                OnStart = onStart,
                OnComplete = onComplete,
                StyleOverride = styleOverride
            };
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

        #region 定时对话方法

        /// <summary>
        /// 添加定时对话（指定秒数后自动推进）
        /// </summary>
        /// <param name="speaker">说话者名称</param>
        /// <param name="content">对话内容</param>
        /// <param name="durationSeconds">持续秒数</param>
        /// <param name="onComplete">完成回调</param>
        /// <param name="onStart">开始回调</param>
        public void AddTimed(string speaker, string content, float durationSeconds, Action onComplete = null, Action onStart = null) {
            var line = new DialogueLine(speaker, content) {
                OnStart = onStart,
                OnComplete = onComplete,
                TimedConfig = TimedDialogueConfig.WithDuration(durationSeconds)
            };
            lines.Add(line);
        }

        /// <summary>
        /// 添加定时对话（使用完整配置）
        /// </summary>
        /// <param name="speaker">说话者名称</param>
        /// <param name="content">对话内容</param>
        /// <param name="timedConfig">定时配置</param>
        /// <param name="onComplete">完成回调</param>
        /// <param name="onStart">开始回调</param>
        public void AddTimed(string speaker, string content, TimedDialogueConfig timedConfig, Action onComplete = null, Action onStart = null) {
            var line = new DialogueLine(speaker, content) {
                OnStart = onStart,
                OnComplete = onComplete,
                TimedConfig = timedConfig
            };
            lines.Add(line);
        }

        /// <summary>
        /// 添加定时对话（角色名和立绘分离）
        /// </summary>
        /// <param name="speaker">显示的说话者名称</param>
        /// <param name="portraitKey">立绘键</param>
        /// <param name="content">对话内容</param>
        /// <param name="durationSeconds">持续秒数</param>
        /// <param name="onComplete">完成回调</param>
        /// <param name="onStart">开始回调</param>
        public void AddTimed(string speaker, string portraitKey, string content, float durationSeconds, Action onComplete = null, Action onStart = null) {
            var line = new DialogueLine(speaker, portraitKey, content) {
                OnStart = onStart,
                OnComplete = onComplete,
                TimedConfig = TimedDialogueConfig.WithDuration(durationSeconds)
            };
            lines.Add(line);
        }

        /// <summary>
        /// 添加定时对话（完整配置，角色名和立绘分离）
        /// </summary>
        /// <param name="speaker">显示的说话者名称</param>
        /// <param name="portraitKey">立绘键</param>
        /// <param name="content">对话内容</param>
        /// <param name="timedConfig">定时配置</param>
        /// <param name="onComplete">完成回调</param>
        /// <param name="onStart">开始回调</param>
        /// <param name="styleOverride">样式重写</param>
        public void AddTimed(string speaker, string portraitKey, string content, TimedDialogueConfig timedConfig, Action onComplete = null, Action onStart = null, Func<DialogueBoxBase> styleOverride = null) {
            var line = new DialogueLine(speaker, portraitKey, content) {
                OnStart = onStart,
                OnComplete = onComplete,
                TimedConfig = timedConfig,
                StyleOverride = styleOverride
            };
            lines.Add(line);
        }

        /// <summary>
        /// 添加带选项的定时对话（时间耗尽后触发默认选项或回调）
        /// </summary>
        /// <param name="speaker">说话者名称</param>
        /// <param name="content">对话内容</param>
        /// <param name="choices">选项列表</param>
        /// <param name="durationSeconds">持续秒数</param>
        /// <param name="onTimeExpired">时间耗尽时的回调（如果为null且有选项，则随机选择一个）</param>
        /// <param name="onStart">对话开始时的回调</param>
        /// <param name="styleOverride">对话框样式重写</param>
        /// <param name="choiceBoxStyle">选项框样式</param>
        public void AddTimedWithChoices(string speaker, string content, List<Choice> choices, float durationSeconds, Action onTimeExpired = null, Action onStart = null, Func<DialogueBoxBase> styleOverride = null, ADVChoiceBox.ChoiceBoxStyle choiceBoxStyle = ADVChoiceBox.ChoiceBoxStyle.Default) {
            var timedConfig = new TimedDialogueConfig {
                Duration = durationSeconds,
                ShowProgressIndicator = true,
                AllowManualAdvance = true, //允许玩家主动点击选项来推进
                SkipOnFinishWhenExpired = true, //时间耗尽后跳过 OnFinish（避免重复弹出选项框）
                OnTimeExpired = onTimeExpired ?? (() => {
                    //默认行为：随机选择一个选项
                    if (choices != null && choices.Count > 0) {
                        var randomChoice = choices[Terraria.Main.rand.Next(choices.Count)];
                        ADVChoiceBox.Hide();
                        randomChoice.OnSelect?.Invoke();
                    }
                })
            };

            var line = new DialogueLine(speaker, content) {
                OnStart = onStart,
                StyleOverride = styleOverride,
                Choices = choices,
                ChoiceBoxStyle = choiceBoxStyle,
                TimedConfig = timedConfig,
                OnComplete = null //选项对话的完成由选项选择触发
            };
            lines.Add(line);
        }

        /// <summary>
        /// 添加带选项的定时对话（完整配置）
        /// </summary>
        /// <param name="speaker">说话者名称</param>
        /// <param name="content">对话内容</param>
        /// <param name="choices">选项列表</param>
        /// <param name="timedConfig">定时配置</param>
        /// <param name="onStart">对话开始时的回调</param>
        /// <param name="styleOverride">对话框样式重写</param>
        /// <param name="choiceBoxStyle">选项框样式</param>
        public void AddTimedWithChoices(string speaker, string content, List<Choice> choices, TimedDialogueConfig timedConfig, Action onStart = null, Func<DialogueBoxBase> styleOverride = null, ADVChoiceBox.ChoiceBoxStyle choiceBoxStyle = ADVChoiceBox.ChoiceBoxStyle.Default) {
            var line = new DialogueLine(speaker, content) {
                OnStart = onStart,
                StyleOverride = styleOverride,
                Choices = choices,
                ChoiceBoxStyle = choiceBoxStyle,
                TimedConfig = timedConfig,
                OnComplete = null //选项对话的完成由选项选择触发
            };
            lines.Add(line);
        }

        #endregion

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
                    //捕获当前行的选项框样式和定时配置
                    ADVChoiceBox.ChoiceBoxStyle capturedStyle = line.ChoiceBoxStyle;
                    var capturedTimedConfig = line.TimedConfig;
                    var capturedChoices = line.Choices;

                    completeCallback = () => {
                        //检查是否需要继承定时配置到选项框
                        if (capturedTimedConfig != null) {
                            //从对话框获取剩余时间
                            var dialogueBox = DialogueUIRegistry.Current;
                            int remainingFrames = dialogueBox?.TimedRemainingFrames ?? 0;

                            if (remainingFrames > 0) {
                                //创建选项框的定时配置，继承剩余时间
                                var choiceTimedConfig = ChoiceBoxTimedConfig.FromRemainingFrames(
                                    remainingFrames,
                                    capturedTimedConfig.OnTimeExpired
                                );

                                //显示定时选项框
                                ADVChoiceBox.ShowTimed(capturedChoices, choiceTimedConfig, null, capturedStyle);
                            }
                            else {
                                //没有剩余时间，直接显示普通选项框
                                ADVChoiceBox.Show(capturedChoices, null, capturedStyle);
                            }
                        }
                        else {
                            //非定时对话，显示普通选项框
                            ADVChoiceBox.Show(capturedChoices, null, capturedStyle);
                        }
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

                //获取当前实际使用的对话框来入队(传递独立的立绘键)
                //根据是否为定时对话选择不同的入队方法
                if (line.IsTimed) {
                    initialBox.EnqueueTimedDialogue(line.Speaker, line.PortraitKey, line.Content, line.TimedConfig, completeCallback, startCallback);
                }
                else {
                    initialBox.EnqueueDialogue(line.Speaker, line.PortraitKey, line.Content, completeCallback, startCallback);
                }
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
}
