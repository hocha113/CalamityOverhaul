using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.Actors;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>死亡演出阶段枚举</summary>
    internal enum PrimeDeathPhase
    {
        /// <summary>假死沉寂</summary>
        FakeDeath,
        /// <summary>再生双钳</summary>
        Summon,
        /// <summary>扑抓</summary>
        Lunge,
        /// <summary>拖拽举起</summary>
        Drag,
        /// <summary>怒吼蓄力</summary>
        Roar,
        /// <summary>终爆真死</summary>
        Finale,
        /// <summary>演出结束</summary>
        Done
    }

    /// <summary>死亡演出，ai[2]+ai[0]==4，钳Actor纯函数，被抓玩家本地权威</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.Death, typeof(PrimeStateContext))]
    internal class PrimeDeathState : PrimeStateBase
    {
        public override string StateName => "Death";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.Death;

        #region 时间线常量

        /// <summary>举起时距头下方距离</summary>
        internal const float DeathLiftDistance = 210f;

        //时间线帧，阶段截止
        internal const int PhaseFakeDeathEnd = 140; //假死爆炸(0-80) + 死寂(80-140)
        internal const int PhaseSummonEnd = 195;    //嗡鸣再生钳子(55f)
        internal const int PhaseLungeEnd = 240;     //双钳迅猛扑抓(45f，最快)
        internal const int PhaseDragEnd = 305;      //拖拽举起(65f)
        internal const int PhaseRoarEnd = 380;      //怒吼高潮定格(75f，最长)
        internal const int PhaseFinaleEnd = 450;    //终爆 + 余波 + 尘埃落定(70f)

        /// <summary>计时推阶段</summary>
        internal static PrimeDeathPhase GetDeathPhase(int t) {
            if (t < PhaseFakeDeathEnd) {
                return PrimeDeathPhase.FakeDeath;
            }
            if (t < PhaseSummonEnd) {
                return PrimeDeathPhase.Summon;
            }
            if (t < PhaseLungeEnd) {
                return PrimeDeathPhase.Lunge;
            }
            if (t < PhaseDragEnd) {
                return PrimeDeathPhase.Drag;
            }
            if (t < PhaseRoarEnd) {
                return PrimeDeathPhase.Roar;
            }
            if (t < PhaseFinaleEnd) {
                return PrimeDeathPhase.Finale;
            }
            return PrimeDeathPhase.Done;
        }

        #endregion

        #region 状态字段

        private bool clawsSpawned;
        private bool fakeDeathJolted;  //假死死寂末的"惊醒"预兆是否已触发
        private float headWobble;      //头部故障摇摆角（叠加在基础朝向之上）
        private float headWobbleVel;   //摇摆角速度

        //殉爆配色橙红→暗红
        private static readonly Color DeathWarmA = new Color(255, 130, 60);
        private static readonly Color DeathWarmB = new Color(200, 40, 30);

        #endregion

        public override void OnEnter(PrimeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;

            clawsSpawned = false;
            fakeDeathJolted = false;
            headWobble = 0f;
            headWobbleVel = 0f;

            npc.ai[PrimeAiSlots.HeadPhase] = PrimePhase.DeathShow;
            npc.velocity *= 0.3f;
            context.DeathTimer = 0;
            context.DeathPhase = PrimeDeathPhase.FakeDeath;
            HeadPrimeAI.ActivePerformanceHead = npc.whoAmI;

            //锁被抓目标
            if (npc.target >= 0 && npc.target < Main.maxPlayers
                && Main.player[npc.target].active && !Main.player[npc.target].dead) {
                context.DeathTargetIndex = npc.target;
            }
            else if (npc.Center.TryFindClosestPlayer(out var p)) {
                context.DeathTargetIndex = p.whoAmI;
            }

            //清debuff
            for (int i = 0; i < npc.buffType.Length; i++) {
                npc.buffTime[i] = 0;
            }

            if (!VaultUtils.isServer) {
                //过载警报
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.8f, Volume = 0.9f }, npc.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.6f, Volume = 0.7f }, npc.Center);
            }
        }

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;

            //锁血急停
            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }
            npc.velocity *= 0.85f;
            if (npc.velocity.Length() < 0.1f) {
                npc.velocity = Vector2.Zero;
            }
            context.FrameMode = 0;

            PrimeDeathPhase phase = GetDeathPhase(Timer);
            context.DeathPhase = phase;
            context.DeathTimer = Timer;

            UpdateHeadRotation(context, phase);

            switch (phase) {
                case PrimeDeathPhase.FakeDeath:
                    UpdateFakeDeath(context);
                    break;
                case PrimeDeathPhase.Summon:
                    UpdateSummon(context);
                    break;
                case PrimeDeathPhase.Lunge:
                    UpdateLunge(context);
                    break;
                case PrimeDeathPhase.Drag:
                    UpdateDrag(context);
                    break;
                case PrimeDeathPhase.Roar:
                    UpdateRoar(context);
                    break;
                case PrimeDeathPhase.Finale:
                    UpdateFinale(context);
                    break;
            }

            Timer++;

            //落幕
            if (Timer >= PhaseFinaleEnd) {
                context.DeathPerformanceFinished = true;
                if (HeadPrimeAI.ActivePerformanceHead == npc.whoAmI) {
                    HeadPrimeAI.ActivePerformanceHead = -1;
                }
                //服务端放行真死
                if (!VaultUtils.isClient) {
                    npc.dontTakeDamage = false;
                    npc.life = 0;
                    npc.HitEffect();
                    npc.checkDead();
                    npc.netUpdate = true;
                }
            }
            return null;
        }

        public override void OnExit(PrimeStateContext context) {
            base.OnExit(context);
            if (HeadPrimeAI.ActivePerformanceHead == context.Npc.whoAmI) {
                HeadPrimeAI.ActivePerformanceHead = -1;
            }
        }

        /// <summary>竖立阻尼摇摆</summary>
        private void UpdateHeadRotation(PrimeStateContext context, PrimeDeathPhase phase) {
            NPC npc = context.Npc;

            //假死爆炸期摇摆冲量
            if (phase == PrimeDeathPhase.FakeDeath && Timer < 80 && Timer % 12 == 0) {
                headWobbleVel += (Timer % 24 == 0) ? 0.05f : -0.05f;
            }

            //摇摆阻尼
            headWobble += headWobbleVel;
            headWobbleVel *= 0.9f;
            headWobble *= 0.92f;

            //先回竖再叠摇摆
            float current = (npc.rotation - headWobble).AngleLerp(0f, 0.12f);
            npc.rotation = current + headWobble;
        }

        #region 各阶段演出

        /// <summary>假死</summary>
        private void UpdateFakeDeath(PrimeStateContext context) {
            if (VaultUtils.isServer) {
                return;
            }
            NPC npc = context.Npc;

            if (Timer < 80) {
                //连环爆
                if (Timer % 11 == 0) {
                    Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.45f, npc.height * 0.45f);
                    SpawnMechBlast(npc, pos, Main.rand.NextFloat(0.9f, 1.5f), false);
                    PrimeDeathPerformancePlayer.RequestShake(5f, 12);
                }
                //接缝火花
                if (Timer % 4 == 0) {
                    SpawnSparks(npc, npc.Center, 6, 6f);
                }
                Lighting.AddLight(npc.Center, DeathWarmA.ToVector3() * 0.8f);
            }
            else {
                //死寂
                if (Timer % 10 == 0) {
                    SpawnSparks(npc, npc.Center, 2, 3f);
                }
                if (Timer % 18 == 0) {
                    Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f);
                    PRTLoader.NewParticle<PRT_Smoke>(pos, -Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.5f),
                        Color.Lerp(new Color(60, 56, 54), new Color(22, 20, 20), Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.7f, 1.1f)).Configure(Main.rand.Next(50, 80), 0.7f, Main.rand.NextFloat(-0.04f, 0.04f));
                }

                //惊醒预兆
                if (Timer == PhaseFakeDeathEnd - 14 && !fakeDeathJolted) {
                    fakeDeathJolted = true;
                    headWobbleVel += 0.2f;
                    SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -1f, Volume = 0.65f }, npc.Center);
                    PrimeDeathPerformancePlayer.RequestShake(7f, 14);
                }
            }
        }

        /// <summary>再生钳</summary>
        private void UpdateSummon(PrimeStateContext context) {
            NPC npc = context.Npc;

            //本地生成钳Actor
            if (!clawsSpawned) {
                clawsSpawned = true;
                TrySpawnDeathClaws(npc);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.9f, Volume = 1.1f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Item92 with { Pitch = -0.5f, Volume = 0.8f }, npc.Center);
                    PrimeDeathPerformancePlayer.RequestShake(8f, 30);
                }
            }

            if (VaultUtils.isServer) {
                return;
            }

            //蓄力电弧
            if (Timer % 3 == 0) {
                SpawnSparks(npc, npc.Center, 8, 8f);
            }
            Lighting.AddLight(npc.Center, DeathWarmA.ToVector3() * (1f + (Timer - PhaseFakeDeathEnd) / 60f));
        }

        /// <summary>扑抓</summary>
        private void UpdateLunge(PrimeStateContext context) {
            NPC npc = context.Npc;

            if (Timer == PhaseSummonEnd && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f, Volume = 1f }, npc.Center);
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.8f }, npc.Center);
            }

            if (VaultUtils.isServer) {
                return;
            }
            if (Timer % 6 == 0) {
                SpawnSparks(npc, npc.Center, 4, 5f);
            }
        }

        /// <summary>拖拽举起</summary>
        private void UpdateDrag(PrimeStateContext context) {
            NPC npc = context.Npc;
            Player target = GetDeathTarget(context);

            if (Timer == PhaseLungeEnd && !VaultUtils.isServer) {
                //抓住撞击
                Vector2 grabPos = target?.Center ?? npc.Center;
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.4f, Volume = 1.1f }, grabPos);
                SpawnMechBlast(npc, grabPos, 1.3f, false);
                PrimeDeathPerformancePlayer.RequestShake(10f, 20);
            }

            if (VaultUtils.isServer) {
                return;
            }

            //被夹火花
            if (target != null && Timer % 4 == 0) {
                SpawnSparks(npc, target.Center, 4, 4.5f);
            }
        }

        /// <summary>怒吼蓄力</summary>
        private void UpdateRoar(PrimeStateContext context) {
            NPC npc = context.Npc;

            if (Timer == PhaseDragEnd && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.2f }, npc.Center);
                PrimeDeathPerformancePlayer.RequestShake(14f, PhaseRoarEnd - PhaseDragEnd);
            }

            if (VaultUtils.isServer) {
                return;
            }
            if (Timer % 2 == 0) {
                SpawnSparks(npc, npc.Center, 6, 7f);
            }
            Lighting.AddLight(npc.Center, new Color(255, 60, 30).ToVector3() * 1.8f);
        }

        /// <summary>终爆</summary>
        private void UpdateFinale(PrimeStateContext context) {
            NPC npc = context.Npc;

            if (Timer == PhaseRoarEnd && !VaultUtils.isServer) {
                SpawnFinaleBlast(context);
            }

            if (VaultUtils.isServer) {
                return;
            }

            int into = Timer - PhaseRoarEnd; //0 → 70
            if (into < 50) {
                //终爆余波
                int interval = into < 22 ? 4 : 7;
                if (into % interval == 0) {
                    Vector2 pos = npc.Center + Main.rand.NextVector2Circular(150f, 150f);
                    SpawnMechBlast(npc, pos, Main.rand.NextFloat(1f, 2.2f), false);
                }
            }
            else {
                //残烟收尾
                if (into % 6 == 0) {
                    Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f);
                    PRTLoader.NewParticle<PRT_Smoke>(pos, -Vector2.UnitY * Main.rand.NextFloat(0.8f, 2f),
                        Color.Lerp(new Color(55, 50, 48), new Color(18, 16, 16), Main.rand.NextFloat()),
                        Main.rand.NextFloat(1f, 1.6f)).Configure(Main.rand.Next(60, 90), 0.7f, Main.rand.NextFloat(-0.04f, 0.04f));
                }
            }
        }

        #endregion

        #region 辅助

        private static Player GetDeathTarget(PrimeStateContext context) {
            int index = context.DeathTargetIndex;
            return (index >= 0 && index < Main.maxPlayers) ? Main.player[index] : null;
        }

        /// <summary>生成本地钳Actor</summary>
        private static void TrySpawnDeathClaws(NPC npc) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int side = -1; side <= 1; side += 2) {
                int idx = ActorLoader.NewActor<PrimeDeathClawActor>(npc.Center, Vector2.Zero);
                if (idx >= 0 && idx < ActorLoader.MaxActorCount
                    && ActorLoader.Actors[idx] is PrimeDeathClawActor claw) {
                    claw.Setup(npc.whoAmI, side);
                }
            }
        }

        /// <summary>机械殉爆VFX</summary>
        private static void SpawnMechBlast(NPC npc, Vector2 pos, float scale, bool isFinale) {
            if (VaultUtils.isServer) {
                return;
            }

            Color warm = Color.Lerp(DeathWarmA, DeathWarmB, Main.rand.NextFloat());

            //核心光团
            PRTLoader.NewParticle<PRT_MechExplosion>(pos, Main.rand.NextVector2Circular(1.5f, 1.5f), warm, scale)
                .Configure(Main.rand.Next(26, 38), warm);

            //火花
            int sparkCount = isFinale ? 52 : Main.rand.Next(4, 8);
            for (int i = 0; i < sparkCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(3f, 11f) * (isFinale ? 1.7f : scale);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel,
                    Color.Lerp(warm, Color.LightGoldenrodYellow, Main.rand.NextFloat()),
                    Main.rand.NextFloat(1.0f, 1.8f)).Configure(true, Main.rand.Next(16, 30));
            }

            //余烬
            int emberCount = isFinale ? 26 : 2;
            for (int i = 0; i < emberCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3.5f, 3.5f);
                PRTLoader.NewParticle<PRT_LavaFire>(pos + Main.rand.NextVector2Circular(20f, 20f) * scale, vel,
                    Color.White, Main.rand.NextFloat(0.8f, 1.3f) * scale).SetLifetime(20, 46);
            }

            //浓烟
            int smokeCount = isFinale ? 18 : 2;
            for (int i = 0; i < smokeCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(2f, 2f) - Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.7f);
                PRTLoader.NewParticle<PRT_Smoke>(pos, vel,
                    Color.Lerp(new Color(60, 56, 54), new Color(20, 18, 18), Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.8f, 1.4f) * scale)
                    .Configure(Main.rand.Next(45, 72), 0.7f, Main.rand.NextFloat(-0.05f, 0.05f));
            }

            Lighting.AddLight(pos, warm.ToVector3() * (isFinale ? 3f : 1.1f) * scale);

            //密爆降音概率
            if (isFinale || Main.rand.NextBool(6)) {
                SoundEngine.PlaySound(SoundID.Item14 with {
                    Pitch = isFinale ? -0.5f : Main.rand.NextFloat(-0.2f, 0.35f),
                    Volume = isFinale ? 1f : 0.45f
                }, pos);
            }
        }

        /// <summary>接缝电火花</summary>
        private static void SpawnSparks(NPC npc, Vector2 center, int count, float speed) {
            if (VaultUtils.isServer) {
                return;
            }
            Color color = Color.Lerp(DeathWarmA, Color.LightGoldenrodYellow, 0.3f);
            for (int i = 0; i < count; i++) {
                Vector2 pos = center + Main.rand.NextVector2Circular(npc.width * 0.45f, npc.height * 0.45f);
                Vector2 vel = Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(1f, speed);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, color, Main.rand.NextFloat(0.7f, 1.3f))
                    .Configure(true, Main.rand.Next(12, 22));
            }
        }

        /// <summary>终爆+连锁+震屏</summary>
        private static void SpawnFinaleBlast(PrimeStateContext context) {
            if (VaultUtils.isServer) {
                return;
            }
            NPC npc = context.Npc;

            SpawnMechBlast(npc, npc.Center, 4.2f, true);

            //周身连锁
            for (int i = 0; i < 8; i++) {
                SpawnMechBlast(npc, npc.Center + Main.rand.NextVector2Circular(140f, 140f), Main.rand.NextFloat(1.4f, 2.4f), false);
            }

            //玩家处同步炸
            Player target = GetDeathTarget(context);
            if (target != null && target.active) {
                SpawnMechBlast(npc, target.Center, 2.6f, false);
            }

            PrimeDeathPerformancePlayer.RequestShake(26f, 45);
            PrimeScreenEffects.PushImpactFrame(1f, 30);
        }

        #endregion
    }
}
