using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.ADV.ADVChoiceBoxs.Styles
{
    /// <summary>
    /// 选项框样式工厂，用于创建和管理样式实例
    /// </summary>
    internal static class ChoiceBoxStyleUtils
    {
        private static readonly Dictionary<ADVChoiceBox.ChoiceBoxStyle, Func<IChoiceBoxStyle>> styleCreators = new()
        {
            { ADVChoiceBox.ChoiceBoxStyle.Default, () => new DefaultChoiceBoxStyle() },
            { ADVChoiceBox.ChoiceBoxStyle.Brimstone, () => new BrimstoneChoiceBoxStyle() },
            { ADVChoiceBox.ChoiceBoxStyle.Draedon, () => new DraedonChoiceBoxStyle() }
        };

        /// <summary>
        /// 创建样式实例
        /// </summary>
        public static IChoiceBoxStyle CreateStyle(ADVChoiceBox.ChoiceBoxStyle styleType) {
            if (styleCreators.TryGetValue(styleType, out var creator)) {
                return creator();
            }
            
            return new DefaultChoiceBoxStyle(); //默认样式
        }

        /// <summary>
        /// 注册自定义样式
        /// </summary>
        public static void RegisterStyle(ADVChoiceBox.ChoiceBoxStyle styleType, Func<IChoiceBoxStyle> creator) {
            styleCreators[styleType] = creator;
        }

        /// <summary>
        /// 检查样式是否已注册
        /// </summary>
        public static bool IsStyleRegistered(ADVChoiceBox.ChoiceBoxStyle styleType) {
            return styleCreators.ContainsKey(styleType);
        }
    }
}
