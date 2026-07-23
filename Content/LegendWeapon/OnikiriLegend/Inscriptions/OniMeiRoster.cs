namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    //铭文名册：每铭=一个可感知赋效+一项明确负担，数值全走 OniMeiCombatProfile 叠算；
    //茎铭取髭切一系改名史，Key 沿用保证存档连续

    /// <summary>髭切，斩首连须的旧名：残血终结增强，面板伤害 -10%</summary>
    internal sealed class MeiHigekiri : OniMeiDefinition
    {
        public override int SortOrder => 10;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.DamageMul *= 0.90f;
            profile.ExecuteLowLifeBonus = true;
        }
    }

    /// <summary>鬼切，一条戾桥断鬼腕得名，出厂默认铭。原铭=严格基准，无赋效无代价，不覆写任何字段</summary>
    internal sealed class MeiOnikiri : OniMeiDefinition
    {
        public override int SortOrder => 20;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;
    }

    /// <summary>狮子之子，夜吼如狮的荣名，金象嵌：完整五连第五拍合颚刃波，连段间隔 +10%</summary>
    internal sealed class MeiShishinoko : OniMeiDefinition
    {
        public override int SortOrder => 30;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;
        public override bool IsGoldTier => true;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.ComboGapMul *= 1.10f;
            profile.LionRoar = true;
        }
    }

    /// <summary>友切，误斩友刀的咎名：疾走取消连段留延迟斩影，承伤 +10% 且积咎耗气</summary>
    internal sealed class MeiTomokiri : OniMeiDefinition
    {
        public override int SortOrder => 40;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.IncomingDamageMul *= 1.10f;
            profile.GuiltEcho = true;
        }
    }

    /// <summary>风樋，轻身之槽：疾走/樱流省气，墨痕伤害 -25%</summary>
    internal sealed class MeiKazehi : OniMeiDefinition
    {
        public override int SortOrder => 50;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Hi;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.DashVigorCostMul *= 0.75f;
            profile.SakuraDrainMul *= 0.70f;
            profile.FlashMarkDamageMul *= 0.75f;
            profile.WindGroove = true;
        }
    }

    /// <summary>血樋，放血之槽：命中回气增强，自然回气减半且回气延迟加长</summary>
    internal sealed class MeiChihi : OniMeiDefinition
    {
        public override int SortOrder => 60;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Hi;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.NaturalRegenMul *= 0.50f;
            profile.ExtraRegenDelayTicks += 24;
            profile.ComboHitVigorBonus += 2f;
            profile.ZanshinHitVigorBonus += 8f;
            profile.BloodGroove = true;
        }
    }

    /// <summary>不动，梵字カーン笔意的护身雕：承诺动作中耗架势挡伤，架势获取 -20%</summary>
    internal sealed class MeiFudo : OniMeiDefinition
    {
        public override int SortOrder => 70;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Horimono;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.StanceGainMul *= 0.80f;
            profile.StanceGuard = true;
        }
    }

    /// <summary>倶利伽罗，缠剑龙雕，金象嵌：处决后点燃龙火连段，气力上限 100→80</summary>
    internal sealed class MeiKurikara : OniMeiDefinition
    {
        public override int SortOrder => 80;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Horimono;
        public override bool IsGoldTier => true;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.VigorMaxMul *= 0.80f;
            profile.DragonfireLoop = true;
        }
    }
}
