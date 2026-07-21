using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.UIs.EntryDecisions
{
    /// <summary>
    /// 入世决策条目；忽略≈跳过，不回答零写入
    /// <br/>经 <see cref="EntryDecisionManager"/> → <see cref="EntryDecisionUI"/> 右缘 pill→卡
    /// </summary>
    internal abstract class EntryDecision
    {
        /// <summary>域强调色</summary>
        public abstract Color Accent { get; }

        /// <summary>通知条一行</summary>
        public abstract string PillText { get; }

        /// <summary>待处理数，&gt;1 显示 ×N</summary>
        public virtual int PendingCount => 1;

        /// <summary>操作卡标题</summary>
        public abstract string CardTitle { get; }

        /// <summary>操作卡描述，可换行</summary>
        public abstract string CardDesc { get; }

        /// <summary>按钮行上小字，null 不画</summary>
        public virtual string CardFooter => null;

        public abstract string ConfirmLabel { get; }
        public abstract string SkipLabel { get; }
        public abstract string TrustLabel { get; }

        /// <summary>仍待处理；false 时管理器移除并 <see cref="Cancelled"/></summary>
        public abstract bool StillValid { get; }

        /// <summary>确认；若仍 Valid(队列推进)卡保持展开</summary>
        public abstract void Confirm();

        /// <summary>跳过，本会话不再问</summary>
        public abstract void Skip();

        /// <summary>信任，永久记住本世界并确认</summary>
        public abstract void Trust();

        /// <summary>被移除时回调，清自身静态引用</summary>
        public virtual void Cancelled() { }

        /// <summary>图标区绘制，size=最大边长</summary>
        public virtual void DrawIcon(SpriteBatch sb, Vector2 center, float size, float alpha) { }
    }
}
