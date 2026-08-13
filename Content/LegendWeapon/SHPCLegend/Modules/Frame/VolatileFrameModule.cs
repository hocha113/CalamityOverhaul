using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>混沌变异种类，索引对齐 <see cref="VolatileFrameModule.MutMain"/></summary>
    internal enum VolatileMutation
    {
        /// <summary>裂变青，中途分裂两道副束，原束继续</summary>
        Fission = 0,
        /// <summary>过载红，伤害显著提升，弹道狂抖</summary>
        Overload = 1,
        /// <summary>失稳橙，首命中小范围爆炸</summary>
        Unstable = 2,
        /// <summary>畸变紫，飞行急转折向最近敌</summary>
        Aberrant = 3,
    }

    /// <summary>
    /// 不稳定机匣，主束诞生时所有者端 roll 混沌变异，
    /// 写入 ai[2]（0=未roll，1..4=变异+1）跨端一致；
    /// 故障覆层标身份色，激光改周期涌动
    /// </summary>
    internal sealed class VolatileFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //不稳定毒黄，保留旧识别色
        public override Color TintColor => new(220, 255, 40);

        //可调参数

        //变异权重，过载最强故最低
        private const float FissionWeight = 0.30f;
        private const float OverloadWeight = 0.10f;
        private const float UnstableWeight = 0.32f;
        private const float AberrantWeight = 0.20f;

        //裂变，AI调用数触发（每刻3次≈14px，54≈750px）、张角、副束伤速
        private const int FissionDelayCalls = 54;
        private const float FissionForkAngle = 0.36f;
        private const float FissionChildDamage = 0.45f;
        private const float FissionChildSpeed = 14f;

        /// <summary>高额伤倍率、每AI抖动步长、回中混合</summary>
        private const float OverloadDamageMul = 2f;
        private const float OverloadJitterStep = 0.085f;
        private const float OverloadReturnBlend = 0.05f;

        //失稳，爆炸半径与伤占比，仅首命中，蠕虫不连爆
        private const float UnstableRadius = 92f;
        private const float UnstableDamage = 0.55f;

        //畸变，折向延迟（30≈420px）、失败重试、索敌半径
        private const int AberrantDelayCalls = 30;
        private const int AberrantRetryCalls = 15;
        private const float AberrantSnapRange = 760f;

        //激光涌动，窗口帧与两种涌动强度
        private const int LaserSurgeMin = 60;
        private const int LaserSurgeMax = 110;
        private const float LaserOverloadEcho = 0.30f;
        private const int LaserUnstableEveryHits = 5;
        //失稳涌动最小间隔帧，防蠕虫连环
        private const int LaserUnstableGateFrames = 45;
        private const float LaserUnstableRadius = 80f;
        private const float LaserUnstableDamage = 0.5f;

        //变异配色，裂变青/过载红/失稳橙/畸变紫

        internal static readonly Color[] MutMain = {
            new(90, 250, 235),
            new(255, 60, 55),
            new(255, 165, 40),
            new(200, 90, 255),
        };
        internal static readonly Color[] MutAccent = {
            new(20, 150, 160),
            new(150, 10, 30),
            new(175, 80, 5),
            new(110, 30, 180),
        };

        //每束状态，拥有者端

        private sealed class BeamState
        {
            public int Mutation = -1;
            public int AICalls;
            public bool ActionDone;
            /// <summary>过载回中基准角</summary>
            public float OriginalAngle;
            /// <summary>故障覆层索引，事件尖峰直达</summary>
            public int OverlayIdx = -1;
        }

        //whoAmI→状态，OnBeamKill 移除 + OnPlayerUpdate purge 兜底
        private readonly Dictionary<int, BeamState> _states = new();
        private int _purgeTimer;

        //激光涌动，激光单例，模块字段即 per-玩家
        private int _surgeMutation = -1;
        private int _surgeTimer;
        private float _surgeCarry;
        private int _laserHitCounter;
        private uint _lastLaserBlastTick;

        public override void Apply(ref ShootContext ctx) {
            ctx.CritAdd += 4;
            ctx.SpreadMul += 0.25f;
            ctx.ManaCostMul += 0.30f;
        }

        //光束 roll 与逐帧变异

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived) return;                 //派生束不回喂
            Projectile proj = beam.Projectile;
            if (proj.owner != Main.myPlayer) return;    //roll 仅所有者端

            if (!_states.TryGetValue(proj.whoAmI, out BeamState st)) {
                st = new BeamState();
                _states[proj.whoAmI] = st;
            }

            //首次 roll 写 ai[2]；一次性副作用
            //仅真正 roll 时结算，防字典重建后重复施加
            if (st.Mutation < 0) {
                bool rolledNow = proj.ai[2] <= 0f;
                if (rolledNow) {
                    proj.ai[2] = 1 + RollMutation();
                    proj.netUpdate = true;
                }
                st.Mutation = Math.Clamp((int)proj.ai[2] - 1, 0, 3);
                st.OriginalAngle = beam.FlightDirection.ToRotation();
                if (rolledNow) {
                    if (st.Mutation == (int)VolatileMutation.Overload) {
                        proj.damage = Math.Max((int)(proj.damage * OverloadDamageMul), 1);
                    }
                    SpawnOverlay(proj, st);
                    RollFeedback(proj, st.Mutation);
                }
            }

            st.AICalls++;
            switch ((VolatileMutation)st.Mutation) {
                case VolatileMutation.Fission:
                    TickFission(beam, st);
                    break;
                case VolatileMutation.Overload:
                    TickOverload(beam, st);
                    break;
                case VolatileMutation.Aberrant:
                    TickAberrant(beam, st);
                    break;
            }
        }

        /// <summary>裂变，到点分裂两道派生副束</summary>
        private void TickFission(CyberTraceBeamProj beam, BeamState st) {
            if (st.ActionDone || st.AICalls < FissionDelayCalls) return;
            st.ActionDone = true;

            Projectile proj = beam.Projectile;
            Vector2 dir = beam.FlightDirection;
            int dmg = Math.Max((int)(proj.damage * FissionChildDamage), 1);
            for (int s = -1; s <= 1; s += 2) {
                Vector2 vel = dir.RotatedBy(FissionForkAngle * s) * FissionChildSpeed;
                int idx = Projectile.NewProjectile(proj.GetSource_FromThis(),
                    proj.Center, vel,
                    ModContent.ProjectileType<CyberTraceBeamProj>(),
                    dmg, proj.knockBack * 0.5f, proj.owner,
                    ai0: 0); //副束等离子青，对齐裂变色
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Main.projectile[idx].ai[1] = proj.ai[1];
                    if (Main.projectile[idx].ModProjectile is CyberTraceBeamProj child) {
                        child.IsDerived = true;
                        child.LifeMul = 0.5f;
                    }
                }
            }

            PulseOverlay(st, proj, 1f);
            if (Main.netMode != NetmodeID.Server && OnScreen(proj.Center)) {
                SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.32f, Pitch = 0.25f }, proj.Center);
                BurstShards(proj.Center, (int)VolatileMutation.Fission, 8, 4.5f);
            }
        }

        /// <summary>过载，随机抖动缓回瞄准角</summary>
        private static void TickOverload(CyberTraceBeamProj beam, BeamState st) {
            float angle = beam.FlightDirection.ToRotation();
            angle += Main.rand.NextFloat(-OverloadJitterStep, OverloadJitterStep);
            angle = angle.AngleLerp(st.OriginalAngle, OverloadReturnBlend);
            beam.SetFlightDirection(angle.ToRotationVector2());
        }

        /// <summary>畸变，延迟急转折向最近敌，无敌可折则重试</summary>
        private void TickAberrant(CyberTraceBeamProj beam, BeamState st) {
            if (st.ActionDone || st.AICalls < AberrantDelayCalls) return;
            if ((st.AICalls - AberrantDelayCalls) % AberrantRetryCalls != 0) return;

            Projectile proj = beam.Projectile;
            NPC target = proj.Center.FindClosestNPC(AberrantSnapRange, true, true);
            if (target == null) return;
            st.ActionDone = true;

            Vector2 dir = (target.Center - proj.Center).SafeNormalize(Vector2.UnitX);
            beam.SetFlightDirection(dir);
            proj.netUpdate = true;

            PulseOverlay(st, proj, 1f);
            if (Main.netMode != NetmodeID.Server && OnScreen(proj.Center)) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.3f, Pitch = 0.65f }, proj.Center);
                BurstShards(proj.Center, (int)VolatileMutation.Aberrant, 7, 4f);
            }
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.IsDerived) return;
            Projectile proj = beam.Projectile;
            if (proj.owner != Main.myPlayer) return;
            if (!_states.TryGetValue(proj.whoAmI, out BeamState st)) return;

            PulseOverlay(st, proj, 0.7f);
            if (st.Mutation != (int)VolatileMutation.Unstable) return;
            //失稳仅首命中起爆，numHits 未自增，蠕虫不连爆
            if (proj.numHits > 0) return;

            SpawnDetonation(proj, target.Center,
                Math.Max((int)(proj.damage * UnstableDamage), 1), UnstableRadius);
            if (Main.netMode != NetmodeID.Server && OnScreen(target.Center)) {
                BurstShards(target.Center, (int)VolatileMutation.Unstable, 9, 5f);
            }
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            if (beam.IsDerived) return;
            //无死亡演出，覆层自检宿主消亡碎解
            if (_states.Remove(beam.Projectile.whoAmI, out BeamState st)) {
                PulseOverlay(st, beam.Projectile, 0.8f);
            }
        }

        //激光周期随机涌动

        public override void OnLaserAI(CyberPrismLaserProj laser) {
            Projectile proj = laser.Projectile;

            //拥有者推进涌动并 roll，写 ai[2]
            if (proj.owner == Main.myPlayer) {
                if (_surgeTimer <= 0) {
                    _surgeMutation = RollMutation();
                    _surgeTimer = Main.rand.Next(LaserSurgeMin, LaserSurgeMax + 1);
                    _surgeCarry = 0f;
                    _laserHitCounter = 0;
                    proj.ai[2] = 1 + _surgeMutation;
                    proj.netUpdate = true;
                    if (Main.netMode != NetmodeID.Server && OnScreen(proj.Center)) {
                        SoundEngine.PlaySound(SoundID.Item114 with {
                            Volume = 0.2f,
                            Pitch = -0.1f + _surgeMutation * 0.18f
                        }, proj.Center);
                    }
                }
                else {
                    TickDown(ref _surgeTimer, ref _surgeCarry);
                }
            }

            int surge = (int)proj.ai[2] - 1;
            if (surge < 0 || surge > 3) return;

            //主题覆写，身份色+阶梯色相跳变
            Color main = MutMain[surge];
            Color accent = MutAccent[surge];
            float step = MathF.Floor((float)Main.timeForVisualEffects * 0.28f);
            float jump = Hash01(step * 12.9898f + surge * 7.31f);
            Color core = Color.Lerp(main, Color.White, 0.25f + jump * 0.3f);
            Color glowCol = Color.Lerp(main, accent, jump * 0.65f);
            laser.ThemeCore = core;
            laser.ThemeGlow = glowCol;
            laser.ThemeAura = accent;
            laser.ThemeParticleMain = main;
            laser.ThemeParticleEdge = accent;

            //沿激光故障碎条，取近段安全
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(6)) {
                Vector2 pos = proj.Center + proj.rotation.ToRotationVector2() * Main.rand.NextFloat(40f, 620f);
                if (OnScreen(pos)) {
                    PRTLoader.NewParticle<PRT_SHPCGlitchShard>(pos,
                        Main.rand.NextVector2Circular(1.2f, 1.2f),
                        main, Main.rand.NextFloat(0.5f, 1f)).Configure(accent, Main.rand.Next(12, 20));
                }
            }
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile proj = laser.Projectile;
            if (proj.owner != Main.myPlayer) return;
            int surge = (int)proj.ai[2] - 1;

            if (surge == (int)VolatileMutation.Overload) {
                //过载涌动追加回声伤
                int echo = Math.Max((int)(damageDone * LaserOverloadEcho), 1);
                target.SimpleStrikeNPC(echo, hit.HitDirection, false, 0f, hit.DamageType, false, 0f, true);
                if (Main.netMode != NetmodeID.Server && OnScreen(target.Center) && Main.rand.NextBool(3)) {
                    BurstShards(target.Center, surge, 3, 3f);
                }
            }
            else if (surge == (int)VolatileMutation.Unstable) {
                //失稳涌动，满 N 且过门限才引爆
                if (++_laserHitCounter < LaserUnstableEveryHits) return;
                if (Main.GameUpdateCount - _lastLaserBlastTick < LaserUnstableGateFrames) return;
                _laserHitCounter = 0;
                _lastLaserBlastTick = Main.GameUpdateCount;
                SpawnDetonation(proj, target.Center,
                    Math.Max((int)(proj.damage * LaserUnstableDamage), 1), LaserUnstableRadius);
                if (Main.netMode != NetmodeID.Server && OnScreen(target.Center)) {
                    BurstShards(target.Center, surge, 6, 4f);
                }
            }
            //裂变/畸变对光柱无意义，涌动仅视觉
        }

        public override void OnLaserKill(CyberPrismLaserProj laser) {
            //熄灭重置涌动
            _surgeMutation = -1;
            _surgeTimer = 0;
            _surgeCarry = 0f;
            _laserHitCounter = 0;
        }

        //维护

        public override void OnPlayerUpdate(Player player) {
            //周期 purge 陈旧条目兜底
            if (++_purgeTimer < 300) return;
            _purgeTimer = 0;
            if (_states.Count == 0) return;
            int beamType = ModContent.ProjectileType<CyberTraceBeamProj>();
            List<int> dead = null;
            foreach (int key in _states.Keys) {
                Projectile p = Main.projectile[key];
                if (!p.active || p.type != beamType) {
                    (dead ??= new List<int>()).Add(key);
                }
            }
            if (dead == null) return;
            foreach (int key in dead) {
                _states.Remove(key);
            }
        }

        //工具

        private static int RollMutation() {
            float r = Main.rand.NextFloat() * (FissionWeight + OverloadWeight + UnstableWeight + AberrantWeight);
            if ((r -= FissionWeight) < 0f) return (int)VolatileMutation.Fission;
            if ((r -= OverloadWeight) < 0f) return (int)VolatileMutation.Overload;
            if ((r -= UnstableWeight) < 0f) return (int)VolatileMutation.Unstable;
            return (int)VolatileMutation.Aberrant;
        }

        private static float Hash01(float x) {
            float v = MathF.Sin(x) * 43758.5453f;
            return v - MathF.Floor(v);
        }

        private static bool OnScreen(Vector2 worldPos)
            => VaultUtils.IsPointOnScreen(worldPos - Main.screenPosition, 200);

        //覆层并存上限，防 Trail 堆积，超出仅无缎带
        private const int MaxOverlays = 24;

        /// <summary>生成故障覆层，ai0=变异索引 ai1=宿主identity</summary>
        private static void SpawnOverlay(Projectile host, BeamState st) {
            Player owner = Main.player[host.owner];
            if (owner == null
                || owner.ownedProjectileCounts[ModContent.ProjectileType<SHPCVolatileGlitchProj>()] >= MaxOverlays) {
                return;
            }
            int idx = Projectile.NewProjectile(host.GetSource_FromThis(),
                host.Center, Vector2.Zero,
                ModContent.ProjectileType<SHPCVolatileGlitchProj>(),
                0, 0f, host.owner,
                ai0: st.Mutation, ai1: host.identity);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                st.OverlayIdx = idx;
            }
        }

        /// <summary>事件尖峰传入覆层，校验槽未复用</summary>
        private static void PulseOverlay(BeamState st, Projectile host, float amount) {
            if (st.OverlayIdx < 0 || st.OverlayIdx >= Main.maxProjectiles) return;
            Projectile p = Main.projectile[st.OverlayIdx];
            if (p.active && p.ModProjectile is SHPCVolatileGlitchProj overlay
                && overlay.HostIdentity == host.identity) {
                overlay.PulseGlitch(amount);
            }
        }

        /// <summary>roll 瞬间出生反馈，两枚变异色碎条</summary>
        private static void RollFeedback(Projectile proj, int mutation) {
            if (Main.netMode == NetmodeID.Server || !OnScreen(proj.Center)) return;
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_SHPCGlitchShard>(proj.Center + Main.rand.NextVector2Circular(6f, 6f),
                    proj.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(Main.rand.NextFloat(-0.7f, 0.7f)) * Main.rand.NextFloat(1f, 3f),
                    MutMain[mutation], Main.rand.NextFloat(0.5f, 0.9f))
                    .Configure(MutAccent[mutation], Main.rand.Next(10, 18));
            }
        }

        /// <summary>变异色故障碎条爆发，调用方判屏内与端</summary>
        private static void BurstShards(Vector2 pos, int mutation, int count, float speed) {
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_SHPCGlitchShard>(pos,
                    Main.rand.NextVector2CircularEdge(speed, speed) * Main.rand.NextFloat(0.4f, 1f),
                    MutMain[mutation], Main.rand.NextFloat(0.7f, 1.2f))
                    .Configure(MutAccent[mutation], Main.rand.Next(14, 26));
            }
        }

        /// <summary>命中点起爆，CyberDetonationProj，ai2 覆写半径走生成包同步</summary>
        private static void SpawnDetonation(Projectile source, Vector2 center, int dmg, float radius) {
            Projectile.NewProjectile(source.GetSource_FromThis(),
                center, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                dmg, 0f, source.owner,
                ai0: 0f, ai1: 0f, ai2: radius);
        }
    }

    /// <summary>
    /// 混沌故障覆层，跟宿主记轨迹，SHPCModVolatile.fx 画 glitch 缎带；
    /// 宿主消亡原地碎解；ai[0]=变异 0..3，ai[1]=宿主 identity
    /// </summary>
    internal sealed class SHPCVolatileGlitchProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int RibbonCap = 14;       //缎带顶点上限 ≈550px
        private const float MinSpacing = 12f;   //历史点最小间距
        private const int DissolveTicks = 14;   //宿主死后碎解帧

        private int Mutation => Math.Clamp((int)Projectile.ai[0], 0, 3);
        internal int HostIdentity => (int)Projectile.ai[1];

        private Vector2[] history;              //history[0]=最新
        private int historyCount;
        private Vector2[] drawBuffer;
        private Trail trail;
        private int cachedHost = -1;
        private float seed;
        private float glitchSpike;              //事件故障尖峰
        private int dissolve = -1;              //>=0 碎解倒计时
        private float fadeIn;

        /// <summary>事件瞬间拉高故障强度</summary>
        public void PulseGlitch(float amount) => glitchSpike = MathF.Max(glitchSpike, amount);

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>按 identity 定位宿主，缓存索引</summary>
        private CyberTraceBeamProj FindHost() {
            int beamType = ModContent.ProjectileType<CyberTraceBeamProj>();
            if (cachedHost >= 0 && cachedHost < Main.maxProjectiles) {
                Projectile p = Main.projectile[cachedHost];
                if (p.active && p.type == beamType && p.owner == Projectile.owner
                    && p.identity == HostIdentity) {
                    return p.ModProjectile as CyberTraceBeamProj;
                }
                cachedHost = -1;
            }
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == beamType && p.owner == Projectile.owner
                    && p.identity == HostIdentity) {
                    cachedHost = i;
                    return p.ModProjectile as CyberTraceBeamProj;
                }
            }
            return null;
        }

        public override void AI() {
            if (seed == 0f) {
                //identity 派生种子，各端一致
                seed = Projectile.identity % 97 * 0.211f + 1f;
            }

            CyberTraceBeamProj host = FindHost();
            if (host == null) {
                //宿主消亡，冻结碎解
                if (dissolve < 0) dissolve = DissolveTicks;
                if (--dissolve <= 0) {
                    Projectile.Kill();
                    return;
                }
            }
            else {
                Projectile.timeLeft = 60;
                Projectile.Center = host.Projectile.Center;
                fadeIn = MathF.Min(fadeIn + 0.14f, 1f);
                PushHistory(host.Projectile.Center);

                //变异色微光
                Lighting.AddLight(Projectile.Center,
                    VolatileFrameModule.MutMain[Mutation].ToVector3() * 0.28f * fadeIn);

                //沿缎带逸散碎条，客户端屏内节流
                if (Main.netMode != NetmodeID.Server && historyCount > 1 && Main.rand.NextBool(4)) {
                    Vector2 pos = history[Main.rand.Next(Math.Min(historyCount, RibbonCap - 1))];
                    if (VaultUtils.IsPointOnScreen(pos - Main.screenPosition, 150)) {
                        PRTLoader.NewParticle<PRT_SHPCGlitchShard>(pos,
                            Main.rand.NextVector2Circular(1.4f, 1.4f),
                            VolatileFrameModule.MutMain[Mutation], Main.rand.NextFloat(0.4f, 0.9f))
                            .Configure(VolatileFrameModule.MutAccent[Mutation], Main.rand.Next(10, 20));
                    }
                }
            }

            glitchSpike *= 0.86f;
            if (glitchSpike < 0.02f) glitchSpike = 0f;
        }

        private void PushHistory(Vector2 center) {
            history ??= new Vector2[RibbonCap];
            if (historyCount == 0) {
                history[0] = center;
                historyCount = 1;
                return;
            }
            if (Vector2.DistanceSquared(center, history[0]) < MinSpacing * MinSpacing) return;
            int copyLen = Math.Min(historyCount, RibbonCap - 1);
            Array.Copy(history, 0, history, 1, copyLen);
            history[0] = center;
            if (historyCount < RibbonCap) historyCount++;
        }

        private float CurrentFade() {
            float fade = fadeIn * 0.9f;
            if (dissolve >= 0) fade *= dissolve / (float)DissolveTicks;
            return fade;
        }

        private float WidthFunction(float progress) {
            //头快起尾收零，压缩防断尾
            float validRatio = MathF.Max((float)Math.Min(historyCount + 1, RibbonCap) / RibbonCap, 0.1f);
            float t = MathHelper.Clamp(progress / validRatio, 0f, 1f);
            float noseRise = MathF.Sin(MathF.Min(t / 0.10f, 1f) * MathHelper.PiOver2);
            float tailTaper = 1f - t * t;
            return MathF.Max(noseRise * tailTaper, 0f) * (20f + glitchSpike * 12f);
        }

        private Color ColorFunction(Vector2 _) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            float fade = CurrentFade();
            if (history == null || historyCount < 2 || fade < 0.02f) return;

            Effect shader = EffectLoader.SHPCModVolatile?.Value;
            if (shader == null) return;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (noise == null) return;

            //组装顶点，头=当前位置，后取历史
            drawBuffer ??= new Vector2[RibbonCap];
            drawBuffer[0] = Projectile.Center;
            for (int i = 1; i < RibbonCap; i++) {
                int histIdx = i - 1;
                drawBuffer[i] = histIdx < historyCount ? history[histIdx] : drawBuffer[i - 1];
            }

            trail ??= new Trail(drawBuffer, WidthFunction, ColorFunction);
            trail.TrailPositions = drawBuffer;

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.045f);
            shader.Parameters["uSeed"]?.SetValue(seed);
            shader.Parameters["fadeAlpha"]?.SetValue(fade);
            shader.Parameters["uGlitch"]?.SetValue(MathHelper.Clamp(glitchSpike + (dissolve >= 0 ? 0.6f : 0f), 0f, 1f));
            shader.Parameters["baseColor"]?.SetValue(VolatileFrameModule.MutMain[Mutation].ToVector3());
            shader.Parameters["accentColor"]?.SetValue(VolatileFrameModule.MutAccent[Mutation].ToVector3());

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            //噪声走 s1 寄存器约定，Apply 前绑定
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            device.BlendState = BlendState.Additive;
            trail.DrawTrail(shader);
            device.BlendState = BlendState.AlphaBlend;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            float fade = CurrentFade();
            if (fade < 0.02f) return;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;

            //真 Additive 批源因子=SourceAlpha，A=0 整层不显示，A 必须随强度走
            Color main = VolatileFrameModule.MutMain[Mutation];
            Color accent = VolatileFrameModule.MutAccent[Mutation];
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = glow.Size() * 0.5f;

            //RGB 分离头部光晕
            float chrom = 2.5f + glitchSpike * 4f;
            spriteBatch.Draw(glow, drawPos, null, accent * (fade * 0.35f), 0f, origin, 0.9f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos - new Vector2(chrom, 0f), null,
                (main with { G = 30, B = 30 }) * (fade * 0.25f), 0f, origin, 0.55f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos + new Vector2(chrom, 0f), null,
                (main with { R = 30 }) * (fade * 0.25f), 0f, origin, 0.55f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos, null, main * (fade * 0.5f), 0f, origin, 0.42f, SpriteEffects.None, 0f);

            //事件尖峰四芒星
            if (glitchSpike > 0.35f) {
                Texture2D star = CWRAsset.StarTexture_White?.Value;
                if (star != null) {
                    float flash = (glitchSpike - 0.35f) / 0.65f;
                    spriteBatch.Draw(star, drawPos, null, main * (flash * 0.7f),
                        (float)Main.timeForVisualEffects * 0.05f + seed,
                        star.Size() * 0.5f, 0.10f + flash * 0.15f, SpriteEffects.None, 0f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
