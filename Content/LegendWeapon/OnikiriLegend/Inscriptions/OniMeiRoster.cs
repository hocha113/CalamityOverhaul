namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    //铭文名册：每铭=一个可感知赋效+一项明确负担，数值全走 OniMeiCombatProfile 叠算；
    //茎铭取髭切一系改名史，Key 沿用保证存档连续。扩册 15 为同族变体（型号 B）

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

    /// <summary>铁截，拆台断首变体：斩杀线仍开，面板更钝</summary>
    internal sealed class MeiTessetsu : OniMeiDefinition
    {
        public override int SortOrder => 25;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.DamageMul *= 0.85f;
            profile.ExecuteLowLifeBonus = true;
        }
    }

    /// <summary>旧首，断首变体：面板税略轻，斩杀仍开</summary>
    internal sealed class MeiKyushu : OniMeiDefinition
    {
        public override int SortOrder => 28;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.DamageMul *= 0.92f;
            profile.ExecuteLowLifeBonus = true;
        }
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

    /// <summary>息合，合颚预告变体（非金）：合颚仍开，连段变慢更轻</summary>
    internal sealed class MeiIkiai : OniMeiDefinition
    {
        public override int SortOrder => 32;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.ComboGapMul *= 1.05f;
            profile.LionRoar = true;
        }
    }

    /// <summary>虚吼，合颚失焦变体：合颚开，连段更慢</summary>
    internal sealed class MeiKyoko : OniMeiDefinition
    {
        public override int SortOrder => 35;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.ComboGapMul *= 1.14f;
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

    /// <summary>假切，咎影学步变体：影仍开，承伤税略重</summary>
    internal sealed class MeiKarikiri : OniMeiDefinition
    {
        public override int SortOrder => 42;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.IncomingDamageMul *= 1.12f;
            profile.GuiltEcho = true;
        }
    }

    /// <summary>默切，终局沉咎变体：影仍开，承伤税略轻</summary>
    internal sealed class MeiMokukiri : OniMeiDefinition
    {
        public override int SortOrder => 45;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.IncomingDamageMul *= 1.08f;
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

    /// <summary>焦樋，避热顺风变体：疾走更省，墨痕税更狠</summary>
    internal sealed class MeiKogehi : OniMeiDefinition
    {
        public override int SortOrder => 52;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Hi;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.DashVigorCostMul *= 0.70f;
            profile.SakuraDrainMul *= 0.75f;
            profile.FlashMarkDamageMul *= 0.65f;
            profile.WindGroove = true;
        }
    }

    /// <summary>闲樋，清静顺风变体：樱流更省，疾走省气略弱</summary>
    internal sealed class MeiKanhi : OniMeiDefinition
    {
        public override int SortOrder => 55;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Hi;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.DashVigorCostMul *= 0.80f;
            profile.SakuraDrainMul *= 0.60f;
            profile.FlashMarkDamageMul *= 0.80f;
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

    /// <summary>滞樋，黏着回流变体：连段回气略多，脱战延迟更重</summary>
    internal sealed class MeiTodohi : OniMeiDefinition
    {
        public override int SortOrder => 62;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Hi;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.NaturalRegenMul *= 0.45f;
            profile.ExtraRegenDelayTicks += 30;
            profile.ComboHitVigorBonus += 3f;
            profile.ZanshinHitVigorBonus += 7f;
            profile.BloodGroove = true;
        }
    }

    /// <summary>谢樋，剪除回流变体：残心回气更厚，连段回气略薄</summary>
    internal sealed class MeiShiorihi : OniMeiDefinition
    {
        public override int SortOrder => 65;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Hi;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.NaturalRegenMul *= 0.55f;
            profile.ExtraRegenDelayTicks += 20;
            profile.ComboHitVigorBonus += 1f;
            profile.ZanshinHitVigorBonus += 11f;
            profile.BloodGroove = true;
        }
    }

    /// <summary>潮樋，潮湿回流变体：命中回气拉长感，自然回气更差</summary>
    internal sealed class MeiShiohi : OniMeiDefinition
    {
        public override int SortOrder => 68;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Hi;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.NaturalRegenMul *= 0.40f;
            profile.ExtraRegenDelayTicks += 28;
            profile.ComboHitVigorBonus += 2f;
            profile.ZanshinHitVigorBonus += 10f;
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

    /// <summary>痺雕，厚护变体：护仍开，架势税更重</summary>
    internal sealed class MeiShibori : OniMeiDefinition
    {
        public override int SortOrder => 72;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Horimono;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.StanceGainMul *= 0.70f;
            profile.StanceGuard = true;
        }
    }

    /// <summary>镇鸣，耳嗡护身变体：护仍开，架势税略轻</summary>
    internal sealed class MeiChinmei : OniMeiDefinition
    {
        public override int SortOrder => 75;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Horimono;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.StanceGainMul *= 0.85f;
            profile.StanceGuard = true;
        }
    }

    /// <summary>止足，站住收束变体：护仍开，架势税中等</summary>
    internal sealed class MeiAshidome : OniMeiDefinition
    {
        public override int SortOrder => 78;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Horimono;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.StanceGainMul *= 0.78f;
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

    /// <summary>余炎，龙火余烬变体：龙火仍开，气上限税略轻</summary>
    internal sealed class MeiYoen : OniMeiDefinition
    {
        public override int SortOrder => 85;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Horimono;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.VigorMaxMul *= 0.85f;
            profile.DragonfireLoop = true;
        }
    }
}
