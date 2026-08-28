using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.NPCs
{
    /// <summary>
    /// 夜行列队首·执幡者（P4 §2.4）：脚本化 apparition 升格的事件敌，全场唯一队，导演调度。<br/>
    /// 规矩：不许碰、不许打、不许挡路。五人送葬队列（执幡 + 抬棺 ×4 + 棺）自枯林东段
    /// 贴雾面西行入墓；让路走完 = 坟前齐躬、纸钱慢落、沉土留供品（裁决13 总开关）；
    /// 惊扰任一条 = 全列同帧回头、白灯笼烧红、棺坠地、化煞。击杀执幡者全队即散。<br/>
    /// 联机合同（§2.4 冻结）：成员 ai[0]=编队位 ai[1]=状态 ai[2]=计时 ai[3]=队首 whoAmI；
    /// 惊扰三条与状态转移全在服务器（队首裁决），回头拍是状态沿各端本地重放，
    /// 同帧性由 ai 过线保证；ServerSyncPacer(24) 低频重锚编队漂移；
    /// 队首探针失败累计 200t 全队入土消散（宁失一场演出不留一队呆子）。<br/>
    /// 视觉：RaggedCaster 施法者滑行架过 KiyumeKaidan.fx TechPaperGhost 纸衣化
    /// （uDissolve 常值低位，走满同帧退场——P4-B 交付约定）；白灯笼 = ChainLantern
    /// 物品贴图挂幡杆顶（画法抄 LanternGuideYokai），队列唯一光源
    /// </summary>
    internal class FuneralCortegeYokai : KiyumeYokaiNPC
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.RaggedCaster;

        //ai 布局（§2.4 冻结）：[0]=编队位（队首恒 0） [1]=状态 [2]=计时 [3]=队首（队首自身不用）
        private ref float CortegeState => ref NPC.ai[1];
        private ref float CortegeTimer => ref NPC.ai[2];

        //──── 服务器侧字段（不入同步）────

        private bool destInit;
        private float destX;
        private int blockTicks;
        private int probeFailTicks;
        private int stuckTicks;
        private float stuckRefX;

        //──── 各端本地表现 ────

        private float presentAlpha;
        private int facing = -1;
        /// <summary>行进方向：服务器由目的地定，客户端从同步速度读（棺位/走廊判定共用）</summary>
        private int marchDir = -1;
        /// <summary>已观察到化煞（迟入端进场即化煞/收场按已惊保守处理，免双棺）</summary>
        private bool everRaged;

        protected override void SetYokaiStaticDefaults() {
            Main.npcFrameCount[Type] = 3;   //施法者滑行架三帧（NPCID.cs/Main.cs 帧表实证）
        }

        protected override void SetYokaiDefaults() {
            NPC.width = 24;
            NPC.height = 46;
            NPC.damage = 0;          //执幡者不近身，化煞期只敲铃
            NPC.defense = KiyumeYokaiMetrics.CortegeLeadDefense;
            NPC.lifeMax = KiyumeYokaiMetrics.CortegeLeadLife;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;   //贴地靠探针，坡地宁飘不卡
            NPC.HitSound = SoundID.NPCHit1 with { Volume = 0.5f, Pitch = -0.4f };
            NPC.DeathSound = SoundID.NPCDeath6 with { Volume = 0.5f, Pitch = -0.5f };
        }

        //==================== AI ====================

        protected override void YokaiAI() {
            HealAlpha(0);
            AmbientClock++;
            if (CortegeStateEdge(out int prev)) {
                PlayStateCue(prev);
            }
            ServerSyncPacer();
            NPC.velocity.Y = 0f;

            if (!VaultUtils.isClient && !destInit) {
                InitDestination();
            }

            switch ((int)CortegeState) {
                case CortegeShared.StateMarch:
                    UpdateMarch();
                    break;
                case CortegeShared.StateTurn:
                    UpdateTurn();
                    break;
                case CortegeShared.StateRage:
                    UpdateRage();
                    break;
                case CortegeShared.StateBow:
                    UpdateBow();
                    break;
                default:
                    UpdateSink();
                    break;
            }

            //惊扰三条只在素队期裁决（Turn=1/Rage=2 数值居中，须显式列举）
            if (!VaultUtils.isClient
                && (int)CortegeState is CortegeShared.StateMarch or CortegeShared.StateBow) {
                JudgeScare();
            }

            NPC.direction = facing;
            NPC.spriteDirection = facing;
            UpdatePresentation();
        }

        private void UpdateMarch() {
            CortegeTimer++;
            if (!VaultUtils.isClient) {
                marchDir = destX > NPC.Center.X ? 1 : -1;
                NPC.velocity.X = marchDir * KiyumeYokaiMetrics.CortegeWalkSpeed;
                if (Math.Abs(NPC.Center.X - destX) <= 24f) {
                    NPC.velocity.X = 0f;
                    SetCortegeState(CortegeShared.StateBow);
                }
                else if (StuckTooLong()) {
                    SetCortegeState(CortegeShared.StateSink);
                }
            }
            else if (Math.Abs(NPC.velocity.X) > 0.01f) {
                //客户端从同步速度读行进向（速度由服务器裁决，同步包外推）
                marchDir = NPC.velocity.X > 0f ? 1 : -1;
            }
            facing = marchDir;
            Glide();
        }

        private void UpdateTurn() {
            //回头拍：20t 全体死寂，帽下余烬这 20t 里点亮，白灯笼开始烧红
            NPC.velocity.X = 0f;
            CortegeTimer++;
            int dir = CortegeShared.ScareFaceDir(NPC);
            if (dir != 0) {
                facing = dir;
            }
            if (!VaultUtils.isClient && CortegeTimer >= KiyumeYokaiMetrics.CortegeTurnBeat) {
                DropCoffin();
                SetCortegeState(CortegeShared.StateRage);
            }
        }

        private void UpdateRage() {
            //化煞：执幡者定点，每 90t 敲铃一响；抬棺四员冲-刹袭击（成员自驱）
            NPC.velocity.X = 0f;
            CortegeTimer++;
            int dir = CortegeShared.ScareFaceDir(NPC);
            if (dir != 0) {
                facing = dir;
            }
            //铃走严格前进沿（resync 回卷不重播），第一响在化煞后 90t
            int beat = (int)(CortegeTimer / KiyumeYokaiMetrics.CortegeBellPeriod);
            if (beat >= 1 && BeatForward(beat)) {
                RingBell();
            }
            if (!VaultUtils.isClient && CortegeTimer >= KiyumeYokaiMetrics.CortegeRageTimeout) {
                SetCortegeState(CortegeShared.StateSink);
            }
        }

        private void UpdateBow() {
            NPC.velocity.X = 0f;
            CortegeTimer++;
            if (!VaultUtils.isClient && CortegeTimer >= KiyumeYokaiMetrics.CortegeBowTicks) {
                SpawnReward();
                SetCortegeState(CortegeShared.StateSink);
            }
        }

        private void UpdateSink() {
            NPC.velocity.X = 0f;
            NPC.velocity.Y = CortegeShared.SinkSpeed;   //入土
            CortegeTimer++;
            if (CortegeTimer >= CortegeShared.SinkTicks) {
                //uDissolve 走满同帧退场（P4-B 交付约定：=1 仍残近透明斑点，残点不上屏）
                NPC.active = false;
            }
        }

        //==================== 服务器裁决 ====================

        /// <summary>惊扰三条：碰任一成员 24px / 任一成员掉血 / 队首前方走廊驻留 >90t</summary>
        private void JudgeScare() {
            bool scare = NPC.life < NPC.lifeMax || AnyMemberTouchedOrHurt();

            //挡路：队首前方 BlockLookAheadPx 走廊、±CortegeBlockBand 路带内有人即累计；
            //只在行进期算挡路——坟前观礼不算（Bow 期仍吃碰触/掉血两条）
            bool corridor = false;
            if ((int)CortegeState == CortegeShared.StateMarch) {
                foreach (Player player in Main.ActivePlayers) {
                    if (player.dead) {
                        continue;
                    }
                    float dx = (player.Center.X - NPC.Center.X) * marchDir;
                    if (dx > 0f && dx <= CortegeShared.BlockLookAheadPx
                        && Math.Abs(player.Center.Y - NPC.Center.Y) <= KiyumeYokaiMetrics.CortegeBlockBand) {
                        corridor = true;
                        break;
                    }
                }
            }
            blockTicks = corridor ? blockTicks + 1 : 0;

            if (scare || blockTicks > KiyumeYokaiMetrics.CortegeBlockTicks) {
                BeginScare();
            }
        }

        private bool AnyMemberTouchedOrHurt() {
            if (TouchedByAnyPlayer(NPC)) {
                return true;
            }
            int porterType = ModContent.NPCType<FuneralCortegePorter>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC other = Main.npc[i];
                if (!other.active || other.type != porterType || (int)other.ai[3] != NPC.whoAmI) {
                    continue;
                }
                if (other.life < other.lifeMax || TouchedByAnyPlayer(other)) {
                    return true;
                }
            }
            return false;
        }

        private static bool TouchedByAnyPlayer(NPC member) {
            foreach (Player player in Main.ActivePlayers) {
                if (!player.dead
                    && player.Distance(member.Center) <= KiyumeYokaiMetrics.CortegeTouchRange) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>抬棺者被一击致死不留血线证词时的亲报入口（OnKill 只在权威端跑）</summary>
        internal void ForceScare() {
            if (!VaultUtils.isClient
                && (int)CortegeState is CortegeShared.StateMarch or CortegeShared.StateBow) {
                BeginScare();
            }
        }

        private void BeginScare() {
            NPC.velocity.X = 0f;
            SetCortegeState(CortegeShared.StateTurn);
        }

        /// <summary>棺坠地（服务器）：prop 生成走会话上限（CortegeCoffinSessionCap 封顶）</summary>
        private void DropCoffin() {
            Vector2 bangAt = CortegeShared.CarriedCoffinPos(NPC, marchDir);
            //坠棺巨响挂上听觉地图（裁决11 天然噪声源，量级对齐开火脉冲同阶）；
            //响声不依赖 prop 是否落地，故报在会话上限判定之前
            Stealth.KiyumeStealthSense.ReportNoise(bangAt, KiyumeHoundMetrics.WeaponImpulse);
            KiyumeHauntDirector inst = KiyumeHauntDirector.Instance;
            if (inst == null || inst.coffinsSpawned >= KiyumeYokaiMetrics.CortegeCoffinSessionCap) {
                return;   //第三具起不留物证，坠地演出照放（Rage 沿的音画不依赖 prop）
            }
            inst.coffinsSpawned++;
            //NewNPC 底中定位：自肩高落下，prop 自坠自落定
            KiyumeHauntDirector.SpawnYokai(ModContent.NPCType<FuneralCortegeCoffin>(),
                bangAt + new Vector2(0f, 15f));
        }

        /// <summary>坟头供品（裁决13 解禁，CortegeRewardOn 总开关）：服务器 NewItem 自带广播</summary>
        private void SpawnReward() {
            if (!KiyumeYokaiMetrics.CortegeRewardOn) {
                return;
            }
            IEntitySource src = NPC.GetSource_Death();
            for (int i = 0; i < KiyumeYokaiMetrics.CortegeRewardHearts; i++) {
                Item.NewItem(src, NPC.Hitbox, ItemID.Heart);
            }
            Item.NewItem(src, NPC.Hitbox, ItemID.SilverCoin, KiyumeYokaiMetrics.CortegeRewardSilver);
        }

        /// <summary>目的地：主坟锚（KiyumeStructures 列表客户端恒空，只在服务器读），
        /// W3 墓地微区未注册前回退列 1850 平地点</summary>
        private void InitDestination() {
            destInit = true;
            stuckRefX = NPC.Center.X;
            destX = KiyumeStructures.GraveMain is Point grave
                ? grave.X * 16f + 8f
                : KiyumeYokaiMetrics.CortegeFallbackDestCol * 16f + 8f;
            NPC.netUpdate = true;
        }

        /// <summary>卡步计：20t 一测，累计 CortegeStuckDissolveTicks 走不动全队入土</summary>
        private bool StuckTooLong() {
            if ((int)AmbientClock % 20 == 0) {
                if (Math.Abs(NPC.Center.X - stuckRefX) < 4f) {
                    stuckTicks += 20;
                }
                else {
                    stuckTicks = 0;
                    stuckRefX = NPC.Center.X;
                }
            }
            return stuckTicks >= KiyumeYokaiMetrics.CortegeStuckDissolveTicks;
        }

        //==================== 状态机工具（ai 布局自定义，绕开基类默认槽位） ====================

        private void SetCortegeState(int state) {
            CortegeState = state;
            CortegeTimer = 0f;
            NPC.netUpdate = true;
        }

        /// <summary>状态沿：挂 ai[1]（镜像基类 StateEdge），出参上一观察态（迟入端 -1）</summary>
        private bool CortegeStateEdge(out int prev) {
            prev = (int)NPC.localAI[2] - 1;
            if (prev == (int)CortegeState) {
                return false;
            }
            NPC.localAI[2] = (int)CortegeState + 1;
            NPC.localAI[3] = 0f;
            return true;
        }

        //==================== 贴地 ====================

        /// <summary>贴地滑行：探针失败保持高度；服务器累计 200t 失败即全队入土（§2.4 回退）</summary>
        private void Glide() {
            if (CortegeShared.GlideToGround(NPC)) {
                probeFailTicks = 0;
                return;
            }
            if (!VaultUtils.isClient
                && ++probeFailTicks >= KiyumeYokaiMetrics.CortegeStuckDissolveTicks) {
                SetCortegeState(CortegeShared.StateSink);
            }
        }

        //==================== 表现（各端本地，由 ai 重放） ====================

        /// <summary>状态变迁沿音画（各端本地重放；迟入端首帧也吃到一次沿）</summary>
        private void PlayStateCue(int prev) {
            if (prev < 0
                && (int)CortegeState is CortegeShared.StateRage or CortegeShared.StateSink) {
                everRaged = true;
            }
            switch ((int)CortegeState) {
                case CortegeShared.StateTurn:
                    //全列停步那一拍：纸衣摩擦 + 帽下很低的一声
                    SoundEngine.PlaySound(SoundID.Grass with {
                        Volume = 0.55f,
                        Pitch = -0.5f,
                        MaxInstances = 2
                    }, NPC.Center);
                    SoundEngine.PlaySound(SoundID.ZombieMoan with {
                        Volume = 0.4f,
                        Pitch = -0.8f,
                        MaxInstances = 2
                    }, NPC.Center);
                    break;
                case CortegeShared.StateRage:
                    everRaged = true;
                    CoffinDropCue();
                    break;
                case CortegeShared.StateSink:
                    SoundEngine.PlaySound(SoundID.NPCDeath6 with {
                        Volume = 0.4f,
                        Pitch = -0.35f,
                        MaxInstances = 2
                    }, NPC.Center);
                    CortegeShared.EmitDissolveMist(NPC.Center, 6);
                    if (prev == CortegeShared.StateBow) {
                        CeremonialCue();
                    }
                    break;
            }
        }

        /// <summary>棺坠地拍（Rage 沿，各端本地）：震屏 3f + PRT_Smoke；prop 是否落地由服务器决定</summary>
        private void CoffinDropCue() {
            if (Main.dedServ) {
                return;
            }
            Vector2 at = CortegeShared.CarriedCoffinPos(NPC, marchDir);
            SoundEngine.PlaySound(SoundID.Dig with {
                Volume = 1f,
                Pitch = -0.85f,
                MaxInstances = 2
            }, at);
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(
                    at + Main.rand.NextVector2Circular(30f, 10f),
                    new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(0.2f, 0.9f)),
                    new Color(40, 34, 30), Main.rand.NextFloat(0.4f, 0.8f))
                    ?.Configure(Main.rand.Next(30, 55), 0.55f, Main.rand.NextFloat(-0.03f, 0.03f));
            }
            CortegeShared.ShakeNearby(at, 3f);
        }

        /// <summary>未惊扰收场（Bow→Sink 沿）：金纸钱慢落 ×12 + 一记远钟</summary>
        private void CeremonialCue() {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item35 with {
                Volume = 0.3f,
                Pitch = 0.6f,
                MaxInstances = 2
            }, NPC.Center);
            for (int i = 0; i < 12; i++) {
                PRTLoader.NewParticle<PRT_Spark>(
                    NPC.Top + new Vector2(Main.rand.NextFloat(-56f, 56f), -Main.rand.NextFloat(10f, 60f)),
                    new Vector2(Main.rand.NextFloat(-0.35f, 0.35f), Main.rand.NextFloat(0.2f, 0.5f)),
                    new Color(222, 182, 92), Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(false, Main.rand.Next(80, 130));
            }
        }

        /// <summary>敲铃：音画各端本地；迟缓服务器统一施加——服务器 AddBuff 不发包
        /// （对源实证：Player.AddBuff 只在 netMode==1 发 55 号），须显式补发给本人端</summary>
        private void RingBell() {
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item35 with {
                    Volume = 0.8f,
                    Pitch = 0.3f,
                    MaxInstances = 2
                }, NPC.Center);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(
                        LanternWorldPos() + Main.rand.NextVector2Circular(6f, 6f),
                        Main.rand.NextVector2Circular(1.2f, 0.8f) - new Vector2(0f, 0.4f),
                        new Color(214, 66, 44), Main.rand.NextFloat(0.4f, 0.6f))
                        ?.Configure(false, Main.rand.Next(20, 34));
                }
            }
            if (VaultUtils.isClient) {
                return;
            }
            //钟声挂上听觉地图（裁决回执：一家犬吠百家应）；每 90t 一响故量级压半防瞬锁
            Stealth.KiyumeStealthSense.ReportNoise(
                LanternWorldPos(), KiyumeHoundMetrics.WeaponImpulse * 0.5f);
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead
                    || player.Distance(NPC.Center) > KiyumeYokaiMetrics.CortegeBellRadius) {
                    continue;
                }
                player.AddBuff(BuffID.Slow, KiyumeYokaiMetrics.CortegeBellSlowTicks);
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.AddPlayerBuff, -1, -1, null,
                        player.whoAmI, BuffID.Slow, KiyumeYokaiMetrics.CortegeBellSlowTicks);
                }
            }
        }

        private void UpdatePresentation() {
            //现形语法：远处雾里剪影，入遭遇带强制现形；回头拍起不许再藏
            float fog = FogRevealTerm(NPC.Center);
            CortegeShared.NearestLivePlayer(NPC.Center, out float dist);
            float engage = 1f - DistanceRevealTerm(dist,
                CortegeShared.RevealNearPx, CortegeShared.RevealFarPx);
            float target = MathHelper.Lerp(fog, 1f, engage);
            if ((int)CortegeState is CortegeShared.StateTurn or CortegeShared.StateRage) {
                target = Math.Max(target, 0.92f);
            }
            presentAlpha = MathHelper.Lerp(presentAlpha, MathHelper.Clamp(target, 0f, 1f), 0.08f);

            //白灯笼是队列唯一光源：随转红换色，沉地随消散收口
            if (!Main.dedServ) {
                float red = Red01();
                float fade = 1f;
                if ((int)CortegeState == CortegeShared.StateSink) {
                    fade = Math.Max(0f, 1f - (int)CortegeTimer / (float)CortegeShared.SinkTicks);
                }
                Vector3 tint = Vector3.Lerp(new Vector3(0.72f, 0.70f, 0.62f),
                    new Vector3(0.85f, 0.16f, 0.10f), red);
                float mul = 0.8f * fade * (0.92f + 0.08f * MathF.Sin(AmbientClock * 0.09f + Seed));
                Lighting.AddLight(LanternWorldPos(), tint * mul);
            }
        }

        /// <summary>白灯笼烧红进度：回头拍起 30t 内 lerp 满（跨 Turn→Rage 连续）</summary>
        private float Red01() {
            int t = (int)CortegeTimer;
            return (int)CortegeState switch {
                CortegeShared.StateTurn => Math.Min(1f,
                    t / (float)KiyumeYokaiMetrics.CortegeLanternRedTicks),
                CortegeShared.StateRage => Math.Min(1f,
                    (KiyumeYokaiMetrics.CortegeTurnBeat + t)
                    / (float)KiyumeYokaiMetrics.CortegeLanternRedTicks),
                CortegeShared.StateSink => everRaged ? 1f : 0f,
                _ => 0f,
            };
        }

        /// <summary>帽下余烬：回头 20t 内 0→0.35（与提灯翁 Turn 拍同源峰值）</summary>
        private float EyeGlow01() {
            int t = (int)CortegeTimer;
            return (int)CortegeState switch {
                CortegeShared.StateTurn => KiyumeYokaiMetrics.LanternEyeGlowMax
                    * Math.Min(1f, t / (float)KiyumeYokaiMetrics.CortegeTurnBeat),
                CortegeShared.StateRage => KiyumeYokaiMetrics.LanternEyeGlowMax,
                CortegeShared.StateSink => everRaged
                    ? KiyumeYokaiMetrics.LanternEyeGlowMax * Math.Max(0f, 1f - t / 20f) : 0f,
                _ => 0f,
            };
        }

        /// <summary>纸衣蚀散：常态碎裾低位，沉地走满（同帧退场兜底在 UpdateSink）</summary>
        private float Dissolve01() {
            if ((int)CortegeState == CortegeShared.StateSink) {
                return MathHelper.Lerp(CortegeShared.PaperDissolveIdle, 1f,
                    MathHelper.Clamp((int)CortegeTimer / (float)CortegeShared.SinkTicks, 0f, 1f));
            }
            return CortegeShared.PaperDissolveIdle;
        }

        /// <summary>坟前一躬：前倾定格</summary>
        private float BowTilt() {
            if ((int)CortegeState != CortegeShared.StateBow) {
                return 0f;
            }
            return facing * 0.22f * Math.Min(1f, (int)CortegeTimer / 12f);
        }

        private Vector2 LanternWorldPos() => NPC.Center + new Vector2(facing * 12f, -34f);

        public override void FindFrame(int frameHeight) {
            if ((int)CortegeState == CortegeShared.StateTurn) {
                return;   //回头拍死寂：帧停
            }
            NPC.frameCounter += (int)CortegeState == CortegeShared.StateRage ? 1.6 : 1.0;
            if (NPC.frameCounter >= 11.0) {
                NPC.frameCounter = 0.0;
                NPC.frame.Y += frameHeight;
                if (NPC.frame.Y >= frameHeight * 3) {
                    NPC.frame.Y = 0;
                }
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            //纸身受击：灰屑无血；倒下（全队即散的钥匙）多给一撮
            if (Main.dedServ) {
                return;
            }
            if (NPC.life <= 0) {
                CortegeShared.EmitDissolveMist(NPC.Center, 10);
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                    DustID.Smoke, hit.HitDirection * 1.2f, -0.6f, 120, new Color(210, 202, 190), 0.9f);
                dust.noGravity = true;
            }
        }

        //==================== 绘制（全接管） ====================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //上游批状态自愈：先归位默认批
            BeginDefault(spriteBatch);
            if (presentAlpha >= 0.02f) {
                CortegeShared.DrawPaperBody(spriteBatch, screenPos, NPC, NPCID.RaggedCaster,
                    CortegeShared.FrameIndexOf(NPC, Type), facing, presentAlpha,
                    EyeGlow01(), Dissolve01(), BowTilt(), Seed);
                DrawBannerLantern(spriteBatch, screenPos);
            }
