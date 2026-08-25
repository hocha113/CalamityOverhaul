using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.GameSystem;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Modifys
{
    /// <summary>
    /// 血腥屠刀重做，三段厚重屠宰连击，两记交替重劈接一记终结巨斩<br/>
    /// 挥舞时沿刃缘甩出多道暗红血弹，血弹吃重力走抛物线，命中与贴壁四处飞溅留渍<br/>
    /// 刀光复用 DivineSourceArc（血色板参数化），血弹拖线复用 FishIchornJet
    /// </summary>
    internal class RBloodButcherer : ItemOverride
    {
        public override int TargetID => ItemID.BloodButcherer;

        //屠刀血色板，深红/血色/黑系
        internal static readonly Color BloodLead = new(226, 88, 70);    //动脉亮红前沿
        internal static readonly Color BloodBright = new(188, 34, 40);  //高亮血红
        internal static readonly Color BloodMain = new(124, 16, 24);    //主体暗红
        internal static readonly Color BloodDeep = new(52, 8, 14);      //近黑拖尾

        /// <summary>连段计数，取模三拍；只在本地玩家的 Shoot 里消费</summary>
        private int comboCounter;
        /// <summary>断手回第一拍的倒计时</summary>
        private int comboResetTimer;

        public override void SetStaticDefaults() {
            //noMelee 会丢近战词缀，强行标回
            ItemMeleePrefixDic[ItemID.BloodButcherer] = true;
        }

        public override void SetDefaults(Item item) {
            item.damage = 26;
            item.useTime = item.useAnimation = 24;
            item.knockBack = 6.5f;
            item.useStyle = ItemUseStyleID.Shoot;
            item.useTurn = false;
            item.noMelee = true;
            item.noUseGraphic = true;
            item.autoReuse = true;
            item.UseSound = null;
            item.shoot = ModContent.ProjectileType<BloodButchererHeld>();
            item.shootSpeed = 13f;
        }

        public override bool? CanUseItem(Item item, Player player) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<BloodButchererHeld>()] > 0) {
                return false;
            }
            return null;
        }

        public override void HoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (comboResetTimer > 0 && --comboResetTimer == 0) {
                comboCounter = 0;
            }
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int combo = comboCounter % 3;
            float swingDir = comboCounter % 2 == 0 ? 1f : -1f;
            comboCounter++;
            comboResetTimer = 60;
            Projectile.NewProjectile(source, player.Center, velocity
                , ModContent.ProjectileType<BloodButchererHeld>(), damage, knockback, player.whoAmI, combo, swingDir);
            return false;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            string[] lines = Tooltip.Value.Split('\n');
            for (int i = 0; i < lines.Length; i++) {
                if (string.IsNullOrWhiteSpace(lines[i])) {
                    continue;
                }
                tooltips.Add(new TooltipLine(CWRMod.Instance, "CWR_RBloodButcherer" + i, lines[i]));
            }
        }
    }

    /// <summary>
    /// 血腥屠刀手持挥砍。相位时间线 举刀→滞帧→斩切→收势，斩切段爆发过冲后回坐冻结<br/>
    /// 扇形刀光沿 SweepT 追刀锋，爆发帧甩出血弹并压一记动脉亮闪<br/>
    /// ai[0]=拍号 0/1=交替重劈 2=终结巨斩，ai[1]=交替符号
    /// </summary>
    internal class BloodButchererHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.BloodButcherer");

        private const int PhaseRaise = 0;
        private const int PhaseHold = 1;
        private const int PhaseSlash = 2;
        private const int PhaseRecover = 3;

        /// <summary>手→刃尖基准距离（px），终结拍乘 reachScale</summary>
        private const float BaseReach = 172f;
        /// <summary>刀身贴图中心停在手→刃尖的几成处</summary>
        private const float BladePark = 0.48f;
        /// <summary>刀尖顶到手→刃尖的几成，略过 1 压在刀光外缘上</summary>
        private const float BladeTipFill = 1.03f;

        //阶段时长与挥砍几何，InitStage 按拍号写入（已含攻速缩放）
        private int raiseDur = 9;
        private int holdDur = 2;
        private int slashDur = 4;
        private int recoverDur = 11;
        private int totalDur;
        private float raiseBack = 2.0f;
        private float follow = 1.1f;
        private float reachScale = 1f;
        private float fanInnerRatio = 0.30f;
        private float leanAmp = 0.05f;
        private int boltCount = 3;
        private int fanSegments = 40;

        private float baseAngle;
        private float swingDir = 1f;
        private int facingDir = 1;
        private float mainAngle;
        private float lastAngle;
        private float mainReach;
        private Vector2 mainTip;
        private float slashProgress;
        private float sweepT;
        private float fanFade = 1f;
        private int flashTimer;
        private int hitstopTimer;
        private bool hitstopApplied;
        private bool boltsFired;
        private bool slashSoundPlayed;
        private bool sweepDamageActive;
        private float bodyLean;
        private bool bodyLeanApplied;
        private readonly HashSet<int> hitNPCs = [];

        private int timer;

        /// <summary>连段拍号 0/1=交替重劈 2=终结巨斩</summary>
        private int ComboStage => Math.Clamp((int)Projectile.ai[0], 0, 2);
        private bool IsFinisher => ComboStage >= 2;

        private int CurrentPhase {
            get {
                if (timer <= raiseDur) {
                    return PhaseRaise;
                }
                if (timer <= raiseDur + holdDur) {
                    return PhaseHold;
                }
                if (timer <= raiseDur + holdDur + slashDur) {
                    return PhaseSlash;
                }
                return PhaseRecover;
            }
        }

        private float FullReach => BaseReach * reachScale;
        private float TotalSweep => raiseBack + follow;
        private float ArcStart => baseAngle - (swingDir * raiseBack);
        private float ArcEnd => baseAngle + (swingDir * follow);
        private Vector2 Hand => Owner.GetPlayerStabilityCenter();

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 56;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 120;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>按拍号写入时长与几何；时长除以攻速，快刀真的变快</summary>
        private void InitStage() {
            baseAngle = Projectile.velocity.ToRotation();
            float cos = MathF.Cos(baseAngle);
            facingDir = MathF.Abs(cos) < 0.05f ? Owner.direction : Math.Sign(cos);
            //ai[1] 只给交替符号，实际扫向乘上朝向
            swingDir = (Projectile.ai[1] >= 0f ? 1f : -1f) * facingDir;

            float speed = Owner.GetWeaponAttackSpeed(Item);
            if (speed <= 0f) {
                speed = 1f;
            }
            int D(int frames) => Math.Max(1, (int)MathF.Round(frames / speed));

            if (IsFinisher) {
                raiseDur = D(13);
                holdDur = D(4);
                slashDur = D(6);
                recoverDur = D(16);
                raiseBack = 2.45f;
                follow = 1.35f;
                reachScale = 1.24f;
                fanInnerRatio = 0.24f;
                leanAmp = 0.11f;
                boltCount = 5;
                fanSegments = 54;
                Projectile.damage = (int)(Projectile.damage * 1.4f);
            }
            else {
                raiseDur = D(9);
                holdDur = D(2);
                slashDur = D(4);
                recoverDur = D(11);
                raiseBack = 2.0f;
                follow = 1.1f;
                reachScale = 1f;
                fanInnerRatio = 0.30f;
                leanAmp = 0.05f;
                boltCount = 3;
                fanSegments = 40;
            }
            totalDur = raiseDur + holdDur + slashDur + recoverDur;
        }

        public override void AI() {
            if (Item.type != ItemID.BloodButcherer || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }

            if (timer == 0) {
                InitStage();
            }

            //命中顿帧：timer 不推进，刀角、扇面、体态一起冻住
            if (hitstopTimer > 0) {
                hitstopTimer--;
            }
            else {
                timer++;
            }
            if (flashTimer > 0) {
                flashTimer--;
            }

            int phase = CurrentPhase;
            lastAngle = mainAngle;
            UpdateBladeTransform(phase);
            UpdateDamageWindow(phase);
            UpdatePose(phase);
            HandlePhaseEvents(phase);
            HandleParticles(phase);

            Lighting.AddLight(Vector2.Lerp(Hand, mainTip, 0.7f)
                , RBloodButcherer.BloodMain.ToVector3() * (1.4f * fanFade));

            if (timer >= totalDur) {
                Projectile.Kill();
            }
        }

        /// <summary>斩切行程曲线，爆发冲到 5.5% 过冲再回坐；回坐完几何冻结</summary>
        private static float SwingCurve(float p) {
            const float burstEnd = 0.58f;
            const float overshoot = 1.055f;
            if (p < burstEnd) {
                return overshoot * SmoothStep01(p / burstEnd);
            }
            return MathHelper.Lerp(overshoot, 1f, SmoothStep01((p - burstEnd) / (1f - burstEnd)));
        }

        /// <summary>由 timer 解算刀角与手→刃尖距离，扇面进度一并从这里出</summary>
        private void UpdateBladeTransform(int phase) {
            float arcStart = ArcStart;
            float heldAngle = arcStart - (swingDir * 0.09f);

            switch (phase) {
                case PhaseRaise: {
                    //自然持位反拉上膛，厚重的举刀
                    float p = timer / (float)raiseDur;
                    float eased = EaseOutCubic(p);
                    float liftFrom = arcStart + (swingDir * raiseBack * 0.72f);
                    mainAngle = MathHelper.Lerp(liftFrom, arcStart, eased);
                    mainReach = FullReach * MathHelper.Lerp(0.55f, 0.92f, eased);
                    sweepT = 0f;
                    slashProgress = 0f;
                    break;
                }
                case PhaseHold: {
                    //死寂滞帧，静默里攒张力
                    float p = (timer - raiseDur) / (float)holdDur;
                    mainAngle = MathHelper.Lerp(arcStart, heldAngle, EaseOutQuad(p));
                    if (IsFinisher) {
                        mainAngle += swingDir * 0.016f * MathF.Sin(timer * 1.9f);
                    }
                    mainReach = FullReach * MathHelper.Lerp(0.92f, 0.97f, EaseOutQuad(p));
                    sweepT = 0f;
                    slashProgress = 0f;
                    break;
                }
                case PhaseSlash: {
                    float p = (timer - raiseDur - holdDur) / (float)slashDur;
                    slashProgress = p;
                    mainAngle = MathHelper.Lerp(heldAngle, ArcEnd, SwingCurve(p));
                    //爆发帧半径小幅顶出，力从地起
                    mainReach = FullReach * (0.96f + 0.05f * MathF.Sin(MathHelper.Clamp(p * 1.8f, 0f, 1f) * MathHelper.Pi));
                    sweepT = MathHelper.Clamp(MathF.Abs((mainAngle - arcStart) / TotalSweep), 0f, 1f);
                    break;
                }
                default: {
                    //小幅回坐落定后真正静止，扇面从拖尾侧蚀散
                    float q = (timer - raiseDur - holdDur - slashDur) / (float)recoverDur;
                    float settle = EaseOutQuad(Math.Min(1f, q * 2.2f));
                    mainAngle = ArcEnd + (swingDir * 0.10f * (1f - settle));
                    mainReach = FullReach * MathHelper.Lerp(0.96f, 0.82f, EaseInQuad(q));
                    slashProgress = 1f;
                    sweepT = 1f;
                    float fadeDur = MathF.Max(5f, recoverDur * 0.72f);
                    fanFade = MathHelper.Clamp(1f - ((timer - raiseDur - holdDur - slashDur) / fadeDur), 0f, 1f);
                    break;
                }
            }

            mainTip = Hand + (mainAngle.ToRotationVector2() * mainReach);
        }

        /// <summary>伤害窗对齐爆发帧，只在刀真的在走的时候出伤</summary>
        private void UpdateDamageWindow(int phase) {
            sweepDamageActive = phase == PhaseSlash
                && slashProgress <= 0.88f
                && MathF.Abs(mainAngle - lastAngle) > 0.004f;
        }

        public override bool? CanDamage() => sweepDamageActive ? null : false;

        /// <summary>持械姿态，双手握柄；体态收势后仰爆发前甩</summary>
        private void UpdatePose(int phase) {
            Owner.ChangeDir(facingDir);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = (mainAngle.ToRotationVector2() * Owner.direction).ToRotation();

            Player.CompositeArmStretchAmount stretch = phase is PhaseRaise or PhaseRecover
                ? Player.CompositeArmStretchAmount.ThreeQuarters
                : Player.CompositeArmStretchAmount.Full;
            Owner.SetCompositeArmFront(true, stretch, mainAngle - MathHelper.PiOver2);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters
                , mainAngle - MathHelper.PiOver2 + (swingDir * 0.28f));

            Projectile.Center = Vector2.Lerp(Hand, mainTip, 0.6f);
            Projectile.rotation = mainAngle;

            //命中顿帧期体态同帧冻结
            if (hitstopTimer > 0) {
                return;
            }
            (float target, float rate) = phase switch {
                PhaseRaise => (-facingDir * leanAmp * 0.8f, 0.22f),
                PhaseHold => (-facingDir * leanAmp, 0.30f),
                PhaseSlash => (facingDir * leanAmp * 1.5f, 0.70f),
                _ => (0f, 0.16f),
            };
            bodyLean = MathHelper.Lerp(bodyLean, target, rate);
            ApplyBodyLean();
        }

        /// <summary>体态倾斜上身，坐骑/冲刺旋转让位，origin 钉脚底</summary>
        private void ApplyBodyLean() {
            CWRPlayer modPlayer = Owner.CWR();
            if (Owner.mount.Active || (modPlayer != null && modPlayer.IsRotatingDuringDash)) {
                bodyLeanApplied = false;
                return;
            }
            Owner.fullRotation = bodyLean * Owner.gravDir;
            Owner.fullRotationOrigin = new Vector2(Owner.width * 0.5f, Owner.gravDir >= 0f ? Owner.height : 0f);
            bodyLeanApplied = true;
        }

        /// <summary>死亡兜底交还 fullRotation，防斜身残留</summary>
        public override void OnKill(int timeLeft) {
            if (bodyLeanApplied && Owner.active) {
                Owner.fullRotation = 0f;
                bodyLeanApplied = false;
            }
        }

        private void HandlePhaseEvents(int phase) {
            //终结拍蓄力完成的瞬间刀身闪一记
            if (IsFinisher && timer == raiseDur + 1) {
                flashTimer = 8;
            }

            if (phase == PhaseSlash && !slashSoundPlayed) {
                slashSoundPlayed = true;
                flashTimer = Math.Max(flashTimer, 7);
                if (!VaultUtils.isServer) {
                    //爆发帧主响，厚重的肉铺挥砍
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.85f, Pitch = -0.4f }, Owner.Center);
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.5f, Pitch = -0.55f }, Owner.Center);
                    if (IsFinisher) {
                        SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.75f, Pitch = -0.15f }, Owner.Center);
                    }
                }
                if (!VaultUtils.isServer && CWRClientConfig.Instance.ScreenVibration) {
                    Vector2 punchDir = (baseAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(Owner.Center, punchDir
                        , IsFinisher ? 7.5f : 2.6f, IsFinisher ? 6.5f : 4f, IsFinisher ? 10 : 5, 1100f, FullName));
                }
            }

            //血弹跟着爆发音拍出手；高攻速下窗口可能整段跳过，收势期兜底
            if (!boltsFired && (phase == PhaseSlash && slashProgress >= 0.20f || phase == PhaseRecover)) {
                boltsFired = true;
                FireBloodBolts();
            }
        }

        /// <summary>沿刃缘甩出多道血弹，方向锁出手瞄准，带上飘让重力弧读得出来</summary>
        private void FireBloodBolts() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Vector2 aimDir = baseAngle.ToRotationVector2();
            float spread = IsFinisher ? 0.38f : 0.22f;
            int boltDamage = Math.Max(1, (int)(Projectile.damage * 0.45f));
            for (int i = 0; i < boltCount; i++) {
                float offset = boltCount <= 1 ? 0f : MathHelper.Lerp(-spread, spread, i / (float)(boltCount - 1));
                offset += Main.rand.NextFloat(-0.05f, 0.05f);
                Vector2 velocity = (baseAngle + offset).ToRotationVector2()
                    * Main.rand.NextFloat(IsFinisher ? 12.5f : 11.5f, IsFinisher ? 16.5f : 14.5f);
                velocity.Y -= Main.rand.NextFloat(0.6f, 1.8f);
                Vector2 spawnPos = Hand + aimDir * (FullReach * Main.rand.NextFloat(0.45f, 0.8f));
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), spawnPos, velocity
                    , ModContent.ProjectileType<BloodButchererBolt>(), boltDamage
                    , Projectile.knockBack * 0.4f, Owner.whoAmI);
            }
        }

        /// <summary>粒子演出：终结蓄力血珠向刃身汇聚，斩切期血沿切线甩出，收势期血雾</summary>
        private void HandleParticles(int phase) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 hand = Hand;
            switch (phase) {
                case PhaseRaise:
                case PhaseHold: {
                    if (!IsFinisher) {
                        break;
                    }
                    //终结拍蓄力：血珠被拽向刃身
                    if (phase == PhaseHold || Main.rand.NextBool(2)) {
                        Vector2 anchor = Vector2.Lerp(hand, mainTip, Main.rand.NextFloat(0.4f, 0.9f));
                        Vector2 offset = Main.rand.NextVector2CircularEdge(66f, 66f);
                        PRTLoader.NewParticle<PRT_HeartcarverDroplet>(anchor + offset, -offset * 0.09f
                            , Main.rand.NextBool() ? RBloodButcherer.BloodBright : RBloodButcherer.BloodMain
                            , Main.rand.NextFloat(0.6f, 1.0f))?.Configure(11, 0.02f);
                    }
                    break;
                }
                case PhaseSlash: {
                    //血沿切线甩出，约三分之一可贴块落地留渍
                    Vector2 sweepVel = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
                    int count = IsFinisher ? 3 : 2;
                    for (int i = 0; i < count; i++) {
                        Vector2 at = Vector2.Lerp(hand, mainTip, Main.rand.NextFloat(0.55f, 1.0f));
                        Vector2 vel = sweepVel * Main.rand.NextFloat(5f, 11f);
                        vel.Y -= Main.rand.NextFloat(0.3f, 1.2f);
                        Color c = Main.rand.NextBool(3) ? RBloodButcherer.BloodBright : RBloodButcherer.BloodMain;
                        if (Main.rand.NextBool(3)) {
                            PRTLoader.NewParticle<PRT_CrimsonBloodStain>(at, vel, c
                                , Main.rand.NextFloat(0.9f, 1.4f))
                                ?.Configure(Main.rand.Next(34, 52), 0.42f, 0.99f, stuckLifetime: Main.rand.Next(34, 52));
                        }
                        else {
                            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(at, vel, c
                                , Main.rand.NextFloat(0.9f, 1.4f))?.Configure(Main.rand.Next(16, 28), 0.30f);
                        }
                    }
                    break;
                }
                default: {
                    //重拍收势血雾，扇面蚀散的余韵
                    if (IsFinisher && timer % 2 == 0 && fanFade > 0.15f) {
                        float u = Main.rand.NextFloat(0.2f, 0.95f);
                        float ang = ArcStart + (swingDir * TotalSweep * u);
                        Vector2 at = hand + ang.ToRotationVector2() * (mainReach * Main.rand.NextFloat(0.7f, 1.0f));
                        PRTLoader.NewParticle<PRT_CrimsonSmoke>(at, Main.rand.NextVector2Circular(0.6f, 0.6f)
                            , Color.White, Main.rand.NextFloat(0.07f, 0.12f))
                            ?.Configure(Main.rand.Next(20, 32), RBloodButcherer.BloodMain, RBloodButcherer.BloodDeep, 0.012f);
                    }
                    break;
                }
            }
        }

        /// <summary>贪婪判定：本帧扫过的角度区间逐段采样，贴身段单独兜一次</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!sweepDamageActive) {
                return false;
            }
            Rectangle greedyBox = targetHitbox;
            greedyBox.Inflate(10, 10);
            Vector2 hand = Hand;
            //画面重叠了却打不到最伤玩家信任
            if (greedyBox.Distance(hand) <= 50f) {
                return true;
            }

            float delta = mainAngle - lastAngle;
            float reach = mainReach * 1.04f + 12f;
            int steps = Math.Clamp((int)MathF.Ceiling(MathF.Abs(delta) * reach / 30f), 1, 24);
            float collisionPoint = 0f;
            for (int i = 0; i <= steps; i++) {
                float ang = MathHelper.Lerp(lastAngle, mainAngle, i / (float)steps);
                Vector2 tip = hand + ang.ToRotationVector2() * reach;
                if (Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size()
                    , hand, tip, 54f, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>割草断藤跟着扫掠走，贴身段也算</summary>
        public override void CutTiles() {
            if (!sweepDamageActive) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            Vector2 hand = Hand;
            const int samples = 3;
            for (int i = 0; i <= samples; i++) {
                float ang = MathHelper.Lerp(lastAngle, mainAngle, i / (float)samples);
                Vector2 tip = hand + ang.ToRotationVector2() * (mainReach * 1.02f);
                Utils.PlotTileLine(hand, tip, 42f, DelegateMethods.CutTiles);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //击退跟出手朝向，不跟当帧刀角
            modifiers.HitDirectionOverride = facingDir;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //保留原版血腥屠戮 debuff
            target.AddBuff(BuffID.BiomeSight, Main.rand.Next(240, 420));

            //本次挥砍对同一目标只转发一次外部命中钩子
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }

            ApplyImpactFeedback(target.Center);

            if (!VaultUtils.isServer) {
                bool steel = CWRLoad.NPCValue.ISTheofSteel(target);
                Vector2 aimDir = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
                SpawnHitBurst(target.Center, aimDir, IsFinisher ? 1f : 0.55f, steel);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.BiomeSight, 240);
            ApplyImpactFeedback(target.Center);
            if (!VaultUtils.isServer) {
                Vector2 aimDir = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
                SpawnHitBurst(target.Center, aimDir, IsFinisher ? 1f : 0.55f, steel: false);
            }
        }

        /// <summary>命中材质分流，血肉重力血珠+贴块血渍，金属弹射钢屑</summary>
        internal static void SpawnHitBurst(Vector2 pos, Vector2 aimDir, float power, bool steel) {
            if (steel) {
                PRTLoader.NewParticle<PRT_CrimsonHitFlash>(pos, Vector2.Zero
                    , new Color(255, 200, 170), 0.55f + power * 0.4f);
                int sparks = 5 + (int)(power * 6f);
                for (int i = 0; i < sparks; i++) {
                    Vector2 vel = aimDir.RotatedByRandom(0.7) * Main.rand.NextFloat(4f, 10f + power * 5f);
                    PRTLoader.NewParticle<PRT_CrimsonSteelSpark>(pos, vel, new Color(255, 110, 70)
                        , Main.rand.NextFloat(0.4f, 0.7f))
                        ?.Configure(Main.rand.Next(16, 28), gravity: true, maxBounces: 2);
                }
                return;
            }

            //动脉喷溅，约三分之二可贴块
            int drops = 7 + (int)(power * 8f);
            for (int i = 0; i < drops; i++) {
                Vector2 vel = aimDir.RotatedByRandom(0.85) * Main.rand.NextFloat(5f, 11f + power * 6f);
                vel.Y -= Main.rand.NextFloat(0.6f, 2.2f);
                Color c = Main.rand.NextBool(4) ? RBloodButcherer.BloodLead
                    : (Main.rand.NextBool() ? RBloodButcherer.BloodBright : RBloodButcherer.BloodMain);
                float sc = Main.rand.NextFloat(0.95f, 1.6f + power * 0.3f);
                if (!Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_CrimsonBloodStain>(pos, vel, c, sc)
                        ?.Configure(Main.rand.Next(38, 58), 0.42f, 0.99f, stuckLifetime: Main.rand.Next(36, 56));
                }
                else {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, c, sc)
                        ?.Configure(Main.rand.Next(20, 34), 0.30f);
                }
            }
            //伤口暗红血雾垫底
            for (int i = 0; i < 1 + (int)(power * 2f); i++) {
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos + Main.rand.NextVector2Circular(7f, 5f)
                    , Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.4f, 1.4f)
                    , Color.White, Main.rand.NextFloat(0.08f, 0.13f))
                    ?.Configure(Main.rand.Next(20, 32), RBloodButcherer.BloodBright, RBloodButcherer.BloodDeep, 0.01f);
            }
        }

        /// <summary>命中顿帧一拍只吃一次；终结拍另补一记沿切线的震屏</summary>
        private void ApplyImpactFeedback(Vector2 hitPos) {
            if (!hitstopApplied && CurrentPhase == PhaseSlash) {
                hitstopApplied = true;
                hitstopTimer = IsFinisher ? 3 : 1;
            }
            if (VaultUtils.isServer || !CWRClientConfig.Instance.ScreenVibration || !IsFinisher) {
                return;
            }
            Vector2 tangent = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(hitPos, tangent, 5f, 6f, 7, 900f, FullName));
        }

        private static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - t, 3f);
        private static float EaseOutQuad(float t) => 1f - ((1f - t) * (1f - t));
        private static float EaseInQuad(float t) => t * t;
        private static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            DrawArcFan(Main.spriteBatch);
            DrawBladeSet(Main.spriteBatch, lightColor);
            return false;
        }

        /// <summary>扇形血光：外缘贴刃尖轨迹、内缘羽化撕碎，着色器沿 SweepT 追着刀锋亮</summary>
        private void DrawArcFan(SpriteBatch sb) {
            if (sweepT <= 0.03f || fanFade <= 0.02f) {
                return;
            }
            Effect effect = EffectLoader.DivineSourceArc?.Value;
            if (effect == null) {
                DrawArcFallback(sb);
                return;
            }

            int segs = Math.Max(8, (int)(fanSegments * sweepT) + 2);
            var verts = new ColoredVertex[segs * 2];
            var inds = new short[(segs - 1) * 6];

            float outerR = mainReach * 1.05f;
            float innerR = mainReach * fanInnerRatio;
            Vector2 center = Hand;
            //起点补 0.25 弧度，扇根和刀背衔接
            float arcStart = ArcStart + (swingDir * 0.25f);

            for (int i = 0; i < segs; i++) {
                float t = i / (float)(segs - 1);
                float u = t * sweepT;
                float ang = arcStart + (swingDir * TotalSweep * u);
                Vector2 dir = ang.ToRotationVector2();
                float bulge = 1f + (0.05f * MathF.Pow(t, 3f));
                Vector2 outer = center + (dir * outerR * bulge) - Main.screenPosition;
                Vector2 inner = center + (dir * innerR) - Main.screenPosition;
                verts[i * 2] = new ColoredVertex(outer, Color.White, new Vector3(u, 0f, 0f));
                verts[(i * 2) + 1] = new ColoredVertex(inner, Color.White, new Vector3(u, 1f, 0f));
            }
            for (int i = 0; i < segs - 1; i++) {
                int vi = i * 2;
                int ii = i * 6;
                inds[ii] = (short)vi;
                inds[ii + 1] = (short)(vi + 1);
                inds[ii + 2] = (short)(vi + 2);
                inds[ii + 3] = (short)(vi + 2);
                inds[ii + 4] = (short)(vi + 1);
                inds[ii + 5] = (short)(vi + 3);
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            sb.End();

            BlendState prevBlend = device.BlendState;
            SamplerState prevSampler = device.SamplerStates[0];
            RasterizerState prevRaster = device.RasterizerState;
            DepthStencilState prevDepth = device.DepthStencilState;

            device.BlendState = BlendState.AlphaBlend;
            device.SamplerStates[0] = SamplerState.LinearWrap;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            //动脉亮闪只走爆发头两三帧，血的白不常驻
            float hotSpike = MathF.Pow(flashTimer / 8f, 3f) * 1.1f;
            Trail.CalculateRenderingMatrices(out Matrix view, out Matrix projection);
            effect.Parameters["WorldViewProjection"]?.SetValue(view * projection);
            effect.Parameters["TotalTime"]?.SetValue((float)Main.GameUpdateCount / 60f);
            effect.Parameters["SweepT"]?.SetValue(sweepT);
            effect.Parameters["FadeOut"]?.SetValue(fanFade);
            effect.Parameters["HeatBoost"]?.SetValue(0.72f + (slashProgress * 0.30f) + hotSpike);
            effect.Parameters["RimIntensity"]?.SetValue((IsFinisher ? 1.65f : 1.3f) + hotSpike * 0.5f);
            effect.Parameters["LeadColor"]?.SetValue(RBloodButcherer.BloodLead.ToVector4());
            effect.Parameters["GoldColor"]?.SetValue(RBloodButcherer.BloodBright.ToVector4());
            effect.Parameters["AmberColor"]?.SetValue(RBloodButcherer.BloodMain.ToVector4());
            effect.Parameters["TailColor"]?.SetValue(RBloodButcherer.BloodDeep.ToVector4());
            Texture2D noise = CWRAsset.Fog?.Value ?? CWRAsset.PerlinNoise?.Value;
            if (noise != null) {
                effect.Parameters["NoiseTexture"]?.SetValue(noise);
            }

            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                Trail.DrawUserPrimitives(verts, inds, device);
            }

            device.BlendState = prevBlend;
            device.SamplerStates[0] = prevSampler;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>着色器缺失时的血弧回退</summary>
        private void DrawArcFallback(SpriteBatch sb) {
            Texture2D wave = CWRAsset.SemiCircularSmear?.Value;
            if (wave == null) {
                return;
            }
            float alpha = fanFade * (0.35f + (slashProgress * 0.4f));
            Vector2 arcCenter = Hand + (mainAngle.ToRotationVector2() * mainReach * 0.6f);
            Color c = RBloodButcherer.BloodMain * alpha;
            c.A = 0;
            sb.Draw(wave, arcCenter - Main.screenPosition, null, c,
                mainAngle + (swingDir * 0.35f), wave.Size() / 2f, new Vector2(0.5f, 0.24f), SpriteEffects.None, 0f);
            Color c2 = RBloodButcherer.BloodBright * (alpha * 0.7f);
            c2.A = 0;
            sb.Draw(wave, arcCenter - Main.screenPosition, null, c2,
                mainAngle + (swingDir * 0.35f), wave.Size() / 2f, new Vector2(0.45f, 0.11f), SpriteEffects.None, 0f);
        }

        /// <summary>刀身残影+暗影垫底+本体+终结加色辉光，全部用原版物品贴图</summary>
        private void DrawBladeSet(SpriteBatch sb, Color lightColor) {
            Main.instance.LoadItem(ItemID.BloodButcherer);
            Texture2D tex = TextureAssets.Item[ItemID.BloodButcherer].Value;
            Vector2 origin = tex.Size() / 2f;
            GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset);
            float scale = GetBladeDrawScale(tex);
            Vector2 hand = Hand;
            int phase = CurrentPhase;

            //斩切期姿态残影，最近的最亮
            if (phase == PhaseSlash && slashProgress > 0.08f) {
                int ghostCount = IsFinisher ? 4 : 3;
                float ghostSpacing = IsFinisher ? 0.26f : 0.20f;
                for (int g = ghostCount; g >= 1; g--) {
                    float ghostAngle = mainAngle - (swingDir * ghostSpacing * g);
                    float ghostAlpha = g switch { 1 => 0.40f, 2 => 0.22f, 3 => 0.10f, _ => 0.05f };
                    Color ghost = RBloodButcherer.BloodBright * ghostAlpha;
                    ghost.A = 0;
                    Vector2 gPos = hand + (ghostAngle.ToRotationVector2() * mainReach * BladePark) - Main.screenPosition;
                    sb.Draw(tex, gPos, null, ghost, ghostAngle + rotOffset, origin, scale, effect, 0);
                }
            }

            Vector2 drawPos = hand + (mainAngle.ToRotationVector2() * mainReach * BladePark) - Main.screenPosition;

            //厚重暗影垫底
            Color shadow = new Color(18, 4, 8, 190) * 0.55f;
            sb.Draw(tex, drawPos + new Vector2(facingDir, 2f), null, shadow, mainAngle + rotOffset, origin, scale * 1.02f, effect, 0);

            sb.Draw(tex, drawPos, null, lightColor, mainAngle + rotOffset, origin, scale, effect, 0);

            //终结拍与蓄力闪的加色辉光
            float flash = flashTimer / 8f;
            if (IsFinisher || flash > 0.01f) {
                Color glow = RBloodButcherer.BloodLead * (0.28f + (flash * 0.5f));
                glow.A = 0;
                sb.Draw(tex, drawPos, null, glow, mainAngle + rotOffset, origin, scale * 1.04f, effect, 0);
            }
        }

        /// <summary>反向拍翻刃，刃口镜像到挥动前缘，否则读作刀背砍人</summary>
        private void GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset) {
            bool edgeFlip = (Projectile.ai[1] >= 0f ? 1 : -1) * facingDir < 0;
            bool flipVertically = (facingDir < 0) != edgeFlip;
            effect = flipVertically ? SpriteEffects.FlipVertically : SpriteEffects.None;
            rotOffset = flipVertically ? -MathHelper.PiOver4 : MathHelper.PiOver4;
        }

        /// <summary>刀身画多大由挥砍半径反推，刀刃沿贴图对角走，对角长即刃轴长</summary>
        private float GetBladeDrawScale(Texture2D tex) {
            float spriteAxis = MathF.Max(new Vector2(tex.Width, tex.Height).Length(), 1f);
            return mainReach * (BladeTipFill - BladePark) * 2f / spriteAxis;
        }
    }

    /// <summary>
    /// 屠刀血弹：一团有体积的粘稠血，不是光条<br/>
    /// 头部三层液团带表面张力抖动，身后拖会珠化断裂的粘血线（FishIchornJet 换血色板）<br/>
    /// 短暂平直后吃重力走抛物线，飞行失稳甩珠；命中/贴壁半球迸溅并留下滴淌血渍
    /// </summary>
    internal class BloodButchererBolt : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>出膛后多少帧开始吃重力，血是甩出去的不是射出去的</summary>
        private const int GravityDelay = 9;

        private ref float Life => ref Projectile.ai[0];

        private Trail trail;
        private bool burstDone;

        /// <summary>连续量抖动的确定性相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 4 帧淡入，避免第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;

            //抛物线：短暂平直后被重量拽下去，粘性阻力让水平段也在缓慢泄劲
            if (Life > GravityDelay) {
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.24f, 15f);
            }
            Projectile.velocity *= 0.996f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //表面张力失稳，从团身后侧撕下小血珠
            if (!Main.dedServ && Life % 3 == 0) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Vector2 spawnPos = Projectile.Center - dir * Main.rand.NextFloat(5f, 14f);
                Vector2 dropVel = Projectile.velocity * Main.rand.NextFloat(0.2f, 0.45f)
                    + dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-1.1f, 1.1f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(spawnPos, dropVel
                    , Main.rand.NextBool(3) ? RBloodButcherer.BloodDeep : RBloodButcherer.BloodMain
                    , Main.rand.NextFloat(0.5f, 0.85f))?.Configure(Main.rand.Next(14, 24), 0.26f);
            }

            float glow = 0.4f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.5f * glow, 0.1f * glow, 0.1f * glow);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            burstDone = true;
            SplashBurst(Projectile.Center, oldVelocity, onTile: true);
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.BiomeSight, Main.rand.Next(180, 300));
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            if (!burstDone) {
                //命中 NPC / 超时坠灭共用
                SplashBurst(Projectile.Center, Projectile.velocity, onTile: false);
            }
            //血线失压散珠，拖尾旧位上留几粒回落的残珠，余痕活得比弹幕久
            Vector2[] oldPos = Projectile.oldPos;
            if (oldPos == null) {
                return;
            }
            for (int i = 2; i < oldPos.Length; i += 4) {
                if (oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 pos = oldPos[i] + Projectile.Size * 0.5f;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos + Main.rand.NextVector2Circular(4f, 4f)
                    , Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.7f, 0.7f)
                    , Main.rand.NextBool(3) ? RBloodButcherer.BloodDeep : RBloodButcherer.BloodMain
                    , Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(14, 24), 0.28f);
            }
        }

        /// <summary>命中迸溅：半球血珠扇+沉重可贴血渍+原版血尘垫底</summary>
        private static void SplashBurst(Vector2 pos, Vector2 impactVel, bool onTile) {
            if (Main.dedServ) {
                return;
            }
            Vector2 normal = -impactVel.SafeNormalize(Vector2.UnitY);
            float ke = MathHelper.Clamp(impactVel.Length() / 18f, 0.3f, 1f);
            float mainAngle = normal.ToRotation();

            //半球迸溅，越贴法线越快
            int count = (int)(5 + 4 * ke);
            for (int i = 0; i < count; i++) {
                float spreadAng = Main.rand.NextFloat(-MathHelper.PiOver2, MathHelper.PiOver2);
                float speedRatio = 1f - MathF.Abs(spreadAng) / MathHelper.PiOver2;
                Vector2 vel = (mainAngle + spreadAng).ToRotationVector2()
                    * Main.rand.NextFloat(2f, 7f) * (0.35f + 0.65f * speedRatio) * (0.5f + ke);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos + Main.rand.NextVector2Circular(4f, 4f), vel
                    , Main.rand.NextBool(3) ? RBloodButcherer.BloodDeep : RBloodButcherer.BloodMain
                    , Main.rand.NextFloat(0.55f, 0.95f))?.Configure(Main.rand.Next(18, 30), 0.32f);
            }
            //沉重血渍，落地贴附滴淌
            int stains = onTile ? 3 : 2;
            for (int i = 0; i < stains; i++) {
                Vector2 vel = normal.RotatedByRandom(0.75f) * Main.rand.NextFloat(1.4f, 3.8f);
                PRTLoader.NewParticle<PRT_CrimsonBloodStain>(pos, vel
                    , Main.rand.NextBool() ? RBloodButcherer.BloodMain : RBloodButcherer.BloodDeep
                    , Main.rand.NextFloat(1.0f, 1.5f))
                    ?.Configure(Main.rand.Next(40, 60), 0.46f, 0.985f, stuckLifetime: Main.rand.Next(40, 62));
            }
            //原版血尘只做底噪
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Blood
                    , normal.RotatedByRandom(0.9f) * Main.rand.NextFloat(1.2f, 3.2f), 100, default, Main.rand.NextFloat(1f, 1.4f));
                d.noGravity = Main.rand.NextBool();
            }

            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.32f, Pitch = -0.1f, MaxInstances = 3 }, pos);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.3f, Pitch = -0.3f, MaxInstances = 3 }, pos);
        }

        //==================== 绘制 ====================

        public float GetWidthFunc(float completionRatio)
            => MathHelper.Lerp(7.5f, 1f, completionRatio) * VisualFade;   //0=团后颈最宽，尾端收成丝

        public Color GetColorFunc(Vector2 coord) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !Projectile.active || VisualFade <= 0.01f) {
                return;
            }
            DrawBloodTrail();

            //液团头部画在条带之上
            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            DrawGlobHead(sb);
            sb.End();
        }

        /// <summary>粘血线条带：借灵液液柱 shader（四色全参数化）换屠刀血色板，尾段自带珠化断裂</summary>
        private void DrawBloodTrail() {
            Effect fx = FishIchornAssets.FishIchornJet;
            if (fx == null || Projectile.oldPos == null || Projectile.oldPos.Length == 0) {
                return;
            }
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Seed);
            fx.Parameters["uFade"]?.SetValue(VisualFade * 0.9f);
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (noise != null) {
                fx.Parameters["uNoiseTex"]?.SetValue(noise);
            }
            fx.Parameters["uColDark"]?.SetValue(RBloodButcherer.BloodDeep.ToVector3());
            fx.Parameters["uColDeep"]?.SetValue(RBloodButcherer.BloodMain.ToVector3());
            fx.Parameters["uColGold"]?.SetValue(RBloodButcherer.BloodBright.ToVector3());
            fx.Parameters["uColBright"]?.SetValue(RBloodButcherer.BloodLead.ToVector3());

            Vector2[] positions = new Vector2[Projectile.oldPos.Length];
            for (int i = 0; i < positions.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    Projectile.oldPos[i] = Projectile.position;
                }
                positions[i] = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            }
            trail ??= new Trail(positions, GetWidthFunc, GetColorFunc);
            trail.TrailPositions = positions;
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
            trail.DrawTrail(fx);
        }

        /// <summary>液团头部：暗血压边→血红主体→血沫亮芯，表面张力抖动+速度拉伸</summary>
        private void DrawGlobHead(SpriteBatch sb) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center + Projectile.velocity * 0.35f - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float rotation = Projectile.rotation;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.032f, 0.15f, 0.8f);

            //表面张力抖动，宽窄反相呼吸
            float wob = MathF.Sin(Life * 0.55f + Seed * 6f) * 0.12f;
            Vector2 jiggle = new(1f + wob, 1f - wob * 0.8f);

            //暗血压边
            sb.Draw(tex, pos, null, RBloodButcherer.BloodDeep * (0.85f * fade), rotation, origin,
                new Vector2(0.5f, 0.54f + stretch * 0.85f) * jiggle, SpriteEffects.None, 0f);
            //血红主体
            sb.Draw(tex, pos, null, RBloodButcherer.BloodMain * fade, rotation, origin,
                new Vector2(0.38f, 0.44f + stretch * 0.75f) * jiggle, SpriteEffects.None, 0f);
            //血沫亮芯，极小面积加色湿反光
            Color core = RBloodButcherer.BloodLead with { A = 0 };
            sb.Draw(tex, pos, null, core * (0.55f * fade), rotation, origin,
                new Vector2(0.13f, 0.22f + stretch * 0.3f) * jiggle, SpriteEffects.None, 0f);
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
