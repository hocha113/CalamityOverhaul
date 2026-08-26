using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.NPCs
{
    /// <summary>
    /// 循迹猎犬：它看不见你，但你 12 秒内走过的每一步都是它的路。
    /// 环形缓冲记录目标玩家足迹（48 点 × 15t 采样 = 12s 记忆），逐点飞行；
    /// 追上就仰头嚎叫 90t，成功 = +18 噪 + NotifySpotted 上门一队猎杀。
    /// 反制三条路：打断嚎叫（累伤 120）、回踩造路径自交（Confused 90t + 剪旧段）、
    /// 拉开 12 秒路程让缓冲彻底翻篇（失锚 → 嗅探 → 离场）。
    /// 无接触伤害：它是叫家长的，不是打手。零贴图程序化绘制
    /// </summary>
    internal class OldNetTracerICE : ModNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //ai[0] 状态位
        private const int StateCast = 0;
        private const int StateTrack = 1;
        private const int StateHowl = 2;
        private const int StateStagger = 3;
        private const int StateSniff = 4;
        private const int StateConfused = 5;
        private const int StateLeave = 6;

        /// <summary>ai[0]：状态</summary>
        private ref float State => ref NPC.ai[0];
        /// <summary>ai[1]：状态计时</summary>
        private ref float StateTimer => ref NPC.ai[1];
        /// <summary>ai[2]：嚎叫冷却倒数（贴身跟随不嚎的窗口）</summary>
        private ref float HowlCooldown => ref NPC.ai[2];

        //足迹缓冲为非同步实例字段（BlackICE.lastKnownPos 先例）
        //TODO MP: 联机化走 SendExtraAI 或服务器权威 trail
        private readonly Vector2[] trail = new Vector2[OldNetMetrics.TracerTrailCap];
        private long writeN;
        private long consumeN;
        private int sampleTimer;
        //嚎叫打断累伤基准（进 Howl 时快照生命值）
        private int howlStartLife;

        private float Seed => NPC.whoAmI * 0.591f;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NPCBestiaryDrawModifiers hide = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, hide);
            NPCID.Sets.MustAlwaysDraw[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 36;
            NPC.height = 24;
            //无接触伤害：威胁全在那一嗓子
            NPC.damage = 0;
            NPC.defense = OldNetMetrics.TracerDefense;
            NPC.lifeMax = OldNetMetrics.TracerLife;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            //数据体穿墙，但 trail 本身是玩家可达空间连线，视觉上基本不穿
            NPC.noTileCollide = true;
            NPC.value = 0;
            NPC.npcSlots = 0.4f;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
        }

        public override void AI() {
            //旧网门控：绝不泄漏到主世界与其他子世界
            if (!OldNetWorld.Active) {
                NPC.active = false;
                return;
            }
            if ((int)State == StateLeave) {
                UpdateLeave();
                return;
            }

            NPC.TargetClosest(faceTarget: false);
            Player player = Main.player[NPC.target];
            if (player == null || !player.active || player.dead) {
                BeginLeave();
                return;
            }

            if (HowlCooldown > 0f) {
                HowlCooldown--;
            }
            SampleTrail(player);

            switch ((int)State) {
                case StateTrack:
                    UpdateTrack(player);
                    break;
                case StateHowl:
                    UpdateHowl(player);
                    break;
                case StateStagger:
                    UpdateStagger();
                    break;
                case StateSniff:
                    UpdateSniff();
                    break;
                case StateConfused:
                    UpdateConfused();
                    break;
                default:
                    UpdateCast();
                    break;
            }

            //鼻尖琥珀微光
            Lighting.AddLight(NPC.Center, 0.16f, 0.10f, 0.02f);
        }

        //──── 足迹采样：15t 一点，防抖跳过，自交检测 ────

        private void SampleTrail(Player player) {
            if (++sampleTimer < OldNetMetrics.TracerSampleTicks) {
                return;
            }
            sampleTimer = 0;
            Vector2 pos = player.Center;

            //防抖：与最近一点太近就不记（站桩玩家不刷点）
            if (writeN > 0) {
                Vector2 last = trail[(int)((writeN - 1) % trail.Length)];
                if (Vector2.Distance(last, pos) < OldNetMetrics.TracerPointSkipDist) {
                    return;
                }
            }

            //路径自交检测：新点落在旧段附近 = 玩家回踩造假径（跳过相邻 2 段防误报）
            //O(48)/采样，开销可忽略
            if ((int)State == StateTrack || (int)State == StateSniff) {
                long lo = Math.Max(consumeN, writeN - trail.Length);
                for (long i = lo; i < writeN - 3; i++) {
                    Vector2 a = trail[(int)(i % trail.Length)];
                    Vector2 b = trail[(int)((i + 1) % trail.Length)];
                    if (DistToSegment(pos, a, b) < OldNetMetrics.TracerCrossDist) {
                        BeginConfused(i);
                        break;
                    }
                }
            }

            trail[(int)(writeN % trail.Length)] = pos;
            writeN++;
        }

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b) {
            Vector2 ab = b - a;
            float lenSq = ab.LengthSquared();
            if (lenSq < 0.001f) {
                return Vector2.Distance(p, a);
            }
            float t = MathHelper.Clamp(Vector2.Dot(p - a, ab) / lenSq, 0f, 1f);
            return Vector2.Distance(p, a + ab * t);
        }

        //trail 目标点是否仍在缓冲有效窗内
        private bool TrailValid => consumeN >= writeN - trail.Length && consumeN < writeN;

        //──── 入场：40t 落地嗅探转圈，正好攒下首批足迹 ────

        private void UpdateCast() {
            float ang = StateTimer * 0.22f + Seed;
            NPC.velocity = new Vector2(MathF.Cos(ang), MathF.Sin(ang) * 0.5f) * 1.8f;
            NPC.direction = NPC.spriteDirection = NPC.velocity.X >= 0f ? 1 : -1;
            if (++StateTimer >= OldNetMetrics.TracerCastTicks) {
                State = StateTrack;
                StateTimer = 0f;
                NPC.netUpdate = true;
            }
        }

        //──── 循迹：沿足迹逐点飞行 ────

        private void UpdateTrack(Player player) {
            //足迹被时间冲掉（玩家拉开 >12s 路程）→ 失锚嗅探
            if (consumeN < writeN - trail.Length) {
                State = StateSniff;
                StateTimer = 0f;
                NPC.velocity *= 0.5f;
                NPC.netUpdate = true;
                return;
            }

            //嚎叫触发：与玩家实际距离近且通视（冷却期贴身跟随不嚎）
            float distToPlayer = Vector2.Distance(player.Center, NPC.Center);
            if (distToPlayer < OldNetMetrics.TracerHowlRange && HowlCooldown <= 0f
                && Collision.CanHitLine(NPC.position, NPC.width, NPC.height,
                    player.position, player.width, player.height)) {
                BeginHowl(player);
                return;
            }

            if (!TrailValid) {
                //缓冲追平（玩家在附近但没新脚印）：原地压速等下一点
                NPC.velocity *= 0.92f;
                return;
            }

            Vector2 goal = trail[(int)(consumeN % trail.Length)];
            Vector2 toGoal = goal - NPC.Center;
            if (toGoal.Length() < OldNetMetrics.TracerPointSkipDist) {
                consumeN++;
                return;
            }
            NPC.velocity = Vector2.Lerp(NPC.velocity,
                toGoal.SafeNormalize(Vector2.UnitX) * OldNetMetrics.TracerTrackSpeed, 0.08f);
            NPC.direction = NPC.spriteDirection = NPC.velocity.X >= 0f ? 1 : -1;
        }

        //──── 嚎叫：站桩仰头 90t 充能，可打断 ────

        private void BeginHowl(Player player) {
            State = StateHowl;
            StateTimer = 0f;
            howlStartLife = NPC.life;
            NPC.netUpdate = true;
            //首次嚎叫红字警告：教打断（会话一次性）
            if (OldNetICEDirector.TryMarkHintOnce(OldNetICEDirector.HintTracerHowl)
                && player.whoAmI == Main.myPlayer) {
                CombatText.NewText(player.getRect(), new Color(235, 64, 44),
                    OldNetTexts.TracerHowlWarn.Value, dramatic: true);
            }
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.DD2_DarkMageCastHeal with { Volume = 0.7f, Pitch = -0.6f },
                    NPC.Center);
            }
        }

        private void UpdateHowl(Player player) {
            NPC.velocity *= 0.85f;

            //打断①：充能期累伤达标 → 硬直
            if (howlStartLife - NPC.life >= OldNetMetrics.TracerHowlInterruptDamage) {
                State = StateStagger;
                StateTimer = 0f;
                NPC.netUpdate = true;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.8f, Pitch = -0.4f }, NPC.Center);
                }
                return;
            }
            //打断②：玩家拉开距离 → 白嚎一场，硬直收尾
            if (Vector2.Distance(player.Center, NPC.Center) > OldNetMetrics.TracerHowlBreakRange) {
                State = StateStagger;
                StateTimer = 0f;
                NPC.netUpdate = true;
                return;
            }

            //充能滴答（渐密，可听的读秒）
            if ((int)StateTimer % 15 == 1 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.MenuTick with {
                    Volume = 0.4f,
                    Pitch = -0.2f + StateTimer / OldNetMetrics.TracerHowlTicks * 0.8f
                }, NPC.Center);
            }

            if (++StateTimer < OldNetMetrics.TracerHowlTicks) {
                return;
            }

            //嚎叫成功：点亮玩家 + 上门一队（NotifySpotted 编成不动，纯加法调用）
            OldNetPlayer.Get(player).AddNoise(OldNetMetrics.TracerHowlNoise);
            OldNetICEDirector.NotifySpotted(player);
            HowlCooldown = OldNetMetrics.TracerHowlCooldownTicks;
            State = StateTrack;
            StateTimer = 0f;
            NPC.netUpdate = true;
            if (!Main.dedServ) {
                SoundEngine.PlaySound(CWRSound.FaultOccurred with { Volume = 0.8f, Pitch = -0.5f },
                    NPC.Center);
            }
        }

        //──── 硬直：60t 垂头 ────

        private void UpdateStagger() {
            NPC.velocity *= 0.9f;
            if (++StateTimer >= OldNetMetrics.TracerStaggerTicks) {
                State = StateTrack;
                StateTimer = 0f;
                NPC.netUpdate = true;
            }
        }

        //──── 失锚嗅探：原地螺旋 180t，缓冲点落进重获半径即重上线索 ────

        private void UpdateSniff() {
            float ang = StateTimer * 0.15f + Seed;
            float radius = 30f + StateTimer * 0.35f;
            NPC.velocity = Vector2.Lerp(NPC.velocity,
                new Vector2(MathF.Cos(ang), MathF.Sin(ang) * 0.6f) * (radius * 0.05f), 0.1f);
            NPC.direction = NPC.spriteDirection = NPC.velocity.X >= 0f ? 1 : -1;

            //重获：有效窗内任一足迹点落在重获半径内 → 从那一点续上
            long lo = Math.Max(0, writeN - trail.Length);
            for (long i = lo; i < writeN; i++) {
                Vector2 pt = trail[(int)(i % trail.Length)];
                if (Vector2.Distance(pt, NPC.Center) < OldNetMetrics.TracerReacquireRange) {
                    consumeN = i;
                    State = StateTrack;
                    StateTimer = 0f;
                    NPC.netUpdate = true;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = 0.7f },
                            NPC.Center);
                    }
                    return;
                }
            }

            if (++StateTimer >= OldNetMetrics.TracerSniffTicks) {
                BeginLeave();
            }
        }

        //──── 被骗：路径自交 90t 打转，剪掉交叉点之前的旧段 ────

        private void BeginConfused(long crossIndex) {
            //裁掉交叉点之前的旧段：玩家绕的那个圈直接作废
            consumeN = Math.Max(consumeN, crossIndex);
            State = StateConfused;
            StateTimer = 0f;
            NPC.netUpdate = true;
            //首次回踩成功的一次性正反馈（技巧型知识要立刻确认）
            Player local = Main.LocalPlayer;
            if (OldNetICEDirector.TryMarkHintOnce(OldNetICEDirector.HintTracerConfused)
                && local?.active == true) {
                CombatText.NewText(local.getRect(), new Color(120, 255, 170),
                    OldNetTexts.TracerConfused.Value);
            }
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.6f, Pitch = -0.9f }, NPC.Center);
            }
        }

        private void UpdateConfused() {
            //原地打转：可读的"被骗"演出
            float ang = StateTimer * 0.3f + Seed;
            NPC.velocity = new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * 1.5f;
            NPC.direction = NPC.spriteDirection = NPC.velocity.X >= 0f ? 1 : -1;
            if (++StateTimer >= OldNetMetrics.TracerConfusedTicks) {
                State = StateTrack;
                StateTimer = 0f;
                NPC.netUpdate = true;
            }
        }

        //──── 离场 ────

        private void BeginLeave() {
            State = StateLeave;
            NPC.netUpdate = true;
        }

        private void UpdateLeave() {
            NPC.velocity = Vector2.Lerp(NPC.velocity,
                new Vector2(-OldNetMetrics.TracerTrackSpeed, 0f), 0.05f);
            NPC.direction = NPC.spriteDirection = -1;
            NPC.EncourageDespawn(10);
        }

        public override void OnKill() {
            //击杀噪音：比巡逻便宜，比缢影贵（它有上门能力）
            int idx = NPC.lastInteraction;
            Player killer = idx >= 0 && idx < Main.maxPlayers ? Main.player[idx] : null;
            if (killer?.active != true) {
                killer = Main.LocalPlayer;
            }
            if (killer?.active == true) {
                OldNetPlayer.Get(killer).AddNoise(OldNetMetrics.NoiseTracerKill);
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < (NPC.life <= 0 ? 14 : 4); i++) {
                Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                    DustID.Electric, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f));
                dust.noGravity = true;
                dust.scale = Main.rand.NextFloat(0.5f, 1f);
            }
        }

        //──── 程序化绘制：低伏四足犬形 + 足迹虚线 + 嚎叫充能弧 ────
        //全接管绘制：透明度全程显式给值，不依赖原版出生 alpha 自愈

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            Vector2 center = NPC.Center - screenPos;
            Vector2 origin = new(px.Width * 0.5f, px.Height * 0.5f);
            float t = Main.GlobalTimeWrappedHourly;
            int state = (int)State;
            int dir = NPC.direction >= 0 ? 1 : -1;

            Color body = new(14, 8, 12);
            Color edge = new(200, 40, 40);
            Color amber = new(255, 170, 60);
            bool confused = state == StateConfused;
            bool howling = state == StateHowl;
            float howlFrac = howling
                ? MathHelper.Clamp(StateTimer / OldNetMetrics.TracerHowlTicks, 0f, 1f) : 0f;

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //正在消费的足迹段：渐隐脚印虚线（玩家看得见"它在闻什么"，回踩反制因此可学）
            long lo = Math.Max(consumeN, writeN - trail.Length);
            for (int k = 0; k < 8; k++) {
                long idx = lo + k;
                if (idx >= writeN) {
                    break;
                }
                Vector2 pt = trail[(int)(idx % trail.Length)];
                float fade = 0.15f * (1f - k / 9f);
                spriteBatch.Draw(px, pt - screenPos, null, amber * fade,
                    MathHelper.PiOver4, origin, Size(4f, 4f), SpriteEffects.None, 0f);
            }

            //三段脊柱：前低后高的低伏姿态（i=2 为前段）；嚎叫期前端抬起
            float lift = howling ? 0.5f * howlFrac : 0f;
            float trot = MathF.Sin(t * 9f + Seed) * 0.06f;
            for (int i = 0; i < 3; i++) {
                float segX = (i - 1) * 9f * dir;
                float segY = (i - 1) * 1.8f - (i == 2 ? lift * 9f : 0f);
                float segAng = trot + (i == 2 ? -lift * 0.9f * dir : 0.12f * dir);
                spriteBatch.Draw(px, center + new Vector2(segX, segY), null, body, segAng,
                    origin, Size(11f, 5.5f - i * 0.8f), SpriteEffects.None, 0f);
                spriteBatch.Draw(px, center + new Vector2(segX, segY - 2f), null, edge * 0.35f,
                    segAng, origin, Size(9f, 1.2f), SpriteEffects.None, 0f);
            }

            //头部楔形（鼻尖朝向）：嚎叫仰起，困惑低垂乱摆
            float headAng = howling ? -0.9f * dir * howlFrac
                : confused ? MathF.Sin(t * 12f) * 0.5f : 0.1f * dir;
            Vector2 headPos = center + new Vector2(14f * dir, -3f - lift * 10f);
            spriteBatch.Draw(px, headPos, null, body, headAng,
                origin, Size(9f, 4f), SpriteEffects.None, 0f);

            //四条相位交替摆动短腿（小跑感）
            for (int leg = 0; leg < 4; leg++) {
                float phase = t * 11f + leg * MathHelper.PiOver2 + Seed;
                float swing = MathF.Sin(phase) * 0.5f;
                Vector2 hip = center + new Vector2((leg - 1.5f) * 7f * dir, 3f);
                spriteBatch.Draw(px, hip + new Vector2(swing * 3f, 4f), null, body * 0.9f,
                    MathHelper.PiOver2 + swing * 0.6f, origin, Size(7f, 2f), SpriteEffects.None, 0f);
            }

            //鼻尖光点（A=0 加色亮层）：琥珀=循迹中，灰=被骗
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex != null) {
                Vector2 nose = headPos + new Vector2(6f * dir, 0f);
                Color noseCol = confused ? new Color(120, 120, 120) : amber;
                Color noseGlow = noseCol * (0.5f + 0.2f * MathF.Sin(t * 6f + Seed));
                noseGlow.A = 0;
                spriteBatch.Draw(glowTex, nose, null, noseGlow, 0f,
                    glowTex.Size() * 0.5f, 0.12f, SpriteEffects.None, 0f);
            }

            //困惑期：周身乱序碎片抖动
            if (confused) {
                for (int i = 0; i < 5; i++) {
                    float ph = t * 7f + i * 1.3f + Seed;
                    Vector2 off = new(MathF.Sin(ph) * 14f, MathF.Cos(ph * 1.7f) * 9f);
                    spriteBatch.Draw(px, center + off, null, edge * 0.3f, ph,
                        origin, Size(3f, 1.5f), SpriteEffects.None, 0f);
                }
            }

            //嚎叫充能弧：头顶 12 段（镜像巡逻充能条语汇，读得出打断窗口）
            if (howling) {
                int litSegs = (int)(howlFrac * 12f);
                for (int i = 0; i < 12; i++) {
                    float ang = MathHelper.Pi + i / 11f * MathHelper.Pi;
                    Vector2 segPos = center + ang.ToRotationVector2() * 26f + new Vector2(0f, -6f);
                    Color segCol = i < litSegs ? edge : body * 1.6f;
                    spriteBatch.Draw(px, segPos, null, segCol * 0.85f, ang + MathHelper.PiOver2,
                        origin, Size(2f, 5f), SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }
}
