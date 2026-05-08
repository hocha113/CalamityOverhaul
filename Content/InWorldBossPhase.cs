using System;
using Terraria;

namespace CalamityOverhaul.Content
{
    //这个类是用来进行判断游戏进度的，这很无赖，但我别无他法
    public static class InWorldBossPhase
    {
        #region Data
        public static bool Level0 => DownedV0.Invoke() && Downed0.Invoke();

        public static bool Level1 => DownedV1.Invoke();

        public static bool Level2 => DownedV2.Invoke();

        public static bool Level3 => Downed3.Invoke() || Downed4.Invoke();

        public static bool Level4 => Downed5.Invoke() || DownedV4.Invoke();

        public static bool Level5 => DownedCalamityandMechBoss1;

        public static bool Level6 => Downed10.Invoke();

        public static bool Level7 => DownedV7.Invoke();

        public static bool Level8 => VDownedV16.Invoke();

        public static bool Level9 => Downed19.Invoke();

        public static bool Level10 => Downed27.Invoke();

        public static bool Level11 => Downed28.Invoke();

        public static bool Level12 => Downed29.Invoke() && Downed30.Invoke();

        public static bool Level13 => Downed31.Invoke() || Downed32.Invoke();
        /// <summary>
        /// 击败所有机械Boss
        /// </summary>
        public static bool DownedAnyMechBoss => NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3;
        /// <summary>
        /// 击败毁灭者和渊灾
        /// </summary>
        public static bool DownedCalamityandMechBoss1 => NPC.downedMechBoss1 && Downed8.Invoke();
        /// <summary>
        /// 击败双子魔眼和硫磺火元素
        /// </summary>
        public static bool DownedCalamityandMechBoss2 => NPC.downedMechBoss2 && Downed7.Invoke();
        /// <summary>
        /// 击败机械骷髅王和极地冰灵
        /// </summary>
        public static bool DownedCalamityandMechBoss3 => NPC.downedMechBoss3 && Downed6.Invoke();
        /// <summary>
        /// 击败所有石后灾厄Boss
        /// </summary>
        public static bool DownedAnyAfterGolemBoss => Downed14.Invoke() && Downed15.Invoke() && Downed16.Invoke();
        /// <summary>
        /// 史莱姆王
        /// </summary>
        public static readonly Func<bool> DownedV0 = () => NPC.downedSlimeKing;
        /// <summary>
        /// 克苏鲁之眼
        /// </summary>
        public static readonly Func<bool> DownedV1 = () => NPC.downedBoss1;
        /// <summary>
        /// 邪恶Boss
        /// </summary>
        public static readonly Func<bool> DownedV2 = () => NPC.downedBoss2;
        /// <summary>
        /// 蜂后
        /// </summary>
        public static readonly Func<bool> DownedV3 = () => NPC.downedQueenBee;
        /// <summary>
        /// 骷髅王
        /// </summary>
        public static readonly Func<bool> DownedV4 = () => NPC.downedBoss3;
        /// <summary>
        /// 任意机械Boss
        /// </summary>
        public static readonly Func<bool> DownedV5 = () => NPC.downedMechBoss1 || NPC.downedMechBoss2 || NPC.downedMechBoss3;
        /// <summary>
        /// 所有机械Boss
        /// </summary>
        public static readonly Func<bool> DownedV6 = () => NPC.downedMechBossAny;
        /// <summary>
        /// 世纪之花
        /// </summary>
        public static readonly Func<bool> VDownedV7 = () => NPC.downedPlantBoss;
        /// <summary>
        /// 南瓜王
        /// </summary>
        public static readonly Func<bool> VDownedV8 = () => NPC.downedHalloweenKing;
        /// <summary>
        /// 冰霜女王
        /// </summary>
        public static readonly Func<bool> VDownedV9 = () => NPC.downedChristmasIceQueen;
        /// <summary>
        /// 石巨人
        /// </summary>
        public static readonly Func<bool> DownedV7 = () => NPC.downedGolemBoss;
        /// <summary>
        /// 邪教徒
        /// </summary>
        public static readonly Func<bool> DownedV8 = () => NPC.downedAncientCultist;
        /// <summary>
        /// 塔1
        /// </summary>
        public static readonly Func<bool> VDownedV10 = () => NPC.downedTowerSolar;
        /// <summary>
        /// 塔2
        /// </summary>
        public static readonly Func<bool> VDownedV11 = () => NPC.downedTowerVortex;
        /// <summary>
        /// 塔3
        /// </summary>
        public static readonly Func<bool> VDownedV12 = () => NPC.downedTowerNebula;
        /// <summary>
        /// 塔4
        /// </summary>
        public static readonly Func<bool> VDownedV13 = () => NPC.downedDeerclops;
        /// <summary>
        /// 月球领主
        /// </summary>
        public static readonly Func<bool> VDownedV16 = () => NPC.downedMoonlord;
        /// <summary>
        /// 荒漠灾虫
        /// </summary>
        public static readonly Func<bool> Downed0 = CWRRef.GetDownedDesertScourge;
        /// <summary>
        /// 巨像蛤
        /// </summary>
        public static readonly Func<bool> Downed1 = CWRRef.GetDownedCLAM;
        /// <summary>
        /// 蘑菇蟹
        /// </summary>
        public static readonly Func<bool> Downed2 = CWRRef.GetDownedCrabulon;
        /// <summary>
        /// 腐巢意志
        /// </summary>
        public static readonly Func<bool> Downed3 = CWRRef.GetDownedHiveMind;
        /// <summary>
        /// 血肉宿主
        /// </summary>
        public static readonly Func<bool> Downed4 = CWRRef.GetDownedPerforator;
        /// <summary>
        /// 史莱姆之神
        /// </summary>
        public static readonly Func<bool> Downed5 = CWRRef.GetDownedSlimeGod;
        /// <summary>
        /// 极地冰灵
        /// </summary>
        public static readonly Func<bool> Downed6 = CWRRef.GetDownedCryogen;
        /// <summary>
        /// 硫磺火元素
        /// </summary>
        public static readonly Func<bool> Downed7 = CWRRef.GetDownedBrimstoneElemental;
        /// <summary>
        /// 渊海灾虫
        /// </summary>
        public static readonly Func<bool> Downed8 = CWRRef.GetDownedAquaticScourge;
        /// <summary>
        /// 辐射之主
        /// </summary>
        public static readonly Func<bool> Downed9 = CWRRef.GetDownedCragmawMire;
        /// <summary>
        /// 灾厄之影
        /// </summary>
        public static readonly Func<bool> Downed10 = CWRRef.GetDownedCalamitasClone;
        /// <summary>
        /// 沙漠巨鲨
        /// </summary>
        public static readonly Func<bool> Downed11 = CWRRef.GetDownedGSS;
        /// <summary>
        /// 利维坦
        /// </summary>
        public static readonly Func<bool> Downed12 = CWRRef.GetDownedLeviathan;
        /// <summary>
        /// 白金星舰
        /// </summary>
        public static readonly Func<bool> Downed13 = CWRRef.GetDownedAstrumAureus;
        /// <summary>
        /// 瘟疫使者
        /// </summary>
        public static readonly Func<bool> Downed14 = CWRRef.GetDownedPlaguebringer;
        /// <summary>
        /// 毁灭魔像
        /// </summary>
        public static readonly Func<bool> Downed15 = CWRRef.GetDownedRavager;
        /// <summary>
        /// 星神游龙
        /// </summary>
        public static readonly Func<bool> Downed16 = CWRRef.GetDownedAstrumDeus;
        /// <summary>
        /// 亵渎使徒
        /// </summary>
        public static readonly Func<bool> Downed17 = CWRRef.GetDownedGuardians;
        /// <summary>
        /// 痴愚金龙
        /// </summary>
        public static readonly Func<bool> Downed18 = CWRRef.GetDownedDragonfolly;
        /// <summary>
        /// 亵渎天神
        /// </summary>
        public static readonly Func<bool> Downed19 = CWRRef.GetDownedProvidence;
        /// <summary>
        /// 无尽虚空
        /// </summary>
        public static readonly Func<bool> Downed20 = CWRRef.GetDownedCeaselessVoid;
        /// <summary>
        /// 风暴编织者
        /// </summary>
        public static readonly Func<bool> Downed21 = CWRRef.GetDownedStormWeaver;
        /// <summary>
        /// 西格纳斯
        /// </summary>
        public static readonly Func<bool> Downed22 = CWRRef.GetDownedSignus;
        /// <summary>
        /// 噬魂幽花
        /// </summary>
        public static readonly Func<bool> Downed23 = CWRRef.GetDownedPolterghast;
        /// <summary>
        /// 酸雨二
        /// </summary>
        public static readonly Func<bool> Downed24 = CWRRef.GetDownedMauler;
        /// <summary>
        /// 生化恐惧
        /// </summary>
        public static readonly Func<bool> Downed25 = CWRRef.GetDownedNuclearTerror;
        /// <summary>
        /// 老核弹
        /// </summary>
        public static readonly Func<bool> Downed26 = CWRRef.GetDownedBoomerDuke;
        /// <summary>
        /// 神明吞噬者
        /// </summary>
        public static readonly Func<bool> Downed27 = CWRRef.GetDownedDoG;
        /// <summary>
        /// 丛林龙
        /// </summary>
        public static readonly Func<bool> Downed28 = CWRRef.GetDownedYharon;
        /// <summary>
        /// 星流巨械
        /// </summary>
        public static readonly Func<bool> Downed29 = CWRRef.GetDownedExoMechs;
        /// <summary>
        /// 至尊灾厄
        /// </summary>
        public static readonly Func<bool> Downed30 = CWRRef.GetDownedCalamitas;
        /// <summary>
        /// 始源妖龙
        /// </summary>
        public static readonly Func<bool> Downed31 = CWRRef.GetDownedPrimordialWyrm;
        /// <summary>
        /// 终焉之战
        /// </summary>
        public static readonly Func<bool> Downed32 = CWRRef.GetDownedBossRush;
        #endregion

