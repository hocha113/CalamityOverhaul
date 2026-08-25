using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Shatterfangs
{
    /// <summary>
    /// 崩牙獠刃手持挥砍。相位时间线 举刀→滞帧→斩切→收势<br/>
    /// 完整态四拍：三记常规挥舞(0/2拍间歇甩碎齿)接终结震撼斩，爆发末端剑身当场崩坏，
    /// 轰出大牙碎片+小碎齿+崩坏爆点；半刃态三拍轻快循环，无弹幕<br/>
    /// ai[0]=拍号(0..2常规/3终结) ai[1]=交替符号 ai[2]=状态 0完整 1半刃 2此挥收势时疲劳碎裂
    /// </summary>
    internal class ShatterfangHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<Shatterfang>();

        private const int PhaseRaise = 0;
        private const int PhaseHold = 1;
        private const int PhaseSlash = 2;
        private const int PhaseRecover = 3;

        /// <summary>手→刃尖基准距离(px)</summary>
        private const float BaseReach = 152f;
        /// <summary>刀身贴图中心停在手→刃尖的几成处</summary>
        private const float BladePark = 0.48f;
        /// <summary>刀尖顶到手→刃尖的几成，略过 1 压在刀光外缘上</summary>
        private const float BladeTipFill = 1.03f;

        //阶段时长与挥砍几何，InitStage 按拍号写入(已含攻速缩放)
        private int raiseDur = 8;
        private int holdDur = 2;
        private int slashDur = 4;
        private int recoverDur = 10;
        private int totalDur;
        private float raiseBack = 2.0f;
        private float follow = 1.15f;
        private float reachScale = 1f;
        private float fanInnerRatio = 0.32f;
        private float leanAmp = 0.045f;
        private int fanSegments = 40;
        private int shardCount;

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
        private bool slashSoundPlayed;
        private bool shardsFired;
        private bool shatterDone;
        private bool fatigueDone;
        private bool sweepDamageActive;
        private float bodyLean;
        private bool bodyLeanApplied;
        /// <summary>崩坏白裂纹闪帧</summary>
        private int crackFlashTimer;
        /// <summary>碎裂时刀身抖动余帧，顿帧期间照常走</summary>
        private int shakeTimer;
        private float shakeAmp;
        /// <summary>当前视觉是否半刃(终结拍中途翻转)</summary>
        private bool brokenVisual;
        /// <summary>剑身不稳颤抖幅度，出手时从持有者稳固度取样</summary>
        private float instabShiver;

        private int timer;

        /// <summary>连段拍号 0/1/2=常规 3=终结震撼斩</summary>
        private int ComboBeat => Math.Clamp((int)Projectile.ai[0], 0, 3);
        private bool IsFinisher => ComboBeat == 3;
        /// <summary>状态码 0完整 1半刃 2完整但此挥收势时疲劳碎裂</summary>
        private int StateCode => Math.Clamp((int)Projectile.ai[2], 0, 2);
        private bool IsBrokenBeat => StateCode == 1;

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
            Projectile.width = Projectile.height = 54;
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
            swingDir = (Projectile.ai[1] >= 0f ? 1f : -1f) * facingDir;
            brokenVisual = IsBrokenBeat;

            float speed = Owner.GetWeaponAttackSpeed(Item);
            if (speed <= 0f) {
                speed = 1f;
            }
            int D(int frames) => Math.Max(1, (int)MathF.Round(frames / speed));

            if (IsBrokenBeat) {
                //半刃轻快拍
                raiseDur = D(5);
                holdDur = D(1);
                slashDur = D(3);
                recoverDur = D(7);
                raiseBack = 1.85f;
                follow = 1.05f;
                reachScale = 0.80f;
                fanInnerRatio = 0.52f;
                leanAmp = 0.03f;
                fanSegments = 34;
                shardCount = 0;
            }
            else if (IsFinisher) {
                raiseDur = D(14);
                holdDur = D(5);
                slashDur = D(6);
                recoverDur = D(18);
                raiseBack = 2.6f;
                follow = 1.42f;
                reachScale = 1.28f;
                fanInnerRatio = 0.36f;
                leanAmp = 0.12f;
                fanSegments = 56;
                shardCount = 0;
                //长蓄势的低鸣起手
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.5f, Pitch = -0.7f }, Owner.Center);
                }
            }
            else {
                bool third = ComboBeat == 2;
                raiseDur = D(third ? 9 : 8);
                holdDur = D(2);
                slashDur = D(4);
                recoverDur = D(third ? 11 : 10);
                raiseBack = third ? 2.15f : 2.0f;
                follow = third ? 1.25f : 1.15f;
                reachScale = third ? 1.06f : 1f;
                fanInnerRatio = third ? 0.42f : 0.44f;
                leanAmp = third ? 0.06f : 0.045f;
                fanSegments = third ? 44 : 40;
                //0拍甩1片、2拍甩2片，间歇性的碎齿
                shardCount = ComboBeat == 0 ? 1 : third ? 2 : 0;
            }
            totalDur = raiseDur + holdDur + slashDur + recoverDur;

            //剑身不稳的颤抖：疲劳挥最烈，其余按持有者本机稳固度取样
            if (StateCode == 2) {
                instabShiver = 0.022f;
            }
            else if (Projectile.IsOwnedByLocalPlayer() && !IsBrokenBeat) {
                float stab = Owner.GetModPlayer<ShatterfangPlayer>().Stability;
                instabShiver = stab < 0.4f ? (0.4f - stab) * 0.035f : 0f;
            }
        }

        public override void AI() {
            if (Item.type != ModContent.ItemType<Shatterfang>() || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }

            if (timer == 0) {
                InitStage();
            }

            //顿帧：timer 不推进，刀角、扇面、体态一起冻住
            if (hitstopTimer > 0) {
                hitstopTimer--;
            }
            else {
                timer++;
            }
            if (flashTimer > 0) {
                flashTimer--;
            }
            if (crackFlashTimer > 0) {
                crackFlashTimer--;
            }
            if (shakeTimer > 0) {
                shakeTimer--;
            }

            int phase = CurrentPhase;
            lastAngle = mainAngle;
            UpdateBladeTransform(phase);
            UpdateDamageWindow(phase);
            UpdatePose(phase);
            HandlePhaseEvents(phase);
            HandleParticles(phase);

            Lighting.AddLight(Vector2.Lerp(Hand, mainTip, 0.7f)
                , ShatterfangFX.BloodMain.ToVector3() * (1.2f * fanFade));

            if (timer >= totalDur) {
                Projectile.Kill();
            }
        }

        /// <summary>斩切行程曲线，爆发冲到过冲再回坐；回坐完几何冻结</summary>
        private static float SwingCurve(float p) {
            const float burstEnd = 0.58f;
            const float overshoot = 1.055f;
            if (p < burstEnd) {
                return overshoot * SmoothStep01(p / burstEnd);
            }
            return MathHelper.Lerp(overshoot, 1f, SmoothStep01((p - burstEnd) / (1f - burstEnd)));
        }

        private void UpdateBladeTransform(int phase) {
            float arcStart = ArcStart;
            float heldAngle = arcStart - (swingDir * 0.09f);

            switch (phase) {
                case PhaseRaise: {
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
                    float p = (timer - raiseDur) / (float)holdDur;
                    mainAngle = MathHelper.Lerp(arcStart, heldAngle, EaseOutQuad(p));
                    if (IsFinisher) {
                        //蓄满的死寂里剑身发着颤
                        mainAngle += swingDir * 0.018f * MathF.Sin(timer * 1.9f);
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
                    mainReach = FullReach * (0.96f + 0.05f * MathF.Sin(MathHelper.Clamp(p * 1.8f, 0f, 1f) * MathHelper.Pi));
                    sweepT = MathHelper.Clamp(MathF.Abs((mainAngle - arcStart) / TotalSweep), 0f, 1f);
                    break;
                }
                default: {
                    float q = (timer - raiseDur - holdDur - slashDur) / (float)recoverDur;
                    float settle = EaseOutQuad(Math.Min(1f, q * 2.2f));
                    mainAngle = ArcEnd + (swingDir * 0.10f * (1f - settle));
                    mainReach = FullReach * MathHelper.Lerp(0.96f, 0.80f, EaseInQuad(q));
                    slashProgress = 1f;
                    sweepT = 1f;
                    float fadeDur = MathF.Max(5f, recoverDur * 0.72f);
                    fanFade = MathHelper.Clamp(1f - ((timer - raiseDur - holdDur - slashDur) / fadeDur), 0f, 1f);
                    break;
                }
            }

            //剑身不稳的细颤，只在举刀/滞帧期
            if (instabShiver > 0.001f && phase is PhaseRaise or PhaseHold) {
                mainAngle += MathF.Sin(timer * 2.1f) * instabShiver;
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
            //终结拍蓄力完成：刀身一记闪 + 裂纹吱响预告
            if (IsFinisher && timer == raiseDur + 1) {
                flashTimer = 8;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.28f, Pitch = 0.55f }, Owner.Center);
                }
            }

            if (phase == PhaseSlash && !slashSoundPlayed) {
                slashSoundPlayed = true;
                if (!VaultUtils.isServer) {
                    if (IsFinisher) {
                        flashTimer = Math.Max(flashTimer, 7);
                        SoundEngine.PlaySound(SoundID.Item1 with { Volume = 1.0f, Pitch = -0.45f }, Owner.Center);
                        SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.7f, Pitch = -0.55f }, Owner.Center);
                        SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.6f, Pitch = -0.2f }, Owner.Center);
                    }
                    else if (IsBrokenBeat) {
                        //半刃轻快高频
                        SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.62f, Pitch = 0.38f + ComboBeat * 0.06f }, Owner.Center);
                    }
                    else {
                        SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = ComboBeat * 0.09f }, Owner.Center);
                        if (ComboBeat == 2) {
                            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.3f, Pitch = 0.15f }, Owner.Center);
                        }
                    }
                }
                Vector2 punchDir = (baseAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
                ShatterfangFX.Punch(Owner.Center, punchDir
                    , IsFinisher ? 7.5f : IsBrokenBeat ? 2f : 2.8f
                    , IsFinisher ? 6.5f : 4f, IsFinisher ? 10 : 5);
            }

            //碎齿跟着爆发音甩出手；高攻速下窗口可能整段跳过，收势期兜底
            if (!shardsFired && shardCount > 0 && (phase == PhaseSlash && slashProgress >= 0.22f || phase == PhaseRecover)) {
                shardsFired = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.5f, Pitch = 0.25f }, Owner.Center);
                }
                FireShards();
            }

            //终结震撼斩的爆发末端：剑身当场崩坏
            if (IsFinisher && !shatterDone && (phase == PhaseSlash && slashProgress >= 0.92f || phase == PhaseRecover)) {
                DoShatter();
            }

            //疲劳碎裂：收势时无声无息地断掉
            if (StateCode == 2 && !fatigueDone && phase == PhaseRecover) {
                DoFatigueBreak();
            }
        }

        /// <summary>沿刃缘甩出小碎齿，方向锁出手瞄准，带上飘让重力弧读得出来</summary>
        private void FireShards() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Vector2 aimDir = baseAngle.ToRotationVector2();
            int shardDamage = Math.Max(1, (int)(Projectile.damage * 0.55f));
            for (int i = 0; i < shardCount; i++) {
                float offset = shardCount <= 1 ? 0f : MathHelper.Lerp(-0.16f, 0.16f, i / (float)(shardCount - 1));
                offset += Main.rand.NextFloat(-0.05f, 0.05f);
                Vector2 velocity = (baseAngle + offset).ToRotationVector2() * Main.rand.NextFloat(10.5f, 13.5f);
                velocity.Y -= Main.rand.NextFloat(0.6f, 1.6f);
                Vector2 spawnPos = Hand + aimDir * (FullReach * Main.rand.NextFloat(0.5f, 0.8f));
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), spawnPos, velocity
                    , ModContent.ProjectileType<ShatterfangShard>(), shardDamage
                    , Projectile.knockBack * 0.3f, Owner.whoAmI, ai1: Main.rand.Next(2));
            }
        }

        /// <summary>
        /// 剑身崩坏：顿帧定格+白裂纹闪+牙屑血雾迸溅+震屏三层响，
        /// 崩下的大牙碎片带小碎齿一并轰出，断口处炸开崩坏爆点
        /// </summary>
        private void DoShatter() {
            shatterDone = true;
            brokenVisual = true;
            crackFlashTimer = 5;
            shakeTimer = 9;
            shakeAmp = 3.8f;
            hitstopTimer = Math.Max(hitstopTimer, 3);

            Vector2 breakPos = Vector2.Lerp(Hand, mainTip, 0.62f);
            Vector2 aimDir = baseAngle.ToRotationVector2();
            Vector2 tangent = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 1.0f, Pitch = -0.5f }, breakPos);
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.65f, Pitch = -0.3f }, breakPos);
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.5f, Pitch = 0.25f }, breakPos);
                ShatterfangFX.ChipBurst(breakPos, aimDir, 1f, 0.4f);
                ShatterfangFX.ChipBurst(breakPos, tangent, 0.6f, 0.3f);
                ShatterfangFX.BloodBurst(breakPos, aimDir, 0.5f);
                ShatterfangFX.BonePuff(breakPos, 4, 1.2f);
            }
            ShatterfangFX.Punch(breakPos, tangent, 8f, 7f, 12);

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Owner.GetModPlayer<ShatterfangPlayer>().BreakBlade();

            //崩下来的大牙碎片
            Vector2 bigVel = aimDir * 12.5f + tangent * 1.5f;
            bigVel.Y -= 1.2f;
            Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), breakPos, bigVel
                , ModContent.ProjectileType<ShatterfangBigShard>()
                , Math.Max(1, (int)(Projectile.damage * 1.7f)), Projectile.knockBack * 1.2f, Owner.whoAmI);
            //少量小碎齿
            for (int i = 0; i < 2; i++) {
                Vector2 vel = (baseAngle + Main.rand.NextFloat(-0.3f, 0.3f)).ToRotationVector2()
                    * Main.rand.NextFloat(9f, 13f);
                vel.Y -= Main.rand.NextFloat(0.8f, 2f);
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), breakPos, vel
                    , ModContent.ProjectileType<ShatterfangShard>()
                    , Math.Max(1, (int)(Projectile.damage * 0.55f)), Projectile.knockBack * 0.3f
                    , Owner.whoAmI, ai1: Main.rand.Next(2));
            }
            //崩坏爆点，小范围震撼判定
            Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), breakPos, Vector2.Zero
                , ModContent.ProjectileType<ShatterfangBurstFX>()
                , Math.Max(1, (int)(Projectile.damage * 1.3f)), Projectile.knockBack, Owner.whoAmI);
        }

        /// <summary>疲劳碎裂：一声闷响几粒碎屑，没有演出也没有伤害</summary>
        private void DoFatigueBreak() {
            fatigueDone = true;
            brokenVisual = true;
            shakeTimer = 6;
            shakeAmp = 2.2f;

            Vector2 bladeMid = Vector2.Lerp(Hand, mainTip, 0.6f);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.5f, Pitch = -0.15f }, bladeMid);
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.3f, Pitch = -0.5f }, bladeMid);
                ShatterfangFX.ChipBurst(bladeMid, -baseAngle.ToRotationVector2(), 0.3f, 0.25f);
                ShatterfangFX.BonePuff(bladeMid, 2);
            }
            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.GetModPlayer<ShatterfangPlayer>().BreakBlade();
            }
        }

        /// <summary>粒子演出：终结蓄力血珠汇聚，斩切期沿切线甩骨渣，不稳时掉牙屑</summary>
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
                    //终结蓄力：血珠被拽向刃身，红光渐醒
                    if (phase == PhaseHold || Main.rand.NextBool(2)) {
                        Vector2 anchor = Vector2.Lerp(hand, mainTip, Main.rand.NextFloat(0.4f, 0.9f));
                        Vector2 offset = Main.rand.NextVector2CircularEdge(60f, 60f);
                        PRTLoader.NewParticle<Content.PRTTypes.PRT_HeartcarverDroplet>(anchor + offset, -offset * 0.09f
                            , Main.rand.NextBool() ? ShatterfangFX.ScarletBright : ShatterfangFX.BloodMain
                            , Main.rand.NextFloat(0.6f, 1.0f))?.Configure(11, 0.02f);
                    }
                    break;
                }
                case PhaseSlash: {
                    Vector2 sweepVel = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
                    //沿切线甩骨渣
                    int count = IsFinisher ? 3 : 1;
                    for (int i = 0; i < count; i++) {
                        Dust d = Dust.NewDustPerfect(Vector2.Lerp(hand, mainTip, Main.rand.NextFloat(0.55f, 1f))
                            , DustID.Bone, sweepVel * Main.rand.NextFloat(4f, 9f), 110, default, Main.rand.NextFloat(0.8f, 1.2f));
                        d.noGravity = true;
                    }
                    //剑身不稳时挥砍掉牙屑
                    if (instabShiver > 0.004f && Main.rand.NextBool(2)) {
                        PRTLoader.NewParticle<PRT_ToothChip>(Vector2.Lerp(hand, mainTip, Main.rand.NextFloat(0.5f, 0.9f))
                            , sweepVel * Main.rand.NextFloat(2f, 5f), ShatterfangFX.Ivory
                            , Main.rand.NextFloat(0.18f, 0.3f))?.Configure(Main.rand.Next(20, 32), 0.22f);
                    }
                    break;
                }
                default: {
                    //终结收势的骨灰余韵
                    if (IsFinisher && timer % 2 == 0 && fanFade > 0.15f) {
                        float u = Main.rand.NextFloat(0.2f, 0.95f);
                        float ang = ArcStart + (swingDir * TotalSweep * u);
                        Vector2 at = hand + ang.ToRotationVector2() * (mainReach * Main.rand.NextFloat(0.7f, 1.0f));
                        PRTLoader.NewParticle<PRT_CrimsonSmoke>(at, Main.rand.NextVector2Circular(0.6f, 0.6f)
                            , Color.White, Main.rand.NextFloat(0.06f, 0.1f))
                            ?.Configure(Main.rand.Next(18, 28), new Color(210, 196, 184), ShatterfangFX.BloodDeep, 0.012f);
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
            if (greedyBox.Distance(hand) <= 50f) {
                return true;
            }

            float delta = mainAngle - lastAngle;
            float reach = mainReach * 1.04f + 12f;
            int steps = Math.Clamp((int)MathF.Ceiling(MathF.Abs(delta) * reach / 30f), 1, 24);
            float collisionPoint = 0f;
            float width = IsFinisher ? 62f : 50f;
            for (int i = 0; i <= steps; i++) {
                float ang = MathHelper.Lerp(lastAngle, mainAngle, i / (float)steps);
                Vector2 tip = hand + ang.ToRotationVector2() * reach;
                if (Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size()
                    , hand, tip, width, ref collisionPoint)) {
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
            //终结震撼斩重击，击退跟出手朝向
            if (IsFinisher) {
                modifiers.SourceDamage *= 2.1f;
            }
            modifiers.HitDirectionOverride = facingDir;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命中顿帧一拍只吃一次
            if (!hitstopApplied && CurrentPhase == PhaseSlash) {
                hitstopApplied = true;
                hitstopTimer = Math.Max(hitstopTimer, IsFinisher ? 3 : 1);
            }
            if (IsFinisher) {
                Vector2 tangent = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
                ShatterfangFX.Punch(target.Center, tangent, 5f, 6f, 7, 900f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 3 }, target.Center);
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            //命中材质分流：血肉喷血，钢铁上是牙先崩屑
            Vector2 outDir = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
            if (CWRLoad.NPCValue.ISTheofSteel(target)) {
                ShatterfangFX.ChipBurst(target.Center, outDir, IsFinisher ? 0.7f : 0.35f, 0.15f);
                SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.35f, Pitch = 0.2f, MaxInstances = 3 }, target.Center);
                int sparks = IsFinisher ? 5 : 3;
                for (int i = 0; i < sparks; i++) {
                    Vector2 vel = outDir.RotatedByRandom(0.7) * Main.rand.NextFloat(4f, 9f);
                    PRTLoader.NewParticle<PRT_CrimsonSteelSpark>(target.Center, vel, new Color(255, 190, 150)
                        , Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(14, 24), gravity: true, maxBounces: 2);
                }
            }
            else {
                ShatterfangFX.BloodBurst(target.Center, outDir, IsFinisher ? 1f : 0.45f);
            }
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
            DrawCrackFlash(Main.spriteBatch);
            return false;
        }

        /// <summary>扇形刀光：骨白前沿红体拖尾，着色器沿 SweepT 追着刀锋亮</summary>
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

            //外缘压在刀尖(1.03)之内，刀尖略微探出刀光
            float outerR = mainReach * 0.96f;
            float innerR = mainReach * fanInnerRatio;
            Vector2 center = Hand;
            //起点补 0.25 弧度，扇根和刀背衔接
            float arcStart = ArcStart + (swingDir * 0.25f);
            //半刃拍刀光更薄更淡
            Color vertCol = IsBrokenBeat ? new Color(255, 255, 255, 205) : Color.White;

            for (int i = 0; i < segs; i++) {
                float t = i / (float)(segs - 1);
                float u = t * sweepT;
                float ang = arcStart + (swingDir * TotalSweep * u);
                Vector2 dir = ang.ToRotationVector2();
                float bulge = 1f + (0.02f * MathF.Pow(t, 3f));
                Vector2 outer = center + (dir * outerR * bulge) - Main.screenPosition;
                Vector2 inner = center + (dir * innerR) - Main.screenPosition;
                verts[i * 2] = new ColoredVertex(outer, vertCol, new Vector3(u, 0f, 0f));
                verts[(i * 2) + 1] = new ColoredVertex(inner, vertCol, new Vector3(u, 1f, 0f));
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

            //骨白亮闪只走爆发头两三帧
            float hotSpike = MathF.Pow(flashTimer / 8f, 3f) * 1.1f;
            Trail.CalculateRenderingMatrices(out Matrix view, out Matrix projection);
            effect.Parameters["WorldViewProjection"]?.SetValue(view * projection);
            effect.Parameters["TotalTime"]?.SetValue((float)Main.GameUpdateCount / 60f);
            effect.Parameters["SweepT"]?.SetValue(sweepT);
            effect.Parameters["FadeOut"]?.SetValue(fanFade);
            effect.Parameters["HeatBoost"]?.SetValue((IsFinisher ? 1.05f : IsBrokenBeat ? 0.6f : 0.82f)
                + (slashProgress * 0.3f) + hotSpike);
            effect.Parameters["RimIntensity"]?.SetValue((IsFinisher ? 1.7f : IsBrokenBeat ? 1.1f : 1.35f) + hotSpike * 0.5f);
            effect.Parameters["LeadColor"]?.SetValue(ShatterfangFX.BoneLead.ToVector4());
            effect.Parameters["GoldColor"]?.SetValue(ShatterfangFX.ScarletBright.ToVector4());
            effect.Parameters["AmberColor"]?.SetValue(ShatterfangFX.BloodMain.ToVector4());
            effect.Parameters["TailColor"]?.SetValue(ShatterfangFX.BloodDeep.ToVector4());
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

        /// <summary>着色器缺失时的弧光回退</summary>
        private void DrawArcFallback(SpriteBatch sb) {
            Texture2D wave = CWRAsset.SemiCircularSmear?.Value;
            if (wave == null) {
                return;
            }
            float alpha = fanFade * (0.35f + (slashProgress * 0.4f));
            Vector2 arcCenter = Hand + (mainAngle.ToRotationVector2() * mainReach * 0.6f);
            Color c = ShatterfangFX.BloodMain * alpha;
            c.A = 0;
            sb.Draw(wave, arcCenter - Main.screenPosition, null, c,
                mainAngle + (swingDir * 0.35f), wave.Size() / 2f, new Vector2(0.5f, 0.24f), SpriteEffects.None, 0f);
            Color c2 = ShatterfangFX.BoneLead * (alpha * 0.6f);
            c2.A = 0;
            sb.Draw(wave, arcCenter - Main.screenPosition, null, c2,
                mainAngle + (swingDir * 0.35f), wave.Size() / 2f, new Vector2(0.45f, 0.11f), SpriteEffects.None, 0f);
        }

        /// <summary>刀身残影+暗影垫底+本体+裂纹红光；崩坏瞬间起换半刃贴图</summary>
        private void DrawBladeSet(SpriteBatch sb, Color lightColor) {
            Texture2D tex = (brokenVisual ? ShatterfangAssets.BrokenBlade : ShatterfangAssets.FullBlade)?.Value;
            if (tex == null) {
                return;
            }
            Vector2 origin = tex.Size() / 2f;
            GetBladeDrawOrientation(tex, out SpriteEffects effect, out float rotOffset);
            float scale = GetBladeDrawScale(tex);
            Vector2 hand = Hand;
            int phase = CurrentPhase;

            //斩切期姿态残影，最近的最亮
            if (phase == PhaseSlash && slashProgress > 0.08f) {
                int ghostCount = IsFinisher ? 4 : IsBrokenBeat ? 2 : 3;
                float ghostSpacing = IsFinisher ? 0.26f : 0.19f;
                for (int g = ghostCount; g >= 1; g--) {
                    float ghostAngle = mainAngle - (swingDir * ghostSpacing * g);
                    float ghostAlpha = g switch { 1 => 0.40f, 2 => 0.22f, 3 => 0.10f, _ => 0.05f };
                    Color ghost = ShatterfangFX.BoneLead * ghostAlpha;
                    ghost.A = 0;
                    Vector2 gPos = hand + (ghostAngle.ToRotationVector2() * mainReach * BladePark) - Main.screenPosition;
                    sb.Draw(tex, gPos, null, ghost, ghostAngle + rotOffset, origin, scale, effect, 0);
                }
            }

            Vector2 drawPos = hand + (mainAngle.ToRotationVector2() * mainReach * BladePark) - Main.screenPosition;
            //碎裂瞬间的小幅抖动，顿帧定格里也在颤
            if (shakeTimer > 0) {
                drawPos += Main.rand.NextVector2Circular(1f, 1f) * (shakeAmp * shakeTimer / 9f);
            }

            //厚重暗影垫底
            Color shadow = new Color(16, 4, 8, 190) * 0.5f;
            sb.Draw(tex, drawPos + new Vector2(facingDir, 2f), null, shadow, mainAngle + rotOffset, origin, scale * 1.02f, effect, 0);

            sb.Draw(tex, drawPos, null, lightColor, mainAngle + rotOffset, origin, scale, effect, 0);

            //裂纹红光：不稳/疲劳挥常亮，终结蓄力期渐醒
            float crackGlow = instabShiver > 0.001f ? MathHelper.Clamp(instabShiver / 0.022f, 0f, 1f) * 0.4f : 0f;
            if (IsFinisher && !brokenVisual) {
                float chargeT = MathHelper.Clamp(timer / (float)raiseDur, 0f, 1f);
                crackGlow = MathF.Max(crackGlow, chargeT * 0.55f);
            }
            float flash = flashTimer / 8f;
            if (crackGlow > 0.02f || flash > 0.01f) {
                Color glow = Color.Lerp(ShatterfangFX.ScarletBright, ShatterfangFX.BoneLead, flash)
                    * (crackGlow + flash * 0.55f);
                glow.A = 0;
                sb.Draw(tex, drawPos, null, glow, mainAngle + rotOffset, origin, scale * 1.04f, effect, 0);
            }
        }

        /// <summary>崩坏瞬间的白裂纹闪：断口锯齿白光+整刃剪影，白是结构不是增益</summary>
        private void DrawCrackFlash(SpriteBatch sb) {
            if (crackFlashTimer <= 0) {
                return;
            }
            float t = crackFlashTimer / 5f;
            Vector2 breakPos = Vector2.Lerp(Hand, mainTip, 0.62f) - Main.screenPosition;

            //断口锯齿白光，沿刃轴两层交错
            Texture2D jag = CWRAsset.HitJagged01?.Value;
            if (jag != null) {
                Color white = ShatterfangFX.BoneLead * (t * 0.95f);
                white.A = 0;
                Vector2 jOrigin = jag.Size() * 0.5f;
                sb.Draw(jag, breakPos, null, white, mainAngle + MathHelper.PiOver2
                    , jOrigin, new Vector2(0.55f, 0.8f) * (1.1f - t * 0.3f), SpriteEffects.None, 0f);
                sb.Draw(jag, breakPos, null, white * 0.7f, mainAngle - MathHelper.PiOver4
                    , jOrigin, new Vector2(0.4f, 0.6f) * (1.2f - t * 0.3f), SpriteEffects.None, 0f);
            }

            //头两帧整刃白剪影
            if (crackFlashTimer >= 4) {
                Texture2D tex = (brokenVisual ? ShatterfangAssets.BrokenBlade : ShatterfangAssets.FullBlade)?.Value;
                if (tex != null) {
                    GetBladeDrawOrientation(tex, out SpriteEffects effect, out float rotOffset);
                    Color sil = ShatterfangFX.BoneLead * 0.9f;
                    sil.A = 0;
                    Vector2 drawPos = Hand + (mainAngle.ToRotationVector2() * mainReach * BladePark) - Main.screenPosition;
                    sb.Draw(tex, drawPos, null, sil, mainAngle + rotOffset, tex.Size() / 2f
                        , GetBladeDrawScale(tex) * 1.03f, effect, 0);
                }
            }
        }

        /// <summary>反向拍翻刃，刃口镜像到挥动前缘，否则读作刀背砍人；对角偏移按贴图真实宽高算，48×64 不是 45°</summary>
        private void GetBladeDrawOrientation(Texture2D tex, out SpriteEffects effect, out float rotOffset) {
            bool edgeFlip = (Projectile.ai[1] >= 0f ? 1 : -1) * facingDir < 0;
            bool flipVertically = (facingDir < 0) != edgeFlip;
            effect = flipVertically ? SpriteEffects.FlipVertically : SpriteEffects.None;
            float axis = ShatterfangFX.BladeAxisOffset(tex);
            rotOffset = flipVertically ? -axis : axis;
        }

        /// <summary>刀身画多大由挥砍半径反推，刀刃沿贴图对角走，对角长即刃轴长</summary>
        private float GetBladeDrawScale(Texture2D tex) {
            float spriteAxis = MathF.Max(new Vector2(tex.Width, tex.Height).Length(), 1f);
            return mainReach * (BladeTipFill - BladePark) * 2f / spriteAxis;
        }
    }
}
