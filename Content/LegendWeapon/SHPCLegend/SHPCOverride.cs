using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.InWorldBossPhase;
using static InnoVault.GameSystem.ItemRebuildLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend
{
    internal class SHPCOverride : ItemOverride, ICWRLoader
    {
        /// <summary>
        /// 目标ID
        /// </summary>
        public static int ID => CWRID.Item_SHPC;
        /// <summary>
        /// 每个时期阶段对应的伤害，这个成员一般不需要直接访问，而是使用<see cref="GetOnDamage"/>
        /// </summary>
        private static Dictionary<int, int> DamageDictionary = new Dictionary<int, int>();
        /// <summary>
        /// 获取开局的伤害
        /// </summary>
        public static int GetStartDamage => DamageDictionary[0];
        /// <summary>
        /// 当前选中的魂魄类型，UI选择后会更新这个值
        /// </summary>
        public static int SelectedSoulType = ItemID.SoulofLight;
        /// <summary>
        /// 左键连发间隔帧数
        /// </summary>
        private const int LeftClickUseTime = 20;
        /// <summary>
        /// 左键每次发射的光束数量
        /// </summary>
        private const int BeamCount = 3;
        /// <summary>
        /// 左键散射角度（弧度）
        /// </summary>
        private const float BeamSpreadAngle = 0.08f;

        public override int TargetID => ID;

        #region 原版方法屏蔽

        private static void OnSHPCToolFunc(On_ModItem_ModifyTooltips_Delegate orig, object obj, List<TooltipLine> list) { }

        private static bool OnSHPCCanUseItemFunc(Func<object, Player, bool> orig, object self, Player player) => true;

        private static bool? OnSHPCUseItemFunc(Func<object, Player, bool?> orig, object self, Player player) => null;

        private static bool OnSHPCShootFunc(
            Func<object, Player, EntitySource_ItemUse_WithAmmo, Vector2, Vector2, int, int, float, bool> orig,
            object self, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) => false;

        private static float OnSHPCUseSpeedMultiplierFunc(Func<object, Player, float> orig, object self, Player player) => 1f;

        private delegate void OnSHPC_ModifyManaCost_Delegate(object self, Player player, ref float reduce, ref float mult);
        private static void OnSHPCModifyManaCostFunc(
            OnSHPC_ModifyManaCost_Delegate orig,
            object self, Player player, ref float reduce, ref float mult) { }

        private delegate void OnSHPC_PostDrawInInventory_Delegate(object self, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale);

        private static void OnPostDrawInInventoryFunc(OnSHPC_PostDrawInInventory_Delegate orig, object self, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) { }

        #endregion

        void ICWRLoader.LoadData() {
            var type = CWRRef.GetItem_SHPC_Type();
            if (type != null) {
                //屏蔽原版 ModifyTooltips
                MethodInfo methodInfo = type.GetMethod("ModifyTooltips", BindingFlags.Public | BindingFlags.Instance);
                if (methodInfo != null) {
                    VaultHook.Add(methodInfo, OnSHPCToolFunc);
                }
                //屏蔽原版 FindSoulForAmmo
                methodInfo = type.GetMethod("FindSoulForAmmo", BindingFlags.Public | BindingFlags.Static);
                if (methodInfo != null) {
                    VaultHook.Add(methodInfo, OnFindSoulForAmmoFunc);
                }
                //屏蔽原版 Shoot —— 阻止原始弹幕生成
                methodInfo = type.GetMethod("Shoot", BindingFlags.Public | BindingFlags.Instance);
                if (methodInfo != null) {
                    VaultHook.Add(methodInfo, OnSHPCShootFunc);
                }
                //屏蔽原版 CanUseItem —— 移除灵魂弹药检测
                methodInfo = type.GetMethod("CanUseItem", BindingFlags.Public | BindingFlags.Instance);
                if (methodInfo != null) {
                    VaultHook.Add(methodInfo, OnSHPCCanUseItemFunc);
                }
                //屏蔽原版 UseItem —— 移除灵魂消耗逻辑
                methodInfo = type.GetMethod("UseItem", BindingFlags.Public | BindingFlags.Instance);
                if (methodInfo != null) {
                    VaultHook.Add(methodInfo, OnSHPCUseItemFunc);
                }
                //屏蔽原版 UseSpeedMultiplier
                methodInfo = type.GetMethod("UseSpeedMultiplier", BindingFlags.Public | BindingFlags.Instance);
                if (methodInfo != null) {
                    VaultHook.Add(methodInfo, OnSHPCUseSpeedMultiplierFunc);
                }
                //屏蔽原版 ModifyManaCost
                methodInfo = type.GetMethod("ModifyManaCost", BindingFlags.Public | BindingFlags.Instance);
                if (methodInfo != null) {
                    VaultHook.Add(methodInfo, OnSHPCModifyManaCostFunc);
                }

                methodInfo = type.GetMethod("PostDrawInInventory", BindingFlags.Public | BindingFlags.Instance);
                if (methodInfo != null) {
                    VaultHook.Add(methodInfo, OnPostDrawInInventoryFunc);
                }
            }
        }

        private static int OnFindSoulForAmmoFunc(Func<Player, int> orig, Player player) {
            return SelectedSoulType;
        }

        /// <summary>
        /// 获得成长等级
        /// </summary>
        public static int GetLevel(Item item) {
            if (item.type != ID) {
                return 0;
            }
            CWRItem cwrItem = item.CWR();
            if (cwrItem == null) {
                return 0;
            }
            if (cwrItem.LegendData == null) {
                return 0;
            }

            return cwrItem.LegendData.Level;
        }

        /// <summary>
        /// 获取时期对应的伤害
        /// </summary>
        public static int GetOnDamage(Item item) => DamageDictionary[GetLevel(item)];

        public static void LoadWeaponData() {
            DamageDictionary = new Dictionary<int, int>(){
                {0, 9 },
                {1, 11 },
                {2, 13 },
                {3, 16 },
                {4, 20 },
                {5, 45 },
                {6, 47 },
                {7, 49 },
                {8, 51 },
                {9, 53 },
                {10, 56 },
                {11, 59 },
                {12, 70 },
                {13, 92 },
                {14, 117 },
                {15, 217 },
                {16, 274 },
                {17, 380 },
                {18, 600 },
                {19, 800 },
                {20, 900 },
                {21, 2077 },
                {22, 4096 },
            };
        }

        public override void SetStaticDefaults() {
            ItemID.Sets.ShimmerTransformToItem[TargetID] = CWRID.Item_PlasmaDriveCore;
            HackTimeAccess.Register(player => player.GetItem().type == SHPCOverride.ID, "SmartWeapon:SHPC");
        }

        public override void SetDefaults(Item item) => SetDefaultsFunc(item);

        public override bool On_ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) => SHPCDamage(item, player, ref damage);

        public override bool? On_ModifyWeaponCrit(Item item, Player player, ref float crit) {
            ShootContext ctx = SHPCModificationSystem.Resolve(player);
            if (ctx.CritAdd != 0) {
                crit += ctx.CritAdd;
            }
            return null;
        }

        public override bool? On_ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRItem.OverModifyTooltip(item, tooltips);
            SetTooltip(item, ref tooltips);
            return false;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) => SetTooltip(item, ref tooltips);

        /// <summary>
        /// 允许右键使用（蓄力能量球）
        /// </summary>
        public override bool? On_AltFunctionUse(Item item, Player player) => true;

        /// <summary>
        /// 拦截原版 CanUseItem，移除灵魂弹药需求
        /// <br/>右键时：场上不能有已存在的蓄力球
        /// <br/>左键时：正常使用
        /// </summary>/*  */
        public override bool? On_CanUseItem(Item item, Player player) {
            ShootContext ctx = SHPCModificationSystem.Resolve(player);
            if (player.altFunctionUse == 2) {
                //右键蓄力模式：channel + noUseGraphic，且场上没有同类蓄力弹幕
                item.channel = true;
                item.noUseGraphic = true;
                item.UseSound = null;
                item.useAnimation = item.useTime = 10;
                return player.ownedProjectileCounts[ModContent.ProjectileType<SHPCChargeHeldProj>()] <= 0;
            }
            else {
                item.noUseGraphic = false;
                item.UseSound = null;
                if (ctx.LaserMode) {
                    //激光模式：通道按住持续照射，每 useTime 帧消耗一次法力模拟持续消耗
                    item.channel = true;
                    item.useAnimation = item.useTime = 8;
                    return player.statMana > 0;
                }
                //左键射击模式，按改件攻速倍率缩放 useTime
                item.channel = false;
                int scaled = (int)(LeftClickUseTime / MathF.Max(ctx.AttackSpeedMul, 0.1f));
                if (scaled < 1) scaled = 1;
                item.useAnimation = item.useTime = scaled;
                return true;
            }
        }

        /// <summary>
        /// 拦截原版 UseItem，阻止灵魂消耗
        /// </summary>
        public override bool? On_UseItem(Item item, Player player) => true;

        /// <summary>
        /// 右键蓄力耗蓝由弹幕AI自行管理，触发帧本身不走原版扣蓝路径
        /// </summary>
        public override void ModifyManaCost(Item item, Player player, ref float reduce, ref float mult) {
            if (player.altFunctionUse == 2) {
                mult = 0f;
                reduce = 0f;
                return;
            }
            ShootContext ctx = SHPCModificationSystem.Resolve(player);
            mult *= ctx.ManaCostMul;
        }

        /// <summary>
        /// 拦截原版射击，实现自定义左右键弹幕
        /// <br/>左键：发射三发 CyberTraceBeamProj
        /// <br/>右键：发射一发 CyberChargeOrbProj（蓄力能量球）
        /// </summary>
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            ShootContext ctx = SHPCModificationSystem.Resolve(player);
            if (player.altFunctionUse == 2) {
                //右键：先生成手持弹幕（绘制武器 + 控制手臂动画）
                int heldIdx = Projectile.NewProjectile(source, player.Center, Vector2.Zero,
                    ModContent.ProjectileType<SHPCChargeHeldProj>(),
                    0, 0f, player.whoAmI);

                //再生成蓄力能量球，ai[1] 传递手持弹幕索引以定位枪口
                Vector2 spawnPos = player.Center + velocity.SafeNormalize(Vector2.UnitX) * 70f;
                int orbDamage = (int)(damage * 2 * ctx.DamageMul);
                int orbIdx = Projectile.NewProjectile(source, spawnPos, Vector2.Zero,
                    ModContent.ProjectileType<CyberChargeOrbProj>(),
                    orbDamage, knockback, player.whoAmI,
                    ai1: heldIdx);
                //通过 localAI 传递蓄力时间与飞行速度倍率，能量球首帧读取
                if (orbIdx >= 0 && orbIdx < Main.maxProjectiles) {
                    Main.projectile[orbIdx].localAI[1] = ctx.ChargeTimeMul;
                    Main.projectile[orbIdx].localAI[2] = ctx.OrbSpeedMul;
                    //行为字段直接写入到 ModProjectile 实例
                    if (Main.projectile[orbIdx].ModProjectile is CyberChargeOrbProj orb) {
                        orb.DrainAura = ctx.OrbDrainAura;
                        orb.ExplosionRadiusMul = ctx.OrbExplosionRadiusMul;
                        orb.DetonationMinions = ctx.OrbDetonationMinions;
                        orb.ExplosionPropels = ctx.OrbExplosionPropels;
                        orb.FlyingAttract = ctx.OrbFlyingAttract;
                        orb.ManaCostMul = ctx.ManaCostMul;
                        orb.AttackSpeedMul = ctx.AttackSpeedMul;
                    }
                }
            }
            else {
                if (ctx.LaserMode) {
                    //激光模式：仅在没有活跃激光时生成一束，后续由弹幕自管理生命周期
                    if (player.ownedProjectileCounts[ModContent.ProjectileType<CyberPrismLaserProj>()] <= 0) {
                        SoundEngine.PlaySound(SoundID.Item92, player.Center);
                        Vector2 laserDir = velocity.SafeNormalize(Vector2.UnitX);
                        Vector2 spawnPos = player.Center + laserDir * 60f;
                        int laserDamage = (int)(damage * ctx.DamageMul);
                        if (laserDamage < 1) laserDamage = 1;
                        int laserIdx = Projectile.NewProjectile(source, spawnPos, laserDir,
                            ModContent.ProjectileType<CyberPrismLaserProj>(),
                            laserDamage, knockback, player.whoAmI);
                        if (laserIdx >= 0 && laserIdx < Main.maxProjectiles
                            && Main.projectile[laserIdx].ModProjectile is CyberPrismLaserProj laserProj) {
                            laserProj.PulseInterval = ctx.LaserPulseInterval;
                            laserProj.PulseRadius = ctx.LaserPulseRadius;
                            laserProj.ScorchOnHit = ctx.LaserScorchOnHit;
                            laserProj.ScorchDuration = ctx.LaserScorchDuration;
                        }
                    }
                    return false;
                }
                //左键：根据改件决定单发或散射
                SoundEngine.PlaySound(SoundID.Item92, player.Center);
                Vector2 baseVel = velocity.SafeNormalize(Vector2.UnitX) * 14f;
                Vector2 dir = velocity.UnitVector();
                position += new Vector2(dir.X * 20, -12);

                int beams = ctx.MergeBeams ? 1 : System.Math.Max(1, BeamCount + ctx.BeamCountAdd);
                float spreadAngle = BeamSpreadAngle * MathF.Max(ctx.SpreadMul, 0f);
                int finalDamage = (int)(damage * ctx.DamageMul * (ctx.MergeBeams ? ctx.MergedDamageBonus : 1f));
                if (finalDamage < 1) finalDamage = 1;

                for (int i = 0; i < beams; i++) {
                    float spreadOffset = beams > 1 ? (i - (beams - 1) / 2f) * spreadAngle : 0f;
                    float randomOffset = spreadAngle > 0f ? Main.rand.NextFloat(-0.03f, 0.03f) : 0f;
                    Vector2 shotVel = baseVel.RotatedBy(spreadOffset + randomOffset);

                    int beamIdx = Projectile.NewProjectile(source, position + shotVel.SafeNormalize(Vector2.UnitX) * 28f, shotVel,
                        ModContent.ProjectileType<CyberTraceBeamProj>(),
                        finalDamage, knockback, player.whoAmI,
                        ai0: Main.rand.Next(3));
                    //ai[1] 传递追踪倍率，>0 时弹幕首帧应用
                    if (beamIdx >= 0 && beamIdx < Main.maxProjectiles) {
                        Main.projectile[beamIdx].ai[1] = ctx.HomingMul;
                        //行为字段直接写入到 ModProjectile 实例（首帧读取）
                        if (Main.projectile[beamIdx].ModProjectile is CyberTraceBeamProj beam) {
                            beam.ExtraPierce = ctx.BeamExtraPierce;
                            beam.LifeMul = ctx.BeamLifeMul;
                            beam.SpeedMul = ctx.BeamSpeedMul;
                            beam.ExplodeOnHit = ctx.BeamExplodeOnHit;
                            beam.ExplodeRadius = ctx.BeamExplodeRadius;
                            beam.ChainCount = ctx.BeamChainCount;
                            beam.ChainRange = ctx.BeamChainRange;
                            beam.SplitOnDeath = ctx.BeamSplitOnDeath;
                            //新星枪管特判：第i发弹幕的爆炸伤害按索引递减
                            if (ctx.BeamExplodeDecayPerBeam > 0f) {
                                beam.ExplodeDamageMul = MathF.Max(1f - ctx.BeamExplodeDecayPerBeam * i, 0.1f);
                            }
                        }
                    }
                }
            }

            return false; //阻止原版射击行为
        }

        public static void SetDefaultsFunc(Item Item) {
            LoadWeaponData();
            Item.damage = GetStartDamage;
            Item.useAnimation = Item.useTime = LeftClickUseTime;
            Item.autoReuse = true;
            Item.mana = 8;
            Item.CWR().LegendData = new SHPCData();
        }

        public static bool SHPCDamage(Item Item, Player player, ref StatModifier damage) {
            CWRUtils.ModifyLegendWeaponDamageFunc(Item, GetOnDamage(Item), GetStartDamage, ref damage);
            ShootContext ctx = SHPCModificationSystem.Resolve(player);
            damage *= ctx.DamageMul;
            return false;
        }

        public static void SetTooltip(Item item, ref List<TooltipLine> tooltips) {
            string keyDisplay = CWRKeySystem.QuestManager_Key?.GetAssignedKeys() is { Count: > 0 } k ? k[0] : CWRLocText.Instance.Notbound.Value;
            tooltips.ReplacePlaceholder("legend_Text", CWRLocText.GetTextValue("Legend_QuestManager_Hint").Replace("{KEY}", keyDisplay), "");
            int index = SHPC_Level();
            string num = (index + 1).ToString();
            if (index == 22) {
                num = CWRLocText.GetTextValue("Murasama_Text_Lang_End");
            }
            string text = LegendData.GetLevelTrialPreText(item.CWR(), "Murasama_Text_Lang_0", num);
            tooltips.ReplacePlaceholder("[Lang4]", text, "");
        }
    }
}
