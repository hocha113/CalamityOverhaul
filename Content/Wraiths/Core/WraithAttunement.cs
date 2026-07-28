using Terraria;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>当前手持载体提供给共鸣效果的只读上下文。</summary>
    public readonly struct WraithAttunementContext(
        Player player,
        Item vesselItem,
        WraithProgressStore store,
        WraithProgressRecord record)
    {
        public Player Player { get; } = player;
        public Item VesselItem { get; } = vesselItem;
        public WraithProgressStore Store { get; } = store;
        public WraithProgressRecord Record { get; } = record;
        public float Mastery => Record?.Mastery ?? 0f;
    }

    /// <summary>
    /// 被点鬼簿选中后逐帧运行的无状态共鸣效果；实例状态应归玩家或实体所有。
    /// </summary>
    public abstract class WraithAttunement
    {
        public WraithDefinition Definition { get; internal set; }

        public abstract void Update(in WraithAttunementContext context);
    }
}