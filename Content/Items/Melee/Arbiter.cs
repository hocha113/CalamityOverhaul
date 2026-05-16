using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 断罪师 —— 一柄沉重的大斧
    /// 左键长按蓄力高举，松开后猛地劈下，撞击地面时向左右两侧释放沿地形蔓延的火焰冲击波
    /// 火焰会严格贴合地面形状绵延，并在地面上持续燃烧一段时间
    /// </summary>
    internal class Arbiter : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "Arbiter";

        public override void SetDefaults() {
            Item.width = 60;
            Item.height = 60;
            Item.damage = 620;
            Item.DamageType = DamageClass.Melee;
            //蓄力斧本体的"使用动画"实际由手持弹幕全权处理，这里把节奏调得很短只是为了允许玩家触发
            Item.useAnimation = 12;
            Item.useTime = 12;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTurn = false;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            //channel = true 让玩家按住左键时 player.channel 持续为 true，便于手持弹幕检测蓄力释放时机
            Item.channel = true;
            Item.knockBack = 13f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.shootSpeed = 1f;
            Item.value = Item.buyPrice(3, 75, 0, 0);
            Item.rare = ItemRarityID.Red;
            Item.shoot = ModContent.ProjectileType<ArbiterHeld>();
            Item.crit = 6;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 4;

        //场上只允许一个 ArbiterHeld 实例，避免玩家狂按重新进入蓄力
        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, player.Center, Vector2.Zero, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    /// <summary>
    /// 断罪师手持弹幕：完整接管玩家手臂动作、蓄力 → 劈砍 → 收手 → 自毁的全过程
    /// </summary>
    internal class ArbiterHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "Arbiter";

        /// <summary>蓄力阶段：玩家高举斧头</summary>
        public const int PhaseCharging = 0;
        /// <summary>劈砍阶段：斧头快速下劈</summary>
        public const int PhaseSlamming = 1;
        /// <summary>收手阶段：劈砍完成后短暂保持姿势</summary>
        public const int PhaseRecovering = 2;

        /// <summary>蓄力可达的最大帧数，超过即视为满蓄</summary>
        private const int MaxChargeTime = 90;
        /// <summary>劈砍持续帧数</summary>
        private const int SlamTime = 14;

        /// <summary>蓄力时的"玩家相对"举斧角度（0=正前方、-π/2=正上方），始终以"朝右"为基准方向计算，再由 <see cref="MirrorAngle"/> 处理镜像</summary>
        private const float LiftAnglePlayerRel = -MathHelper.Pi * 0.62f;
        /// <summary>劈砍终点的玩家相对角度（>0 表示斜向下方）</summary>
        private const float SwingEndPlayerRel = MathHelper.Pi * 0.42f;
        /// <summary>断罪师纹理中斧刃的"自然指向"（无旋转时斧刃从中心指向 -π/4，即屏幕右上方）</summary>
        private const float TextureBladeAngle = -MathHelper.PiOver4;
        /// <summary>收手持续帧数</summary>
        private const int RecoverTime = 14;
        /// <summary>蓄力阶段斧头与玩家中心的距离</summary>
        private const float HoldDistance = 56f;
        /// <summary>劈砍阶段斧头与玩家中心的距离</summary>
        private const float SwingDistance = 84f;

        //蓄力阶段开始时锁定的方向，避免蓄力过程中玩家转向影响动画
        private int lockedDirection = 1;
        //已蓄力的帧数，用于决定最终伤害与火焰强度
        private float chargeFrames;
        //劈砍的起始与终止角度（基于锁定的玩家朝向）
        private float swingStartAngle;
        private float swingEndAngle;
        //当前帧斧头的视觉旋转（用于刀光与拖尾）
        private float currentRotation;
        //上一帧斧头的视觉旋转，用于检测劈砍峰值
        private float lastRotation;
        //是否已经在本次劈砍中触发了地面冲击
        private bool impactTriggered;
        //轻微的视觉抖动相位
        private float shakePhase;
        //蓄力时积累的环境粒子计数
        private int chargeParticleClock;

        //斧头中心点的世界坐标（手持位置 + 偏移）
        private Vector2 axePivot;

        public int Phase {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }
        private ref float PhaseTimer => ref Projectile.ai[1];

        /// <summary>当前蓄力的归一化值 [0, 1]</summary>
        public float ChargeRatio => MathHelper.Clamp(chargeFrames / MaxChargeTime, 0f, 1f);

        public override void SetDefaults() {
            //宽广的 hitbox 让劈砍弧线上的所有 NPC 都被检测，再由 Colliding 二次精确判定
            Projectile.width = 200;
            Projectile.height = 200;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.hide = true;
        }

        public override bool ShouldUpdatePosition() => false;

        //蓄力阶段不参与碰撞判定，仅在劈砍阶段（且过半进度）才造成伤害
        public override bool? CanDamage() => Phase == PhaseSlamming && PhaseTimer >= SlamTime * 0.25f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Phase != PhaseSlamming) {
                return false;
            }
            //斧头沿当前旋转方向延伸出一段长方形作为碰撞箱
            Vector2 tip = axePivot + currentRotation.ToRotationVector2() * 70f;
            Vector2 root = axePivot;
            float collisionDistance = 0f;
            if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , root, tip, 36f, ref collisionDistance)) {
                return true;
            }
            return false;
        }

        public override void AI() {
            //持枪校验：物品丢了或者切走了，立刻收手
            if (Item.type != ModContent.ItemType<Arbiter>()) {
                Projectile.Kill();
                return;
            }

            //首帧初始化：锁定玩家朝向、播放抬手音效
            if (Projectile.localAI[2] == 0) {
                Projectile.localAI[2] = 1;
                lockedDirection = Math.Sign(ToMouse.X);
                if (lockedDirection == 0) {
                    lockedDirection = Owner.direction;
                }
                Owner.direction = lockedDirection;
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.5f, Pitch = -0.45f }, Owner.Center);
            }

            switch (Phase) {
                case PhaseCharging:
                    UpdateCharging();
                    break;
                case PhaseSlamming:
                    UpdateSlamming();
                    break;
                case PhaseRecovering:
                    UpdateRecovering();
                    break;
            }

            UpdatePlayerPose();
            Lighting.AddLight(axePivot, 0.85f * (0.5f + ChargeRatio * 0.5f)
                , 0.32f * (0.5f + ChargeRatio * 0.5f), 0.12f * (0.4f + ChargeRatio * 0.4f));

            PhaseTimer++;
        }

        /// <summary>
        /// 蓄力阶段：玩家持续按住左键累计蓄力，松开或达到上限即进入劈砍
        /// </summary>
        private void UpdateCharging() {
            //蓄力到上限后强制释放
            bool reachedCap = chargeFrames >= MaxChargeTime;
            //松手判定：DownLeft 由 BaseHeldProj 维护，等价于玩家在按左键
            bool released = !DownLeft && chargeFrames >= 1;

            //蓄力位置：斧头举至玩家上方稍偏后的位置，斧刃朝后
            //朝左和朝右是绕 Y 轴的镜像（π - θ），不是简单变号
            float liftAngle = MirrorAngle(LiftAnglePlayerRel);
            //蓄力时随机轻微抖动，营造肌肉发力的感觉
            shakePhase += 0.35f + ChargeRatio * 0.5f;
            float tremor = (float)Math.Sin(shakePhase) * ChargeRatio * 0.05f;
            currentRotation = liftAngle + tremor;
            axePivot = GetHandPos() + currentRotation.ToRotationVector2() * (HoldDistance * (1f + ChargeRatio * 0.08f));

            //每 18 帧播放一次蓄力闷响，pitch 随充能升高
            if (chargeFrames > 0 && chargeFrames % 18 == 0) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyFlameBreath with {
                    Volume = 0.25f + ChargeRatio * 0.35f,
                    Pitch = -0.6f + ChargeRatio * 0.6f
                }, axePivot);
            }

            //蓄力粒子效果
            SpawnChargingParticles();

            //满蓄一瞬间的闪光提示
            if (Math.Abs(chargeFrames - MaxChargeTime + 1) < 0.5f) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact with { Volume = 0.6f, Pitch = -0.3f }, axePivot);
                for (int i = 0; i < 24; i++) {
                    Vector2 v = (MathHelper.TwoPi * i / 24f).ToRotationVector2() * Main.rand.NextFloat(2.5f, 5f);
                    Dust d = Dust.NewDustPerfect(axePivot, DustID.Torch, v, 100, Color.Orange, 1.6f);
                    d.noGravity = true;
                    d.fadeIn = 1.2f;
                }
            }

            chargeFrames++;

            if (released || reachedCap) {
                EnterSlamPhase();
            }
        }

        /// <summary>
        /// 进入劈砍阶段，确定起止角度与速度
        /// </summary>
        private void EnterSlamPhase() {
            Phase = PhaseSlamming;
            PhaseTimer = 0;
            impactTriggered = false;
            //蓄力越满，挥砍幅度越大、终点越靠下方
            //朝左 / 朝右的角度通过 π - θ 镜像，而不是变号
            swingStartAngle = MirrorAngle(LiftAnglePlayerRel);
            swingEndAngle = MirrorAngle(SwingEndPlayerRel);
            currentRotation = swingStartAngle;
            lastRotation = swingStartAngle;

            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 1.05f, Pitch = -0.5f - ChargeRatio * 0.1f }, Owner.Center);
            SoundEngine.PlaySound(SoundID.DD2_OgreRoar with { Volume = 0.45f, Pitch = 0.2f }, Owner.Center);
        }

        /// <summary>
        /// 劈砍阶段：斧头沿弧线快速向下劈砍，到达末段触发地面冲击
        /// </summary>
        private void UpdateSlamming() {
            lastRotation = currentRotation;

            float progress = MathHelper.Clamp(PhaseTimer / SlamTime, 0f, 1f);
            //EaseInQuart：前期慢蓄势、后期猛然落下
            float eased = progress * progress * progress * progress;
            currentRotation = MathHelper.Lerp(swingStartAngle, swingEndAngle, eased);

            //劈砍过程中斧头略向前伸，强化"砸下"的视觉冲击
            float distance = MathHelper.Lerp(HoldDistance, SwingDistance, eased);
            axePivot = GetHandPos() + currentRotation.ToRotationVector2() * distance;

            //每帧沿劈砍轨迹喷火星
            SpawnSlamTrailParticles(progress);

            //达到 80% 进度时触发一次性的地面冲击效果（火焰冲击波生成在玩家正下方的地面）
            if (!impactTriggered && progress >= 0.78f) {
                impactTriggered = true;
                TriggerGroundImpact();
            }

            if (PhaseTimer >= SlamTime) {
                Phase = PhaseRecovering;
                PhaseTimer = 0;
            }
        }

        /// <summary>
        /// 收手阶段：劈砍完成后斧头保持下劈姿态短暂停留，然后销毁手持弹幕
        /// </summary>
        private void UpdateRecovering() {
            //从劈砍终点缓慢回归一个稍微抬起的"收力"姿势
            //"抬起" = 在玩家相对坐标下让角度更负（更朝上），所以朝左时需要"加" 0.35（因为左方向的世界角度是镜像的）
            float t = MathHelper.Clamp(PhaseTimer / RecoverTime, 0f, 1f);
            float pullBack = 0.35f * lockedDirection;
            float restAngle = MathHelper.Lerp(swingEndAngle, swingEndAngle - pullBack, t);
            currentRotation = restAngle;
            axePivot = GetHandPos() + currentRotation.ToRotationVector2() * SwingDistance;

            if (PhaseTimer >= RecoverTime) {
                Projectile.Kill();
            }
        }

        /// <summary>
        /// 触发地面冲击：查找劈砍点正下方的地面，并在地面上生成 ArbiterShockwave
        /// </summary>
        private void TriggerGroundImpact() {
            if (CWRServerConfig.Instance.ScreenVibration) {
                Vector2 dir = new Vector2(lockedDirection, 1f).SafeNormalize(Vector2.UnitY);
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    Owner.Center, dir, 7.5f * (0.4f + ChargeRatio * 0.6f), 8f, 12, 700f, FullName));
                Owner.CWR().GetScreenShake(4f + ChargeRatio * 4f);
            }

            //从斧头当前位置垂直向下扫描，找到第一块实心地面
            //优先以斧刃尖端的水平坐标作为冲击中心点，使冲击波出现在斧头真正"砸到"的位置
            //扫描起点用玩家上身位置作为基准，避免斧头已经"挥到地面以下"导致扫描到错误的下层地形
            Vector2 bladeTip = axePivot + currentRotation.ToRotationVector2() * 70f;
            Vector2 scanStart = new Vector2(bladeTip.X, Owner.Center.Y - 24f);
            Vector2 groundPos = FindGroundBelow(scanStart, 28);

            if (Projectile.IsOwnedByLocalPlayer()) {
                int damage = (int)(Projectile.damage * (0.55f + ChargeRatio * 1.55f));
                //蓄力越满，蔓延距离越远（每边最多 60 格）
                int spreadTiles = (int)MathHelper.Lerp(14f, 60f, ChargeRatio);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), groundPos
                    , Vector2.Zero, ModContent.ProjectileType<ArbiterShockwave>()
                    , damage, Projectile.knockBack, Projectile.owner, ai0: spreadTiles, ai1: ChargeRatio);
            }
        }

        /// <summary>
        /// 更新玩家姿势：让玩家的双臂跟随斧头朝向，避免出现"凭空举斧"的违和感
        /// </summary>
        private void UpdatePlayerPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = lockedDirection;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
            Owner.itemRotation = currentRotation;

            //双手抬起跟随斧头：两只手指向斧头方向，模拟双手紧握斧柄
            if (CWRServerConfig.Instance.WeaponHandheldDisplay) {
                float armAngle = currentRotation - MathHelper.PiOver2;
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armAngle);
                Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full
                    , armAngle + 0.15f * lockedDirection);
            }

            //hitbox 跟着斧头位置走，broad-phase 才能覆盖到劈砍轨迹上的 NPC
            Projectile.Center = axePivot;
            Projectile.timeLeft = 2;
        }

        /// <summary>
        /// 蓄力时持续在斧刃周围喷出火星与暗色烟雾，越接近满蓄越剧烈
        /// </summary>
        private void SpawnChargingParticles() {
            chargeParticleClock++;

            //斧刃位置：从持柄沿当前旋转方向延伸 50 像素
            Vector2 bladePos = axePivot + currentRotation.ToRotationVector2() * 35f;

            //常规火星
            int chance = Math.Max(1, 4 - (int)(ChargeRatio * 3));
            if (Main.rand.NextBool(chance)) {
                Vector2 jitter = Main.rand.NextVector2Circular(20f, 20f);
                Vector2 vel = (jitter * -0.05f) + new Vector2(0, -Main.rand.NextFloat(1f, 3f));
                Dust d = Dust.NewDustPerfect(bladePos + jitter, DustID.Torch, vel
                    , 0, Color.Lerp(Color.Yellow, Color.OrangeRed, ChargeRatio), 0.9f + ChargeRatio * 0.8f);
                d.noGravity = true;
                d.fadeIn = 1.1f;
            }

            //满蓄附近时的浓烟与火焰粒子
            if (ChargeRatio > 0.6f && chargeParticleClock % 3 == 0) {
                Vector2 vel = -currentRotation.ToRotationVector2() * Main.rand.NextFloat(1.2f, 2.6f)
                    + Main.rand.NextVector2Circular(1.4f, 1.4f);
                PRTLoader.NewParticle<PRT_LavaFire>(bladePos + Main.rand.NextVector2Circular(18f, 18f), vel
                    , Color.White, 0.6f + ChargeRatio * 0.5f);
            }

            //满蓄后的"汇聚"光线，从远处向斧刃汇聚
            if (ChargeRatio > 0.4f && Main.rand.NextBool(2)) {
                float r = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(60f, 130f) * (0.5f + ChargeRatio * 0.5f);
                Vector2 spawn = bladePos + r.ToRotationVector2() * radius;
                Vector2 vel = (bladePos - spawn).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3.5f, 6f);
                Dust d = Dust.NewDustPerfect(spawn, DustID.RedTorch, vel, 100, default, 1.1f + ChargeRatio);
                d.noGravity = true;
                d.fadeIn = 0.4f;
            }

            //满蓄持续震屏
            if (ChargeRatio >= 0.999f) {
                Owner.CWR().GetScreenShake(2f);
            }
        }

        /// <summary>
        /// 劈砍过程中沿弧线持续喷出火焰拖尾
        /// </summary>
        private void SpawnSlamTrailParticles(float progress) {
            //拖尾从 root 到 tip 沿斧头方向喷出
            Vector2 axisDir = currentRotation.ToRotationVector2();
            Vector2 bladeTip = axePivot + axisDir * 70f;

            //每帧 4-6 颗火星 + 1-2 个 PRT 火焰
            int sparkCount = 5;
            for (int i = 0; i < sparkCount; i++) {
                float t = Main.rand.NextFloat();
                Vector2 pos = Vector2.Lerp(axePivot, bladeTip, t) + Main.rand.NextVector2Circular(6f, 6f);
                Vector2 perp = new Vector2(-axisDir.Y, axisDir.X);
                Vector2 swirl = perp * Main.rand.NextFloat(-3f, 3f);
                Vector2 vel = axisDir * Main.rand.NextFloat(1f, 4f) * (1f - progress) + swirl;
                Dust d = Dust.NewDustPerfect(pos, DustID.Torch, vel, 100, Color.OrangeRed
                    , 1f + ChargeRatio * 0.6f);
                d.noGravity = true;
                d.fadeIn = 1.2f;
            }

            //大颗的火焰粒子（PRT），跟随挥砍方向
            if (Main.rand.NextBool(2)) {
                Vector2 pos = bladeTip + Main.rand.NextVector2Circular(10f, 10f);
                Vector2 vel = axisDir * Main.rand.NextFloat(0.5f, 2.0f);
                PRTLoader.NewParticle<PRT_LavaFire>(pos, vel, Color.White, 0.8f + ChargeRatio * 0.7f);
            }
        }

        /// <summary>
        /// 从给定点向下扫描，找到第一块实心地形的顶部并返回世界坐标；找不到则返回 fallback
        /// </summary>
        private static Vector2 FindGroundBelow(Vector2 worldPos, int maxTileSearch) {
            int tx = (int)(worldPos.X / 16f);
            int ty = (int)(worldPos.Y / 16f);
            for (int dy = 0; dy <= maxTileSearch; dy++) {
                int y = ty + dy;
                if (!WorldGen.InWorld(tx, y)) {
                    break;
                }
                Tile t = Framing.GetTileSafely(tx, y);
                if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                    return new Vector2(worldPos.X, y * 16f - 4f);
                }
            }
            //找不到地面时返回原点附近（防止冲击波生成在天上）
            return new Vector2(worldPos.X, (ty + 4) * 16f);
        }

        /// <summary>
        /// 玩家"手心"的世界坐标：身体稳定中心向上偏移一些，作为持斧的旋转支点
        /// </summary>
        private Vector2 GetHandPos() {
            //身体中心 + 一点点垂直/水平偏移，让斧头不会贴脸
            Vector2 pivot = Owner.GetPlayerStabilityCenter();
            pivot.Y -= 6f * Owner.gravDir;
            return pivot;
        }

        /// <summary>
        /// 把一个"以朝右为基准"的世界角度，按玩家朝向镜像到正确的世界角度上
        /// 朝右时直接返回 <paramref name="rightFacingAngle"/>；
        /// 朝左时返回绕 Y 轴对称的 π - <paramref name="rightFacingAngle"/>，
        /// 这样 "上方偏后" / "下方偏前" 这类不对称姿势在两个方向上都能正确呈现
        /// （直接乘以 -1 只对正上 / 正下这类纵向对称姿势有效，对斜向角度是错误的）
        /// </summary>
        private float MirrorAngle(float rightFacingAngle) {
            return lockedDirection > 0 ? rightFacingAngle : MathHelper.Pi - rightFacingAngle;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!CWRServerConfig.Instance.WeaponHandheldDisplay) {
                return false;
            }

            Texture2D tex = TextureAssets.Item[ModContent.ItemType<Arbiter>()].Value;
            Vector2 origin = tex.Size() / 2f;
            //纹理无旋转时斧刃指向 -π/4，所以补偿 +π/4 让 currentRotation 表示斧刃在世界中的实际指向
            //不做左右翻转：只靠旋转就能让斧刃在两个方向上都正确指向斧头的"前进方向"
            float drawRot = currentRotation - TextureBladeAngle;
            SpriteEffects effect = SpriteEffects.None;
            if (Owner.direction == -1) {
                effect = SpriteEffects.FlipVertically;
                drawRot -= MathHelper.PiOver2;
            }

            Vector2 drawPos = axePivot - Main.screenPosition;
            float scale = 1.05f + ChargeRatio * 0.18f;

            //蓄力中的红色外光晕（最外层）
            if (ChargeRatio > 0.05f) {
                Texture2D glow = CWRUtils.GetT2DValue(CWRConstant.Masking + "SoftGlow");
                if (glow != null) {
                    Color glowColor = Color.Lerp(new Color(255, 80, 30, 0), new Color(255, 200, 100, 0), ChargeRatio);
                    float pulse = 0.85f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.15f;
                    Main.spriteBatch.Draw(glow, drawPos, null, glowColor * ChargeRatio * 0.95f * pulse
                        , 0f, glow.Size() / 2f, (0.8f + ChargeRatio * 1.1f) * pulse, SpriteEffects.None, 0);
                }
            }

            //劈砍过程的拖尾残影（采样起点到当前位置的若干个中间角度）
            if (Phase == PhaseSlamming) {
                int trail = 6;
                for (int i = 0; i < trail; i++) {
                    float t = (i + 1) / (float)(trail + 1);
                    float rot = MathHelper.Lerp(lastRotation, currentRotation, t);
                    Vector2 pos = GetHandPos() + rot.ToRotationVector2() * MathHelper.Lerp(HoldDistance, SwingDistance
                        , MathHelper.Clamp(PhaseTimer / SlamTime, 0f, 1f)) - Main.screenPosition;
                    float trailRot = rot - TextureBladeAngle;
                    if (Owner.direction == -1) {
                        trailRot -= MathHelper.PiOver2;
                    }
                    Color trailColor = Color.Lerp(Color.OrangeRed, Color.Yellow, t) * (0.45f * (1f - i / (float)trail));
                    trailColor.A = 0;
                    Main.spriteBatch.Draw(tex, pos, null, trailColor, trailRot, origin, scale * 0.96f, effect, 0);
                }
            }

            //主体斧头
            Main.spriteBatch.Draw(tex, drawPos, null, lightColor, drawRot, origin, scale, effect, 0);

            //满蓄时的核心高光叠加
            if (ChargeRatio > 0.7f) {
                Color hot = Color.Lerp(new Color(255, 200, 120, 0), new Color(255, 255, 255, 0)
                    , (ChargeRatio - 0.7f) / 0.3f);
                Main.spriteBatch.Draw(tex, drawPos, null, hot, drawRot, origin, scale * 1.02f, effect, 0);
            }

            return false;
        }
    }

    /// <summary>
    /// 劈砍命中地面时生成的冲击点：负责生成竖直火柱、左右两条火蛇以及一次性持久火坑
    /// </summary>
    internal class ArbiterShockwave : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        /// <summary>本次冲击波的最远蔓延格数（每边）</summary>
        private ref float SpreadTiles => ref Projectile.ai[0];
        /// <summary>蓄力比例，影响视觉规模</summary>
        private ref float ChargeRatio => ref Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 30;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override bool? CanDamage() => Projectile.timeLeft >= 26;

        public override void AI() {
            if (Projectile.timeLeft == 30) {
                OnImpact();
            }

            //撞击点的中央火柱粒子（短暂存在 12 帧）
            if (Projectile.timeLeft >= 18) {
                int density = (int)MathHelper.Lerp(3, 8, ChargeRatio);
                for (int i = 0; i < density; i++) {
                    Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-22f, 22f), 0f);
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(4f, 9f));
                    Dust d = Dust.NewDustPerfect(pos, DustID.Torch, vel, 0
                        , Color.Lerp(Color.OrangeRed, Color.Yellow, Main.rand.NextFloat()), Main.rand.NextFloat(1.6f, 2.6f));
                    d.noGravity = true;
                    d.fadeIn = 1.2f;
                }

                //更大的火焰粒子，垂直上喷
                if (Main.rand.NextBool(2)) {
                    Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-30f, 30f), 0f);
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(2f, 4f));
                    PRTLoader.NewParticle<PRT_LavaFire>(pos, vel, Color.White, 1.0f + ChargeRatio * 0.8f);
                }
            }

            Lighting.AddLight(Projectile.Center, 1.4f, 0.7f, 0.25f);
        }

        private void OnImpact() {
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1f, Pitch = -0.4f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.95f, Pitch = -0.3f }, Projectile.Center);

            //向四周喷射环形火星
            for (int i = 0; i < 60; i++) {
                float angle = MathHelper.TwoPi * i / 60f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 9f);
                vel.Y -= 2f;//稍微整体上抬，更像爆裂
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel
                    , 0, default, Main.rand.NextFloat(1.4f, 2.6f));
                d.noGravity = true;
            }

            //中央留一团 PRT 火焰
            for (int i = 0; i < 12; i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1.5f, 4f));
                PRTLoader.NewParticle<PRT_LavaFire>(Projectile.Center + Main.rand.NextVector2Circular(20f, 8f)
                    , vel, Color.White, 1.0f + ChargeRatio);
            }

            //左右各放一条火蛇
            if (Projectile.IsOwnedByLocalPlayer()) {
                int snakeDmg = Math.Max(1, (int)(Projectile.damage * 0.45f));
                int spread = Math.Max(8, (int)SpreadTiles);
                //向右的火蛇
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center
                    , new Vector2(1f, 0f), ModContent.ProjectileType<ArbiterFireSnake>()
                    , snakeDmg, Projectile.knockBack * 0.6f, Projectile.owner, ai0: 1f, ai1: spread);
                //向左的火蛇
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center
                    , new Vector2(-1f, 0f), ModContent.ProjectileType<ArbiterFireSnake>()
                    , snakeDmg, Projectile.knockBack * 0.6f, Projectile.owner, ai0: -1f, ai1: spread);
                //撞击点本身留一团持续火（蓄力越满持续越久）
                int holdFire = (int)MathHelper.Lerp(180, 360, ChargeRatio);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center
                    , Vector2.Zero, ModContent.ProjectileType<ArbiterGroundFire>()
                    , Math.Max(1, (int)(Projectile.damage * 0.18f)), 0f
                    , Projectile.owner, ai0: holdFire, ai1: 1.2f);
            }
        }
    }

    /// <summary>
    /// 火蛇：沿地形蔓延的快速火焰冲击波
    /// 每帧尝试向给定方向移动一步，并通过垂直扫描把位置吸附到地面表面，
    /// 遇到小台阶会自动跨上去，遇到悬崖会顺势下滑，遇到无法翻越的高墙或深渊则结束蔓延
    /// 沿途持续投放 ArbiterGroundFire 与火焰粒子
    /// </summary>
    internal class ArbiterFireSnake : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        /// <summary>移动方向：+1 向右，-1 向左</summary>
        private ref float MoveDir => ref Projectile.ai[0];
        /// <summary>剩余可走的"格数预算"，每跨过一个 tile 就消耗 1</summary>
        private ref float TileBudget => ref Projectile.ai[1];

        //每帧移动的像素数（约等于半个 tile，让蔓延肉眼可见但不过快）
        private const float StepPixels = 8f;
        //最大允许向上爬升的台阶高度（tile）
        private const int MaxStepUp = 3;
        //向下查找地面的最大 tile 数（更大可以"摔"过更深的坑）
        private const int MaxFallSearch = 18;
        //生成持久火坑的间隔（像素）
        private const float GroundFireSpacing = 22f;

        //上次投放火坑的世界坐标
        private Vector2 lastGroundFirePos;
        //初始 X，用于限制总位移
        private float startX;
        //是否已经停止（仅播放熄灭动画后死亡）
        private bool stopped;
        //停止后的衰减计时
        private int deathTimer;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 22;
            Projectile.timeLeft = 240;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override bool? CanDamage() => !stopped;

        public override void AI() {
            if (Projectile.localAI[0] == 0) {
                Projectile.localAI[0] = 1;
                startX = Projectile.Center.X;
                lastGroundFirePos = Projectile.Center;
                //首帧立刻在生成点放一团火
                SpawnGroundFireAt(Projectile.Center, lifetime: 240, scale: 1.3f);
            }

            if (stopped) {
                //停止后让粒子继续喷一段时间再死亡
                deathTimer++;
                SpawnEdgeFlames(0.6f);
                if (deathTimer >= 30) {
                    Projectile.Kill();
                }
                return;
            }

            float dir = Math.Sign(MoveDir);
            if (dir == 0) {
                Projectile.Kill();
                return;
            }

            //在本帧的目标 X 位置尝试推进
            Vector2 next = Projectile.Center + new Vector2(StepPixels * dir, 0f);
            //火蛇 hitbox 的"脚底"刚好踩在地面上方，所以脚下的 tile y 取下方一格作为基准
            int currentTileY = (int)Math.Floor(Projectile.Bottom.Y / 16f);

            //优先：在目标 X 位置上方搜索"能否站立的地面"
            int nextTileX = (int)Math.Floor(next.X / 16f);
            int groundY = FindStandableGroundY(nextTileX, currentTileY);

            if (groundY == -1) {
                //没找到地面：可能掉入深渊
                stopped = true;
                SoundEngine.PlaySound(SoundID.NPCDeath14 with { Volume = 0.3f, Pitch = 0.3f }, Projectile.Center);
                return;
            }

            //如果地面比当前高得太多（高墙），停止
            int deltaY = groundY - currentTileY;
            if (deltaY < -MaxStepUp) {
                stopped = true;
                SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact with { Volume = 0.4f, Pitch = 0.4f }, Projectile.Center);
                return;
            }

            //合法：吸附到目标地面之上 2 像素
            Projectile.Center = new Vector2(next.X, groundY * 16f - 2f);

            //预算扣减
            if ((int)Math.Floor(next.X / 16f) != (int)Math.Floor((Projectile.Center.X - StepPixels * dir) / 16f)) {
                TileBudget -= 1f;
            }

            //超出预算或者总位移太远 → 停止
            float traveled = Math.Abs(Projectile.Center.X - startX);
            if (TileBudget <= 0 || traveled > 16f * 80f) {
                stopped = true;
                return;
            }

            //每移动一段距离就投放一个持久火坑
            if (Projectile.Center.Distance(lastGroundFirePos) >= GroundFireSpacing) {
                lastGroundFirePos = Projectile.Center;
                int life = 180 + Main.rand.Next(60);
                SpawnGroundFireAt(Projectile.Center, lifetime: life, scale: Main.rand.NextFloat(0.9f, 1.25f));
            }

            //每帧的火焰沿地表喷溅
            SpawnEdgeFlames(1f);

            //蛇头自身的强光
            Lighting.AddLight(Projectile.Center + new Vector2(0, -8), 1.2f, 0.55f, 0.2f);
        }

        /// <summary>
        /// 在指定 tile 列上寻找"可以站立"的地面：先在当前高度附近向上爬，再向下扫描
        /// 返回找到的实心 tile 的 y 索引；-1 表示放弃
        /// </summary>
        private static int FindStandableGroundY(int tx, int currentY) {
            if (!WorldGen.InWorld(tx, currentY)) {
                return -1;
            }

            //先看看目标位置本身（current）是不是实心 tile：如果是，意味着前方是台阶/墙，尝试向上找一个空气格
            for (int dy = 0; dy <= MaxStepUp + 1; dy++) {
                int y = currentY - dy;
                if (!WorldGen.InWorld(tx, y)) {
                    break;
                }
                Tile here = Framing.GetTileSafely(tx, y);
                bool hereIsSolid = here.HasTile && Main.tileSolid[here.TileType] && !Main.tileSolidTop[here.TileType];
                if (hereIsSolid) {
                    continue;
                }
                //here 是空气：再检查它正下方一格是不是实心 → 即可站立
                int below = y + 1;
                if (!WorldGen.InWorld(tx, below)) {
                    return -1;
                }
                Tile under = Framing.GetTileSafely(tx, below);
                bool underIsSolid = under.HasTile && Main.tileSolid[under.TileType] && !Main.tileSolidTop[under.TileType];
                if (underIsSolid) {
                    return below;
                }
                break;//空中开始向下扫描
            }

            //向下扫描寻找首块实心 tile（处理向下走的斜坡 / 落差）
            for (int dy = 1; dy <= MaxFallSearch; dy++) {
                int y = currentY + dy;
                if (!WorldGen.InWorld(tx, y)) {
                    return -1;
                }
                Tile t = Framing.GetTileSafely(tx, y);
                if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                    return y;
                }
            }
            return -1;
        }

        /// <summary>
        /// 在火蛇行进位置喷溅火焰粒子（地表特效）
        /// 火蛇头部本身仅是冲击波的"先锋"，主要的火焰特效由身后留下的 GroundFire 持续呈现
        /// 因此这里的粒子数量控制得较为克制
        /// </summary>
        private void SpawnEdgeFlames(float densityMul) {
            Vector2 basePos = Projectile.Center;

            //橙色火星向上飘
            int sparks = Math.Max(1, (int)(3 * densityMul));
            for (int i = 0; i < sparks; i++) {
                Vector2 pos = basePos + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-4f, 0f));
                Vector2 vel = new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(2f, 5f));
                Dust d = Dust.NewDustPerfect(pos, DustID.Torch, vel, 0
                    , Color.Lerp(Color.OrangeRed, Color.Yellow, Main.rand.NextFloat()), Main.rand.NextFloat(1.3f, 2.2f));
                d.noGravity = true;
                d.fadeIn = 1.1f;
            }

            //大颗 PRT 火焰，跟随火蛇的方向
            if (Main.rand.NextFloat() < 0.5f * densityMul) {
                Vector2 pos = basePos + new Vector2(Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(-4f, 2f));
                Vector2 vel = new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(0.8f, 2.2f));
                PRTLoader.NewParticle<PRT_LavaFire>(pos, vel, Color.White, Main.rand.NextFloat(0.8f, 1.3f));
            }
        }

        /// <summary>
        /// 在指定位置生成一个持久火坑（ArbiterGroundFire）
        /// </summary>
        private void SpawnGroundFireAt(Vector2 worldPos, int lifetime, float scale) {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), worldPos
                , Vector2.Zero, ModContent.ProjectileType<ArbiterGroundFire>()
                , Math.Max(1, (int)(Projectile.damage * 0.5f)), 0f
                , Projectile.owner, ai0: lifetime, ai1: scale);
        }
    }

    /// <summary>
    /// 持久地面火坑：贴在地面上持续燃烧并对接触的敌人造成伤害
    /// 使用 Dust + PRT 模拟自然摇曳的火焰，无需自身贴图
    /// </summary>
    internal class ArbiterGroundFire : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        /// <summary>初始生命（帧数）</summary>
        private ref float LifeMax => ref Projectile.ai[0];
        /// <summary>视觉规模</summary>
        private ref float VisualScale => ref Projectile.ai[1];

        private int age;
        //火焰摇曳相位
        private float swayPhase;
        //视觉火焰宽高与地面锚点，和 Projectile.width/height 的伤害判定完全解耦
        private float visualHeight;
        private float visualWidth;
        private float visualGroundY;

        //仅用于碰撞判定。以后调整这两个值不会影响火焰粒子的外观
        private static int HitboxWidth => 40;
        private static int HitboxHeight => 124;
        private static float BaseVisualHeight => 28f;
        private static float BaseVisualWidth => 40f;

        public override void SetDefaults() {
            Projectile.width = HitboxWidth;
            Projectile.height = HitboxHeight;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 600;
        }

        public override void OnSpawn(IEntitySource source) {
            if (LifeMax > 0) {
                Projectile.timeLeft = (int)LifeMax;
            }
            if (VisualScale <= 0.01f) {
                VisualScale = 1f;
            }
            swayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            visualGroundY = Projectile.Center.Y;
            visualWidth = BaseVisualWidth * VisualScale;
        }

        public override bool? CanDamage() => age > 4 && age < Projectile.timeLeft - 6;

        public override void AI() {
            age++;
            swayPhase += 0.18f;

            //火焰高度的呼吸：先快速点燃 → 长时间稳定燃烧 → 末段快速衰减
            float lifeRatio = Projectile.timeLeft / Math.Max(LifeMax, 1f);
            float baseHeight = BaseVisualHeight * VisualScale;
            visualWidth = BaseVisualWidth * VisualScale;
            if (age < 10) {
                //点燃阶段：从 0 快速涨到 baseHeight
                visualHeight = MathHelper.Lerp(0f, baseHeight, age / 10f);
            } else if (lifeRatio < 0.3f) {
                //衰减阶段
                visualHeight = baseHeight * (lifeRatio / 0.3f);
            } else {
                //稳定燃烧：用 sin 波让高度自然起伏
                visualHeight = baseHeight * (0.9f + (float)Math.Sin(swayPhase) * 0.1f);
            }

            SpawnFlameDust();

            //发光（强度跟随火焰高度）
            float lightFactor = visualHeight / Math.Max(baseHeight, 1f);
            Lighting.AddLight(new Vector2(Projectile.Center.X, visualGroundY - visualHeight * 0.5f)
                , 1.0f * lightFactor, 0.45f * lightFactor, 0.15f * lightFactor);
        }

        /// <summary>
        /// 喷出火焰粒子：底部 RedTorch 余烬，中段 Torch 主体，顶部 PRT 火焰，营造层次感
        /// 每个火坑同屏数量可能多达数十个，所以这里的粒子数量做了严格限制以保证性能
        /// </summary>
        private void SpawnFlameDust() {
            if (visualHeight < 4f) {
                return;
            }

            float baseY = visualGroundY;
            float spreadX = visualWidth * 0.45f;

            //底层余烬（红）：每 3 帧才生成一颗
            if (age % 3 == 0) {
                Vector2 pos = new Vector2(Projectile.Center.X + Main.rand.NextFloat(-spreadX, spreadX), baseY);
                Vector2 vel = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.6f, 1.4f));
                Dust d = Dust.NewDustPerfect(pos, DustID.RedTorch, vel, 100, default
                    , Main.rand.NextFloat(0.9f, 1.4f) * VisualScale);
                d.noGravity = true;
                d.fadeIn = 0.8f;
            }

            //中层主体（橙黄）：每 2 帧 1 颗
            if (age % 2 == 0) {
                float hOffset = Main.rand.NextFloat(-2f, visualHeight * 0.4f);
                Vector2 pos = new Vector2(Projectile.Center.X + Main.rand.NextFloat(-spreadX * 0.7f, spreadX * 0.7f)
                    , baseY - hOffset);
                Vector2 vel = new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(1.4f, 3.2f));
                Dust d = Dust.NewDustPerfect(pos, DustID.Torch, vel, 80
                    , Color.Lerp(Color.OrangeRed, Color.Yellow, Main.rand.NextFloat()), Main.rand.NextFloat(1.2f, 1.9f) * VisualScale);
                d.noGravity = true;
                d.fadeIn = 1.0f;
            }

            //顶端的 PRT 火焰：每 6 帧 1 颗，营造跳动感
            if (age % 6 == 0) {
                float h = Main.rand.NextFloat(visualHeight * 0.3f, visualHeight * 0.8f);
                Vector2 pos = new Vector2(Projectile.Center.X + Main.rand.NextFloat(-spreadX * 0.5f, spreadX * 0.5f)
                    , baseY - h);
                Vector2 vel = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.6f, 1.6f));
                PRTLoader.NewParticle<PRT_LavaFire>(pos, vel, Color.White, Main.rand.NextFloat(0.7f, 1.1f) * VisualScale);
            }

            //偶尔的火星向上喷出，模拟噼里啪啦
            if (Main.rand.NextBool(20)) {
                Vector2 pos = new Vector2(Projectile.Center.X + Main.rand.NextFloat(-spreadX, spreadX), baseY);
                Vector2 vel = new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), -Main.rand.NextFloat(3f, 7f));
                Dust d = Dust.NewDustPerfect(pos, DustID.GoldFlame, vel, 0, default
                    , Main.rand.NextFloat(0.8f, 1.3f));
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命中点的小爆裂
            for (int i = 0; i < 4; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3f, 3f);
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Torch, vel, 0
                    , Color.OrangeRed, Main.rand.NextFloat(1.1f, 1.7f));
                d.noGravity = true;
            }
            target.AddBuff(BuffID.OnFire3, 180);
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
