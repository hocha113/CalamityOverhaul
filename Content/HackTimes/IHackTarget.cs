using Terraria;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>可被骇入目标抽象，在<see cref="IScannable"/>基础上补齐锁定与骇入行为</summary>
    internal interface IHackTarget : IScannable
    {
        /// <summary>所属注册类型工厂</summary>
        HackTargetType TargetType { get; }

        /// <summary>锁定框半宽半高（屏幕像素，含 padding）</summary>
        Vector2 LockFrameHalfSize { get; }

        /// <summary>锁定框下方目标名</summary>
        string LockFrameTitle { get; }

        /// <summary>锁定框副状态，无则返回 false</summary>
        bool TryGetLockFrameStatus(out string text, out Color color);

        /// <summary>上传完成后将协议作用到目标</summary>
        /// <returns>true 表示效果生效或已注册追踪器</returns>
        bool ApplyHack(QuickHackDef hack, Player caster);

        /// <summary>判断两目标是否同一实体</summary>
        bool TargetEquals(IHackTarget other);
    }
}
