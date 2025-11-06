using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using static InnoVault.VaultUtils;

namespace CalamityOverhaul.Content.ADV
{
    internal abstract class ModifyDisplayText : VaultType<ModifyDisplayText>, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        /// <summary>
        /// 台词覆盖字典，键为台词key，值为新的台词内容
        /// </summary>
        private readonly Dictionary<string, DialogueOverride> dialogueOverrides = new();

        /// <summary>
        /// 动态台词提供器字典，键为台词key，值为提供台词的函数
        /// </summary>
        private readonly Dictionary<string, Func<DialogueOverride>> dynamicDialogueProviders = new();

        /// <summary>
        /// 台词覆盖数据结构
        /// </summary>
        public class DialogueOverride
        {
            public string Text { get; set; }
            public Color? Color { get; set; }
            public LocalizedText LocalizedText { get; set; }

            public DialogueOverride(string text, Color? color = null) {
                Text = text;
                Color = color;
            }

            public DialogueOverride(LocalizedText text, Color? color = null) {
                LocalizedText = text;
                Color = color;
            }

            /// <summary>
            /// 获取最终显示的文本（优先使用本地化文本）
            /// </summary>
            public string GetDisplayText() {
                return LocalizedText?.Value ?? Text;
            }
        }

        /// <summary>
        /// 条件台词选择器，根据条件返回不同的台词
        /// </summary>
        public class ConditionalDialogue
        {
            private readonly List<(Func<bool> condition, DialogueOverride dialogue)> conditions = new();
            private DialogueOverride defaultDialogue;

            /// <summary>
            /// 添加一个条件分支（硬编码文本）
            /// </summary>
            public ConditionalDialogue When(Func<bool> condition, string text, Color? color = null) {
                conditions.Add((condition, new DialogueOverride(text, color)));
                return this;
            }

            /// <summary>
            /// 添加一个条件分支（使用本地化文本）
            /// </summary>
            public ConditionalDialogue When(Func<bool> condition, LocalizedText localizedText, Color? color = null) {
                conditions.Add((condition, new DialogueOverride(string.Empty, color) { LocalizedText = localizedText }));
                return this;
            }

            /// <summary>
            /// 添加一个条件分支（使用DialogueOverride对象）
            /// </summary>
            public ConditionalDialogue When(Func<bool> condition, DialogueOverride dialogue) {
                conditions.Add((condition, dialogue));
                return this;
            }

            /// <summary>
            /// 设置默认台词（硬编码文本），当所有条件都不满足时使用
            /// </summary>
            public ConditionalDialogue Otherwise(string text, Color? color = null) {
                defaultDialogue = new DialogueOverride(text, color);
                return this;
            }

            /// <summary>
            /// 设置默认台词（使用本地化文本）
            /// </summary>
            public ConditionalDialogue Otherwise(LocalizedText localizedText, Color? color = null) {
                defaultDialogue = new DialogueOverride(string.Empty, color) { LocalizedText = localizedText };
                return this;
            }

            /// <summary>
            /// 设置默认台词（使用DialogueOverride对象）
            /// </summary>
            public ConditionalDialogue Otherwise(DialogueOverride dialogue) {
                defaultDialogue = dialogue;
                return this;
            }

            /// <summary>
            /// 获取当前应该使用的台词
            /// </summary>
            public DialogueOverride Get() {
                foreach (var (condition, dialogue) in conditions) {
                    if (condition()) {
                        return dialogue;
                    }
                }
                return defaultDialogue;
            }
        }

        protected sealed override void VaultRegister() {
            Instances.Add(this);
        }

        public sealed override void VaultSetup() {
            SetStaticDefaults();
        }

        #region 硬编码文本方法

        /// <summary>
        /// 设置单条台词覆盖
        /// </summary>
        /// <param name="key">台词的key（不含前缀）</param>
        /// <param name="text">新的台词文本</param>
        /// <param name="color">可选的颜色，如果为null则使用默认颜色</param>
        public void SetDialogue(string key, string text, Color? color = null) {
            dialogueOverrides[key] = new DialogueOverride(text, color);
            //如果已经有动态提供器，移除它
            dynamicDialogueProviders.Remove(key);
        }

        /// <summary>
        /// 批量设置台词覆盖
        /// </summary>
        /// <param name="overrides">台词key和文本的字典</param>
        public void SetDialogues(Dictionary<string, string> overrides) {
            foreach (var kvp in overrides) {
                SetDialogue(kvp.Key, kvp.Value);
            }
        }

        #endregion

        #region 本地化文本方法

        /// <summary>
        /// 设置单条台词覆盖
        /// </summary>
        /// <param name="key">台词的key（不含前缀）</param>
        /// <param name="localizedText">本地化文本对象</param>
        /// <param name="color">可选的颜色，如果为null则使用默认颜色</param>
        public void SetDialogueLocalized(string key, LocalizedText localizedText, Color? color = null) {
            dialogueOverrides[key] = new DialogueOverride(string.Empty, color) { LocalizedText = localizedText };
            //如果已经有动态提供器，移除它
            dynamicDialogueProviders.Remove(key);
        }