#if DEBUG
            Utils.DrawBorderString(spriteBatch,
                $"状态 {(int)CortegeState}  计时 {(int)CortegeTimer}",
                NPC.Top - screenPos + new Vector2(-28f, -34f),
                Color.LightGoldenrodYellow, 0.7f);
#endif
            return false;
        }

        /// <summary>幡杆 + 白幡 + 白灯笼（ChainLantern 摆锤，镜像 LanternGuideYokai 画法）</summary>
        private void DrawBannerLantern(SpriteBatch spriteBatch, Vector2 screenPos) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            Main.instance.LoadItem(ItemID.ChainLantern);
            Texture2D lantern = TextureAssets.Item[ItemID.ChainLantern]?.Value;
            if (px == null || lantern == null) {
                return;
            }
            float fade = presentAlpha;
            if ((int)CortegeState == CortegeShared.StateSink) {
                fade *= Math.Max(0f, 1f - (int)CortegeTimer / (float)CortegeShared.SinkTicks);
            }
            //灯先于人影可见（惨白一点先亮出来）
            float lampAlpha = MathHelper.Clamp(fade * 1.6f, 0f, 1f);
            if (lampAlpha < 0.02f) {
                return;
            }

            var src = new Rectangle(0, 0, 1, 1);
            Vector2 grip = NPC.Center + new Vector2(facing * 10f, 2f);
            Vector2 poleTop = LanternWorldPos();

            //幡杆一线
            Vector2 pole = poleTop - grip;
            spriteBatch.Draw(px, grip - screenPos, src, new Color(64, 52, 44) * lampAlpha,
                pole.ToRotation(), new Vector2(0f, 0.5f), new Vector2(pole.Length(), 2f),
                SpriteEffects.None, 0f);

            //白幡：杆顶垂布，随夜风微摆
            float sway = MathF.Sin(AmbientClock * 0.05f + Seed) * 0.12f + facing * 0.06f;
            spriteBatch.Draw(px, poleTop + new Vector2(facing * 3f, 1f) - screenPos, src,
                new Color(214, 206, 196) * (lampAlpha * 0.9f), sway,
                new Vector2(0.5f, 0f), new Vector2(6f, 26f), SpriteEffects.None, 0f);

            //白灯笼：挂杆顶，摆幅随步速；tint 随回头拍 30t 烧红
            float red = Red01();
            float rot = MathF.Sin(AmbientClock * 0.07f + Seed) * (0.1f + 0.08f * Math.Min(1f,
                Math.Abs(NPC.velocity.X) / KiyumeYokaiMetrics.CortegeWalkSpeed));
            Color body = Color.Lerp(new Color(226, 222, 214), new Color(186, 44, 32), red) * lampAlpha;
            spriteBatch.Draw(lantern, poleTop - screenPos, null, body,
                rot, new Vector2(lantern.Width * 0.5f, 2f), 0.8f, SpriteEffects.None, 0f);

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            //加色批：强度写进色值整体（A 随乘法收缩），禁 A=0
            BeginAdditive(spriteBatch);
            Vector2 flamePos = poleTop + new Vector2(0f, 12f).RotatedBy(rot) - screenPos;
            Vector2 gOrigin = glow.Size() * 0.5f;
            Color halo = Color.Lerp(new Color(240, 234, 210), new Color(255, 74, 46), red);
            Color core = Color.Lerp(new Color(255, 250, 236), new Color(255, 128, 96), red);
            spriteBatch.Draw(glow, flamePos, null, halo * (0.42f * lampAlpha), 0f, gOrigin,
                new Vector2(24f * 2f / glow.Width), SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, flamePos, null, core * (0.34f * lampAlpha), 0f, gOrigin,
                new Vector2(10f * 2f / glow.Width), SpriteEffects.None, 0f);
            BeginDefault(spriteBatch);
        }
    }

    /// <summary>
    /// 夜行列成员·抬棺人：从队首镜像状态与计时（各端本地逐帧，确定一致），
    /// 编队滑行架跟位；化煞转袭击体（3.4px/f 冲-刹 40t 循环，接触伤只在冲刺段——
    /// 伤害窗纪律，各端逐帧重设不依赖同步包字段）；队首消失=即散。<br/>
    /// 贴图按编队位混编：奇位 Necromancer / 偶位 NecromancerArmored（283/284，帧表同架）；
    /// 编队位 2 号共担画棺（黑漆木盒 88×30 双杠，随步相 bob ±2px，队首行进位相驱动）
    /// </summary>
    internal class FuneralCortegePorter : KiyumeYokaiNPC
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.Necromancer;

        //ai 布局（§2.4 冻结）：[0]=编队位(1..4) [1]=状态 [2]=计时 [3]=队首 whoAmI
        private ref float FormationSlot => ref NPC.ai[0];
        private ref float CortegeState => ref NPC.ai[1];
        private ref float CortegeTimer => ref NPC.ai[2];
        private ref float LeaderIndex => ref NPC.ai[3];

        //──── 各端本地表现 ────

        private float presentAlpha;
        private int facing = -1;
        private int marchDir = -1;
        private int dashDir;
        private bool everRaged;

        /// <summary>怪谈死亡语（{0}=玩家名；ToNetworkText 各端按己语解）</summary>
        private static LocalizedText wraithDeathReason;

        /// <summary>贴图混编：奇位褴褛法师、偶位重甲亡灵法师（同为三帧施法者架）</summary>
        private int VanillaType => (int)FormationSlot % 2 == 1
            ? NPCID.Necromancer : NPCID.NecromancerArmored;

        /// <summary>画棺共担：2 号位居中执笔（march 期无人可死，掉棒即已入化煞）</summary>
        private bool CoffinBearer => (int)FormationSlot == 2;

        protected override void SetYokaiStaticDefaults() {
            Main.npcFrameCount[Type] = 3;
            wraithDeathReason = this.GetLocalization("WraithDeathReason", () => "{0}挡了送葬的路");
        }

        protected override void SetYokaiDefaults() {
            NPC.width = 24;
            NPC.height = 46;
            NPC.damage = 0;          //默认零伤：接触窗只在化煞冲刺段逐帧设回
            NPC.defense = KiyumeYokaiMetrics.CortegeWraithDefense;
            NPC.lifeMax = KiyumeYokaiMetrics.CortegeWraithLife;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.HitSound = SoundID.NPCHit1 with { Volume = 0.5f, Pitch = -0.35f };
            NPC.DeathSound = SoundID.NPCDeath6 with { Volume = 0.45f, Pitch = -0.45f };
        }

        //==================== AI ====================

        protected override void YokaiAI() {
            HealAlpha(0);
            AmbientClock++;

            NPC leader = LeaderNPC();
            if (leader != null) {
                //从队首镜像状态与计时：沿的同帧性由队首 ai 过线保证（回头拍全列同帧翻的实现）
                CortegeState = leader.ai[1];
                CortegeTimer = leader.ai[2];
            }
            else if ((int)CortegeState != CortegeShared.StateSink) {
                //队首消失=即散（击杀执幡者全队即散的具体执行；各端确定重放）
                CortegeState = CortegeShared.StateSink;
                CortegeTimer = 0f;
            }
            else {
                CortegeTimer++;   //无首自走沉地
            }

            if (CortegeStateEdge(out int prev)) {
                PlayStateCue(prev);
            }
            ServerSyncPacer();
            NPC.velocity.Y = 0f;
            NPC.damage = 0;   //伤害窗纪律：每帧从状态重放，不依赖同步包字段

            switch ((int)CortegeState) {
                case CortegeShared.StateMarch:
                    UpdateMarch(leader);
                    break;
                case CortegeShared.StateTurn:
                    UpdateTurn(leader);
                    break;
                case CortegeShared.StateRage:
                    UpdateRage();
                    break;
                case CortegeShared.StateBow:
                    NPC.velocity.X = 0f;
                    break;
                default:
                    UpdateSink();
                    break;
            }

            NPC.direction = facing;
            NPC.spriteDirection = facing;
            UpdatePresentation();
        }

        private void UpdateMarch(NPC leader) {
            if (leader == null) {
                NPC.velocity.X = 0f;
                return;
            }
            if (Math.Abs(leader.velocity.X) > 0.01f) {
                marchDir = leader.velocity.X > 0f ? 1 : -1;
            }
            facing = marchDir;
            //纵列跟位：队首身后 slot×52px；追赶上限两倍步速，就位后自然同速
            float targetX = leader.Center.X
                - marchDir * KiyumeYokaiMetrics.CortegeSpacing * (int)FormationSlot;
            NPC.velocity.X = MathHelper.Clamp((targetX - NPC.Center.X) * 0.2f,
                -KiyumeYokaiMetrics.CortegeWalkSpeed * 2f,
                KiyumeYokaiMetrics.CortegeWalkSpeed * 2f);
            CortegeShared.GlideToGround(NPC);
        }

        private void UpdateTurn(NPC leader) {
            //回头拍：同帧静止 + 全列朝同一方向翻面（以队首为基准，各端同源）
            NPC.velocity.X = 0f;
            if (leader != null) {
                int dir = CortegeShared.ScareFaceDir(leader);
                if (dir != 0) {
                    facing = dir;
                }
            }
        }

        private void UpdateRage() {
            //冲-刹 40t 循环，按编队位错拍免四体齐撞；接触伤只在冲刺段
            int cyc = ((int)CortegeTimer + (int)FormationSlot * 10)
                % KiyumeYokaiMetrics.CortegeWraithDashPeriod;
            if (cyc == 0 || dashDir == 0) {
                Player prey = CortegeShared.NearestLivePlayer(NPC.Center, out _);
                if (prey != null) {
                    dashDir = Math.Sign(prey.Center.X - NPC.Center.X);
                }
                if (dashDir == 0) {
                    dashDir = facing;
                }
            }
            if (cyc < CortegeShared.DashWindowTicks) {
                NPC.velocity.X = dashDir * KiyumeYokaiMetrics.CortegeWraithSpeed;
                NPC.damage = KiyumeYokaiMetrics.CortegeWraithDamage;
                facing = dashDir;
            }
            else {
                NPC.velocity.X *= 0.8f;
            }
            CortegeShared.GlideToGround(NPC);
        }

        private void UpdateSink() {
            NPC.velocity.X = 0f;
            NPC.velocity.Y = CortegeShared.SinkSpeed;
            if (CortegeTimer >= CortegeShared.SinkTicks) {
                NPC.active = false;   //uDissolve 走满同帧退场
            }
        }

        //==================== 队首与状态沿 ====================

        /// <summary>队首解析：槽位 + 类型双验（会话内全场唯一队，index+type 即够；
        /// 槽位被他类复用 = 队首已亡，走即散，语义自洽）</summary>
        private NPC LeaderNPC() {
            int idx = (int)LeaderIndex;
            if (idx < 0 || idx >= Main.maxNPCs) {
                return null;
            }
            NPC leader = Main.npc[idx];
            return leader.active && leader.type == ModContent.NPCType<FuneralCortegeYokai>()
                ? leader : null;
        }

        private bool CortegeStateEdge(out int prev) {
            prev = (int)NPC.localAI[2] - 1;
            if (prev == (int)CortegeState) {
                return false;
            }
            NPC.localAI[2] = (int)CortegeState + 1;
            NPC.localAI[3] = 0f;
            return true;
        }

        private void PlayStateCue(int prev) {
            if (prev < 0
                && (int)CortegeState is CortegeShared.StateRage or CortegeShared.StateSink) {
                everRaged = true;   //迟入端保守按已惊处理（免与棺 prop 双棺）
            }
            switch ((int)CortegeState) {
                case CortegeShared.StateRage:
                    everRaged = true;
                    break;
                case CortegeShared.StateSink:
                    //音量收敛：五体齐沉靠 MaxInstances 封顶，主响在队首
                    SoundEngine.PlaySound(SoundID.NPCDeath6 with {
                        Volume = 0.3f,
                        Pitch = -0.35f,
                        MaxInstances = 2
                    }, NPC.Center);
                    CortegeShared.EmitDissolveMist(NPC.Center, 4);
                    break;
            }
        }

        //==================== 命中与死亡 ====================

        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers) {
            //怪谈死亡语：受害端本地改写死因（命中解算本就在被打端，原版路径）
            modifiers.ModifyHurtInfo += (ref Player.HurtInfo info) =>
                info.DamageSource = PlayerDeathReason.ByCustomReason(
                    wraithDeathReason.ToNetworkText(target.name));
        }

        public override void OnKill() {
            //一击致死不留血线证词：亲报队首（OnKill 只在权威端跑）
            if (LeaderNPC()?.ModNPC is FuneralCortegeYokai head) {
                head.ForceScare();
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            if (NPC.life <= 0) {
                CortegeShared.EmitDissolveMist(NPC.Center, 8);
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                    DustID.Smoke, hit.HitDirection * 1.2f, -0.6f, 120, new Color(210, 202, 190), 0.9f);
                dust.noGravity = true;
            }
        }

        //==================== 表现 ====================

        private void UpdatePresentation() {
            float fog = FogRevealTerm(NPC.Center);
            CortegeShared.NearestLivePlayer(NPC.Center, out float dist);
            float engage = 1f - DistanceRevealTerm(dist,
                CortegeShared.RevealNearPx, CortegeShared.RevealFarPx);
            float target = MathHelper.Lerp(fog, 1f, engage);
            if ((int)CortegeState is CortegeShared.StateTurn or CortegeShared.StateRage) {
                target = Math.Max(target, 0.92f);
            }
            presentAlpha = MathHelper.Lerp(presentAlpha, MathHelper.Clamp(target, 0f, 1f), 0.08f);
        }

        private float EyeGlow01() {
            int t = (int)CortegeTimer;
            return (int)CortegeState switch {
                CortegeShared.StateTurn => KiyumeYokaiMetrics.LanternEyeGlowMax
                    * Math.Min(1f, t / (float)KiyumeYokaiMetrics.CortegeTurnBeat),
                CortegeShared.StateRage => KiyumeYokaiMetrics.LanternEyeGlowMax,
                CortegeShared.StateSink => everRaged
                    ? KiyumeYokaiMetrics.LanternEyeGlowMax * Math.Max(0f, 1f - t / 20f) : 0f,
                _ => 0f,
            };
        }

        private float Dissolve01() {
            if ((int)CortegeState == CortegeShared.StateSink) {
                return MathHelper.Lerp(CortegeShared.PaperDissolveIdle, 1f,
                    MathHelper.Clamp((int)CortegeTimer / (float)CortegeShared.SinkTicks, 0f, 1f));
            }
            return CortegeShared.PaperDissolveIdle;
        }

        private float BowTilt() {
            if ((int)CortegeState != CortegeShared.StateBow) {
                return 0f;
            }
            return facing * 0.22f * Math.Min(1f, (int)CortegeTimer / 12f);
        }

        public override void FindFrame(int frameHeight) {
            if ((int)CortegeState == CortegeShared.StateTurn) {
                return;   //死寂帧停
            }
            NPC.frameCounter += (int)CortegeState == CortegeShared.StateRage ? 1.6 : 1.0;
            if (NPC.frameCounter >= 11.0) {
                NPC.frameCounter = 0.0;
                NPC.frame.Y += frameHeight;
                if (NPC.frame.Y >= frameHeight * 3) {
                    NPC.frame.Y = 0;
                }
            }
        }

        //==================== 绘制（全接管） ====================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            BeginDefault(spriteBatch);
            if (presentAlpha >= 0.02f) {
                //棺先画：杠身压在抬棺人身后一层
                if (CoffinBearer && CoffinCarried()) {
                    DrawCarriedCoffin(spriteBatch, screenPos);
                }
                CortegeShared.DrawPaperBody(spriteBatch, screenPos, NPC, VanillaType,
                    CortegeShared.FrameIndexOf(NPC, Type), facing, presentAlpha,
                    EyeGlow01(), Dissolve01(), BowTilt(), Seed);
            }
            return false;
        }

        /// <summary>棺还在肩上：化煞前全程 + 未惊扰的沉地收场（人和棺一起沉进土里）</summary>
        private bool CoffinCarried()
            => !everRaged && (int)CortegeState != CortegeShared.StateRage;

        private void DrawCarriedCoffin(SpriteBatch spriteBatch, Vector2 screenPos) {
            NPC leader = LeaderNPC();
            Vector2 at = leader != null
                ? CortegeShared.CarriedCoffinPos(leader, marchDir)
                : NPC.Center + new Vector2(0f, -20f);
            //步相 bob ±2px：以队首行进位相驱动（同步量，各端一致；停步自然定格）
            float phase = (leader?.Center.X ?? NPC.Center.X) * 0.11f;
            at.Y += MathF.Sin(phase) * 2f;

            float alpha = presentAlpha;
            if ((int)CortegeState == CortegeShared.StateSink) {
                alpha *= Math.Max(0f, 1f - (int)CortegeTimer / (float)CortegeShared.SinkTicks);
            }
            CortegeShared.DrawCoffin(spriteBatch, at - screenPos, alpha);
        }
    }

    /// <summary>
    /// 旧棺（事故的物证 prop）：回头拍序列坠地后生成（服务器，会话上限 2 具），
    /// 不可击打零伤害，留在原地直到离开梦境（基类 CheckActive=false + 梦外自杀兜底）。
    /// 自坠自落定：探地各端确定一致；落定拍走前进沿且只在见过下坠的端播（迟入端不补一响）
    /// </summary>
    internal class FuneralCortegeCoffin : KiyumeYokaiNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>本端见过下坠（迟入端为假：落定拍不重播）</summary>
        private bool fallingSeen;

        protected override void SetYokaiStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
        }

        protected override void SetYokaiDefaults() {
            NPC.width = 88;
            NPC.height = 30;
            NPC.damage = 0;
            NPC.defense = 0;
            NPC.lifeMax = 5;
            NPC.dontTakeDamage = true;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
        }

        protected override void YokaiAI() {
            HealAlpha(0);
            AmbientClock++;
            ServerSyncPacer(60);   //静物低频锚

            if (CortegeShared.TryFindGround(NPC.Center.X, NPC.Bottom.Y - 8f, out float ground)) {
                if (NPC.Bottom.Y < ground - 2f) {
                    fallingSeen = true;
                    NPC.velocity = new Vector2(0f, Math.Min(7f, NPC.velocity.Y + 0.5f));
                    return;
                }
                NPC.position.Y = ground - NPC.height;
            }
            NPC.velocity = Vector2.Zero;

            if (fallingSeen && BeatForward(1) && !Main.dedServ) {
                //落定轻响 + 几缕尘（坠地主响在回头拍序列里，这里只补落定）
                SoundEngine.PlaySound(SoundID.Dig with {
                    Volume = 0.45f,
                    Pitch = -0.7f,
                    MaxInstances = 2
                }, NPC.Center);
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_Smoke>(
                        NPC.Bottom + new Vector2(Main.rand.NextFloat(-38f, 38f), -4f),
                        new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.1f, 0.5f)),
                        new Color(40, 34, 30), Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(Main.rand.Next(24, 40), 0.45f, Main.rand.NextFloat(-0.02f, 0.02f));
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            BeginDefault(spriteBatch);
            CortegeShared.DrawCoffin(spriteBatch, NPC.Center - screenPos, 0.96f);
            return false;
        }
    }

    /// <summary>夜行列三类共用件：状态码、几何判定、纸衣/棺体画艺（魔法像素，零新 PNG）</summary>
    internal static class CortegeShared
    {
        //状态码（队首驱动，成员镜像）
        internal const int StateMarch = 0;
        internal const int StateTurn = 1;
        internal const int StateRage = 2;
        internal const int StateBow = 3;
        internal const int StateSink = 4;

        //沉地消散时长（uDissolve 满值同帧退场——P4-B 交付约定）与下沉速率
        internal const int SinkTicks = 46;
        internal const float SinkSpeed = 0.35f;
        //纸衣常值碎裾（uDissolve 低位）
        internal const float PaperDissolveIdle = 0.10f;
        //冲-刹 40t 循环里的冲刺段（伤害窗）
        internal const int DashWindowTicks = 18;
        //挡路走廊前探（与 ±CortegeBlockBand 路带配对；90t 驻留 + 0.8px/f 步速推得的够近尺度）
        internal const float BlockLookAheadPx = 150f;
        //现形语法带宽：遭遇带内强制现形（提灯翁同族极性）
        internal const float RevealNearPx = 260f;
        internal const float RevealFarPx = 900f;

        //纸衣主调 / 缘光（血暮系，与提灯翁同族）
        internal static readonly Vector3 PaperTint = new(0.88f, 0.84f, 0.78f);
        internal static readonly Vector3 EdgeTint = new Color(112, 26, 26).ToVector3();
        //眼锚：施法者帧兜帽位估值（281/283/284 同架；偏差待游戏内校，糊在兜帽内仍读作余烬）
        internal static readonly Vector2 EyeAnchor = new(0.45f, 0.28f);

        internal static Player NearestLivePlayer(Vector2 from, out float dist) {
            Player best = null;
            dist = float.MaxValue;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead) {
                    continue;
                }
                float d = player.Distance(from);
                if (d < dist) {
                    dist = d;
                    best = player;
                }
            }
            return best;
        }

        /// <summary>全列回头方向：以队首最近活人为基准（各端同源，全列同向同帧）</summary>
        internal static int ScareFaceDir(NPC leader) {
            Player nearest = NearestLivePlayer(leader.Center, out _);
            return nearest == null ? 0 : Math.Sign(nearest.Center.X - leader.Center.X);
        }

        //从起始高度向下探地表（镜像 KiyumeHoundShade/LanternGuideYokai）
        internal static bool TryFindGround(float x, float fromY, out float groundY) {
            int tileX = (int)(x / 16f);
            int tileY = (int)(fromY / 16f);
            for (int i = 0; i < 60; i++) {
                int y = tileY + i;
                if (!WorldGen.InWorld(tileX, y, 20)) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                    groundY = y * 16f;
                    return true;
                }
            }
            groundY = 0f;
            return false;
        }

        /// <summary>贴地滑行（tile 已同步，两端探针结果确定一致）；失败返回假由调用方兜底</summary>
        internal static bool GlideToGround(NPC npc) {
            if (TryFindGround(npc.Center.X, npc.Bottom.Y - 48f, out float ground)) {
                float targetY = ground - npc.height;
                npc.position.Y = MathHelper.Lerp(npc.position.Y, targetY, 0.25f);
                return true;
            }
            return false;
        }

        /// <summary>肩上棺位：在场抬棺人质心上方；全灭兜底队首身后一段</summary>
        internal static Vector2 CarriedCoffinPos(NPC leader, int marchDir) {
            int porterType = ModContent.NPCType<FuneralCortegePorter>();
            Vector2 sum = Vector2.Zero;
            int n = 0;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC other = Main.npc[i];
                if (other.active && other.type == porterType && (int)other.ai[3] == leader.whoAmI) {
                    sum += other.Center;
                    n++;
                }
            }
            if (n == 0) {
                return leader.Center + new Vector2(-marchDir * 130f, -20f);
            }
            return sum / n + new Vector2(0f, -20f);
        }

        /// <summary>本地距离门震屏（镜像 GaolDormantSkull.ShakeNearby）</summary>
        internal static void ShakeNearby(Vector2 from, float amount, float range = 900f) {
            if (Main.dedServ || Main.LocalPlayer?.active != true) {
                return;
            }
            if (Vector2.Distance(Main.LocalPlayer.Center, from) > range) {
                return;
            }
            Main.LocalPlayer.CWR()?.GetScreenShake(amount);
        }

        internal static void EmitDissolveMist(Vector2 center, int count) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    center + Main.rand.NextVector2Circular(14f, 20f),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-0.55f, -0.1f)),
                    new Color(196, 188, 178), Main.rand.NextFloat(0.24f, 0.4f))
                    ?.Configure(Main.rand.Next(50, 80));
            }
        }

        /// <summary>由自类帧表折算帧序（0..2）：混编贴图与自类贴图尺寸解耦</summary>
        internal static int FrameIndexOf(NPC npc, int modType) {
            Texture2D own = TextureAssets.Npc[modType]?.Value;
            if (own == null) {
                return 0;
            }
            int frameH = own.Height / Math.Max(1, Main.npcFrameCount[modType]);
            return frameH > 0 ? Math.Clamp(npc.frame.Y / frameH, 0, 2) : 0;
        }

        /// <summary>纸衣化身体（TechPaperGhost，参数链镜像 LanternGuideYokai.DrawPaperBody）；
        /// 着色器缺编回退近白剪影平涂。底中枢轴带旋转（坟前一躬用）</summary>
        internal static void DrawPaperBody(SpriteBatch spriteBatch, Vector2 screenPos, NPC npc,
            int vanillaType, int frameIdx, int facing, float alpha, float eyeGlow, float dissolve,
            float rotation, float seed) {
            Main.instance.LoadNPC(vanillaType);
            Texture2D tex = TextureAssets.Npc[vanillaType].Value;
            if (tex == null) {
                return;
            }
            int frameH = tex.Height / 3;
            //源矩形上下各内缩 1px + shader 帧界钳制，双通道防帧表渗色（提灯翁同款纪律）
            var source = new Rectangle(0, Math.Clamp(frameIdx, 0, 2) * frameH + 1,
                tex.Width, frameH - 2);
            Vector2 pivot = new(npc.Center.X, npc.Bottom.Y + 2f);
            Vector2 origin = new(tex.Width * 0.5f, source.Height);

            Effect fx = EffectLoader.KiyumeKaidan?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                //着色器缺编：近白剪影平涂回退（HoundShade 同款语义）
                SpriteEffects flip = facing > 0
                    ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                spriteBatch.Draw(tex, pivot - screenPos, source,
                    new Color(214, 206, 196) * (alpha * 0.85f),
                    rotation, origin, 1f, flip, 0f);
                return;
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uUvRect"]?.SetValue(new Vector4(
                0f, source.Y / (float)tex.Height, 1f, source.Height / (float)tex.Height));
            fx.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            fx.Parameters["uAspect"]?.SetValue(tex.Width / (float)source.Height);
            fx.Parameters["uFlipH"]?.SetValue(facing > 0 ? 1f : 0f);
            fx.Parameters["uFlipV"]?.SetValue(0f);
            fx.Parameters["uEyeGlow"]?.SetValue(eyeGlow);
            fx.Parameters["uEyeAnchor"]?.SetValue(EyeAnchor);
            fx.Parameters["uDissolve"]?.SetValue(dissolve);
            fx.Parameters["uEdgeTint"]?.SetValue(EdgeTint);
            fx.Parameters["uPaperTint"]?.SetValue(PaperTint);
            fx.Parameters["uFaceRect"]?.SetValue(Vector4.Zero);   //纸衣 pass 不吃面区，显式清零
            fx.CurrentTechnique = fx.Techniques["TechPaperGhost"];
            fx.CurrentTechnique.Passes[0].Apply();

            spriteBatch.Draw(tex, pivot - screenPos, source,
                Color.White * MathHelper.Clamp(alpha * 1.25f, 0f, 1f),
                rotation, origin, 1f, SpriteEffects.None, 0f);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
            gd.Textures[1] = null;
        }

        /// <summary>黑漆木棺 88×30 双杠（魔法像素画布，杠头出体两端；抬棺画法与落地 prop 共用）</summary>
        internal static void DrawCoffin(SpriteBatch spriteBatch, Vector2 screenCenter, float alpha) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || alpha < 0.02f) {
                return;
            }
            var src = new Rectangle(0, 0, 1, 1);
            void Box(float dx, float dy, float w, float h, Color color) =>
                spriteBatch.Draw(px, screenCenter + new Vector2(dx - w * 0.5f, dy - h * 0.5f),
                    src, color, 0f, Vector2.Zero, new Vector2(w, h), SpriteEffects.None, 0f);
            //双杠：远深近浅
            Box(0f, 9f, 116f, 3f, new Color(34, 26, 20) * (alpha * 0.85f));
            Box(0f, 13f, 118f, 3f, new Color(52, 40, 30) * alpha);
            //黑漆木体 + 盖沿 + 一线漆光
            Box(0f, 0f, 88f, 30f, new Color(16, 12, 14) * alpha);
            Box(0f, -12f, 92f, 5f, new Color(34, 24, 26) * alpha);
            Box(0f, -7f, 82f, 2f, new Color(64, 44, 48) * (alpha * 0.7f));
        }
    }
}
