using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
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
                        (int)(shootState.WeaponDamage * (1f + HalibutData.GetDomainLayer() * 0.25f)),
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
                        SpawnOilCreateEffect(position, oilVelocity);
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

        //油污离体喷洒，顺喷射方向的重力油珠扇
        private static void SpawnOilCreateEffect(Vector2 position, Vector2 sprayVel) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 dir = sprayVel.UnitVector();
            for (int i = 0; i < 9; i++) {
                Vector2 v = dir.RotatedBy(Main.rand.NextFloat(-0.55f, 0.55f))
                    * sprayVel.Length() * Main.rand.NextFloat(0.35f, 0.85f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(position, v,
                    FishLardPalette.Droplet(), Main.rand.NextFloat(0.9f, 1.6f))
                    ?.Configure(Main.rand.Next(20, 34), 0.30f, 0.988f);
            }
            //离体拉丝残余
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                    position + Main.rand.NextVector2Circular(8f, 8f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-0.5f, 0.5f)),
                    FishLardPalette.OilDeep, Main.rand.NextFloat(1.4f, 2f))
                    ?.Configure(Main.rand.Next(26, 40), 0.22f, 0.97f);
            }
        }
    }

    /// <summary>全局钩子，Halibut 攻击附加点燃</summary>
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
    /// 油污球弹幕，飞行为受重力的速度拉伸油滴，触块压扁成附着油渍
    /// 存活期缓慢下垂流淌并析出薄膜虹彩，遇火转入燃烧焦化
    /// </summary>
    internal class OilBlob : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

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
        private Vector2 stuckNormal;

        //油污物理参数
        private const float Gravity = 0.35f;
        private const float AirFriction = 0.97f;
        private const int MaxLifeTime = 600;
        private const int BurnDuration = 180;

        //视觉状态（纯客户端，不参与同步）
        private float blobScale = 1f;
        private float attachAt = -1f;   //进入附着态时的 OilLife，-1 为未附着
        private float sagAmount;        //0-0.85 流淌进度
        private float iridAmount;       //0-1 油膜虹彩成膜度
        private float burnBlend;        //0-1 燃烧焦化过渡
        private float igniteFlash;      //点燃瞬间闪光包络

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

            UpdateVisualBlend();

            //油不发光，只有燃烧时照明
            if (State == OilState.Burning) {
                float lightIntensity = 0.8f * blobScale;
                Lighting.AddLight(Projectile.Center,
                    1.0f * lightIntensity,
                    0.5f * lightIntensity,
                    0.1f * lightIntensity);
            }
        }

        //视觉混合量推进
        private void UpdateVisualBlend() {
            if (State == OilState.Stuck && attachAt >= 0f) {
                float held = OilLife - attachAt;
                sagAmount = MathHelper.Clamp(held / 260f, 0f, 0.85f);
                iridAmount = MathHelper.Clamp(held / 70f, 0f, 1f);
            }
            else if (State == OilState.Burning) {
                burnBlend = MathHelper.Clamp(BurnTimer / 12f, 0f, 1f);
                iridAmount = MathHelper.Lerp(iridAmount, 0f, 0.12f);
                igniteFlash *= 0.82f;
            }
            else {
                //飞行/滴落中膜面破碎、流淌复位
                sagAmount *= 0.9f;
                iridAmount *= 0.92f;
            }
        }

        private void FlyingPhaseAI() {
            //应用重力
            Projectile.velocity.Y += Gravity;

            //空气阻力
            Projectile.velocity *= AirFriction;

            //液滴朝向由速度决定，无自旋
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (VaultUtils.isServer) {
                return;
            }
            //粘稠脱滴
            if (OilLife % 4 == 0 && Projectile.velocity.Length() > 2.5f) {
                ShedDroplet(0.5f);
            }
        }

        private void StuckPhaseAI() {
            //保持附着位置
            Projectile.velocity *= 0.7f;

            //附着时的缓慢滴落效果
            if (OilLife % 90 == 0 && Main.rand.NextBool(3)) {
                SpawnDripThread();
            }

            //油面滋滋冒泡，成膜后偶发小气泡
            if (!VaultUtils.isServer && iridAmount > 0.4f && Main.rand.NextBool(40)) {
                SpawnSurfaceBubble();
            }

            if (OilLife > 180 && Main.rand.NextBool(120)) {
                State = OilState.Dripping;
                attachAt = -1f;
                Projectile.velocity = stuckNormal.RotatedBy(MathHelper.PiOver2) * 2f;
                Projectile.tileCollide = true;
            }

            CheckForIgnition();
        }

        private void DrippingPhaseAI() {
            //重新应用重力
            Projectile.velocity.Y += Gravity * 1.2f;
            Projectile.velocity.X *= 0.98f;

            Projectile.rotation = Projectile.velocity.ToRotation();

            //滴落拉丝
            if (!VaultUtils.isServer && OilLife % 5 == 0) {
                ShedDroplet(0.35f);
            }

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

            //尺寸逐渐缩小
            blobScale = 1f - BurnTimer / (float)BurnDuration;

            if (!VaultUtils.isServer) {
                SpawnBurnEffects();
            }

            if (BurnTimer >= BurnDuration) {
                Projectile.Kill();
            }
        }

        //燃烧期粒子预算，暗烟 1/5f
        private void SpawnBurnEffects() {
            Vector2 topFace = Projectile.Center - Vector2.UnitY * (14f * blobScale);
            if (Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_FishLardSmoke>(
                    topFace + Main.rand.NextVector2Circular(12f * blobScale, 4f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.4f, 0.9f)),
                    FishLardPalette.SmokeDark, Main.rand.NextFloat(0.14f, 0.24f))
                    ?.Configure(Main.rand.Next(44, 72));
            }
            if (Main.rand.NextBool(6)) {
                PRTLoader.NewParticle<PRT_PallbearerEmber>(
                    topFace + Main.rand.NextVector2Circular(10f * blobScale, 3f),
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(1.5f, 3.2f)),
                    FishLardPalette.HeatOrange, Main.rand.NextFloat(0.8f, 1.3f))
                    ?.Configure(Main.rand.Next(16, 26), 0.06f);
            }
            if (Main.rand.NextBool(8)) {
                SpawnSurfaceBubble();
            }
            //廉价填充底噪，少量火炬尘
            if (Main.rand.NextBool(4)) {
                Dust flame = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(16f * blobScale, 8f),
                    DustID.Torch,
                    new Vector2(0, Main.rand.NextFloat(-2.4f, -0.8f)).RotatedByRandom(0.4f),
                    0, default, Main.rand.NextFloat(1.1f, 1.7f));
                flame.noGravity = true;
            }
        }

        private void CheckForIgnition() {
            //检测附近是否有火焰弹幕或者燃烧中的NPC
            float checkRange = 80f;

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.friendly &&
                    Vector2.Distance(Projectile.Center, proj.Center) < checkRange) {
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
            igniteFlash = 1f;

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

        //尾侧甩滴
        private void ShedDroplet(float speedKeep) {
            Vector2 tail = Projectile.Center - Projectile.velocity.UnitVector() * 12f;
            Vector2 dropVel = Projectile.velocity * speedKeep
                + Main.rand.NextVector2Circular(0.7f, 0.7f);
            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(tail, dropVel,
                FishLardPalette.Droplet(), Main.rand.NextFloat(0.8f, 1.3f))
                ?.Configure(Main.rand.Next(18, 30), 0.30f, 0.985f);
            //断丝，本体与脱滴之间的过渡微滴
            for (int i = 1; i <= 2; i++) {
                float t = i / 3f;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                    Vector2.Lerp(Projectile.Center, tail, t),
                    Vector2.Lerp(Projectile.velocity * 0.9f, dropVel, t),
                    FishLardPalette.OilDeep, Main.rand.NextFloat(0.35f, 0.55f))
                    ?.Configure(Main.rand.Next(10, 18), 0.26f, 0.99f);
            }
        }

        //附着油渍底缘垂滴
        private void SpawnDripThread() {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 tangent = stuckNormal.RotatedBy(MathHelper.PiOver2);
            Vector2 dripPos = Projectile.Center
                + tangent * Main.rand.NextFloat(-12f, 12f)
                + Vector2.UnitY * 6f;
            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(dripPos,
                new Vector2(0f, Main.rand.NextFloat(0.3f, 0.8f)),
                FishLardPalette.Droplet(), Main.rand.NextFloat(1f, 1.5f))
                ?.Configure(Main.rand.Next(24, 38), 0.20f, 0.995f);
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                    dripPos - Vector2.UnitY * (3f + i * 4f),
                    new Vector2(0f, 0.15f * (2 - i)),
                    FishLardPalette.OilDeep, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(Main.rand.Next(12, 20), 0.16f, 0.995f);
            }
        }

        //油面小气泡，出生在朝上的油面带
        private void SpawnSurfaceBubble() {
            Vector2 tangent = stuckNormal == Vector2.Zero
                ? Vector2.UnitX : stuckNormal.RotatedBy(MathHelper.PiOver2);
            Vector2 pos = Projectile.Center
                + tangent * Main.rand.NextFloat(-13f, 13f) * blobScale
                + stuckNormal * Main.rand.NextFloat(2f, 6f);
            PRTLoader.NewParticle<PRT_FishLardBubble>(pos, Vector2.Zero,
                Color.White, 1f)?.Configure(Main.rand.Next(26, 44),
                State == OilState.Burning ? 0.3f : 0.14f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (State == OilState.Flying) {
                //附着到物块
                State = OilState.Stuck;
                attachAt = OilLife;

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
                SpawnStickEffect(oldVelocity);

                return false;
            }

            if (State == OilState.Dripping) {
                //滴落到地面后也附着
                State = OilState.Stuck;
                attachAt = OilLife;
                stuckNormal = -Vector2.UnitY;
                Projectile.velocity *= 0.99f;
                Projectile.tileCollide = false;

                SpawnStickEffect(oldVelocity * 0.5f);
                return false;
            }

            return false;
        }

        //拍上表面，沿切向甩出压溅油珠
        private void SpawnStickEffect(Vector2 impactVel) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 tangent = stuckNormal.RotatedBy(MathHelper.PiOver2);
            float force = MathHelper.Clamp(impactVel.Length() * 0.4f, 1.2f, 4.5f);
            for (int i = 0; i < 8; i++) {
                float side = Main.rand.NextBool() ? 1f : -1f;
                Vector2 v = tangent * side * force * Main.rand.NextFloat(0.4f, 1f)
                    + stuckNormal * force * Main.rand.NextFloat(0.2f, 0.7f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                    Projectile.Center + tangent * Main.rand.NextFloat(-8f, 8f),
                    v, FishLardPalette.Droplet(), Main.rand.NextFloat(0.7f, 1.3f))
                    ?.Configure(Main.rand.Next(16, 28), 0.30f, 0.985f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 180);

            //击中油污飞溅，顺行进方向偏折的油珠扇
            if (!VaultUtils.isServer) {
                Vector2 dir = Projectile.velocity.UnitVector();
                for (int i = 0; i < 8; i++) {
                    Vector2 v = dir.RotatedBy(Main.rand.NextFloat(-1.1f, 1.1f))
                        * Main.rand.NextFloat(2f, 5.5f);
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center, v,
                        FishLardPalette.Droplet(), Main.rand.NextFloat(0.8f, 1.4f))
                        ?.Configure(Main.rand.Next(18, 30), 0.30f, 0.985f);
                }
            }

            SoundEngine.PlaySound(SoundID.NPCHit13 with {
                Volume = 0.4f,
                Pitch = 0.2f
            }, Projectile.Center);
        }

        //点燃爆发
        private void SpawnIgniteEffect() {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_FishLardSmoke>(
                    Projectile.Center + Main.rand.NextVector2Circular(10f, 6f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.6f, 1.4f)),
                    FishLardPalette.SmokeDark, Main.rand.NextFloat(0.16f, 0.28f))
                    ?.Configure(Main.rand.Next(50, 80));
            }
            for (int i = 0; i < 12; i++) {
                float ang = MathHelper.TwoPi * i / 12f + Main.rand.NextFloat(0.3f);
                PRTLoader.NewParticle<PRT_PallbearerEmber>(Projectile.Center,
                    ang.ToRotationVector2() * Main.rand.NextFloat(1.5f, 4.5f) - Vector2.UnitY * 1.5f,
                    FishLardPalette.HeatOrange, Main.rand.NextFloat(0.9f, 1.5f))
                    ?.Configure(Main.rand.Next(18, 30), 0.07f);
            }
            //燃滴迸出，着火的油珠本身也是亮部
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center,
                    Main.rand.NextVector2Circular(3.5f, 2.5f) - Vector2.UnitY * 2f,
                    Color.Lerp(FishLardPalette.HeatOrange, FishLardPalette.OilBrown, Main.rand.NextFloat(0.5f)),
                    Main.rand.NextFloat(0.8f, 1.3f))
                    ?.Configure(Main.rand.Next(16, 26), 0.28f, 0.985f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                //死亡油污飞溅，大而慢的重力油珠
                int count = State == OilState.Burning ? 8 : 12;
                for (int i = 0; i < count; i++) {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center,
                        Main.rand.NextVector2Circular(4.5f, 3.5f) - Vector2.UnitY * Main.rand.NextFloat(1.5f),
                        FishLardPalette.Droplet(), Main.rand.NextFloat(1f, 1.9f))
                        ?.Configure(Main.rand.Next(22, 38), 0.28f, 0.985f);
                }

                if (State == OilState.Burning) {
                    //燃尽余波
                    for (int i = 0; i < 5; i++) {
                        PRTLoader.NewParticle<PRT_FishLardSmoke>(
                            Projectile.Center + Main.rand.NextVector2Circular(10f, 6f),
                            new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.5f, 1.2f)),
                            FishLardPalette.SmokeDark, Main.rand.NextFloat(0.18f, 0.3f))
                            ?.Configure(Main.rand.Next(60, 90));
                    }
                    for (int i = 0; i < 4; i++) {
                        PRTLoader.NewParticle<PRT_PallbearerEmber>(Projectile.Center,
                            Main.rand.NextVector2Circular(2.5f, 2f) - Vector2.UnitY * 2f,
                            FishLardPalette.HeatOrange, Main.rand.NextFloat(0.7f, 1.1f))
                            ?.Configure(Main.rand.Next(20, 32), 0.07f);
                    }
                }
            }

            SoundEngine.PlaySound(SoundID.NPCDeath1 with {
                Volume = 0.5f,
                Pitch = -0.3f
            }, Projectile.Center);
        }

        //形体坐标系，x=主轴（飞行为速度向，附着为表面切向），y=其法向
        private void GetBodyAxes(out Vector2 ax, out Vector2 ay) {
            ax = State switch {
                OilState.Flying or OilState.Dripping => Projectile.velocity.UnitVector(),
                _ => stuckNormal == Vector2.Zero ? Vector2.UnitX : stuckNormal.RotatedBy(MathHelper.PiOver2),
            };
            ay = ax.RotatedBy(MathHelper.PiOver2);
        }

        //燃烧火舌帧，油面之下的根部层，PreDraw 时机先于图元油体 = 夹心下层
        public override bool PreDraw(ref Color lightColor) {
            if (State != OilState.Burning || blobScale <= 0.05f) {
                return false;
            }
            Texture2D fire = CWRAsset.Fire?.Value;
            if (fire == null) {
                return false;
            }
            int fw = fire.Width / 4;
            int fh = fire.Height / 4;
            GetBodyAxes(out Vector2 ax, out _);

            for (int i = 0; i < 3; i++) {
                int frameIdx = ((int)OilLife / 3 + i * 5) % 16;
                Rectangle src = new(frameIdx % 4 * fw, frameIdx / 4 * fh, fw, fh);
                float off = (i - 1) * 13f * blobScale;
                Vector2 basePos = Projectile.Center + ax * off - Vector2.UnitY * (6f * blobScale);
                float sway = MathF.Sin(OilLife * 0.13f + i * 2.1f) * 0.14f;
                float s = (0.42f + 0.16f * MathF.Sin(OilLife * 0.21f + i)) * blobScale * burnBlend;
                //根部锚在油面下缘，origin 取帧底中
                Main.EntitySpriteDraw(fire, basePos - Main.screenPosition, src,
                    new Color(255, 118, 30, 0) * (0.62f * burnBlend), sway,
                    new Vector2(fw * 0.5f, fh), s, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(fire, basePos - Main.screenPosition, src,
                    new Color(255, 190, 84, 0) * (0.30f * burnBlend), -sway * 0.6f,
                    new Vector2(fw * 0.5f, fh), s * 0.62f, SpriteEffects.None, 0);
            }
            return false;
        }

        //油体本身
        void IPrimitiveDrawable.DrawPrimitives() {
            Effect effect = FishLardAssets.FishLardBlob;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null || Main.dedServ) {
                return;
            }

            GetBodyAxes(out Vector2 ax, out Vector2 ay);

            //出生 6 帧从小滴涨到全尺寸，禁 pop-in
            float grow = MathHelper.Clamp(OilLife / 6f, 0.35f, 1f);
            float speed = Projectile.velocity.Length();
            float baseHalf = 30f * Projectile.scale * MathHelper.Lerp(0.55f, 1f, blobScale) * grow;

            float halfX;
            float halfY;
            float tear;
            if (State == OilState.Flying || State == OilState.Dripping) {
                //飞行，顺速度拉长、横向收窄
                float stretch = MathHelper.Clamp(speed * 0.055f, 0f, 0.8f);
                halfX = baseHalf * (1f + stretch);
                halfY = baseHalf * (1f - stretch * 0.38f);
                tear = MathHelper.Clamp(speed * 0.075f, 0.1f, 1f);
            }
            else {
                //附着，8 帧内压扁摊开
                float settle = attachAt >= 0f
                    ? MathHelper.Clamp((OilLife - attachAt) / 8f, 0f, 1f) : 1f;
                settle = 1f - MathF.Pow(1f - settle, 3f);
                halfX = baseHalf * MathHelper.Lerp(1f, 1.5f + sagAmount * 0.25f, settle);
                halfY = baseHalf * MathHelper.Lerp(1f, 0.62f, settle);
                tear = 0f;
            }

            Vector2 c = Projectile.Center;
            Vector2 ex = ax * halfX;
            Vector2 ey = ay * halfY;
            var quad = new VertexPositionColorTexture[4];
            quad[0] = new VertexPositionColorTexture((c - ex - ey).ToVector3(), Color.White, new Vector2(0f, 0f));
            quad[1] = new VertexPositionColorTexture((c + ex - ey).ToVector3(), Color.White, new Vector2(1f, 0f));
            quad[2] = new VertexPositionColorTexture((c - ex + ey).ToVector3(), Color.White, new Vector2(0f, 1f));
            quad[3] = new VertexPositionColorTexture((c + ex + ey).ToVector3(), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.313f % 1f);
            effect.Parameters["uTear"]?.SetValue(tear);
            effect.Parameters["uSag"]?.SetValue(sagAmount);
            effect.Parameters["uBurn"]?.SetValue(burnBlend);
            effect.Parameters["uIrid"]?.SetValue(iridAmount);
            effect.Parameters["uFade"]?.SetValue(1f);
            //世界向下在(ax,ay)形体系中的分量，sag 与反光带定向用
            effect.Parameters["uDown"]?.SetValue(new Vector2(ax.Y, ay.Y));
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, quad, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        //火舌热尖，图元油体之上的加色上层
        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (State != OilState.Burning || Main.dedServ) {
                return;
            }
            //点燃瞬间琥珀闪环
            if (igniteFlash > 0.1f && CWRAsset.Ring01?.Value is Texture2D ring) {
                float fs = (1.4f - igniteFlash) * 80f / ring.Width;
                spriteBatch.Draw(ring, Projectile.Center - Main.screenPosition, null,
                    FishLardPalette.OilAmber with { A = 0 } * (igniteFlash * 0.7f),
                    0f, ring.Size() * 0.5f, fs, SpriteEffects.None, 0f);
            }

            Texture2D fire = CWRAsset.Fire?.Value;
            if (fire == null || blobScale <= 0.05f) {
                return;
            }
            int fw = fire.Width / 4;
            int fh = fire.Height / 4;
            GetBodyAxes(out Vector2 ax, out _);
            //只画一条小热舌探出油面，主体火留在油下层
            int frameIdx = ((int)OilLife / 2 + 7) % 16;
            Rectangle src = new(frameIdx % 4 * fw, frameIdx / 4 * fh, fw, fh);
            Vector2 pos = Projectile.Center + ax * MathF.Sin(OilLife * 0.07f) * 8f * blobScale
                - Vector2.UnitY * (10f * blobScale);
            float s = (0.30f + 0.08f * MathF.Sin(OilLife * 0.17f)) * blobScale * burnBlend;
            spriteBatch.Draw(fire, pos - Main.screenPosition, src,
                new Color(255, 150, 46, 0) * (0.5f * burnBlend), MathF.Sin(OilLife * 0.11f) * 0.1f,
                new Vector2(fw * 0.5f, fh), s, SpriteEffects.None, 0f);
        }
    }
}
