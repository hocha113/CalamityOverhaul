using CalamityOverhaul.Content.GameModes.BrutalMobs.Common;
using CalamityOverhaul.Content.GameModes.BrutalMobs.Eclipse.Projectiles;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Eclipse
{
    /// <summary>
    /// 蛾怪（日食小Boss）签名技层：行为叠加不接管原版 AI（原版产卵/冲撞照跑）。
    /// 签名技一「三连俯冲」：每段独立锁定的预告俯冲（预告≥40帧），三段全部挥空则进入
    /// 加长破绽态（小Boss 破绽 74-90 帧）；任一段碰到玩家则正常收招。
    /// 签名技二「护巢狂怒」：玩家打碎蛾卵触发短暂提速（预告=卵破裂视觉本身，豁免条款见
    /// EclNestFuryProj 注释），狂怒同时加速签名技冷却。
    /// 幼蛾（MothronSpawn）自带「蜷缩扑脸」近身体术：≥24 帧蜷缩前摇（压速+翅尘即预告，
    /// 无 omen 实体）→ MobDash 包络弧线扑击（锁向不重瞄）→ 力竭漂移后摇；
    /// 姿态相位经 GlobalNPC ExtraAI 随 NPC 同步包镜像到客户端，前摇可见性与压速各端一致。
    /// 每实例同时至多一个签名技进行中（单状态机保证）；狂怒是反应式状态不占用签名槽。
    /// 蛾怪 boss 旗标离线未查证：本层按显式类型名单放行（仅 Mothron/MothronEgg/MothronSpawn），
    /// 提速补偿走运行时旗标判读（旗标无关设计）。卵只做机制配角（破裂触发狂怒）；
    /// 小Boss 死亡流程不加任何机制
    /// </summary>
    internal class EclMothronNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        //==== 三连俯冲 ====
        /// <summary>俯冲段数（三连，每段独立锁定）</summary>
        private const int DiveCount = 3;
        /// <summary>俯冲速度（名义值；实际注入按运行时旗标决定是否除提速补偿）</summary>
        private const float DiveSpeed = 14f;
        /// <summary>段间收势悬停帧</summary>
        private const int HoverGapFrames = 14;
        /// <summary>签名技冷却（档位 1/2/3）</summary>
        private static readonly int[] SigCooldownByTier = [540, 460, 380];
        private const int CooldownJitter = 60;
        private const int RetryDelay = 40;
        private const float TriggerRangeMin = 200f;
        private const float TriggerRangeMax = 700f;

        //==== 护巢狂怒 ====
        /// <summary>卵破裂寻找受益蛾怪的半径</summary>
        private const float NestFuryRadius = 1400f;

        //==== 幼蛾·蜷缩扑脸 ====
        /// <summary>幼蛾蓄力前摇帧（近身体术契约 ≥24：压速+翅尘即预告）</summary>
        private const int SpawnCurlFrames = 26;
        /// <summary>前摇压速系数（每帧乘，滞空蜷缩可读；各端按镜像相位同判）</summary>
        private const float SpawnCurlDamp = 0.8f;
        /// <summary>扑击包络：爬升/保持/衰减帧（MobDash.Envelope 三段）</summary>
        private const int SpawnPounceRise = 5;
        private const int SpawnPounceHold = 12;
        private const int SpawnPounceDecay = 12;
        /// <summary>扑击总帧</summary>
        private const int SpawnPounceTotal = SpawnPounceRise + SpawnPounceHold + SpawnPounceDecay;
        /// <summary>扑击名义峰速（档位 1/2/3；注入除提速补偿）</summary>
        private static readonly float[] SpawnPounceSpeedByTier = [8.6f, 9.4f, 10.2f];
        /// <summary>出手上抬偏置：初速垂直分量，扑击途中对称转为下坠，形成先扬后落的弧线</summary>
        private const float SpawnArcLift = 3.4f;
        /// <summary>力竭漂移后摇帧</summary>
        private const int SpawnDriftFrames = 20;
        /// <summary>漂移压速系数（每帧乘，读作力竭；各端按镜像相位同判）</summary>
        private const float SpawnDriftDamp = 0.9f;
        /// <summary>幼蛾扑击冷却（档位 1/2/3，契约 ≥300）</summary>
        private static readonly int[] SpawnCooldownByTier = [420, 360, 300];
        /// <summary>幼蛾首攻错拍窗（M7 契约 60~180）</summary>
        private const int SpawnFirstCooldownMin = 60;
        private const int SpawnFirstCooldownMax = 180;
        /// <summary>同屏同时处于蜷缩/扑击段的幼蛾上限（并发闸，服务端数各实例相位）</summary>
        private const int SpawnPounceCap = 4;
        private const float SpawnTriggerRangeMin = 70f;
        private const float SpawnTriggerRangeMax = 380f;

        //==== 破绽踉跄 ====
        /// <summary>破绽期全轴拖拽系数（飞行体，略缓于小怪保持巨物感）</summary>
        private const float OpeningDrag = 0.94f;

        private const byte PhaseIdle = 0;
        private const byte PhaseTelegraph = 1;
        private const byte PhaseDive = 2;
        private const byte PhaseHover = 3;
        //幼蛾专属相位段与蛾怪错开，ExtraAI 镜像值不歧义
        private const byte PhaseSpawnCurl = 10;
        private const byte PhaseSpawnPounce = 11;
        private const byte PhaseSpawnDrift = 12;

        private int boundTier;
        /// <summary>角色：卵实例只承担破裂触发，不跑签名状态机</summary>
        private bool isEgg;
        /// <summary>角色：幼蛾实例只跑「蜷缩扑脸」状态机</summary>
        private bool isSpawn;

        //——服务端决策私产——
        private bool initialized;
        private byte phase;
        private int timer;
        private int cooldown;
        private int diveIndex;
        /// <summary>三连中任一段是否碰到过玩家（服务端几何采样）</summary>
        private bool anyDiveConnected;
        private float lockDir;
        private Vector2 dashVec;
        private int omenIndex = -1;

        //——镜像字段：由已同步实体每帧盖戳，各端一致——
        private uint openingUntil;
        private uint furyUntil;
        /// <summary>俯冲执行窗镜像（EclMothronDiveOmen 执行段盖戳）：狂怒推进的豁免只读它</summary>
        private uint diveUntil;
        /// <summary>本端最后看到的幼蛾相位（各端本地，用于在相位沿播一次性尘/声提示）</summary>
        private byte lastVisualPhase;

        internal void StampOpening() => openingUntil = Main.GameUpdateCount + 2;
        internal void StampFury() => furyUntil = Main.GameUpdateCount + 2;
        internal void StampDive() => diveUntil = Main.GameUpdateCount + 2;
        /// <summary>是否处于签名俯冲执行窗（镜像戳判读，各端一致）</summary>
        internal bool InDiveWindow => Main.GameUpdateCount < diveUntil;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && (entity.type == NPCID.Mothron || entity.type == NPCID.MothronEgg
                || entity.type == NPCID.MothronSpawn);

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            boundTier = tier;
            isEgg = npc.type == NPCID.MothronEgg;
            isSpawn = npc.type == NPCID.MothronSpawn;
            //幼蛾首发错拍推迟到首个决策帧播种（此刻 whoAmI 恒为 0）
        }

        /// <summary>
        /// 签名技资格：显式名单放行（AppliesToEntity 已限定 Mothron），故意不查 npc.boss——
        /// 多数事件小 Boss 旗标为 false 但离线未逐一查证，名单+运行时读旗标的设计对两种取值都正确
        /// </summary>
        private static bool SignatureEligible(NPC npc) {
            if (npc.friendly || npc.immortal || npc.dontTakeDamage || npc.SpawnedFromStatue) {
                return false;
            }
            return npc.realLife < 0 && npc.lifeMax > 5 && npc.damage > 0;
        }

        private static bool CanSee(NPC npc, Player player)
            => Collision.CanHit(npc.position, npc.width, npc.height, player.position, player.width, player.height);

        public override void PostAI(NPC npc) {
            if (boundTier <= 0 || isEgg) {
                return;
            }
            if (isSpawn) {
                SpawnPostAI(npc);
                return;
            }

            //破绽踉跄：所有端读同一镜像戳（实体已同步），模拟一致
            if (Main.GameUpdateCount < openingUntil) {
                npc.velocity *= OpeningDrag;
            }

            if (VaultUtils.isClient) {
                return;
            }

            if (!initialized) {
                initialized = true;
                //首个决策帧播种（SetDefaults 期 whoAmI 恒为 0，不在那里读）
                cooldown = SigCooldownByTier[boundTier - 1] / 2 + npc.whoAmI * 41 % 120;
            }

            switch (phase) {
                case PhaseIdle:
                    //护巢狂怒加速冷却：狂怒窗内双倍走表（狂怒本身由实体可见）
                    cooldown -= Main.GameUpdateCount < furyUntil ? 2 : 1;
                    if (cooldown <= 0) {
                        TryStartDives(npc);
                    }
                    break;
                case PhaseTelegraph:
                    TickTelegraph(npc);
                    break;
                case PhaseDive:
                    TickDive(npc);
                    break;
                default:
                    TickHover(npc);
                    break;
            }
        }

        private void TryStartDives(NPC npc) {
            if (!SignatureEligible(npc)) {
                cooldown = RetryDelay * 3;
                return;
            }
            if (Main.GameUpdateCount < openingUntil) {
                cooldown = RetryDelay;
                return;
            }
            if (!npc.HasValidTarget) {
                cooldown = RetryDelay;
                return;
            }
            Player player = Main.player[npc.target];
            float dist = npc.Distance(player.Center);
            if (!player.Alives() || dist < TriggerRangeMin || dist > TriggerRangeMax || !CanSee(npc, player)) {
                cooldown = RetryDelay;
                return;
            }
            diveIndex = 0;
            anyDiveConnected = false;
            StartDiveSegment(npc);
        }

        /// <summary>起一段俯冲预告：预告即实体，生成失败则整个签名技作废（失败方向=安全方向）</summary>
        private void StartDiveSegment(NPC npc) {
            if (npc.HasValidTarget && Main.player[npc.target].Alives()) {
                lockDir = (Main.player[npc.target].Center - npc.Center).ToRotation();
            }
            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<EclMothronDiveOmen>(), 0, 0f, Main.myPlayer,
                npc.whoAmI, diveIndex, 0f);
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                omenIndex = -1;
                phase = PhaseIdle;
                cooldown = RetryDelay * 2;
                return;
            }
            //刹车蓄势（仅脉冲帧跟同步）
            npc.velocity *= 0.35f;
            npc.netUpdate = true;
            timer = diveIndex == 0 ? EclMothronDiveOmen.TelegraphFirst : EclMothronDiveOmen.TelegraphNext;
            phase = PhaseTelegraph;
        }

        private bool TryGetBoundOmen(NPC npc, out Projectile omen) {
            omen = null;
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                return false;
            }
            Projectile p = Main.projectile[omenIndex];
            if (!p.active || p.type != ModContent.ProjectileType<EclMothronDiveOmen>() || (int)p.ai[0] != npc.whoAmI) {
                return false;
            }
            omen = p;
            return true;
        }

        private void TickTelegraph(NPC npc) {
            timer--;

            //中段刹车压漂移，预告线贴住出发点
            if (timer == 22 || timer == 10) {
                npc.velocity *= 0.4f;
                npc.netUpdate = true;
            }

            //锁定帧：本段方向自此为承诺（每段独立锁定），写回预兆做各端权威纠偏
            if (timer == EclMothronDiveOmen.LockFrames) {
                if (npc.HasValidTarget && Main.player[npc.target].Alives()) {
                    lockDir = (Main.player[npc.target].Center - npc.Center).ToRotation();
                }
                if (TryGetBoundOmen(npc, out Projectile omen)) {
                    omen.ai[2] = lockDir + 10f;
                    omen.netUpdate = true;
                }
            }

            if (timer <= 0) {
                //俯冲注入：位移项除提速补偿（运行时读旗标决定，旗标无关设计），重力项无（直线俯冲）
                float gain = EclEclipseSets.MoveGain(npc, boundTier);
                dashVec = lockDir.ToRotationVector2() * (DiveSpeed / gain);
                npc.velocity = dashVec;
                npc.netUpdate = true;
                timer = EclMothronDiveOmen.StrikeFrames;
                phase = PhaseDive;
            }
        }

        private void TickDive(NPC npc) {
            timer--;
            npc.velocity = dashVec;    //抵住原版 AI 转向，兑现本段直线承诺
            if (timer % 8 == 0) {
                npc.netUpdate = true;
            }

            if (!anyDiveConnected) {
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player player = Main.player[i];
                    if (player.active && !player.dead && npc.Hitbox.Intersects(player.Hitbox)) {
                        anyDiveConnected = true;
                        break;
                    }
                }
            }

            if (timer <= 0) {
                npc.velocity *= 0.4f;
                npc.netUpdate = true;
                diveIndex++;
                if (diveIndex < DiveCount) {
                    timer = HoverGapFrames;
                    phase = PhaseHover;
                }
                else {
                    ResolveDives(npc);
                }
            }
        }

        private void TickHover(NPC npc) {
            timer--;
            if (timer <= 0) {
                StartDiveSegment(npc);
            }
        }

        /// <summary>三连了结：全部挥空=加长破绽（躲满三段的大反打窗口），任一命中=正常收招</summary>
        private void ResolveDives(NPC npc) {
            if (!anyDiveConnected) {
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                    ModContent.ProjectileType<EclOpeningProj>(), 0, 0f, Main.myPlayer,
                    npc.whoAmI, npc.type, EclEclipseSets.MothronOpeningFramesByTier[boundTier - 1]);
            }
            phase = PhaseIdle;
            omenIndex = -1;
            cooldown = SigCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
        }

        #region 幼蛾·蜷缩扑脸（近身体术：≥24 帧姿态前摇代替 omen 实体，相位经 ExtraAI 镜像可见）
        private void SpawnPostAI(NPC npc) {
            //姿态段共同逻辑先行：压速所有端同判（相位已镜像），尘/声按本端相位沿播放
            SpawnSharedTick(npc);

            if (VaultUtils.isClient) {
                //决策只在服务端/单人；客户端画面来自镜像相位+原生 NPC 同步
                return;
            }

            if (!initialized) {
                initialized = true;
                //首攻错拍收进 60~180 帧（M7）：此刻 whoAmI 已有效，个体错帧防同屏齐动
                cooldown = SpawnFirstCooldownMin
                    + npc.whoAmI * 37 % (SpawnFirstCooldownMax - SpawnFirstCooldownMin + 1);
            }

            switch (phase) {
                case PhaseIdle:
                    if (--cooldown <= 0) {
                        TrySpawnPounce(npc);
                    }
                    break;
                case PhaseSpawnCurl:
                    TickSpawnCurl(npc);
                    break;
                case PhaseSpawnPounce:
                    TickSpawnPounce(npc);
                    break;
                case PhaseSpawnDrift:
                    TickSpawnDrift(npc);
                    break;
            }
        }

        /// <summary>
        /// 幼蛾姿态段各端共同逻辑：相位经 ExtraAI 镜像到客户端，压速在所有端同判（模拟一致不橡皮筋）；
        /// 尘/声由本端相位驱动（无 omen 实体的近身体术，可见性契约靠姿态给足；专用服务器只算不演）
        /// </summary>
        private void SpawnSharedTick(NPC npc) {
            if (phase != lastVisualPhase) {
                if (!Main.dedServ) {
                    if (phase == PhaseSpawnCurl) {
                        SoundEngine.PlaySound(SoundID.MaxMana with {
                            Volume = 0.35f, Pitch = 0.45f, MaxInstances = 4
                        }, npc.Center);
                    }
                    else if (phase == PhaseSpawnPounce && lastVisualPhase == PhaseSpawnCurl) {
                        SoundEngine.PlaySound(SoundID.Item1 with {
                            Volume = 0.5f, Pitch = 0.5f, MaxInstances = 4
                        }, npc.Center);
                        for (int i = 0; i < 10; i++) {
                            Dust burst = Dust.NewDustPerfect(npc.Center, DustID.Torch,
                                Main.rand.NextVector2Circular(2.6f, 2.2f), 130, EclEclipseSets.DiveTint, 1.2f);
                            burst.noGravity = true;
                        }
                    }
                }
                lastVisualPhase = phase;
            }

            if (phase == PhaseSpawnCurl) {
                //蜷缩蓄力前摇：压速+翅尘即预告（≥24 帧可见）
                npc.velocity *= SpawnCurlDamp;
                if (!Main.dedServ) {
                    for (int i = 0; i < 2; i++) {
                        Vector2 wing = npc.Center + new Vector2(
                            (Main.rand.NextBool() ? 1f : -1f) * Main.rand.NextFloat(6f, 16f),
                            Main.rand.NextFloat(-8f, 6f));
                        Dust dust = Dust.NewDustPerfect(wing, DustID.Torch,
                            new Vector2(Main.rand.NextFloat(-0.7f, 0.7f), Main.rand.NextFloat(0.3f, 1.2f)),
                            140, EclEclipseSets.DiveTint, 1.05f);
                        dust.noGravity = true;
                    }
                    Lighting.AddLight(npc.Center, 0.2f, 0.13f, 0.05f);
                }
            }
            else if (phase == PhaseSpawnDrift) {
                //力竭漂移后摇：压速+疲态尘稀落
                npc.velocity *= SpawnDriftDamp;
                if (!Main.dedServ && Main.rand.NextBool(3)) {
                    Dust tired = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(10f, 8f),
                        DustID.Torch, new Vector2(0f, Main.rand.NextFloat(0.5f, 1.2f)),
                        160, EclEclipseSets.DiveTint, 0.8f);
                    tired.noGravity = false;
                }
            }
        }

        /// <summary>幼蛾机制资格：标准小怪排除清单（每次触发复查）</summary>
        private static bool SpawnEligible(NPC npc) {
            if (npc.friendly || npc.townNPC || npc.immortal || npc.dontTakeDamage || npc.boss) {
                return false;
            }
            if (npc.lifeMax <= 5 || npc.damage <= 0) {
                return false;
            }
            if (npc.SpawnedFromStatue) {
                return false;
            }
            return npc.realLife < 0;
        }

        /// <summary>并发闸：数同屏处于蜷缩/扑击段的幼蛾（服务端私产相位，仅触发时调用）</summary>
        private static int CountPouncingSpawns() {
            int count = 0;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC other = Main.npc[i];
                if (other.active && other.type == NPCID.MothronSpawn
                    && other.TryGetGlobalNPC(out EclMothronNPC moth)
                    && moth.phase is PhaseSpawnCurl or PhaseSpawnPounce) {
                    count++;
                }
            }
            return count;
        }

        private void TrySpawnPounce(NPC npc) {
            if (!SpawnEligible(npc)) {
                cooldown = RetryDelay * 3;
                return;
            }
            if (!npc.HasValidTarget) {
                cooldown = RetryDelay;
                return;
            }
            Player player = Main.player[npc.target];
            float dist = npc.Distance(player.Center);
            if (!player.Alives() || dist < SpawnTriggerRangeMin || dist > SpawnTriggerRangeMax
                || !CanSee(npc, player)) {
                cooldown = RetryDelay;
                return;
            }
            if (CountPouncingSpawns() >= SpawnPounceCap) {
                cooldown = 45;
                return;
            }
            //入相刹车脉冲：相位沿跟同步（镜像相位+压速一并过线）
            npc.velocity *= 0.5f;
            npc.netUpdate = true;
            timer = SpawnCurlFrames;
            phase = PhaseSpawnCurl;
        }

        private void TickSpawnCurl(NPC npc) {
            timer--;
            //镜像载波低频重推（含压速演化，丢包自愈）
            if (timer % 10 == 0) {
                npc.netUpdate = true;
            }
            if (timer <= 0) {
                CommitSpawnPounce(npc);
            }
        }

        /// <summary>
        /// 扑击出手：锁向即承诺，整条弧线不再重瞄。上抬偏置让初速偏离直线，
        /// 途中对称转为下坠，弧线中段回穿目标点（扑脸）。蛾类无重力项，
        /// 整条弧全是承诺位移，含偏置一并除提速补偿
        /// </summary>
        private void CommitSpawnPounce(NPC npc) {
            if (!npc.HasValidTarget || !Main.player[npc.target].Alives()) {
                //目标失效：不出手，安全回待机（失败方向=安全方向）
                phase = PhaseIdle;
                cooldown = RetryDelay;
                npc.netUpdate = true;
                return;
            }
            float gain = EclEclipseSets.MoveGain(npc, boundTier);
            Vector2 dir = (Main.player[npc.target].Center - npc.Center)
                .SafeNormalize(Vector2.UnitX * npc.direction);
            dashVec = (dir * SpawnPounceSpeedByTier[boundTier - 1] + Vector2.UnitY * -SpawnArcLift) / gain;
            timer = SpawnPounceTotal;
            phase = PhaseSpawnPounce;
            npc.netUpdate = true;
        }

        private void TickSpawnPounce(NPC npc) {
            timer--;
            int t = SpawnPounceTotal - timer;
            float gain = EclEclipseSets.MoveGain(npc, boundTier);
            float env = MobDash.Envelope(t, SpawnPounceRise, SpawnPounceHold, SpawnPounceDecay);
            //渐进下坠分量与上抬偏置对称，包络整形出「先扬后落」的锁定弧
            float sag = 2f * SpawnArcLift / gain * (t / (float)SpawnPounceTotal);
            npc.velocity = (dashVec + Vector2.UnitY * sag) * env;
            if (timer % 6 == 0) {
                npc.netUpdate = true;
            }
            if (timer <= 0) {
                timer = SpawnDriftFrames;
                phase = PhaseSpawnDrift;
                npc.netUpdate = true;
            }
        }

        private void TickSpawnDrift(NPC npc) {
            timer--;
            if (timer % 10 == 0) {
                npc.netUpdate = true;
            }
            if (timer <= 0) {
                npc.velocity *= 0.5f;    //清残速，控制权干净还给原版 AI
                phase = PhaseIdle;
                cooldown = SpawnCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
                npc.netUpdate = true;
            }
        }

        /// <summary>
        /// 幼蛾姿态相位镜像：无 omen 实体的近身体术，前摇可见性靠相位随 NPC 同步包过线。
        /// 恒定长写入（Mothron/卵/幼蛾三角色同布局），杜绝流错位；相位沿必置 netUpdate 作载波
        /// </summary>
        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
            => binaryWriter.Write(phase);

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
            => phase = binaryReader.ReadByte();
        #endregion

        /// <summary>
        /// 卵破裂触发护巢狂怒（服务端钩子）：为最近的蛾怪刷新狂怒实体。
        /// 刷新走"杀旧生新"，全端同步无 timeLeft 漂移。小Boss 自身死亡不挂任何机制
        /// </summary>
        public override void OnKill(NPC npc) {
            if (!isEgg || boundTier <= 0 || VaultUtils.isClient) {
                return;
            }

            //找最近的活蛾怪
            int best = -1;
            float bestDist = NestFuryRadius;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC candidate = Main.npc[i];
                if (candidate.active && candidate.type == NPCID.Mothron) {
                    float dist = candidate.Distance(npc.Center);
                    if (dist < bestDist) {
                        bestDist = dist;
                        best = i;
                    }
                }
            }
            if (best < 0) {
                return;
            }

            //杀掉旧狂怒实体再生成（=刷新持续时间，各端由弹幕生灭原生同步）
            int furyType = ModContent.ProjectileType<EclNestFuryProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == furyType && (int)proj.ai[0] == best) {
                    proj.Kill();
                }
            }
            Projectile.NewProjectile(npc.GetSource_Death(), Main.npc[best].Center, Vector2.Zero,
                furyType, 0, 0f, Main.myPlayer, best, 0f, 0f);
        }

        #region 破绽命中门（读镜像，各端一致）
        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers) {
            if (boundTier > 0 && !isEgg && Main.GameUpdateCount < openingUntil) {
                modifiers.FinalDamage *= EclEclipseSets.OpeningDamageAmp;
            }
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) {
            if (boundTier > 0 && !isEgg && Main.GameUpdateCount < openingUntil) {
                modifiers.FinalDamage *= EclEclipseSets.OpeningDamageAmp;
            }
        }
        #endregion
    }
}
