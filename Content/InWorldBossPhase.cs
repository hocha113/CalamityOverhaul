using CalamityMod;
using System;
using Terraria;

namespace CalamityOverhaul.Content
{
    //这个类是用来进行判断游戏进度的，这很无赖，但我别无他法
    public class InWorldBossPhase
    {
        public static InWorldBossPhase Instance { get; private set; }

        public void Load() => Instance = this;

        public bool level0 => DownedV0.Invoke() || Downed0.Invoke() || Downed2.Invoke();

        public bool level1 => DownedV1.Invoke() || Downed1.Invoke();

        public bool level2 => DownedV2.Invoke() || DownedV3.Invoke();

        public bool level3 => DownedV4.Invoke() || Downed3.Invoke() || Downed4.Invoke();

        public bool level4 => Downed5.Invoke();

        public bool level5 => Downed6.Invoke() || Downed7.Invoke() || Downed8.Invoke() || DownedV5.Invoke();

        public bool level6 => Downed10.Invoke() || Downed12.Invoke() || DownedV7.Invoke();

        public bool level7 => Downed14.Invoke() || Downed15.Invoke() || Downed16.Invoke();

        public bool level8 => Downed17.Invoke() || Downed18.Invoke();

        public bool level9 => Downed19.Invoke() || Downed20.Invoke() || Downed21.Invoke() || Downed22.Invoke() || Downed23.Invoke();

        public bool level10 => Downed26.Invoke() || Downed27.Invoke();

        public bool level11 => Downed28.Invoke();

        public bool level12 => Downed29.Invoke() || Downed30.Invoke();

        public bool level13 => Downed31.Invoke() || Downed32.Invoke();

