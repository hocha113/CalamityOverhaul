using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
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
    internal class FishBloodyManowar : FishSkill
    {
        public override int UnlockFishID => ItemID.BloodyManowar;
        public override int DefaultCooldown => 300 - HalibutData.GetDomainLayer() * 24;
        public override int ResearchDuration => 60 * 18;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (Cooldown <= 0) {
                Use(item, player);
            }
            return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
        }

        public override void Use(Item item, Player player) {
            SetCooldown();

            Vector2 targetPos = Main.MouseWorld;
            ShootState shootState = player.GetShootState();

            //生成水母群控制器
            Projectile.NewProjectile(
                player.GetSource_ItemUse(item),
                targetPos,
                Vector2.Zero,
                ModContent.ProjectileType<BloodySwarmController>(),
                (int)(shootState.WeaponDamage * (2f + HalibutData.GetDomainLayer() * 0.5f)),
                shootState.WeaponKnockback * 2.5f,
                player.whoAmI
            );

            //召唤音效
            SoundEngine.PlaySound(SoundID.NPCDeath19 with { Volume = 0.7f, Pitch = -0.3f }, targetPos);
            SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.6f, Pitch = -0.2f }, targetPos);
        }
    }

    /// <summary>
    /// 血腥水母群控制器：Spawning 血雾中泳出 → Hovering 呼吸游曳 → Converging 屏息聚拢 → Exploding 逐只破裂。<br/>
    /// 全部水母伞膜经 <see cref="IPrimitiveDrawable"/> 一次索引图元批量提交（shader 缺省时单位侧精灵降级）。<br/>
    /// ai[0]=阶段 ai[1]=阶段计时 ai[2]=聚拢进度
    /// </summary>
    internal class BloodySwarmController : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public enum SwarmPhase
        {
            Spawning,//生成扩散
            Hovering,//悬浮等待
            Converging,//聚拢冲击
            Exploding//爆炸消散
        }

        public SwarmPhase Phase {
            get => (SwarmPhase)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        public ref float PhaseTimer => ref Projectile.ai[1];
        public ref float ConvergenceProgress => ref Projectile.ai[2];
        public Player Owner => Main.player[Projectile.owner];

        public List<int> jellyfishList = new List<int>();
        public Vector2 centerPoint;//聚集中心点
        public bool hasCausedDamage = false;

        private const int SpawnDuration = 25;//生成扩散阶段
        private const int HoverDuration = 35;//悬浮等待阶段
        private const int ConvergeDuration = 20;//聚拢冲击阶段
        private const int ExplodeDuration = 30;//爆炸消散阶段

        //伞膜批量顶点缓冲，按水母数量一次分配
        private VertexPositionColorTexture[] bellVerts;
        private short[] bellIndices;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 400;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = SpawnDuration + HoverDuration + ConvergeDuration + ExplodeDuration + 10;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            PhaseTimer++;

            //初始化中心点
            if (PhaseTimer == 1) {
                centerPoint = Projectile.Center;
            }

            switch (Phase) {
                case SwarmPhase.Spawning:
                    SpawningPhaseAI();
                    break;
                case SwarmPhase.Hovering:
                    HoveringPhaseAI();
                    break;
                case SwarmPhase.Converging:
                    ConvergingPhaseAI();
                    break;
                case SwarmPhase.Exploding:
                    ExplodingPhaseAI();
                    break;
            }

            Projectile.Center = centerPoint;
        }

        private void SpawningPhaseAI() {
            if (PhaseTimer == 1) {
                int layer = HalibutData.GetDomainLayer(Owner);
                int jellyfishCount = 25 + layer * 6;//水母数量随层数增长

                //环形扩散生成水母
                for (int i = 0; i < jellyfishCount; i++) {
                    float angle = MathHelper.TwoPi * i / jellyfishCount;
                    float distance = Main.rand.NextFloat(180f, 280f);
                    Vector2 offset = angle.ToRotationVector2() * distance;

                    int proj = Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        centerPoint,
                        Vector2.Zero,
                        ModContent.ProjectileType<BloodyJellyfishUnit>(),
                        Projectile.damage,
                        Projectile.knockBack,
                        Projectile.owner,
                        Projectile.whoAmI,
                        i
                    );

                    if (proj >= 0) {
                        jellyfishList.Add(proj);
                        if (Main.projectile[proj].ModProjectile is BloodyJellyfishUnit unit) {
                            unit.targetOffset = offset;
                            unit.hoverHeight = Main.rand.NextFloat(9f, 16f);
                        }
                    }
                }

                //召唤原点：暗血在水中晕开，禁 pop-in
                FishBloodyManowarVFX.MistPuff(centerPoint, 9, 3.2f, 1.5f, 34, 56);
                FishBloodyManowarVFX.DropletSpray(centerPoint, -Vector2.UnitY, 7, 2f, 6f, 1.15f, 0.24f);
            }

            //扩散途中的稀薄血尘（Dust 只作底噪）
            if (PhaseTimer % 4 == 0 && !Main.dedServ) {
                float progress = PhaseTimer / (float)SpawnDuration;
                float radius = MathHelper.Lerp(30f, 260f, progress);
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Dust d = Dust.NewDustPerfect(centerPoint + angle.ToRotationVector2() * radius, DustID.Blood,
                    Vector2.Zero, 150, FishBloodyManowarVFX.BloodDeep, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = true;
            }

            if (PhaseTimer >= SpawnDuration) {
                Phase = SwarmPhase.Hovering;
                PhaseTimer = 0;
                SoundEngine.PlaySound(SoundID.NPCHit19 with { Volume = 0.5f, Pitch = 0.2f }, centerPoint);
            }
        }

        private void HoveringPhaseAI() {
            //环境血尘：稀薄悬浮
            if (PhaseTimer % 8 == 0 && !Main.dedServ) {
                Vector2 pos = centerPoint + Main.rand.NextVector2Circular(250f, 220f);
                Dust mist = Dust.NewDustPerfect(pos, DustID.Blood,
                    Main.rand.NextVector2Circular(0.6f, 0.6f), 160,
                    FishBloodyManowarVFX.BloodDeep, Main.rand.NextFloat(1f, 1.6f));
                mist.noGravity = true;
            }

            //脉动光效
            if (PhaseTimer % 20 == 0) {
                Lighting.AddLight(centerPoint, 0.5f, 0.07f, 0.09f);
            }

            if (PhaseTimer >= HoverDuration) {
                Phase = SwarmPhase.Converging;
                PhaseTimer = 0;
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.7f, Pitch = -0.4f }, centerPoint);
            }
        }

        private void ConvergingPhaseAI() {
            ConvergenceProgress = PhaseTimer / ConvergeDuration;

            //屏息段：血雾自外围被吸向中心，预告收束方向
            if (PhaseTimer <= 8 && PhaseTimer % 2 == 0 && !Main.dedServ) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = centerPoint + ang.ToRotationVector2() * Main.rand.NextFloat(130f, 230f);
                Vector2 vel = (centerPoint - pos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2.6f, 4f);
                PRTLoader.NewParticle<PRT_FishBloodyManowarMist>(pos, vel
                    , FishBloodyManowarVFX.BloodDeep, Main.rand.NextFloat(0.8f, 1.1f))
                    ?.Configure(Main.rand.Next(20, 30), 0.5f);
            }

            //聚拢冲击音效
            if (PhaseTimer == 5) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.8f, Pitch = -0.5f }, centerPoint);
            }

            //产生冲击波伤害（聚拢完成瞬间）
            if (PhaseTimer == ConvergeDuration - 3 && !hasCausedDamage) {
                CreateImpactWave();
                hasCausedDamage = true;
            }

            if (PhaseTimer >= ConvergeDuration) {
                Phase = SwarmPhase.Exploding;
                PhaseTimer = 0;
            }
        }

        private void ExplodingPhaseAI() {
            //触发所有水母消散
            if (PhaseTimer == 1) {
                foreach (int projIndex in jellyfishList) {
                    if (Main.projectile.IndexInRange(projIndex) && Main.projectile[projIndex].active) {
                        Main.projectile[projIndex].ai[2] = 1f;//消散标记
                    }
                }

                //余韵：血雾沉降 + 慢血滴
                SpawnAftermathBloom(centerPoint);
            }

            if (PhaseTimer >= ExplodeDuration) {
                Projectile.Kill();
            }
        }

        private void CreateImpactWave() {
            //生成多层冲击波
            int waveCount = 1 + HalibutData.GetDomainLayer(Owner) / 3;
            for (int i = 0; i < waveCount; i++) {
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    centerPoint,
                    Vector2.Zero,
                    ModContent.ProjectileType<BloodyStrikeWave>(),
                    Projectile.damage * 2,
                    Projectile.knockBack * 2f,
                    Projectile.owner,
                    ai0: i * 0.15f//延迟错开
                );
            }

            //冲击音效叠加：闷响 + 爆点 + 湿腻破膜
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f, Pitch = -0.4f }, centerPoint);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.7f, Pitch = -0.5f }, centerPoint);
            SoundEngine.PlaySound(SoundID.NPCDeath19 with { Volume = 0.55f, Pitch = -0.15f }, centerPoint);

            FishBloodyManowarVFX.Punch(centerPoint, Vector2.UnitY, 6.5f, 10f, 12, 900f);

            if (Main.dedServ) {
                return;
            }

            //暖色过曝点：极小热环数帧即灭，仍属液面语言而非白光球
            PRTLoader.NewParticle<PRT_DWave>(centerPoint, Vector2.Zero, FishBloodyManowarVFX.HotFlash, 0.08f)
                ?.Configure(new Vector2(1f, 0.86f), 0f, 0.5f, 8);
            //双层血环：外深内亮，纵向微压出液面透视
            PRTLoader.NewParticle<PRT_DWave>(centerPoint, Vector2.Zero, FishBloodyManowarVFX.BloodDeep, 0.24f)
                ?.Configure(new Vector2(1f, 0.86f), 0f, 1.7f, 18);
            PRTLoader.NewParticle<PRT_DWave>(centerPoint, Vector2.Zero, FishBloodyManowarVFX.Blood, 0.15f)
                ?.Configure(new Vector2(1f, 0.86f), 0f, 1.05f, 13);
            //血珠喷泉：向四周甩出后被重力接管
            for (int i = 0; i < 16; i++) {
                float ang = MathHelper.TwoPi * i / 16f + Main.rand.NextFloat(-0.16f, 0.16f);
                Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(4f, 12.5f);
                vel.Y -= 1.6f;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(centerPoint, vel
                    , Main.rand.NextBool(3) ? FishBloodyManowarVFX.BloodDeep : FishBloodyManowarVFX.Blood
                    , Main.rand.NextFloat(1f, 1.8f))?.Configure(Main.rand.Next(24, 40), 0.32f);
            }
            //伞膜撕片与压底暗雾
            FishBloodyManowarVFX.ShredBurst(centerPoint, -Vector2.UnitY, 8, 7f);
            FishBloodyManowarVFX.MistPuff(centerPoint, 6, 4.5f, 1.7f, 36, 60);
            //廉价血尘补场
            for (int i = 0; i < 12; i++) {
                Dust blood = Dust.NewDustPerfect(centerPoint, DustID.Blood, Main.rand.NextVector2Circular(11f, 9f),
                    120, FishBloodyManowarVFX.Blood, Main.rand.NextFloat(1.4f, 2.2f));
                blood.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>爆发后的余韵：沉降暗雾与慢速血滴，活得比控制器久</summary>
        private static void SpawnAftermathBloom(Vector2 center) {
            if (Main.dedServ) {
                return;
            }
            FishBloodyManowarVFX.MistPuff(center, 7, 2.2f, 1.9f, 46, 72);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3f, 2f);
                vel.Y -= 0.8f;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(center + Main.rand.NextVector2Circular(46f, 34f), vel
                    , FishBloodyManowarVFX.BloodDeep, Main.rand.NextFloat(1.1f, 1.7f))
                    ?.Configure(Main.rand.Next(30, 48), 0.24f);
            }
            SoundEngine.PlaySound(SoundID.NPCDeath19 with { Volume = 0.6f, Pitch = -0.2f }, center);
        }

        public override void OnKill(int timeLeft) {
            //清理所有水母
            foreach (int projIndex in jellyfishList) {
                if (Main.projectile.IndexInRange(projIndex) && Main.projectile[projIndex].active) {
                    Main.projectile[projIndex].Kill();
                }
            }
        }

        /// <summary>取回仍存活的水母单位实例</summary>
        private BloodyJellyfishUnit GetUnit(int listIndex) {
            int projIndex = jellyfishList[listIndex];
            if (!Main.projectile.IndexInRange(projIndex)) {
                return null;
            }
            Projectile proj = Main.projectile[projIndex];
            if (!proj.active || proj.ModProjectile is not BloodyJellyfishUnit unit) {
                return null;
            }
            return unit;
        }

        //==== 血丝拉线：聚拢时个体间与中心之间的粘稠牵连，先垂坠后绷直 ====

        public override bool PreDraw(ref Color lightColor) {
            if (Phase != SwarmPhase.Converging || jellyfishList.Count == 0) {
                return false;
            }
            float ct = MathHelper.Clamp(ConvergenceProgress, 0f, 1f);
            //淡入于屏息段，收束后随个体贴近而自然消失
            float alpha = MathHelper.Clamp(ct / 0.22f, 0f, 1f) * 0.5f;
            if (alpha <= 0.02f) {
                return false;
            }
            float sag = 1f - VaultUtils.EaseInCubic(ct);
            SpriteBatch sb = Main.spriteBatch;
            int n = jellyfishList.Count;
            for (int i = 0; i < n; i++) {
                BloodyJellyfishUnit unit = GetUnit(i);
                if (unit == null || !unit.MembraneAlive) {
                    continue;
                }
                //环邻拉线：相邻个体之间的血丝
                BloodyJellyfishUnit next = GetUnit((i + 1) % n);
                if (next != null && next.MembraneAlive) {
                    float dist = Vector2.Distance(unit.Projectile.Center, next.Projectile.Center);
                    if (dist > 26f && dist < 340f) {
                        FishBloodyManowarVFX.DrawBloodThread(sb, unit.Projectile.Center, next.Projectile.Center
                            , sag, alpha, i * 0.173f);
                    }
                }
                //向心拉线：每三只一根，收束方向的可读化
                if (i % 3 == 0) {
                    float distC = Vector2.Distance(unit.Projectile.Center, centerPoint);
                    if (distC > 40f) {
                        FishBloodyManowarVFX.DrawBloodThread(sb, unit.Projectile.Center, centerPoint
                            , sag * 0.7f, alpha * 0.8f, i * 0.311f + 0.5f);
                    }
                }
            }
            return false;
        }

        //==== 伞膜批量绘制：一次索引图元提交全部水母伞钟 ====

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect fx = FishBloodyManowarAssets.FishBloodyManowarBell;
            if (fx == null || !Projectile.active || CWRAsset.PerlinNoise?.Value == null || jellyfishList.Count == 0) {
                return;
            }

            int n = 0;
            int total = jellyfishList.Count;
            if (bellVerts == null || bellVerts.Length < total * 4) {
                bellVerts = new VertexPositionColorTexture[total * 4];
                bellIndices = new short[total * 6];
            }

            for (int i = 0; i < total; i++) {
                BloodyJellyfishUnit unit = GetUnit(i);
                if (unit == null || !unit.MembraneAlive) {
                    continue;
                }

                Vector2 center = unit.Projectile.Center;
                float rot = unit.swimAngle + MathHelper.PiOver2;//伞顶指向游动方向
                float scale = unit.Projectile.scale;
                float hw = 27f * scale;
                float hh = 30f * scale;
                Vector2 axX = rot.ToRotationVector2();
                Vector2 axY = (rot + MathHelper.PiOver2).ToRotationVector2();
                //顶点色打包：R 收缩量 G 透明度 B 种子
                Color pack = new Color(unit.contract, unit.membraneAlpha, unit.seedVal);

                int v = n * 4;
                Vector2 p0 = center - axX * hw - axY * hh;
                Vector2 p1 = center + axX * hw - axY * hh;
                Vector2 p2 = center - axX * hw + axY * hh;
                Vector2 p3 = center + axX * hw + axY * hh;
                bellVerts[v + 0] = new VertexPositionColorTexture(new Vector3(p0.X, p0.Y, 0f), pack, new Vector2(0f, 0f));
                bellVerts[v + 1] = new VertexPositionColorTexture(new Vector3(p1.X, p1.Y, 0f), pack, new Vector2(1f, 0f));
                bellVerts[v + 2] = new VertexPositionColorTexture(new Vector3(p2.X, p2.Y, 0f), pack, new Vector2(0f, 1f));
                bellVerts[v + 3] = new VertexPositionColorTexture(new Vector3(p3.X, p3.Y, 0f), pack, new Vector2(1f, 1f));

                int idx = n * 6;
                bellIndices[idx + 0] = (short)(v + 0);
                bellIndices[idx + 1] = (short)(v + 1);
                bellIndices[idx + 2] = (short)(v + 2);
                bellIndices[idx + 3] = (short)(v + 2);
                bellIndices[idx + 4] = (short)(v + 1);
                bellIndices[idx + 5] = (short)(v + 3);
                n++;
            }

            if (n == 0) {
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            DepthStencilState prevDepth = device.DepthStencilState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uNoiseTex"]?.SetValue(CWRAsset.PerlinNoise.Value);
            fx.Parameters["uColMembrane"]?.SetValue(FishBloodyManowarVFX.Membrane.ToVector3());
            fx.Parameters["uColDark"]?.SetValue(FishBloodyManowarVFX.MembraneDark.ToVector3());
            fx.Parameters["uColRim"]?.SetValue(FishBloodyManowarVFX.Rim.ToVector3());
            fx.Parameters["uColOrgan"]?.SetValue(FishBloodyManowarVFX.Organ.ToVector3());

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, bellVerts, 0, n * 4, bellIndices, 0, n * 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }
    }

    /// <summary>
    /// 血腥水母单元：收缩-滑行两拍泳姿的软体推进器。<br/>
    /// 收缩拍瞬间获得向目标的冲量并甩落血珠，滑行拍靠阻尼漂移；伞膜（shader 批量层）
    /// 盖在内脏精灵之上构成半透明夹心。破裂 = 伞膜撕片 + 血珠 + 暗雾，内脏残躯坠落淌血。<br/>
    /// ai[0]=控制器索引 ai[1]=单位序号 ai[2]=消散标记
    /// </summary>
    internal class BloodyJellyfishUnit : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.BloodyManowar;

        private ref float ControllerID => ref Projectile.ai[0];
        private ref float UnitIndex => ref Projectile.ai[1];
        private ref float IsDissipating => ref Projectile.ai[2];

        public Vector2 targetOffset;//目标偏移位置
        public float hoverHeight;//悬浮摆动幅度

        //==== 泳姿状态（供控制器批量绘制读取）====
        public float contract;//收缩量0-1，驱动伞形挤压与缘部提亮
        public float membraneAlpha;//伞膜透明度
        public float seedVal;//每只随机种子
        public float swimAngle;//游动朝向（伞顶指向）

        private Vector2 swimVel;//滑行速度
        private float swimPhase;//泳动相位
        private float prevWave;//上一帧收缩波形，检测收缩拍触发
        private float hoverPhase;
        private Vector2 currentPos;
        private float spawnFade;//材质化淡入
        private float organFade = 1f;//内脏残躯透明度
        private int ruptureDelay = -1;//破裂错峰延迟
        private int ruptureTimer;
        private bool membranePopped;

        public bool MembraneAlive => membraneAlpha > 0.02f && !membranePopped;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 200;
        }

        public override void AI() {
            if (!ControllerID.TryGetProjectile(out var controller)
                || controller.ModProjectile is not BloodySwarmController swarmCtrl) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 200;

            if (spawnFade == 0f) {
                //出生初始化：叠在中心，相位错开
                currentPos = swarmCtrl.centerPoint + Main.rand.NextVector2Circular(14f, 10f);
                seedVal = Main.rand.NextFloat();
                swimPhase = Main.rand.NextFloat(MathHelper.TwoPi);
                hoverPhase = Main.rand.NextFloat(MathHelper.TwoPi);
                Projectile.Center = currentPos;
            }

            hoverPhase += 0.05f;

            if (IsDissipating != 0f) {
                RuptureAI();
                return;
            }

            spawnFade = MathF.Min(spawnFade + 0.1f, 1f);
            membraneAlpha = spawnFade;

            SwimAI(swarmCtrl);

            //伞体呼吸缩放
            Projectile.scale = 0.9f + 0.08f * MathF.Sin(hoverPhase + UnitIndex * 0.4f) + contract * 0.08f;

            //裙缘偶发滴血：液体在自重下离体
            if (!Main.dedServ && Main.rand.NextBool(70)) {
                Vector2 skirt = currentPos + (swimAngle + MathHelper.Pi).ToRotationVector2() * 14f;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(skirt, new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), 0.4f)
                    , FishBloodyManowarVFX.BloodDeep, Main.rand.NextFloat(0.7f, 1.1f))
                    ?.Configure(Main.rand.Next(20, 32), 0.26f);
            }

            Lighting.AddLight(currentPos, 0.14f, 0.02f, 0.03f);
        }

        /// <summary>收缩-滑行推进：收缩拍打出冲量，其余时间阻尼滑行 + 弱弹簧回位</summary>
        private void SwimAI(BloodySwarmController swarmCtrl) {
            Vector2 centerPoint = swarmCtrl.centerPoint;
            Vector2 slotPos = centerPoint + targetOffset;
            float phase = (float)swarmCtrl.Phase;

            Vector2 target;
            float track;//弱弹簧回位强度
            float pulseRate;//泳动相位速度
            float pulseImpulse;//收缩拍冲量
            float contractFloor = 0f;//收缩量下限（聚拢屏息时全员紧绷）

            if (phase == 0f) {//Spawning泳出扩散
                float t = MathHelper.Clamp(swarmCtrl.PhaseTimer / 25f, 0f, 1f);
                target = centerPoint + targetOffset * VaultUtils.EaseOutCubic(t);
                track = 0.14f;
                pulseRate = 0.17f;
                pulseImpulse = 1.6f;
            }
            else if (phase == 1f) {//Hovering原位游曳
                float bob = MathF.Sin(hoverPhase + UnitIndex * 0.3f) * hoverHeight;
                target = slotPos + new Vector2(MathF.Sin(hoverPhase * 0.6f + seedVal * 9f) * 6f, bob);
                track = 0.05f;
                pulseRate = 0.085f;
                pulseImpulse = 0.9f;
                //末段泳动相位向全群同步收拢：攻击前的整齐吸气预告
                if (swarmCtrl.PhaseTimer > 23f) {
                    float shared = swarmCtrl.PhaseTimer * 0.12f;
                    swimPhase = MathHelper.Lerp(swimPhase, shared + MathF.Floor((swimPhase - shared) / MathHelper.TwoPi + 0.5f) * MathHelper.TwoPi, 0.12f);
                }
            }
            else if (phase == 2f) {//Converging屏息-释放
                float ct = swarmCtrl.ConvergenceProgress;
                if (ct < 0.25f) {
                    //屏息：轻微后坐，全员深收缩蓄势
                    float inhale = ct / 0.25f;
                    target = slotPos + targetOffset.SafeNormalize(Vector2.Zero) * 9f * inhale;
                    track = 0.12f;
                    pulseRate = 0.03f;
                    pulseImpulse = 0f;
                    contractFloor = inhale * 0.95f;
                }
                else {
                    //释放：越贴近越快的向心猛冲
                    float rel = (ct - 0.25f) / 0.75f;
                    target = Vector2.Lerp(slotPos, centerPoint, VaultUtils.EaseInCubic(rel));
                    track = 0.13f + 0.5f * rel * rel;
                    pulseRate = 0.23f;
                    pulseImpulse = 2.7f;
                    contractFloor = 0.62f;
                }
            }
            else {//Exploding未破裂个体挤在中心躁动
                target = centerPoint + Main.rand.NextVector2Circular(5f, 5f);
                track = 0.3f;
                pulseRate = 0.3f;
                pulseImpulse = 0.4f;
            }

            swimPhase += pulseRate;
            float wave = 0.5f + 0.5f * MathF.Sin(swimPhase);
            contract = MathF.Max(MathF.Pow(wave, 3f), contractFloor);

            //收缩拍触发：波形爬上峰值窗口的那一帧打出冲量并甩落血珠
            Vector2 toTarget = target - currentPos;
            if (wave > 0.86f && prevWave <= 0.86f && pulseImpulse > 0f) {
                Vector2 dir = toTarget.SafeNormalize((swimAngle).ToRotationVector2());
                swimVel += dir * pulseImpulse;
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Vector2 skirt = currentPos - dir * 13f;
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(skirt, -dir * Main.rand.NextFloat(1f, 2.2f)
                        , FishBloodyManowarVFX.Blood, Main.rand.NextFloat(0.6f, 1f))
                        ?.Configure(Main.rand.Next(14, 24), 0.24f);
                }
            }
            prevWave = wave;

            //滑行 + 弱弹簧回位
            swimVel *= 0.90f;
            currentPos += swimVel;
            currentPos += toTarget * track;
            Projectile.Center = currentPos;

            //朝向：滑行速度足够时沿速度，否则伞顶朝上带轻微摇摆
            Vector2 heading = swimVel + toTarget * track * 2f;
            if (heading.LengthSquared() > 0.5f) {
                float targetAngle = heading.ToRotation();
                swimAngle = swimAngle.AngleLerp(targetAngle, 0.2f);
            }
            else {
                swimAngle = swimAngle.AngleLerp(-MathHelper.PiOver2 + MathF.Sin(hoverPhase * 0.7f + seedVal * 5f) * 0.24f, 0.08f);
            }
            Projectile.rotation = swimAngle + MathHelper.PiOver2;

            //高速冲刺时收缩量随速度进一步顶满：速度可读
            contract = MathF.Min(contract + swimVel.Length() * 0.03f, 1f);
        }

        /// <summary>破裂时序：错峰躁动 → 伞膜爆开 → 内脏残躯坠落淌血，禁 pop-out</summary>
        private void RuptureAI() {
            if (ruptureDelay < 0) {
                ruptureDelay = Main.rand.Next(0, 15);
            }

            if (!membranePopped) {
                //破裂前的高频痉挛
                swimPhase += 0.5f;
                contract = 0.6f + 0.4f * MathF.Abs(MathF.Sin(swimPhase));
                currentPos += Main.rand.NextVector2Circular(1.2f, 1.2f);
                Projectile.Center = currentPos;

                if (ruptureTimer >= ruptureDelay) {
                    membranePopped = true;
                    membraneAlpha = 0f;
                    if (!Main.dedServ) {
                        Vector2 up = swimAngle.ToRotationVector2();
                        FishBloodyManowarVFX.ShredBurst(currentPos, up, 2, 5f);
                        FishBloodyManowarVFX.DropletSpray(currentPos, up, 3, 2.5f, 7f, 1.3f, 0.3f);
                        if (Main.rand.NextBool(3)) {
                            FishBloodyManowarVFX.MistPuff(currentPos, 1, 1.5f, 1f, 26, 40);
                        }
                        SoundEngine.PlaySound(SoundID.NPCDeath19 with {
                            Volume = 0.22f,
                            Pitch = Main.rand.NextFloat(-0.45f, -0.1f),
                            MaxInstances = 5
                        }, currentPos);
                    }
                }
                ruptureTimer++;
                return;
            }

            //残躯坠落：重力接管，边坠边淌血
            swimVel.X *= 0.95f;
            swimVel.Y = MathF.Min(swimVel.Y + 0.24f, 6.5f);
            currentPos += swimVel;
            Projectile.Center = currentPos;
            Projectile.rotation += swimVel.X * 0.02f + 0.03f;
            organFade -= 0.075f;

            if (!Main.dedServ && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(currentPos, new Vector2(0f, 0.6f)
                    , FishBloodyManowarVFX.BloodDeep, Main.rand.NextFloat(0.6f, 1f))
                    ?.Configure(Main.rand.Next(16, 26), 0.3f);
            }

            if (organFade <= 0f) {
                Projectile.Kill();
            }
        }

        public override void OnKill(int timeLeft) {
            //死亡血滴：受重力的液体而非发光雾
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center, Main.rand.NextVector2Circular(2.5f, 2f)
                    , FishBloodyManowarVFX.BloodDeep, Main.rand.NextFloat(0.7f, 1.2f))
                    ?.Configure(Main.rand.Next(16, 28), 0.28f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;
            float fade = membranePopped ? organFade : spawnFade;
            if (fade <= 0f) {
                return false;
            }

            //冲刺速度残影：旧位置的暗红内脏虚影
            float speed = swimVel.Length();
            if (speed > 5f && !membranePopped) {
                Color ghost = FishBloodyManowarVFX.BloodDeep with { A = 0 };
                float speedT = MathHelper.Clamp((speed - 5f) / 9f, 0f, 1f);
                for (int i = 2; i < 6; i += 2) {
                    if (i >= Projectile.oldPos.Length || Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    Vector2 gp = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                    Main.EntitySpriteDraw(texture, gp, null, ghost * ((0.3f - i * 0.045f) * speedT * fade)
                        , Projectile.rotation, origin, Projectile.scale * 0.62f, SpriteEffects.None, 0);
                }
            }

            //内脏核体：收缩时沿游向挤压拉伸，压暗到瘀血调
            Color organCol = Color.Lerp(lightColor, FishBloodyManowarVFX.MembraneDark, 0.45f) * fade;
            Vector2 organScale = new Vector2(1f - contract * 0.16f, 1f + contract * 0.2f) * (Projectile.scale * 0.62f);
            Main.EntitySpriteDraw(texture, drawPos, null, organCol,
                Projectile.rotation, origin, organScale, SpriteEffects.None, 0);

            //收缩拍的内脏微光：小面积暖沉血色，随收缩即逝
            if (!membranePopped) {
                Color organGlow = FishBloodyManowarVFX.Organ with { A = 0 };
                Main.EntitySpriteDraw(texture, drawPos, null, organGlow * (0.3f * contract * fade),
                    Projectile.rotation, origin, organScale * 1.06f, SpriteEffects.None, 0);
            }

            //shader 缺省降级：半透明膜色叠罩近似伞膜
            if (FishBloodyManowarAssets.FishBloodyManowarBell == null && !membranePopped) {
                Color memCol = FishBloodyManowarVFX.Membrane * (0.4f * membraneAlpha);
                Main.EntitySpriteDraw(texture, drawPos, null, memCol, Projectile.rotation, origin
                    , new Vector2(1.2f - contract * 0.24f, 1.28f + contract * 0.18f) * Projectile.scale, SpriteEffects.None, 0);
            }

            return false;
        }
    }

    /// <summary>
    /// 血腥冲击波：液面涟漪式扩张，快启动缓收尾，波沿甩出受重力的血珠。<br/>
    /// ai[0]=波次延迟
    /// </summary>
    internal class BloodyStrikeWave : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float DelayOffset => ref Projectile.ai[0];
        private ref float VisualTimer => ref Projectile.localAI[0];
        private float delayTimer = 0f;

        private const float MaxRimRadius = 250f;//波沿最大半径px

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 360;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 45;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.75f;
            }
            if (target.type == CWRID.NPC_DevourerofGodsHead || target.type == CWRID.NPC_DevourerofGodsTail) {
                modifiers.FinalDamage *= 1.33f;
            }
        }

        private float RimRadius => VaultUtils.EaseOutCubic(MathHelper.Clamp(VisualTimer / 32f, 0f, 1f)) * MaxRimRadius;

        public override void AI() {
            //延迟启动
            if (delayTimer < DelayOffset * 60f) {
                delayTimer++;
                Projectile.scale = 0.1f;
                Projectile.alpha = 255;
                return;
            }

            Projectile.scale += 0.2f;
            Projectile.alpha += 7;
            Projectile.rotation += 0.04f;
            VisualTimer++;

            //波沿血珠：涟漪推着液体外缘走，甩出后被重力接管
            if (!Main.dedServ && VisualTimer % 3 == 0 && VisualTimer < 26) {
                float rim = RimRadius;
                for (int i = 0; i < 2; i++) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 rimPos = Projectile.Center + ang.ToRotationVector2() * rim * new Vector2(1f, 0.86f);
                    Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(1.6f, 3.4f);
                    vel.Y -= 0.7f;
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(rimPos, vel
                        , Main.rand.NextBool(3) ? FishBloodyManowarVFX.BloodDeep : FishBloodyManowarVFX.Blood
                        , Main.rand.NextFloat(0.7f, 1.2f))?.Configure(Main.rand.Next(18, 30), 0.3f);
                }
            }

            if (Projectile.alpha >= 255) {
                Projectile.Kill();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //强力减速
            target.velocity *= 0.3f;

            //命中处血珠迸溅 + 暗雾压底
            Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(-Vector2.UnitY);
            FishBloodyManowarVFX.DropletSpray(target.Center, dir, 6, 3f, 7.5f, 0.55f);
            FishBloodyManowarVFX.MistPuff(target.Center, 1, 1.6f, 0.9f, 22, 34);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fadeAlpha = 1f - Projectile.alpha / 255f;
            if (fadeAlpha <= 0f || RimRadius < 8f) {
                return false;
            }
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 squish = new Vector2(1f, 0.86f);//液面透视微压

            //底层软晕：深血色单层，铺氛围不作主体
            Texture2D soft = CWRAsset.DiffusionCircle.Value;
            Main.spriteBatch.Draw(soft, drawPos, null, FishBloodyManowarVFX.BloodDeep with { A = 0 } * (fadeAlpha * 0.4f)
                , Projectile.rotation, soft.Size() / 2f, RimRadius * 2.3f / soft.Width * squish, SpriteEffects.None, 0f);

            //波沿主体：暗红液环线（正常混合压得住背景）
            Texture2D ring = CWRAsset.Ring01?.Value;
            if (ring != null) {
                float ringScale = RimRadius * 2f / ring.Width;
                Main.spriteBatch.Draw(ring, drawPos, null, FishBloodyManowarVFX.Membrane * (fadeAlpha * 0.85f)
                    , Projectile.rotation, ring.Size() / 2f, ringScale * squish, SpriteEffects.None, 0f);
                //内缘饱和血色描线：略小半径的加色薄层
                Main.spriteBatch.Draw(ring, drawPos, null, FishBloodyManowarVFX.Blood with { A = 0 } * (fadeAlpha * 0.5f)
                    , -Projectile.rotation * 0.6f, ring.Size() / 2f, ringScale * 0.93f * squish, SpriteEffects.None, 0f);
            }

            return false;
        }
    }
}
