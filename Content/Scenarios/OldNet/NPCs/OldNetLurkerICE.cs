using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RAMSystems;
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
    /// 缢影 ICE：吊在头顶阴影里的垂落伏击者。慢速通过不触发（与巡逻潜行门共用
    /// "慢=安全"语法），快速从猎杀漏斗下方穿过则 12t 颤抖前摇后垂直俯冲一口
    /// （唯一伤害窗），命中或坠程用尽即沿丝回卷，回卷是它的处决窗口。
    /// 任意隐蔽状态受击直接转俯冲：打它=逼它现身。击杀 +8 噪（它不报信）。
    /// 全家族最脆（400 血），威胁模型是"一口"不是"纠缠"。零贴图程序化绘制
    /// </summary>
    internal class OldNetLurkerICE : ModNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //ai[2] 状态位
        private const int StateDormant = 0;
        private const int StateArm = 1;
        private const int StateDrop = 2;
        private const int StateReel = 3;

        /// <summary>ai[0]：丝根世界 X（director 布防时写入）</summary>
        private ref float AnchorX => ref NPC.ai[0];
        /// <summary>ai[1]：丝根世界 Y（吊面底缘）</summary>
        private ref float AnchorY => ref NPC.ai[1];
        /// <summary>ai[2]：状态（休眠/前摇/俯冲/回卷）</summary>
        private ref float State => ref NPC.ai[2];
        /// <summary>ai[3]：状态计时（休眠期=再触发冷却倒数）</summary>
        private ref float StateTimer => ref NPC.ai[3];
        /// <summary>localAI[0]：出生初始化旗标（吊位对齐一次）</summary>
        private ref float Initialized => ref NPC.localAI[0];

        private float Seed => NPC.whoAmI * 0.733f;

        /// <summary>吊挂静止位（丝根正下方）</summary>
        private Vector2 HangPos => new(AnchorX, AnchorY + OldNetMetrics.LurkerHangOffset);

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NPCBestiaryDrawModifiers hide = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, hide);
            NPCID.Sets.MustAlwaysDraw[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 22;
            NPC.height = 22;
            //平时零伤害，仅俯冲窗口设回（Wraith 门控惯例）
            NPC.damage = 0;
            NPC.defense = OldNetMetrics.LurkerDefense;
            NPC.lifeMax = OldNetMetrics.LurkerLife;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            //俯冲要被地板挡住（坠到底=空口），仅回卷期穿行
            NPC.noTileCollide = false;
            NPC.value = 0;
            NPC.npcSlots = 0.2f;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
        }

        //一次性布防的陷阱体：不参与原版远离despawn（门控自杀兜底离场）
        public override bool CheckActive() => false;

        public override void AI() {
            //旧网门控：绝不泄漏到主世界与其他子世界
            if (!OldNetWorld.Active) {
                NPC.active = false;
                return;
            }
            //出生兜底：director 正常写锚点，异常生成用当前位置自锚
            if (AnchorX <= 0f) {
                AnchorX = NPC.Center.X;
                AnchorY = NPC.Center.Y - OldNetMetrics.LurkerHangOffset;
            }
            if (Initialized == 0f) {
                Initialized = 1f;
                NPC.Center = HangPos;
            }

            NPC.TargetClosest(faceTarget: false);
            Player player = Main.player[NPC.target];
            bool hasTarget = player != null && player.active && !player.dead;

            //受击强制现身：隐蔽期被打即坠落（射击鉴别有代价也有收益）；
            //俯冲/回卷中不重触发，处决窗口不被自身机制吞掉
            if (NPC.justHit && (int)State != StateDrop && (int)State != StateReel) {
                BeginDrop();
            }

            switch ((int)State) {
                case StateArm:
                    UpdateArm();
                    break;
                case StateDrop:
                    UpdateDrop();
                    break;
                case StateReel:
                    UpdateReel();
                    break;
                default:
                    UpdateDormant(player, hasTarget);
                    break;
            }

            //休眠零光照（伏击者不发光），现身后微红
            if ((int)State != StateDormant) {
                Lighting.AddLight(NPC.Center, 0.18f, 0.03f, 0.03f);
            }
        }

        //──── 休眠：吊挂静止，漏斗判定 ────

        private void UpdateDormant(Player player, bool hasTarget) {
            NPC.damage = 0;
            NPC.velocity = Vector2.Zero;
            NPC.Center = HangPos;

            //再触发冷却
            if (StateTimer > 0f) {
                StateTimer--;
                return;
            }
            if (!hasTarget) {
                return;
            }

            //猎杀漏斗：水平 ±80px、体下 220px；慢速通过不触发（潜行语法同巡逻）
            //TODO MP: 漏斗触发为本地裁决，联机化移交服务器
            Vector2 hang = HangPos;
            bool inFunnel = MathF.Abs(player.Center.X - hang.X) < OldNetMetrics.LurkerFunnelHalfWidth
                && player.Center.Y > hang.Y
                && player.Center.Y - hang.Y < OldNetMetrics.LurkerFunnelDepth;
            if (!inFunnel || player.velocity.Length() < OldNetMetrics.PatrolSneakSpeedGate) {
                return;
            }
            //公平阀：隔着地形不偷袭
            if (!Collision.CanHitLine(NPC.position, NPC.width, NPC.height,
                player.position, player.width, player.height)) {
                return;
            }

            State = StateArm;
            StateTimer = OldNetMetrics.LurkerArmTicks;
            NPC.netUpdate = true;
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = -0.8f }, NPC.Center);
            }
        }

        //──── 前摇：12t 颤抖 + 红缘闪烁（公平阀）────

        private void UpdateArm() {
            NPC.damage = 0;
            NPC.velocity = Vector2.Zero;
            //颤抖：绕吊位高频抖动
            NPC.Center = HangPos + new Vector2(
                MathF.Sin(StateTimer * 2.7f + Seed) * 1.6f, MathF.Sin(StateTimer * 3.9f) * 1.2f);
            if (--StateTimer <= 0f) {
                BeginDrop();
            }
        }

        private void BeginDrop() {
            State = StateDrop;
            StateTimer = 0f;
            NPC.damage = OldNetMetrics.LurkerContactDamage;
            NPC.noTileCollide = false;
            NPC.velocity = new Vector2(0f, OldNetMetrics.LurkerDropSpeed);
            NPC.netUpdate = true;
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.6f, Pitch = -0.5f }, NPC.Center);
            }
        }

        //──── 俯冲：垂直坠落，接触窗口唯一开启 ────

        private void UpdateDrop() {
            //撞地判定必须在覆写速度之前：collideY 由上帧移动结算写入，
            //覆写后再读永远是假（审查实锤的死码根因）
            bool blocked = NPC.collideY;
            bool spent = NPC.Center.Y - HangPos.Y >= OldNetMetrics.LurkerDropMaxDist;
            //硬超时兜底：房间内膛比坠程浅、或任何卡位，40t 内必收进回卷
            if (blocked || spent || ++StateTimer > OldNetMetrics.LurkerDropTimeoutTicks) {
                BeginReel();
                return;
            }
            NPC.velocity = new Vector2(0f, OldNetMetrics.LurkerDropSpeed);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            //TODO MP: 本钩子只在被打端跑，RAM 扣减联机化走请求包
            RamSystem.TryConsume(target, OldNetMetrics.LurkerBiteRam, out _);
            //单口即回卷：命中就走，不纠缠
            if ((int)State == StateDrop) {
                BeginReel();
            }
        }

        private void BeginReel() {
            State = StateReel;
            StateTimer = 0f;
            NPC.damage = 0;
            //回卷沿丝爬升：数据体穿行，免卡台阶
            NPC.noTileCollide = true;
            NPC.netUpdate = true;
        }

        //──── 回卷：慢速爬回吊位，全程可打（处决窗口）────

        private void UpdateReel() {
            Vector2 hang = HangPos;
            Vector2 toHang = hang - NPC.Center;
            //到位或兜底超时：直接归位入眠
            if (toHang.Length() < 4f || ++StateTimer > OldNetMetrics.LurkerReelTimeoutTicks) {
                NPC.Center = hang;
                NPC.velocity = Vector2.Zero;
                NPC.noTileCollide = false;
                State = StateDormant;
                StateTimer = OldNetMetrics.LurkerCooldownTicks;
                NPC.netUpdate = true;
                return;
            }
            NPC.velocity = toHang.SafeNormalize(-Vector2.UnitY) * OldNetMetrics.LurkerReelSpeed;
        }

        public override void OnKill() {
            //击杀噪音：伏击者不报信，代价低于巡逻
            int idx = NPC.lastInteraction;
            Player killer = idx >= 0 && idx < Main.maxPlayers ? Main.player[idx] : null;
            if (killer?.active != true) {
                killer = Main.LocalPlayer;
            }
            if (killer?.active == true) {
                OldNetPlayer.Get(killer).AddNoise(OldNetMetrics.NoiseLurkerKill);
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < (NPC.life <= 0 ? 12 : 3); i++) {
                Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                    DustID.Electric, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f));
                dust.noGravity = true;
                dust.scale = Main.rand.NextFloat(0.5f, 0.9f);
            }
        }

        //──── 程序化绘制：吊丝 + 对折斜quad倒三角小团 ────
        //全接管绘制：出生透明度全程显式给值，不依赖原版 alpha 自愈

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            Vector2 center = NPC.Center - screenPos;
            Vector2 origin = new(px.Width * 0.5f, px.Height * 0.5f);
            float t = Main.GlobalTimeWrappedHourly;
            int state = (int)State;

            Player viewer = Main.LocalPlayer;
            float viewDist = viewer?.active == true
                ? Vector2.Distance(viewer.Center, NPC.Center) : 9999f;

            //状态透明度：休眠 0.12 悄声细语，前摇渐显，现身全亮
            float bodyAlpha = state switch {
                StateArm => MathHelper.Lerp(0.12f, 0.85f,
                    1f - StateTimer / OldNetMetrics.LurkerArmTicks),
                StateDrop => 1f,
                StateReel => 0.8f,
                _ => 0.12f,
            };
            Color body = new(12, 6, 10);
            Color edge = new(200, 40, 40);

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //吊丝：丝根到本体的 1px 竖线（近距升亮 = 提前可见的观察反制）
            Vector2 silkTop = new Vector2(AnchorX, AnchorY) - screenPos;
            Vector2 silkVec = center - silkTop;
            float silkLen = silkVec.Length();
            if (silkLen > 1f) {
                float silkAlpha = state is StateDrop or StateReel ? 0.3f
                    : viewDist < 240f ? 0.25f : 0.08f;
                spriteBatch.Draw(px, silkTop + silkVec * 0.5f, null,
                    new Color(160, 170, 180) * silkAlpha, silkVec.ToRotation(),
                    origin, Size(silkLen, 1f), SpriteEffects.None, 0f);
            }

            //本体：三组对折斜 quad 收拢成倒三角小团；俯冲期折肢展开+速度拉伸
            float spread = state switch {
                StateDrop => 1f,
                StateReel => 0.5f,
                StateArm => 0.25f,
                _ => 0.1f,
            };
            float stretch = state == StateDrop ? 1f + NPC.velocity.Length() * 0.06f : 1f;
            for (int i = 0; i < 3; i++) {
                float baseAng = MathHelper.PiOver2 + (i - 1) * (0.35f + spread * 0.75f);
                float wob = MathF.Sin(t * (2f + i) + Seed + i * 2.1f) * 0.08f;
                //左右对折的两片
                for (int s = -1; s <= 1; s += 2) {
                    float ang = baseAng + s * (0.28f + spread * 0.5f) + wob;
                    Vector2 dir = ang.ToRotationVector2();
                    float len = (7f + i * 2f) * stretch;
                    spriteBatch.Draw(px, center + dir * len * 0.4f, null, body * bodyAlpha,
                        ang, origin, Size(len, 2.6f), SpriteEffects.None, 0f);
                    spriteBatch.Draw(px, center + dir * len * 0.4f, null,
                        edge * (bodyAlpha * 0.4f), ang, origin,
                        Size(len * 0.7f, 1.1f), SpriteEffects.None, 0f);
                }
            }
            //中心小核
            spriteBatch.Draw(px, center, null, body * bodyAlpha, MathHelper.PiOver4 + t * 0.4f,
                origin, Size(5f, 5f), SpriteEffects.None, 0f);

            //前摇红缘脉冲 / 俯冲红芯（A=0 加色亮层）
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex != null && state != StateDormant) {
                float pulse = state == StateArm
                    ? 0.5f + 0.5f * MathF.Sin(StateTimer * 1.8f)
                    : 0.55f + 0.25f * MathF.Sin(t * 8f + Seed);
                Color glowCol = edge * (0.5f * pulse * bodyAlpha);
                glowCol.A = 0;
                spriteBatch.Draw(glowTex, center, null, glowCol, 0f,
                    glowTex.Size() * 0.5f, 0.2f + spread * 0.08f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
