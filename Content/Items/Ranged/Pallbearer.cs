using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Particles;
using CalamityMod.Projectiles;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    //抬棺人 - 一把需要蓄力的强力弩
    internal class Pallbearer : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "Pallbearer";
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 80;
            Item.height = 32;
            Item.damage = 185;
            Item.DamageType = DamageClass.Ranged;
            Item.useTime = 45;
            Item.useAnimation = 45;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 6.5f;
            Item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            Item.rare = ItemRarityID.Yellow;
            Item.UseSound = null;
            Item.autoReuse = false;
            Item.shoot = ModContent.ProjectileType<PallbearerHeld>();
            Item.shootSpeed = 15f;
            Item.useAmmo = AmmoID.Arrow;
            Item.channel = true; //允许持续按住
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            //确保同时只有一个手持弹幕存在
            return player.ownedProjectileCounts[Item.shoot] == 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<PallbearerBoomerang>()] == 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //生成手持弹幕而不是直接射出箭矢
            Projectile.NewProjectile(source, position, velocity, Item.shoot, damage, knockback, player.whoAmI);
            return false;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity
            , ref int type, ref int damage, ref float knockback) {
            //修正射击起始位置到玩家中心
            position = player.GetPlayerStabilityCenter();
        }
    }

    /// <summary>
    /// 抬棺人弩发射的强力箭矢，带有华丽的视觉效果和强大的伤害
    /// </summary>
    internal class PallbearerArrow : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Ranged + "CondemnationArrow";

        private ref float ChargeLevel => ref Projectile.ai[0];  //蓄力等级
        private ref float Time => ref Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 15;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 5 + (int)(ChargeLevel * 5); //最多10次穿透
            Projectile.timeLeft = 600;
            Projectile.arrow = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
            Projectile.Calamity().pointBlankShotDuration = CalamityGlobalProjectile.DefaultPointBlankDuration;
        }

        public override void AI() {
            Time++;

            //旋转
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //轻微的追踪效果（蓄力越高追踪越强）
            if (ChargeLevel > 0.5f) {
                NPC target = Projectile.Center.FindClosestNPC(600f + ChargeLevel * 400f);
                if (target != null && Projectile.velocity.Length() > 2f) {
                    float homingStrength = 0.02f + ChargeLevel * 0.05f;
                    Projectile.velocity = Vector2.Lerp(
                        Projectile.velocity,
                        Projectile.DirectionTo(target.Center) * Projectile.velocity.Length(),
                        homingStrength
                    );
                }
            }

            //缩放脉冲效果
            Projectile.scale = 1f + (float)Math.Sin(Time * 0.2f) * 0.1f * ChargeLevel;

            //光照
            float lightIntensity = 0.5f + ChargeLevel * 0.8f;
            Color lightColor = Color.Lerp(Color.Yellow, Color.OrangeRed, ChargeLevel);
            Lighting.AddLight(Projectile.Center, lightColor.ToVector3() * lightIntensity);

            //粒子拖尾
            if (Time % 3 == 0) {
                SpawnTrailParticle();
            }

            //满蓄力时每隔一段时间释放能量环
            if (ChargeLevel >= 0.9f && Time % 20 == 0) {
                SpawnEnergyRing();
            }
        }

        private void SpawnTrailParticle() {
            if (Main.dedServ)
                return;

            Color particleColor = Color.Lerp(Color.Yellow, Color.Red, ChargeLevel);
            Vector2 particlePos = Projectile.Center - Projectile.velocity * 0.5f;

            Dust trail = Dust.NewDustPerfect(particlePos, DustID.Torch,
                -Projectile.velocity * 0.3f, 100, particleColor, 1.5f + ChargeLevel * 0.5f);
            trail.noGravity = true;
            trail.fadeIn = 1.2f;
        }

        private void SpawnEnergyRing() {
            if (Main.dedServ)
                return;

            int dustCount = 12;
            for (int i = 0; i < dustCount; i++) {
                float angle = MathHelper.TwoPi * i / dustCount;
                Vector2 offset = angle.ToRotationVector2() * 15f;
                Vector2 velocity = offset * 0.3f;

                Dust energy = Dust.NewDustPerfect(Projectile.Center + offset, DustID.Electric,
                    velocity, 100, Color.OrangeRed, 1.8f);
                energy.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //播放击中音效
            SoundEngine.PlaySound(SoundID.NPCHit53 with {
                Volume = 0.6f + ChargeLevel * 0.4f,
                Pitch = -0.2f + ChargeLevel * 0.3f
            }, Projectile.Center);

            //击中特效
            SpawnHitEffect(target);

            //蓄力越高，特殊效果越强
            if (ChargeLevel > 0.6f) {
                //高蓄力造成额外的小范围AOE
                if (Main.rand.NextBool(2)) {
                    CreateMiniExplosion(target);
                }
            }

            //满蓄力时有几率生成追踪箭矢
            if (ChargeLevel >= 0.9f && Main.rand.NextBool(3) && Projectile.IsOwnedByLocalPlayer()) {
                SpawnHomingArrow(target);
            }
        }

        private void SpawnHitEffect(NPC target) {
            if (Main.dedServ)
                return;

            //冲击火花
            int sparkCount = 8 + (int)(ChargeLevel * 12);
            for (int i = 0; i < sparkCount; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                Color sparkColor = Color.Lerp(Color.Yellow, Color.Red, Main.rand.NextFloat());

                Dust spark = Dust.NewDustPerfect(target.Center, DustID.Torch, velocity,
                    100, sparkColor, 1.5f + ChargeLevel);
                spark.noGravity = true;
            }

            //能量波纹
            if (ChargeLevel > 0.5f) {
                for (int i = 0; i < 3; i++) {
                    LineParticle line = new LineParticle(
                        target.Center,
                        Main.rand.NextVector2Circular(8f, 8f),
                        false,
                        (int)(15 + ChargeLevel * 10),
                        1.2f + ChargeLevel * 0.8f,
                        Color.OrangeRed
                    );
                    GeneralParticleHandler.SpawnParticle(line);
                }
            }
        }

        private void CreateMiniExplosion(NPC target) {
            if (Main.dedServ)
                return;

            //小型AOE伤害
            float explosionRadius = 80f + ChargeLevel * 60f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.whoAmI == target.whoAmI || !npc.CanBeChasedBy())
                    continue;

                float distance = Vector2.Distance(npc.Center, target.Center);
                if (distance < explosionRadius) {
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        int aoeGamage = (int)(Projectile.damage * 0.4f * (1f - distance / explosionRadius));
                        npc.SimpleStrikeNPC(aoeGamage, Math.Sign(npc.Center.X - target.Center.X),
                            false, 0f, null, false, 0f, true);
                    }
                }
            }

            //爆炸特效
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                Dust explosion = Dust.NewDustPerfect(target.Center, DustID.Torch,
                    velocity, 100, Color.OrangeRed, 2f);
                explosion.noGravity = true;
            }

            //爆炸音效
            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = 0.4f,
                Pitch = 0.2f
            }, target.Center);
        }

        private void SpawnHomingArrow(NPC target) {
            //在周围生成1-2支小型追踪箭
            int arrowCount = Main.rand.Next(1, 3);
            for (int i = 0; i < arrowCount; i++) {
                Vector2 spawnPos = Projectile.Center + Main.rand.NextVector2Circular(100f, 100f);
                Vector2 velocity = (target.Center - spawnPos).SafeNormalize(Vector2.Zero) * 12f;

                int arrow = Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    spawnPos,
                    velocity,
                    Type,
                    Projectile.damage / 3,
                    Projectile.knockBack * 0.5f,
                    Projectile.owner,
                    0.7f //中等蓄力等级
                );

                if (arrow >= 0 && arrow < Main.maxProjectiles) {
                    Main.projectile[arrow].timeLeft = 180;
                    Main.projectile[arrow].scale = 0.8f;
                }
            }
        }

        public override void OnKill(int timeLeft) {
            //死亡时的烟雾效果
            if (Main.dedServ)
                return;

            for (int i = 0; i < 10; i++) {
                Dust smoke = Dust.NewDustDirect(Projectile.position, Projectile.width,
                    Projectile.height, DustID.Smoke, 0f, 0f, 100, default, 1.5f);
                smoke.velocity = Main.rand.NextVector2Circular(2f, 2f);
                smoke.noGravity = true;
            }

            //高蓄力箭矢死亡时额外的能量释放
            if (ChargeLevel > 0.7f) {
                for (int i = 0; i < 15; i++) {
                    Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                    Dust energy = Dust.NewDustPerfect(Projectile.Center, DustID.Electric,
                        velocity, 100, Color.OrangeRed, 2f);
                    energy.noGravity = true;
                }
            }
        }

        public override Color? GetAlpha(Color lightColor) {
            //根据蓄力等级调整颜色
            Color baseColor = Color.Lerp(Color.White, Color.Orange, ChargeLevel * 0.6f);
            return baseColor * Projectile.Opacity;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Time <= 2)
                return false;

            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;

            //绘制残影
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = i / (float)Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(Color.Yellow, Color.Red, ChargeLevel) * (1f - progress) * 0.5f;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float trailRot = Projectile.oldRot[i];

                Main.EntitySpriteDraw(texture, trailPos, null, trailColor,
                    trailRot + MathHelper.PiOver2, origin, Projectile.scale * (1f - progress * 0.3f),
                    SpriteEffects.None, 0);
            }

            //绘制主体
            Main.EntitySpriteDraw(texture, drawPos, null, Projectile.GetAlpha(lightColor),
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            //满蓄力时绘制额外光晕
            if (ChargeLevel >= 0.9f) {
                float glowScale = Projectile.scale * (1.2f + (float)Math.Sin(Time * 0.3f) * 0.2f);
                Color glowColor = Color.OrangeRed * 0.4f;
                Main.EntitySpriteDraw(texture, drawPos, null, glowColor,
                    Projectile.rotation, origin, glowScale, SpriteEffects.None, 0);
            }

            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return Projectile.RotatingHitboxCollision(targetHitbox.TopLeft(), targetHitbox.Size(), null, ChargeLevel);
        }
    }

    /// <summary>
    /// 抬棺人弩被投掷后的回旋镖形态，会旋转攻击敌人后返回玩家手中
    /// </summary>
    internal class PallbearerBoomerang : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Ranged + "Pallbearer";

        private enum BoomerangState
        {
            Throwing,   //飞出阶段
            Returning   //返回阶段
        }

        private BoomerangState State {
            get => (BoomerangState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float Time => ref Projectile.ai[1];
        private ref float SpinSpeed => ref Projectile.localAI[0];

        private const int MaxThrowTime = 60;        //最大飞行时间
        private const float ReturnSpeed = 18f;      //返回速度
        private const float MaxSpinSpeed = 0.6f;    //最大旋转速度

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];

            //检查玩家是否存活
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            Time++;

            //旋转速度逐渐加快
            SpinSpeed = MathHelper.Min(SpinSpeed + 0.02f, MaxSpinSpeed);
            Projectile.rotation += SpinSpeed * Math.Sign(Projectile.velocity.X);

            //状态机逻辑
            switch (State) {
                case BoomerangState.Throwing:
                    HandleThrowing(owner);
                    break;
                case BoomerangState.Returning:
                    HandleReturning(owner);
                    break;
            }

            //产生旋转风暴粒子效果
            if (Time % 3 == 0) {
                SpawnSpinParticle();
            }

            //光照
            Lighting.AddLight(Projectile.Center, Color.Cyan.ToVector3() * 0.6f);
        }

        private void HandleThrowing(Player owner) {
            //飞行过程中减速
            Projectile.velocity *= 0.98f;

            //到达最大距离或时间后开始返回
            float distanceToOwner = Vector2.Distance(Projectile.Center, owner.GetPlayerStabilityCenter());
            if (distanceToOwner > 800f || Time > MaxThrowTime) {
                State = BoomerangState.Returning;
                Time = 0;
                SoundEngine.PlaySound(SoundID.Item8, Projectile.Center);
            }
        }

        private void HandleReturning(Player owner) {
            //持续向玩家移动
            Vector2 toOwner = owner.GetPlayerStabilityCenter() - Projectile.Center;
            float distance = toOwner.Length();

            if (distance < 50f) {
                //返回玩家手中
                Projectile.Kill();
                //播放回收音效
                SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.6f }, owner.Center);
                return;
            }

            //加速飞向玩家
            float acceleration = 0.5f;
            Projectile.velocity = Vector2.Lerp(
                Projectile.velocity,
                toOwner.SafeNormalize(Vector2.Zero) * ReturnSpeed,
                acceleration / distance * 100f
            );

            //确保速度不会太慢
            if (Projectile.velocity.LengthSquared() < 4f) {
                Projectile.velocity = toOwner.SafeNormalize(Vector2.Zero) * 2f;
            }
        }

        private void SpawnSpinParticle() {
            if (Main.dedServ)
                return;

            //旋转产生的风暴粒子
            for (int i = 0; i < 2; i++) {
                float angle = Projectile.rotation + MathHelper.PiOver2 * i;
                Vector2 offset = angle.ToRotationVector2() * 30f;
                Vector2 velocity = offset.RotatedBy(MathHelper.PiOver2) * 0.5f;

                Dust wind = Dust.NewDustPerfect(Projectile.Center + offset, DustID.Cloud,
                    velocity, 100, Color.LightCyan, 1.5f);
                wind.noGravity = true;
                wind.fadeIn = 1.1f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中音效
            SoundEngine.PlaySound(SoundID.NPCHit4 with {
                Volume = 0.5f,
                Pitch = -0.1f
            }, Projectile.Center);

            //击中特效 - 旋转斩击
            SpawnHitEffect(target);

            //返回阶段击中敌人后加速返回
            if (State == BoomerangState.Returning) {
                Projectile.velocity *= 1.1f;
            }
        }

        private void SpawnHitEffect(NPC target) {
            if (Main.dedServ)
                return;

            //旋转斩击效果
            int sparkCount = 12;
            for (int i = 0; i < sparkCount; i++) {
                float angle = MathHelper.TwoPi * i / sparkCount + Projectile.rotation;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 8f);

                Dust spark = Dust.NewDustPerfect(target.Center, DustID.Electric,
                    velocity, 100, Color.Cyan, 1.8f);
                spark.noGravity = true;
            }

            //冲击波
            for (int i = 0; i < 3; i++) {
                Dust shockwave = Dust.NewDustPerfect(target.Center, DustID.Cloud,
                    Main.rand.NextVector2Circular(5f, 5f), 100, Color.White, 2f);
                shockwave.noGravity = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //碰到墙壁反弹
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * 0.8f;
            }
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.8f;
            }

            //播放反弹音效
            SoundEngine.PlaySound(SoundID.Item10 with {
                Volume = 0.4f,
                Pitch = 0.3f
            }, Projectile.Center);

            //反弹特效
            for (int i = 0; i < 5; i++) {
                Dust bounce = Dust.NewDustDirect(Projectile.position, Projectile.width,
                    Projectile.height, DustID.Cloud, 0f, 0f, 100, default, 1.3f);
                bounce.velocity = Main.rand.NextVector2Circular(3f, 3f);
            }

            return false; //不销毁弹幕
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Item[ModContent.ItemType<Items.Ranged.Pallbearer>()].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;
            float scale = Projectile.scale;

            //绘制残影
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = i / (float)Projectile.oldPos.Length;
                Color trailColor = Color.Cyan * (1f - progress) * 0.4f;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float trailRot = Projectile.oldRot[i];

                Main.EntitySpriteDraw(texture, trailPos, null, trailColor,
                    trailRot, origin, scale * (1f - progress * 0.2f), SpriteEffects.None, 0);
            }

            //绘制主体
            Main.EntitySpriteDraw(texture, drawPos, null, lightColor,
                Projectile.rotation, origin, scale, SpriteEffects.None, 0);

            //绘制旋转能量环
            if (Time % 6 < 3) {
                float ringScale = scale * (1.2f + (float)Math.Sin(Time * 0.3f) * 0.2f);
                Color ringColor = Color.Cyan * 0.3f;
                Main.EntitySpriteDraw(texture, drawPos, null, ringColor,
                    Projectile.rotation + MathHelper.PiOver4, origin, ringScale, SpriteEffects.None, 0);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            //返回时的光效
            if (Main.dedServ)
                return;

            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                Dust returnDust = Dust.NewDustPerfect(Projectile.Center, DustID.Electric,
                    velocity, 100, Color.Cyan, 1.8f);
                returnDust.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 抬棺人弩的手持弹幕，负责蓄力、装填和射击的动画逻辑
    /// </summary>
    internal class PallbearerHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Ranged + "Pallbearer";

        //弩的状态机
        private enum CrossbowState
        {
            Idle,           //待机
            Loading,        //装填箭矢
            Charged,        //蓄力完成
            Firing,         //发射
            Throwing        //投掷弩本身
        }

        private CrossbowState State {
            get => (CrossbowState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float StateTimer => ref Projectile.ai[1];
        private ref float ChargeLevel => ref Projectile.localAI[0]; //蓄力等级 0-1
        private ref float ThrowCooldown => ref Projectile.localAI[1]; //投掷冷却

        //动画帧控制
        private int animationFrame = 0;
        private float armRotation = 0f;

        //常量配置
        private const int LoadDuration = 35;        //装填时长
        private const int MaxChargeDuration = 60;   //最大蓄力时长
        private const int FireDuration = 15;        //射击动画时长
        private const int ThrowCooldownTime = 120;  //投掷后的冷却

        //弩弦相关
        private float bowstringPullback = 0f; //弓弦拉动进度

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 4; //4帧动画
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 2;
        }

        public override void AI() {
            //基础设置
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            SetHeld();

            //根据当前状态执行对应逻辑
            switch (State) {
                case CrossbowState.Idle:
                    HandleIdle();
                    break;
                case CrossbowState.Loading:
                    HandleLoading();
                    break;
                case CrossbowState.Charged:
                    HandleCharged();
                    break;
                case CrossbowState.Firing:
                    HandleFiring();
                    break;
            }

            //更新持有者的手臂动作
            UpdateOwnerArms();

            //更新位置和旋转
            UpdatePositionAndRotation();

            StateTimer++;
        }

        private void HandleIdle() {
            animationFrame = 0;
            bowstringPullback = 0f;

            //按住左键开始装填
            if (DownLeft && Owner.HasAmmo(Owner.ActiveItem())) {
                State = CrossbowState.Loading;
                StateTimer = 0;
                SoundEngine.PlaySound(SoundID.Item149, Owner.Center); //装填音效
            }

            //右键投掷（需要冷却结束）
            if (DownRight && ThrowCooldown <= 0) {
                ThrowCrossbow();
            }
        }

        private void HandleLoading() {
            float loadProgress = StateTimer / LoadDuration;
            animationFrame = (int)MathHelper.Lerp(0, 2, loadProgress);
            bowstringPullback = MathHelper.SmoothStep(0f, 1f, loadProgress);

            //装填动画期间的粒子效果
            if (StateTimer % 8 == 0 && !Main.dedServ) {
                Vector2 dustPos = Projectile.Center + Projectile.velocity * 20f;
                for (int i = 0; i < 3; i++) {
                    Dust dust = Dust.NewDustPerfect(dustPos, DustID.Smoke,
                        Main.rand.NextVector2Circular(2f, 2f), 100, default, 1.2f);
                    dust.noGravity = true;
                }
            }

            //装填完成
            if (StateTimer >= LoadDuration) {
                State = CrossbowState.Charged;
                StateTimer = 0;
                ChargeLevel = 0f;
                SoundEngine.PlaySound(SoundID.Item102 with { Pitch = -0.3f }, Owner.Center);

                //装填完成特效
                SpawnLoadCompleteEffect();
            }

            //松开按键取消装填
            if (!DownLeft) {
                State = CrossbowState.Idle;
                StateTimer = 0;
            }
        }

        private void HandleCharged() {
            animationFrame = 2; //保持装填状态帧
            bowstringPullback = 1f;

            //持续按住蓄力
            if (DownLeft && StateTimer < MaxChargeDuration) {
                ChargeLevel = StateTimer / MaxChargeDuration;

                //蓄力粒子效果
                if (StateTimer % 5 == 0) {
                    SpawnChargeParticle();
                }

                //蓄力音效
                if (StateTimer % 15 == 0) {
                    SoundEngine.PlaySound(SoundID.Item149 with {
                        Volume = 0.3f,
                        Pitch = ChargeLevel * 0.5f
                    }, Owner.Center);
                }
            }

            //松开或达到最大蓄力时发射
            if (!DownLeft || StateTimer >= MaxChargeDuration) {
                State = CrossbowState.Firing;
                StateTimer = 0;
                FireArrow();
            }
        }

        private void HandleFiring() {
            float fireProgress = StateTimer / FireDuration;
            animationFrame = 3; //射击帧
            bowstringPullback = 1f - fireProgress;

            //射击完成后判断是否投掷
            if (StateTimer >= FireDuration) {
                //随机决定是否投掷（70%概率）
                if (Main.rand.NextBool(7, 10)) {
                    ThrowCrossbow();
                }
                else {
                    //返回待机状态
                    State = CrossbowState.Idle;
                    StateTimer = 0;
                    ChargeLevel = 0f;
                }
            }
        }

        private void FireArrow() {
            if (!Projectile.IsOwnedByLocalPlayer())
                return;

            //消耗弹药
            Owner.PickAmmo(Owner.ActiveItem(), out int projToShoot, out float speed,
                out int damage, out float knockback, out int usedAmmoItemId, false);

            //计算伤害加成（基于蓄力等级）
            float damageMultiplier = 1f + ChargeLevel * 1.5f; //最高250%伤害
            int finalDamage = (int)(Projectile.damage * damageMultiplier);

            //发射箭矢
            Vector2 shootVelocity = Projectile.velocity * (Item.shootSpeed + ChargeLevel * 5f);
            int arrow = Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center + Projectile.velocity * 30f,
                shootVelocity,
                ModContent.ProjectileType<PallbearerArrow>(),
                finalDamage,
                Projectile.knockBack * (1f + ChargeLevel * 0.5f),
                Owner.whoAmI,
                ChargeLevel
            );

            //播放射击音效
            SoundEngine.PlaySound(SoundID.DD2_BallistaTowerShot with {
                Volume = 0.8f + ChargeLevel * 0.4f,
                Pitch = -0.1f + ChargeLevel * 0.2f
            }, Projectile.Center);

            //射击特效
            SpawnFireEffect();
        }

        private void ThrowCrossbow() {
            if (!Projectile.IsOwnedByLocalPlayer())
                return;

            //生成回旋镖弹幕
            int boomerang = Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                Projectile.velocity * 14f,
                ModContent.ProjectileType<PallbearerBoomerang>(),
                (int)(Projectile.damage * 0.8f),
                Projectile.knockBack * 1.2f,
                Owner.whoAmI
            );

            //播放投掷音效
            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.3f }, Projectile.Center);

            //设置冷却
            ThrowCooldown = ThrowCooldownTime;

            //杀死当前弹幕
            Projectile.Kill();
        }

        private void UpdateOwnerArms() {
            float targetArmRot = Projectile.rotation;

            //根据状态调整手臂角度
            switch (State) {
                case CrossbowState.Loading:
                    //装填时手臂向后拉
                    armRotation = MathHelper.Lerp(armRotation, targetArmRot - 0.5f * Owner.direction, 0.15f);
                    Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, armRotation);
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Quarter, targetArmRot);
                    break;

                case CrossbowState.Charged:
                    //蓄力时保持拉弦姿势，轻微震动
                    float vibration = (float)Math.Sin(StateTimer * 0.3f) * 0.03f;
                    armRotation = targetArmRot - 0.6f * Owner.direction + vibration;
                    Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, armRotation);
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, targetArmRot);
                    break;

                case CrossbowState.Firing:
                    //射击时快速前伸
                    armRotation = MathHelper.Lerp(armRotation, targetArmRot, 0.4f);
                    Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, armRotation);
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, targetArmRot);
                    break;

                default:
                    //待机状态
                    armRotation = MathHelper.Lerp(armRotation, targetArmRot, 0.2f);
                    Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Quarter, armRotation);
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.ThreeQuarters, targetArmRot);
                    break;
            }

            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
        }

        private void UpdatePositionAndRotation() {
            Vector2 ownerCenter = Owner.GetPlayerStabilityCenter();

            //计算弩的持握距离
            float holdDistance = 20f;
            if (State == CrossbowState.Loading || State == CrossbowState.Charged) {
                holdDistance += bowstringPullback * 8f; //装填/蓄力时稍微拉远
            }

            //更新位置
            Projectile.Center = ownerCenter + Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction) * holdDistance;

            //更新旋转
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Projectile.velocity.X < 0) {
                Projectile.rotation += MathHelper.Pi;
            }

            //更新朝向
            Owner.ChangeDir(Projectile.velocity.X > 0 ? 1 : -1);
            Owner.itemRotation = Projectile.rotation * Owner.direction;

            //冷却倒计时
            if (ThrowCooldown > 0) {
                ThrowCooldown--;
            }
        }

        //生成装填完成特效
        private void SpawnLoadCompleteEffect() {
            if (Main.dedServ)
                return;

            for (int i = 0; i < 12; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(4f, 4f);
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, velocity, 100, Color.Cyan, 1.5f);
                dust.noGravity = true;
            }
        }

        //生成蓄力粒子
        private void SpawnChargeParticle() {
            if (Main.dedServ)
                return;

            Color chargeColor = Color.Lerp(Color.Yellow, Color.OrangeRed, ChargeLevel);
            Vector2 particlePos = Projectile.Center + Main.rand.NextVector2Circular(15f, 15f);
            Vector2 particleVel = (Projectile.Center - particlePos).SafeNormalize(Vector2.Zero) * 2f;

            Dust charge = Dust.NewDustPerfect(particlePos, DustID.Electric, particleVel, 100, chargeColor, 1.2f);
            charge.noGravity = true;
            charge.fadeIn = 1.2f;
        }

        //生成射击特效
        private void SpawnFireEffect() {
            if (Main.dedServ)
                return;

            Vector2 muzzlePos = Projectile.Center + Projectile.velocity * 30f;

            //火花爆发
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Projectile.velocity.RotatedByRandom(0.4f) * Main.rand.NextFloat(2f, 8f);
                Dust spark = Dust.NewDustPerfect(muzzlePos, DustID.Torch, velocity, 100, Color.OrangeRed, 1.8f);
                spark.noGravity = true;
            }

            //烟雾
            for (int i = 0; i < 8; i++) {
                Dust smoke = Dust.NewDustPerfect(muzzlePos, DustID.Smoke,
                    Projectile.velocity.RotatedByRandom(0.2f) * Main.rand.NextFloat(1f, 3f), 100, default, 2f);
                smoke.noGravity = false;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;
            float rotation = Projectile.rotation;
            SpriteEffects effects = Projectile.velocity.X < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            //绘制弩身
            Main.EntitySpriteDraw(texture, drawPos, null, lightColor, rotation, origin,
                Projectile.scale, effects, 0);

            //绘制蓄力时的光效
            if (State == CrossbowState.Charged && ChargeLevel > 0.3f) {
                Color glowColor = Color.Lerp(Color.Yellow, Color.Red, ChargeLevel) * (0.4f + ChargeLevel * 0.6f);
                for (int i = 0; i < 3; i++) {
                    float offsetAngle = MathHelper.TwoPi * i / 3f + StateTimer * 0.1f;
                    Vector2 offset = offsetAngle.ToRotationVector2() * (2f + ChargeLevel * 3f);
                    Main.EntitySpriteDraw(texture, drawPos + offset, null, glowColor * 0.5f,
                        rotation, origin, Projectile.scale, effects, 0);
                }
            }

            return false;
        }
    }
}