        public static int SHPC_Level() {
            int level = 0;
            //试炼0: 克苏鲁之眼
            if (DownedV1.Invoke()) level = 1; else return level;
            //试炼1: 邪恶Boss（世吞/克脑）
            if (DownedV2.Invoke()) level = 2; else return level;
            //试炼2: 腐巢意志/血肉宿主
            if (Downed3.Invoke() || Downed4.Invoke()) level = 3; else return level;
            //试炼3: 史莱姆之神
            if (Downed5.Invoke()) level = 4; else return level;
            //试炼4: 血肉墙
            if (Main.hardMode) level = 5; else return level;
            //试炼5: 渊海灾虫
            if (Downed8.Invoke()) level = 6; else return level;
            //试炼6: 硫磺火元素
            if (Downed7.Invoke()) level = 7; else return level;
            //试炼7: 毁灭者
            if (NPC.downedMechBoss1) level = 8; else return level;
            //试炼8: 双子魔眼
            if (NPC.downedMechBoss2) level = 9; else return level;
            //试炼9: 机械骷髅王
            if (NPC.downedMechBoss3) level = 10; else return level;
            //试炼10: 灾厄之影
            if (Downed10.Invoke()) level = 11; else return level;
            //试炼11: 世纪之花
            if (VDownedV7.Invoke()) level = 12; else return level;
            //试炼12: 石巨人
            if (DownedV7.Invoke()) level = 13; else return level;
            //试炼13: 邪教徒
            if (DownedV8.Invoke()) level = 14; else return level;
            //试炼14: 月球领主
            if (VDownedV16.Invoke()) level = 15; else return level;
            //试炼15: 亵渎天神
            if (Downed19.Invoke()) level = 16; else return level;
            //试炼16: 噬魂幽花
            if (Downed23.Invoke()) level = 17; else return level;
            //试炼17: 神明吞噬者
            if (Downed27.Invoke()) level = 18; else return level;
            //试炼18: 丛林龙犽戎
            if (Downed28.Invoke()) level = 19; else return level;
            //试炼19: 星流巨械
            if (Downed29.Invoke()) level = 20; else return level;
            //试炼20: 至尊灾厄
            if (Downed30.Invoke()) level = 21; else return level;
            //试炼21: 终焉之战
            if (Downed32.Invoke()) level = 22; else return level;
            return level;
        }

