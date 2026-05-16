using CalamityOverhaul.Common;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 统帅之钳 —— 战士的重型链锤式投掷武器
    /// 将沉重的铁爪挥出，借助铁链的牵引高速旋转飞行，命中时产生冲击波与屏幕震动
    /// 撞墙后强力反弹，最终被铁链拽回玩家手中
    /// </summary>
    internal class CommandersClaw : ModItem
    {
        public override string Texture => CWRConstant.Item_Rogue + "CommandersClaw";

        public override void SetDefaults() {
            Item.width = Item.height = 52;
            Item.damage = 105;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useAnimation = Item.useTime = 32;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 9.5f;
            Item.UseSound = SoundID.Item71 with { Pitch = -0.3f, Volume = 0.9f };
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<CommandersClawThrow>();
            Item.shootSpeed = 19f;
            Item.DamageType = DamageClass.Melee;
            Item.rare = ItemRarityID.Purple;
            Item.value = Item.buyPrice(0, 1, 65, 0);
            Item.CWR().DeathModeItem = true;
        }

        public override bool CanUseItem(Player player) {
            //同时只允许场上存在一只铁爪，防止铁链视觉混乱
            return player.ownedProjectileCounts[Item.shoot] <= 0;
        }

        public override Vector2? HoldoutOffset() => new Vector2(-4f, 0f);
    }

    /// <summary>
    /// 统帅之钳投出的链锤实体
    /// 阶段0(飞出): 直线高速旋转飞行，逐步减速
    /// 阶段1(回收): 铁链将爪子拽回，自动追踪玩家，速度持续提高
    /// </summary>
    internal class CommandersClawThrow : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Rogue + "CommandersClawThrow";

        //阶段标记: 0 = 飞出, 1 = 回收
        private ref float Phase => ref Projectile.ai[0];
        //阶段计时
        private ref float PhaseTimer => ref Projectile.ai[1];
        //旋转速率，逐步衰减以体现金属重量感
        private ref float SpinSpeed => ref Projectile.localAI[0];

        private Player Owner => Main.player[Projectile.owner];

        private const int FlyMaxTime = 32;          //飞出阶段最大持续帧数
        private const int MaxLifetime = 600;        //总寿命兜底
        private const float ReturnMinSpeed = 12f;   //回收阶段最低速度
        private const float ReturnAcceleration = 1.18f; //回收加速倍率

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLifetime;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
            Projectile.tileCollide = true;
            Projectile.netImportant = true;
        }

        public override void AI() {
            //初始化朝向与旋转速率
            if (Projectile.localAI[1] == 0f) {
                Projectile.spriteDirection = Projectile.velocity.X >= 0 ? 1 : -1;
                SpinSpeed = 0.65f * Projectile.spriteDirection;
                Projectile.localAI[1] = 1f;

                //出手时的金属火星迸溅
                SpawnLaunchSparks();
            }

            //自身始终保持高速旋转，体现链锤甩动的力量感
            Projectile.rotation += SpinSpeed;
            //旋转速度随时间柔和衰减，再受到回收加速影响
            SpinSpeed = MathHelper.Lerp(SpinSpeed, 0.35f * Math.Sign(SpinSpeed), 0.01f);

            if (Phase == 0f) {
                FlyOutPhase();
            }
            else {
                ReturnPhase();
            }

            //飞行轨迹上的拖尾粒子
            EmitTrailParticles();

            Lighting.AddLight(Projectile.Center, 0.55f, 0.45f, 0.25f);

            PhaseTimer++;
        }

        private void FlyOutPhase() {
            //轻微减速，营造重物投掷的惯性感
            if (Projectile.velocity.Length() > 6f) {
                Projectile.velocity *= 0.985f;
            }

            //超过飞出时长或者速度过低，铁链开始回收
            if (PhaseTimer >= FlyMaxTime || Projectile.velocity.Length() < 4f) {
                EnterReturnPhase(playSound: true);
            }
        }

        private void ReturnPhase() {
            Projectile.tileCollide = false;

            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            Vector2 toPlayer = Owner.MountedCenter - Projectile.Center;
            float speed = Math.Max(Projectile.velocity.Length(), ReturnMinSpeed);
            //铁链强力拽回，速度逐步提升
            speed = Math.Min(speed * ReturnAcceleration, 28f);
            Projectile.velocity = toPlayer.SafeNormalize(Vector2.UnitX) * speed;

            //回到玩家附近时回收
            if (toPlayer.Length() < 36f) {
                Projectile.Kill();
            }
        }

        private void EnterReturnPhase(bool playSound) {
            if (Phase == 1f) {
                return;
            }

            Phase = 1f;
            PhaseTimer = 0f;
            Projectile.tileCollide = false;
            Projectile.netUpdate = true;

            if (playSound) {
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.4f, Volume = 0.55f }, Projectile.Center);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //撞击地形时迸发火星与冲击力，并立即进入回收阶段
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.2f, Volume = 0.85f }, Projectile.Center);
            SpawnImpactSparks(oldVelocity);

            if (CWRServerConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    Projectile.Center, oldVelocity.SafeNormalize(Vector2.UnitX), 4.5f, 5f, 8, 600f, FullName));
            }

            EnterReturnPhase(playSound: false);
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命中时的强力冲击反馈：屏幕震动 + 火星爆裂 + 冲击波
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = 0.1f, Volume = 0.9f }, target.Center);

            SpawnHitImpact(target);

            if (CWRServerConfig.Instance.ScreenVibration) {
                Vector2 hitDir = (target.Center - Owner.Center).SafeNormalize(Vector2.UnitX);
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    target.Center, hitDir, 3.5f, 4.5f, 6, 500f, FullName));
            }

            //仅在飞出阶段命中三次以上后强制回收，避免长时间滞留
            if (Phase == 0f && Projectile.numHits >= 3) {
                EnterReturnPhase(playSound: true);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //飞出阶段挥击伤害更高，回收阶段为顺势拉回伤害稍低
            if (Phase == 0f) {
                modifiers.SourceDamage *= 1.15f;
                modifiers.Knockback *= 1.25f;
            }
            else {
                modifiers.SourceDamage *= 0.85f;
            }
        }

        private void SpawnLaunchSparks() {
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 12; i++) {
                Vector2 vel = dir.RotatedByRandom(0.6f) * Main.rand.NextFloat(2f, 6f);
                Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.Iron, vel,
                    100, default, Main.rand.NextFloat(1.1f, 1.6f));
                spark.noGravity = true;
            }

            for (int i = 0; i < 4; i++) {
                Vector2 vel = dir.RotatedByRandom(0.4f) * Main.rand.NextFloat(3f, 7f);
                PRT_Spark prt = new PRT_Spark(Projectile.Center, vel, false, 14, 1.4f, Color.Goldenrod);
                PRTLoader.AddParticle(prt);
            }
        }

        private void SpawnImpactSparks(Vector2 oldVelocity) {
            Vector2 dir = (-oldVelocity).SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 18; i++) {
                Vector2 vel = dir.RotatedByRandom(1.1f) * Main.rand.NextFloat(3f, 9f);
                Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel,
                    100, default, Main.rand.NextFloat(1.2f, 1.9f));
                spark.noGravity = true;
                spark.fadeIn = 1.1f;
            }

            for (int i = 0; i < 8; i++) {
                Vector2 vel = dir.RotatedByRandom(0.9f) * Main.rand.NextFloat(4f, 10f);
                PRT_Spark prt = new PRT_Spark(Projectile.Center, vel, false, 18,
                    Main.rand.NextFloat(1.3f, 2.1f), Color.Orange);
                PRTLoader.AddParticle(prt);
            }
        }

        private void SpawnHitImpact(NPC target) {
            //定向锥形碎片
            Vector2 hitDir = (target.Center - Owner.Center).SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 16; i++) {
                Vector2 vel = hitDir.RotatedByRandom(0.9f) * Main.rand.NextFloat(3f, 9f);
                Dust spark = Dust.NewDustPerfect(target.Center, DustID.Iron, vel,
                    100, default, Main.rand.NextFloat(1.3f, 1.9f));
                spark.noGravity = true;
                spark.fadeIn = 1.05f;
            }

            //环形冲击波 Spark
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);
                PRT_Spark prt = new PRT_Spark(target.Center, vel, false, 16,
                    Main.rand.NextFloat(1.4f, 2.2f),
                    Color.Lerp(Color.Goldenrod, Color.OrangeRed, Main.rand.NextFloat()));
                PRTLoader.AddParticle(prt);
            }
        }

        private void EmitTrailParticles() {
            //每隔几帧抛出一些火星，强化飞行的金属感
            if (Main.rand.NextBool(3)) {
                Vector2 spawnPos = Projectile.Center + Main.rand.NextVector2Circular(10f, 10f);
                Vector2 vel = -Projectile.velocity * 0.05f + Main.rand.NextVector2Circular(1.5f, 1.5f);
                Dust trail = Dust.NewDustPerfect(spawnPos, DustID.Iron, vel,
                    150, default, Main.rand.NextFloat(0.9f, 1.3f));
                trail.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            DrawChain();
            DrawClaw(lightColor);
            return false;
        }

        private void DrawChain() {
            if (!Owner.active) {
                return;
            }

            Texture2D chainTexture = TextureAssets.Chain12.Value;
            Vector2 playerCenter = Owner.MountedCenter;
            Vector2 toClaw = Projectile.Center - playerCenter;
            float chainRotation = toClaw.ToRotation() - MathHelper.PiOver2;
            float distance = toClaw.Length();
            int segmentHeight = Math.Max(chainTexture.Height - 2, 8);
            Vector2 step = toClaw.SafeNormalize(Vector2.UnitY) * segmentHeight;

            Vector2 currentPos = playerCenter;
            int safety = 0;
            while (distance > segmentHeight && safety++ < 64) {
                Color chainColor = Lighting.GetColor(currentPos.ToTileCoordinates());
                Main.EntitySpriteDraw(chainTexture, currentPos - Main.screenPosition, null, chainColor,
                    chainRotation, chainTexture.Size() / 2f, 1f, SpriteEffects.None, 0);
                currentPos += step;
                distance -= segmentHeight;
            }
        }

        private void DrawClaw(Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;
            SpriteEffects effects = Projectile.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float drawRot = Projectile.rotation + (Projectile.spriteDirection > 0 ? 0f : -MathHelper.PiOver2);

            //残影拖尾，营造重型旋转的速度感
            for (int k = Projectile.oldPos.Length - 1; k >= 0; k--) {
                if (Projectile.oldPos[k] == Vector2.Zero) {
                    continue;
                }

                float fade = (Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length;
                Color trailColor = lightColor * fade * 0.45f;
                Vector2 trailPos = Projectile.oldPos[k] + Projectile.Size / 2f - Main.screenPosition;
                float trailRot = (Projectile.oldRot.Length > k ? Projectile.oldRot[k] : drawRot)
                    + (Projectile.spriteDirection > 0 ? 0f : -MathHelper.PiOver2);

                Main.EntitySpriteDraw(texture, trailPos, null, trailColor, trailRot,
                    origin, Projectile.scale * (0.85f + fade * 0.15f), effects, 0);
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor,
                drawRot, origin, Projectile.scale, effects, 0);
        }

        public override void OnKill(int timeLeft) {
            //返回时的微弱火星，整体收束
            for (int i = 0; i < 6; i++) {
                Dust spark = Dust.NewDustPerfect(Projectile.Center,
                    DustID.Iron, Main.rand.NextVector2Circular(2.5f, 2.5f),
                    150, default, Main.rand.NextFloat(0.8f, 1.2f));
                spark.noGravity = true;
            }
        }
    }
}