        /// <summary>
        /// 设置单条台词覆盖
        /// </summary>
        /// <param name="key">台词的key（不含前缀）</param>
        /// <param name="localizationKey">本地化key（如 "Mods.YourMod.Dialogue.SomeKey"）</param>
        /// <param name="color">可选的颜色，如果为null则使用默认颜色</param>
        public void SetDialogueLocalized(string key, string localizationKey, Color? color = null) {
            var localizedText = Language.GetText(localizationKey);
            SetDialogueLocalized(key, localizedText, color);
        }

        /// <summary>
        /// 批量设置台词覆盖
        /// </summary>
        /// <param name="overrides">台词key和本地化文本的字典</param>
        public void SetDialoguesLocalized(Dictionary<string, LocalizedText> overrides) {
            foreach (var kvp in overrides) {
                SetDialogueLocalized(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// 批量设置台词覆盖
        /// </summary>
        /// <param name="overrides">台词key和本地化key的字典</param>
        public void SetDialoguesLocalizedByKey(Dictionary<string, string> overrides) {
            foreach (var kvp in overrides) {
                SetDialogueLocalized(kvp.Key, kvp.Value);
            }
        }

        #endregion

        #region 动态台词方法

        /// <summary>
        /// 设置动态台词，每次调用Handle时都会重新计算
        /// </summary>
        /// <param name="key">台词的key（不含前缀）</param>
        /// <param name="provider">提供台词的函数</param>
        public void SetDynamicDialogue(string key, Func<DialogueOverride> provider) {
            dynamicDialogueProviders[key] = provider;
            //移除静态覆盖
            dialogueOverrides.Remove(key);
        }

        /// <summary>
        /// 设置条件台词，根据条件返回不同的台词
        /// </summary>
        /// <param name="key">台词的key（不含前缀）</param>
        /// <param name="conditionalDialogue">条件台词构建器</param>
        public void SetConditionalDialogue(string key, ConditionalDialogue conditionalDialogue) {
            SetDynamicDialogue(key, () => conditionalDialogue.Get());
        }

        /// <summary>
        /// 创建一个新的条件台词构建器
        /// </summary>
        public ConditionalDialogue CreateConditional() {
            return new ConditionalDialogue();
        }

        #endregion

        #region 通用方法

        /// <summary>
        /// 批量设置带颜色的台词覆盖
        /// </summary>
        /// <param name="overrides">台词key和DialogueOverride对象的字典</param>
        public void SetDialoguesWithColor(Dictionary<string, DialogueOverride> overrides) {
            foreach (var kvp in overrides) {
                dialogueOverrides[kvp.Key] = kvp.Value;
                dynamicDialogueProviders.Remove(kvp.Key);
            }
        }

        /// <summary>
        /// 移除台词覆盖，恢复原始台词
        /// </summary>
        /// <param name="key">台词的key</param>
        public void RemoveDialogue(string key) {
            dialogueOverrides.Remove(key);
            dynamicDialogueProviders.Remove(key);
        }

        /// <summary>
        /// 清空所有台词覆盖
        /// </summary>
        public void ClearDialogues() {
            dialogueOverrides.Clear();
            dynamicDialogueProviders.Clear();
        }

        /// <summary>
        /// 检查是否有台词覆盖
        /// </summary>
        /// <param name="key">台词的key</param>
        /// <returns>如果有覆盖返回true</returns>
        public bool HasDialogueOverride(string key) {
            return dialogueOverrides.ContainsKey(key) || dynamicDialogueProviders.ContainsKey(key);
        }

        /// <summary>
        /// 获取台词覆盖的数量
        /// </summary>
        public int GetOverrideCount() {
            return dialogueOverrides.Count + dynamicDialogueProviders.Count;
        }

        /// <summary>
        /// 获取所有已覆盖的台词key
        /// </summary>
        public IEnumerable<string> GetOverriddenKeys() {
            return dialogueOverrides.Keys.Concat(dynamicDialogueProviders.Keys).Distinct();
        }

        #endregion

        public virtual bool Alive(Player player) => true;

        public virtual bool PreHandle(ref string key, ref Color color) {
            return true;
        }

        public bool Handle(ref string key, ref Color color) {
            string result = key.Split('.').Last();

            if (!PreHandle(ref key, ref color)) {
                return false;
            }

            //优先检查动态提供器
            if (dynamicDialogueProviders.TryGetValue(result, out var provider)) {
                var dialogue = provider();
                if (dialogue != null) {
                    Text(dialogue.GetDisplayText(), dialogue.Color ?? color);
                    return false;
                }
            }

            //然后检查静态覆盖
            if (dialogueOverrides.TryGetValue(result, out var over)) {
                Text(over.GetDisplayText(), over.Color ?? color);
                return false;
            }

            //如果没有覆盖，返回true使用原始台词
            return true;
        }
    }
}
