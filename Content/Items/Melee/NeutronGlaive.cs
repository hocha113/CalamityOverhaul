using CalamityOverhaul.Common;
using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.GameSystem;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
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

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class NeutronGlaive : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "NeutronGlaive";

        /// <summary>连段计数，取模三拍</summary>
        private int comboCounter;
        /// <summary>连段重置倒计时，断手后回到第一拍</summary>
        private int comboResetTimer;

        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 16));
        }

        public override void SetDefaults() {
            Item.height = 154;
            Item.width = 154;
            Item.damage = 855;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 13;
            Item.scale = 1;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 7.5f;
            Item.UseSound = SoundID.Item60;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(13, 53, 75, 0);
            Item.rare = ItemRarityID.Red;
            Item.crit = 8;
            Item.shoot = ModContent.ProjectileType<NeutronGlaiveHeld>();
            Item.shootSpeed = 18f;
            //noMelee 会丢近战词缀，这里强行标回
            ItemOverride.ItemMeleePrefixDic[Type] = true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<NeutronStarIngot>(11)
                .AddEndgameStation()
                .DisableDecraft()
                .Register();
        }

        public override bool CanUseItem(Player player) {
            Item.UseSound = SoundID.Item60;
            if (player.altFunctionUse == 2) {
                Item.UseSound = SoundID.AbigailAttack;
            }
            return player.ownedProjectileCounts[ModContent.ProjectileType<NeutronGlaiveHeldAlt>()] == 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<NeutronGlaiveHeld>()] == 0;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override void HoldItem(Player player) {
            if (comboResetTimer > 0 && --comboResetTimer == 0) {
                comboCounter = 0;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<NeutronGlaiveHeldAlt>(), damage, knockback, player.whoAmI);
                comboCounter = 0;//右键打断连段
                return false;
            }

            int combo = comboCounter % 3;
            float swingDir = comboCounter % 2 == 0 ? 1f : -1f;
            comboCounter++;
            comboResetTimer = 60;
            Projectile.NewProjectile(source, player.Center, velocity, type
                , damage, knockback, player.whoAmI, combo, swingDir);
            return false;
        }
    }

    /// <summary>
    /// 黑域斩切主挥砍。三拍连击：两记左右交替的快斩 → 一记更深更远的终结重斩。<br/>
    /// 相位时间线 举刀 → 滞帧 → 斩切 → 收势，动作与刀光结构对齐
    /// <see cref="DivineSourceBlades.DivineSourceBladeHeld"/> 的成熟做法：
    /// 刀角直接沿弧插值，刀光是从玩家展开的扇形网格。<br/>
    /// 扇形复用 DivineSourceArc 着色器（调色板参数化，这里换中子星紫蓝色板），
    /// 背景扭曲仍走 NeutronWarp 的 GravitationalLens——这一刀的身份是把空间掰弯
    /// </summary>
    internal class NeutronGlaiveHeld : BaseHeldProj, IWarpDrawable
    {
        public override string Texture => CWRConstant.Item_Melee + "NeutronGlaive";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<NeutronGlaive>();

        private const int PhaseRaise = 0;
        private const int PhaseHold = 1;
        private const int PhaseSlash = 2;
        private const int PhaseRecover = 3;

        private const int FrameCount = 16;
        /// <summary>手→刃尖基准距离（px），终结拍乘 reachScale 放大</summary>
        private const float BaseReach = 186f;
        /// <summary>刀身贴图中心停在手→刃尖的几成处，刀柄因此落在手上略后（长柄该有的配重）</summary>
        private const float BladePark = 0.5f;
        /// <summary>刀尖顶到手→刃尖的几成，略过 1 让刀尖压在刀光外缘上而不是被色片盖住</summary>
        private const float BladeTipFill = 1.06f;

        //中子星色板：白紫前沿 / 亮蓝 / 主紫 / 暗靛拖尾
        private static readonly Color LeadPale = new(238, 232, 255);
        private static readonly Color BrightBlue = new(150, 200, 255);
        private static readonly Color NeutronViolet = new(138, 80, 255);
        private static readonly Color DeepIndigo = new(54, 30, 116);

        //阶段时长与挥砍几何，InitStage 按拍号写入（已含攻速缩放）
        private int raiseDur = 6;
        private int holdDur = 2;
        private int slashDur = 6;
        private int recoverDur = 9;
        private int totalDur;
        private float raiseBack = 2.15f;
        private float follow = 1.25f;
        private float reachScale = 1f;
        private float slashEasePow = 2.6f;
        private int fanSegments = 42;

        private float baseAngle;
        private float swingDir = 1f;
        private int facingDir = 1;
        private float mainAngle;
        private float mainReach;
        private Vector2 mainTip;
        private float slashProgress;
        private float sweepT;
        private float fanFade = 1f;
        private int flashTimer;
        private int hitstopTimer;
        private bool hitstopApplied;
        private bool waveFired;
        private bool slashSoundPlayed;
        private readonly HashSet<int> hitNPCs = [];

        /// <summary>连段拍号 0/1=交替快斩 2=终结重斩</summary>
        private int ComboStage => Math.Clamp((int)Projectile.ai[0], 0, 2);
        private bool IsFinisher => ComboStage >= 2;

        private int Timer {
            get => (int)Projectile.localAI[0];
            set => Projectile.localAI[0] = value;
        }

        private int CurrentPhase {
            get {
                if (Timer <= raiseDur) {
                    return PhaseRaise;
                }
                if (Timer <= raiseDur + holdDur) {
                    return PhaseHold;
                }
                if (Timer <= raiseDur + holdDur + slashDur) {
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
            Projectile.width = Projectile.height = 66;
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
            //ai[1] 只给交替符号，实际扫向乘上朝向，背身出刀也从背后拉到身前
            swingDir = (Projectile.ai[1] >= 0f ? 1f : -1f) * facingDir;

            float speed = Owner.GetWeaponAttackSpeed(Item);
            if (speed <= 0f) {
                speed = 1f;
            }
            int D(int frames) => Math.Max(1, (int)MathF.Round(frames / speed));

            if (IsFinisher) {
                raiseDur = D(11);
                holdDur = D(3);
                slashDur = D(8);
                recoverDur = D(14);
                raiseBack = 2.65f;
                follow = 1.5f;
                reachScale = 1.22f;
                slashEasePow = 4.2f;
                fanSegments = 56;
                Projectile.damage = (int)(Projectile.damage * 1.35f);
            }
            else {
                raiseDur = D(6);
                holdDur = D(2);
                slashDur = D(6);
                recoverDur = D(9);
                raiseBack = 2.15f;
                follow = 1.25f;
                reachScale = 1f;
                slashEasePow = 2.6f;
                fanSegments = 42;
            }
            totalDur = raiseDur + holdDur + slashDur + recoverDur;
        }

        public override void AI() {
            if (Item.type != ModContent.ItemType<NeutronGlaive>() || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }

            if (Timer == 0) {
                InitStage();
            }

            //命中顿帧：Timer 不推进，刀角、扇面、姿态一起冻住
            if (hitstopTimer > 0) {
                hitstopTimer--;
            }
            else {
                Timer++;
            }
            if (flashTimer > 0) {
                flashTimer--;
            }

            int phase = CurrentPhase;
            UpdateBladeTransform(phase);

            Owner.ChangeDir(facingDir);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Player.CompositeArmStretchAmount stretch = phase is PhaseRaise or PhaseRecover
                ? Player.CompositeArmStretchAmount.ThreeQuarters
                : Player.CompositeArmStretchAmount.Full;
            Owner.SetCompositeArmFront(true, stretch, mainAngle - MathHelper.PiOver2);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters
                , mainAngle - MathHelper.PiOver2 + (swingDir * 0.25f));

            Projectile.Center = Vector2.Lerp(Hand, mainTip, 0.6f);
            Projectile.rotation = mainAngle;

            HandlePhaseEvents(phase);
            HandleParticles(phase);

            VaultUtils.ClockFrame(ref Projectile.frame, 5, FrameCount - 1);
            Lighting.AddLight(Vector2.Lerp(Hand, mainTip, 0.7f), NeutronViolet.ToVector3() * 0.7f);

            if (Timer >= totalDur) {
                Projectile.Kill();
            }
        }

        /// <summary>由 Timer 解算刀角与手→刃尖距离，扇面进度一并从这里出</summary>
        private void UpdateBladeTransform(int phase) {
            float arcStart = ArcStart;
            float heldAngle = arcStart - (swingDir * 0.07f);

            switch (phase) {
                case PhaseRaise: {
                    float p = Timer / (float)raiseDur;
                    float eased = EaseOutCubic(p);
                    float liftFrom = arcStart + (swingDir * raiseBack * 0.75f);
                    mainAngle = MathHelper.Lerp(liftFrom, arcStart, eased);
                    mainReach = FullReach * MathHelper.Lerp(0.5f, 0.92f, eased);
                    sweepT = 0f;
                    slashProgress = 0f;
                    break;
                }
                case PhaseHold: {
                    float p = (Timer - raiseDur) / (float)holdDur;
                    mainAngle = MathHelper.Lerp(arcStart, heldAngle, EaseOutQuad(p));
                    if (IsFinisher) {
                        //蓄力微颤，静默里攒张力
                        mainAngle += swingDir * 0.018f * MathF.Sin(Timer * 1.7f);
                    }
                    mainReach = FullReach * MathHelper.Lerp(0.92f, 0.97f, EaseOutQuad(p));
                    sweepT = 0f;
                    slashProgress = 0f;
                    break;
                }
                case PhaseSlash: {
                    float p = (Timer - raiseDur - holdDur) / (float)slashDur;
                    slashProgress = p;
                    float eased = 1f - MathF.Pow(1f - p, slashEasePow);
                    mainAngle = MathHelper.Lerp(heldAngle, ArcEnd, eased);
                    mainReach = FullReach * MathHelper.Lerp(0.97f, 1f, MathF.Sin(p * MathHelper.Pi));
                    sweepT = MathHelper.Clamp(MathF.Abs((mainAngle - arcStart) / TotalSweep), 0f, 1f);
                    break;
                }
                default: {
                    float q = (Timer - raiseDur - holdDur - slashDur) / (float)recoverDur;
                    float settle = EaseOutQuad(Math.Min(1f, q * 1.8f));
                    mainAngle = ArcEnd + (swingDir * 0.2f * settle);
                    mainReach = FullReach * MathHelper.Lerp(0.97f, 0.78f, EaseInQuad(q));
                    slashProgress = 1f;
                    sweepT = 1f;
                    float fadeDur = MathF.Max(5f, recoverDur * 0.7f);
                    fanFade = MathHelper.Clamp(1f - ((Timer - raiseDur - holdDur - slashDur) / fadeDur), 0f, 1f);
                    break;
                }
            }

            mainTip = Hand + (mainAngle.ToRotationVector2() * mainReach);
        }

        private void HandlePhaseEvents(int phase) {
            //终结拍蓄力完成的瞬间，刀身闪一记
            if (IsFinisher && Timer == raiseDur + 1) {
                flashTimer = 12;
            }

            if (phase == PhaseSlash && !slashSoundPlayed) {
                slashSoundPlayed = true;
                if (IsFinisher) {
                    flashTimer = 10;
                }
                if (!VaultUtils.isServer) {
                    SoundStyle style = IsFinisher
                        ? SoundID.Item71 with { Volume = 0.9f, Pitch = -0.35f }
                        : SoundID.Item71 with { Volume = 0.6f, Pitch = 0.15f };
                    SoundEngine.PlaySound(style, Owner.Center);
                }
                if (!VaultUtils.isServer && CWRServerConfig.Instance.ScreenVibration) {
                    Vector2 punchDir = (baseAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(Owner.Center, punchDir
                        , IsFinisher ? 8f : 3f, IsFinisher ? 7f : 4.5f, IsFinisher ? 11 : 6, 1100f, FullName));
                }
            }

            //剑气等刀锋扫过瞄准线之后脱手，方向锁定出手瞄准，玩家能瞄；
            //高攻速下窗口可能整段跳过，收势期兜底保证不漏发
            if (!waveFired && (phase == PhaseSlash && slashProgress >= 0.55f || phase == PhaseRecover)) {
                waveFired = true;
                FireGravityWave();
            }
        }

        /// <summary>
        /// 引力波剑气沿出手时锁定的瞄准方向甩出去。
        /// 终结拍的巨浪更大更快、存活更久，形状升级让终结显得贵
        /// </summary>
        private void FireGravityWave() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Vector2 dir = baseAngle.ToRotationVector2();
            Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), Hand + (dir * 46f)
                , dir * (IsFinisher ? 20f : 15f)
                , ModContent.ProjectileType<NeutronGravityWave>(), Projectile.damage
                , Projectile.knockBack, Owner.whoAmI, IsFinisher ? 1.85f : 0.95f);

            if (IsFinisher && !VaultUtils.isServer && CWRServerConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(Owner.Center, dir
                    , 9f, 7f, 12, 1300f, FullName));
            }
        }

        /// <summary>星屑演出：终结蓄力时被引力拽向刀身，斩切时沿切线甩出</summary>
        private void HandleParticles(int phase) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 hand = Hand;
            switch (phase) {
                case PhaseRaise:
                case PhaseHold: {
                    if (!IsFinisher) {
                        //快斩起手极短，只点缀零星星屑
                        if (Main.rand.NextBool(3)) {
                            Vector2 at = Vector2.Lerp(hand, mainTip, Main.rand.NextFloat(0.45f, 1f));
                            PRTLoader.NewParticle<PRT_HeavenfallStar>(at, -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1f)
                                , Color.Lerp(NeutronViolet, BrightBlue, Main.rand.NextFloat())
                                , Main.rand.NextFloat(0.2f, 0.32f)).Configure(false, 10);
                        }
                        break;
                    }
                    //终结拍蓄力：星屑被拽向刀身，先压缩再释放
                    float chargeT = phase == PhaseHold ? 1f : Timer / (float)raiseDur;
                    if (phase == PhaseHold || Main.rand.NextBool(2)) {
                        Vector2 anchor = Vector2.Lerp(hand, mainTip, Main.rand.NextFloat(0.4f, 0.95f));
                        Vector2 offset = Main.rand.NextVector2CircularEdge(84f, 84f);
                        PRTLoader.NewParticle<PRT_HeavenfallStar>(anchor + offset, -offset * 0.1f
                            , Color.Lerp(BrightBlue, NeutronViolet, Main.rand.NextFloat())
                            , Main.rand.NextFloat(0.24f, 0.4f) * (0.5f + (chargeT * 0.5f))).Configure(false, 12);
                    }
                    break;
                }
                case PhaseSlash: {
                    //刃口星屑沿切线甩出
                    Vector2 sweepVel = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
                    int count = IsFinisher ? 3 : 2;
                    for (int i = 0; i < count; i++) {
                        Vector2 at = Vector2.Lerp(hand, mainTip, Main.rand.NextFloat(0.5f, 1.02f));
                        PRTLoader.NewParticle<PRT_HeavenfallStar>(at
                            , sweepVel * Main.rand.NextFloat(2.5f, 7f)
                            , Color.Lerp(NeutronViolet, BrightBlue, Main.rand.NextFloat())
                            , Main.rand.NextFloat(0.25f, 0.42f)).Configure(false, 14);
                    }
                    break;
                }
                default: {
                    if (Main.rand.NextBool(5)) {
                        Vector2 at = Vector2.Lerp(hand, mainTip, Main.rand.NextFloat(0.5f, 1f));
                        PRTLoader.NewParticle<PRT_HeavenfallStar>(at, -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.8f)
                            , NeutronViolet, Main.rand.NextFloat(0.18f, 0.3f) * fanFade).Configure(false, 10);
                    }
                    break;
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CurrentPhase != PhaseSlash) {
                return false;
            }
            Vector2 hand = Hand;
            //贴身段单独兜一次，画面重叠了却打不到最伤玩家信任
            if (targetHitbox.Distance(hand) <= 46f) {
                return true;
            }
            Vector2 tip = mainTip + (mainAngle.ToRotationVector2() * 12f);
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , hand, tip, 56f, ref collisionPoint);
        }

        public override void CutTiles() {
            if (CurrentPhase != PhaseSlash) {
                return;
            }
            Vector2 hand = Hand;
            Vector2 tip = mainTip + (mainAngle.ToRotationVector2() * 12f);
            Utils.PlotTileLine(hand, tip, 46f, DelegateMethods.CutTiles);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //击退跟出手时锁定的朝向，不跟当帧刀角，免得把敌人朝反方向推
            modifiers.HitDirectionOverride = facingDir;
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.425f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //本次挥砍对同一目标只转发一次外部命中钩子
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }

            ApplyImpactFeedback(target.Center);

            if (Projectile.numHits == 0 && Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), target.Center, Vector2.Zero
                    , ModContent.ProjectileType<NeutronExplode>(), Projectile.damage / 2, 0, Owner.whoAmI);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            ApplyImpactFeedback(target.Center);
            if (Projectile.numHits == 0 && Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), target.Center, Vector2.Zero
                    , ModContent.ProjectileType<NeutronExplode>(), Projectile.damage / 2, 0, Owner.whoAmI);
            }
        }

        /// <summary>命中顿帧一拍只吃一次；终结拍另补一记沿切线的震屏</summary>
        private void ApplyImpactFeedback(Vector2 hitPos) {
            if (!hitstopApplied && CurrentPhase == PhaseSlash) {
                hitstopApplied = true;
                hitstopTimer = IsFinisher ? 3 : 1;
            }
            if (VaultUtils.isServer || !CWRServerConfig.Instance.ScreenVibration || !IsFinisher) {
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

        /// <summary>扇形刀光画在世界层，会被自己的引力透镜轻微掰弯——光也逃不出引力</summary>
        public override bool PreDraw(ref Color lightColor) {
            DrawArcFan(Main.spriteBatch);
            return false;
        }

        /// <summary>扇形网格：外缘贴刃尖轨迹、内缘羽化，着色器沿 SweepT 追着刀锋亮</summary>
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

            float outerR = mainReach * 1.04f;
            float innerR = mainReach * 0.3f;
            Vector2 center = Hand;
            //起点补 0.3 弧度，让扇根和刀背衔接
            float arcStart = ArcStart + (swingDir * 0.3f);

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

            Trail.CalculateRenderingMatrices(out Matrix view, out Matrix projection);
            effect.Parameters["WorldViewProjection"]?.SetValue(view * projection);
            effect.Parameters["TotalTime"]?.SetValue((float)Main.GameUpdateCount / 60f);
            effect.Parameters["SweepT"]?.SetValue(sweepT);
            effect.Parameters["FadeOut"]?.SetValue(fanFade);
            effect.Parameters["HeatBoost"]?.SetValue((IsFinisher ? 1.3f : 1.1f) + (slashProgress * (IsFinisher ? 0.7f : 0.45f)));
            effect.Parameters["RimIntensity"]?.SetValue(IsFinisher ? 1.45f : 1.15f);
            effect.Parameters["LeadColor"]?.SetValue(LeadPale.ToVector4());
            effect.Parameters["GoldColor"]?.SetValue(BrightBlue.ToVector4());
            effect.Parameters["AmberColor"]?.SetValue(NeutronViolet.ToVector4());
            effect.Parameters["TailColor"]?.SetValue(DeepIndigo.ToVector4());
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

        private void DrawArcFallback(SpriteBatch sb) {
            Texture2D wave = CWRAsset.SemiCircularSmear?.Value;
            if (wave == null) {
                return;
            }
            float alpha = fanFade * (0.35f + (slashProgress * 0.45f));
            Vector2 arcCenter = Hand + (mainAngle.ToRotationVector2() * mainReach * 0.6f);
            Color c = NeutronViolet * alpha;
            c.A = 0;
            sb.Draw(wave, arcCenter - Main.screenPosition, null, c,
                mainAngle + (swingDir * 0.35f), wave.Size() / 2f, new Vector2(0.5f, 0.22f), SpriteEffects.None, 0f);
            Color c2 = BrightBlue * (alpha * 0.7f);
            c2.A = 0;
            sb.Draw(wave, arcCenter - Main.screenPosition, null, c2,
                mainAngle + (swingDir * 0.35f), wave.Size() / 2f, new Vector2(0.45f, 0.1f), SpriteEffects.None, 0f);
        }

        bool IWarpDrawable.CanDrawCustom() => true;

        bool IWarpDrawable.DontUseBlueshiftEffect() => true;

        /// <summary>引力透镜：斩切期沿已扫过的弧带把背景掰弯，强度随斩进度起落</summary>
        void IWarpDrawable.Warp() {
            if (totalDur <= 0) {
                return;
            }
            int slashEnd = raiseDur + holdDur + slashDur;
            if (Timer <= raiseDur + holdDur || Timer > slashEnd + 3) {
                return;
            }
            float decay = Timer <= slashEnd ? 1f : 1f - ((Timer - slashEnd) / 3f);
            float power = SmoothStep01(slashProgress * 2.4f) * decay;
            if (power <= 0.02f) {
                return;
            }
            Vector2 hand = Hand;
            float arcStart = ArcStart + (swingDir * 0.3f);
            int samples = IsFinisher ? 3 : 2;
            for (int i = 0; i < samples; i++) {
                float ang = MathHelper.Lerp(arcStart, mainAngle, (i + 0.5f) / samples);
                Vector2 pos = hand + (ang.ToRotationVector2() * mainReach * 0.7f);
                float span = mainReach * 0.95f;
                NeutronWarpHelper.DrawWarp(pos
                    , screenWidth: span, screenHeight: span
                    , intensity: power * (IsFinisher ? 0.6f : 0.4f)
                    , progress: slashProgress
                    , rotation: ang
                    , technique: "GravitationalLens"
                    , radius: 0.42f);
            }
        }

        /// <summary>刀身画在扭曲层之上：背景被引力掰弯，刀本身必须保持锐利</summary>
        void IWarpDrawable.DrawCustom(SpriteBatch spriteBatch) {
            if (totalDur <= 0) {
                return;
            }
            Texture2D tex = TextureValue;
            Rectangle rect = tex.GetRectangle(Projectile.frame, FrameCount);
            Vector2 origin = rect.Size() / 2f;
            GetBladeDraw(out SpriteEffects effect, out float rotOffset);
            float scale = GetBladeDrawScale(rect);
            Vector2 hand = Hand;

            //斩切期残影，最近的最亮
            if (CurrentPhase == PhaseSlash && slashProgress > 0.1f) {
                int ghostCount = IsFinisher ? 3 : 2;
                float ghostSpacing = IsFinisher ? 0.24f : 0.19f;
                for (int g = ghostCount; g >= 1; g--) {
                    float ghostAngle = mainAngle - (swingDir * ghostSpacing * g);
                    float ghostAlpha = g switch { 1 => 0.4f, 2 => 0.18f, _ => 0.08f };
                    Color ghost = NeutronViolet * ghostAlpha;
                    ghost.A = 0;
                    Vector2 gPos = hand + (ghostAngle.ToRotationVector2() * mainReach * BladePark) - Main.screenPosition;
                    spriteBatch.Draw(tex, gPos, rect, ghost, ghostAngle + rotOffset, origin, scale, effect, 0);
                }
            }

            Vector2 drawPos = hand + (mainAngle.ToRotationVector2() * mainReach * BladePark) - Main.screenPosition;
            spriteBatch.Draw(tex, drawPos, rect, Color.White, mainAngle + rotOffset, origin, scale, effect, 0);

            //终结拍叠一层加色辉光，蓄力完成的闪也走这层
            float flash = flashTimer / 12f;
            if (IsFinisher || flash > 0.01f) {
                Color glow = BrightBlue * (0.4f + (flash * 0.45f));
                glow.A = 0;
                spriteBatch.Draw(tex, drawPos, rect, glow, mainAngle + rotOffset, origin, scale * 1.04f, effect, 0);
            }
        }

        private void GetBladeDraw(out SpriteEffects effect, out float rotOffset) {
            bool flip = facingDir == -1;
            effect = flip ? SpriteEffects.FlipVertically : SpriteEffects.None;
            rotOffset = flip ? -MathHelper.PiOver4 : MathHelper.PiOver4;
        }

        /// <summary>
        /// 刀身画多大由挥砍半径反推：贴图每帧只有 78x82（<c>Item.width=154</c> 只是物品显示尺寸），
        /// 固定 scale 会让刀只剩刀光的四成，读作"小气"。
        /// 刀刃沿贴图帧对角走（绘制补了 ±PiOver4），对角长即刃轴长
        /// </summary>
        private float GetBladeDrawScale(Rectangle rect) {
            float spriteAxis = MathF.Max(new Vector2(rect.Width, rect.Height).Length(), 1f);
            return mainReach * (BladeTipFill - BladePark) * 2f / spriteAxis;
        }
    }

    /// <summary>
    /// 引力波剑气：挥砍轰出去的一道新月形时空涟漪，物品说明里的"引力波剑气"本体。<br/>
    /// 新月网格结构对齐 <see cref="DivineSourceBlades.DivineSourceWaveProjectile"/>，
    /// 着色器复用 DivineSourceCrescent（换中子星紫蓝色板）。<br/>
    /// 飞行中减速扩张、带体溶解，沿途背景被 GravitationalLens 掰弯，
    /// 星尘余痕活得比弹幕更久。ai[0] 为尺寸倍率，终结拍的巨浪存活更久
    /// </summary>
    internal class NeutronGravityWave : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 48;
        private const float BaseRadius = 150f;
        private const float ThickRatio = 0.62f;
        private const float ArcHalf = 1.95f;
        private const int Segments = 56;
        private const float SpeedDecay = 0.985f;

        //与母刀光同一套中子星色板
        private static readonly Color RimPale = new(238, 232, 255);
        private static readonly Color BrightBlue = new(150, 200, 255);
        private static readonly Color NeutronViolet = new(138, 80, 255);
        private static readonly Color DeepIndigo = new(54, 30, 116);

        private float traveled;
        private int lifetime = Lifetime;
        private bool payloadSpawned;

        private float SizeMul => Projectile.ai[0] > 0.05f ? Projectile.ai[0] : 1f;
        private bool IsGiant => SizeMul >= 1.3f;

        private int Age => lifetime - Projectile.timeLeft;
        private float LifeT => MathHelper.Clamp(Age / (float)lifetime, 0f, 1f);

        /// <summary>出生 12 帧内快速撑开，之后随寿命缓慢扩张——振幅随距离衰减的波</summary>
        private float WaveScale {
            get {
                float burst = 1f - MathF.Pow(1f - Math.Min(1f, Age / 12f), 3f);
                return (0.55f + (0.45f * burst) + (0.32f * LifeT)) * SizeMul;
            }
        }

        private float Opacity {
            get {
                float fadeIn = Math.Min(1f, Age / 4f);
                float fadeOut = 1f - SmoothStep01((LifeT - 0.7f) / 0.3f);
                return fadeIn * fadeOut;
            }
        }

        private float Dissolve => SmoothStep01((LifeT - 0.45f) / 0.55f) * 0.85f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 90;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            //穿透但每个目标只吃一次，扩张的波面不许变成多段刮伤
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        /// <summary>发射相位：波面撑开之前先有一次空间压缩，星屑向弧面收拢</summary>
        public override void OnSpawn(IEntitySource source) {
            if (VaultUtils.isServer) {
                return;
            }
            //只有终结拍加低频闷响，轻拍不叠第二个音免得和挥砍声糊在一起
            if (IsGiant) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.42f, Pitch = -0.8f }, Projectile.Center);
            }
            float rot = Projectile.velocity.ToRotation();
            float outerR = BaseRadius * WaveScale;
            for (int i = 0; i <= 12; i++) {
                float u = i / 12f;
                float theta = (u - 0.5f) * 2f * ArcHalf;
                Vector2 dir = (rot + theta).ToRotationVector2();
                Vector2 from = Projectile.Center + (dir * outerR * 1.6f);
                PRTLoader.NewParticle<PRT_HeavenfallStar>(from, -dir * Main.rand.NextFloat(2.4f, 5.2f)
                    , Color.Lerp(BrightBlue, NeutronViolet, u), Main.rand.NextFloat(0.3f, 0.5f))
                    .Configure(false, 12);
            }
        }

        public override void AI() {
            //首帧按尺寸倍率重设寿命（放 AI 而非 OnSpawn，保证多人各端一致）
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                lifetime = (int)(Lifetime * MathHelper.Clamp(SizeMul, 0.68f, 1.38f));
                Projectile.timeLeft = lifetime;
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
            traveled += Projectile.velocity.Length();
            //波在扩张中散失能量，速度持续衰减——不是匀速平移的贴图
            Projectile.velocity *= SpeedDecay;

            if (VaultUtils.isServer) {
                return;
            }

            float outerR = BaseRadius * WaveScale;
            Vector2 backDrift = -Projectile.velocity.SafeNormalize(Vector2.UnitX);

            //波面向身后渗出星尘，余痕活得比弹幕久
            int shed = IsGiant ? 2 : 1;
            for (int i = 0; i < shed; i++) {
                if (!Main.rand.NextBool(2)) {
                    continue;
                }
                float theta = Main.rand.NextFloat(-0.85f, 0.85f) * ArcHalf;
                float thick = MaxThick(outerR) * ThickProfile(theta);
                Vector2 at = Projectile.Center + ((Projectile.rotation + theta).ToRotationVector2()
                    * (outerR - (thick * Main.rand.NextFloat(0.2f, 0.9f))));
                PRTLoader.NewParticle<PRT_HeavenfallStar>(at
                    , backDrift * Main.rand.NextFloat(0.8f, 2.6f)
                    , Color.Lerp(NeutronViolet, BrightBlue, Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.22f, 0.4f)).Configure(false, 22);
            }
            //角尖偶尔溅一粒亮星
            if (Main.rand.NextBool(4)) {
                float hornSign = Main.rand.NextBool() ? 1f : -1f;
                Vector2 horn = Projectile.Center
                    + ((Projectile.rotation + (hornSign * ArcHalf)).ToRotationVector2() * outerR);
                PRTLoader.NewParticle<PRT_HeavenfallStar>(horn, backDrift * Main.rand.NextFloat(0.4f, 1.6f)
                    , RimPale, Main.rand.NextFloat(0.18f, 0.3f)).Configure(false, 16);
            }

            Lighting.AddLight(Projectile.Center + (Projectile.velocity.SafeNormalize(Vector2.Zero) * outerR * 0.5f)
                , NeutronViolet.ToVector3() * (0.85f * Opacity));
        }

        private static float MaxThick(float outerR) => outerR * ThickRatio;

        /// <summary>波峰最厚、双翼收尖的厚度包络</summary>
        private static float ThickProfile(float theta) =>
            MathF.Pow(MathF.Max(0f, MathF.Cos(theta / ArcHalf * MathHelper.PiOver2)), 0.8f);

        private static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        private void BuildCrescentMesh(Vector2 worldCenter, float rot, float outerR,
            out ColoredVertex[] verts, out short[] inds) {

            verts = new ColoredVertex[Segments * 2];
            float maxThick = MaxThick(outerR);

            for (int i = 0; i < Segments; i++) {
                float t = i / (float)(Segments - 1);
                float theta = (t - 0.5f) * 2f * ArcHalf;
                Vector2 dir = (rot + theta).ToRotationVector2();
                float thick = maxThick * ThickProfile(theta);

                Vector2 outer = worldCenter + (dir * outerR) - Main.screenPosition;
                Vector2 inner = worldCenter + (dir * (outerR - thick)) - Main.screenPosition;

                verts[i * 2] = new ColoredVertex(outer, Color.White, new Vector3(t, 0f, 0f));
                verts[(i * 2) + 1] = new ColoredVertex(inner, Color.White, new Vector3(t, 1f, 0f));
            }

            inds = new short[(Segments - 1) * 6];
            for (int i = 0; i < Segments - 1; i++) {
                int vi = i * 2;
                int ii = i * 6;
                inds[ii] = (short)vi;
                inds[ii + 1] = (short)(vi + 1);
                inds[ii + 2] = (short)(vi + 2);
                inds[ii + 3] = (short)(vi + 2);
                inds[ii + 4] = (short)(vi + 1);
                inds[ii + 5] = (short)(vi + 3);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float outerR = BaseRadius * WaveScale;
            float maxThick = MaxThick(outerR);

            const int samples = 13;
            Vector2 prev = Vector2.Zero;
            for (int i = 0; i < samples; i++) {
                float t = i / (float)(samples - 1);
                float theta = (t - 0.5f) * 2f * (ArcHalf * 0.88f);
                float thick = maxThick * ThickProfile(theta);
                Vector2 point = Projectile.Center
                    + ((Projectile.rotation + theta).ToRotationVector2() * (outerR - (thick * 0.45f)));

                if (i > 0) {
                    //擦边算中，波面看着盖住了就该打到
                    float width = MathF.Max(26f, thick * 0.7f);
                    float collisionPoint = 0f;
                    if (Collision.CheckAABBvLineCollision(
                        targetHitbox.TopLeft(), targetHitbox.Size(),
                        prev, point, width, ref collisionPoint)) {
                        return true;
                    }
                }
                prev = point;
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (payloadSpawned || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            //命中后落一次中子星爆炸，对应物品说明的载荷
            payloadSpawned = true;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero
                , ModContent.ProjectileType<NeutronExplode>(), Projectile.damage / 2, 0, Projectile.owner);
        }

        /// <summary>余痕：波散之后仍在原地慢慢消的引力残留</summary>
        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.35f, Pitch = -0.6f }, Projectile.Center);
            float outerR = BaseRadius * WaveScale;
            float maxThick = MaxThick(outerR);
            for (int i = 0; i <= 14; i++) {
                float u = i / 14f;
                float theta = (u - 0.5f) * 2f * ArcHalf;
                Vector2 dir = (Projectile.rotation + theta).ToRotationVector2();
                Vector2 at = Projectile.Center
                    + (dir * (outerR - (maxThick * ThickProfile(theta) * Main.rand.NextFloat(0f, 1f))));
                PRTLoader.NewParticle<PRT_HeavenfallStar>(at, dir * Main.rand.NextFloat(0.15f, 0.7f)
                    , Color.Lerp(NeutronViolet, BrightBlue, u), Main.rand.NextFloat(0.28f, 0.46f))
                    .Configure(false, 34);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float opacity = Opacity;
            if (opacity <= 0.01f) {
                return false;
            }
            Effect effect = EffectLoader.DivineSourceCrescent?.Value;
            if (effect == null) {
                return false;
            }
            DrawCrescentMeshes(Main.spriteBatch, effect, BaseRadius * WaveScale, opacity);
            return false;
        }

        bool IWarpDrawable.CanDrawCustom() => false;

        bool IWarpDrawable.DontUseBlueshiftEffect() => true;

        void IWarpDrawable.DrawCustom(SpriteBatch spriteBatch) { }

        /// <summary>波面沿途把背景掰弯，强度随振幅一起衰减</summary>
        void IWarpDrawable.Warp() {
            float amp = (1f - SmoothStep01(LifeT)) * (IsGiant ? 0.7f : 0.46f);
            if (amp <= 0.02f) {
                return;
            }
            float outerR = BaseRadius * WaveScale;
            int samples = IsGiant ? 3 : 2;
            for (int i = 0; i < samples; i++) {
                float theta = (((i + 0.5f) / samples) - 0.5f) * 2f * (ArcHalf * 0.8f);
                Vector2 pos = Projectile.Center
                    + ((Projectile.rotation + theta).ToRotationVector2() * (outerR * 0.72f));
                float span = outerR * 1.05f;
                NeutronWarpHelper.DrawWarp(pos
                    , screenWidth: span, screenHeight: span
                    , intensity: amp
                    , progress: LifeT
                    , rotation: Projectile.rotation + theta
                    , technique: "GravitationalLens"
                    , radius: 0.4f);
            }
        }

        /// <summary>先画 oldPos 残影再画本体，扩张的波留下一串渐淡的旧波面</summary>
        private void DrawCrescentMeshes(SpriteBatch sb, Effect effect, float outerR, float opacity) {
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

            Trail.CalculateRenderingMatrices(out Matrix view, out Matrix projection);
            effect.Parameters["WorldViewProjection"]?.SetValue(view * projection);
            effect.Parameters["TotalTime"]?.SetValue((float)Main.GameUpdateCount / 60f);
            effect.Parameters["Dissolve"]?.SetValue(Dissolve);
            effect.Parameters["RimIntensity"]?.SetValue(IsGiant ? 2.1f : 1.8f);
            effect.Parameters["StreakStrength"]?.SetValue(IsGiant ? 0.8f : 0.65f);
            effect.Parameters["FlowOffset"]?.SetValue(traveled / 480f);
            effect.Parameters["RimColor"]?.SetValue(RimPale.ToVector4());
            effect.Parameters["GoldColor"]?.SetValue(BrightBlue.ToVector4());
            effect.Parameters["OrangeColor"]?.SetValue(NeutronViolet.ToVector4());
            effect.Parameters["DeepColor"]?.SetValue(DeepIndigo.ToVector4());
            Texture2D noise = CWRAsset.Fog?.Value ?? CWRAsset.PerlinNoise?.Value;
            if (noise != null) {
                effect.Parameters["NoiseTexture"]?.SetValue(noise);
            }

            ReadOnlySpan<(int idx, float alpha, float scaleMul)> ghosts =
                [(9, 0.10f, 0.86f), (6, 0.20f, 0.92f), (3, 0.34f, 0.97f)];

            foreach ((int idx, float ghostAlpha, float scaleMul) in ghosts) {
                if (idx >= Projectile.oldPos.Length) {
                    continue;
                }
                Vector2 oldPos = Projectile.oldPos[idx];
                if (oldPos == Vector2.Zero) {
                    continue;
                }

                Vector2 oldCenter = oldPos + (Projectile.Size * 0.5f);
                float oldRot = Projectile.oldRot[idx] != 0f ? Projectile.oldRot[idx] : Projectile.rotation;

                BuildCrescentMesh(oldCenter, oldRot, outerR * scaleMul, out var gVerts, out var gInds);
                effect.Parameters["Opacity"]?.SetValue(opacity * ghostAlpha);
                effect.Parameters["Dissolve"]?.SetValue(MathHelper.Clamp(Dissolve + ((1f - ghostAlpha) * 0.35f), 0f, 1f));

                foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                    pass.Apply();
                    Trail.DrawUserPrimitives(gVerts, gInds, device);
                }
            }

            BuildCrescentMesh(Projectile.Center, Projectile.rotation, outerR, out var verts, out var inds);
            effect.Parameters["Opacity"]?.SetValue(opacity);
            effect.Parameters["Dissolve"]?.SetValue(Dissolve);

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
    }

    internal class NeutronGlaiveHeldAlt : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "NeutronGlaive";
        [VaultLoaden(CWRConstant.UI + "NeutronsBar")]
        private static Asset<Texture2D> bar1 = null;
        [VaultLoaden(CWRConstant.UI + "NeutronsBar2")]
        private static Asset<Texture2D> bar2 = null;
        [VaultLoaden(CWRConstant.UI + "NeutronsBarTop")]
        private static Asset<Texture2D> bar3 = null;
        [VaultLoaden(CWRConstant.UI + "NeutronsBarTop2")]
        private static Asset<Texture2D> bar4 = null;
        private bool canatcck;
        private bool canatcck2 = true;
        private bool canatcck3 = true;
        private int uiframe;
        private const int maxatcck = 80;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 112;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 4;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.hide = true;
        }

        public override void AI() {
            if (Owner.dead || !Owner.active || canatcck || !DownRight) {
                canatcck = true;
                if (Projectile.ai[0] >= maxatcck) {
                    Projectile.Kill();
                }
                else {
                    canatcck2 = false;
                    Projectile.scale = 1.25f;

                    if (++Projectile.ai[1] > 5) {
                        SoundEngine.PlaySound(SoundID.Item4, Projectile.Center);
                        Vector2 pos = Projectile.Center + Projectile.velocity.UnitVector() * Main.rand.Next(-52, 112);
                        int proj = Projectile.NewProjectile(Projectile.GetSource_FromThis(), pos
                        , Projectile.velocity.RotatedByRandom(0.2f), ModContent.ProjectileType<NeutronsOrb>(), Projectile.damage, 0);
                        Main.projectile[proj].SetAllProjectilesHome(true);
                        for (int i = 0; i < 4; i++) {
                            float rot1 = MathHelper.PiOver2 * i;
                            Vector2 vr = rot1.ToRotationVector2();
                            for (int j = 0; j < 13; j++) {
                                PRTLoader.NewParticle<PRT_HeavenfallStar>(pos, vr * (0.1f + j * 0.14f), Color.BlueViolet, Main.rand.NextFloat(0.5f, 0.7f)).Configure(false, 17);
                            }
                        }
                        Projectile.ai[1] = 0;
                    }

                    Projectile.ai[0]--;
                    if (Projectile.ai[0] <= 0) {
                        Projectile.Kill();
                    }
                }
            }
            if (canatcck2) {
                Projectile.velocity = ToMouse.UnitVector() * 18;
            }
            Projectile.Center = Owner.GetPlayerStabilityCenter() + Projectile.velocity.UnitVector() * 40 * Projectile.scale;
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!canatcck && Projectile.ai[0] <= maxatcck) {
                Projectile.ai[0]++;
            }
            if (Projectile.ai[0] >= maxatcck) {
                if (canatcck3) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.2f }, Projectile.Center);
                    canatcck3 = false;
                }
                Projectile.scale = 1.5f;
            }
            SetHeld();
            VaultUtils.ClockFrame(ref Projectile.frame, 5, 15);
            if (canatcck2) {
                VaultUtils.ClockFrame(ref uiframe, 5, 6);
            }
            float rot = (MathHelper.PiOver2 * SafeGravDir - Projectile.rotation) * DirSign * SafeGravDir;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rot * -DirSign);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, rot * -DirSign);
            Owner.direction = Math.Sign(Projectile.velocity.X);
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer() && canatcck2) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + Projectile.velocity.UnitVector() * 255
                    , Vector2.Zero, ModContent.ProjectileType<EXNeutronExplode>(), Projectile.damage * 10, 0);
            }
        }

        public static void DrawBar(Player Owner, float sengs, int uiframe) {
            sengs = MathHelper.Clamp(sengs, 0, maxatcck);
            if (!(sengs <= 0f)) {
                Texture2D barBG = bar3.Value;
                Texture2D barFG = bar1.Value;
                if (sengs >= maxatcck) {
                    barBG = bar4.Value;
                    barFG = bar2.Value;
                }
                float barScale = 1.2f;
                Vector2 drawPos = Owner.GetPlayerStabilityCenter() + new Vector2(0, 90) - Main.screenPosition;
                Rectangle frameCrop = new Rectangle(0, 0, (int)(sengs / maxatcck * barFG.Width), barFG.Height);
                Color color = Color.White;
                Main.spriteBatch.Draw(barBG, drawPos, barBG.GetRectangle(uiframe, 7), color, 0f, VaultUtils.GetOrig(barBG, 7), barScale, 0, 0f);
                Main.spriteBatch.Draw(barFG, drawPos + new Vector2(2, 4), frameCrop, color, 0f, VaultUtils.GetOrig(barFG, 1), barScale, 0, 0f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            DrawBar(Owner, Projectile.ai[0], uiframe);
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, value.GetRectangle(Projectile.frame, 16)
                , Color.White, Projectile.rotation + MathHelper.PiOver4 * Owner.direction, VaultUtils.GetOrig(value, 16) + new Vector2(0, 5 * Owner.direction)
                , Projectile.scale, Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically, 0);
            return false;
        }
    }

    internal class NeutronsOrb : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.timeLeft = 120;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, Projectile.velocity, Color.BlueViolet, Main.rand.NextFloat(0.2f, 0.3f)).Configure(false, 17);
        }
    }

    internal class NeutronExplode : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 200;
            Projectile.timeLeft = 20;
            Projectile.aiStyle = -1;
            Projectile.localNPCHitCooldown = 4;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
        }

        public bool CanDrawCustom() => false;

        public override void AI() {
            if (Projectile.ai[2] == 0) {
                for (int i = 0; i < 4; i++) {
                    float rot1 = MathHelper.PiOver2 * i;
                    Vector2 vr = rot1.ToRotationVector2();
                    for (int j = 0; j < 133; j++) {
                        PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vr * (0.1f + i * 0.24f), Color.BlueViolet, Main.rand.NextFloat(1.2f, 2.3f)).Configure(false, 7);
                    }
                }
                Projectile.ai[2]++;
            }
            Projectile.ai[0] += 0.25f;
            if (Projectile.timeLeft > 15) {
                Projectile.localAI[0] += 0.25f;
                Projectile.ai[1] += 0.2f;
            }
            else {
                Projectile.localAI[0] -= 0.13f;
                Projectile.ai[1] -= 0.066f;
            }

            Projectile.localAI[1] += 0.07f;
            Projectile.ai[1] = Math.Clamp(Projectile.ai[1], 0f, 1f);

            Lighting.AddLight(Projectile.Center, new Vector3(1, 1, 1));
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        public void Warp() {
            float scale = Math.Max(Projectile.localAI[0], 0.01f);
            NeutronWarpHelper.DrawWarp(
                Projectile.Center,
                screenWidth: 400f * scale,
                screenHeight: 400f * scale,
                intensity: Projectile.ai[1] * 0.85f,
                progress: Projectile.ai[1],
                rotation: Projectile.ai[0],
                technique: "GravitationalVortex"
            );
        }

        public void DrawCustom(SpriteBatch spriteBatch) { }
    }

    internal class EXNeutronExplode : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 2000;
            Projectile.timeLeft = 20;
            Projectile.aiStyle = -1;
            Projectile.localNPCHitCooldown = 4;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.DamageType = EndlessDamageClass.Instance;
        }

        public bool CanDrawCustom() => false;

        public override void AI() {
            if (Projectile.ai[2] == 0) {
                SoundEngine.PlaySound(CWRSound.Pecharge with { Pitch = -0.1f, Volume = 0.8f }, Projectile.Center);
                for (int i = 0; i < 4; i++) {
                    float rot1 = MathHelper.PiOver2 * i;
                    Vector2 vr = rot1.ToRotationVector2();
                    for (int j = 0; j < 133; j++) {
                        PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vr * (0.1f + j * 0.34f), Color.BlueViolet, Main.rand.NextFloat(2.2f, 2.3f)).Configure(false, 7);
                    }
                }
            }
            if (Projectile.ai[2] % 6 == 0) {
                float randvalue = Main.rand.NextFloat(MathHelper.TwoPi);
                float randvalue2 = Main.rand.NextFloat(0.3f, 1.6f);
                for (int z = 0; z < 4; z++) {
                    Vector2 rand = (MathHelper.PiOver2 * z + randvalue).ToRotationVector2() * 130 * randvalue2;
                    for (int i = 0; i < 4; i++) {
                        float rot1 = MathHelper.PiOver2 * i;
                        Vector2 vr = rot1.ToRotationVector2();
                        for (int j = 0; j < 33; j++) {
                            PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center + rand, vr * 0.24f, Color.CadetBlue, Main.rand.NextFloat(0.9f, 1.3f)).Configure(false, 13);
                        }
                    }
                }
            }
            Projectile.ai[0] += 0.25f;
            if (Projectile.timeLeft > 15) {
                Projectile.localAI[0] += 0.25f;
                Projectile.ai[1] += 0.2f;
            }
            else {
                Projectile.localAI[0] -= 0.13f;
                Projectile.ai[1] -= 0.066f;
            }

            Projectile.localAI[1] += 0.07f;
            Projectile.ai[1] = Math.Clamp(Projectile.ai[1], 0f, 1f);
            Projectile.ai[2]++;
            Lighting.AddLight(Projectile.Center, new Vector3(1, 1, 1));
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        public void Warp() {
            float scale = Math.Max(Projectile.localAI[0], 0.01f);
            NeutronWarpHelper.DrawWarp(
                Projectile.Center,
                screenWidth: 1200f * scale,
                screenHeight: 1200f * scale,
                intensity: Projectile.ai[1] * 1.0f,
                progress: Projectile.ai[1],
                rotation: Projectile.ai[0],
                technique: "GravitationalVortex",
                radius: 0.48f
            );
        }

        public void DrawCustom(SpriteBatch spriteBatch) { }
    }
}
