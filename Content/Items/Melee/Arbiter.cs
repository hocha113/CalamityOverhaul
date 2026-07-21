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
    /// 断罪师重斧，蓄力高举松手劈下+火冲击波
    internal class Arbiter : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "Arbiter";

        public override void SetDefaults() {
            Item.width = 60;
            Item.height = 60;
            Item.damage = 320;
            Item.DamageType = DamageClass.Melee;
            //蓄力由手持弹幕接管
            Item.useAnimation = 12;
            Item.useTime = 12;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTurn = false;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            //channel 供手持弹幕检测蓄力释放
            Item.channel = true;
            Item.knockBack = 13f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.shootSpeed = 1f;
            Item.value = Item.buyPrice(0, 75, 0, 0);
            Item.rare = ItemRarityID.LightRed;
            Item.shoot = ModContent.ProjectileType<ArbiterHeld>();
            Item.crit = 6;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 4;

        //仅允许一个 ArbiterHeld 实例
        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, player.Center, Vector2.Zero, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    /// 断罪师手持，蓄力→劈砍→收手→自毁
    internal class ArbiterHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "Arbiter";

        /// 蓄力阶段
        public const int PhaseCharging = 0;
        /// 劈砍阶段
        public const int PhaseSlamming = 1;
        /// 收手阶段
        public const int PhaseRecovering = 2;

        /// 蓄力最大帧数
        private const int MaxChargeTime = 90;
        /// 劈砍帧数
        private const int SlamTime = 14;

        /// 蓄力举斧角(玩家相对，MirrorAngle 镜像)
        private const float LiftAnglePlayerRel = -MathHelper.Pi * 0.62f;
        /// 劈砍终点角(玩家相对，>0 斜下)
        private const float SwingEndPlayerRel = MathHelper.Pi * 0.42f;
        /// 纹理斧刃自然指向(-π/4)
        private const float TextureBladeAngle = -MathHelper.PiOver4;
        /// 收手帧数
        private const int RecoverTime = 14;
        /// 蓄力持距
        private const float HoldDistance = 56f;
        /// 劈砍持距
        private const float SwingDistance = 84f;

        //蓄力锁定朝向
        private int lockedDirection = 1;
        //蓄力帧数，决定伤害与火焰强度
        private float chargeFrames;
        //劈砍起止角(锁定朝向)
        private float swingStartAngle;
        private float swingEndAngle;
        //当前斧旋转(刀光拖尾)
        private float currentRotation;
        //上帧斧旋转(峰值检测)
        private float lastRotation;
        //本次劈砍已触发地面冲击
        private bool impactTriggered;
        //轻微的视觉抖动相位
        private float shakePhase;
        //蓄力时积累的环境粒子计数
        private int chargeParticleClock;

        //斧心世界坐标
        private Vector2 axePivot;

        public int Phase {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }
        private ref float PhaseTimer => ref Projectile.ai[1];

        /// 当前蓄力比 [0,1]
        public float ChargeRatio => MathHelper.Clamp(chargeFrames / MaxChargeTime, 0f, 1f);

        public override void SetDefaults() {
            //宽 hitbox + Colliding 精判
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

        //蓄力不参与碰撞，劈砍过半才伤
        public override bool? CanDamage() => Phase == PhaseSlamming && PhaseTimer >= SlamTime * 0.25f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Phase != PhaseSlamming) {
                return false;
            }
            //斧刃方向延伸碰撞箱
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
            //物品丢失则收手
            if (Item.type != ModContent.ItemType<Arbiter>()) {
                Projectile.Kill();
                return;
            }

            //首帧锁朝向+抬手音
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

        /// 蓄力，按住累计，松手或满蓄进劈砍
        private void UpdateCharging() {
            //蓄力到上限后强制释放
            bool reachedCap = chargeFrames >= MaxChargeTime;
            //松手判定(DownLeft=按住左键)
            bool released = !DownLeft && chargeFrames >= 1;

            //举斧位(π−θ 镜像非变号)
            float liftAngle = MirrorAngle(LiftAnglePlayerRel);
            //蓄力抖动
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

            //满蓄闪光
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

        /// 进入劈砍，定起止角
        private void EnterSlamPhase() {
            Phase = PhaseSlamming;
            PhaseTimer = 0;
            impactTriggered = false;
            //满蓄挥幅更大终点更下(π−θ 镜像)
            swingStartAngle = MirrorAngle(LiftAnglePlayerRel);
            swingEndAngle = MirrorAngle(SwingEndPlayerRel);
            currentRotation = swingStartAngle;
            lastRotation = swingStartAngle;

            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 1.05f, Pitch = -0.5f - ChargeRatio * 0.1f }, Owner.Center);
            SoundEngine.PlaySound(SoundID.DD2_OgreRoar with { Volume = 0.45f, Pitch = 0.2f }, Owner.Center);
        }

        /// 劈砍弧线，末段触发地面冲击
        private void UpdateSlamming() {
            lastRotation = currentRotation;

            float progress = MathHelper.Clamp(PhaseTimer / SlamTime, 0f, 1f);
            //EaseInQuart 末段猛落
            float eased = progress * progress * progress * progress;
            currentRotation = MathHelper.Lerp(swingStartAngle, swingEndAngle, eased);

            //劈砍略前伸强化砸感
            float distance = MathHelper.Lerp(HoldDistance, SwingDistance, eased);
            axePivot = GetHandPos() + currentRotation.ToRotationVector2() * distance;

            //每帧沿劈砍轨迹喷火星
            SpawnSlamTrailParticles(progress);

            //80% 进度地面冲击
            if (!impactTriggered && progress >= 0.78f) {
                impactTriggered = true;
                TriggerGroundImpact();
            }

            if (PhaseTimer >= SlamTime) {
                Phase = PhaseRecovering;
                PhaseTimer = 0;
            }
        }

        /// 收手，短暂停留再自毁
        private void UpdateRecovering() {
            //收力姿势(朝左镜像加 0.35)
            float t = MathHelper.Clamp(PhaseTimer / RecoverTime, 0f, 1f);
            float pullBack = 0.35f * lockedDirection;
            float restAngle = MathHelper.Lerp(swingEndAngle, swingEndAngle - pullBack, t);
            currentRotation = restAngle;
            axePivot = GetHandPos() + currentRotation.ToRotationVector2() * SwingDistance;

            if (PhaseTimer >= RecoverTime) {
                Projectile.Kill();
            }
        }

        /// 地面冲击，扫描地面生成 ArbiterShockwave
        private void TriggerGroundImpact() {
            if (CWRServerConfig.Instance.ScreenVibration) {
                Vector2 dir = new Vector2(lockedDirection, 1f).SafeNormalize(Vector2.UnitY);
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    Owner.Center, dir, 7.5f * (0.4f + ChargeRatio * 0.6f), 8f, 12, 700f, FullName));
                Owner.CWR().GetScreenShake(4f + ChargeRatio * 4f);
            }

            //斧下扫描地面，斧尖水平为冲击中心
            Vector2 bladeTip = axePivot + currentRotation.ToRotationVector2() * 70f;
            Vector2 scanStart = new Vector2(bladeTip.X, Owner.Center.Y - 24f);
            Vector2 groundPos = FindGroundBelow(scanStart, 28);

            if (Projectile.IsOwnedByLocalPlayer()) {
                int damage = (int)(Projectile.damage * (0.55f + ChargeRatio * 1.55f));
                //满蓄蔓延更远(每边≤60格)
                int spreadTiles = (int)MathHelper.Lerp(14f, 60f, ChargeRatio);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), groundPos
                    , Vector2.Zero, ModContent.ProjectileType<ArbiterShockwave>()
                    , damage, Projectile.knockBack, Projectile.owner, ai0: spreadTiles, ai1: ChargeRatio);
            }
        }

        /// 双臂跟斧朝向
        private void UpdatePlayerPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = lockedDirection;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
            Owner.itemRotation = currentRotation;

            //双手跟斧朝向
            float armAngle = currentRotation - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armAngle);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full
                , armAngle + 0.15f * lockedDirection);

            //hitbox 跟斧位覆盖劈砍轨迹
            Projectile.Center = axePivot;
            Projectile.timeLeft = 2;
        }

        /// 蓄力斧刃火星烟雾
        private void SpawnChargingParticles() {
            chargeParticleClock++;

            //斧刃位(持柄延伸 50px)
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

            //满蓄浓烟火焰
            if (ChargeRatio > 0.6f && chargeParticleClock % 3 == 0) {
                Vector2 vel = -currentRotation.ToRotationVector2() * Main.rand.NextFloat(1.2f, 2.6f)
                    + Main.rand.NextVector2Circular(1.4f, 1.4f);
                PRTLoader.NewParticle<PRT_LavaFire>(bladePos + Main.rand.NextVector2Circular(18f, 18f), vel
                    , Color.White, 0.6f + ChargeRatio * 0.5f);
            }

            //满蓄汇聚光线
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

        /// 劈砍弧线火焰拖尾
        private void SpawnSlamTrailParticles(float progress) {
            //拖尾从 root 到 tip 沿斧头方向喷出
            Vector2 axisDir = currentRotation.ToRotationVector2();
            Vector2 bladeTip = axePivot + axisDir * 70f;

            //每帧 4~6 火星 + 1~2 PRT
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

            //大颗 PRT 沿挥砍方向
            if (Main.rand.NextBool(2)) {
                Vector2 pos = bladeTip + Main.rand.NextVector2Circular(10f, 10f);
                Vector2 vel = axisDir * Main.rand.NextFloat(0.5f, 2.0f);
                PRTLoader.NewParticle<PRT_LavaFire>(pos, vel, Color.White, 0.8f + ChargeRatio * 0.7f);
            }
        }

        /// 向下扫描实心地面顶，无则 fallback
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
            //无地面 fallback 近原点
            return new Vector2(worldPos.X, (ty + 4) * 16f);
        }

        /// 手心世界坐标(持斧支点)
        private Vector2 GetHandPos() {
            //身体中心微偏移防贴脸
            Vector2 pivot = Owner.GetPlayerStabilityCenter();
            pivot.Y -= 6f * Owner.gravDir;
            return pivot;
        }

        /// 玩家相对角按朝向镜像(π−θ，非简单变号)
        private float MirrorAngle(float rightFacingAngle) {
            return lockedDirection > 0 ? rightFacingAngle : MathHelper.Pi - rightFacingAngle;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Item[ModContent.ItemType<Arbiter>()].Value;
            Vector2 origin = tex.Size() / 2f;
            //纹理 -π/4 补偿，currentRotation 表斧刃指向
            float drawRot = currentRotation - TextureBladeAngle;
            SpriteEffects effect = SpriteEffects.None;
            if (Owner.direction == -1) {
                effect = SpriteEffects.FlipVertically;
                drawRot -= MathHelper.PiOver2;
            }

            Vector2 drawPos = axePivot - Main.screenPosition;
            float scale = 1.05f + ChargeRatio * 0.18f;

            //蓄力红色外光晕
            if (ChargeRatio > 0.05f) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                if (glow != null) {
                    Color glowColor = Color.Lerp(new Color(255, 80, 30, 0), new Color(255, 200, 100, 0), ChargeRatio);
                    float pulse = 0.85f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.15f;
                    Main.spriteBatch.Draw(glow, drawPos, null, glowColor * ChargeRatio * 0.95f * pulse
                        , 0f, glow.Size() / 2f, (0.8f + ChargeRatio * 1.1f) * pulse, SpriteEffects.None, 0);
                }
            }

            //劈砍拖尾残影(角度采样)
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

            //满蓄核心高光
            if (ChargeRatio > 0.7f) {
                Color hot = Color.Lerp(new Color(255, 200, 120, 0), new Color(255, 255, 255, 0)
                    , (ChargeRatio - 0.7f) / 0.3f);
                Main.spriteBatch.Draw(tex, drawPos, null, hot, drawRot, origin, scale * 1.02f, effect, 0);
            }

            return false;
        }
    }

    /// 地面冲击点，火柱+火蛇+火坑
    internal class ArbiterShockwave : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// 蔓延格数(每边)
        private ref float SpreadTiles => ref Projectile.ai[0];
        /// 蓄力比(视觉规模)
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

            //撞击点火柱(≈12帧)
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

                //大火焰粒垂直上喷
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
                vel.Y -= 2f;//整体上抬
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
                //撞击点持续火(蓄力越久越久)
                int holdFire = (int)MathHelper.Lerp(180, 360, ChargeRatio);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center
                    , Vector2.Zero, ModContent.ProjectileType<ArbiterGroundFire>()
                    , Math.Max(1, (int)(Projectile.damage * 0.18f)), 0f
                    , Projectile.owner, ai0: holdFire, ai1: 1.2f);
            }
        }
    }

    /// 火蛇，沿地形蔓延
    internal class ArbiterFireSnake : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// 方向 +1右 -1左
        private ref float MoveDir => ref Projectile.ai[0];
        /// 剩余可走格数(每跨 tile 减1)
        private ref float TileBudget => ref Projectile.ai[1];

        //约半格/帧
        private const float StepPixels = 8f;
        //最大向上爬升(tile)
        private const int MaxStepUp = 3;
        //向下找地面最大 tile 数
        private const int MaxFallSearch = 18;
        //持久火坑间隔(像素)
        private const float GroundFireSpacing = 22f;

        //上次火坑坐标
        private Vector2 lastGroundFirePos;
        //初始 X 限总位移
        private float startX;
        //已停止(熄灭后死亡)
        private bool stopped;
        //停止衰减计时
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
            Projectile.localNPCHitCooldown = 24;
            Projectile.timeLeft = 240;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override bool? CanDamage() {
            if (stopped) {
                return false;
            }
            return null;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0) {
                Projectile.localAI[0] = 1;
                startX = Projectile.Center.X;
                lastGroundFirePos = Projectile.Center;
                //首帧生成点火
                SpawnGroundFireAt(Projectile.Center, lifetime: 240, scale: 1.3f);
            }

            if (stopped) {
                //停止后粒子续喷再死亡
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

            //本帧目标 X 推进
            Vector2 next = Projectile.Center + new Vector2(StepPixels * dir, 0f);
            //脚底 tile 基准
            int currentTileY = (int)Math.Floor(Projectile.Bottom.Y / 16f);

            //目标 X 上方寻可站立地面
            int nextTileX = (int)Math.Floor(next.X / 16f);
            int groundY = FindStandableGroundY(nextTileX, currentTileY);

            if (groundY == -1) {
                //无地面(深渊)
                stopped = true;
                SoundEngine.PlaySound(SoundID.NPCDeath14 with { Volume = 0.3f, Pitch = 0.3f }, Projectile.Center);
                return;
            }

            //地面过高(高墙)则停
            int deltaY = groundY - currentTileY;
            if (deltaY < -MaxStepUp) {
                stopped = true;
                SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact with { Volume = 0.4f, Pitch = 0.4f }, Projectile.Center);
                return;
            }

            //吸附地面之上 2px
            Projectile.Center = new Vector2(next.X, groundY * 16f - 2f);

            //预算扣减
            if ((int)Math.Floor(next.X / 16f) != (int)Math.Floor((Projectile.Center.X - StepPixels * dir) / 16f)) {
                TileBudget -= 1f;
            }

            //超预算或位移过远则停
            float traveled = Math.Abs(Projectile.Center.X - startX);
            if (TileBudget <= 0 || traveled > 16f * 80f) {
                stopped = true;
                return;
            }

            //间隔投放持久火坑
            if (Projectile.Center.Distance(lastGroundFirePos) >= GroundFireSpacing) {
                lastGroundFirePos = Projectile.Center;
                int life = 180 + Main.rand.Next(60);
                SpawnGroundFireAt(Projectile.Center, lifetime: life, scale: Main.rand.NextFloat(0.9f, 1.25f));
            }

            //地表喷溅火焰
            SpawnEdgeFlames(1f);

            //蛇头自身的强光
            Lighting.AddLight(Projectile.Center + new Vector2(0, -8), 1.2f, 0.55f, 0.2f);
        }

        /// tile 列上寻可站立地面，失败返回 -1
        private static int FindStandableGroundY(int tx, int currentY) {
            if (!WorldGen.InWorld(tx, currentY)) {
                return -1;
            }

            //实心 tile 则向上找空气格
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
                //空气格+下方实心=可站立
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

            //向下扫斜坡/落差
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

        /// 地表喷溅火焰(克制，主效在 GroundFire)
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

            //大颗 PRT 沿火蛇方向
            if (Main.rand.NextFloat() < 0.5f * densityMul) {
                Vector2 pos = basePos + new Vector2(Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(-4f, 2f));
                Vector2 vel = new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(0.8f, 2.2f));
                PRTLoader.NewParticle<PRT_LavaFire>(pos, vel, Color.White, Main.rand.NextFloat(0.8f, 1.3f));
            }
        }

        /// 生成持久火坑 ArbiterGroundFire
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

    /// 持久地面火坑，贴地燃烧伤害
    internal class ArbiterGroundFire : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// 初始生命(帧)
        private ref float LifeMax => ref Projectile.ai[0];
        /// 视觉规模
        private ref float VisualScale => ref Projectile.ai[1];

        private ref float Timer => ref Projectile.ai[2];

        //火焰摇曳相位
        private float swayPhase;
        //视觉尺寸与 hitbox 解耦
        private float visualHeight;
        private float visualWidth;
        private float visualGroundY;

        //仅碰撞判定，不影响粒子外观
        internal static int HitboxWidth => 40;
        internal static int HitboxHeight => 124;
        internal static float BaseVisualHeight => 28f;
        internal static float BaseVisualWidth => 40f;

        public override void SetDefaults() {
            Projectile.width = HitboxWidth;
            Projectile.height = HitboxHeight;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 30;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 600;
        }

        public override bool? CanDamage() {
            if (Timer <= 4 || Timer >= Projectile.timeLeft - 6)
                return false;
            return null;
        }

        public override void AI() {
            if (Timer == 0) {
                if (LifeMax > 0) {
                    Projectile.timeLeft = (int)LifeMax;
                }
                if (VisualScale <= 0.01f) {
                    VisualScale = 1f;
                }
                Projectile.position.Y -= HitboxHeight / 2;
                swayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
                visualGroundY = Projectile.Bottom.Y;
                visualWidth = BaseVisualWidth * VisualScale;
            }

            Timer++;
            swayPhase += 0.18f;

            //高度呼吸
            float lifeRatio = Projectile.timeLeft / Math.Max(LifeMax, 1f);
            float baseHeight = BaseVisualHeight * VisualScale;
            visualWidth = BaseVisualWidth * VisualScale;
            if (Timer < 10) {
                //点燃 0→baseHeight
                visualHeight = MathHelper.Lerp(0f, baseHeight, Timer / 10f);
            }
            else if (lifeRatio < 0.3f) {
                //衰减阶段
                visualHeight = baseHeight * (lifeRatio / 0.3f);
            }
            else {
                //稳定 sin 起伏
                visualHeight = baseHeight * (0.9f + (float)Math.Sin(swayPhase) * 0.1f);
            }

            if (lifeRatio > 0.2f) {
                SpawnFlameDust();
            }

            //发光随火焰高度
            float lightFactor = visualHeight / Math.Max(baseHeight, 1f);
            Lighting.AddLight(new Vector2(Projectile.Center.X, visualGroundY - visualHeight * 0.5f)
                , 1.0f * lightFactor, 0.45f * lightFactor, 0.15f * lightFactor);
        }

        /// 分层火焰粒子(限数保性能)
        private void SpawnFlameDust() {
            if (visualHeight < 4f) {
                return;
            }

            float baseY = visualGroundY;
            float spreadX = visualWidth * 0.45f;

            //底层余烬每 3 帧 1 颗
            if (Timer % 3 == 0) {
                Vector2 pos = new Vector2(Projectile.Center.X + Main.rand.NextFloat(-spreadX, spreadX), baseY);
                Vector2 vel = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.6f, 1.4f));
                Dust d = Dust.NewDustPerfect(pos, DustID.RedTorch, vel, 100, default
                    , Main.rand.NextFloat(0.9f, 1.4f) * VisualScale);
                d.noGravity = true;
                d.fadeIn = 0.8f;
            }

            //中层主体每 2 帧 1 颗
            if (Timer % 2 == 0) {
                float hOffset = Main.rand.NextFloat(-2f, visualHeight * 0.4f);
                Vector2 pos = new Vector2(Projectile.Center.X + Main.rand.NextFloat(-spreadX * 0.7f, spreadX * 0.7f)
                    , baseY - hOffset);
                Vector2 vel = new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(1.4f, 3.2f));
                Dust d = Dust.NewDustPerfect(pos, DustID.Torch, vel, 80
                    , Color.Lerp(Color.OrangeRed, Color.Yellow, Main.rand.NextFloat()), Main.rand.NextFloat(1.2f, 1.9f) * VisualScale);
                d.noGravity = true;
                d.fadeIn = 1.0f;
            }

            //顶端 PRT 每 6 帧 1 颗
            if (Timer % 6 == 0) {
                float h = Main.rand.NextFloat(visualHeight * 0.3f, visualHeight * 0.8f);
                Vector2 pos = new Vector2(Projectile.Center.X + Main.rand.NextFloat(-spreadX * 0.5f, spreadX * 0.5f)
                    , baseY - h);
                Vector2 vel = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.6f, 1.6f));
                PRTLoader.NewParticle<PRT_LavaFire>(pos, vel, Color.White, Main.rand.NextFloat(0.7f, 1.1f) * VisualScale);
            }

            //偶发向上火星
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
