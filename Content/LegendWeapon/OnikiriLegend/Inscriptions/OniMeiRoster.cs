namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    //铭文名册：每铭=一个可感知赋效+一项明确负担，稳定 Key 保持存档连续

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

    /// <summary>蜘蛛切，斩土蜘蛛的源氏旧名：刀击钉丝锚，三锚闭网内收；结网期回气变慢</summary>
    internal sealed class MeiKumokiri : OniMeiDefinition
    {
        public override int SortOrder => 47;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.SilkSnare = true;
        }
    }

    /// <summary>鬼丸，天下五剑自行断鬼足的旧名：站定够久刀自己出手；脱手期间无刀可用</summary>
    internal sealed class MeiOnimaru : OniMeiDefinition
    {
        public override int SortOrder => 22;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;
        public override bool IsGoldTier => true;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.SelfCut = true;
        }
    }

    /// <summary>雷切，立花道雪雨中斩雷神：大招引雷贯敌；晴天蓄雷更慢，洞里不落</summary>
    internal sealed class MeiRaikiri : OniMeiDefinition
    {
        public override int SortOrder => 33;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            //蓄雷的代价压在排拍上：晴天也一样慢，别让玩家只在雷暴天才觉得亏
            profile.ComboGapMul *= OniMeiCombat.ThunderClearSkyWindupMul;
            profile.ThunderCall = true;
        }
    }

    /// <summary>鵺切，源赖政射落鵺：空中第五拍改俯冲砸地；落地收势不能疾走</summary>
    internal sealed class MeiNuekiri : OniMeiDefinition
    {
        public override int SortOrder => 44;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Nakago;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.NueDive = true;
        }
    }

    /// <summary>风樋，轻身之槽：疾走/樱流省气，墨痕伤害 -20%</summary>
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

    /// <summary>血樋，放血之槽：命中回气增强，自然回气 -35% 且回气延迟加长</summary>
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

    /// <summary>紙樋，把面影带到表世界：疾走穿身挂纸型，斩纸传导本体；有纸在场疾走更费</summary>
    internal sealed class MeiKamihi : OniMeiDefinition
    {
        public override int SortOrder => 57;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Hi;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.PaperEffigy = true;
        }
    }

    /// <summary>空樋，浮身：离地多一次疾走并滞空；落地沉底回气归零，地面疾走略贵</summary>
    internal sealed class MeiSorahi : OniMeiDefinition
    {
        public override int SortOrder => 58;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Hi;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.DashVigorCostMul *= 1.10f;
            profile.AirGroove = true;
        }
    }

    /// <summary>鏡樋，镜写：疾走终点留纸镜立像复刻下一刀；分神令面板伤害 -5%</summary>
    internal sealed class MeiKagamihi : OniMeiDefinition
    {
        public override int SortOrder => 59;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Hi;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.DamageMul *= 0.95f;
            profile.MirrorEcho = true;
        }
    }

    /// <summary>雨樋，落雨：樱流沿途滴墨成洼；樱流耗气 +15%</summary>
    internal sealed class MeiAmahi : OniMeiDefinition
    {
        public override int SortOrder => 66;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Hi;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.SakuraDrainMul *= OniMeiCombat.InkRainSakuraDrainMul;
            profile.InkRain = true;
        }
    }

    /// <summary>綴樋，缀痕：墨痕之间连缀切开；单枚墨痕伤害 -30%</summary>
    internal sealed class MeiTsuzurihi : OniMeiDefinition
    {
        public override int SortOrder => 67;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Hi;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.FlashMarkDamageMul *= OniMeiCombat.MarkStitchSoloMarkMul;
            profile.MarkStitch = true;
        }
    }

    /// <summary>不动，梵字カーン笔意的护身雕：承诺动作中耗架势挡伤，架势获取 -15%</summary>
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

    /// <summary>梵鐘，一撞：满架势憋住不放终结即自鸣撞钟，架势砍半换一圈控场</summary>
    internal sealed class MeiBonsho : OniMeiDefinition
    {
        public override int SortOrder => 76;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Horimono;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.BellToll = true;
        }
    }

    /// <summary>般若，面变：残血翻鬼面，刀更重更狠还吸血；鬼面期更脆，女面期略钝</summary>
    internal sealed class MeiHannya : OniMeiDefinition
    {
        public override int SortOrder => 77;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Horimono;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            //女面期的钝压在面板上；鬼面的加深走命中侧，翻面才有得赚
            profile.DamageMul *= 0.96f;
            profile.HannyaMask = true;
        }
    }

    /// <summary>枯山水，砂纹：立定耙出留在原地的砂纹场，场内持续割并涨架势</summary>
    internal sealed class MeiKaresansui : OniMeiDefinition
    {
        public override int SortOrder => 79;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Horimono;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.SandGarden = true;
        }
    }

    /// <summary>千手，金象嵌：终结定格多浮六手同斩；终结后气清零且久不能疾走</summary>
    internal sealed class MeiSenju : OniMeiDefinition
    {
        public override int SortOrder => 88;
        public override OniMeiSlotKind SlotKind => OniMeiSlotKind.Horimono;
        public override bool IsGoldTier => true;

        public override void ModifyCombatProfile(ref OniMeiCombatProfile profile) {
            profile.SenjuArms = true;
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
