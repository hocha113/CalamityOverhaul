using CalamityOverhaul.Content.UIs;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>
    /// 斑驳油鱼技能，生成可附着在物块上的大团油污
    /// </summary>
    internal class FishVariegatedLard : FishSkill
    {
        public override int UnlockFishID => ItemID.VariegatedLardfish;
        public override int DefaultCooldown => 45 - HalibutData.GetDomainLayer() * 3;
        public override int ResearchDuration => 60 * 20;
        //活跃的油污追踪
        private static readonly List<int> ActiveOils = new();
        private static int MaxOils => 6 + HalibutData.GetDomainLayer();

        public void Use(Player player, Projectile projectile) {
            ShootState shootState = player.GetShootState();
            Vector2 velocity = player.velocity;
            Vector2 position = projectile.Center;

            //周期性生成油污
            if (Cooldown <= 0) {
                SetCooldown();

                CleanupInactiveOils();

                if (ActiveOils.Count < MaxOils) {
                    //生成油污球
                    Vector2 shootDir = Main.MouseWorld.To(position).UnitVector();
                    Vector2 oilVelocity = shootDir * Main.rand.NextFloat(6f, 12f);
                    oilVelocity += Main.rand.NextVector2Circular(2f, 2f);

                    int oilProj = Projectile.NewProjectile(
                        shootState.Source,
                        position,
                        oilVelocity,
                        ModContent.ProjectileType<OilBlob>(),
                        (int)(shootState.WeaponDamage * (1.4f + HalibutData.GetDomainLayer() * 0.35f)),
                        shootState.WeaponKnockback * 1.2f,
                        player.whoAmI
                    );

                    if (oilProj >= 0) {
                        ActiveOils.Add(oilProj);

                        //油污生成音效
                        SoundEngine.PlaySound(SoundID.Item95 with {
                            Volume = 0.4f,
                            Pitch = -0.4f
                        }, position);

                        SoundEngine.PlaySound(SoundID.NPCHit13 with {
                            Volume = 0.3f,
                            Pitch = -0.3f
                        }, position);

                        //生成效果
                        SpawnOilCreateEffect(position);
                    }
                }
            }
        }

        private static void CleanupInactiveOils() {
            ActiveOils.RemoveAll(id => {
                if (id < 0 || id >= Main.maxProjectiles) return true;
                Projectile proj = Main.projectile[id];
                return !proj.active || proj.type != ModContent.ProjectileType<OilBlob>();
            });
        }

        //油污生成效果
        private static void SpawnOilCreateEffect(Vector2 position) {
            //暗色油污粒子
            for (int i = 0; i < 10; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(3f, 3f);
                Dust oil = Dust.NewDustPerfect(
                    position,
                    DustID.Smoke,
                    velocity,
                    100,
                    new Color(40, 40, 45),
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                oil.noGravity = true;
                oil.fadeIn = 1.3f;
            }
        }
    }

    /// <summary>
    /// 全局弹幕钩子
    /// </summary>
    internal class FishVariegatedLardGlobalProj : GlobalProjectile
    {
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) {
            if (projectile.owner.TryGetPlayer(out var player)
                && FishSkill.GetT<FishVariegatedLard>().Active(player)) {
                //在这个技能下攻击会附加点燃效果
                target.AddBuff(BuffID.OnFire, 120 + HalibutData.GetDomainLayer() * 15);
                FishSkill.GetT<FishVariegatedLard>().Use(player, projectile);
            }
        }
    }

    /// <summary>
    /// 油污球弹幕
    /// </summary>
    internal class OilBlob : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        //油污状态
        private enum OilState
        {
            Flying,     //飞行状态
            Stuck,      //附着状态
            Dripping,   //滴落状态
            Burning     //燃烧状态
        }

        private OilState State {
            get => (OilState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float OilLife => ref Projectile.ai[1];
        private ref float BurnTimer => ref Projectile.ai[2];

        //附着相关
        private Vector2 stuckPosition;
        private float stuckRotation;
        private Vector2 stuckNormal;

        //油污物理参数
        private const float Gravity = 0.35f;
        private const float AirFriction = 0.97f;
        private const int MaxLifeTime = 600;
        private const int BurnDuration = 180;

        //视觉效果
        private float blobScale = 1f;
        private float pulsePhase = 0f;
        private readonly List<OilParticle> oilParticles = new();
        private const int MaxOilParticles = 30;

        public override void SetDefaults() {
            Projectile.width = 42;
            Projectile.height = 42;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLifeTime;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.alpha = 0;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.65f;
            }
        }

        public override void AI() {
            OilLife++;
            pulsePhase += 0.05f;

            //状态机
            switch (State) {
                case OilState.Flying:
                    FlyingPhaseAI();
                    break;
                case OilState.Stuck:
                    StuckPhaseAI();
                    break;
                case OilState.Dripping:
                    DrippingPhaseAI();
                    break;
                case OilState.Burning:
                    BurningPhaseAI();
                    break;
            }

            //更新油污粒子
            UpdateOilParticles();

            //油污光照
            float lightIntensity = 0.3f * (1f - Projectile.alpha / 255f);
            if (State == OilState.Burning) {
                lightIntensity = 0.8f;
                Lighting.AddLight(Projectile.Center,
                    1.0f * lightIntensity,
                    0.5f * lightIntensity,
                    0.1f * lightIntensity);
            }
            else {
                Lighting.AddLight(Projectile.Center,
                    0.2f * lightIntensity,
                    0.2f * lightIntensity,
                    0.25f * lightIntensity);
            }
        }

        private void FlyingPhaseAI() {
            //应用重力
            Projectile.velocity.Y += Gravity;

            //空气阻力
            Projectile.velocity *= AirFriction;

            //旋转
            Projectile.rotation += Projectile.velocity.X * 0.05f;

            //生成油污粒子
            if (OilLife % 4 == 0 && oilParticles.Count < MaxOilParticles) {
                SpawnOilParticle();
            }

            //飞行轨迹粒子
            if (Main.rand.NextBool(6)) {
                SpawnFlyingTrail();
            }
        }

        private void StuckPhaseAI() {
            //保持附着位置
            Projectile.velocity *= 0.7f;

            //附着时的缓慢滴落效果
            if (OilLife % 90 == 0 && Main.rand.NextBool(3)) {
                SpawnDripParticle();
            }

            //检查是否应该开始滴落
            if (OilLife > 180 && Main.rand.NextBool(120)) {
                State = OilState.Dripping;
                Projectile.velocity = stuckNormal.RotatedBy(MathHelper.PiOver2) * 2f;
                Projectile.tileCollide = true;
            }

            //检查附近是否有火焰，如果有则点燃
            CheckForIgnition();
        }

        private void DrippingPhaseAI() {
            //重新应用重力
            Projectile.velocity.Y += Gravity * 1.2f;
            Projectile.velocity.X *= 0.98f;

            Projectile.rotation += Projectile.velocity.Y * 0.03f;

            //滴落粒子
            if (Main.rand.NextBool(5)) {
                SpawnDripParticle();
            }

            //检查附近是否有火焰
            CheckForIgnition();
        }

        private void BurningPhaseAI() {
            BurnTimer++;

            //燃烧时固定不动
            Projectile.velocity *= 0.9f;
            Projectile.tileCollide = false;

            //燃烧时持续造成伤害
            if (BurnTimer % 15 == 0) {
                DamageNearbyEnemies();
            }

            //燃烧粒子效果
            if (Main.rand.NextBool(2)) {
                SpawnBurnParticle();
            }

            //燃烧火焰效果
            SpawnFlameEffect();

            //尺寸逐渐缩小
            blobScale = 1f - BurnTimer / (float)BurnDuration;

            if (BurnTimer >= BurnDuration) {
                Projectile.Kill();
            }
        }

        private void CheckForIgnition() {
            //检测附近是否有火焰弹幕或者燃烧中的NPC
            float checkRange = 80f;

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.friendly &&
                    Vector2.Distance(Projectile.Center, proj.Center) < checkRange) {
                    //检查是否是火焰类弹幕
                    if (proj.type == ProjectileID.Flames ||
                        proj.type == ProjectileID.FlamesTrap ||
                        proj.type == ProjectileID.Fireball ||
                        proj.type == ProjectileID.Meteor1 ||
                        proj.type == ProjectileID.Meteor2 ||
                        proj.type == ProjectileID.Meteor3) {
                        Ignite();
                        return;
                    }
                }
            }

            //检查附近NPC是否着火
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && Vector2.Distance(Projectile.Center, npc.Center) < checkRange) {
                    if (npc.HasBuff(BuffID.OnFire) ||
                        npc.HasBuff(BuffID.OnFire3) ||
                        npc.HasBuff(BuffID.CursedInferno)) {
                        Ignite();
                        return;
                    }
                }
            }
        }

        private void Ignite() {
            if (State == OilState.Burning) return;

            State = OilState.Burning;
            BurnTimer = 0;
            Projectile.timeLeft = BurnDuration;

            //点燃效果
            SpawnIgniteEffect();

            //点燃音效
            SoundEngine.PlaySound(SoundID.Item74 with {
                Volume = 0.6f,
                Pitch = -0.5f
            }, Projectile.Center);
        }

        private void DamageNearbyEnemies() {
            float damageRange = 120f + HalibutData.GetDomainLayer() * 12f;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy() && !npc.friendly) {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < damageRange) {
                        //距离越近伤害越高
                        float damageRatio = 1f - dist / damageRange;
                        int burnDamage = (int)(Projectile.damage * (0.3f + damageRatio * 0.4f));

                        npc.SimpleStrikeNPC(burnDamage, 0, false, 0f, null, false, 0f, true);
                        npc.AddBuff(BuffID.OnFire3, 180);
                    }
                }
            }
        }

        private void UpdateOilParticles() {
            for (int i = oilParticles.Count - 1; i >= 0; i--) {
                OilParticle p = oilParticles[i];
                p.Life++;

                //粒子行为根据状态改变
                if (State == OilState.Flying || State == OilState.Dripping) {
                    Vector2 toCenter = Projectile.Center - p.Position;
                    float dist = toCenter.Length();
                    if (dist > 5f) {
                        p.Velocity += toCenter.SafeNormalize(Vector2.Zero) * 0.06f;
                    }
                }

                p.Position += p.Velocity;
                p.Velocity *= 0.96f;

                //透明度衰减
                float lifeRatio = p.Life / (float)p.MaxLife;
                p.Opacity = (1f - lifeRatio) * 0.8f;

                //移除消逝的粒子
                if (p.Life >= p.MaxLife || p.Opacity <= 0.05f) {
                    oilParticles.RemoveAt(i);
                    continue;
                }

                oilParticles[i] = p;
            }
        }

        private void SpawnOilParticle() {
            Vector2 particlePos = Projectile.Center + Main.rand.NextVector2Circular(22f, 22f);
            Vector2 particleVel = Main.rand.NextVector2Circular(0.4f, 0.4f);

            OilParticle particle = new OilParticle {
                Position = particlePos,
                Velocity = particleVel,
                Size = Main.rand.NextFloat(0.7f, 1.3f),
                Life = 0,
                MaxLife = Main.rand.Next(35, 70),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                Opacity = 0.8f
            };

            oilParticles.Add(particle);
        }

        private void SpawnFlyingTrail() {
            Dust trail = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                DustID.Smoke,
                -Projectile.velocity * 0.25f,
                100,
                new Color(40, 40, 45),
                Main.rand.NextFloat(1.2f, 1.8f)
            );
            trail.noGravity = true;
            trail.fadeIn = 1.2f;
            trail.alpha = 100;
        }

        private void SpawnDripParticle() {
            Vector2 dripPos = Projectile.Bottom + new Vector2(
                Main.rand.NextFloat(-Projectile.width / 3f, Projectile.width / 3f),
                0
            );

            Dust drip = Dust.NewDustPerfect(
                dripPos,
                DustID.Smoke,
                new Vector2(0, Main.rand.NextFloat(1f, 3f)),
                100,
                new Color(35, 35, 40),
                Main.rand.NextFloat(0.8f, 1.4f)
            );
            drip.noGravity = false;
            drip.fadeIn = 1f;
        }

        private void SpawnBurnParticle() {
            for (int i = 0; i < 2; i++) {
                Vector2 velocity = new Vector2(0, Main.rand.NextFloat(-4f, -1f));
                velocity = velocity.RotatedByRandom(0.5f);

                Dust burn = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.Smoke,
                    velocity,
                    100,
                    new Color(60, 60, 65),
                    Main.rand.NextFloat(2f, 3f)
                );
                burn.noGravity = true;
                burn.fadeIn = 1.4f;
            }
        }

        private void SpawnFlameEffect() {
            //火焰效果
            if (Main.rand.NextBool(2)) {
                Dust flame = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(25f, 25f),
                    DustID.Torch,
                    new Vector2(0, Main.rand.NextFloat(-3f, -1f)).RotatedByRandom(0.6f),
                    0,
                    Color.OrangeRed,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                flame.noGravity = true;
            }

            //火星飞溅
            if (Main.rand.NextBool(5)) {
                Dust spark = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Torch,
                    Main.rand.NextVector2Circular(4f, 4f) + new Vector2(0, -2f),
                    0,
                    Color.Yellow,
                    Main.rand.NextFloat(1f, 1.5f)
                );
                spark.noGravity = true;
            }
        }

        private void SpawnIgniteEffect() {
            //点燃爆发
            for (int i = 0; i < 25; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                Dust ignite = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Torch,
                    velocity,
                    0,
                    Color.OrangeRed,
                    Main.rand.NextFloat(1.8f, 3f)
                );
                ignite.noGravity = true;
            }

            //烟雾环
            for (int i = 0; i < 15; i++) {
                float angle = MathHelper.TwoPi * i / 15f;
                Vector2 velocity = angle.ToRotationVector2() * 4f;

                Dust smoke = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Smoke,
                    velocity,
                    100,
                    new Color(50, 50, 55),
                    Main.rand.NextFloat(2f, 3f)
                );
                smoke.noGravity = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (State == OilState.Flying) {
                //附着到物块
                State = OilState.Stuck;
                stuckPosition = Projectile.Center;
                stuckRotation = Projectile.rotation;

                //计算法线
                if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                    stuckNormal = new Vector2(-Math.Sign(oldVelocity.X), 0);
                }
                else if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                    stuckNormal = new Vector2(0, -Math.Sign(oldVelocity.Y));
                }

                Projectile.tileCollide = false;
                Projectile.timeLeft = MaxLifeTime;

                //附着音效
                SoundEngine.PlaySound(SoundID.NPCHit13 with {
                    Volume = 0.5f,
                    Pitch = -0.4f
                }, Projectile.Center);

                //附着特效
                SpawnStickEffect();

                return false;
            }

            if (State == OilState.Dripping) {
                //滴落到地面后也附着
                State = OilState.Stuck;
                stuckPosition = Projectile.Center;
                stuckRotation = Projectile.rotation;
                stuckNormal = -Vector2.UnitY;
                Projectile.velocity *= 0.99f;
                Projectile.tileCollide = false;

                SpawnStickEffect();
                return false;
            }

            return false;
        }

        private void SpawnStickEffect() {
            for (int i = 0; i < 12; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(4f, 4f);
                Dust stick = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Smoke,
                    velocity,
                    100,
                    new Color(40, 40, 45),
                    Main.rand.NextFloat(1.5f, 2.2f)
                );
                stick.noGravity = true;
                stick.fadeIn = 1.3f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 180);

            //击中油污飞溅
            for (int i = 0; i < 10; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                Dust hit_dust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Smoke,
                    velocity,
                    100,
                    new Color(40, 40, 45),
                    Main.rand.NextFloat(1.3f, 2f)
                );
                hit_dust.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.NPCHit13 with {
                Volume = 0.4f,
                Pitch = 0.2f
            }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            //死亡油污飞溅
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(7f, 7f);
                Dust kill_dust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Smoke,
                    velocity,
                    100,
                    new Color(40, 40, 45),
                    Main.rand.NextFloat(1.8f, 3f)
                );
                kill_dust.noGravity = Main.rand.NextBool();
            }

            if (State == OilState.Burning) {
                //燃烧状态死亡额外火焰效果
                for (int i = 0; i < 15; i++) {
                    Dust flame = Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.Torch,
                        Main.rand.NextVector2Circular(8f, 8f),
                        0,
                        Color.OrangeRed,
                        Main.rand.NextFloat(2f, 3f)
                    );
                    flame.noGravity = true;
                }
            }

            SoundEngine.PlaySound(SoundID.NPCDeath1 with {
                Volume = 0.5f,
                Pitch = -0.3f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;

            //绘制油污粒子
            DrawOilParticles(sb);

            //绘制主体油污球
            DrawOilBlob(sb, lightColor);

            return false;
        }

        private void DrawOilParticles(SpriteBatch sb) {
            Texture2D particleTex = CWRAsset.StarTexture_White.Value;

            foreach (var particle in oilParticles) {
                Vector2 drawPos = particle.Position - Main.screenPosition;
                Color particleColor = new Color(40, 40, 50) * particle.Opacity * 0.7f;

                if (State == OilState.Burning) {
                    particleColor = Color.Lerp(
                        new Color(40, 40, 50),
                        new Color(255, 100, 50),
                        BurnTimer / (float)BurnDuration
                    ) * particle.Opacity;
                }

                sb.Draw(
                    particleTex,
                    drawPos,
                    null,
                    particleColor with { A = 0 },
                    particle.Rotation,
                    particleTex.Size() / 2f,
                    particle.Size * 0.04f,
                    SpriteEffects.None,
                    0
                );
            }
        }

        private void DrawOilBlob(SpriteBatch sb, Color lightColor) {
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Texture2D glowTex = DyeMachineAsset.SoftGlow;

            float currentScale = blobScale * Projectile.scale;
            float pulse = (float)Math.Sin(pulsePhase) * 0.08f + 0.92f;
            currentScale *= pulse;

            //燃烧状态特殊效果
            if (State == OilState.Burning) {
                //火焰发光层
                for (int i = 0; i < 3; i++) {
                    float flameScale = currentScale * (1.3f + i * 0.15f);
                    float flameAlpha = (1f - i * 0.3f) * 0.6f;

                    sb.Draw(
                        glowTex,
                        drawPos,
                        null,
                        new Color(255, 120, 50, 0) * flameAlpha,
                        Projectile.rotation,
                        glowTex.Size() / 2f,
                        flameScale,
                        SpriteEffects.None,
                        0
                    );
                }

                //火焰核心
                sb.Draw(
                    glowTex,
                    drawPos,
                    null,
                    new Color(255, 200, 100, 0) * 0.8f,
                    Projectile.rotation,
                    glowTex.Size() / 2f,
                    currentScale * 0.8f,
                    SpriteEffects.None,
                    0
                );
            }

            //外层暗影
            Color outerColor = State == OilState.Burning
                ? new Color(80, 60, 50, 0)
                : new Color(30, 30, 35, 0);

            sb.Draw(
                glowTex,
                drawPos,
                null,
                outerColor * 0.6f,
                Projectile.rotation,
                glowTex.Size() / 2f,
                currentScale * 1.25f,
                SpriteEffects.None,
                0
            );

            //主体油污
            Color mainColor = State == OilState.Burning
                ? Color.Lerp(new Color(40, 40, 45), new Color(150, 80, 40), BurnTimer / (float)BurnDuration)
                : new Color(40, 40, 45);

            sb.Draw(
                glowTex,
                drawPos,
                null,
                mainColor with { A = 0 } * 0.9f,
                Projectile.rotation,
                glowTex.Size() / 2f,
                currentScale,
                SpriteEffects.None,
                0
            );

            //高光层
            float highlightIntensity = State == OilState.Burning ? 0.5f : 0.3f;
            Vector2 highlightOffset = new Vector2(-5, -5);

            sb.Draw(
                glowTex,
                drawPos + highlightOffset,
                null,
                new Color(100, 100, 110, 0) * highlightIntensity,
                Projectile.rotation,
                glowTex.Size() / 2f,
                currentScale * 0.5f,
                SpriteEffects.None,
                0
            );
        }
    }

    /// <summary>
    /// 油污粒子数据结构
    /// </summary>
    internal struct OilParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Size;
        public int Life;
        public int MaxLife;
        public float Rotation;
        public float Opacity;
    }
}
