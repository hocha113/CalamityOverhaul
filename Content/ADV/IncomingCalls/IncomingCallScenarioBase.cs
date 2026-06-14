using CalamityOverhaul.Content.ADV.DialogueBoxs;
using System;
using System.Collections.Generic;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.IncomingCalls
{
    /// <summary>来电场景基类——立绘注册、台词构建与呼出<br/>用法同 <see cref="Scenarios.ADVScenarioBase"/>：重写 <see cref="Build"/>，调用 <see cref="Start"/></summary>
    internal abstract class IncomingCallScenarioBase : VaultType<IncomingCallScenarioBase>, ILocalizedModType
    {
        /// <summary>来电者显示名称</summary>
        protected abstract string CallerName { get; }

        /// <summary>来电者立绘键（为 null 时使用 <see cref="CallerName"/>）</summary>
        protected virtual string CallerPortraitKey => CallerName;

        /// <summary>是否自动接听（跳过振铃阶段直接进入通话）</summary>
        protected virtual bool AutoAnswer => false;

        public string LocalizationCategory => "ADV";

        /// <summary>获取来电UI实例，默认使用 <see cref="IncomingCallRegistry.Current"/></summary>
        protected virtual IncomingCallBase GetCallUI() => IncomingCallRegistry.Current;

        /// <summary>台词缓存列表</summary>
        private readonly List<CallLine> lines = [];

        protected override void VaultRegister() {
            Instances.Add(this);
            TypeToInstance[GetType()] = this;
        }

        public override void VaultSetup() {
            SetStaticDefaults();
        }

        public static T GetScenario<T>() where T : IncomingCallScenarioBase => (T)TypeToInstance[typeof(T)];

        /// <summary>子类重写：注册立绘并添加台词</summary>
        protected abstract void Build();

        /// <summary>场景开始前触发（Build之前）</summary>
        protected virtual void OnScenarioStart() { }

        /// <summary>来电结束后触发</summary>
        protected virtual void OnScenarioComplete() { }

        #region 构建辅助方法

        /// <summary>注册头像立绘（复用 DialogueBoxBase 的立绘注册表）</summary>
        protected static void RegisterPortrait(string key, string texturePath, bool silhouette = false) {
            DialogueBoxBase.RegisterPortrait(key, texturePath, silhouette: silhouette);
        }

        /// <summary>注册头像立绘</summary>
        protected static void RegisterPortrait(string key, Microsoft.Xna.Framework.Graphics.Texture2D texture, bool silhouette = false) {
            DialogueBoxBase.RegisterPortrait(key, texture, silhouette: silhouette);
        }

        /// <summary>添加一条台词</summary>
        /// <param name="speaker">说话者名称</param>
        /// <param name="content">台词内容</param>
        /// <param name="autoAdvanceDelay">自动推进延迟帧数（默认120帧≈2秒）</param>
        /// <param name="onStart">本条台词开始时回调</param>
        /// <param name="onFinish">本条台词结束时回调</param>
        protected void Add(string speaker, string content, int autoAdvanceDelay = 120,
            Action onStart = null, Action onFinish = null) {
            lines.Add(new CallLine {
                Speaker = speaker,
                PortraitKey = null,
                Content = content,
                AutoAdvanceDelay = autoAdvanceDelay,
                OnStart = onStart,
                OnFinish = onFinish
            });
        }

        /// <summary>添加一条台词（角色名和立绘键分离）</summary>
        protected void Add(string speaker, string portraitKey, string content, int autoAdvanceDelay = 120,
            Action onStart = null, Action onFinish = null) {
            lines.Add(new CallLine {
                Speaker = speaker,
                PortraitKey = portraitKey,
                Content = content,
                AutoAdvanceDelay = autoAdvanceDelay,
                OnStart = onStart,
                OnFinish = onFinish
            });
        }

        #endregion

        /// <summary>发起来电，构建台词并呼出</summary>
        public void Start() {
            lines.Clear();
            OnScenarioStart();
            Build();

            if (lines.Count == 0) {
                OnScenarioComplete();
                return;
            }

            var call = GetCallUI();
            call.StartCall(CallerName, CallerPortraitKey);

            for (int i = 0; i < lines.Count; i++) {
                var line = lines[i];
                bool isLast = i == lines.Count - 1;

                Action finishCallback = isLast
                    ? () => { line.OnFinish?.Invoke(); OnScenarioComplete(); }
                : line.OnFinish;

                if (line.PortraitKey != null) {
                    call.EnqueueLine(line.Speaker, line.PortraitKey, line.Content,
                        onFinish: finishCallback, onStart: line.OnStart,
                        autoAdvanceDelay: line.AutoAdvanceDelay);
                }
                else {
                    call.EnqueueLine(line.Speaker, line.Content,
                        onFinish: finishCallback, onStart: line.OnStart,
                        autoAdvanceDelay: line.AutoAdvanceDelay);
                }
            }

            if (AutoAnswer) {
                call.AutoAnswer();
            }
        }

        private class CallLine
        {
            public string Speaker;
            public string PortraitKey;
            public string Content;
            public int AutoAdvanceDelay;
            public Action OnStart;
            public Action OnFinish;
        }
    }
}