        public static int Halibut_Level() {
            int level = 0;

            if (DownedV0.Invoke()) {
                level = 1;
            }
            else {
                return level;
            }

            if (DownedV1.Invoke()) {
                level = 2;
            }
            else {
                return level;
            }

            if (DownedV3.Invoke()) {
                level = 3;
            }
            else {
                return level;
            }

            if (DownedV4.Invoke() && Main.hardMode) {
                level = 4;
            }
            else {
                return level;
            }

            if (DownedV5.Invoke() || Downed8.Invoke()) {
                level = 5;
            }
            else {
                return level;
            }

            if (Downed10.Invoke() || VDownedV7.Invoke()) {
                level = 6;
            }
            else {
                return level;
            }

            if (DownedV7.Invoke()) {
                level = 7;
            }
            else {
                return level;
            }

            if (VDownedV16.Invoke()) {
                level = 8;
            }
            else {
                return level;
            }

            if (Downed19.Invoke()) {
                level = 9;
            }
            else {
                return level;
            }

            if (Downed23.Invoke()) {
                level = 10;
            }
            else {
                return level;
            }

            if (Downed27.Invoke()) {
                level = 11;
            }
            else {
                return level;
            }

            if (Downed28.Invoke()) {
                level = 12;
            }
            else {
                return level;
            }

            if (Downed29.Invoke() && Downed30.Invoke()) {
                level = 13;
            }
            else {
                return level;
            }

            if (Downed31.Invoke() || Downed32.Invoke()) {
                level = 14;
            }
            else {
                return level;
            }

            return level;
        }

