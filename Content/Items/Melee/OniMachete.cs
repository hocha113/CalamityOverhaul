using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class OniMachete : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "OniMachete";

        [VaultLoaden("@CalamityMod/NPCs/SupremeCalamitas/")]
        public static Texture2D SepulcherForearm; //反射加载手臂纹理
        [VaultLoaden("@CalamityMod/NPCs/SupremeCalamitas/")]
        public static Texture2D SepulcherHand; //反射加载手掌纹理，正面朝下

        public override void SetDefaults() {
            Item.damage = 85;
            Item.DamageType = DamageClass.Melee;
            Item.width = 60;
            Item.height = 60;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6.5f;
            Item.value = Item.sellPrice(gold: 5);
            Item.rare = ItemRarityID.Yellow;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.useTurn = true;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            OniMachetePlayer modPlayer = player.GetModPlayer<OniMachetePlayer>();
            modPlayer.AddArm(player, Item.damage, Item.knockBack);
        }

        public override void HoldItem(Player player) {
            OniMachetePlayer modPlayer = player.GetModPlayer<OniMachetePlayer>();
            modPlayer.HoldingOniMachete = true;
        }
    }

    /// <summary>
    /// 玩家数据管理类，用于管理鬼柴刀的骷髅手臂
    /// </summary>
    internal class OniMachetePlayer : ModPlayer
    {
        public bool HoldingOniMachete;
        private static readonly List<int> ActiveArms = new();
        private const int MaxArms = 8;
        private int armDecayTimer;
        private const int ArmDecayTime = 60 * 10; //10秒后开始减少手臂

        public override void ResetEffects() {
            HoldingOniMachete = false;
        }

        public override void PostUpdate() {
            if (!HoldingOniMachete) {
                //不再持有武器时，清理所有手臂
                CleanupAllArms();
                return;
            }

            //清理失效的手臂
            CleanupInactiveArms();

            //手臂衰减计时
            if (ActiveArms.Count > 0) {
                armDecayTimer++;
                if (armDecayTimer >= ArmDecayTime) {
                    armDecayTimer = 0;
                    //移除最老的手臂
                    if (ActiveArms.Count > 0) {
                        int index = ActiveArms[0];
                        if (index.TryGetProjectile(out var arm)) {
                            arm.Kill();
                        }
                        ActiveArms.RemoveAt(0);
                    }
                }
            }
            else {
                armDecayTimer = 0;
            }
        }

        public void AddArm(Player player, int damage, float knockback) {
            if (ActiveArms.Count >= MaxArms) return;

            //重置衰减计时
            armDecayTimer = 0;

            IEntitySource source = player.GetSource_ItemUse(player.HeldItem);
            Vector2 spawnPos = player.Center + new Vector2(0, -20);

            int armProj = Projectile.NewProjectile(
                source,
                spawnPos,
                Vector2.Zero,
                ModContent.ProjectileType<OniSkeletonArm>(),
                damage,
                knockback,
                player.whoAmI,
                ActiveArms.Count
            );

            if (armProj >= 0) {
                ActiveArms.Add(armProj);
                SpawnSummonEffect(spawnPos);

                //召唤音效
                SoundEngine.PlaySound(SoundID.Item8 with {
                    Volume = 0.5f,
                    Pitch = -0.2f
                }, spawnPos);
            }
        }

        private static void CleanupInactiveArms() {
            ActiveArms.RemoveAll(id => {
                if (id < 0 || id >= Main.maxProjectiles) return true;
                Projectile proj = Main.projectile[id];
                return !proj.active || proj.type != ModContent.ProjectileType<OniSkeletonArm>();
            });
        }

        private static void CleanupAllArms() {
            foreach (int id in ActiveArms) {
                if (id >= 0 && id < Main.maxProjectiles) {
                    Projectile proj = Main.projectile[id];
                    if (proj.active && proj.type == ModContent.ProjectileType<OniSkeletonArm>()) {
                        proj.Kill();
                    }
                }
            }
            ActiveArms.Clear();
        }

        private static void SpawnSummonEffect(Vector2 position) {
            //火焰粒子
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 6f);

                Dust dust = Dust.NewDustPerfect(
                    position,
                    DustID.Torch,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.5f, 2f)
                );
                dust.noGravity = true;
                dust.fadeIn = 1.2f;
            }

            //烟雾
            for (int i = 0; i < 10; i++) {
                Dust smoke = Dust.NewDustPerfect(
                    position,
                    DustID.Smoke,
                    Main.rand.NextVector2Circular(2f, 2f),
                    100,
                    new Color(80, 80, 80),
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                smoke.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 鬼柴刀的骷髅手臂弹幕
    /// </summary>
    internal class OniSkeletonArm : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private enum ArmState
        {
            Idle, //待机漂浮
            Targeting, //锁定目标
            WindingUp, //蓄力
            Slashing, //挥砍
            Recovering //恢复
        }

        private ref float ArmIndex => ref Projectile.ai[0];
        private ref float StateRaw => ref Projectile.ai[1];
        private ref float StateTimer => ref Projectile.localAI[0];

        private ArmState State {
            get => (ArmState)StateRaw;
            set => StateRaw = (float)value;
        }

        private int targetNPCID = -1;
        private Vector2 idleOffset = Vector2.Zero;
        private Vector2 attackStartPos = Vector2.Zero;
        private Vector2 attackTargetPos = Vector2.Zero;

        //IK手臂参数
        private readonly List<Vector2> armSegments = new();
        private const int ArmSegmentCount = 4;
        private const float SegmentLength = 35f;
        private Vector2 shoulderPos = Vector2.Zero;
        private Vector2 handPos = Vector2.Zero;
        private float armTension = 0f;

        //攻击参数
        private const float SearchRange = 600f;
        private const int IdleDuration = 30;
        private const int WindUpDuration = 20;
        private const int SlashDuration = 15;
        private const int RecoverDuration = 25;

        //视觉效果
        private float glowIntensity = 0f;
        private readonly List<Vector2> trailPositions = new();
        private const int MaxTrailLength = 15;
        private float handRotation = 0f;
        private float slashAngle = 0f;

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;

            //初始化IK手臂段
            for (int i = 0; i < ArmSegmentCount; i++) {
                armSegments.Add(Vector2.Zero);
            }
        }

        public override bool? CanDamage() => State == ArmState.Slashing;

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            OniMachetePlayer modPlayer = owner.GetModPlayer<OniMachetePlayer>();
            if (!modPlayer.HoldingOniMachete) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 60;
            StateTimer++;
            UpdateIdleOffset();
            UpdateShoulderPosition(owner);

            //状态机
            switch (State) {
                case ArmState.Idle:
                    IdleBehavior(owner);
                    break;
                case ArmState.Targeting:
                    TargetingBehavior(owner);
                    break;
                case ArmState.WindingUp:
                    WindUpBehavior();
                    break;
                case ArmState.Slashing:
                    SlashingBehavior();
                    break;
                case ArmState.Recovering:
                    RecoveringBehavior(owner);
                    break;
            }

            //更新IK手臂
            UpdateArmIK();

            //更新拖尾
            UpdateTrail();

            //发光效果
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.3f + 0.7f;
            Lighting.AddLight(Projectile.Center, 0.8f * pulse, 0.3f * pulse, 0.1f * pulse);
        }

        private void UpdateIdleOffset() {
            idleOffset.X = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + ArmIndex) * 40f;
            idleOffset.Y = (float)Math.Cos(Main.GlobalTimeWrappedHourly * 1.8f + ArmIndex) * 25f;
        }

        private void UpdateShoulderPosition(Player owner) {
            //手臂从玩家背后生成，根据索引分布
            float offsetAngle = MathHelper.Pi + (ArmIndex - 3.5f) * 0.3f;
            shoulderPos = owner.Center + offsetAngle.ToRotationVector2() * 30f + new Vector2(0, -10f);
        }

        private void IdleBehavior(Player owner) {
            //在玩家背后漂浮
            float angle = MathHelper.Pi + (ArmIndex - 3.5f) * 0.4f;
            Vector2 targetPos = shoulderPos + angle.ToRotationVector2() * 80f + idleOffset;
            MoveToPosition(targetPos, 0.12f);

            glowIntensity = 0.3f;
            armTension = 0.2f;

            //搜索敌人
            if (StateTimer > IdleDuration) {
                NPC target = owner.Center.FindClosestNPC(SearchRange);
                if (target != null) {
                    targetNPCID = target.whoAmI;
                    State = ArmState.Targeting;
                    StateTimer = 0;
                }
            }

            //火焰粒子
            if (Main.rand.NextBool(8)) {
                SpawnIdleFlame();
            }
        }

        private void TargetingBehavior(Player owner) {
            if (!IsTargetValid()) {
                State = ArmState.Idle;
                StateTimer = 0;
                return;
            }

            NPC target = Main.npc[targetNPCID];

            //移动到目标侧面
            Vector2 toTarget = target.Center - owner.Center;
            float targetAngle = toTarget.ToRotation();
            Vector2 approachPos = target.Center + (targetAngle + MathHelper.PiOver2).ToRotationVector2() * 120f;

            MoveToPosition(approachPos, 0.18f);

            glowIntensity = 0.5f;
            armTension = 0.5f;

            //到达位置后开始蓄力
            if (Vector2.Distance(Projectile.Center, approachPos) < 100f || StateTimer > 40) {
                State = ArmState.WindingUp;
                StateTimer = 0;
                attackStartPos = Projectile.Center;
                attackTargetPos = target.Center;
            }
        }

        private void WindUpBehavior() {
            if (!IsTargetValid()) {
                State = ArmState.Idle;
                StateTimer = 0;
                return;
            }

            float progress = StateTimer / WindUpDuration;
            glowIntensity = 0.5f + progress * 0.4f;
            armTension = 0.8f;

            //向后拉，准备挥砍
            Vector2 windUpOffset = new Vector2(-120f, -80f);
            Vector2 targetPos = attackStartPos + windUpOffset;
            MoveToPosition(targetPos, 0.25f);

            //蓄力粒子
            if (Main.rand.NextBool(2)) {
                SpawnWindUpFlame();
            }

            if (StateTimer >= WindUpDuration) {
                State = ArmState.Slashing;
                StateTimer = 0;
                slashAngle = 0f;

                //挥砍音效
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Volume = 0.7f,
                    Pitch = -0.1f
                }, Projectile.Center);
            }
        }

        private void SlashingBehavior() {
            float progress = StateTimer / SlashDuration;
            glowIntensity = 1f;
            armTension = 1f;

            //快速弧形挥砍
            slashAngle = MathHelper.Lerp(
                MathHelper.Pi * 0.8f,
                -MathHelper.PiOver4,
                CWRUtils.EaseInOutCubic(progress)
            );

            Vector2 slashOffset = new Vector2(
                (float)Math.Cos(slashAngle) * 180f,
                (float)Math.Sin(slashAngle) * 140f
            );

            Projectile.Center = attackTargetPos + slashOffset;
            Projectile.velocity = (attackTargetPos - Projectile.Center) * 0.8f;

            //挥砍轨迹特效
            SpawnSlashEffect();

            if (StateTimer >= SlashDuration) {
                State = ArmState.Recovering;
                StateTimer = 0;
                CreateSlashImpact(Projectile.Center);
            }
        }

        private void RecoveringBehavior(Player owner) {
            float progress = StateTimer / RecoverDuration;
            glowIntensity = 1f - progress * 0.7f;
            armTension = 0.3f;

            //返回待机位置
            float angle = MathHelper.Pi + (ArmIndex - 3.5f) * 0.4f;
            Vector2 recoverPos = shoulderPos + angle.ToRotationVector2() * 80f + idleOffset;
            MoveToPosition(recoverPos, 0.15f);

            if (StateTimer >= RecoverDuration) {
                State = ArmState.Idle;
                StateTimer = 0;
            }
        }

        private void MoveToPosition(Vector2 target, float speed) {
            Vector2 direction = target - Projectile.Center;
            float distance = direction.Length();

            if (distance > 5f) {
                direction.Normalize();
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * distance * speed, 0.25f);
            }
            else {
                Projectile.velocity *= 0.9f;
            }

            Projectile.Center += Projectile.velocity;
        }

        private void UpdateArmIK() {
            handPos = Projectile.Center;

            float targetDistance = Vector2.Distance(shoulderPos, handPos);
            float maxReach = SegmentLength * ArmSegmentCount;

            if (targetDistance > maxReach * 0.98f) {
                Vector2 direction = (handPos - shoulderPos).SafeNormalize(Vector2.Zero);
                handPos = shoulderPos + direction * maxReach * 0.98f;
                Projectile.Center = handPos;
            }

            //FABRIK算法 - 前向
            armSegments[0] = handPos;
            for (int i = 1; i < ArmSegmentCount; i++) {
                Vector2 direction = (armSegments[i - 1] - (i == ArmSegmentCount - 1 ? shoulderPos : armSegments[i])).SafeNormalize(Vector2.Zero);
                float bendFactor = (float)Math.Sin((i / (float)ArmSegmentCount) * MathHelper.Pi) * armTension;
                Vector2 perpendicular = new Vector2(-direction.Y, direction.X) * bendFactor * 12f;
                armSegments[i] = armSegments[i - 1] - direction * SegmentLength + perpendicular;
            }

            //FABRIK算法 - 反向
            armSegments[ArmSegmentCount - 1] = shoulderPos;
            for (int i = ArmSegmentCount - 2; i >= 0; i--) {
                Vector2 direction = (armSegments[i] - armSegments[i + 1]).SafeNormalize(Vector2.Zero);
                float bendFactor = (float)Math.Sin((i / (float)ArmSegmentCount) * MathHelper.Pi) * armTension;
                Vector2 perpendicular = new Vector2(-direction.Y, direction.X) * bendFactor * 12f;
                armSegments[i] = armSegments[i + 1] + direction * SegmentLength + perpendicular;
            }

            Projectile.Center = armSegments[0];

            //更新手部旋转
            if (armSegments.Count >= 2) {
                Vector2 handDirection = armSegments[0] - armSegments[1];
                handRotation = handDirection.ToRotation();
            }
        }

        private void UpdateTrail() {
            trailPositions.Insert(0, Projectile.Center);
            if (trailPositions.Count > MaxTrailLength) {
                trailPositions.RemoveAt(trailPositions.Count - 1);
            }
        }

        private bool IsTargetValid() {
            if (targetNPCID < 0 || targetNPCID >= Main.maxNPCs) return false;
            NPC target = Main.npc[targetNPCID];
            return target.active && target.CanBeChasedBy();
        }

        //特效方法
        private void SpawnIdleFlame() {
            Dust dust = Dust.NewDustDirect(
                Projectile.position,
                Projectile.width,
                Projectile.height,
                DustID.Torch,
                Scale: Main.rand.NextFloat(0.8f, 1.2f)
            );
            dust.velocity = Main.rand.NextVector2Circular(1f, 1f);
            dust.noGravity = true;
        }

        private void SpawnWindUpFlame() {
            Vector2 velocity = Main.rand.NextVector2Circular(2f, 2f);
            Dust dust = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(30f, 30f),
                DustID.Torch,
                velocity,
                100,
                default,
                Main.rand.NextFloat(1.2f, 1.6f)
            );
            dust.noGravity = true;
        }

        private void SpawnSlashEffect() {
            if (Main.rand.NextBool(2)) {
                Dust flame = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Torch,
                    Scale: Main.rand.NextFloat(1.5f, 2.2f)
                );
                flame.velocity = Projectile.velocity * 0.3f;
                flame.noGravity = true;
            }

            //烟雾轨迹
            if (Main.rand.NextBool()) {
                Dust smoke = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Smoke,
                    Projectile.velocity * 0.2f,
                    100,
                    new Color(100, 100, 100),
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                smoke.noGravity = true;
            }
        }

        private void CreateSlashImpact(Vector2 position) {
            //火焰冲击波
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);

                Dust dust = Dust.NewDustPerfect(
                    position,
                    DustID.Torch,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                dust.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = 0.6f,
                Pitch = -0.3f
            }, position);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中特效
            for (int i = 0; i < 10; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                Dust dust = Dust.NewDustPerfect(
                    target.Center,
                    DustID.Torch,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.5f, 2f)
                );
                dust.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.NPCHit2 with {
                Volume = 0.5f,
                Pitch = 0.1f
            }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //消散特效
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(5f, 5f);
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Torch,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                dust.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item8 with {
                Volume = 0.4f,
                Pitch = -0.4f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;

            //绘制手臂链
            DrawArmChain(sb, lightColor);

            //绘制手掌和柴刀
            DrawHandAndMachete(sb, lightColor);

            return false;
        }

        private void DrawArmChain(SpriteBatch sb, Color lightColor) {
            if (OniMachete.SepulcherForearm == null) return;

            Texture2D armTexture = OniMachete.SepulcherForearm;

            for (int i = 0; i < armSegments.Count - 1; i++) {
                Vector2 start = armSegments[i + 1];
                Vector2 end = armSegments[i];
                Vector2 diff = end - start;
                float rotation = diff.ToRotation() + MathHelper.PiOver2;
                float length = diff.Length();

                Color drawColor = lightColor;
                //添加火焰发光
                drawColor = Color.Lerp(drawColor, new Color(255, 150, 50), glowIntensity * 0.5f);

                sb.Draw(
                    armTexture,
                    start - Main.screenPosition,
                    null,
                    drawColor * 0.9f,
                    rotation,
                    new Vector2(0, armTexture.Height / 2f),
                    1f,
                    SpriteEffects.None,
                    0
                );
            }
        }

        private void DrawHandAndMachete(SpriteBatch sb, Color lightColor) {
            if (OniMachete.SepulcherHand == null) return;

            Texture2D handTexture = OniMachete.SepulcherHand;
            Vector2 handDrawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = handTexture.Size() / 2f;

            Color drawColor = lightColor;
            drawColor = Color.Lerp(drawColor, new Color(255, 180, 80), glowIntensity * 0.6f);

            //发光层
            if (glowIntensity > 0.5f) {
                for (int i = 0; i < 2; i++) {
                    float glowAlpha = (glowIntensity - 0.5f) * (1f - i * 0.4f);
                    sb.Draw(
                        handTexture,
                        handDrawPos,
                        null,
                        new Color(255, 150, 50, 0) * glowAlpha,
                        handRotation + MathHelper.PiOver2,
                        origin,
                        Projectile.scale * (1.1f + i * 0.1f),
                        SpriteEffects.None,
                        0
                    );
                }
            }

            //主体绘制
            sb.Draw(
                handTexture,
                handDrawPos,
                null,
                drawColor,
                handRotation + MathHelper.PiOver2,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            //绘制柴刀（简化为一条亮线）
            Texture2D linePix = TextureAssets.Item[ModContent.ItemType<OniMachete>()].Value;
            Vector2 macheteStart = Projectile.Center;
            Vector2 macheteEnd = Projectile.Center + handRotation.ToRotationVector2() * 50f;
            Vector2 macheteDiff = macheteEnd - macheteStart;
            float macheteRot = macheteDiff.ToRotation();
            sb.Draw(linePix, macheteStart - Main.screenPosition, null,
            new Color(200, 200, 220) * 0.9f, macheteRot, new Vector2(0, 0.5f),
            1f, SpriteEffects.None, 0f);
        }
    }
}