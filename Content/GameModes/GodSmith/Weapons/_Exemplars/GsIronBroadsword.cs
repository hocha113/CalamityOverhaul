using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons._Exemplars
{
    /// <summary>
    /// 【范例·近战接管】铁宽剑重铸：三拍屠宰连击（举-滞-斩），两记交替重劈接一记前压终结斩。<br/>
    /// 全流程镜像 <c>RBloodButcherer</c> 的骨架但按框架规矩走：不碰 SetDefaults，
    /// 由 GsCanUseItem 在 owner 侧生成手持弹幕并压掉原版挥舞，切模式即时恢复原版。<br/>
    /// 材质：冷锻铁刃。签名行为：①挥砍拖出钢灰残影与摩擦火星 ②终结斩前压半步且刃面短暂灼橙
    /// ③命中钢质目标迸溅弹跳钢屑
    /// </summary>
    internal class GsIronBroadsword : GodSmithScheme
    {
        public override int TargetItemID => ItemID.IronBroadsword;

        public override string GsFamily => "Exemplars";

        protected override string GsDescFallback =>
            "Reforged: a three-beat butcher combo; the third strike lunges forward with a heavier arc";

        //冷锻铁刃色板
        internal static readonly Color IronBright = new(222, 226, 232);  //钢灰亮
        internal static readonly Color IronMain = new(158, 164, 176);    //铁身
        internal static readonly Color IronHot = new(255, 168, 92);      //摩擦灼橙
        internal static readonly Color IronDeep = new(64, 66, 76);       //近黑铁影

        /// <summary>连段计数，取模三拍；只在本地玩家路径消费（方案单例跨玩家共享）</summary>
        private int comboCounter;
        /// <summary>断手回第一拍的倒计时，只在本地玩家路径消费</summary>
        private int comboResetTimer;

        public override bool? GsCanUseItem(Item item, Player player) {
            //手持弹幕在场即攻击冷却（真实冷却 = max(useTime, 弹幕总帧)，两者都吃攻速）
            if (HeldAlive<GsIronBroadswordHeld>(player)) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                int beat = comboCounter % 3;
                float swingSign = comboCounter % 2 == 0 ? 1f : -1f;
                comboCounter++;
                comboResetTimer = 55;
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    ModContent.ProjectileType<GsIronBroadswordHeld>(),
                    player.GetWeaponDamage(item), item.knockBack, player.whoAmI, beat, swingSign);
            }
            //全端返回 false 压掉原版挥舞；远端靠弹幕同步看到动作
            return false;
        }

        public override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (comboResetTimer > 0 && --comboResetTimer == 0) {
                comboCounter = 0;
            }
        }

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.10f;//弱势前期武器，重铸补一成底伤，综合 DPS 落在原版 105%~115%
    }

    /// <summary>
    /// 铁宽剑手持挥砍。相位时间线 举刀-滞帧-斩切-收势；
    /// 命中顿帧从收势尾巴等量扣回，不白送冷却。<br/>
    /// ai[0]=拍号 0/1=交替重劈 2=前压终结斩，ai[1]=交替符号
    /// </summary>
    internal class GsIronBroadswordHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.IronBroadsword");

        private const int PhaseRaise = 0;
        private const int PhaseHold = 1;
        private const int PhaseSlash = 2;
        private const int PhaseRecover = 3;

        /// <summary>手→刃尖基准距离（px），终结拍乘 reachScale</summary>
        private const float BaseReach = 118f;
        /// <summary>刀身贴图中心停在手→刃尖的几成处</summary>
        private const float BladePark = 0.46f;
        /// <summary>刀尖顶到手→刃尖的几成</summary>
        private const float BladeTipFill = 1.02f;

        //阶段时长与几何，InitStage 按拍号写入（已含攻速缩放）
        private int raiseDur = 6;
        private int holdDur = 2;
        private int slashDur = 4;
        private int recoverDur = 9;
        private int totalDur;
        private float raiseBack = 1.85f;
        private float follow = 1.0f;
        private float reachScale = 1f;
        private float leanAmp = 0.045f;

        private float baseAngle;
        private float swingDir = 1f;
        private int facingDir = 1;
        private float mainAngle;
        private float lastAngle;
        private float mainReach;
        private Vector2 mainTip;
        private float slashProgress;
        private float fanFade = 1f;
        private int flashTimer;
        private int hitstopTimer;
        private int hitstopSpent;
        private bool hitstopApplied;
        private bool slashSoundPlayed;
        private bool lungeApplied;
        private bool sweepDamageActive;
        private float bodyLean;
        private bool bodyLeanApplied;
        private readonly HashSet<int> hitNPCs = [];

        private int timer;

        /// <summary>连段拍号 0/1=交替重劈 2=前压终结斩</summary>
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
            Projectile.width = Projectile.height = 44;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 90;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>按拍号写入时长与几何；各相时长除以攻速，攻速词条真实生效</summary>
        private void InitStage() {
            baseAngle = Projectile.velocity.ToRotation();
            float cos = MathF.Cos(baseAngle);
            facingDir = MathF.Abs(cos) < 0.05f ? Owner.direction : Math.Sign(cos);
            swingDir = (Projectile.ai[1] >= 0f ? 1f : -1f) * facingDir;

            float speed = Owner.GetWeaponAttackSpeed(Item);
            if (speed <= 0f) {
                speed = 1f;
            }
            int D(int frames) => Math.Max(1, (int)MathF.Round(frames / speed));

            if (IsFinisher) {
                raiseDur = D(8);
                holdDur = D(3);
                slashDur = D(5);
                recoverDur = D(12);
                raiseBack = 2.25f;
                follow = 1.25f;
                reachScale = 1.18f;
                leanAmp = 0.09f;
                Projectile.damage = (int)(Projectile.damage * 1.35f);
            }
            else {
                raiseDur = D(6);
                holdDur = D(2);
                slashDur = D(4);
                recoverDur = D(9);
                raiseBack = 1.85f;
                follow = 1.0f;
                reachScale = 1f;
                leanAmp = 0.045f;
            }
            totalDur = raiseDur + holdDur + slashDur + recoverDur;
        }

        public override void AI() {
            if (Item.type != ItemID.IronBroadsword || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }

            if (timer == 0) {
                InitStage();
            }

            //命中顿帧：timer 冻结，刀角体态一起停
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
            sweepDamageActive = phase == PhaseSlash
                && slashProgress <= 0.9f
                && MathF.Abs(mainAngle - lastAngle) > 0.004f;
            UpdatePose(phase);
            HandlePhaseEvents(phase);
            HandleParticles(phase);

            Lighting.AddLight(Vector2.Lerp(Hand, mainTip, 0.7f)
                , GsIronBroadsword.IronMain.ToVector3() * (0.5f * fanFade));

            //顿帧从收势尾巴等量扣回，命中不延长真实冷却
            int effectiveTotal = Math.Max(raiseDur + holdDur + slashDur + 4, totalDur - hitstopSpent);
            if (timer >= effectiveTotal) {
                Projectile.Kill();
            }
        }

        /// <summary>斩切行程曲线：爆发过冲 4.5% 再回坐</summary>
        private static float SwingCurve(float p) {
            const float burstEnd = 0.56f;
            const float overshoot = 1.045f;
            if (p < burstEnd) {
                return overshoot * SmoothStep01(p / burstEnd);
            }
            return MathHelper.Lerp(overshoot, 1f, SmoothStep01((p - burstEnd) / (1f - burstEnd)));
        }

        private void UpdateBladeTransform(int phase) {
            float arcStart = ArcStart;
            float heldAngle = arcStart - (swingDir * 0.08f);

            switch (phase) {
                case PhaseRaise: {
                    float p = timer / (float)raiseDur;
                    float eased = 1f - MathF.Pow(1f - p, 3f);
                    float liftFrom = arcStart + (swingDir * raiseBack * 0.68f);
                    mainAngle = MathHelper.Lerp(liftFrom, arcStart, eased);
                    mainReach = FullReach * MathHelper.Lerp(0.58f, 0.92f, eased);
                    slashProgress = 0f;
                    break;
                }
                case PhaseHold: {
                    float p = (timer - raiseDur) / (float)holdDur;
                    mainAngle = MathHelper.Lerp(arcStart, heldAngle, EaseOutQuad(p));
                    mainReach = FullReach * MathHelper.Lerp(0.92f, 0.96f, EaseOutQuad(p));
                    slashProgress = 0f;
                    break;
                }
                case PhaseSlash: {
                    float p = (timer - raiseDur - holdDur) / (float)slashDur;
                    slashProgress = p;
                    mainAngle = MathHelper.Lerp(heldAngle, ArcEnd, SwingCurve(p));
                    mainReach = FullReach * (0.96f + 0.04f * MathF.Sin(MathHelper.Clamp(p * 1.8f, 0f, 1f) * MathHelper.Pi));
                    break;
                }
                default: {
                    float q = (timer - raiseDur - holdDur - slashDur) / (float)recoverDur;
                    float settle = EaseOutQuad(Math.Min(1f, q * 2.2f));
                    mainAngle = ArcEnd + (swingDir * 0.09f * (1f - settle));
                    mainReach = FullReach * MathHelper.Lerp(0.96f, 0.82f, q * q);
                    slashProgress = 1f;
                    float fadeDur = MathF.Max(4f, recoverDur * 0.7f);
                    fanFade = MathHelper.Clamp(1f - ((timer - raiseDur - holdDur - slashDur) / fadeDur), 0f, 1f);
                    break;
                }
            }

            mainTip = Hand + (mainAngle.ToRotationVector2() * mainReach);
        }

        public override bool? CanDamage() => sweepDamageActive ? null : false;

        /// <summary>持械姿态，体态收势后仰爆发前甩</summary>
        private void UpdatePose(int phase) {
            Owner.ChangeDir(facingDir);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = (mainAngle.ToRotationVector2() * Owner.direction).ToRotation();

            Player.CompositeArmStretchAmount stretch = phase is PhaseRaise or PhaseRecover
                ? Player.CompositeArmStretchAmount.ThreeQuarters
                : Player.CompositeArmStretchAmount.Full;
            Owner.SetCompositeArmFront(true, stretch, mainAngle - MathHelper.PiOver2);

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

        public override void OnKill(int timeLeft) {
            if (bodyLeanApplied && Owner.active) {
                Owner.fullRotation = 0f;
                bodyLeanApplied = false;
            }
        }

        private void HandlePhaseEvents(int phase) {
            //终结拍蓄力完成的瞬间刃身闪一记
            if (IsFinisher && timer == raiseDur + 1) {
                flashTimer = 7;
            }

            if (phase == PhaseSlash && !slashSoundPlayed) {
                slashSoundPlayed = true;
                flashTimer = Math.Max(flashTimer, 5);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = IsFinisher ? -0.28f : -0.08f }, Owner.Center);
                    if (IsFinisher) {
                        SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = -0.45f }, Owner.Center);
                    }
                }
            }

            //终结斩的体术前压：爆发首帧沿出手向踏半步（owner 端权威，位置随原版同步）
            if (IsFinisher && !lungeApplied && phase == PhaseSlash) {
                lungeApplied = true;
                if (Owner.whoAmI == Main.myPlayer && !Owner.mount.Active) {
                    Owner.velocity.X += facingDir * 3.2f;
                }
            }
        }

        /// <summary>粒子演出：斩切期沿切线甩钢灰火星，终结拍收势补两粒余烬</summary>
        private void HandleParticles(int phase) {
            if (VaultUtils.isServer) {
                return;
            }
            if (phase == PhaseSlash) {
                Vector2 sweepVel = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
                int count = IsFinisher ? 2 : 1;
                for (int i = 0; i < count; i++) {
                    Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.6f, 1.0f));
                    Color c = Main.rand.NextBool(3) ? GsIronBroadsword.IronHot : GsIronBroadsword.IronBright;
                    PRTLoader.NewParticle<PRT_Spark>(at, sweepVel * Main.rand.NextFloat(3.5f, 7f), c
                        , Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(12, 20));
                }
            }
            else if (phase == PhaseRecover && IsFinisher && timer % 3 == 0 && fanFade > 0.2f) {
                Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 0.95f));
                PRTLoader.NewParticle<PRT_Spark>(at, new Vector2(0f, -Main.rand.NextFloat(0.4f, 1f))
                    , GsIronBroadsword.IronHot, Main.rand.NextFloat(0.25f, 0.4f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        /// <summary>贪婪判定：本帧扫过的角度区间逐段采样，贴身段单独兜一次</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!sweepDamageActive) {
                return false;
            }
            Rectangle greedyBox = targetHitbox;
            greedyBox.Inflate(8, 8);
            Vector2 hand = Hand;
            if (greedyBox.Distance(hand) <= 42f) {
                return true;
            }

            float delta = mainAngle - lastAngle;
            float reach = mainReach * 1.04f + 10f;
            int steps = Math.Clamp((int)MathF.Ceiling(MathF.Abs(delta) * reach / 30f), 1, 18);
            float collisionPoint = 0f;
            for (int i = 0; i <= steps; i++) {
                float ang = MathHelper.Lerp(lastAngle, mainAngle, i / (float)steps);
                Vector2 tip = hand + ang.ToRotationVector2() * reach;
                if (Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size()
                    , hand, tip, 40f, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        public override void CutTiles() {
            if (!sweepDamageActive) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            Vector2 hand = Hand;
            const int samples = 2;
            for (int i = 0; i <= samples; i++) {
                float ang = MathHelper.Lerp(lastAngle, mainAngle, i / (float)samples);
                Vector2 tip = hand + ang.ToRotationVector2() * (mainReach * 1.02f);
                Utils.PlotTileLine(hand, tip, 34f, DelegateMethods.CutTiles);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = facingDir;//击退跟出手朝向

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //本次挥砍对同一目标只转发一次外部命中钩子（模拟物品直击链，喂饰品与神赋）
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }

            //命中顿帧一拍只吃一次，扣回额度记账
            if (!hitstopApplied && CurrentPhase == PhaseSlash) {
                hitstopApplied = true;
                int stop = IsFinisher ? 2 : 1;
                hitstopTimer = stop;
                hitstopSpent = stop;
            }

            if (!VaultUtils.isServer) {
                bool steel = CWRLoad.NPCValue.ISTheofSteel(target);
                Vector2 aimDir = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
                SpawnHitBurst(target.Center, aimDir, IsFinisher ? 1f : 0.55f, steel);
            }
        }

        /// <summary>命中材质分流：钢质弹跳钢屑，血肉钢灰火星+原版血尘垫底</summary>
        private static void SpawnHitBurst(Vector2 pos, Vector2 aimDir, float power, bool steel) {
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero
                , steel ? GsIronBroadsword.IronHot : GsIronBroadsword.IronBright, 0.16f + power * 0.10f)
                ?.Configure(10, 0.8f);
            int sparks = 4 + (int)(power * 4f);
            for (int i = 0; i < sparks; i++) {
                Vector2 vel = aimDir.RotatedByRandom(0.7) * Main.rand.NextFloat(3.5f, 8f + power * 4f);
                Color c = steel
                    ? (Main.rand.NextBool() ? GsIronBroadsword.IronHot : GsIronBroadsword.IronBright)
                    : (Main.rand.NextBool(3) ? GsIronBroadsword.IronHot : GsIronBroadsword.IronBright);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(true, Main.rand.Next(14, 24));
            }
            if (!steel) {
                for (int i = 0; i < 3; i++) {
                    Dust d = Dust.NewDustPerfect(pos, DustID.Blood
                        , aimDir.RotatedByRandom(0.9) * Main.rand.NextFloat(1.5f, 3.5f), 100, default, Main.rand.NextFloat(0.9f, 1.3f));
                    d.noGravity = Main.rand.NextBool();
                }
            }
        }

        private static float EaseOutQuad(float t) => 1f - ((1f - t) * (1f - t));
        private static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        //==================== 绘制（全用原版物品贴图） ====================

        public override bool PreDraw(ref Color lightColor) {
            DrawSmearArc(Main.spriteBatch);
            DrawBladeSet(Main.spriteBatch, lightColor);
            return false;
        }

        /// <summary>紧凑刀光：双层弧形涂抹贴图沿刀角走（加色 A=0），斩切亮收势蚀散</summary>
        private void DrawSmearArc(SpriteBatch sb) {
            if (slashProgress <= 0.02f || fanFade <= 0.02f) {
                return;
            }
            Texture2D wave = CWRAsset.SemiCircularSmear?.Value;
            if (wave == null) {
                return;
            }
            float alpha = fanFade * (0.30f + slashProgress * 0.35f);
            Vector2 arcCenter = Hand + (mainAngle.ToRotationVector2() * mainReach * 0.55f) - Main.screenPosition;
            float rot = mainAngle + (swingDir * 0.35f);
            Color c = GsIronBroadsword.IronBright * alpha;
            c.A = 0;
            sb.Draw(wave, arcCenter, null, c, rot, wave.Size() / 2f
                , new Vector2(0.46f, 0.22f) * (mainReach / 118f), SpriteEffects.None, 0f);
            Color c2 = (IsFinisher ? GsIronBroadsword.IronHot : GsIronBroadsword.IronMain) * (alpha * 0.7f);
            c2.A = 0;
            sb.Draw(wave, arcCenter, null, c2, rot, wave.Size() / 2f
                , new Vector2(0.42f, 0.10f) * (mainReach / 118f), SpriteEffects.None, 0f);
        }

        /// <summary>残影+暗影垫底+本体+终结灼橙辉光</summary>
        private void DrawBladeSet(SpriteBatch sb, Color lightColor) {
            Main.instance.LoadItem(ItemID.IronBroadsword);
            Texture2D tex = TextureAssets.Item[ItemID.IronBroadsword].Value;
            Vector2 origin = tex.Size() / 2f;
            GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset);
            float scale = mainReach * (BladeTipFill - BladePark) * 2f / MathF.Max(new Vector2(tex.Width, tex.Height).Length(), 1f);
            Vector2 hand = Hand;

            //斩切期姿态残影，最近的最亮
            if (CurrentPhase == PhaseSlash && slashProgress > 0.10f) {
                int ghostCount = IsFinisher ? 3 : 2;
                float ghostSpacing = IsFinisher ? 0.24f : 0.18f;
                for (int g = ghostCount; g >= 1; g--) {
                    float ghostAngle = mainAngle - (swingDir * ghostSpacing * g);
                    float ghostAlpha = g switch { 1 => 0.34f, 2 => 0.18f, _ => 0.08f };
                    Color ghost = GsIronBroadsword.IronBright * ghostAlpha;
                    ghost.A = 0;
                    Vector2 gPos = hand + (ghostAngle.ToRotationVector2() * mainReach * BladePark) - Main.screenPosition;
                    sb.Draw(tex, gPos, null, ghost, ghostAngle + rotOffset, origin, scale, effect, 0);
                }
            }

            Vector2 drawPos = hand + (mainAngle.ToRotationVector2() * mainReach * BladePark) - Main.screenPosition;

            //铁影垫底
            Color shadow = new Color(14, 14, 20, 190) * 0.5f;
            sb.Draw(tex, drawPos + new Vector2(facingDir, 2f), null, shadow, mainAngle + rotOffset, origin, scale * 1.02f, effect, 0);

            sb.Draw(tex, drawPos, null, lightColor, mainAngle + rotOffset, origin, scale, effect, 0);

            //终结拍与蓄力闪的灼橙辉光
            float flash = flashTimer / 7f;
            if (IsFinisher || flash > 0.01f) {
                Color glow = GsIronBroadsword.IronHot * (0.22f + flash * 0.45f);
                glow.A = 0;
                sb.Draw(tex, drawPos, null, glow, mainAngle + rotOffset, origin, scale * 1.04f, effect, 0);
            }
        }

        /// <summary>反向拍翻刃：刃口镜像到挥动前缘，双向朝向都要读得对</summary>
        private void GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset) {
            bool edgeFlip = (Projectile.ai[1] >= 0f ? 1 : -1) * facingDir < 0;
            bool flipVertically = (facingDir < 0) != edgeFlip;
            effect = flipVertically ? SpriteEffects.FlipVertically : SpriteEffects.None;
            rotOffset = flipVertically ? -MathHelper.PiOver4 : MathHelper.PiOver4;
        }
    }
}
