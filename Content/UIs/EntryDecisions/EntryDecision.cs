using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.UIs.EntryDecisions
{
    /// <summary>
    /// 入世决策条目，进入世界后需要玩家确认的低紧迫度事项(任务检测、传奇武器升级等)
    /// <br/>注册进 <see cref="EntryDecisionManager"/> 后由 <see cref="EntryDecisionUI"/> 以
    /// "右侧通知条 → 点击展开操作卡"的两段式形态呈现，不再使用全屏弹窗
    /// <br/>忽略通知即等价于"跳过"，实现方保证不回答时零数据写入
    /// </summary>
    internal abstract class EntryDecision
    {
        /// <summary>域强调色，通知条与操作卡的发光/线条用色</summary>
        public abstract Color Accent { get; }

        /// <summary>通知条一行文本</summary>
        public abstract string PillText { get; }

        /// <summary>待处理数量，&gt;1 时通知条显示 ×N 徽章</summary>
        public virtual int PendingCount => 1;

        /// <summary>操作卡标题</summary>
        public abstract string CardTitle { get; }

        /// <summary>操作卡描述，可含换行</summary>
        public abstract string CardDesc { get; }

        /// <summary>操作卡按钮行上方的小字补充(如队列提示)，null 不绘制</summary>
        public virtual string CardFooter => null;

        public abstract string ConfirmLabel { get; }
        public abstract string SkipLabel { get; }
        public abstract string TrustLabel { get; }

        /// <summary>决策仍待处理；false 时由管理器移除并触发 <see cref="Cancelled"/></summary>
        public abstract bool StillValid { get; }

        /// <summary>确认动作；执行后若 <see cref="StillValid"/> 仍为 true(如队列推进)卡片保持展开</summary>
        public abstract void Confirm();

        /// <summary>跳过动作，语义与"忽略通知"一致：本次会话不再询问，下次进世界重新提醒</summary>
        public abstract void Skip();

        /// <summary>信任动作，永久记住当前世界并执行确认</summary>
        public abstract void Trust();

        /// <summary>被管理器移除时回调(世界切换 CancelAll 或失效剔除)，清理自身静态引用</summary>
        public virtual void Cancelled() { }

        /// <summary>在通知条/操作卡的图标区绘制图标，size 为期望的最大边长</summary>
        public virtual void DrawIcon(SpriteBatch sb, Vector2 center, float size, float alpha) { }
    }
}
