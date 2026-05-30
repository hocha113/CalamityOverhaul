using CalamityOverhaul.Content.PRTTypes;
using InnoVault.Actors;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>
    /// 机械骷髅王死亡演出阶段
    /// </summary>
    internal enum PrimeDeathPhase
    {
        /// <summary>假死：头部停摆、身上连环爆炸后陷入沉寂，制造"和前两个机械Boss一样炸完就死"的错觉</summary>
        FakeDeath,
        /// <summary>再生钳子：低沉嗡鸣，头部两侧重新生成两只钳子机械臂</summary>
        Summon,
        /// <summary>扑抓：双钳高速飞向目标玩家</summary>
        Lunge,
        /// <summary>拖拽举起：抓住玩家拖到头部正前方举起</summary>
        Drag,
        /// <summary>怒吼：头部前倾怒吼、钳子夹紧蓄力</summary>
        Roar,
        /// <summary>终爆：猛烈大爆炸，钳子炸碎，真正死亡</summary>
        Finale,
        /// <summary>演出结束</summary>
        Done
    }

    /// <summary>
    /// 机械骷髅王死亡演出——当只剩头部（noArm）且生命值低于 <see cref="DeathTriggerLife"/> 时触发。
    /// <para>流程：假死爆炸（误导） → 再生双钳 → 扑抓玩家 → 拖拽举起 → 怒吼 → 终爆真死。</para>
    /// <para>多人同步策略：演出主状态用原版自动同步的 <c>npc.ai[0] == <see cref="DeathPerformanceMainState"/></c> 标记，
    /// 各端检测到后本地确定性推进计时；钳子由 <see cref="PrimeDeathClawActor"/>（本地视觉 Actor）表现，
    /// 其位置是"阶段+计时+头部位置+目标玩家位置"的纯函数；被抓玩家位置由其本地权威驱动，
    /// 所有粒子/音效/运镜/震动均在客户端本地生成，彻底规避原生钳子手 NPC 的自毁与网络耦合。</para>
    /// </summary>
    internal partial class HeadPrimeAI
    {
        #region 常量与状态

        /// <summary>死亡演出占用的 <c>npc.ai[0]</c> 主状态值（0/1/2/3 已被常规阶段占用）</summary>
        internal const int DeathPerformanceMainState = 4;
        /// <summary>触发死亡演出的生命阈值</summary>
        internal const int DeathTriggerLife = 10;
        /// <summary>玩家被举起时距头部中心的下方距离（骷髅头面朝下方，正好把玩家按在"脸"前）</summary>
        internal const float DeathLiftDistance = 210f;

        //演出时间线（单位：帧，60帧/秒）——各阶段累计结束帧
        internal const int PhaseFakeDeathEnd = 150; //假死爆炸 + 沉寂
        internal const int PhaseSummonEnd = 210;     //嗡鸣再生钳子
        internal const int PhaseLungeEnd = 280;      //双钳扑抓
        internal const int PhaseDragEnd = 370;       //拖拽举起
        internal const int PhaseRoarEnd = 415;       //怒吼蓄力
        internal const int PhaseFinaleEnd = 470;     //终爆死亡

        private int deathTimer;
        private bool deathInitialized;
        private bool clawsSpawned;
        private int deathTargetIndex = -1;

        //殉爆配色（机械骷髅王：橙红 → 暗红，冷酷的金属过载质感）
        private static readonly Color DeathWarmA = new Color(255, 130, 60);
        private static readonly Color DeathWarmB = new Color(200, 40, 30);

        /// <summary>当前正在进行死亡演出的头部 whoAmI（供运镜/玩家锁定快速查询），无则为 -1</summary>
        internal static int ActivePerformanceHead = -1;

        /// <summary>是否正处于死亡演出主状态</summary>
        internal bool InDeathPerformance => npc.ai[0] == DeathPerformanceMainState;
        /// <summary>演出已运行帧数（各端本地推进）</summary>
        internal int DeathTimer => deathTimer;
        /// <summary>锁定的被抓玩家索引</summary>
        internal int DeathTargetIndex => deathTargetIndex;
        /// <summary>玩家被举起的目标世界坐标（头部正下方）</summary>
        internal Vector2 DeathLiftPoint => npc.Center + new Vector2(0f, DeathLiftDistance);
        /// <summary>当前演出阶段</summary>
        internal PrimeDeathPhase CurrentDeathPhase => GetDeathPhase(deathTimer);
        /// <summary>被抓玩家实例，无效时为 null</summary>
        internal Player DeathTargetPlayer =>
            (deathTargetIndex >= 0 && deathTargetIndex < Main.maxPlayers) ? Main.player[deathTargetIndex] : null;

        /// <summary>由演出计时推导当前阶段</summary>
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

        /// <summary>
        /// 死亡演出主驱动。返回 <see langword="true"/> 表示演出已接管 AI（调用方应提前结束本帧常规 AI）。
        /// </summary>
        internal bool UpdateDeathPerformance() {
            //触发检测：只剩头部、处于三阶段、生命见底 → 由服务端/单人端开启演出（经 npc.ai[0] 同步）
            if (npc.ai[0] != DeathPerformanceMainState) {
                if (!VaultUtils.isClient && noArm && npc.ai[0] == 3f && npc.life <= DeathTriggerLife) {
                    npc.ai[0] = DeathPerformanceMainState;
                    npc.netUpdate = true;
                }
                else {
                    return false;
                }
            }

            if (!deathInitialized) {
                InitDeathPerformance();
            }

            //全程锁血、停止接触伤害、急停悬停
            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }
            npc.velocity *= 0.85f;
            if (npc.velocity.Length() < 0.1f) {
                npc.velocity = Vector2.Zero;
            }

            PrimeDeathPhase phase = GetDeathPhase(deathTimer);

            //头部缓缓怒视被抓玩家
            Player target = DeathTargetPlayer;
            if (target != null && target.active) {
                float desiredRot = npc.Center.To(target.Center).ToRotation() - MathHelper.PiOver2;
                npc.rotation = npc.rotation.AngleLerp(desiredRot, phase >= PrimeDeathPhase.Roar ? 0.12f : 0.05f);
            }

            switch (phase) {
                case PrimeDeathPhase.FakeDeath:
                    UpdateFakeDeath();
                    break;
                case PrimeDeathPhase.Summon:
                    UpdateSummon();
                    break;
                case PrimeDeathPhase.Lunge:
                    UpdateLunge();
                    break;
                case PrimeDeathPhase.Drag:
                    UpdateDrag();
                    break;
                case PrimeDeathPhase.Roar:
                    UpdateRoar();
                    break;
                case PrimeDeathPhase.Finale:
                    UpdateFinale();
                    break;
            }

            deathTimer++;

            //演出落幕
            if (deathTimer >= PhaseFinaleEnd) {
                if (ActivePerformanceHead == npc.whoAmI) {
                    ActivePerformanceHead = -1;
                }
                //真正击杀由服务端/单人端放行，触发正常掉落与击杀标记
                if (!VaultUtils.isClient) {
                    npc.dontTakeDamage = false;
                    npc.life = 0;
                    npc.HitEffect();
                    npc.checkDead();
                    npc.netUpdate = true;
                }
            }

            return true;
        }

        private void InitDeathPerformance() {
            deathInitialized = true;
            deathTimer = 0;
            clawsSpawned = false;
            npc.ai[1] = 0f;
            npc.ai[2] = 0f;
            npc.velocity *= 0.3f;
            ActivePerformanceHead = npc.whoAmI;

            //锁定被抓目标
            if (npc.target >= 0 && npc.target < Main.maxPlayers
                && Main.player[npc.target].active && !Main.player[npc.target].dead) {
                deathTargetIndex = npc.target;
            }
            else if (npc.Center.TryFindClosestPlayer(out var p)) {
                deathTargetIndex = p.whoAmI;
            }

            //清除负面 buff，避免演出期间继续掉血/被控
            for (int i = 0; i < npc.buffType.Length; i++) {
                npc.buffTime[i] = 0;
            }

            if (!VaultUtils.isServer) {
                //过载警报音
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.8f, Volume = 0.9f }, npc.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.6f, Volume = 0.7f }, npc.Center);
            }
        }

        #region 各阶段演出

        /// <summary>假死：先连环爆炸再陷入沉寂，误导玩家以为战斗结束</summary>
        private void UpdateFakeDeath() {
            if (VaultUtils.isServer) {
                return;
            }

            if (deathTimer < 90) {
                //连环爆炸
                if (deathTimer % 12 == 0) {
                    Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.45f, npc.height * 0.45f);
                    SpawnMechBlast(pos, Main.rand.NextFloat(0.9f, 1.5f), false);
                    PrimeDeathPerformancePlayer.RequestShake(5f, 12);
                }
                //接缝漏火花
                if (deathTimer % 4 == 0) {
                    SpawnSparks(npc.Center, 6, 6f);
                }
                Lighting.AddLight(npc.Center, DeathWarmA.ToVector3() * 0.8f);
            }
            else {
                //沉寂——只剩残烟与零星电火花，营造"它已经死了"的假象
                if (deathTimer % 9 == 0) {
                    SpawnSparks(npc.Center, 2, 3f);
                }
                if (deathTimer % 18 == 0) {
                    Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f);
                    PRTLoader.NewParticle<PRT_Smoke>(pos, -Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.5f),
                        Color.Lerp(new Color(60, 56, 54), new Color(22, 20, 20), Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.7f, 1.1f)).Configure(Main.rand.Next(50, 80), 0.7f, Main.rand.NextFloat(-0.04f, 0.04f));
                }
            }
        }

        /// <summary>再生钳子：低沉嗡鸣，头部两侧重新长出双钳，蓄力电弧四溅</summary>
        private void UpdateSummon() {
            //各端本地生成一次钳子 Actor（纯视觉，服务端无需）
            if (!clawsSpawned) {
                clawsSpawned = true;
                TrySpawnDeathClaws();
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
            if (deathTimer % 3 == 0) {
                SpawnSparks(npc.Center, 8, 8f);
            }
            Lighting.AddLight(npc.Center, DeathWarmA.ToVector3() * (1f + (deathTimer - PhaseFakeDeathEnd) / 60f));
        }

        /// <summary>扑抓：双钳高速扑向玩家（钳子运动由 Actor 处理，这里负责音效/火花）</summary>
        private void UpdateLunge() {
            if (deathTimer == PhaseSummonEnd && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f, Volume = 1f }, npc.Center);
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.8f }, npc.Center);
            }

            if (VaultUtils.isServer) {
                return;
            }
            if (deathTimer % 6 == 0) {
                SpawnSparks(npc.Center, 4, 5f);
            }
        }

        /// <summary>拖拽举起：钳子夹住玩家拖到头部正前方，玩家位置锁定由 <see cref="PrimeDeathPerformancePlayer"/> 处理</summary>
        private void UpdateDrag() {
            if (deathTimer == PhaseLungeEnd && !VaultUtils.isServer) {
                //抓住瞬间的金属撞击
                Vector2 grabPos = DeathTargetPlayer?.Center ?? npc.Center;
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.4f, Volume = 1.1f }, grabPos);
                SpawnMechBlast(grabPos, 1.3f, false);
                PrimeDeathPerformancePlayer.RequestShake(10f, 20);
            }

            if (VaultUtils.isServer) {
                return;
            }

            //被夹玩家周围迸溅火花
            Player target = DeathTargetPlayer;
            if (target != null && deathTimer % 4 == 0) {
                SpawnSparks(target.Center, 4, 4.5f);
            }
        }

        /// <summary>怒吼蓄力：头部前倾咆哮，钳子夹紧抖动，红光持续灌注</summary>
        private void UpdateRoar() {
            if (deathTimer == PhaseDragEnd && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.2f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = -0.8f, Volume = 0.9f }, npc.Center);
                PrimeDeathPerformancePlayer.RequestShake(14f, PhaseRoarEnd - PhaseDragEnd);
            }

            if (VaultUtils.isServer) {
                return;
            }
            if (deathTimer % 2 == 0) {
                SpawnSparks(npc.Center, 6, 7f);
            }
            Lighting.AddLight(npc.Center, new Color(255, 60, 30).ToVector3() * 1.8f);
        }

        /// <summary>终爆：核心炸裂、钳子崩碎，玩家被掀飞，进入真正的死亡</summary>
        private void UpdateFinale() {
            if (deathTimer == PhaseRoarEnd && !VaultUtils.isServer) {
                SpawnFinaleBlast();
            }

            if (VaultUtils.isServer) {
                return;
            }
            //终爆余波连环小爆
            if (deathTimer % 5 == 0) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(130f, 130f);
                SpawnMechBlast(pos, Main.rand.NextFloat(1f, 2f), false);
            }
        }

        #endregion

        #region 钳子生成

        /// <summary>本地生成左右两只死亡演出钳子 Actor</summary>
        private void TrySpawnDeathClaws() {
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

        #endregion

        #region 爆炸 / 火花视觉

        /// <summary>
        /// 生成一团机械殉爆：爆炸光团（SoftGlow 叠加）+ 火花四溅 + 岩浆余烬 + 浓烟 + 动态光照 + 音效
        /// </summary>
        private void SpawnMechBlast(Vector2 pos, float scale, bool isFinale) {
            if (VaultUtils.isServer) {
                return;
            }

            Color warm = Color.Lerp(DeathWarmA, DeathWarmB, Main.rand.NextFloat());

            //核心爆炸光团
            PRTLoader.NewParticle<PRT_MechExplosion>(pos, Main.rand.NextVector2Circular(1.5f, 1.5f), warm, scale)
                .Configure(Main.rand.Next(26, 38), warm);

            //火花四溅
            int sparkCount = isFinale ? 52 : Main.rand.Next(4, 8);
            for (int i = 0; i < sparkCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(3f, 11f) * (isFinale ? 1.7f : scale);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel,
                    Color.Lerp(warm, Color.LightGoldenrodYellow, Main.rand.NextFloat()),
                    Main.rand.NextFloat(1.0f, 1.8f)).Configure(true, Main.rand.Next(16, 30));
            }

            //岩浆余烬
            int emberCount = isFinale ? 26 : 2;
            for (int i = 0; i < emberCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3.5f, 3.5f);
                PRTLoader.NewParticle<PRT_LavaFire>(pos + Main.rand.NextVector2Circular(20f, 20f) * scale, vel,
                    Color.White, Main.rand.NextFloat(0.8f, 1.3f) * scale).SetLifetime(20, 46);
            }

            //滚滚浓烟
            int smokeCount = isFinale ? 18 : 2;
            for (int i = 0; i < smokeCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(2f, 2f) - Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.7f);
                PRTLoader.NewParticle<PRT_Smoke>(pos, vel,
                    Color.Lerp(new Color(60, 56, 54), new Color(20, 18, 18), Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.8f, 1.4f) * scale)
                    .Configure(Main.rand.Next(45, 72), 0.7f, Main.rand.NextFloat(-0.05f, 0.05f));
            }

            Lighting.AddLight(pos, warm.ToVector3() * (isFinale ? 3f : 1.1f) * scale);

            //密集爆炸时按概率播放，避免爆音同帧堆叠破音
            if (isFinale || Main.rand.NextBool(3)) {
                SoundEngine.PlaySound(SoundID.Item14 with {
                    Pitch = isFinale ? -0.5f : Main.rand.NextFloat(-0.2f, 0.35f),
                    Volume = isFinale ? 1f : 0.4f
                }, pos);
            }
        }

        /// <summary>在指定位置喷射电火花，模拟电路过载/接缝喷火</summary>
        private void SpawnSparks(Vector2 center, int count, float speed) {
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

        /// <summary>核心终极殉爆 + 周身连锁爆裂 + 玩家位置同步炸裂 + 强烈屏幕震动</summary>
        private void SpawnFinaleBlast() {
            if (VaultUtils.isServer) {
                return;
            }

            SpawnMechBlast(npc.Center, 4.2f, true);

            //头部周身连锁
            for (int i = 0; i < 8; i++) {
                SpawnMechBlast(npc.Center + Main.rand.NextVector2Circular(140f, 140f), Main.rand.NextFloat(1.4f, 2.4f), false);
            }

            //被举起玩家处的同步炸裂
            Player target = DeathTargetPlayer;
            if (target != null && target.active) {
                SpawnMechBlast(target.Center, 2.6f, false);
            }

            PrimeDeathPerformancePlayer.RequestShake(26f, 45);
            SoundEngine.PlaySound(SoundID.DD2_KoboldExplosion with { Volume = 1.2f, Pitch = -0.35f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1f }, npc.Center);
        }

        #endregion
    }
}
