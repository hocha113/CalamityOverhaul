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
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.InWorldBossPhase;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend
{
    internal class SHPCOverride : ItemOverride
    {
        /// <summary>
        /// 目标ID，指向本模组独立物品 <see cref="SHPCItem"/>
        /// </summary>
        public static int ID => ModContent.ItemType<SHPCItem>();
        /// <summary>
        /// 每个时期阶段对应的伤害，这个成员一般不需要直接访问，而是使用<see cref="GetOnDamage"/>
        /// </summary>
        private static Dictionary<int, int> DamageDictionary = new Dictionary<int, int>();
        /// <summary>
        /// 获取开局的伤害
        /// </summary>
        public static int GetStartDamage => DamageDictionary[0];
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
        public static float BeamSpreadAngle => 0.08f;
        /// <summary>武器大小缩放，适当的缩放可以提升观感</summary>
        public static float ItemScale => 0.8f;
        /// <summary>持握时武器中心距玩家的距离</summary>
        public static float HoldDistance => 0f * ItemScale;
        /// <summary>左键开火后坐力最大回退距离（像素）</summary>
        public static float RecoilMaxOffset => 8f * ItemScale;
        /// <summary>持握精灵的原点偏移</summary>
        public static Vector2 HoldOrigin => new Vector2(-56, 10) * ItemScale;
        /// <summary>后坐力发生的动画前段占比</summary>
        public static float RecoilPhase => 1f / 3f;

        public override int TargetID => ID;

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

        #region 持握动画
        /// <summary>
        /// 与 Terraria Overhaul 的持握样式冲突时，不接管动画
        /// </summary>
        private static bool DontModifyHeldStyle() => CWRMod.Instance.terrariaOverhaul != null;

        /// <summary>
        /// 左键使用时的持握样式：武器朝鼠标瞄准，并在开火瞬间产生后坐力回退。
        /// <br/>右键蓄力由 <see cref="SHPCChargeHeldProj"/> 接管手臂与绘制，这里直接跳过
        /// </summary>
        public override void UseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.altFunctionUse == 2 || DontModifyHeldStyle()) {
                return;
            }

            Vector2 mouseWorld = Main.MouseWorld;
            player.ChangeDir(Math.Sign((mouseWorld - player.Center).X));

            float itemRotation = player.compositeFrontArm.rotation + MathHelper.PiOver2 * player.gravDir;
            Vector2 itemPosition = player.GetPlayerStabilityCenter() + itemRotation.ToRotationVector2() * HoldDistance;

            if (!SHPCModificationSystem.Resolve(player).LaserMode) {
                //开火后坐力：动画前段沿瞄准方向快速回退，随后归位
                float progress = GetAnimationProgress(player);
                if (progress < RecoilPhase) {
                    float kick = (RecoilPhase - progress) / RecoilPhase * RecoilMaxOffset;
                    itemPosition -= (mouseWorld - player.Center).SafeNormalize(Vector2.UnitX) * kick;
                }
            }

            ApplyHoldingStyle(player, itemRotation, itemPosition,
                item.Size, HoldOrigin);
        }

        /// <summary>
        /// 左键使用时的手臂动画：复合前臂跟随鼠标方向
        /// </summary>
        public override void UseItemFrame(Item item, Player player) {
            if (player.altFunctionUse == 2 || DontModifyHeldStyle()) {
                return;
            }

            Vector2 mouseWorld = Main.MouseWorld;
            player.ChangeDir(Math.Sign((mouseWorld - player.Center).X));

            float rotation = (player.Center - mouseWorld).ToRotation() * player.gravDir + MathHelper.PiOver2;
            player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rotation);
        }

        /// <summary>
        /// 计算使用动画进度（0 起始 → 1 结束）
        /// </summary>
        private static float GetAnimationProgress(Player player) {
            if (player.itemTimeMax <= 0) {
                return 1f;
            }
            return 1f - player.itemTime / (float)player.itemTimeMax;
        }

        /// <summary>
        /// 应用清爽的持握样式，参照大比目鱼的实现，使武器以鼠标方向锚定绘制
        /// </summary>
        private static void ApplyHoldingStyle(Player player, float rotation, Vector2 position, Vector2 itemSize, Vector2 originOffset) {
            originOffset.X *= player.direction;
            originOffset.Y *= player.gravDir;

            player.itemRotation = rotation;
            if (player.direction < 0) {
                player.itemRotation += MathHelper.Pi;
            }

            Vector2 centerAnchor = player.itemRotation.ToRotationVector2() * (itemSize.X / -2f - 10f) * player.direction;
            Vector2 anchor = centerAnchor - originOffset.RotatedBy(player.itemRotation);
            Vector2 finalPosition = position + itemSize * -0.5f + anchor;

            int frame = player.bodyFrame.Y / player.bodyFrame.Height;
            if ((frame > 6 && frame < 10) || (frame > 13 && frame < 17)) {
                finalPosition -= Vector2.UnitY * 2f;
            }

            player.itemLocation = finalPosition + new Vector2(itemSize.X * 0.5f, 0);
        }
        #endregion

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
