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
    /// <summary>混沌变异种类，索引与 <see cref="VolatileFrameModule.MutMain"/> 配色对齐</summary>
    internal enum VolatileMutation
    {
        /// <summary>裂变（青）：中途分裂出两道副光束，原束继续飞行</summary>
        Fission = 0,
        /// <summary>过载（红）：伤害翻倍，弹道狂乱抖动</summary>
        Overload = 1,
        /// <summary>失稳（橙）：首次命中处引爆小范围爆炸</summary>
        Unstable = 2,
        /// <summary>畸变（紫）：飞行中急转折向最近敌人（折向而非传送）</summary>
        Aberrant = 3,
    }

    /// <summary>
    /// 不稳定机匣：每道主光束诞生时在所有者端随机 roll 一种混沌变异，
    /// 结果写入弹幕 ai[2]（0=未roll，1..4=变异+1）跨端一致；
    /// 每束由专属故障覆层弹幕标示变异身份色；激光模式改为周期涌动
    /// </summary>
    internal sealed class VolatileFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //不稳定毒黄（保留旧识别色身份）
        public override Color TintColor => new(220, 255, 40);

        //═════════════ 可调参数 ═════════════

        //变异权重（归一化前），过载最强故权重最低
        private const float FissionWeight = 0.30f;
        private const float OverloadWeight = 0.18f;
        private const float UnstableWeight = 0.32f;
        private const float AberrantWeight = 0.20f;

        //裂变：触发时机（AI调用数，光束每刻 3 次、每次前进约14px；54≈750px 处分裂）、张角、副束伤害与速度
        private const int FissionDelayCalls = 54;
        private const float FissionForkAngle = 0.36f;
        private const float FissionChildDamage = 0.45f;
        private const float FissionChildSpeed = 14f;

        //过载：伤害倍率（任务锚点：翻倍）、每次AI调用的抖动步长、回中混合
        private const float OverloadDamageMul = 2f;
        private const float OverloadJitterStep = 0.085f;
        private const float OverloadReturnBlend = 0.05f;

        //失稳：爆炸半径与伤害占比，仅光束首个命中触发（蠕虫不逐节连爆）
        private const float UnstableRadius = 92f;
        private const float UnstableDamage = 0.55f;

        //畸变：折向延迟（30调用≈420px，单体Boss战内可兑现）、失败重试间隔、索敌半径
        private const int AberrantDelayCalls = 30;
        private const int AberrantRetryCalls = 15;
        private const float AberrantSnapRange = 760f;

        //激光涌动：窗口帧数区间与两种可兑现涌动的强度
        private const int LaserSurgeMin = 60;
        private const int LaserSurgeMax = 110;
        private const float LaserOverloadEcho = 0.30f;
        private const int LaserUnstableEveryHits = 5;
        //失稳涌动起爆的实时最小间隔（帧），蠕虫多节高频命中不会连环起爆
        private const int LaserUnstableGateFrames = 45;
        private const float LaserUnstableRadius = 80f;
        private const float LaserUnstableDamage = 0.5f;

        //═════════════ 变异专属配色（裂变青/过载红/失稳橙/畸变紫） ═════════════

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

        //═════════════ 每束状态（拥有者端） ═════════════

        private sealed class BeamState
        {
            public int Mutation = -1;
            public int AICalls;
            public bool ActionDone;
            /// <summary>过载回中基准角</summary>
            public float OriginalAngle;
            /// <summary>故障覆层弹幕索引，事件尖峰直达</summary>
            public int OverlayIdx = -1;
        }

        //whoAmI → 状态；OnBeamKill 移除 + OnPlayerUpdate 周期purge兜底（改件卸下期间钩子停摆）
        private readonly Dictionary<int, BeamState> _states = new();
        private int _purgeTimer;

        //激光涌动状态：激光是玩家单例弹幕，模块 Item 实例字段即 per-玩家
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

        //═════════════ 光束：roll 与逐帧变异行为 ═════════════

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived) return;                 //全局约定：派生束不回喂机制
            Projectile proj = beam.Projectile;
            if (proj.owner != Main.myPlayer) return;    //roll 与机制全在所有者端

            if (!_states.TryGetValue(proj.whoAmI, out BeamState st)) {
                st = new BeamState();
                _states[proj.whoAmI] = st;
            }

            //首次：roll 变异写入 ai[2] 同步；一次性副作用（伤害翻倍/覆层/反馈）
            //仅在本次真正执行了 roll 时结算，防状态字典重建（改件卸装/purge）后重复施加
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

        /// <summary>裂变：到点分裂两道派生副束，原束不减不停</summary>
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
                    ai0: 0); //副束固定等离子青主题，与裂变身份色一致
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

        /// <summary>过载：随机游走抖动 + 缓回原始瞄准角，狂野但大致向前</summary>
        private static void TickOverload(CyberTraceBeamProj beam, BeamState st) {
            float angle = beam.FlightDirection.ToRotation();
            angle += Main.rand.NextFloat(-OverloadJitterStep, OverloadJitterStep);
            angle = angle.AngleLerp(st.OriginalAngle, OverloadReturnBlend);
            beam.SetFlightDirection(angle.ToRotationVector2());
        }

        /// <summary>畸变：延迟后急转折向最近敌人，一次性；无敌可折则周期重试</summary>
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
            //失稳：仅光束首个命中起爆一次（OnHitNPC 时 numHits 尚未自增），蠕虫多节不逐节连爆
            if (proj.numHits > 0) return;

            SpawnDetonation(proj, target.Center,
                Math.Max((int)(proj.damage * UnstableDamage), 1), UnstableRadius);
            if (Main.netMode != NetmodeID.Server && OnScreen(target.Center)) {
                BurstShards(target.Center, (int)VolatileMutation.Unstable, 9, 5f);
            }
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            if (beam.IsDerived) return;
            //无死亡演出（SuppressDeathEffects 光束同样只做回收）；覆层自会检测宿主消亡进入碎解
            if (_states.Remove(beam.Projectile.whoAmI, out BeamState st)) {
                PulseOverlay(st, beam.Projectile, 0.8f);
            }
        }

        //═════════════ 激光：周期随机涌动 ═════════════

        public override void OnLaserAI(CyberPrismLaserProj laser) {
            Projectile proj = laser.Projectile;

            //拥有者端推进涌动节拍并 roll，结果写 ai[2] 跨端一致（0=未roll）
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
                            Volume = 0.2f, Pitch = -0.1f + _surgeMutation * 0.18f
                        }, proj.Center);
                    }
                }
                else {
                    TickDown(ref _surgeTimer, ref _surgeCarry);
                }
            }

            int surge = (int)proj.ai[2] - 1;
            if (surge < 0 || surge > 3) return;

            //主题覆写：涌动身份色 + 阶梯色相跳变（量化时间伪随机，各端趋同）
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

            //沿激光随机位置的故障碎条（激光全长恒为最大射程，取近段安全）
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
                //过载涌动：命中追加回声伤（SimpleStrikeNPC 自带同步）
                int echo = Math.Max((int)(damageDone * LaserOverloadEcho), 1);
                target.SimpleStrikeNPC(echo, hit.HitDirection, false, 0f, hit.DamageType, false, 0f, true);
                if (Main.netMode != NetmodeID.Server && OnScreen(target.Center) && Main.rand.NextBool(3)) {
                    BurstShards(target.Center, surge, 3, 3f);
                }
            }
            else if (surge == (int)VolatileMutation.Unstable) {
                //失稳涌动：计数满 N 且距上次起爆超过实时门限才引爆，蠕虫多节高频命中不连环
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
            //裂变/畸变属弹道类变异，对锁向光柱无意义，涌动期仅提供视觉主题
        }

        public override void OnLaserKill(CyberPrismLaserProj laser) {
            //熄灭重置涌动，下次开火重新 roll
            _surgeMutation = -1;
            _surgeTimer = 0;
            _surgeCarry = 0f;
            _laserHitCounter = 0;
        }

        //═════════════ 维护 ═════════════

        public override void OnPlayerUpdate(Player player) {
            //周期purge：光束槽位复用/改件曾被卸下时的陈旧条目兜底
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

        //═════════════ 工具 ═════════════

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

        /// <summary>生成故障覆层（拥有者端）；ai0=变异索引 ai1=宿主identity（跨端稳定）</summary>
        private static void SpawnOverlay(Projectile host, BeamState st) {
            int idx = Projectile.NewProjectile(host.GetSource_FromThis(),
                host.Center, Vector2.Zero,
                ModContent.ProjectileType<SHPCVolatileGlitchProj>(),
                0, 0f, host.owner,
                ai0: st.Mutation, ai1: host.identity);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                st.OverlayIdx = idx;
            }
        }

        /// <summary>事件尖峰传入覆层（校验槽位未被复用）</summary>
        private static void PulseOverlay(BeamState st, Projectile host, float amount) {
            if (st.OverlayIdx < 0 || st.OverlayIdx >= Main.maxProjectiles) return;
            Projectile p = Main.projectile[st.OverlayIdx];
            if (p.active && p.ModProjectile is SHPCVolatileGlitchProj overlay
                && overlay.HostIdentity == host.identity) {
                overlay.PulseGlitch(amount);
            }
        }

        /// <summary>roll 瞬间的出生反馈：两枚变异色碎条（束在枪口，天然在屏内）</summary>
        private static void RollFeedback(Projectile proj, int mutation) {
            if (Main.netMode == NetmodeID.Server || !OnScreen(proj.Center)) return;
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_SHPCGlitchShard>(proj.Center + Main.rand.NextVector2Circular(6f, 6f),
                    proj.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(Main.rand.NextFloat(-0.7f, 0.7f)) * Main.rand.NextFloat(1f, 3f),
                    MutMain[mutation], Main.rand.NextFloat(0.5f, 0.9f))
                    .Configure(MutAccent[mutation], Main.rand.Next(10, 18));
            }
        }

        /// <summary>变异色故障碎条爆发（调用方负责屏内与端判定）</summary>
        private static void BurstShards(Vector2 pos, int mutation, int count, float speed) {
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_SHPCGlitchShard>(pos,
                    Main.rand.NextVector2CircularEdge(speed, speed) * Main.rand.NextFloat(0.4f, 1f),
                    MutMain[mutation], Main.rand.NextFloat(0.7f, 1.2f))
                    .Configure(MutAccent[mutation], Main.rand.Next(14, 26));
            }
        }

        /// <summary>命中点起爆：复用 CyberDetonationProj，localAI[2] 覆写半径</summary>
        private static void SpawnDetonation(Projectile source, Vector2 center, int dmg, float radius) {
            int idx = Projectile.NewProjectile(source.GetSource_FromThis(),
                center, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                dmg, 0f, source.owner,
                ai0: 0f, ai1: 0f);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].localAI[2] = radius;
            }
        }
    }

    /// <summary>
    /// 混沌变异故障覆层：跟随宿主光束记录轨迹，用 SHPCModVolatile.fx 画 glitch 缎带
    /// （色相跳变/行撕裂/坏块/RGB分离），变异身份色即光束身份；宿主消亡后原地碎解。
    /// ai[0]=变异索引 0..3，ai[1]=宿主 identity（生成即定，跨端一致）
    /// </summary>
    internal sealed class SHPCVolatileGlitchProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int RibbonCap = 14;       //缎带顶点上限（≈550px 尾迹）
        private const float MinSpacing = 12f;   //历史点最小间距
        private const int DissolveTicks = 14;   //宿主死后碎解帧数

        private int Mutation => Math.Clamp((int)Projectile.ai[0], 0, 3);
        internal int HostIdentity => (int)Projectile.ai[1];

        private Vector2[] history;              //history[0]=最新历史点
        private int historyCount;
        private Vector2[] drawBuffer;
        private Trail trail;
        private int cachedHost = -1;
        private float seed;
        private float glitchSpike;              //事件故障尖峰，指数衰减
        private int dissolve = -1;              //>=0 碎解倒计时
        private float fadeIn;

        /// <summary>模块钩子调用：事件瞬间拉高故障强度</summary>
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

        /// <summary>按 identity 定位宿主光束，缓存索引避免每帧全扫</summary>
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
                //identity 派生种子：各端一致，错开每束跳变节奏
                seed = Projectile.identity % 97 * 0.211f + 1f;
            }

            CyberTraceBeamProj host = FindHost();
            if (host == null) {
                //宿主消亡：轨迹冻结原地碎解
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

                //变异色微光标示身份
                Lighting.AddLight(Projectile.Center,
                    VolatileFrameModule.MutMain[Mutation].ToVector3() * 0.28f * fadeIn);

                //沿缎带随机点逸散故障碎条（仅客户端、屏内、节流）
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
            //头部快起、尾部收零；有效顶点区间压缩防断尾切口
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

            //组装绘制顶点：头部=当前位置，其后取历史，末尾复读补满
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
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            trail.DrawTrail(shader);
            device.BlendState = BlendState.AlphaBlend;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            float fade = CurrentFade();
            if (fade < 0.02f) return;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;

            Color main = VolatileFrameModule.MutMain[Mutation] with { A = 0 };
            Color accent = VolatileFrameModule.MutAccent[Mutation] with { A = 0 };
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = glow.Size() * 0.5f;

            //RGB 分离头部光晕：主色本位 + 左右偏移的红/蓝残影
            float chrom = 2.5f + glitchSpike * 4f;
            spriteBatch.Draw(glow, drawPos, null, accent * (fade * 0.35f), 0f, origin, 0.9f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos - new Vector2(chrom, 0f), null,
                (main with { G = 30, B = 30 }) * (fade * 0.25f), 0f, origin, 0.55f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos + new Vector2(chrom, 0f), null,
                (main with { R = 30 }) * (fade * 0.25f), 0f, origin, 0.55f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos, null, main * (fade * 0.5f), 0f, origin, 0.42f, SpriteEffects.None, 0f);

            //事件尖峰：四芒星闪光强调变异瞬间
            if (glitchSpike > 0.35f) {
                Texture2D star = CWRAsset.StarTexture_White?.Value;
                if (star != null) {
                    float flash = (glitchSpike - 0.35f) / 0.65f;
                    spriteBatch.Draw(star, drawPos, null, main * (flash * 0.8f),
                        (float)Main.timeForVisualEffects * 0.05f + seed,
                        star.Size() * 0.5f, 0.05f + flash * 0.1f, SpriteEffects.None, 0f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
