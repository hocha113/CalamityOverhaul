using Terraria;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>可骇入目标，在 <see cref="IScannable"/> 上补锁定与施加</summary>
    internal interface IHackTarget : IScannable
    {
        /// <summary>所属目标工厂</summary>
        HackTargetType TargetType { get; }

        /// <summary>锁定框半宽半高（屏幕 px，含 padding）</summary>
        Vector2 LockFrameHalfSize { get; }

        /// <summary>锁定框下方目标名</summary>
        string LockFrameTitle { get; }

        /// <summary>锁定框副状态，无则 false</summary>
        bool TryGetLockFrameStatus(out string text, out Color color);

        /// <summary>上传完成后施加协议</summary>
        /// <returns>true 表示已生效或已入追踪器</returns>
        bool ApplyHack(QuickHackDef hack, Player caster);

        /// <summary>是否同一实体</summary>
        bool TargetEquals(IHackTarget other);
    }
}
