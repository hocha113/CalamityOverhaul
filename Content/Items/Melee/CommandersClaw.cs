using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
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
    /// 统帅之钳 —— 战士的重型投掷长矛
    /// 将装饰华丽的指挥官长矛掷向敌人，标枪保持枪尖朝向飞行方向
    /// 命中时迸发金属火星与冲击波，并轻微震屏，撞墙后插入地面
    /// </summary>
    internal class CommandersClaw : ModItem
    {
        public override string Texture => CWRConstant.Item_Rogue + "CommandersClaw";

        public override void SetDefaults() {
            Item.width = Item.height = 52;
            Item.damage = 205;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useAnimation = Item.useTime = 26;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 9.5f;
            Item.UseSound = SoundID.Item1 with { Pitch = -0.2f, Volume = 0.85f };
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<CommandersClawThrow>();
            Item.shootSpeed = 21f;
            Item.DamageType = DamageClass.Melee;
            Item.rare = ItemRarityID.Purple;
            Item.value = Item.buyPrice(0, 1, 65, 0);
            Item.CWR().DeathModeItem = true;
        }

        public override bool CanUseItem(Player player) => true;
    }

    /// <summary>
    /// 统帅长矛实体
    /// 阶段0: 飞行 —— 枪尖朝向速度方向，受轻微重力影响
    /// 阶段1: 嵌入 —— 撞墙后插入地形或附着到 NPC 上，短暂保留视觉
    /// </summary>
    internal class CommandersClawThrow : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Rogue + "CommandersClawThrow";

        //阶段标记: 0 = 飞行, 1 = 嵌入静止
        private ref float Phase => ref Projectile.ai[0];
        //嵌入或飞行计时
        private ref float PhaseTimer => ref Projectile.ai[1];
        //贴附到的 NPC 索引（嵌入阶段使用，-1 = 无）
        private ref float StuckNPC => ref Projectile.ai[2];
        //贴附时的相对偏移
        private ref float StuckOffsetX => ref Projectile.localAI[0];
        private ref float StuckOffsetY => ref Projectile.localAI[1];

        private Player Owner => Main.player[Projectile.owner];

        private const int FlightLifetime = 240;     //飞行阶段最大寿命
        private const int StuckLifetime = 90;       //嵌入后保留时间
        private const float Gravity = 0.18f;        //重力下坠
        private const float MaxFallSpeed = 18f;     //最大下落速度

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 3;
            Projectile.timeLeft = FlightLifetime;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
            Projectile.tileCollide = true;
            Projectile.netImportant = true;
            Projectile.arrow = false;
            StuckNPC = -1;
        }

        public override void AI() {
            //初始化朝向（枪尖始终朝向速度方向）
            if (Projectile.localAI[0] == 0f && Phase == 0f) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.4f, Volume = 0.8f }, Projectile.Center);
                SpawnLaunchSparks();
                Projectile.localAI[0] = 1f;
            }

            if (Phase == 0f) {
                FlightPhase();
            }
            else {
                StuckPhase();
            }

            EmitTrailParticles();
            Lighting.AddLight(Projectile.Center, 0.45f, 0.4f, 0.25f);

            PhaseTimer++;
        }

        private void FlightPhase() {
            //标枪经典: 枪尖始终朝向速度方向，纹理本身竖向 (枪尖朝上)，因此 +PiOver2
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

            //轻微重力下坠，起手 12 帧内不下坠保证投掷感
            if (PhaseTimer > 12 && Projectile.velocity.Y < MaxFallSpeed) {
                Projectile.velocity.Y += Gravity;
            }
        }

        private void StuckPhase() {
            //嵌入阶段不再移动，处理"贴在 NPC 身上"的情况
            int idx = (int)StuckNPC;
            if (idx >= 0 && idx < Main.maxNPCs) {
                NPC stuck = Main.npc[idx];
                if (stuck.active && !stuck.dontTakeDamage) {
                    Projectile.Center = stuck.Center + new Vector2(StuckOffsetX, StuckOffsetY);
                }
                else {
                    //目标已死或失活，长矛掉落继续受重力下坠
                    StuckNPC = -1;
                }
            }
            else if (StuckNPC == -1f && Phase == 1f) {
                //自由下坠的尾段
                Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.4f, 12f);
                Projectile.Center += Projectile.velocity;
            }

            if (PhaseTimer >= StuckLifetime) {
                Projectile.Kill();
            }
        }

        public override bool ShouldUpdatePosition() {
            //嵌入阶段手动控制位置
            return Phase == 0f;
        }

        public override bool? CanDamage() {
            //嵌入阶段不再造成伤害
            return Phase == 0f ? null : false;
        }

        private void EnterStuckPhase(NPC target) {
            Phase = 1f;
            PhaseTimer = 0f;
            Projectile.tileCollide = false;
            Projectile.velocity = Vector2.Zero;
            Projectile.timeLeft = StuckLifetime + 2;
            Projectile.netUpdate = true;

            if (target != null) {
                StuckNPC = target.whoAmI;
                Vector2 offset = Projectile.Center - target.Center;
                StuckOffsetX = offset.X;
                StuckOffsetY = offset.Y;
            }
            else {
                StuckNPC = -1;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //撞击地形时插入墙壁
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.2f, Volume = 0.8f }, Projectile.Center);
            SpawnImpactSparks(oldVelocity);

            if (CWRServerConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    Projectile.Center, oldVelocity.SafeNormalize(Vector2.UnitX), 3.5f, 5f, 8, 600f, FullName));
            }

            //保持插入时枪尖朝向飞行方向
            Projectile.rotation = oldVelocity.ToRotation() + MathHelper.PiOver4;
            //向墙内推进一点点，呈现真正"扎进"地形的视觉
            Projectile.Center += oldVelocity.SafeNormalize(Vector2.Zero) * 6f;

            EnterStuckPhase(null);
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命中时的强力冲击反馈
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = 0.05f, Volume = 0.85f }, target.Center);
            SpawnHitImpact(target);

            if (CWRServerConfig.Instance.ScreenVibration) {
                Vector2 hitDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    target.Center, hitDir, 3.2f, 4.5f, 6, 500f, FullName));
            }

            //最后一次穿透命中后嵌入到敌人身上
            if (Projectile.penetrate <= 1) {
                EnterStuckPhase(target);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //战士标枪的"重击"特性: 高伤害与击退加成
            modifiers.SourceDamage *= 1.1f;
            modifiers.Knockback *= 1.2f;
        }

        private void SpawnLaunchSparks() {
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = dir.RotatedByRandom(0.5f) * Main.rand.NextFloat(2f, 5f);
                Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.Iron, vel,
                    100, default, Main.rand.NextFloat(1.0f, 1.4f));
                spark.noGravity = true;
            }

            for (int i = 0; i < 3; i++) {
                Vector2 vel = dir.RotatedByRandom(0.3f) * Main.rand.NextFloat(3f, 6f);
                PRT_Spark prt = new PRT_Spark(Projectile.Center, vel, false, 12, 1.2f, Color.Goldenrod);
                PRTLoader.AddParticle(prt);
            }
        }

        private void SpawnImpactSparks(Vector2 oldVelocity) {
            Vector2 dir = (-oldVelocity).SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 14; i++) {
                Vector2 vel = dir.RotatedByRandom(1.0f) * Main.rand.NextFloat(3f, 8f);
                Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel,
                    100, default, Main.rand.NextFloat(1.1f, 1.7f));
                spark.noGravity = true;
                spark.fadeIn = 1.05f;
            }

            for (int i = 0; i < 6; i++) {
                Vector2 vel = dir.RotatedByRandom(0.8f) * Main.rand.NextFloat(4f, 9f);
                PRT_Spark prt = new PRT_Spark(Projectile.Center, vel, false, 16,
                    Main.rand.NextFloat(1.3f, 2.0f), Color.Orange);
                PRTLoader.AddParticle(prt);
            }
        }

        private void SpawnHitImpact(NPC target) {
            //定向锥形碎片（沿飞行方向喷出）
            Vector2 hitDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 14; i++) {
                Vector2 vel = hitDir.RotatedByRandom(0.85f) * Main.rand.NextFloat(3f, 8f);
                Dust spark = Dust.NewDustPerfect(target.Center, DustID.Iron, vel,
                    100, default, Main.rand.NextFloat(1.2f, 1.8f));
                spark.noGravity = true;
                spark.fadeIn = 1.05f;
            }

            //环形冲击波 Spark
            for (int i = 0; i < 10; i++) {
                float angle = MathHelper.TwoPi * i / 10f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3.5f, 7f);
                PRT_Spark prt = new PRT_Spark(target.Center, vel, false, 14,
                    Main.rand.NextFloat(1.3f, 2.0f),
                    Color.Lerp(Color.Goldenrod, Color.OrangeRed, Main.rand.NextFloat()));
                PRTLoader.AddParticle(prt);
            }
        }

        private void EmitTrailParticles() {
            //仅飞行阶段抛洒火星，强化飞行金属感
            if (Phase != 0f) {
                return;
            }

            if (Main.rand.NextBool(4)) {
                Vector2 spawnPos = Projectile.Center + Main.rand.NextVector2Circular(6f, 6f);
                Vector2 vel = -Projectile.velocity * 0.04f + Main.rand.NextVector2Circular(1f, 1f);
                Dust trail = Dust.NewDustPerfect(spawnPos, DustID.Iron, vel,
                    150, default, Main.rand.NextFloat(0.8f, 1.2f));
                trail.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;
            float drawRot = Projectile.rotation;

            //飞行阶段绘制残影拖尾，强化高速飞行的速度感
            if (Phase == 0f) {
                for (int k = Projectile.oldPos.Length - 1; k >= 0; k--) {
                    if (Projectile.oldPos[k] == Vector2.Zero) {
                        continue;
                    }

                    float fade = (Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length;
                    Color trailColor = lightColor * fade * 0.4f;
                    Vector2 trailPos = Projectile.oldPos[k] + Projectile.Size / 2f - Main.screenPosition;

                    Main.EntitySpriteDraw(texture, trailPos, null, trailColor, drawRot,
                        origin, Projectile.scale, SpriteEffects.None, 0);
                }
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor,
                drawRot, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            //收束时的微弱火星
            for (int i = 0; i < 4; i++) {
                Dust spark = Dust.NewDustPerfect(Projectile.Center,
                    DustID.Iron, Main.rand.NextVector2Circular(2f, 2f),
                    150, default, Main.rand.NextFloat(0.7f, 1.1f));
                spark.noGravity = true;
            }
        }
    }
}
