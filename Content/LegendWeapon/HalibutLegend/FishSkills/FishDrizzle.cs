using CalamityMod.Items.Fishing.BrimstoneCragCatches;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishDrizzle : FishSkill
    {
        [VaultLoaden(CWRConstant.Masking)]
        public static Texture2D Fire = null;//火焰的纹理灰度图，总共4*4帧，也就是四行四列的帧图
        public override int UnlockFishID => ModContent.ItemType<DragoonDrizzlefish>();
        public override int DefaultCooldown => 480 - HalibutData.GetDomainLayer() * 24;
        public override int ResearchDuration => 60 * 16;

        private static int _drizzleVolleyIdSeed = 0;
        internal static int DepartureDelay => 90 - (HalibutData.GetDomainLayer() * 3);
        internal static int DepartureDuration => 90 - (HalibutData.GetDomainLayer() * 3);
        internal static int shootDir;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            var hp = player.GetOverride<HalibutPlayer>();
            if (Cooldown <= 0 && !HasActiveDrizzle(player)) {
                TriggerDrizzleVolley(item, player, hp, source, damage, knockback, velocity);
                SetCooldown();
            }
            return null;
        }

        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            return !HasActiveDrizzle(player);
        }

        private static bool HasActiveDrizzle(Player player) {
            return player.CountProjectilesOfID<DrizzleFishHolder>() > 0;
        }

        private static void TriggerDrizzleVolley(Item item, Player player, HalibutPlayer hp, EntitySource_ItemUse_WithAmmo source
            , int damage, float knockback, Vector2 aimVelocity) {
            shootDir = player.direction;

            int fishCount = 3 + HalibutData.GetDomainLayer() / 2;
            int volleyId = _drizzleVolleyIdSeed++;

            Vector2 aimDir = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            Vector2 behind = (-aimDir).SafeNormalize(Vector2.UnitX);
            float arc = MathHelper.ToRadians(140f);
            float radius = 110f;
            ShootState shootState = player.GetShootState();

            //中心火焰爆发特效
            Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, Vector2.Zero
                , ModContent.ProjectileType<DrizzleSpawnEffect>(), 0, 0f, player.whoAmI, -1, volleyId);

            for (int i = 0; i < fishCount; i++) {
                float t = fishCount == 1 ? 0.5f : i / (float)(fishCount - 1);
                float angOff = (t - 0.5f) * arc;
                Vector2 offsetDir = behind.RotatedBy(angOff);
                Vector2 spawnPos = player.Center + offsetDir * radius + new Vector2(0, (float)Math.Sin(Main.GameUpdateCount * 0.05f + i) * 8f);

                int proj = Projectile.NewProjectile(player.GetSource_ItemUse(item), spawnPos, Vector2.Zero,
                    ModContent.ProjectileType<DrizzleFishHolder>(), shootState.WeaponDamage, shootState.WeaponKnockback, player.whoAmI,
                    ai0: volleyId, ai1: i);

                if (Main.projectile[proj].ModProjectile is DrizzleFishHolder holder) {
                    holder.Owner = player;
                    holder.StoredDamage = damage;
                    holder.StoredKnockback = knockback;
                    holder.StoredSource = source;
                    holder.AimVelocity = aimVelocity;
                }

                //鱼体出现火焰特效
                Projectile.NewProjectile(player.GetSource_ItemUse(item), spawnPos, Vector2.Zero
                    , ModContent.ProjectileType<DrizzleSpawnEffect>(), 0, 0f, player.whoAmI, Main.projectile[proj].identity, volleyId);
            }

            SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.3f, Volume = 0.7f }, player.Center);
        }
    }

    /// <summary>
    /// 硫火鱼出现特效
    /// </summary>
    internal class DrizzleSpawnEffect : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";

        private ref float Index => ref Projectile.ai[0];
        private const int LifeTime = 38;
        private float seed;
        private float startScale;
        private float endScale;
        private Color colA;
        private Color colB;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 10;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifeTime;
            Projectile.alpha = 0;
        }

        public override void OnSpawn(IEntitySource source) {
            seed = Main.rand.NextFloat(10000f);
            if (Index < 0) {
                startScale = 0.5f;
                endScale = 4.8f;
            }
            else {
                startScale = 0.3f;
                endScale = 2.2f + Main.rand.NextFloat(0.5f);
            }

            float hue = Index < 0 ? 0.15f : Index % 7 / 7f;
            colA = Color.Lerp(new Color(255, 100, 50), new Color(255, 200, 80), 0.35f + 0.4f * hue);
            colB = Color.Lerp(new Color(200, 60, 20), new Color(255, 140, 40), 0.55f * (1 - hue) + 0.2f);

            int dustAmt = Index < 0 ? 42 : 16;
            for (int i = 0; i < dustAmt; i++) {
                float rot = MathHelper.TwoPi * i / dustAmt;
                Vector2 dVel = rot.ToRotationVector2() * (Index < 0 ? 7f : 4.2f) * Main.rand.NextFloat(0.5f, 1.2f);
                var d = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool() ? DustID.Torch : DustID.Flare, dVel, 150,
                    Color.Lerp(colA, colB, Main.rand.NextFloat()), Main.rand.NextFloat(1.2f, 1.8f));
                d.noGravity = true;
            }
        }

        public override void AI() {
            float t = 1f - Projectile.timeLeft / (float)LifeTime;
            float ease = MathF.Pow(t, 0.6f);
            Projectile.scale = MathHelper.Lerp(startScale, endScale, ease);

            Projectile.rotation += 0.05f + (Index < 0 ? 0.03f : 0.07f) * MathF.Sin(seed + Main.GlobalTimeWrappedHourly * 6f);

            if (Index < 0 && Main.rand.NextBool(3)) {
                Vector2 ringPos = Projectile.Center + Main.rand.NextVector2CircularEdge(Projectile.scale * 20f, Projectile.scale * 20f);
                var d = Dust.NewDustPerfect(ringPos, DustID.Torch, Vector2.Zero, 160, Color.OrangeRed, Main.rand.NextFloat(0.6f, 1.1f));
                d.noGravity = true;
            }

            if (Index >= 0 && t < 0.45f && Main.rand.NextBool(4)) {
                Vector2 dir = Main.rand.NextVector2Unit();
                var d2 = Dust.NewDustPerfect(Projectile.Center + dir * Projectile.scale * 14f, DustID.Flare, dir * 2.5f, 120,
                    Color.Lerp(colA, colB, Main.rand.NextFloat()), Main.rand.NextFloat(0.7f, 1.3f));
                d2.noGravity = true;
            }

            if (t > 0.75f) {
                Projectile.alpha = (int)MathHelper.Lerp(0, 255, (t - 0.75f) / 0.25f);
            }

            if (Index.TryGetProjectile(out var fish)) {
                Projectile.Center = fish.Center + fish.rotation.ToRotationVector2() * 36;
                if (fish.ai[2] == 0 && Projectile.timeLeft < LifeTime / 2) {
                    Projectile.timeLeft = LifeTime / 2;
                }
            }
            else if (Projectile.owner.TryGetPlayer(out var owner)) {
                Projectile.Center = owner.Center;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float fade = 1f - Projectile.alpha / 255f;

            Color outer = Color.Lerp(colA, colB, 0.5f) * 0.65f * fade;
            outer.A = 0;
            Color inner = Color.Lerp(Color.OrangeRed, Color.Yellow, 0.3f) * 0.95f * fade;
            inner.A = 0;

            float scaleOuter = Projectile.scale * (Index < 0 ? 1.5f : 1.2f);
            Main.spriteBatch.Draw(tex, drawPos, null, outer, Projectile.rotation, origin, scaleOuter, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, drawPos, null, inner, -Projectile.rotation * 0.6f, origin, Projectile.scale * 0.7f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 硫火鱼承载弹幕
    /// </summary>
    internal class DrizzleFishHolder : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Item + "Fishing/BrimstoneCragCatches/DragoonDrizzlefish";
        public Player Owner;
        internal bool Fired {
            get => Projectile.ai[2] == 1f;
            set => Projectile.ai[2] = value ? 1f : 0f;
        }

        public int StoredDamage;
        public float StoredKnockback;
        public EntitySource_ItemUse_WithAmmo StoredSource;
        public Vector2 AimVelocity;

        private const int PreFireDelay = 18;
        private ref float FishIndex => ref Projectile.ai[1];

        private float glowPulse;
        private float fadeOut;
        private bool shouldDepart = false;
        private int departureTimer = 0;
        private const int FireInterval = 16;

        public override void SetDefaults() {
            Projectile.width = 40; Projectile.height = 40;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 600;
            Projectile.friendly = false;
            Projectile.hostile = false;
        }

        public override void AI() {
            if (Owner == null || !Owner.active) { Projectile.Kill(); return; }

            glowPulse = (float)Math.Sin(Main.GameUpdateCount * 0.28f + FishIndex) * 0.5f + 0.5f;

            if (!shouldDepart) {
                //环绕阶段
                Vector2 aimDir = (Main.MouseWorld - Owner.Center).SafeNormalize(Vector2.UnitX);
                Vector2 behind = (-aimDir).SafeNormalize(Vector2.UnitX);
                float arc = MathHelper.ToRadians(140f);
                float radius = 210f;

                int fishCount = Owner.CountProjectilesOfID<DrizzleFishHolder>();
                float t = fishCount <= 1 ? 0.5f : FishIndex / (fishCount - 1);
                float angOff = (t - 0.5f) * arc;
                Vector2 offsetDir = behind.RotatedBy(angOff * FishDrizzle.shootDir * -1);
                Vector2 basePos = Owner.Center + offsetDir * radius;
                float bob = (float)Math.Sin(Main.GameUpdateCount * 0.09f + FishIndex) * 8f;
                Projectile.Center = Vector2.Lerp(Projectile.Center, basePos + new Vector2(0, bob), 0.28f);
                Projectile.rotation = Projectile.To(Main.MouseWorld).ToRotation();

                //按序发射火焰柱
                if (Projectile.timeLeft < 600 - PreFireDelay - (int)FishIndex * FireInterval && !Fired) {
                    FirePillar();
                    Fired = true;
                    Projectile.netUpdate = true;
                }

                //检查是否所有鱼都已发射且所有火柱都已消失
                if (Fired) {
                    bool allFired = true;
                    bool anyPillarActive = false;

                    for (int i = 0; i < Main.maxProjectiles; i++) {
                        Projectile p = Main.projectile[i];
                        if (p.active && p.owner == Owner.whoAmI) {
                            if (p.type == Projectile.type && p.ModProjectile is DrizzleFishHolder h) {
                                if (!h.Fired) {
                                    allFired = false;
                                }
                            }
                            else if (p.type == ModContent.ProjectileType<DrizzleFirePillar>()) {
                                anyPillarActive = true;
                            }
                        }
                    }

                    //只有当所有鱼都发射完毕且所有火柱都消失后才开始离场
                    if (allFired && !anyPillarActive) {
                        shouldDepart = true;
                        departureTimer = 0;
                    }
                }
            }
            else {
                //离场阶段
                departureTimer++;

                //先等待一段时间
                if (departureTimer < FishDrizzle.DepartureDelay) {
                    //原地轻微浮动
                    Projectile.rotation += 0.02f * (FishIndex % 2 == 0 ? 1 : -1);
                    float idleBob = (float)Math.Sin(Main.GameUpdateCount * 0.1f + FishIndex) * 4f;
                    Projectile.Center += new Vector2(0, idleBob * 0.05f);
                }
                else {
                    //开始真正的离场动画
                    int flyTime = departureTimer - FishDrizzle.DepartureDelay;
                    float progress = Math.Clamp(flyTime / (float)FishDrizzle.DepartureDuration, 0f, 1f);
                    progress = MathF.Pow(progress, 0.65f);

                    //计算离开方向（远离玩家中心）
                    Vector2 outward = Projectile.Center - Owner.Center;
                    if (outward.LengthSquared() < 4f) {
                        outward = new Vector2(FishDrizzle.shootDir * 100, -100);
                    }
                    outward = outward.SafeNormalize(Vector2.UnitY);

                    //速度逐渐加快
                    float baseSpeed = MathHelper.Lerp(3f, 18f, progress);
                    baseSpeed *= 1f + 0.15f * (float)Math.Sin(flyTime * 0.18f + FishIndex);

                    Projectile.velocity = outward * baseSpeed;
                    Projectile.Center += Projectile.velocity;

                    //淡出效果
                    fadeOut = Math.Clamp((progress - 0.5f) / 0.5f, 0f, 1f);

                    //离开屏幕范围检测
                    Rectangle safeBounds = new((int)Main.screenPosition.X - 200, (int)Main.screenPosition.Y - 200,
                        Main.screenWidth + 400, Main.screenHeight + 400);

                    if (!safeBounds.Contains(Projectile.Center.ToPoint()) || fadeOut >= 0.99f) {
                        Projectile.Kill();
                        return;
                    }
                }
            }

            Projectile.spriteDirection = Projectile.rotation.ToRotationVector2().X > 0 ? 1 : -1;
        }

        private void FirePillar() {
            SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.2f, Volume = 0.9f }, Projectile.Center);

            Vector2 dir = (Main.MouseWorld - Projectile.Center).SafeNormalize(Vector2.UnitX);
            int damage = (int)(StoredDamage * (0.6f + HalibutData.GetDomainLayer() * 0.15f));

            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + dir * 12f, dir * 0.1f,
                ModContent.ProjectileType<DrizzleFirePillar>(), damage, StoredKnockback * 1.6f, Projectile.owner, Projectile.identity);

            for (int i = 0; i < 16; i++) {
                Vector2 v = dir.RotatedByRandom(0.4f) * Main.rand.NextFloat(5f, 11f);
                var d = Dust.NewDustPerfect(Projectile.Center + dir * 18f, DustID.Torch, v, 150, default, Main.rand.NextFloat(1.2f, 1.8f));
                d.noGravity = true;
                d.color = Color.Lerp(Color.OrangeRed, Color.Yellow, Main.rand.NextFloat());
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = value.Frame();
            Vector2 origin = sourceRect.Size() / 2f;
            float drawRotation = Projectile.rotation + MathHelper.PiOver4;
            float pulseScale = 1f + glowPulse * 0.18f;
            float opacity = 1f - fadeOut;

            Color baseCol = Color.Lerp(Color.OrangeRed, Color.Yellow, 0.3f + 0.4f * glowPulse);
            baseCol *= opacity;

            //绘制外层光晕
            Main.spriteBatch.Draw(value, drawPosition, sourceRect, baseCol * 0.7f, drawRotation, origin, pulseScale * 1.3f, SpriteEffects.None, 0f);
            //绘制主体
            Main.spriteBatch.Draw(value, drawPosition, sourceRect, Color.White * opacity, drawRotation, origin, pulseScale, SpriteEffects.None, 0f);

            return false;
        }
    }

    /// <summary>
    /// 硫火柱弹幕
    /// </summary>
    internal class DrizzleFirePillar : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private readonly List<FireParticle> fireParticles = new();
        private const int MaxParticles = 180;

        private readonly Vector2[] topEdge = new Vector2[80];
        private readonly Vector2[] botEdge = new Vector2[80];
        private Vector2 topEnd, botEnd;

        private Color gradientStart = new(255, 120, 40);
        private Color gradientMid = new(255, 180, 80);
        private Color gradientEnd = new(255, 220, 120);

        private float pillarWidth = 0f;
        private float targetWidth = 140f;

        //火焰核心效果参数
        private float coreIntensity = 0f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 10;
            Projectile.timeLeft = 85;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.5f;
            }
            if (target.type == CWRID.NPC_DevourerofGodsHead || target.type == CWRID.NPC_DevourerofGodsTail) {
                modifiers.FinalDamage *= 2f;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , Projectile.Center, Projectile.Center + Projectile.rotation.ToRotationVector2() * 2800, pillarWidth * 2f, ref p);
        }

        public override void AI() {
            if (Projectile.ai[0].TryGetProjectile(out var projectile)) {
                Projectile.Center = projectile.Center;
                Projectile.rotation = projectile.rotation;
            }

            //火柱宽度动画
            if (Projectile.timeLeft > 60) {
                pillarWidth = MathHelper.Lerp(pillarWidth, targetWidth, 0.15f);
                coreIntensity = MathHelper.Lerp(coreIntensity, 1f, 0.12f);
            }
            else {
                pillarWidth *= 0.92f;
                coreIntensity *= 0.88f;
            }

            float progress = 1f - Projectile.timeLeft / 85f;
            //优化颜色渐变，让火焰更有层次感
            gradientStart = Color.Lerp(new Color(255, 90, 20), new Color(255, 130, 50), progress);
            gradientMid = Color.Lerp(new Color(255, 150, 60), new Color(255, 190, 90), progress);
            gradientEnd = Color.Lerp(new Color(255, 180, 80), new Color(255, 230, 120), progress);

            //计算火柱边缘
            for (int i = 0; i < 80; i++) {
                float x = i * 18f;
                float wave = 10f * (float)Math.Sin(Projectile.localAI[0] * 0.08f + i * 0.3f);
                float y = pillarWidth * 0.5f + wave;
                topEdge[i] = new Vector2(x, y);
                botEdge[i] = new Vector2(x, -y);
            }

            float endX = 80 * 18f;
            float endWave = 10f * (float)Math.Sin(Projectile.localAI[0] * 0.08f + 80 * 0.3f);
            topEnd = new Vector2(endX, pillarWidth * 0.5f + endWave);
            botEnd = new Vector2(endX, -(pillarWidth * 0.5f + endWave));

            Projectile.localAI[0] += 1f;

            //生成火焰粒子 - 优化生成频率，靠近根部的区域生成更多粒子
            if (fireParticles.Count < MaxParticles) {
                SpawnFireParticle();
                //额外在根部生成粒子
                if (Main.rand.NextBool(2)) {
                    SpawnFireParticle(true);
                }
            }

            UpdateFireParticles();

            //增强火焰光照，添加脉冲效果
            float lightPulse = (float)Math.Sin(Projectile.localAI[0] * 0.3f) * 0.2f + 1f;
            Lighting.AddLight(Projectile.Center, 1.4f * lightPulse, 0.9f * lightPulse, 0.4f * lightPulse);
        }

        private void SpawnFireParticle(bool nearRoot = false) {
            float dist;
            if (nearRoot) {
                //在根部区域生成更密集的粒子
                dist = Main.rand.NextFloat(20f, 350f);
            }
            else {
                dist = Main.rand.NextFloat(50f, 1400f);
            }

            float offsetY = Main.rand.NextFloat(-pillarWidth * 0.4f, pillarWidth * 0.4f);
            Vector2 pos = Projectile.Center + Projectile.rotation.ToRotationVector2() * dist;
            pos += Projectile.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * offsetY;

            Vector2 vel = Projectile.rotation.ToRotationVector2() * Main.rand.NextFloat(1.5f, 4f);
            vel += Projectile.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-2f, 2f);

            fireParticles.Add(new FireParticle {
                Position = pos,
                Velocity = vel,
                Life = 0,
                MaxLife = Main.rand.Next(35, 60),
                Scale = Main.rand.NextFloat(nearRoot ? 2f : 1.5f, nearRoot ? 4.5f : 3.5f),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                RotSpeed = Main.rand.NextFloat(-0.15f, 0.15f),
                Frame = Main.rand.Next(0, 16),
                Opacity = nearRoot ? 1f : 0.85f
            });
        }

        private void UpdateFireParticles() {
            for (int i = fireParticles.Count - 1; i >= 0; i--) {
                FireParticle p = fireParticles[i];
                p.Life++;
                p.Position += p.Velocity;
                p.Velocity *= 0.97f;
                p.Rotation += p.RotSpeed;
                p.Frame = (p.Frame + 0.3f) % 16;

                float lifeRatio = p.Life / (float)p.MaxLife;
                p.Opacity = 1f - lifeRatio;
                p.Scale *= 0.985f;

                if (p.Life >= p.MaxLife || p.Opacity <= 0.05f) {
                    fireParticles.RemoveAt(i);
                    continue;
                }

                fireParticles[i] = p;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 240 + HalibutData.GetDomainLayer() * 30);

            for (int i = 0; i < 8; i++) {
                Vector2 pos = target.Center + Main.rand.NextVector2Circular(target.width * 0.4f, target.height * 0.4f);
                var d = Dust.NewDustPerfect(pos, DustID.Torch, Main.rand.NextVector2Circular(4f, 4f), 150, default, Main.rand.NextFloat(1.2f, 2f));
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (FishDrizzle.Fire == null) return false;

            //绘制火焰粒子
            DrawFireParticles();

            for (int i = 0; i < 3; i++) {
                //绘制火柱核心
                DrawPillarCore();

                //绘制火柱主体
                DrawPillarMainBody();
            }


            return false;
        }

        private void DrawFireParticles() {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D fireTex = FishDrizzle.Fire;
            foreach (var p in fireParticles) {
                Vector2 drawPos = p.Position - Main.screenPosition;
                int frameX = (int)p.Frame % 4;
                int frameY = (int)p.Frame / 4;
                Rectangle frame = new Rectangle(frameX * (fireTex.Width / 4), frameY * (fireTex.Height / 4), fireTex.Width / 4, fireTex.Height / 4);

                //增强粒子颜色
                Color color = Color.Lerp(gradientStart, gradientEnd, Main.rand.NextFloat()) * p.Opacity;
                color.A = 0;

                sb.Draw(fireTex, drawPos, frame, color, p.Rotation, frame.Size() * 0.5f, p.Scale * 0.28f, SpriteEffects.None, 0f);
            }
        }

        //绘制火柱核心层，提供最强的发光效果
        private void DrawPillarCore() {
            List<ColoredVertex> vertices = new();

            //核心层缩小到主体的60%
            float coreWidthMultiplier = 0.6f;

            for (int i = 0; i < 80; i++) {
                float u = i / 80f;

                //使用更亮的核心颜色
                Color coreColor = Color.Lerp(
                    new Color(255, 240, 200),  //接近白色的亮黄
                    new Color(255, 200, 120),  //金黄色
                    u
                );

                //根部（u接近0）更亮，远端逐渐变暗
                float intensityByDistance = MathHelper.Lerp(1.2f, 0.6f, u);
                coreColor *= coreIntensity * intensityByDistance;

                Vector2 topCore = topEdge[i] * coreWidthMultiplier;
                Vector2 botCore = botEdge[i] * coreWidthMultiplier;

                vertices.Add(new ColoredVertex(topCore.RotatedBy(Projectile.rotation) + Projectile.Center - Main.screenPosition, coreColor, new Vector3(u, 0, 1)));
                vertices.Add(new ColoredVertex(botCore.RotatedBy(Projectile.rotation) + Projectile.Center - Main.screenPosition, coreColor, new Vector3(u, 1, 1)));
            }

            Vector2 topCoreEnd = topEnd * coreWidthMultiplier;
            Vector2 botCoreEnd = botEnd * coreWidthMultiplier;
            Color endCoreColor = new Color(255, 200, 120) * coreIntensity * 0.6f;

            vertices.Add(new ColoredVertex(topCoreEnd.RotatedBy(Projectile.rotation) + Projectile.Center - Main.screenPosition, endCoreColor, new Vector3(1, 0, 1)));
            vertices.Add(new ColoredVertex(botCoreEnd.RotatedBy(Projectile.rotation) + Projectile.Center - Main.screenPosition, endCoreColor, new Vector3(1, 1, 1)));

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[0] = CWRAsset.LightShot.Value;
            if (vertices.Count >= 3) {
                Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices.ToArray(), 0, vertices.Count - 2);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        //改进：主体绘制，优化透明度渐变
        private void DrawPillarMainBody() {
            List<ColoredVertex> vertices = new();

            for (int i = 0; i < 80; i++) {
                float u = i / 80f;

                //改进颜色插值，使用三重渐变
                Color colA = Color.Lerp(
                    Color.Lerp(gradientStart, gradientMid, u),
                    Color.Lerp(gradientMid, gradientEnd, u),
                    u
                );
                Color colB = Color.Lerp(gradientStart, gradientEnd, u);

                //关键改进：根据距离调整透明度，根部（u小）更不透明
                float alphaMultiplier = MathHelper.Lerp(1.0f, 0.7f, u);  //从100%到70%
                colA *= alphaMultiplier;
                colB *= alphaMultiplier;

                vertices.Add(new ColoredVertex(topEdge[i].RotatedBy(Projectile.rotation) + Projectile.Center - Main.screenPosition, colA, new Vector3(u, 0, 1 - u)));
                vertices.Add(new ColoredVertex(botEdge[i].RotatedBy(Projectile.rotation) + Projectile.Center - Main.screenPosition, colB, new Vector3(u, 1, 1 - u)));
            }

            //末端颜色保持一定透明度
            Color endColor = gradientEnd * 0.7f;
            vertices.Add(new ColoredVertex(topEnd.RotatedBy(Projectile.rotation) + Projectile.Center - Main.screenPosition, endColor, new Vector3(1, 0, 1)));
            vertices.Add(new ColoredVertex(botEnd.RotatedBy(Projectile.rotation) + Projectile.Center - Main.screenPosition, endColor, new Vector3(1, 1, 1)));

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[0] = CWRAsset.LightShot.Value;
            if (vertices.Count >= 3) {
                Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices.ToArray(), 0, vertices.Count - 2);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }

    internal struct FireParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public int Life;
        public int MaxLife;
        public float Scale;
        public float Rotation;
        public float RotSpeed;
        public float Frame;
        public float Opacity;
    }
}
