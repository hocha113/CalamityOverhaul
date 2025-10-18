using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishTunabeard : FishSkill
    {
        public override int UnlockFishID => ItemID.CapnTunabeard;
        public override int DefaultCooldown => 90 - HalibutData.GetDomainLayer() * 6;//冷却时间随领域层数减少
        public override int ResearchDuration => 60 * 15;

        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override bool? CanUseItem(Item item, Player player) {
            if (player.altFunctionUse == 2) {
                //检查冷却
                if (Cooldown > 0) {
                    return false;
                }

                item.UseSound = null;
                Use(item, player);
                return false;
            }
            return null;
        }

        public override void Use(Item item, Player player) {
            //设置冷却
            SetCooldown();

            //计算冲刺方向（朝向光标）
            Vector2 dashDirection = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX * player.direction);
            float swingDir = dashDirection.X > 0 ? 1f : -1f;

            ShootState shootState = player.GetShootState();

            //生成冲刺弹幕
            Projectile.NewProjectile(
                player.GetSource_ItemUse(item),
                player.Center,
                dashDirection * 45f,//初始高速
                ModContent.ProjectileType<TunabeardDashProj>(),
                (int)(shootState.WeaponDamage * (3f + HalibutData.GetDomainLayer() * 0.5f)),//强力伤害倍率
                shootState.WeaponKnockback * 2f,
                player.whoAmI,
                ai0: 0,
                ai1: swingDir
            );

            //播放冲刺音效
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = -0.3f }, player.Center);
            SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.6f, Pitch = 0.2f }, player.Center);
        }
    }

    /// <summary>
    /// 金枪鱼须船长冲刺突刺弹幕
    /// </summary>
    internal class TunabeardDashProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.CapnTunabeard;

        private ref float DashTimer => ref Projectile.ai[0];
        private ref float SwingDirection => ref Projectile.ai[1];

        private Player Owner => Main.player[Projectile.owner];
        private const int DashDuration = 28;//冲刺持续时间
        private const int ImpactDelay = 8;//冲击延迟
        private float rotation = 0f;
        private float scale = 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 18;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = DashDuration;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            DashTimer++;

            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            //玩家跟随弹幕
            Owner.Center = Projectile.Center;

            //免疫帧
            if (DashTimer < DashDuration - 5) {
                Owner.GivePlayerImmuneState(8);
            }

            //冲刺阶段动态速度
            if (DashTimer <= 12) {
                //加速阶段
                Projectile.velocity *= 1.08f;
                scale = MathHelper.Lerp(1f, 1.6f, DashTimer / 12f);
            }
            else if (DashTimer > DashDuration - 8) {
                //减速阶段
                Projectile.velocity *= 0.88f;
                scale = MathHelper.Lerp(1.6f, 1f, (DashTimer - (DashDuration - 8)) / 8f);
            }

            //旋转效果
            rotation += Projectile.velocity.Length() * 0.008f * SwingDirection;

            //拖尾粒子效果
            if (Main.rand.NextBool(2)) {
                SpawnDashTrail();
            }

            //冲击波效果
            if (DashTimer == ImpactDelay) {
                CreateImpactWave();
            }

            //周期性冲击粒子
            if (DashTimer % 4 == 0) {
                SpawnImpactParticles();
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.5f, 0.7f, 1f));

            Owner.direction = Projectile.direction = Projectile.spriteDirection = Math.Sign(Projectile.velocity.X);
        }

        private void SpawnDashTrail() {
            Vector2 trailPos = Projectile.Center + Main.rand.NextVector2Circular(30f, 30f);
            Dust trail = Dust.NewDustPerfect(
                trailPos,
                DustID.Water,
                -Projectile.velocity * Main.rand.NextFloat(0.2f, 0.5f),
                100,
                Color.Lerp(Color.SkyBlue, Color.White, Main.rand.NextFloat()),
                Main.rand.NextFloat(1.5f, 2.5f)
            );
            trail.noGravity = true;
            trail.fadeIn = 1.2f;
        }

        private void CreateImpactWave() {
            //冲击波音效
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.7f, Pitch = -0.2f }, Projectile.Center);

            //生成冲击环
            for (int i = 0; i < 24; i++) {
                float angle = MathHelper.TwoPi * i / 24f;
                Vector2 shockVel = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 14f);

                Dust shock = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Water,
                    shockVel,
                    100,
                    Color.White,
                    Main.rand.NextFloat(2f, 3f)
                );
                shock.noGravity = true;
                shock.fadeIn = 1.5f;
            }

            //强力冲击特效弹幕
            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                Vector2.Zero,
                ModContent.ProjectileType<TunabeardImpactWave>(),
                0,
                0f,
                Projectile.owner
            );
        }

        private void SpawnImpactParticles() {
            //向前方发射粒子
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);

            for (int i = 0; i < 3; i++) {
                Vector2 particleVel = forward.RotatedByRandom(0.4f) * Main.rand.NextFloat(6f, 12f);

                Dust impact = Dust.NewDustPerfect(
                    Projectile.Center + forward * 40f,
                    DustID.Water,
                    particleVel,
                    100,
                    Color.Cyan,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                impact.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中强力音效
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.9f, Pitch = -0.4f }, target.Center);

            //击中爆炸特效
            for (int i = 0; i < 20; i++) {
                Vector2 burstVel = Main.rand.NextVector2Circular(10f, 10f);

                Dust burst = Dust.NewDustPerfect(
                    target.Center,
                    DustID.Water,
                    burstVel,
                    100,
                    Color.White,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                burst.noGravity = Main.rand.NextBool();
            }

            //击退增强
            target.velocity += Projectile.velocity.SafeNormalize(Vector2.Zero) * 12f;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Item[ItemID.CapnTunabeard].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;
            float drawRotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4 * Projectile.spriteDirection;

            SpriteEffects effects = SwingDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;

            //绘制拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float trailProgress = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = Color.Lerp(Color.SkyBlue, Color.White, trailProgress) * (trailProgress * 0.8f);
                trailColor.A = 0;

                Main.EntitySpriteDraw(
                    texture,
                    trailPos,
                    null,
                    trailColor,
                    drawRotation,
                    origin,
                    scale * trailProgress * 0.9f,
                    effects,
                    0
                );
            }

            //绘制主体
            Main.EntitySpriteDraw(
                texture,
                drawPos,
                null,
                Color.White,
                drawRotation,
                origin,
                scale,
                effects,
                0
            );

            //绘制发光层
            Color glowColor = new Color(150, 200, 255) * 0.6f;
            glowColor.A = 0;
            Main.EntitySpriteDraw(
                texture,
                drawPos,
                null,
                glowColor,
                drawRotation,
                origin,
                scale * 1.15f,
                effects,
                0
            );

            return false;
        }

        public override void OnKill(int timeLeft) {
            //结束爆发特效
            for (int i = 0; i < 30; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);

                Dust end = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Water,
                    velocity,
                    100,
                    Color.SkyBlue,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                end.noGravity = Main.rand.NextBool();
            }
        }
    }

    /// <summary>
    /// 冲击波特效弹幕
    /// </summary>
    internal class TunabeardImpactWave : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 310;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.alpha = 0;
        }

        public override void AI() {
            Projectile.scale += 0.1f;
            Projectile.alpha += 12;

            if (Projectile.alpha >= 255) {
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = CWRAsset.DiffusionCircle.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color drawColor = Color.Lerp(Color.Cyan, Color.White, 0.5f) * (1f - Projectile.alpha / 255f);

            for (int i = 0; i < 13; i++) {
                Main.spriteBatch.Draw(
                    texture,
                    drawPos,
                    null,
                    drawColor * 0.6f,
                    Projectile.rotation + i * MathHelper.TwoPi / 3f,
                    texture.Size() / 2f,
                    Projectile.scale * (1f + i * 0.01f),
                    SpriteEffects.None,
                    0f
                );
            }

            return false;
        }
    }
}
