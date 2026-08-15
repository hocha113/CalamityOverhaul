using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using CalamityOverhaul.OtherMods.Wikithis;
using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend
{
    internal class SHPCOverride : ItemOverride, ILocalizedModType
    {
        public override string LocalizationCategory => "Legend";
        /// <summary>目标 ID，SHPCItem</summary>
        public static int ID => ModContent.ItemType<SHPCItem>();
        /// <summary>各时期伤害表，用 GetOnDamage 访问</summary>
        private static Dictionary<int, int> DamageDictionary = new Dictionary<int, int>();
        /// <summary>开局伤害</summary>
        public static int GetStartDamage => DamageDictionary[0];
        /// <summary>左键连发间隔帧</summary>
        private const int LeftClickUseTime = 20;
        /// <summary>左键光束数</summary>
        private const int BeamCount = 3;
        /// <summary>左键散射弧度</summary>
        public static float BeamSpreadAngle => 0.08f;
        /// <summary>武器缩放</summary>
        public static float ItemScale => 0.8f;
        public override int TargetID => ID;

        /// <summary>成长等级</summary>
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

        /// <summary>时期对应伤害</summary>
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
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
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
            WikithisRef.TryAppendWikiTooltip(item, tooltips);
            return false;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) => SetTooltip(item, ref tooltips);

        /// <summary>允许右键蓄力</summary>
        public override bool? On_AltFunctionUse(Item item, Player player) => true;

        /// <summary>CanUseItem，无灵魂弹，右键禁重复蓄力球</summary>
        public override bool? On_CanUseItem(Item item, Player player) {
            ShootContext ctx = SHPCModificationSystem.Resolve(player);
            if (player.altFunctionUse == 2) {
                //右键蓄力，channel+noUseGraphic，场上无同类球
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
                    //激光通道，每useTime耗蓝
                    item.channel = true;
                    item.useAnimation = item.useTime = 8;
                    return player.statMana > 0;
                }
                //左键，攻速缩放useTime
                item.channel = false;
                int scaled = (int)(LeftClickUseTime / MathF.Max(ctx.AttackSpeedMul, 0.1f));
                if (scaled < 1) scaled = 1;
                item.useAnimation = item.useTime = scaled;
                return true;
            }
        }

        /// <summary>UseItem，阻止灵魂消耗</summary>
        public override bool? On_UseItem(Item item, Player player) => true;

        /// <summary>右键耗蓝由蓄力弹幕管理</summary>
        public override void ModifyManaCost(Item item, Player player, ref float reduce, ref float mult) {
            if (player.altFunctionUse == 2) {
                mult = 0f;
                reduce = 0f;
                return;
            }
            ShootContext ctx = SHPCModificationSystem.Resolve(player);
            mult *= ctx.ManaCostMul;
            //ManaFree强制免蓝，不被ManaCostMul加算抵消
            if (ctx.ManaFree) {
                mult = 0f;
                reduce = 0f;
            }
        }

        /// <summary>On_Shoot，左键光束/激光，右键蓄力球</summary>
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            ShootContext ctx = SHPCModificationSystem.Resolve(player);
            if (player.altFunctionUse == 2) {
                //右键先生成手持弹幕
                int heldIdx = Projectile.NewProjectile(source, player.Center, Vector2.Zero,
                    ModContent.ProjectileType<SHPCChargeHeldProj>(),
                    0, 0f, player.whoAmI);

                //再生成蓄力球，ai1=手持索引，ai2=聚合攻速倍率
                Vector2 spawnPos = player.Center + velocity.SafeNormalize(Vector2.UnitX) * 70f;
                int orbDamage = (int)(damage * 2);
                int orbIdx = Projectile.NewProjectile(source, spawnPos, Vector2.Zero,
                    ModContent.ProjectileType<CyberChargeOrbProj>(),
                    orbDamage, knockback, player.whoAmI,
                    ai1: heldIdx, ai2: ctx.AttackSpeedMul);
                //localAI传蓄力时间与球速倍率
                if (orbIdx >= 0 && orbIdx < Main.maxProjectiles) {
                    Main.projectile[orbIdx].localAI[1] = ctx.ChargeTimeMul;
                    Main.projectile[orbIdx].localAI[2] = ctx.OrbSpeedMul;
                    //行为字段写入ModProjectile
                    if (Main.projectile[orbIdx].ModProjectile is CyberChargeOrbProj orb) {
                        orb.DrainAura = ctx.OrbDrainAura;
                        orb.ExplosionRadiusMul = ctx.OrbExplosionRadiusMul;
                        orb.DetonationMinions = ctx.OrbDetonationMinions;
                        orb.ExplosionPropels = ctx.OrbExplosionPropels;
                        orb.FlyingAttract = ctx.OrbFlyingAttract;
                        orb.ManaCostMul = ctx.ManaCostMul;
                    }
                }
            }
            else {
                if (ctx.LaserMode) {
                    //激光，无活跃束时生成一发自管生命周期
                    if (player.ownedProjectileCounts[ModContent.ProjectileType<CyberPrismLaserProj>()] <= 0) {
                        SoundEngine.PlaySound(SoundID.Item92, player.Center);
                        Vector2 laserDir = velocity.SafeNormalize(Vector2.UnitX);
                        Vector2 spawnPos = player.Center + laserDir * 60f;
                        int laserDamage = damage;
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
                //左键单发或散射
                SoundEngine.PlaySound(SoundID.Item92, player.Center);
                Vector2 baseVel = velocity.SafeNormalize(Vector2.UnitX) * 14f;
                Vector2 dir = velocity.UnitVector();
                position += new Vector2(dir.X * 20, -12);

                int beams = ctx.MergeBeams ? 1 : System.Math.Max(1, BeamCount + ctx.BeamCountAdd);
                float spreadAngle = BeamSpreadAngle * MathF.Max(ctx.SpreadMul, 0f);
                int finalDamage = (int)(damage * (ctx.MergeBeams ? ctx.MergedDamageBonus : 1f));
                if (finalDamage < 1) finalDamage = 1;

                for (int i = 0; i < beams; i++) {
                    float spreadOffset = beams > 1 ? (i - (beams - 1) / 2f) * spreadAngle : 0f;
                    float randomOffset = spreadAngle > 0f ? Main.rand.NextFloat(-0.03f, 0.03f) : 0f;
                    Vector2 shotVel = baseVel.RotatedBy(spreadOffset + randomOffset);

                    int beamIdx = Projectile.NewProjectile(source, position + shotVel.SafeNormalize(Vector2.UnitX) * 28f, shotVel,
                        ModContent.ProjectileType<CyberTraceBeamProj>(),
                        finalDamage, knockback, player.whoAmI,
                        ai0: Main.rand.Next(3));
                    //ai1传追踪倍率
                    if (beamIdx >= 0 && beamIdx < Main.maxProjectiles) {
                        Main.projectile[beamIdx].ai[1] = ctx.HomingMul;
                        //行为字段写入ModProjectile
                        if (Main.projectile[beamIdx].ModProjectile is CyberTraceBeamProj beam) {
                            beam.ExtraPierce = ctx.BeamExtraPierce;
                            beam.LifeMul = ctx.BeamLifeMul;
                            beam.SpeedMul = ctx.BeamSpeedMul;
                            beam.ExplodeOnHit = ctx.BeamExplodeOnHit;
                            beam.ExplodeRadius = ctx.BeamExplodeRadius;
                            beam.ChainCount = ctx.BeamChainCount;
                            beam.ChainRange = ctx.BeamChainRange;
                            beam.SplitOnDeath = ctx.BeamSplitOnDeath;
                            //新星枪管，爆炸伤按索引递减
                            if (ctx.BeamExplodeDecayPerBeam > 0f) {
                                beam.ExplodeDamageMul = MathF.Max(1f - ctx.BeamExplodeDecayPerBeam * i, 0.1f);
                            }
                        }
                    }
                }
            }

            return false; //拦原版射击
        }

        public static void SetDefaultsFunc(Item Item) {
            LoadWeaponData();
            Item.damage = GetStartDamage;
            Item.useAnimation = Item.useTime = LeftClickUseTime;
            Item.autoReuse = true;
            Item.mana = 8;
            Item.scale = ItemScale;
            Item.CWR().LegendData = new SHPCData();
        }

        public static bool SHPCDamage(Item Item, Player player, ref StatModifier damage) {
            VaultUtils.ApplyWeaponDamageScaling(Item, GetOnDamage(Item), GetStartDamage, ref damage);
            ShootContext ctx = SHPCModificationSystem.Resolve(player);
            damage *= ctx.DamageMul;
            return false;
        }

        public static void SetTooltip(Item item, ref List<TooltipLine> tooltips) {
            string keyDisplay = CWRKeySystem.QuestLog_Key?.GetAssignedKeys() is { Count: > 0 } k ? k[0] : CWRKeySystem.Notbound.Value;
            tooltips.ReplacePlaceholder("legend_Text", LegendUpgradeManagerSystem.QuestManagerHint.Value.Replace("{KEY}", keyDisplay), "");
            int index = item.CWR()?.LegendData?.TargetLevel ?? 0;
            string num = (index + 1).ToString();
            if (index == 22) {
                num = LegendUpgradeManagerSystem.TrialPassed.Value;
            }
            string text = LegendData.GetLevelTrialPreText(item.CWR(), LegendUpgradeManagerSystem.Text_Lang_0, num);
            tooltips.ReplacePlaceholder("[Lang4]", text, "");
        }
    }
}
