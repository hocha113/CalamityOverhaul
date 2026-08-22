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
        SoundStyle FireSound,  //开火音：直接借原武器的 UseSound——个性化白送
        float MuzzleLen,       //枪口探出距离
        float DrawScale);      //绘制缩放（超大贴图收一号、小贴图放一号）

    /// <summary>刀奴档案：同枪奴契约——行为字段服务器安全，贴图字段只管绘制</summary>
    internal readonly record struct KikasaBladeProfile(
        KikasaBladeWeight Weight,
        int RelayPeriod,       //轮转突斩的接力节拍基准
        float SlashDamageMul,  //单斩伤害倍率（已含节奏折算）
        int MaxUnits,          //刃数上限
        SoundStyle SwingSound, //挥砍音：借原武器 UseSound
        bool HasWave,          //原武器自带剑气（item.shoot），预留的差异化轴
        float BladeLen,        //刃长（对角线口径），斩痕规格与判定宽度的基准
        float DrawScale);      //绘制缩放

    /// <summary>
    /// 鞭奴档案：几何与时序全部对齐原版 AI_165 契约——
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

    /// <summary>
    /// 械奴档案推断器：从沉湖物品的基础数值推族别与个性化档案。
    /// 全部读 ContentSamples 模板（不读玩家实例——词缀不参与，远端与服务器无湖藏数据）；
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
            //鞭先判：鞭也是 Swing 挥舞，但 noMelee=true 不会撞刀剑判定，先后只为语义清晰
            if (IsWhip(sample)) {
                return KikasaArmsKind.Whip;
            }
            if (IsGun(sample)) {
                return KikasaArmsKind.Gun;
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

        /// <summary>枪：远程且吃子弹/镖弹药（镖族把吹管也带进来，视作水凝射管接受）</summary>
        private static bool IsGun(Item item)
            => item.damage > 0
            && item.DamageType.CountsAsClass<RangedDamageClass>()
            && (item.useAmmo == AmmoID.Bullet || item.useAmmo == AmmoID.Dart);

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

        /// <summary>枪身量尺：客户端量贴图宽、服务器回退 item.width——两处都只喂绘制与出膛点</summary>
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
