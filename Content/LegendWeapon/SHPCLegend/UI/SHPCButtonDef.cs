using System;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI
{
    /// <summary>扇形 HUD 按钮配置，字段用委托每帧求值</summary>
    internal class SHPCButtonDef
    {
        /// <summary>短标题</summary>
        public Func<string> Title;
        /// <summary>副标题</summary>
        public Func<string> Subtitle;
        /// <summary>说明文本</summary>
        public Func<string> Description;
        /// <summary>按钮中心符号</summary>
        public string Glyph;
        /// <summary>是否可点击</summary>
        public Func<bool> Enabled;
        /// <summary>状态弧 0~1，负值不绘制</summary>
        public Func<float> StatusValue;
        /// <summary>状态文本</summary>
        public Func<string> StatusText;
        /// <summary>点击回调</summary>
        public Action OnClick;
        /// <summary>是否弹出固定二级面板</summary>
        public bool UsesFixedPanel;
    }
}
