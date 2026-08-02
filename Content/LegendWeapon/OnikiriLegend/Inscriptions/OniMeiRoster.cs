namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    //铭文名册：每铭=一个可感知赋效+一项明确负担，数值全走 OniMeiCombatProfile 叠算；
    //茎铭取髭切一系改名史，Key 沿用保证存档连续。
    //L0 已改型号 U：铁截/滞樋/闲樋/镇鸣；其余扩册暂仍型号 B 待下批。

    /// <summary>髭切，斩首连须的旧名：残血终结增强，面板伤害 -2%</summary>
    internal sealed class MeiHigekiri : OniMeiDefinition
    {
        public override int SortOrder => 10;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.DamageMul *= 0.98f;
            profile.ExecuteLowLifeBonus = true;
        }
    }

    /// <summary>鬼切，一条戾桥断鬼腕得名，出厂默认铭。原铭=严格基准，无赋效无代价，不覆写任何字段</summary>
    internal sealed class MeiOnikiri : OniMeiDefinition
    {
        public override int SortOrder => 20;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;
    }

    /// <summary>铁截，拆台钝铁：连段首击截金钢铁体，血肉局面板钝</summary>
    internal sealed class MeiTessetsu : OniMeiDefinition
    {
        public override int SortOrder => 25;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.DamageMul *= 0.90f;
            profile.IronSever = true;
        }
    }

    /// <summary>旧首，取首：残心/灭世专收头残血；清杂略钝</summary>
    internal sealed class MeiKyushu : OniMeiDefinition
    {
        public override int SortOrder => 28;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.DamageMul *= 0.97f;
            profile.HeadHunt = true;
        }
    }

    /// <summary>狮子之子，夜吼如狮的荣名，金象嵌：完整五连第五拍合颚刃波，连段间隔 +2%</summary>
    internal sealed class MeiShishinoko : OniMeiDefinition
    {
        public override int SortOrder => 30;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;
        public override bool IsGoldTier => true;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.ComboGapMul *= 1.02f;
            profile.LionRoar = true;
        }
    }

    /// <summary>息合，吐息：完整五连第五拍甩出一道行进弧形剑气；连段略慢</summary>
    internal sealed class MeiIkiai : OniMeiDefinition
    {
        public override int SortOrder => 32;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.ComboGapMul *= 1.04f;
            profile.BreathWave = true;
        }
    }

    /// <summary>虚吼，空鸣：空场周期威压；远离再近一刀；贴身失焦</summary>
    internal sealed class MeiKyoko : OniMeiDefinition
    {
        public override int SortOrder => 35;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.HollowRoar = true;
        }
    }

    /// <summary>友切，误斩友刀的咎名：疾走取消连段留延迟斩影，承伤 +6% 且积咎耗气</summary>
    internal sealed class MeiTomokiri : OniMeiDefinition
    {
        public override int SortOrder => 40;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.IncomingDamageMul *= 1.06f;
            profile.GuiltEcho = true;
        }
    }

    /// <summary>假切，假身：疾走留残影吸一击；影在/影破承伤与疾走税</summary>
    internal sealed class MeiKarikiri : OniMeiDefinition
    {
        public override int SortOrder => 42;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.FalseBody = true;
        }
    }

    /// <summary>默切，默杀：疾走结束后短窗下一刀加深；狂闪不亮</summary>
    internal sealed class MeiMokukiri : OniMeiDefinition
    {
        public override int SortOrder => 45;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.SilentKill = true;
        }
    }

    /// <summary>风樋，轻身之槽：疾走/樱流省气，墨痕伤害 -25%</summary>
    internal sealed class MeiKazehi : OniMeiDefinition
    {
        public override int SortOrder => 50;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Hi;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.DashVigorCostMul *= 0.85f;
            profile.SakuraDrainMul *= 0.80f;
            profile.FlashMarkDamageMul *= 0.80f;
            profile.WindGroove = true;
        }
    }

    /// <summary>焦樋，焦痕：疾走路径留短灼地；站桩亏输出</summary>
    internal sealed class MeiKogehi : OniMeiDefinition
    {
        public override int SortOrder => 52;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Hi;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.DamageMul *= 0.96f;
            profile.ScorchTrail = true;
        }
    }

    /// <summary>闲樋，清静回气：脱战闲息快回；交战疾走/樱流略贵</summary>
    internal sealed class MeiKanhi : OniMeiDefinition
    {
        public override int SortOrder => 55;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Hi;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.DashVigorCostMul *= 1.08f;
            profile.SakuraDrainMul *= 1.08f;
            profile.QuietBreath = true;
        }
    }

    /// <summary>血樋，放血之槽：命中回气增强，自然回气减半且回气延迟加长</summary>
    internal sealed class MeiChihi : OniMeiDefinition
    {
        public override int SortOrder => 60;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Hi;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.NaturalRegenMul *= 0.65f;
            profile.ExtraRegenDelayTicks += 18;
            profile.ComboHitVigorBonus += 1f;
            profile.ZanshinHitVigorBonus += 4f;
            profile.BloodGroove = true;
        }
    }

    /// <summary>滞樋，黏着之槽：命中黏敌，疾走起步自黏</summary>
    internal sealed class MeiTodohi : OniMeiDefinition
    {
        public override int SortOrder => 62;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Hi;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.StickyBind = true;
        }
    }

    /// <summary>谢樋，剪落：击杀溅邻域小剪刃；空磨旱</summary>
    internal sealed class MeiShiorihi : OniMeiDefinition
    {
        public override int SortOrder => 65;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Hi;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.NaturalRegenMul *= 0.90f;
            profile.PetalPrune = true;
        }
    }

    /// <summary>潮樋，潮拍：合潮回气；错拍连段略亏</summary>
    internal sealed class MeiShiohi : OniMeiDefinition
    {
        public override int SortOrder => 68;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Hi;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.TideBeat = true;
        }
    }

    /// <summary>不动，梵字カーン笔意的护身雕：承诺动作中耗架势挡伤，架势获取 -20%</summary>
    internal sealed class MeiFudo : OniMeiDefinition
    {
        public override int SortOrder => 70;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Horimono;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.StanceGainMul *= 0.85f;
            profile.StanceGuard = true;
        }
    }

    /// <summary>痺雕，痺反：穿身格挡成功反麻来手；架势账更苛</summary>
    internal sealed class MeiShibori : OniMeiDefinition
    {
        public override int SortOrder => 72;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Horimono;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.StanceGainMul *= 0.85f;
            profile.NumbCounter = true;
        }
    }

    /// <summary>镇鸣，耳嗡抗弹：受弹伤/击退削弱；架势获取略慢</summary>
    internal sealed class MeiChinmei : OniMeiDefinition
    {
        public override int SortOrder => 75;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Horimono;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.StanceGainMul *= 0.85f;
            profile.QuellProjectiles = true;
        }
    }

    /// <summary>止足，止步：立定充电后残心/灭世/第五拍加深；跑砍无</summary>
    internal sealed class MeiAshidome : OniMeiDefinition
    {
        public override int SortOrder => 78;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Horimono;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.PlantedStep = true;
        }
    }

    /// <summary>倶利伽罗，缠剑龙雕，金象嵌：处决后点燃三次龙火连段，气力上限 100→90</summary>
    internal sealed class MeiKurikara : OniMeiDefinition
    {
        public override int SortOrder => 80;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Horimono;
        public override bool IsGoldTier => true;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.VigorMaxMul *= 0.90f;
            profile.DragonfireLoop = true;
        }
    }

    /// <summary>余炎，余烬场：处决后留持续灼地；气上限略紧，场在疾走更贵</summary>
    internal sealed class MeiYoen : OniMeiDefinition
    {
        public override int SortOrder => 85;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Horimono;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.VigorMaxMul *= 0.95f;
            profile.EmberField = true;
        }
    }
}
