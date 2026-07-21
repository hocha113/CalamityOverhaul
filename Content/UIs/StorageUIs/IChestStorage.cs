using Terraria;

namespace CalamityOverhaul.Content.UIs.StorageUIs
{
    /// <summary>箱子存取接口，统一本地副本/直接引用</summary>
    internal interface IChestStorage
    {
        int SlotsPerRow { get; }
        int SlotRows { get; }
        int TotalSlots => SlotsPerRow * SlotRows;
        int UsedSlotCount { get; }
        Item GetItem(int slot);
        void SetItem(int slot, Item item);
    }
}
