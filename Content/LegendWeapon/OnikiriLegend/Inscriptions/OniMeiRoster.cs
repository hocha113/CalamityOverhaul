namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    //铭文名册占位，纯数据（无效果挂点）；茎铭取髭切一系改名史，Key 沿用保证存档连续

    /// <summary>髭切，斩首连须的旧名</summary>
    internal sealed class MeiHigekiri : OniMeiDefinition
    {
        public override int SortOrder => 10;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;
    }

    /// <summary>鬼切，一条戾桥断鬼腕得名，出厂默认铭</summary>
    internal sealed class MeiOnikiri : OniMeiDefinition
    {
        public override int SortOrder => 20;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;
    }

    /// <summary>狮子之子，夜吼如狮的荣名，金象嵌</summary>
    internal sealed class MeiShishinoko : OniMeiDefinition
    {
        public override int SortOrder => 30;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;
        public override bool IsGoldTier => true;
    }

    /// <summary>友切，误斩友刀的咎名</summary>
    internal sealed class MeiTomokiri : OniMeiDefinition
    {
        public override int SortOrder => 40;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;
    }

    /// <summary>风樋，轻身之槽</summary>
    internal sealed class MeiKazehi : OniMeiDefinition
    {
        public override int SortOrder => 50;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Hi;
    }

    /// <summary>血樋，放血之槽</summary>
    internal sealed class MeiChihi : OniMeiDefinition
    {
        public override int SortOrder => 60;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Hi;
    }

    /// <summary>不动，梵字カーン笔意的护身雕</summary>
    internal sealed class MeiFudo : OniMeiDefinition
    {
        public override int SortOrder => 70;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Horimono;
    }

    /// <summary>倶利伽罗，缠剑龙雕，金象嵌</summary>
    internal sealed class MeiKurikara : OniMeiDefinition
    {
        public override int SortOrder => 80;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Horimono;
        public override bool IsGoldTier => true;
    }
}