        public static int Mura_Level() {
            int level = 0;
            //试炼0: 史莱姆王
            if (DownedV0.Invoke()) level = 1; else return level;
            //试炼1: 荒漠灾虫
            if (Downed0.Invoke()) level = 2; else return level;
            //试炼2: 克苏鲁之眼
            if (DownedV1.Invoke()) level = 3; else return level;
            //试炼3: 邪恶Boss（世吞/克脑）
            if (DownedV2.Invoke()) level = 4; else return level;
            //试炼4: 灾厄邪恶Boss（腐巢意志/血肉宿主）
            if (Downed3.Invoke() || Downed4.Invoke()) level = 5; else return level;
            //试炼5: 骷髅王
            if (DownedV4.Invoke()) level = 6; else return level;
            //试炼6: 史莱姆之神
            if (Downed5.Invoke()) level = 7; else return level;
            //试炼7: 血肉墙
            if (Main.hardMode) level = 8; else return level;
            //试炼8: 渊海灾虫
            if (Downed8.Invoke()) level = 9; else return level;
            //试炼9: 硫磺火元素
            if (Downed7.Invoke()) level = 10; else return level;
            //试炼10: 极地冰灵
            if (Downed6.Invoke()) level = 11; else return level;
            //试炼11: 毁灭者
            if (NPC.downedMechBoss1) level = 12; else return level;
            //试炼12: 双子魔眼
            if (NPC.downedMechBoss2) level = 13; else return level;
            //试炼13: 机械骷髅王
            if (NPC.downedMechBoss3) level = 14; else return level;
            //试炼14: 灾厄之影
            if (Downed10.Invoke()) level = 15; else return level;
            //试炼15: 世纪之花
            if (VDownedV7.Invoke()) level = 16; else return level;
            //试炼16: 石巨人
            if (DownedV7.Invoke()) level = 17; else return level;
            //试炼17: 瘟疫使者
            if (Downed14.Invoke()) level = 18; else return level;
            //试炼18: 毁灭魔像
            if (Downed15.Invoke()) level = 19; else return level;
            //试炼19: 星神游龙
            if (Downed16.Invoke()) level = 20; else return level;
            //试炼20: 月球领主
            if (VDownedV16.Invoke()) level = 21; else return level;
            //试炼21: 亵渎天神
            if (Downed19.Invoke()) level = 22; else return level;
            //试炼22: 噬魂幽花
            if (Downed23.Invoke()) level = 23; else return level;
            //试炼23: 神明吞噬者
            if (Downed27.Invoke()) level = 24; else return level;
            //试炼24: 丛林龙犽戎
            if (Downed28.Invoke()) level = 25; else return level;
            //试炼25: 星流巨械
            if (Downed29.Invoke()) level = 26; else return level;
            //试炼26: 至尊灾厄
            if (Downed30.Invoke()) level = 27; else return level;
            //试炼27: 始源妖龙
            if (Downed31.Invoke() || Downed32.Invoke()) level = 28; else return level;
            return level;
        }
    }
}