        public int Level() {
            //int a = (int)((Main.GameUpdateCount / 120) % 15);
            //return a;
            int level = 0;
            if (level0) {
                level = 1;
            }
            else {
                return level;
            }

            if (level1) {
                level = 2;
            }
            else {
                return level;
            }

            if (level2) {
                level = 3;
            }
            else {
                return level;
            }

            if (level3) {
                level = 4;
            }
            else {
                return level;
            }

            if (level4) {
                level = 5;
            }
            else {
                return level;
            }

            if (level5) {
                level = 6;
            }
            else {
                return level;
            }

            if (level6) {
                level = 7;
            }
            else {
                return level;
            }

            if (level7) {
                level = 8;
            }
            else {
                return level;
            }

            if (level8) {
                level = 9;
            }
            else {
                return level;
            }

            if (level9) {
                level = 10;
            }
            else {
                return level;
            }

            if (level10) {
                level = 11;
            }
            else {
                return level;
            }

            if (level11) {
                level = 12;
            }
            else {
                return level;
            }

            if (level12) {
                level = 13;
            }
            else {
                return level;
            }

            if (level13) {
                level = 14;
            }
            else {
                return level;
            }

            return level;
        }

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
        /// 石巨人
        /// </summary>
        public static readonly Func<bool> DownedV7 = () => NPC.downedGolemBoss;
        /// <summary>
        /// 邪教徒
        /// </summary>
        public static readonly Func<bool> DownedV8 = () => NPC.downedAncientCultist;
        /// <summary>
        /// 荒漠灾虫
        /// </summary>
        public static readonly Func<bool> Downed0 = () => DownedBossSystem.downedDesertScourge;
        /// <summary>
        /// 巨像蛤
        /// </summary>
        public static readonly Func<bool> Downed1 = () => DownedBossSystem.downedCLAM;
        /// <summary>
        /// 蘑菇蟹
        /// </summary>
        public static readonly Func<bool> Downed2 = () => DownedBossSystem.downedCrabulon;
        /// <summary>
        /// 腐巢意志
        /// </summary>
        public static readonly Func<bool> Downed3 = () => DownedBossSystem.downedHiveMind;
        /// <summary>
        /// 血肉宿主
        /// </summary>
        public static readonly Func<bool> Downed4 = () => DownedBossSystem.downedPerforator;
        /// <summary>
        /// 史莱姆之神
        /// </summary>
        public static readonly Func<bool> Downed5 = () => DownedBossSystem.downedSlimeGod;
        /// <summary>
        /// 极地冰灵
        /// </summary>
        public static readonly Func<bool> Downed6 = () => DownedBossSystem.downedCryogen;
        /// <summary>
        /// 硫磺火元素
        /// </summary>
        public static readonly Func<bool> Downed7 = () => DownedBossSystem.downedBrimstoneElemental;
        /// <summary>
        /// 渊海灾虫
        /// </summary>
        public static readonly Func<bool> Downed8 = () => DownedBossSystem.downedAquaticScourge;
        /// <summary>
        /// 辐射之主
        /// </summary>
        public static readonly Func<bool> Downed9 = () => DownedBossSystem.downedCragmawMire;
        /// <summary>
        /// 灾厄之影
        /// </summary>
        public static readonly Func<bool> Downed10 = () => DownedBossSystem.downedCalamitasClone;
        /// <summary>
        /// 沙漠巨鲨
        /// </summary>
        public static readonly Func<bool> Downed11 = () => DownedBossSystem.downedGSS;
        /// <summary>
        /// 利维坦
        /// </summary>
        public static readonly Func<bool> Downed12 = () => DownedBossSystem.downedLeviathan;
        /// <summary>
        /// 白金星舰
        /// </summary>
        public static readonly Func<bool> Downed13 = () => DownedBossSystem.downedAstrumAureus;
        /// <summary>
        /// 瘟疫使者
        /// </summary>
        public static readonly Func<bool> Downed14 = () => DownedBossSystem.downedPlaguebringer;
        /// <summary>
        /// 毁灭魔像
        /// </summary>
        public static readonly Func<bool> Downed15 = () => DownedBossSystem.downedRavager;
        /// <summary>
        /// 星神游龙
        /// </summary>
        public static readonly Func<bool> Downed16 = () => DownedBossSystem.downedAstrumDeus;
        /// <summary>
        /// 亵渎使徒
        /// </summary>
        public static readonly Func<bool> Downed17 = () => DownedBossSystem.downedGuardians;
        /// <summary>
        /// 痴愚金龙
        /// </summary>
        public static readonly Func<bool> Downed18 = () => DownedBossSystem.downedDragonfolly;
        /// <summary>
        /// 亵渎天神
        /// </summary>
        public static readonly Func<bool> Downed19 = () => DownedBossSystem.downedProvidence;
        /// <summary>
        /// 无尽虚空
        /// </summary>
        public static readonly Func<bool> Downed20 = () => DownedBossSystem.downedCeaselessVoid;
        /// <summary>
        /// 风暴吞噬者
        /// </summary>
        public static readonly Func<bool> Downed21 = () => DownedBossSystem.downedStormWeaver;
        /// <summary>
        /// 席格纳斯
        /// </summary>
        public static readonly Func<bool> Downed22 = () => DownedBossSystem.downedSignus;
        /// <summary>
        /// 噬魂幽花
        /// </summary>
        public static readonly Func<bool> Downed23 = () => DownedBossSystem.downedPolterghast;
        /// <summary>
        /// 酸雨二
        /// </summary>
        public static readonly Func<bool> Downed24 = () => DownedBossSystem.downedMauler;
        /// <summary>
        /// 生化恐惧
        /// </summary>
        public static readonly Func<bool> Downed25 = () => DownedBossSystem.downedNuclearTerror;
        /// <summary>
        /// 老核弹
        /// </summary>
        public static readonly Func<bool> Downed26 = () => DownedBossSystem.downedBoomerDuke;
        /// <summary>
        /// 神明吞噬者
        /// </summary>
        public static readonly Func<bool> Downed27 = () => DownedBossSystem.downedDoG;
        /// <summary>
        /// 丛林龙
        /// </summary>
        public static readonly Func<bool> Downed28 = () => DownedBossSystem.downedYharon;
        /// <summary>
        /// 星流巨械
        /// </summary>
        public static readonly Func<bool> Downed29 = () => DownedBossSystem.downedExoMechs;
        /// <summary>
        /// 至尊灾厄
        /// </summary>
        public static readonly Func<bool> Downed30 = () => DownedBossSystem.downedCalamitas;
        /// <summary>
        /// 始源妖龙
        /// </summary>
        public static readonly Func<bool> Downed31 = () => DownedBossSystem.downedPrimordialWyrm;
        /// <summary>
        /// 终焉之战
        /// </summary>
        public static readonly Func<bool> Downed32 = () => DownedBossSystem.downedBossRush;
    }
}
