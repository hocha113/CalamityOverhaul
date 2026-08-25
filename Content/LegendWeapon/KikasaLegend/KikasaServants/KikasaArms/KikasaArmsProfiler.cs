using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms
{
    /// <summary>械奴武器族别：None 之外都能被湖学会驱使</summary>
    internal enum KikasaArmsKind : byte
    {
        /// <summary>不是可复制的武器，沉湖只作纯存储</summary>
        None,
        /// <summary>枪械：远程且吃子弹/镖弹药</summary>
        Gun,
        /// <summary>刀剑：挥舞型近战且非工具</summary>
        Blade,
        /// <summary>鞭子：发射物登记在 ProjectileID.Sets.IsAWhip 的召唤武器</summary>
        Whip,
        /// <summary>弓弩：远程且吃箭弹药</summary>
        Bow,
        /// <summary>鱼竿：fishingPole 大于 0 的钓具（无伤害武器，渔力定强度）</summary>
        Rod,
        /// <summary>长矛：近战且发射物是原版矛 AI（aiStyle 19），短剑（Rapier 突刺）同族</summary>
        Spear,
        /// <summary>投掷消耗品：手里剑/苦无/炸弹族（消耗品自含弹幕）</summary>
        Thrown,
        /// <summary>回旋镖：非消耗且发射物是原版回旋镖 AI（aiStyle 3），伤害类型不限</summary>
        Boomerang,
        /// <summary>悠悠球：Sets.Yoyo 登记的球（原版悬浮机制的必要旗，模组球同旗）</summary>
        Yoyo,
        /// <summary>连枷：发射物是原版连枷 AI（aiStyle 15）的甩锤链兵</summary>
        Flail,
    }

    /// <summary>弓弩原型：决定拉弦节奏与出招池</summary>
    internal enum KikasaBowArchetype : byte
    {
        /// <summary>速射连弩族：平射连珠 + 箭雨</summary>
        Rapid,
        /// <summary>制式弓族：抛射排箭 + 箭雨</summary>
        Standard,
        /// <summary>重弓族：贯穿重箭轮值 + 抛射排箭</summary>
        Longbow,
    }

    /// <summary>枪械原型：决定出招池与编队规模，数值个性化走档案字段</summary>
    internal enum KikasaGunArchetype : byte
    {
        /// <summary>速射连发族（迷你鲨系）：列阵齐射 + 环猎</summary>
        Auto,
        /// <summary>点射手枪/步枪族：齐射放缓、单发加重</summary>
        Standard,
        /// <summary>狙击族：点名狙杀轮值，慢而重</summary>
        Sniper,
        /// <summary>霰弹族：拢射墙齐轰，整队后坐</summary>
        Shotgun,
    }

    /// <summary>刀剑轻重档：决定刃数与突斩的蓄/停配重</summary>
    internal enum KikasaBladeWeight : byte
    {
        /// <summary>轻刃：快接力小斩痕</summary>
        Light,
        /// <summary>重剑：蓄更长停更硬</summary>
        Heavy,
        /// <summary>巨兵：刃少拍重，带过顶下劈</summary>
        Grand,
    }

    /// <summary>
    /// 枪奴档案：行为与时序字段全部由 ContentSamples 基础数值确定性推得（各端与服务器一致）；
    /// 贴图相关字段（枪口探出/绘制缩放）只影响绘制与 owner 端出膛点，不进入远端模拟
    /// </summary>
    internal readonly record struct KikasaGunProfile(
        KikasaGunArchetype Archetype,
        int FirePeriod,        //轮转开火周期（齐射/环猎的节拍基准）
        int FireStagger,       //相邻枪错帧
        float ShotDamageMul,   //单发伤害倍率（已含节奏折算）
        float BulletSpeed,     //出膛速度
        int Pellets,           //霰弹每喷粒数，其余为 1
        int MaxUnits,          //编队上限（重武器凝得少）
        float RecoilMul,       //后坐幅度倍率
        SoundStyle FireSound,  //开火音：直接借原武器的 UseSound：个性化白送
        float MuzzleLen,       //枪口探出距离
        float DrawScale);      //绘制缩放（超大贴图收一号、小贴图放一号）

    /// <summary>刀奴档案：同枪奴契约，行为字段服务器安全，贴图字段只管绘制</summary>
    internal readonly record struct KikasaBladeProfile(
        KikasaBladeWeight Weight,
        int RelayPeriod,       //轮转突斩的接力节拍基准
        float SlashDamageMul,  //单斩伤害倍率（已含节奏折算）
        int MaxUnits,          //刃数上限
        SoundStyle SwingSound, //挥砍音：借原武器 UseSound
        bool HasWave,          //原武器自带剑气（item.shoot），预留的差异化轴
        float BladeLen,        //刃长（对角线口径），斩痕规格与判定宽度的基准
        float DrawScale);      //绘制缩放

    /// <summary>弓奴档案：同枪奴契约，行为字段服务器安全、贴图字段只管绘制</summary>
    internal readonly record struct KikasaBowProfile(
        KikasaBowArchetype Archetype,
        int DrawPeriod,        //拉弦-放箭一轮的节拍基准
        int FireStagger,       //相邻弓错帧
        float ArrowDamageMul,  //单箭伤害倍率（已含节奏折算）
        float ArrowSpeed,      //出弦速度
        int MaxUnits,          //编队上限
        SoundStyle FireSound,  //放箭音：借原武器 UseSound
        float BowSpan,         //弓身纵距（贴图高折算），握点与搭箭位的基准
        float DrawScale);      //绘制缩放

    /// <summary>
    /// 钓奴档案：强度不走 DPS 曲线（鱼竿无伤害），渔力是唯一证词，
    /// CatchDamage 为甩出渔获的基伤、CastPeriod 为收竿节拍（渔力越高收得越勤）
    /// </summary>
    internal readonly record struct KikasaRodProfile(
        int FishPower,         //渔力（item.fishingPole）
        int CastPeriod,        //一轮起竿-收获的节拍
        int CatchDamage,       //单件渔获基伤
        SoundStyle CastSound,  //起竿音：借原武器 UseSound
        float RodLen,          //竿身对角线长，竿梢出线点的基准
        float DrawScale);      //绘制缩放

    /// <summary>矛奴档案：同刀奴契约</summary>
    internal readonly record struct KikasaSpearProfile(
        int ThrustPeriod,      //轮转突刺的接力节拍
        float ThrustDamageMul, //单刺伤害倍率（已含节奏折算）
        int MaxUnits,          //矛数上限
        SoundStyle ThrustSound,//突刺音：借原武器 UseSound
        float ReachLen,        //矛身对角线长，突刺行程与判定长度的基准
        float DrawScale);      //绘制缩放

    /// <summary>
    /// 掷奴档案：唯一转发原武器弹幕的族，安全性由消耗品结构背书
    /// （物品掷出即消耗，弹幕不可能依赖仍被手持）
    /// </summary>
    internal readonly record struct KikasaThrowProfile(
        int ThrowProjType,     //原武器登记的投掷弹幕（item.shoot），直接转发
        int ThrowPeriod,       //轮转投掷的接力节拍
        int ThrowStagger,      //相邻掷手错帧
        float ThrowDamageMul,  //单掷伤害倍率（已含节奏折算）
        float ThrowSpeed,      //掷出速度（原武器 shootSpeed 兜底抬升）
        int MaxUnits,          //掷手上限
        SoundStyle ThrowSound, //掷出音：借原武器 UseSound
        float DrawScale);      //绘制缩放

    /// <summary>
    /// 鞭奴档案：几何与时序全部对齐原版 AI_165 契约
    /// 段数/射程倍率读鞭弹幕模板的 WhipSettings，甩出时长与 itemAnimationMax×MaxUpdates
    /// 的实际帧数等价（复制体不吃玩家挥舞，用 useAnimation 自持计时）
    /// </summary>
    internal readonly record struct KikasaWhipProfile(
        int WhipProjType,      //原鞭发射物类型：曲线设置/分段贴图/标签映射之源
        int Segments,          //控制点段数（WhipSettings.Segments）
        float RangeMul,        //射程倍率（WhipSettings.RangeMultiplier）
        int UseAnimation,      //原武器挥舞周期：曲线 reach 公式的时长因子
        int LashTime,          //一记鞭笞的甩出-回收总帧数
        int LashPeriod,        //轮转鞭笞的接力节拍
        float LashDamageMul,   //单鞭伤害倍率（已含节奏折算）
        float ShootSpeed,      //原武器弹速：曲线 reach 公式的速度因子
        float PeakReach,       //鞭尖最大探出 px（甩出峰值处），驻位距离的依据
        int MaxUnits,          //鞭数上限
        SoundStyle SwingSound, //起鞭挥音：借原武器 UseSound
        float DrawScale);      //盘鞭本体（物品贴图）绘制缩放

    /// <summary>镖奴档案：同刀奴契约，行为字段服务器安全、贴图字段只管绘制</summary>
    internal readonly record struct KikasaBoomerangProfile(
        int ThrowPeriod,       //轮转掷镖的接力节拍
        float ThrowDamageMul,  //单镖伤害倍率（已含节奏折算）
        float FlightSpeed,     //出手速度
        float Range,           //去程射程 px（回旋折返点）
        int MaxUnits,          //镖手上限
        SoundStyle ThrowSound, //掷镖音：借原武器 UseSound
        float DrawScale);      //绘制缩放

    /// <summary>
    /// 球奴档案：个性化全押原版悠悠球三件套静态集合（服务器可用）——
    /// 顶速/最大放线距离定强度姿态，寿命倍率（-1=无限）折驻留时长
    /// </summary>
    internal readonly record struct KikasaYoyoProfile(
        int CastPeriod,        //轮转放球的接力节拍
        float TickDamageMul,   //单跳伤害倍率（驻留高频跳，单跳轻）
        float TopSpeed,        //球速（Sets.YoyosTopSpeed）
        float MaxReach,        //放线距离 px（Sets.YoyosMaximumRange）
        int DwellTime,         //驻留磨伤时长帧
        int MaxUnits,          //球手上限
        SoundStyle CastSound,  //放球音：借原武器 UseSound
        float DrawScale);      //绘制缩放

    /// <summary>锤奴档案：锤头贴图取原武器弹幕（连枷物品贴图是整柄），链条画血水珠链</summary>
    internal readonly record struct KikasaFlailProfile(
        int HeadProjType,      //原连枷锤头弹幕类型：锤头贴图之源
        int SlamPeriod,        //轮转抡掷的接力节拍
        float SlamDamageMul,   //单掷伤害倍率（已含节奏折算）
        float FlightSpeed,     //掷出速度
        float Reach,           //甩程 px
        int MaxUnits,          //锤数上限
        SoundStyle SwingSound, //抡掷音：借原武器 UseSound
        float DrawScale);      //锤头绘制缩放

    /// <summary>
    /// 械奴档案推断器：从沉湖物品的基础数值推族别与个性化档案。
    /// 全部读 ContentSamples 模板（不读玩家实例，词缀不参与，远端与服务器无湖藏数据）；
    /// 影响行为/时序的字段只用服务器可用的数值，贴图尺寸只允许影响纯绘制
    /// </summary>
    internal static class KikasaArmsProfiler
    {
        //==================== 分类 ====================

        internal static KikasaArmsKind Classify(int itemType) {
            Item sample = SampleOf(itemType);
            if (sample == null) {
                return KikasaArmsKind.None;
            }
            //鱼竿先判：无伤害钓具，后面的战斗判定全会漏掉它
            if (IsRod(sample)) {
                return KikasaArmsKind.Rod;
            }
            //鞭先判：鞭也是 Swing 挥舞，但 noMelee=true 不会撞刀剑判定，先后只为语义清晰
            if (IsWhip(sample)) {
                return KikasaArmsKind.Whip;
            }
            //近战投射三族：特征互斥（Sets.Yoyo / aiStyle 3 / aiStyle 15），排在通用远程判定前
            if (IsYoyo(sample)) {
                return KikasaArmsKind.Yoyo;
            }
            if (IsBoomerang(sample)) {
                return KikasaArmsKind.Boomerang;
            }
            if (IsFlail(sample)) {
                return KikasaArmsKind.Flail;
            }
            if (IsGun(sample)) {
                return KikasaArmsKind.Gun;
            }
            if (IsBow(sample)) {
                return KikasaArmsKind.Bow;
            }
            if (IsThrown(sample)) {
                return KikasaArmsKind.Thrown;
            }
            if (IsSpear(sample)) {
                return KikasaArmsKind.Spear;
            }
            if (IsBlade(sample)) {
                return KikasaArmsKind.Blade;
            }
            return KikasaArmsKind.None;
        }

        private static Item SampleOf(int itemType) {
            if (itemType <= ItemID.None || itemType >= ItemLoader.ItemCount) {
                return null;
            }
            return ContentSamples.ItemsByType.TryGetValue(itemType, out Item item)
                && item?.IsAir == false ? item : null;
        }

        /// <summary>
        /// 枪：远程且吃"从管中射出"式弹药（镖族把吹管也带进来，视作水凝射管接受）。
        /// 火箭/凝胶/星/雪球/沙/硬币这些特殊弹药炮同样举炮出弹，
        /// 演出与枪奴同款成立，个性化由档案的音效/贴图/节奏承担
        /// </summary>
        private static bool IsGun(Item item)
            => item.damage > 0
            && item.DamageType.CountsAsClass<RangedDamageClass>()
            && (item.useAmmo == AmmoID.Bullet || item.useAmmo == AmmoID.Dart
                || item.useAmmo == AmmoID.Rocket || item.useAmmo == AmmoID.Gel
                || item.useAmmo == AmmoID.FallenStar || item.useAmmo == AmmoID.Snowball
                || item.useAmmo == AmmoID.Sand || item.useAmmo == AmmoID.Coin);

        /// <summary>刀剑：挥舞型近战且非工具（镐/斧/锤、回旋镖、矛、鞭都被条件自然排除）</summary>
        private static bool IsBlade(Item item)
            => item.damage > 0
            && item.DamageType.CountsAsClass<MeleeDamageClass>()
            && item.useStyle == ItemUseStyleID.Swing
            && !item.noMelee
            && item.pick == 0 && item.axe == 0 && item.hammer == 0;

        /// <summary>鞭子：发射物登记在 IsAWhip 集合（原版与规范模组鞭都走这面旗）</summary>
        private static bool IsWhip(Item item)
            => item.damage > 0
            && item.shoot > ProjectileID.None && item.shoot < ProjectileLoader.ProjectileCount
            && ProjectileID.Sets.IsAWhip[item.shoot];

        /// <summary>弓弩：远程且吃箭弹药</summary>
        private static bool IsBow(Item item)
            => item.damage > 0
            && item.DamageType.CountsAsClass<RangedDamageClass>()
            && item.useAmmo == AmmoID.Arrow;

        /// <summary>鱼竿：钓具旗即身份（伤害为 0，不与任何战斗判定相争）</summary>
        private static bool IsRod(Item item)
            => item.fishingPole > 0;

        /// <summary>
        /// 长矛：近战且发射物是原版矛 AI（模组规范矛同 aiStyle）。
        /// 短剑（Rapier 持出突刺）演出与矛同款并入此族，短突程由量尺自然折出
        /// </summary>
        private static bool IsSpear(Item item) {
            if (item.damage <= 0 || !item.DamageType.CountsAsClass<MeleeDamageClass>()) {
                return false;
            }
            if (item.useStyle == ItemUseStyleID.Rapier) {
                return true;
            }
            return item.shoot > ProjectileID.None && item.shoot < ProjectileLoader.ProjectileCount
                && ContentSamples.ProjectilesByType.TryGetValue(item.shoot, out Projectile proj)
                && proj?.aiStyle == ProjAIStyleID.Spear;
        }

        /// <summary>回旋镖：非消耗且发射物走原版回旋镖 AI；伤害类型不限（近战镖与模组盗贼镖同收）</summary>
        private static bool IsBoomerang(Item item)
            => item.damage > 0
            && !item.consumable
            && item.shoot > ProjectileID.None && item.shoot < ProjectileLoader.ProjectileCount
            && ContentSamples.ProjectilesByType.TryGetValue(item.shoot, out Projectile proj)
            && proj?.aiStyle == ProjAIStyleID.Boomerang;

        /// <summary>悠悠球：Sets.Yoyo 旗即身份（原版悬浮机制的必要登记，模组球同旗）</summary>
        private static bool IsYoyo(Item item)
            => item.damage > 0 && ItemID.Sets.Yoyo[item.type];

        /// <summary>连枷：发射物走原版连枷 AI（甩绕/掷出/回收三态一体的锤链）</summary>
        private static bool IsFlail(Item item)
            => item.damage > 0
            && item.shoot > ProjectileID.None && item.shoot < ProjectileLoader.ProjectileCount
            && ContentSamples.ProjectilesByType.TryGetValue(item.shoot, out Projectile proj)
            && proj?.aiStyle == ProjAIStyleID.Flail;

        /// <summary>
        /// 投掷消耗品：消耗品自带弹幕且不是弹药本身
        /// （ammo==None 是承重墙——箭/子弹也是带弹幕的远程消耗品）。
        /// 伤害类型认远程或投掷系：灾厄盗贼类只继承 Throwing 而 Throwing 不算 Ranged，
        /// 盗贼消耗投掷由后半边收进来；转发安全性同样由消耗品结构背书
        /// </summary>
        private static bool IsThrown(Item item)
            => item.damage > 0
            && item.consumable
            && item.ammo == AmmoID.None
            && (item.DamageType.CountsAsClass<RangedDamageClass>()
                || item.DamageType.CountsAsClass<ThrowingDamageClass>())
            && item.shoot > ProjectileID.None && item.shoot < ProjectileLoader.ProjectileCount;

        //==================== 伤害自平衡 ====================

        /// <summary>DPS 幂曲线指数：标定为巨兽鲨自然落在 ≈2.4×（手调锚点复现）</summary>
        private const float DamageCurveExp = 0.61f;

        /// <summary>速射档基准开火周期（迷你鲨现值），节奏折算的分母</summary>
        private const float BaseFirePeriod = 15f;

        private static float DpsOf(Item item)
            => item.damage * 60f / Math.Max(item.useAnimation, 1);

        /// <summary>武器 DPS 相对迷你鲨的幂折算：越强越钝增，模组神器被上限钳住</summary>
        private static float DamageCurve(Item item) {
            float anchor = 51f; //迷你鲨 DPS 兜底值，正常路径被实时读数覆盖
            if (ContentSamples.ItemsByType.TryGetValue(ItemID.Minishark, out Item mini)
                && mini?.IsAir == false) {
                anchor = Math.Max(DpsOf(mini), 1f);
            }
            return Math.Clamp(MathF.Pow(DpsOf(item) / anchor, DamageCurveExp), 0.35f, 6f);
        }

        //==================== 枪档案 ====================

        /// <summary>原版霰弹枪族：喷散射击无法从数值上辨认，点名登记</summary>
        private static bool IsShotgunId(int type)
            => type is ItemID.Boomstick or ItemID.Shotgun or ItemID.TacticalShotgun
            or ItemID.OnyxBlaster or ItemID.QuadBarrelShotgun or ItemID.Xenopopper;

        private static KikasaGunArchetype ArchetypeOf(Item item) {
            if (IsShotgunId(item.type)) {
                return KikasaGunArchetype.Shotgun;
            }
            if (item.useAnimation <= 10) {
                return KikasaGunArchetype.Auto;
            }
            if (item.useAnimation >= 32) {
                return KikasaGunArchetype.Sniper;
            }
            return KikasaGunArchetype.Standard;
        }

        internal static KikasaGunProfile GunProfileOf(int itemType) {
            Item sample = SampleOf(itemType);
            SoundStyle sound = sample?.UseSound ?? SoundID.Item11;

            //手调覆写优先：迷你鲨/巨兽鲨保持演进前的手调数值，行为零回归
            if (itemType == ItemID.Minishark) {
                return new(KikasaGunArchetype.Auto, 15, 3, 1f, 16.5f, 1, 5, 1f, sound, 26f, 1f);
            }
            if (itemType == ItemID.Megashark) {
                return new(KikasaGunArchetype.Auto, 15, 3, 2.4f, 16.5f, 1, 5, 1f, sound, 34f, 1f);
            }
            if (sample == null || !IsGun(sample)) {
                //异常兜底：按迷你鲨速射档出场，别让坏数据打断出水
                return new(KikasaGunArchetype.Auto, 15, 3, 1f, 16.5f, 1, 5, 1f, sound, 26f, 1f);
            }

            KikasaGunArchetype arch = ArchetypeOf(sample);
            float baseMul = DamageCurve(sample);
            float speed = 16.5f * Math.Clamp(sample.shootSpeed / 7f, 0.75f, 1.5f);
            (float muzzleLen, float drawScale) = MeasureGun(sample);

            //节奏折算：单发倍率乘 周期/基准，让编队总输出跟随武器 DPS 曲线而非开火密度
            switch (arch) {
                case KikasaGunArchetype.Auto: {
                    int period = Math.Clamp((int)(sample.useAnimation * 2.15f), 8, 18);
                    float mul = Math.Clamp(baseMul * period / BaseFirePeriod, 0.3f, 8f);
                    float recoil = Math.Clamp(0.75f + mul * 0.18f, 0.75f, 2f);
                    return new(arch, period, 3, mul, speed, 1, 5, recoil, sound, muzzleLen, drawScale);
                }
                case KikasaGunArchetype.Standard: {
                    int period = Math.Clamp((int)(sample.useAnimation * 1.35f), 19, 30);
                    float mul = Math.Clamp(baseMul * period / BaseFirePeriod, 0.5f, 9f);
                    float recoil = Math.Clamp(1f + mul * 0.22f, 1.1f, 2.4f);
                    return new(arch, period, 4, mul, speed, 1, 5, recoil, sound, muzzleLen, drawScale);
                }
                case KikasaGunArchetype.Sniper: {
                    //FirePeriod 供第二式慢重齐射用；点名狙杀另有自己的轮值时间线
                    const int period = 30;
                    float mul = Math.Clamp(baseMul * period / BaseFirePeriod, 1.2f, 10f);
                    return new(arch, period, 5, mul, speed, 1, 3, 2.4f, sound, muzzleLen, drawScale);
                }
                default: {
                    //霰弹：单发字段供环猎独弹用，拢射墙按 Pellets 拆粒
                    const int period = 26;
                    float mul = Math.Clamp(baseMul * period / BaseFirePeriod, 0.5f, 7f);
                    return new(arch, period, 4, mul, speed, 4, 4, 2f, sound, muzzleLen, drawScale);
                }
            }
        }

        //==================== 弓档案 ====================

        internal static KikasaBowProfile BowProfileOf(int itemType) {
            Item sample = SampleOf(itemType);
            SoundStyle sound = sample?.UseSound ?? SoundID.Item5;
            if (sample == null || !IsBow(sample)) {
                //异常兜底：按制式弓出场
                return new(KikasaBowArchetype.Standard, 26, 4, 1.5f, 15.5f, 4, sound, 40f, 1f);
            }

            KikasaBowArchetype arch =
                sample.useAnimation <= 17 ? KikasaBowArchetype.Rapid
                : sample.useAnimation >= 30 ? KikasaBowArchetype.Longbow
                : KikasaBowArchetype.Standard;

            float baseMul = DamageCurve(sample);
            float speed = 15.5f * Math.Clamp(sample.shootSpeed / 9f, 0.8f, 1.4f);
            (float bowSpan, float drawScale) = MeasureBow(sample);

            switch (arch) {
                case KikasaBowArchetype.Rapid: {
                    int period = Math.Clamp((int)(sample.useAnimation * 1.5f), 12, 22);
                    float mul = Math.Clamp(baseMul * period / BaseFirePeriod, 0.4f, 8f);
                    return new(arch, period, 3, mul, speed, 4, sound, bowSpan, drawScale);
                }
                case KikasaBowArchetype.Longbow: {
                    //DrawPeriod 供排箭用；贯穿重箭另有自己的轮值时间线
                    const int period = 34;
                    float mul = Math.Clamp(baseMul * period / BaseFirePeriod, 1f, 10f);
                    return new(arch, period, 5, mul, speed * 1.1f, 3, sound, bowSpan, drawScale);
                }
                default: {
                    int period = Math.Clamp((int)(sample.useAnimation * 1.3f), 20, 32);
                    float mul = Math.Clamp(baseMul * period / BaseFirePeriod, 0.5f, 9f);
                    return new(arch, period, 4, mul, speed, 4, sound, bowSpan, drawScale);
                }
            }
        }

        /// <summary>弓身量尺：弓贴图竖长，量高折算；只喂绘制与出弦点</summary>
        private static (float bowSpan, float drawScale) MeasureBow(Item sample) {
            float height = Math.Max(sample.height, 24f);
            if (!Main.dedServ) {
                Main.instance.LoadItem(sample.type);
                Texture2D tex = TextureAssets.Item[sample.type]?.Value;
                if (tex != null) {
                    height = Math.Max(tex.Height, 24f);
                }
            }
            float drawScale = Math.Clamp(46f / height, 0.8f, 1.35f);
            return (height * drawScale, drawScale);
        }

        //==================== 钓档案 ====================

        internal static KikasaRodProfile RodProfileOf(int itemType) {
            Item sample = SampleOf(itemType);
            SoundStyle sound = sample?.UseSound ?? SoundID.Item1;
            int power = Math.Max(sample?.fishingPole ?? 0, 1);
            //渔获基伤与收竿节拍全押渔力：木竿 5 力≈开荒配枪，金竿 50 力≈困难前中期
            int catchDamage = 12 + power * 2;
            int period = Math.Clamp(66 - power / 2, 34, 66);
            (float rodLen, float drawScale) = MeasureDiag(sample, 52f, 0.7f, 1.4f);
            return new(power, period, catchDamage, sound, rodLen, drawScale);
        }

        //==================== 矛档案 ====================

        internal static KikasaSpearProfile SpearProfileOf(int itemType) {
            Item sample = SampleOf(itemType);
            SoundStyle sound = sample?.UseSound ?? SoundID.Item1;
            if (sample == null || !IsSpear(sample)) {
                return new(30, 1.4f, 3, sound, 78f, 1f);
            }
            int period = Math.Clamp((int)(sample.useAnimation * 1.6f), 26, 56);
            float mul = Math.Clamp(DamageCurve(sample) * period / BaseFirePeriod * 0.95f, 0.6f, 12f);
            (float reach, float drawScale) = MeasureDiag(sample, 84f, 0.65f, 1.5f);
            return new(period, mul, 3, sound, reach, drawScale);
        }

        //==================== 掷档案 ====================

        internal static KikasaThrowProfile ThrowProfileOf(int itemType) {
            Item sample = SampleOf(itemType);
            SoundStyle sound = sample?.UseSound ?? SoundID.Item1;
            if (sample == null || !IsThrown(sample)) {
                return new(ProjectileID.Shuriken, 30, 5, 1f, 10f, 3, sound, 1.1f);
            }
            int period = Math.Clamp((int)(sample.useAnimation * 1.8f), 22, 50);
            float mul = Math.Clamp(DamageCurve(sample) * period / BaseFirePeriod, 0.4f, 8f);
            float speed = Math.Max(sample.shootSpeed, 8f);
            (_, float drawScale) = MeasureDiag(sample, 30f, 0.9f, 1.7f);
            return new(sample.shoot, period, 5, mul, speed, 3, sound, drawScale);
        }

        /// <summary>通用对角线量尺：目标长度→缩放，客户端量贴图、服务器回退 item 宽高</summary>
        private static (float len, float drawScale) MeasureDiag(Item sample, float targetLen, float minScale, float maxScale) {
            float w = Math.Max(sample?.width ?? 24, 16f);
            float h = Math.Max(sample?.height ?? 24, 16f);
            float diag = MathF.Sqrt(w * w + h * h);
            if (!Main.dedServ && sample != null) {
                Main.instance.LoadItem(sample.type);
                Texture2D tex = TextureAssets.Item[sample.type]?.Value;
                if (tex != null) {
                    diag = MathF.Sqrt(tex.Width * tex.Width + tex.Height * tex.Height);
                }
            }
            diag = Math.Max(diag, 20f);
            float drawScale = Math.Clamp(targetLen / diag, minScale, maxScale);
            return (diag * drawScale, drawScale);
        }

        /// <summary>枪身量尺：客户端量贴图宽、服务器回退 item.width：两处都只喂绘制与出膛点</summary>
        private static (float muzzleLen, float drawScale) MeasureGun(Item sample) {
            float width = Math.Max(sample.width, 20f);
            if (!Main.dedServ) {
                Main.instance.LoadItem(sample.type);
                Texture2D tex = TextureAssets.Item[sample.type]?.Value;
                if (tex != null) {
                    width = Math.Max(tex.Width, 20f);
                }
            }
            float drawScale = Math.Clamp(46f / width, 0.72f, 1.3f);
            return (width * drawScale * 0.55f + 4f, drawScale);
        }

        //==================== 刀档案 ====================

        internal static KikasaBladeProfile BladeProfileOf(int itemType) {
            Item sample = SampleOf(itemType);
            SoundStyle sound = sample?.UseSound ?? SoundID.Item1;
            if (sample == null || !IsBlade(sample)) {
                return new(KikasaBladeWeight.Light, 26, 1.5f, 4, sound, false, 60f, 1f);
            }

            //轻重档只用服务器可用的数值：宽高≈贴图规格，挥舞周期是分量的另一半证词
            int span = Math.Max(sample.width, sample.height);
            KikasaBladeWeight weight =
                span >= 58 || sample.useAnimation >= 40 ? KikasaBladeWeight.Grand
                : span >= 42 || sample.useAnimation >= 28 ? KikasaBladeWeight.Heavy
                : KikasaBladeWeight.Light;

            int relay = Math.Clamp((int)(sample.useAnimation * 1.5f), 24, 60);
            float mul = Math.Clamp(DamageCurve(sample) * relay / BaseFirePeriod * 0.9f, 0.6f, 14f);
            int maxUnits = weight switch {
                KikasaBladeWeight.Grand => 2,
                KikasaBladeWeight.Heavy => 3,
                _ => 4,
            };
            (float bladeLen, float drawScale) = MeasureBlade(sample, weight);
            return new(weight, relay, mul, maxUnits, sound, sample.shoot > ProjectileID.None,
                bladeLen, drawScale);
        }

        //==================== 鞭档案 ====================

        /// <summary>
        /// 原版鞭标签 buff 映射：逐条抄自 Projectile.StatusNPC 的鞭弹幕分支
        /// （841 皮鞭/952 脊柱骨鞭/847 杜兰达尔/849 暗黑收割/913 鞭炮/912 酷鞭/
        /// 914 荆棘鞭另带 1/5 概率中毒（由鞭体弹幕自行处理）/848 晨星/915 万花筒）；
        /// 模组鞭的标签逻辑活在它们自己的弹幕里无从复刻，返回空表
        /// </summary>
        private static readonly Dictionary<int, int[]> whipTagBuffs = new() {
            [841] = [307],
            [952] = [326],
            [847] = [309],
            [849] = [310],
            [913] = [313, 323],
            [912] = [340, 324],
            [914] = [315],
            [848] = [319],
            [915] = [316],
        };

        private static readonly int[] whipTagNone = [];

        /// <summary>该鞭弹幕命中时要挂的标签 buff（240 帧，与原版一致）；无则空表</summary>
        internal static int[] WhipTagBuffsOf(int whipProjType)
            => whipTagBuffs.TryGetValue(whipProjType, out int[] buffs) ? buffs : whipTagNone;

        internal static KikasaWhipProfile WhipProfileOf(int itemType) {
            Item sample = SampleOf(itemType);
            SoundStyle sound = sample?.UseSound ?? SoundID.Item152;
            if (sample == null || !IsWhip(sample)) {
                //异常兜底：按皮鞭规格出场，别让坏数据打断出水
                return new(ProjectileID.BlandWhip, 20, 0.75f, 30, 30, 64, 1f, 4f, 120f, 3, sound, 1f);
            }

            //段数/射程倍率读鞭弹幕模板的 WhipSettings（模组鞭在自己 SetDefaults 里定，一样能读到）
            int whipProj = sample.shoot;
            int segments = 20;
            float rangeMul = 1f;
            if (ContentSamples.ProjectilesByType.TryGetValue(whipProj, out Projectile projSample)
                && projSample != null) {
                segments = Math.Clamp(projSample.WhipSettings.Segments, 6, 80);
                rangeMul = Math.Clamp(projSample.WhipSettings.RangeMultiplier, 0.3f, 4f);
            }

            int useAnim = Math.Max(sample.useAnimation, 12);
            //extraUpdates=0 的复制鞭体：LashTime 帧数与原版 itemAnimationMax×MaxUpdates 的实际时长等价
            int lashTime = Math.Clamp(useAnim, 18, 50);
            int lashPeriod = Math.Clamp(useAnim * 2, 44, 96);
            float mul = Math.Clamp(DamageCurve(sample) * lashPeriod / BaseFirePeriod, 0.8f, 9f);
            float speed = Math.Max(sample.shootSpeed, 2f);
            //鞭尖峰值探出：总长 = 弹速 × (useAnimation×2×num) × num5 × 倍率，num×num5 峰值 = 2/3
            float peakReach = speed * useAnim * (4f / 3f) * rangeMul;

            float width = Math.Max(sample.width, 20f);
            if (!Main.dedServ) {
                Main.instance.LoadItem(sample.type);
                Texture2D tex = TextureAssets.Item[sample.type]?.Value;
                if (tex != null) {
                    width = Math.Max(tex.Width, 20f);
                }
            }
            float drawScale = Math.Clamp(36f / width, 0.8f, 1.25f);
            return new(whipProj, segments, rangeMul, useAnim, lashTime, lashPeriod, mul,
                speed, peakReach, 3, sound, drawScale);
        }

        //==================== 镖档案 ====================

        internal static KikasaBoomerangProfile BoomerangProfileOf(int itemType) {
            Item sample = SampleOf(itemType);
            SoundStyle sound = sample?.UseSound ?? SoundID.Item1;
            if (sample == null || !IsBoomerang(sample)) {
                //异常兜底：按附魔回旋镖规格出场，别让坏数据打断出水
                return new(30, 1.2f, 12f, 260f, 3, sound, 1.1f);
            }
            int period = Math.Clamp((int)(sample.useAnimation * 1.6f), 24, 54);
            float mul = Math.Clamp(DamageCurve(sample) * period / BaseFirePeriod, 0.5f, 9f);
            float speed = Math.Clamp(sample.shootSpeed, 9f, 16f) * 1.1f;
            //去程射程押弹速：原版镖弹速 9~13 对应中近距回旋圈
            float range = Math.Clamp(sample.shootSpeed * 26f, 190f, 430f);
            (_, float drawScale) = MeasureDiag(sample, 34f, 0.8f, 1.6f);
            return new(period, mul, speed, range, 3, sound, drawScale);
        }

        //==================== 球档案 ====================

        internal static KikasaYoyoProfile YoyoProfileOf(int itemType) {
            Item sample = SampleOf(itemType);
            SoundStyle sound = sample?.UseSound ?? SoundID.Item1;
            if (sample == null || !IsYoyo(sample)) {
                //异常兜底：按木悠悠球规格出场
                return new(112, 0.4f, 12f, 240f, 78, 3, sound, 1.2f);
            }
            //三件套按悠悠球弹幕类型索引（原版契约），物品侧只有 Yoyo 身份旗
            int yoyoProj = sample.shoot > ProjectileID.None && sample.shoot < ProjectileLoader.ProjectileCount
                ? sample.shoot : ProjectileID.None;
            float topSpeed = yoyoProj > 0 ? ProjectileID.Sets.YoyosTopSpeed[yoyoProj] : 0f;
            if (topSpeed <= 0f) {
                topSpeed = 12f;
            }
            float reach = yoyoProj > 0 ? ProjectileID.Sets.YoyosMaximumRange[yoyoProj] : 0f;
            if (reach <= 0f) {
                reach = 240f;
            }
            //寿命倍率是"能悬多久"的官方证词：秒数折驻留帧，-1 无限档给满
            float life = yoyoProj > 0 ? ProjectileID.Sets.YoyosLifeTimeMultiplier[yoyoProj] : 8f;
            int dwell = life < 0f ? 132 : (int)Math.Clamp(60f + life * 7f, 72f, 132f);
            int period = dwell + 40;
            //驻留期高频跳（约 6 跳/秒），单跳倍率整体压一档让总 DPS 跟曲线走
            float mul = Math.Clamp(DamageCurve(sample) * 0.42f, 0.18f, 3.5f);
            (_, float drawScale) = MeasureDiag(sample, 26f, 0.9f, 1.6f);
            return new(period, mul, Math.Clamp(topSpeed, 9f, 17.5f),
                Math.Clamp(reach, 160f, 400f), dwell, 3, sound, drawScale);
        }

        //==================== 锤档案 ====================

        internal static KikasaFlailProfile FlailProfileOf(int itemType) {
            Item sample = SampleOf(itemType);
            SoundStyle sound = sample?.UseSound ?? SoundID.Item1;
            if (sample == null || !IsFlail(sample)) {
                //异常兜底：按痛苦之球规格出场
                return new(ProjectileID.BallOHurt, 44, 2f, 13f, 300f, 3, sound, 1f);
            }
            int period = Math.Clamp((int)(sample.useAnimation * 1.2f), 38, 64);
            float mul = Math.Clamp(DamageCurve(sample) * period / BaseFirePeriod, 0.8f, 12f);
            float speed = Math.Clamp(sample.shootSpeed + 2f, 11f, 18f);
            float reach = Math.Clamp(sample.shootSpeed * 24f, 240f, 420f);
            return new(sample.shoot, period, mul, speed, reach, 3, sound,
                MeasureFlailHead(sample.shoot));
        }

        /// <summary>锤头量尺：量原连枷弹幕贴图（客户端），服务器回退默认；只喂绘制</summary>
        private static float MeasureFlailHead(int projType) {
            float size = 26f;
            if (!Main.dedServ && projType > ProjectileID.None && projType < ProjectileLoader.ProjectileCount) {
                Main.instance.LoadProjectile(projType);
                Texture2D tex = TextureAssets.Projectile[projType]?.Value;
                if (tex != null) {
                    size = Math.Max(Math.Max(tex.Width, tex.Height), 16f);
                }
            }
            return Math.Clamp(30f / size, 0.75f, 1.4f);
        }

        /// <summary>刃身量尺：剑贴图是斜置画法，刃长按对角线折算；同样只喂绘制与斩痕规格</summary>
        private static (float bladeLen, float drawScale) MeasureBlade(Item sample, KikasaBladeWeight weight) {
            float diag = MathF.Sqrt(sample.width * sample.width + sample.height * sample.height);
            if (!Main.dedServ) {
                Main.instance.LoadItem(sample.type);
                Texture2D tex = TextureAssets.Item[sample.type]?.Value;
                if (tex != null) {
                    diag = MathF.Sqrt(tex.Width * tex.Width + tex.Height * tex.Height);
                }
            }
            diag = Math.Max(diag, 30f);
            float targetLen = weight switch {
                KikasaBladeWeight.Grand => 94f,
                KikasaBladeWeight.Heavy => 76f,
                _ => 60f,
            };
            float drawScale = Math.Clamp(targetLen / diag, 0.65f, 1.5f);
            return (diag * drawScale, drawScale);
        }
    }
}
