using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>蜂蜜鱼技能，右键生成蜂群核心</summary>
    internal class FishHoney : FishSkill
    {
        public override int UnlockFishID => ItemID.Honeyfin;
        public override int DefaultCooldown => 60 * (15 - HalibutData.GetDomainLayer());
        public override int ResearchDuration => 60 * 16;
        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override bool? CanUseItem(Item item, Player player) {
            if (player.altFunctionUse != 2) {
                return null;
            }

            if (Cooldown > 0) {
                return false;
            }

            SummonBeeSwarm(player, item);
            SetCooldown();
            return false;
        }

        private static void SummonBeeSwarm(Player player, Item item) {
            if (Main.myPlayer != player.whoAmI) return;

            var source = player.GetSource_FromThis();

            //生成蜂巢核心
            int swarmCore = Projectile.NewProjectile(
                source,
                player.Center,
                Vector2.Zero,
                ModContent.ProjectileType<HoneyBeeSwarmCore>(),
                (int)(player.GetShootState().WeaponDamage * (0.3f + HalibutData.GetDomainLayer() * 0.07f)),
                2f,
                player.whoAmI
            );

            if (swarmCore >= 0) {
                //召唤音效，蜂鸣 + 粘液闷响
                SoundEngine.PlaySound(SoundID.Item97 with {
                    Volume = 0.85f,
                    Pitch = -0.2f
                }, player.Center);
                FishHoneyVFX.GlugSound(player.Center, -0.55f, 0.6f);

                //召唤特效
                SpawnSummonEffect(player.Center);
            }
        }

        private static void SpawnSummonEffect(Vector2 position) {
            Vector2 blobPos = position + new Vector2(0f, -54f);
            //慢蜜滴环形迸出
            FishHoneyVFX.DropletBurst(blobPos, Vector2.Zero, 14, 3.2f);
            //深琥珀微冲击环
            PRTLoader.NewParticle<PRT_DWave>(blobPos, Vector2.Zero, FishHoneyVFX.HoneyDeep, 0.06f)
                ?.Configure(Vector2.One, 0f, 0.28f, 11);
            //金尘底噪填充
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.8f, 2.2f);
                Dust dust = Dust.NewDustPerfect(blobPos, DustID.Honey, vel, 150
                    , FishHoneyVFX.HoneyGold, Main.rand.NextFloat(0.8f, 1.3f));
                dust.noGravity = true;
                dust.fadeIn = 1.1f;
            }
        }
    }

    #region 蜂群核心控制器
    /// <summary>蜂群核心，周期生成并指挥蜜蜂；可视为悬浮于头顶的粘稠蜜团（蜜诏）</summary>
    internal class HoneyBeeSwarmCore : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private readonly List<int> activeBees = new();
        private const int MaxBees = 12;
        private const int SwarmLifetime = 600; //10秒
        private const int SpawnInterval = 8;
        private int spawnTimer = 0;
        private int beesSpawned = 0;

        private const float orbitRadius = 80f;
        /// <summary>蜜团溶解窗口帧数</summary>
        private const int DissolveWindow = 26;

        private int dripTimer;
        private int nextDripInterval = 50;
        private float spawnBeat;

        /// <summary>蜜团视觉中心</summary>
        internal Vector2 BlobCenter => Projectile.Center
            + new Vector2(0f, -54f + MathF.Sin(Main.GlobalTimeWrappedHourly * 1.35f + Projectile.identity * 0.7f) * 5f);

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = SwarmLifetime;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];

            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            //跟随玩家
            Projectile.Center = Vector2.Lerp(Projectile.Center, owner.Center, 0.15f);

            //周期性生成蜜蜂
            if (beesSpawned < MaxBees) {
                spawnTimer++;
                if (spawnTimer >= SpawnInterval) {
                    spawnTimer = 0;
                    SpawnBee(owner);
                }
            }

            //清理失效蜜蜂
            CleanupInactiveBees();

            //产蜂挤压脉冲衰减
            spawnBeat *= 0.86f;

            //蜜滴垂落
            if (!Main.dedServ && ++dripTimer >= nextDripInterval) {
                dripTimer = 0;
                nextDripInterval = Main.rand.Next(42, 66);
                Vector2 dripPos = BlobCenter + new Vector2(Main.rand.NextFloat(-7f, 7f), 10f);
                PRTLoader.NewParticle<PRT_FishHoneyDrop>(dripPos, new Vector2(0f, 0.25f), FishHoneyVFX.HoneyAmber
                    , Main.rand.NextFloat(0.6f, 0.9f))?.Configure(Main.rand.Next(80, 120), 0.13f, true);
            }

            //稀疏金尘底噪
            if (!Main.dedServ && Main.rand.NextBool(22)) {
                Dust d = Dust.NewDustPerfect(BlobCenter + Main.rand.NextVector2Circular(18f, 14f), DustID.Honey
                    , new Vector2(0f, Main.rand.NextFloat(-0.25f, 0.1f)), 160, FishHoneyVFX.HoneyGold, Main.rand.NextFloat(0.6f, 0.9f));
                d.noGravity = true;
            }

            //蜜非光源
            Lighting.AddLight(BlobCenter, 0.26f, 0.17f, 0.04f);
        }

        private void SpawnBee(Player owner) {
            if (Main.myPlayer != owner.whoAmI) return;

            //自蜜团中心生出，朝环绕位飞去（禁 pop-in）
            float angle = MathHelper.TwoPi * beesSpawned / MaxBees;
            Vector2 spawnPos = BlobCenter;
            Vector2 initialVel = angle.ToRotationVector2() * 3.2f;

            int bee = Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                spawnPos,
                initialVel,
                ModContent.ProjectileType<HoneyBeeMinion>(),
                Projectile.damage,
                2f,
                owner.whoAmI,
                Projectile.whoAmI, //传递核心ID
                beesSpawned //传递蜜蜂索引
            );

            if (bee >= 0) {
                activeBees.Add(bee);
                beesSpawned++;

                //产蜂拍
                spawnBeat = 1f;
                FishHoneyVFX.DropletBurst(spawnPos, initialVel.SafeNormalize(Vector2.UnitY), 2, 1.5f, 0.5f, false);

                //音效
                SoundEngine.PlaySound(SoundID.Item97 with {
                    Volume = 0.3f,
                    Pitch = 0.2f + beesSpawned * 0.05f
                }, spawnPos);
            }
        }

        private void CleanupInactiveBees() {
            activeBees.RemoveAll(id => {
                if (id < 0 || id >= Main.maxProjectiles) return true;
                Projectile proj = Main.projectile[id];
                return !proj.active || proj.type != ModContent.ProjectileType<HoneyBeeMinion>();
            });
        }

        public override void OnKill(int timeLeft) {
            //召回所有蜜蜂
            foreach (int beeID in activeBees) {
                if (beeID >= 0 && beeID < Main.maxProjectiles) {
                    Projectile bee = Main.projectile[beeID];
                    if (bee.active && bee.ModProjectile is HoneyBeeMinion minion) {
                        minion.BeginReturn();
                    }
                }
            }

            //蜜团失稳
            FishHoneyVFX.DropletBurst(BlobCenter, Vector2.Zero, 8, 3f);
            PRTLoader.NewParticle<PRT_DWave>(BlobCenter, Vector2.Zero, FishHoneyVFX.HoneyDeep, 0.08f)
                ?.Configure(Vector2.One, 0f, 0.3f, 12);
            FishHoneyVFX.GlugSound(BlobCenter, -0.35f, 0.55f);
            SoundEngine.PlaySound(SoundID.Item97 with { Volume = 0.4f, Pitch = -0.4f }, BlobCenter);
        }

        public override bool PreDraw(ref Color lightColor) {
            float age = SwarmLifetime - Projectile.timeLeft;
            float reveal = MathHelper.Clamp(age / 14f, 0f, 1f);
            float dissolve = Projectile.timeLeft < DissolveWindow ? 1f - Projectile.timeLeft / (float)DissolveWindow : 0f;
            DrawHoneyBlob(reveal, dissolve);
            return false;
        }

        private void DrawHoneyBlob(float reveal, float dissolve) {
            Effect fx = FishHoneyAssets.FishHoneyCore;
            Vector2 center = BlobCenter;

            if (fx == null || CWRAsset.PerlinNoise?.Value == null) {
                //shader 未就绪降级
                Texture2D soft = CWRAsset.SoftGlow?.Value;
                if (soft != null) {
                    float a = reveal * (1f - dissolve);
                    Vector2 dp = center - Main.screenPosition;
                    Main.EntitySpriteDraw(soft, dp, null, FishHoneyVFX.HoneyDeep * (0.8f * a), 0f
                        , soft.Size() / 2f, 0.62f, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(soft, dp, null, FishHoneyVFX.HoneyAmber * (0.65f * a), 0f
                        , soft.Size() / 2f, 0.45f, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(soft, dp + new Vector2(-5f, -6f), null
                        , FishHoneyVFX.HoneyGlint with { A = 0 } * (0.5f * a), 0f, soft.Size() / 2f, 0.1f, SpriteEffects.None, 0);
                }
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            const float half = 33f;

            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            DepthStencilState prevDepth = device.DepthStencilState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Projectile.identity % 97 * 0.211f);
            fx.Parameters["uReveal"]?.SetValue(reveal);
            fx.Parameters["uDissolve"]?.SetValue(dissolve);
            fx.Parameters["uWobble"]?.SetValue(0.1f * (1f - reveal) + 0.12f * dissolve + 0.05f * spawnBeat);
            fx.Parameters["uSquash"]?.SetValue(spawnBeat);
            fx.Parameters["uSizePx"]?.SetValue(new Vector2(half * 2f, half * 2f));
            fx.Parameters["uNoiseTex"]?.SetValue(CWRAsset.PerlinNoise.Value);
            fx.Parameters["uColDeep"]?.SetValue(FishHoneyVFX.HoneyDeep.ToVector3());
            fx.Parameters["uColBody"]?.SetValue(FishHoneyVFX.HoneyAmber.ToVector3());
            fx.Parameters["uColGold"]?.SetValue(FishHoneyVFX.HoneyGold.ToVector3());
            fx.Parameters["uColGlint"]?.SetValue(FishHoneyVFX.HoneyGlint.ToVector3());

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture(new Vector3(center.X - half, center.Y - half, 0f), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture(new Vector3(center.X + half, center.Y - half, 0f), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture(new Vector3(center.X - half, center.Y + half, 0f), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture(new Vector3(center.X + half, center.Y + half, 0f), Color.White, new Vector2(1f, 1f));

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }
    }
    #endregion

    #region 蜜蜂仆从
    /// <summary>蜂蜜鱼召唤的蜜蜂仆从</summary>
    internal class HoneyBeeMinion : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bee;

        private enum BeeState
        {
            Orbiting,   //环绕核心待命
            Seeking,    //搜索敌人
            Attacking,  //攻击敌人
            Returning   //返回核心
        }

        private ref float CoreID => ref Projectile.ai[0];
        private ref float BeeIndex => ref Projectile.ai[1];
        private ref float StateRaw => ref Projectile.localAI[0];
        private ref float StateTimer => ref Projectile.localAI[1];

        private BeeState State {
            get => (BeeState)StateRaw;
            set => StateRaw = (float)value;
        }

        private int targetNPCID = -1;
        private int stingTimer = 0;
        private Vector2 latchOffset = Vector2.Zero;

        private const float MaxSpeed = 14f;
        private const float Acceleration = 0.6f;
        private const float OrbitRadius = 80f;
        private const float AttackRange = 800f;
        private const float LatchDistance = 20f;
        private const int MaxAttackTime = 120;
        private const int StingInterval = 18;
        /// <summary>出击预告帧窗（后拉蓄势）</summary>
        private const int WindupFrames = 8;

        private int spawnAge;
        private bool facingRight;
        private float dashBoost = 1f;
        //蜜丝
        private bool strandActive;
        private bool strandLiveAnchor;
        private Vector2 strandAnchorPos;
        private Vector2 lastStrandAnchor;
        private int strandAge;
        private float strandSnapLen = 64f;
        private float stubTimer;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 4;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            ProjectileID.Sets.CultistIsResistantTo[Projectile.type] = true;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override bool? CanDamage() => true; //使用自定义伤害系统

        public override void AI() {
            Player owner = Main.player[Projectile.owner];

            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            //检查核心是否存在
            if (!IsCoreLive()) {
                State = BeeState.Returning;
            }

            StateTimer++;

            //出生化形
            if (spawnAge == 0) {
                strandActive = true;
                strandLiveAnchor = true;
                strandAge = 0;
                strandSnapLen = 64f;
                lastStrandAnchor = Projectile.Center;
            }
            spawnAge++;
            Projectile.scale = spawnAge < 10 ? MathHelper.Lerp(0.45f, 1f, 1f - MathF.Pow(1f - spawnAge / 10f, 3f)) : 1f;

            //动画帧，快扇翅
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 3) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Projectile.type];
            }

            //状态机
            switch (State) {
                case BeeState.Orbiting:
                    OrbitingBehavior();
                    break;

                case BeeState.Seeking:
                    SeekingBehavior();
                    break;

                case BeeState.Attacking:
                    AttackingBehavior();
                    break;

                case BeeState.Returning:
                    ReturningBehavior();
                    break;
            }

            //分离力避免重叠
            ApplySeparation();

            UpdateStrand();
            UpdateFacingAndRotation();
        }

        private void UpdateStrand() {
            if (strandActive) {
                strandAge++;
                Vector2 anchor = ResolveStrandAnchor();
                lastStrandAnchor = anchor;
                float dist = Vector2.Distance(anchor, Projectile.Center);
                if (dist > strandSnapLen || strandAge > (strandLiveAnchor ? 16 : 12)) {
                    SnapStrand(anchor, dist);
                }
            }
            if (stubTimer > 0.05f) {
                stubTimer *= 0.76f;
            }
            else {
                stubTimer = 0f;
            }
        }

        private Vector2 ResolveStrandAnchor() {
            if (!strandLiveAnchor) {
                return strandAnchorPos;
            }
            Projectile core = GetCoreProjectile();
            if (core != null && core.ModProjectile is HoneyBeeSwarmCore swarmCore) {
                return swarmCore.BlobCenter;
            }
            return lastStrandAnchor;
        }

        /// <summary>断丝</summary>
        private void SnapStrand(Vector2 anchor, float dist) {
            strandActive = false;
            stubTimer = 1f;
            if (Main.dedServ) {
                return;
            }
            Vector2 neck = (anchor + Projectile.Center) * 0.5f + new Vector2(0f, dist * 0.06f);
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_FishHoneyDrop>(neck, Main.rand.NextVector2Circular(0.8f, 0.5f)
                    , FishHoneyVFX.HoneyGold, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(30, 50), 0.15f, false);
            }
        }

        private void UpdateFacingAndRotation() {
            //滞回防抖
            if (Projectile.velocity.X > 0.8f) {
                facingRight = true;
            }
            else if (Projectile.velocity.X < -0.8f) {
                facingRight = false;
            }
            if (State == BeeState.Attacking && IsTargetValid()) {
                facingRight = Main.npc[targetNPCID].Center.X > Projectile.Center.X;
            }

            float spd = Projectile.velocity.Length();
            if (State == BeeState.Seeking && StateTimer > WindupFrames && spd > 7f) {
                //突进，机身对齐速度方向
                Projectile.rotation = facingRight
                    ? Projectile.velocity.ToRotation()
                    : Projectile.velocity.ToRotation() + MathHelper.Pi;
            }
            else {
                //悬停，轻微俯仰
                Projectile.rotation = MathHelper.Clamp(Projectile.velocity.Y * 0.05f, -0.35f, 0.35f) * (facingRight ? 1f : -1f);
            }
        }

        private void OrbitingBehavior() {
            Projectile coreProj = GetCoreProjectile();
            if (coreProj == null) {
                BeginReturn();
                return;
            }

            //环绕核心运动
            float angle = Main.GlobalTimeWrappedHourly * 2f + BeeIndex * MathHelper.TwoPi / 12f;
            float radius = OrbitRadius + MathF.Sin(Main.GlobalTimeWrappedHourly * 0.9f + BeeIndex * 2.1f) * 10f;
            Vector2 targetPos = coreProj.Center + angle.ToRotationVector2() * radius;

            //添加轻微的上下波动
            targetPos.Y += (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + BeeIndex) * 10f;

            MoveTowards(targetPos, 0.8f);

            //搜索敌人
            if (StateTimer > 30) {
                NPC target = Projectile.Center.FindClosestNPC(AttackRange, false, true);
                if (target != null) {
                    targetNPCID = target.whoAmI;
                    State = BeeState.Seeking;
                    StateTimer = 0;

                    SoundEngine.PlaySound(SoundID.Item97 with {
                        Volume = 0.25f,
                        Pitch = 0.4f
                    }, Projectile.Center);
                }
            }
        }

        private void SeekingBehavior() {
            if (!IsTargetValid()) {
                State = BeeState.Orbiting;
                StateTimer = 0;
                return;
            }

            NPC target = Main.npc[targetNPCID];

            //攻击拍
            if (StateTimer <= WindupFrames) {
                Vector2 away = (Projectile.Center - target.Center).SafeNormalize(Vector2.UnitY);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, away * 3.2f, 0.25f);
                if ((int)StateTimer == WindupFrames) {
                    Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    Projectile.velocity = dir * MaxSpeed * 1.35f;
                    dashBoost = 1.35f;
                    //出击拖蜜丝，锚定出发点
                    strandActive = true;
                    strandLiveAnchor = false;
                    strandAnchorPos = Projectile.Center;
                    lastStrandAnchor = Projectile.Center;
                    strandAge = 0;
                    strandSnapLen = 90f;
                }
                return;
            }

            dashBoost = MathHelper.Lerp(dashBoost, 1f, 0.08f);

            //追踪目标
            MoveTowards(target.Center, 1.2f * dashBoost);

            //急飞甩蜜，偶发小滴向后甩落
            if (!Main.dedServ && Projectile.velocity.Length() > 9f && Main.rand.NextBool(6)) {
                PRTLoader.NewParticle<PRT_FishHoneyDrop>(Projectile.Center, -Projectile.velocity * 0.12f
                    , FishHoneyVFX.HoneyAmber, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(26, 40), 0.15f, true);
            }

            //检查是否接近目标
            if (Vector2.DistanceSquared(Projectile.Center, target.Center) < LatchDistance * LatchDistance) {
                BeginAttack();
            }

            //超时返回
            if (StateTimer > 180) {
                State = BeeState.Orbiting;
                StateTimer = 0;
            }
        }

        private void AttackingBehavior() {
            if (!IsTargetValid()) {
                State = BeeState.Orbiting;
                StateTimer = 0;
                return;
            }

            NPC target = Main.npc[targetNPCID];

            //附着在目标上
            Projectile.Center = target.Center + latchOffset;
            Projectile.velocity = target.velocity;

            //啄刺节律计时（伤害由接触判定结算，节律只驱动动画）
            stingTimer++;
            if (stingTimer >= StingInterval) {
                stingTimer = 0;
            }

            //攻击时间结束或目标死亡
            if (StateTimer > MaxAttackTime || target.life <= 0) {
                State = BeeState.Orbiting;
                StateTimer = 0;

                //脱离，拽断的蜜挂丝化两粒小滴
                Vector2 outward = latchOffset.SafeNormalize(-Vector2.UnitY);
                FishHoneyVFX.DropletBurst(Projectile.Center, outward, 2, 1.6f, 0.5f, false);
                stubTimer = 1f;
            }
        }

        private void ReturningBehavior() {
            Player owner = Main.player[Projectile.owner];

            MoveTowards(owner.Center, 0.9f);

            if (Vector2.DistanceSquared(Projectile.Center, owner.Center) < 30f * 30f) {
                Projectile.Kill();
            }
        }

        private void MoveTowards(Vector2 target, float speedMultiplier) {
            Vector2 direction = target - Projectile.Center;
            float distance = direction.Length();

            if (distance > 5f) {
                direction.Normalize();
                Vector2 desiredVelocity = direction * MaxSpeed * speedMultiplier;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, Acceleration / 10f);
            }

            //添加细微随机扰动
            Projectile.velocity += Main.rand.NextVector2Circular(0.3f, 0.3f);

            //限制最大速度
            if (Projectile.velocity.Length() > MaxSpeed * speedMultiplier) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * MaxSpeed * speedMultiplier;
            }
        }

        private void ApplySeparation() {
            Vector2 separation = Vector2.Zero;
            int count = 0;

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (other.active &&
                    other.type == Projectile.type &&
                    other.whoAmI != Projectile.whoAmI &&
                    other.owner == Projectile.owner) {

                    float distance = Vector2.Distance(Projectile.Center, other.Center);
                    if (distance < 25f && distance > 0.1f) {
                        separation += (Projectile.Center - other.Center) / distance;
                        count++;
                    }
                }
            }

            if (count > 0) {
                separation /= count;
                Projectile.velocity += separation * 0.5f;
            }
        }

        private bool IsTargetValid() {
            if (targetNPCID < 0 || targetNPCID >= Main.maxNPCs) return false;
            NPC target = Main.npc[targetNPCID];
            return target.active && target.CanBeChasedBy();
        }

        private bool IsCoreLive() {
            int coreID = (int)CoreID;
            if (coreID < 0 || coreID >= Main.maxProjectiles) return false;
            Projectile core = Main.projectile[coreID];
            return core.active && core.type == ModContent.ProjectileType<HoneyBeeSwarmCore>();
        }

        private Projectile GetCoreProjectile() {
            int coreID = (int)CoreID;
            if (coreID < 0 || coreID >= Main.maxProjectiles) return null;
            Projectile core = Main.projectile[coreID];
            if (!core.active || core.type != ModContent.ProjectileType<HoneyBeeSwarmCore>()) return null;
            return core;
        }

        private void BeginAttack() {
            State = BeeState.Attacking;
            StateTimer = 0;
            stingTimer = 0;
            strandActive = false;

            //计算附着偏移
            NPC target = Main.npc[targetNPCID];
            latchOffset = Projectile.Center - target.Center;
            if (latchOffset.LengthSquared() < 1f) {
                latchOffset = new Vector2(Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(-10f, 10f));
            }

            //音效
            SoundEngine.PlaySound(SoundID.NPCHit1 with {
                Volume = 0.4f,
                Pitch = 0.3f
            }, Projectile.Center);
        }

        public void BeginReturn() {
            State = BeeState.Returning;
            StateTimer = 0;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //真实命中拍
            Vector2 outward = latchOffset == Vector2.Zero ? -Vector2.UnitY : latchOffset.SafeNormalize(-Vector2.UnitY);
            FishHoneyVFX.StingSplash(Projectile.Center, outward);
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.22f, Pitch = 0.5f, MaxInstances = 3 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item97 with { Volume = 0.12f, Pitch = 0.65f, MaxInstances = 2 }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            //融滴退场
            FishHoneyVFX.DropletBurst(Projectile.Center, Vector2.Zero, Main.rand.Next(3, 5), 2.2f, 0.7f);
            FishHoneyVFX.GlugSound(Projectile.Center, 0.15f, 0.22f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            int frameHeight = texture.Height / Main.projFrames[Projectile.type];
            Rectangle sourceRect = new Rectangle(0, frameHeight * Projectile.frame, texture.Width, frameHeight);
            Vector2 origin = new Vector2(texture.Width / 2f, frameHeight / 2f);

            float speed = Projectile.velocity.Length();
            bool dashing = State == BeeState.Seeking && StateTimer > WindupFrames && speed > 7f;

            //嗡嗡感
            float tt = Main.GameUpdateCount;
            float agit = 1f;
            if (State == BeeState.Seeking && StateTimer <= WindupFrames) {
                agit = 2.2f;
            }
            else if (State == BeeState.Attacking) {
                agit = 0.7f;
            }
            Vector2 buzz = new Vector2(
                MathF.Sin(tt * 0.73f + Projectile.whoAmI * 1.7f) + 0.5f * MathF.Sin(tt * 0.41f + Projectile.whoAmI * 3.1f),
                MathF.Cos(tt * 0.67f + Projectile.whoAmI * 2.3f) + 0.5f * MathF.Sin(tt * 0.53f + Projectile.whoAmI * 0.9f)
            ) * (1.25f * agit);

            //附着啄刺节律，蓄-刺-回
            Vector2 jab = Vector2.Zero;
            float stabSquash = 0f;
            if (State == BeeState.Attacking && IsTargetValid()) {
                NPC target = Main.npc[targetNPCID];
                Vector2 inward = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                float cyc = stingTimer / (float)StingInterval;
                if (cyc < 0.7f) {
                    jab = -inward * (4f * (cyc / 0.7f));
                }
                else {
                    float s = (cyc - 0.7f) / 0.3f;
                    jab = inward * (7f * s - 4f);
                    stabSquash = MathF.Sin(s * MathHelper.Pi) * 0.22f;
                }
            }

            Vector2 drawCenter = Projectile.Center + buzz + jab;

            //蜜丝画在蜂体之下，根部锚在蜜团/出发点
            DrawStrands(drawCenter);

            //飞行时的轻微波动
            float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.whoAmI * 0.5f) * 0.15f;
            float velStretch = dashing ? MathHelper.Clamp((speed - 6f) * 0.05f, 0f, 0.4f) : 0f;
            float scaleX = 1f + wave * 0.1f + velStretch;
            float scaleY = 1f - wave * 0.08f - velStretch * 0.4f - stabSquash;

            SpriteEffects effects = facingRight ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            //出生蜜釉
            float glaze = 1f - MathHelper.Clamp(spawnAge / 14f, 0f, 1f);
            Color bodyCol = Color.Lerp(lightColor, FishHoneyVFX.HoneyAmber, 0.3f + glaze * 0.45f);

            //突进残影链，速度方向的旋转拖影
            if (dashing) {
                for (int k = 3; k >= 1; k--) {
                    if (Projectile.oldPos[k] == Vector2.Zero) {
                        continue;
                    }
                    Vector2 gp = Projectile.oldPos[k] + Projectile.Size / 2f - Main.screenPosition;
                    Color gcol = Color.Lerp(lightColor, FishHoneyVFX.HoneyAmber, 0.65f) * (0.3f - k * 0.08f);
                    Main.EntitySpriteDraw(texture, gp, sourceRect, gcol, Projectile.oldRot[k], origin
                        , new Vector2(scaleX, scaleY) * Projectile.scale * (1f - k * 0.06f), effects, 0);
                }
            }

            Main.EntitySpriteDraw(
                texture,
                drawCenter - Main.screenPosition,
                sourceRect,
                bodyCol,
                Projectile.rotation,
                origin,
                new Vector2(scaleX, scaleY) * Projectile.scale,
                effects,
                0
            );

            //翅面微闪
            if (Projectile.frame == 1 || Projectile.frame == 3) {
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    Main.EntitySpriteDraw(glow, drawCenter + new Vector2(0f, -5f) * Projectile.scale - Main.screenPosition, null
                        , FishHoneyVFX.HoneyGlint with { A = 0 } * 0.3f, 0f, glow.Size() / 2f, 0.035f, SpriteEffects.None, 0);
                }
            }

            return false;
        }

        private void DrawStrands(Vector2 drawCenter) {
            if (strandActive) {
                float stretch = MathHelper.Clamp(Vector2.Distance(lastStrandAnchor, drawCenter) / strandSnapLen, 0f, 1f);
                FishHoneyVFX.DrawStrand(Main.spriteBatch, lastStrandAnchor, drawCenter, stretch, 0.9f);
            }
            else if (stubTimer > 0.05f) {
                //断丝回缩，残端拖在蜂后收拢
                Vector2 tail = drawCenter - Projectile.velocity.SafeNormalize(Vector2.UnitY) * (26f * stubTimer);
                FishHoneyVFX.DrawStrand(Main.spriteBatch, tail, drawCenter, 0.4f, 0.7f * stubTimer);
            }
        }
    }
    #endregion
}
