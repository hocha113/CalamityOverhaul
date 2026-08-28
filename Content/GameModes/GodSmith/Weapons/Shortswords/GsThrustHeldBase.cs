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

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords
{
    /// <summary>
    /// 突刺武器方案共享层（短剑/长矛两族共用，骑枪除外）。<br/>
    /// 负责标准接管流：手持弹幕在场即冷却、myPlayer 守门生成、连段计数与断拍衰减。
    /// 连段字段是跨玩家共享单例的瞬时状态，只在本地玩家路径消费
    /// </summary>
    internal abstract class GsThrustScheme : GodSmithScheme
    {
        /// <summary>手持突刺弹幕类型（子类返回 ModContent.ProjectileType&lt;GsXxxHeld&gt;()）</summary>
        protected abstract int HeldProjType { get; }

        /// <summary>连段拍数，1 = 无连段</summary>
        protected virtual int ComboBeats => 1;

        /// <summary>断手回第一拍的帧数</summary>
        protected virtual int ComboResetFrames => 48;

        /// <summary>连段计数，只在 myPlayer 路径消费</summary>
        protected int comboCounter;
        /// <summary>断拍倒计时，只在 myPlayer 路径消费</summary>
        protected int comboResetTimer;

        public override bool? GsCanUseItem(Item item, Player player) {
            //手持弹幕在场即攻击冷却（真实冷却 = 弹幕总帧，吃攻速）
            if (player.ownedProjectileCounts[HeldProjType] > 0) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                int beat = ComboBeats > 1 ? comboCounter % ComboBeats : 0;
                comboCounter++;
                comboResetTimer = ComboResetFrames;
                SpawnHeld(item, player, beat);
            }
            //全端返回 false 压掉原版使用（含原版短剑/长矛弹幕）；远端靠弹幕同步看动作
            return false;
        }

        /// <summary>生成手持突刺（仅 myPlayer 路径进入）。ai0=拍号，ai1 由 <see cref="SpawnAi1"/> 提供</summary>
        protected virtual void SpawnHeld(Item item, Player player, int beat) {
            Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                HeldProjType, player.GetWeaponDamage(item), item.knockBack, player.whoAmI, beat, SpawnAi1(item, player));
        }

        /// <summary>随生成传入 ai1 的武器自定义参数（节奏层数等）</summary>
        protected virtual float SpawnAi1(Item item, Player player) => 0f;

        public override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (comboResetTimer > 0 && --comboResetTimer == 0) {
                comboCounter = 0;
                OnComboReset();
            }
        }

        /// <summary>断拍回第一拍时（myPlayer 路径），节奏类签名在此清账</summary>
        protected virtual void OnComboReset() { }
    }

    /// <summary>
    /// 突刺手持共享骨架（短剑/长矛两族共用）：出-驻-回三相时间线
    /// （出相含回拉蓄势与爆发刺出两段）+ 刺尖贪婪判定 + 攻速缩放 + 命中顿帧。<br/>
    /// 可选蓄力：<see cref="MaxChargeFrames"/> &gt; 0 时按住左键在蓄势末驻留蓄力，
    /// 松手或满蓄放刺（蓄力长刺范式）。<br/>
    /// ai[0]=拍号（语义由武器定义），ai[1]=武器自定义参数
    /// </summary>
    internal abstract class GsThrustHeldBase : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override LocalizedText DisplayName =>
            TargetItemType < ItemID.Count
                ? Language.GetText("ItemName." + ItemID.Search.GetName(TargetItemType))
                : base.DisplayName;

        //==================== 子类必填 ====================

        /// <summary>目标物品 ID，换武器即自杀</summary>
        protected abstract int TargetItemType { get; }

        //==================== 相位时间线（逻辑帧，实际按攻速缩放） ====================

        protected const int PhaseWindup = 0;
        protected const int PhaseThrust = 1;
        protected const int PhaseDwell = 2;
        protected const int PhaseRecover = 3;

        /// <summary>回拉蓄势帧</summary>
        protected virtual float WindupFrames => 4f;
        /// <summary>爆发刺出帧（越短越猛）</summary>
        protected virtual float ThrustFrames => 4f;
        /// <summary>驻相帧：刺尖定格的静谷，衬出爆发</summary>
        protected virtual float DwellFrames => 3f;
        /// <summary>收回帧，死得温柔</summary>
        protected virtual float RecoverFrames => 7f;

        protected float TotalFrames => WindupFrames + ThrustFrames + DwellFrames + RecoverFrames;

        //==================== 几何参数 ====================

        /// <summary>静息持距（手→刀根）</summary>
        protected virtual float RestHoldout => 8f;
        /// <summary>回拉深度</summary>
        protected virtual float PullbackDist => 10f;
        /// <summary>突刺顶点突出距离（不含蓄力加成）</summary>
        protected virtual float StabReach => 34f;
        /// <summary>持握点→刃尖长度（判定与贴图共用）</summary>
        protected virtual float BladeLength => 46f;
        /// <summary>刺线判定宽度</summary>
        protected virtual float CollisionWidth => 26f;
        /// <summary>刺尖贪婪圆半径（尖端额外兜一圈）</summary>
        protected virtual float TipGreedRadius => 24f;
        /// <summary>贴身救济半径：贴脸也要能刺中</summary>
        protected virtual float PointBlankRadius => 34f;
        /// <summary>刺出 ease-out 幂，越大首帧越暴力</summary>
        protected virtual float ThrustEasePower => 5.5f;
        /// <summary>刺出过冲比例，顶点先冲过头再回坐（硬停顿感）</summary>
        protected virtual float OvershootRatio => 1.035f;
        /// <summary>贴图对角线上刃身占比（换算绘制缩放）</summary>
        protected virtual float BladeTexFill => 0.9f;
        /// <summary>是否双手持（长矛补后手臂姿）</summary>
        protected virtual bool TwoHanded => false;
        /// <summary>体态倾斜幅度，0 = 关</summary>
        protected virtual float LeanAmp => 0.03f;
        /// <summary>碰撞箱边长</summary>
        protected virtual int HitboxSize => 36;

        //==================== 蓄力（可选） ====================

        /// <summary>最大蓄力帧（真实帧），0 = 无蓄力</summary>
        protected virtual float MaxChargeFrames => 0f;
        /// <summary>蓄力进度 0~1</summary>
        protected float ChargeT => MaxChargeFrames > 0f ? Math.Clamp(chargeFrames / MaxChargeFrames, 0f, 1f) : 0f;

        //==================== 反馈参数 ====================

        /// <summary>命中顿帧数（真实帧），从收势尾巴等量扣回</summary>
        protected virtual int HitstopFrames => 2;
        /// <summary>色板：亮缘色（残影/速度线）</summary>
        protected virtual Color EdgeColor => new(222, 226, 232);
        /// <summary>色板：能量/核心色（辉光/蓄力）</summary>
        protected virtual Color CoreColor => new(255, 200, 120);

        //==================== 运行时状态 ====================

        protected Vector2 stabUnit;
        protected int facingDir = 1;
        protected float speedMul = 1f;
        /// <summary>当前持距（手→刀根），相位机每帧写入</summary>
        protected float holdout;
        /// <summary>蓄力后的顶点距离乘子，子类在 OnChargeRelease 里写</summary>
        protected float reachChargeMul = 1f;
        /// <summary>生成时的基础伤害快照，蓄力伤害以此为基</summary>
        protected int BaseDamage { get; private set; }

        private float elapsed;
        private float chargeFrames;
        private bool chargeReleased;
        private bool thrustStarted;
        private bool dwellStarted;
        private int hitstopTimer;
        private float hitstopSpent;
        private bool hitstopApplied;
        private float flashTimer;
        private float fanFade = 1f;
        private float bodyLean;
        private bool bodyLeanApplied;
        private readonly HashSet<int> hitNPCs = [];

        protected int ComboStage => (int)Projectile.ai[0];
        protected float WeaponParam => Projectile.ai[1];
        protected Vector2 Hand => Owner.GetPlayerStabilityCenter();
        /// <summary>刺尖世界坐标</summary>
        protected Vector2 TipPos => Hand + stabUnit * (holdout + BladeLength);
        /// <summary>蓄力加成后的顶点距离</summary>
        protected float ReachNow => StabReach * reachChargeMul;
        protected float Elapsed => elapsed;
        protected float FlashT => flashTimer / 8f;
        protected float FanFade => fanFade;

        protected int CurrentPhase {
            get {
                if (elapsed < WindupFrames) {
                    return PhaseWindup;
                }
                if (elapsed < WindupFrames + ThrustFrames) {
                    return PhaseThrust;
                }
                if (elapsed < WindupFrames + ThrustFrames + DwellFrames) {
                    return PhaseDwell;
                }
                return PhaseRecover;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = HitboxSize;
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

        public override void Initialize() {
            stabUnit = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
            float cos = stabUnit.X;
            facingDir = MathF.Abs(cos) < 0.05f ? Owner.direction : Math.Sign(cos);
            ModifyStabDirection(ref stabUnit);

            speedMul = Owner.GetWeaponAttackSpeed(Item);
            if (speedMul <= 0f) {
                speedMul = 1f;
            }
            BaseDamage = Projectile.damage;
            OnInit();
        }

        /// <summary>拍号微调出手方向（高低线交替等），facingDir 已就绪</summary>
        protected virtual void ModifyStabDirection(ref Vector2 unit) { }

        /// <summary>初始化尾钩：按拍号写伤害/顿帧等（stabUnit/speedMul/BaseDamage 已就绪）</summary>
        protected virtual void OnInit() { }

        public override void AI() {
            if (Item.type != TargetItemType || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 90;

            //命中顿帧：时间线冻结，姿态一起停
            if (hitstopTimer > 0) {
                hitstopTimer--;
            }
            else {
                AdvanceTimeline();
            }
            if (flashTimer > 0f) {
                flashTimer--;
            }

            int phase = CurrentPhase;
            UpdateHoldout(phase);
            UpdatePose(phase);
            OnTick(phase);

            Lighting.AddLight(Vector2.Lerp(Hand, TipPos, 0.8f), CoreColor.ToVector3() * (0.32f * fanFade));

            //顿帧从收势尾巴等量扣回，命中不延长真实冷却；只有 owner 有权收刀，远端等击杀包
            float effectiveTotal = MathF.Max(WindupFrames + ThrustFrames + DwellFrames + 1f,
                TotalFrames - hitstopSpent * speedMul);
            if (elapsed >= effectiveTotal && Projectile.IsOwnedByLocalPlayer()) {
                Projectile.Kill();
            }
        }

        /// <summary>时间线推进：攻速缩放 + 蓄势末蓄力驻留</summary>
        private void AdvanceTimeline() {
            if (MaxChargeFrames > 0f && !chargeReleased && elapsed + speedMul >= WindupFrames) {
                if (DownLeft && chargeFrames < MaxChargeFrames) {
                    //按住不放：钉在蓄势末，积攒蓄力
                    elapsed = MathF.Max(elapsed, WindupFrames - 0.01f);
                    chargeFrames++;
                    OnChargingTick();
                    return;
                }
                chargeReleased = true;
                OnChargeRelease();
            }
            elapsed += speedMul;
        }

        /// <summary>蓄力期每真实帧（收束粒子/低鸣）</summary>
        protected virtual void OnChargingTick() { }

        /// <summary>放刺瞬间（写 reachChargeMul、按 ChargeT 改 Projectile.damage）</summary>
        protected virtual void OnChargeRelease() { }

        /// <summary>相位机：出（回拉→爆发过冲）- 驻（定格回坐）- 回（温柔收刀）</summary>
        private void UpdateHoldout(int phase) {
            switch (phase) {
                case PhaseWindup: {
                    float t = MathHelper.Clamp(elapsed / WindupFrames, 0f, 1f);
                    holdout = MathHelper.Lerp(RestHoldout, -PullbackDist, MathF.Sin(t * MathHelper.PiOver2));
                    break;
                }
                case PhaseThrust: {
                    if (!thrustStarted) {
                        thrustStarted = true;
                        flashTimer = MathF.Max(flashTimer, 6f);
                        OnThrustBurst();
                    }
                    float t = (elapsed - WindupFrames) / ThrustFrames;
                    float eased = 1f - MathF.Pow(1f - MathHelper.Clamp(t, 0f, 1f), ThrustEasePower);
                    holdout = MathHelper.Lerp(-PullbackDist, ReachNow * OvershootRatio, eased);
                    break;
                }
                case PhaseDwell: {
                    if (!dwellStarted) {
                        dwellStarted = true;
                        OnDwellStart();
                    }
                    //过冲回坐后硬停：静谷衬爆发
                    float t = (elapsed - WindupFrames - ThrustFrames) / DwellFrames;
                    float settle = MathHelper.Clamp(t * 2.5f, 0f, 1f);
                    holdout = MathHelper.Lerp(ReachNow * OvershootRatio, ReachNow, settle);
                    break;
                }
                default: {
                    float t = MathHelper.Clamp((elapsed - WindupFrames - ThrustFrames - DwellFrames) / RecoverFrames, 0f, 1f);
                    holdout = MathHelper.Lerp(ReachNow, RestHoldout, t * t * (3f - 2f * t));
                    fanFade = MathHelper.Clamp(1f - t * 1.4f, 0f, 1f);
                    break;
                }
            }
        }

        /// <summary>刺出爆发帧（音效/爆发粒子/体术前压）。默认一记快刺音 + 两粒方向火花</summary>
        protected virtual void OnThrustBurst() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.7f, Pitch = ThrustPitch }, Owner.Center);
            for (int i = 0; i < 2; i++) {
                Vector2 at = Vector2.Lerp(Hand, TipPos, Main.rand.NextFloat(0.5f, 0.95f));
                PRTLoader.NewParticle<PRT_Spark>(at, stabUnit * Main.rand.NextFloat(4f, 8f),
                    EdgeColor, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        /// <summary>刺出音调（短剑清脆上挑，长矛低沉）</summary>
        protected virtual float ThrustPitch => 0.12f;

        /// <summary>驻相开始（尖端定格瞬间）</summary>
        protected virtual void OnDwellStart() { }

        /// <summary>每帧尾钩：武器自有粒子/驻场逻辑（粒子守 !VaultUtils.isServer）</summary>
        protected virtual void OnTick(int phase) { }

        /// <summary>持械姿态：臂姿 + 体态倾斜（坐骑/冲刺让位）</summary>
        private void UpdatePose(int phase) {
            Owner.ChangeDir(facingDir);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = (stabUnit * Owner.direction).ToRotation();

            float armRot = stabUnit.ToRotation() - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRot);
            if (TwoHanded) {
                Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters, armRot - facingDir * 0.30f);
            }

            Projectile.Center = Hand + stabUnit * (holdout + BladeLength * 0.5f);
            Projectile.rotation = stabUnit.ToRotation();

            if (LeanAmp <= 0f || hitstopTimer > 0) {
                return;
            }
            float chargeDeep = 1f + ChargeT * 0.8f;
            (float target, float rate) = phase switch {
                PhaseWindup => (-facingDir * LeanAmp * 0.8f * chargeDeep, 0.25f),
                PhaseThrust => (facingDir * LeanAmp * 1.4f, 0.65f),
                PhaseDwell => (facingDir * LeanAmp, 0.35f),
                _ => (0f, 0.16f),
            };
            bodyLean = MathHelper.Lerp(bodyLean, target, rate);
            ApplyBodyLean();
        }

        /// <summary>体态倾斜钉脚底，坐骑/冲刺旋转让位</summary>
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

        //==================== 判定 ====================

        /// <summary>伤害窗：爆发刺出 + 驻相（驻相尖端仍然致命）</summary>
        public override bool? CanDamage() {
            float t = elapsed;
            return t >= WindupFrames && t <= WindupFrames + ThrustFrames + DwellFrames + 1f ? null : false;
        }

        /// <summary>贪婪判定：刺线 + 刺尖圆 + 贴身救济</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CurrentPhase is not PhaseThrust and not PhaseDwell) {
                return false;
            }
            Rectangle greedyBox = targetHitbox;
            greedyBox.Inflate(6, 6);
            Vector2 hand = Hand;
            //贴身救济：贴脸也要能刺中
            if (greedyBox.Distance(hand) <= PointBlankRadius) {
                return true;
            }
            Vector2 tip = TipPos;
            //刺尖贪婪圆
            if (greedyBox.Distance(tip) <= TipGreedRadius) {
                return true;
            }
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size(),
                hand, tip, CollisionWidth, ref collisionPoint);
        }

        public override void CutTiles() {
            if (CurrentPhase is not PhaseThrust and not PhaseDwell) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            Utils.PlotTileLine(Hand, TipPos, 28f, DelegateMethods.CutTiles);
        }

        //==================== 命中 ====================

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = stabUnit.X >= 0f ? 1 : -1;
            ModifyHitExtra(target, ref modifiers);
        }

        /// <summary>命中伤害修饰尾钩（要害/破甲等）</summary>
        protected virtual void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) { }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //本次突刺对同一目标只转发一次外部命中钩子（模拟物品直击链，喂饰品与神赋）
            bool firstOnTarget = hitNPCs.Add(target.whoAmI);
            if (firstOnTarget) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }

            //顿帧一刺只吃一次，扣回额度记账
            if (!hitstopApplied) {
                hitstopApplied = true;
                int stop = HitstopFrames;
                hitstopTimer = stop;
                hitstopSpent = stop;
            }

            OnHitTarget(target, hit, damageDone, firstOnTarget);

            if (!VaultUtils.isServer) {
                SpawnHitEffects(target, hit);
            }
        }

        /// <summary>命中尾钩（挂 buff/资源结算；owner 端执行）</summary>
        protected virtual void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) { }

        /// <summary>命中反馈：默认按材质分流，钢质弹钢屑、血肉火花+血尘（子类换识别度）</summary>
        protected virtual void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            bool steel = CWRLoad.NPCValue.ISTheofSteel(target);
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, steel ? CoreColor : EdgeColor, 0.18f)
                ?.Configure(9, 0.7f);
            int sparks = 4 + HitstopFrames;
            for (int i = 0; i < sparks; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(0.55) * Main.rand.NextFloat(3.5f, 8f);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel,
                    Main.rand.NextBool() ? CoreColor : EdgeColor, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(true, Main.rand.Next(12, 20));
            }
            if (!steel) {
                for (int i = 0; i < 2; i++) {
                    Dust d = Dust.NewDustPerfect(pos, DustID.Blood,
                        stabUnit.RotatedByRandom(0.8) * Main.rand.NextFloat(1.5f, 3.5f), 100, default, Main.rand.NextFloat(0.9f, 1.2f));
                    d.noGravity = Main.rand.NextBool();
                }
            }
        }

        //==================== 绘制（原版贴图垫底 + 自绘速度线/残影/辉光层） ====================

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            DrawUnderBlade(sb);
            DrawThrustStreak(sb);
            DrawBladeSet(sb, lightColor);
            DrawOverBlade(sb);
            return false;
        }

        /// <summary>刀身之下的武器自有层（驻场焰/领域等）</summary>
        protected virtual void DrawUnderBlade(SpriteBatch sb) { }

        /// <summary>最上层的武器自有层</summary>
        protected virtual void DrawOverBlade(SpriteBatch sb) { }

        /// <summary>突刺速度线：藏行程露停顿——行程由拉丝烟线代言（加色 A=0，无随机）</summary>
        private void DrawThrustStreak(SpriteBatch sb) {
            int phase = CurrentPhase;
            if (phase is not PhaseThrust and not PhaseDwell || fanFade <= 0.02f) {
                return;
            }
            Texture2D streak = CWRAsset.LightShot?.Value;
            if (streak == null) {
                return;
            }
            //速度线从回拉点画到当前刀根，驻相期快速蚀散
            float dwellT = phase == PhaseDwell
                ? (elapsed - WindupFrames - ThrustFrames) / MathF.Max(DwellFrames, 1f) : 0f;
            float alpha = (0.55f - dwellT * 0.45f) * fanFade;
            if (alpha <= 0.02f) {
                return;
            }
            float len = holdout + PullbackDist + BladeLength * 0.8f;
            if (len <= 8f) {
                return;
            }
            Vector2 mid = Hand + stabUnit * (len * 0.5f - PullbackDist) - Main.screenPosition;
            float rot = stabUnit.ToRotation();
            Vector2 texSize = streak.Size();
            Color c1 = EdgeColor with { A = 0 } * alpha;
            sb.Draw(streak, mid, null, c1, rot, texSize / 2f,
                new Vector2(len / texSize.X, 0.20f), SpriteEffects.None, 0f);
            Color c2 = CoreColor with { A = 0 } * (alpha * 0.75f);
            sb.Draw(streak, mid, null, c2, rot, texSize / 2f,
                new Vector2(len / texSize.X * 0.9f, 0.10f), SpriteEffects.None, 0f);
        }

        /// <summary>残影 + 暗影垫底 + 本体 + 辉光（辉光强度 = 闪帧 + 蓄力 + 子类增量）</summary>
        private void DrawBladeSet(SpriteBatch sb, Color lightColor) {
            Main.instance.LoadItem(TargetItemType);
            Texture2D tex = TextureAssets.Item[TargetItemType].Value;
            Vector2 origin = tex.Size() / 2f;
            float scale = BladeLength / MathF.Max(tex.Size().Length() * BladeTexFill, 1f);

            //贴图刃尖指向右上：沿刺向旋转；朝左翻转再补角
            float rot = stabUnit.ToRotation() + MathHelper.PiOver4;
            SpriteEffects effect = SpriteEffects.None;
            if (facingDir < 0) {
                rot += MathHelper.PiOver2;
                effect = SpriteEffects.FlipHorizontally;
            }

            Vector2 hand = Hand;
            int phase = CurrentPhase;

            //刺出/驻相前段的持距残影，最近的最亮
            if (phase is PhaseThrust or PhaseDwell && fanFade > 0.05f) {
                for (int g = 1; g <= 3; g++) {
                    float ghostHold = holdout - g * (holdout + PullbackDist) * 0.22f;
                    if (ghostHold <= -PullbackDist) {
                        continue;
                    }
                    float ghostAlpha = g switch { 1 => 0.30f, 2 => 0.16f, _ => 0.07f } * fanFade;
                    Color ghost = EdgeColor with { A = 0 } * ghostAlpha;
                    Vector2 gPos = hand + stabUnit * (ghostHold + BladeLength * 0.5f) - Main.screenPosition;
                    sb.Draw(tex, gPos, null, ghost, rot, origin, scale, effect, 0f);
                }
            }

            Vector2 drawPos = hand + stabUnit * (holdout + BladeLength * 0.5f) - Main.screenPosition;

            //暗影垫底
            Color shadow = new Color(14, 14, 20, 190) * 0.45f;
            sb.Draw(tex, drawPos + new Vector2(facingDir, 2f), null, shadow, rot, origin, scale * 1.02f, effect, 0f);

            //本体（原版贴图只当垫底本体，识别度全在自绘层）
            sb.Draw(tex, drawPos, null, lightColor, rot, origin, scale, effect, 0f);

            //辉光层：爆发闪 + 蓄力升温 + 子类增量
            float glowStrength = FlashT * 0.5f + ChargeT * 0.4f + ExtraGlowStrength();
            if (glowStrength > 0.02f) {
                Color glow = CoreColor with { A = 0 } * glowStrength;
                sb.Draw(tex, drawPos, null, glow, rot, origin, scale * 1.045f, effect, 0f);
            }
        }

        /// <summary>子类附加辉光强度（节奏层数/资源状态可视化）</summary>
        protected virtual float ExtraGlowStrength() => 0f;
    }
}
