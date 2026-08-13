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
    /// Black ICE 猎杀者：快而笨、可甩掉、不可杀。极速高于玩家但转向率极低
    /// （天然过冲/回头钝感），断视线拉开距离可甩；伤害偏向 RAM 而非血量——
    /// 接触咬合小额 HP + 扣 RAM，近距嗅探场持续压 RAM，精英三连咬合触发 RAM 锁定
    /// （锁定 = 清零 → 链路烧断弹出）。锁定目标期间按间隔武器化施放 PvP 骇入协议。
    /// TODO(MP)：OnHitPlayer 只在被打端跑而 RAM 扣减需权威端，联机化按 tml-netcode-pitfalls 重排
    /// </summary>
    internal class OldNetBlackICE : ModNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int StateHunt = 0;
        private const int StateLeave = 1;

        /// <summary>ai[0]：状态（猎杀/回墙离场）</summary>
        private ref float State => ref NPC.ai[0];
        /// <summary>ai[1]：失联计时（断视线且超距的累计帧）</summary>
        private ref float LoseTimer => ref NPC.ai[1];
        /// <summary>ai[2]：下次施放倒数</summary>
        private ref float CastTimer => ref NPC.ai[2];
        /// <summary>ai[3]：精英变体旗标（T3+ 生成时写 1）</summary>
        private ref float EliteFlag => ref NPC.ai[3];

        private bool Elite => EliteFlag >= 1f;

        //以下实例状态不参与同步（M1 单人语义；MP 化时改走 ai/SendExtraAI TODO）
        private Vector2 lastKnownPos;
        private int telegraphTimer;
        private int biteCombo;
        private int biteComboWindow;

        private float Seed => NPC.whoAmI * 1.313f;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NPCBestiaryDrawModifiers hide = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, hide);
            NPCID.Sets.MustAlwaysDraw[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 32;
            NPC.height = 32;
            //小额接触伤：命中事件载体（纯 0 伤不会产生接触事件），真正的牙是 RAM
            NPC.damage = OldNetMetrics.HunterContactDamage;
            NPC.defense = 0;
            NPC.lifeMax = 600;
            //不可打死：免疫语义最清晰（备选"锁血反施"案 M1 不用）
            NPC.dontTakeDamage = true;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            //数据体穿行地形；甩脱判定靠视线与距离而非碰撞
            NPC.noTileCollide = true;
            NPC.value = 0;
            NPC.npcSlots = 0f;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) {
            CastTimer = RollCastInterval();
            lastKnownPos = NPC.Center;
        }

        public override void AI() {
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

            float speed = OldNetMetrics.HunterSpeed * (Elite ? OldNetMetrics.HunterEliteSpeedMul : 1f);
            Vector2 toPlayer = player.Center - NPC.Center;
            float dist = toPlayer.Length();

            //感知：清剿波全图；否则近距 + 通视
            bool perceive = OldNetICEDirector.CleanupWaveActive
                || (dist < OldNetMetrics.HunterPerceptionRange
                    && Collision.CanHitLine(NPC.position, NPC.width, NPC.height,
                        player.position, player.width, player.height));

            if (perceive) {
                lastKnownPos = player.Center;
                LoseTimer = 0f;
            }
            else if (dist > OldNetMetrics.HunterPerceptionRange) {
                LoseTimer++;
                if (LoseTimer >= OldNetMetrics.HunterLoseTicks) {
                    BeginLeave();
                    return;
                }
            }

            //追击：低转向率是"笨"的本体，甩掉手感全靠它
            Vector2 toGoal = lastKnownPos - NPC.Center;
            if (toGoal.Length() > 24f) {
                NPC.velocity = Vector2.Lerp(NPC.velocity,
                    toGoal.SafeNormalize(Vector2.UnitX) * speed, OldNetMetrics.HunterTurnRate);
            }
            else if (!perceive) {
                //追到最后已知位置却没人：原地盘旋，失联计时照走
                NPC.velocity *= 0.94f;
                LoseTimer += 2f;
                if (LoseTimer >= OldNetMetrics.HunterLoseTicks) {
                    BeginLeave();
                    return;
                }
            }
            NPC.direction = NPC.spriteDirection = NPC.velocity.X >= 0f ? 1 : -1;

            //嗅探场：无视无敌帧的近距 RAM 压力（与距离底噪叠加，总量盯 §4.4）
            if (dist < OldNetMetrics.HunterSniffRange) {
                RamSystem.TryConsumeOverTime(player, OldNetMetrics.HunterSniffRamPerSecond, out _);
            }

            //咬合连击窗口衰减
            if (biteComboWindow > 0 && --biteComboWindow <= 0) {
                biteCombo = 0;
            }

            UpdateProtocolCast(player, perceive);

            Lighting.AddLight(NPC.Center, 0.3f, 0.04f, 0.03f);
        }

        //──── 武器化协议：0.8s 红光前摇 → 施放 ────

        private void UpdateProtocolCast(Player player, bool perceive) {
            if (telegraphTimer > 0) {
                telegraphTimer--;
                if (telegraphTimer == 0) {
                    int tier = Main.LocalPlayer?.active == true
                        ? OldNetPlayer.Get(Main.LocalPlayer).NoiseTier : 2;
                    OldNetHostileHack.TryCast(player,
                        OldNetHostileHack.PickForTier(tier, Elite), NPC.TypeName);
                    CastTimer = RollCastInterval();
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(CWRSound.Hacker with { Volume = 0.5f, Pitch = -0.2f }, NPC.Center);
                    }
                }
                return;
            }
            if (!perceive || player.dead) {
                return;
            }
            if (--CastTimer <= 0f) {
                telegraphTimer = OldNetMetrics.HunterCastTelegraphTicks;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DD2_DarkMageCastHeal with { Volume = 0.6f, Pitch = -0.4f }, NPC.Center);
                }
            }
        }

        private float RollCastInterval() {
            int interval = Main.rand.Next(OldNetMetrics.HunterCastIntervalMin,
                OldNetMetrics.HunterCastIntervalMax + 1);
            //精英施放间隔减半
            return Elite ? interval / 2f : interval;
        }

        //──── 离场：回墙淡出 ────

        private void BeginLeave() {
            State = StateLeave;
            NPC.damage = 0;
            NPC.netUpdate = true;
        }

        private void UpdateLeave() {
            NPC.velocity = Vector2.Lerp(NPC.velocity,
                new Vector2(-OldNetMetrics.HunterSpeed, 0f), 0.05f);
            NPC.direction = NPC.spriteDirection = -1;
            NPC.EncourageDespawn(10);
        }

        //──── 咬合：RAM 才是牙 ────

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            //TODO(MP)：本钩子只在被打端跑，RAM 扣减 MP 客户端直调必失败——联机化走请求包
            RamSystem.TryConsume(target, OldNetMetrics.HunterBiteRam, out _);

            if (!Elite) {
                return;
            }
            //精英三连咬合 → RAM 锁定：清零 + 恢复暂停，OldNetPlayer 随即链路烧断
            biteCombo++;
            biteComboWindow = OldNetMetrics.HunterBiteComboWindow;
            if (biteCombo >= OldNetMetrics.HunterEliteLockBites) {
                biteCombo = 0;
                RamSystem.SystemLock(target, OldNetMetrics.HunterEliteLockFrames);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(CWRSound.Fault with { Volume = 0.8f, Pitch = -0.3f }, target.Center);
                }
            }
        }

        //──── 程序化绘制：速度拉伸的黑色碎片群 + 红芯 ────

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            Vector2 center = NPC.Center - screenPos;
            Vector2 origin = new(px.Width * 0.5f, px.Height * 0.5f);
            float t = Main.GlobalTimeWrappedHourly;
            float vel = NPC.velocity.Length();
            float rot = NPC.velocity.SafeNormalize(Vector2.UnitX * NPC.direction).ToRotation();

            Color edge = Elite ? new Color(255, 40, 60) : new Color(200, 40, 40);
            Color body = new(12, 6, 10);
            float telegraphGlow = telegraphTimer > 0
                ? 1f - telegraphTimer / (float)OldNetMetrics.HunterCastTelegraphTicks : 0f;

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //移动 = 速度拉伸：主体沿速度方向拉长（材质身份，非贴图平移）
            float bodyLen = 24f + vel * 1.6f;
            float bodyWide = 13f - MathHelper.Clamp(vel * 0.25f, 0f, 5f);
            //红缘先铺（略大）
            spriteBatch.Draw(px, center, null, edge * 0.55f, rot,
                origin, Size(bodyLen + 4f, bodyWide + 3f), SpriteEffects.None, 0f);
            //黑体压上
            spriteBatch.Draw(px, center, null, body, rot,
                origin, Size(bodyLen, bodyWide), SpriteEffects.None, 0f);

            //环绕碎片群：不规则黑片抖动，读作不稳定的数据聚合体
            for (int i = 0; i < 4; i++) {
                float ph = t * (1.3f + i * 0.31f) + Seed + i * 1.7f;
                Vector2 off = new(MathF.Sin(ph) * (10f + i * 4f), MathF.Cos(ph * 1.37f) * (7f + i * 2f));
                float fragLen = 7f + i * 2f + vel * 0.4f;
                spriteBatch.Draw(px, center + off, null, body * 0.9f, rot + MathF.Sin(ph) * 0.6f,
                    origin, Size(fragLen, 3.2f), SpriteEffects.None, 0f);
                spriteBatch.Draw(px, center + off, null, edge * 0.3f, rot + MathF.Sin(ph) * 0.6f,
                    origin, Size(fragLen * 0.6f, 1.4f), SpriteEffects.None, 0f);
            }

            //精英龙骨线：一条贯穿的亮白keel，一眼分辨变体
            if (Elite) {
                spriteBatch.Draw(px, center, null, Color.White * 0.5f, rot,
                    origin, Size(bodyLen * 0.9f, 1f), SpriteEffects.None, 0f);
            }

            //红芯与前摇警告辉光（A=0 亮层）
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex != null) {
                float corePulse = 0.6f + 0.4f * MathF.Sin(t * 7f + Seed);
                Color coreCol = edge * (0.55f * corePulse + telegraphGlow * 0.6f);
                coreCol.A = 0;
                spriteBatch.Draw(glowTex, center, null, coreCol, 0f,
                    glowTex.Size() * 0.5f, 0.28f + telegraphGlow * 0.22f, SpriteEffects.None, 0f);
                Color whiteCore = Color.White * (0.35f * corePulse + telegraphGlow * 0.4f);
                whiteCore.A = 0;
                spriteBatch.Draw(glowTex, center, null, whiteCore, 0f,
                    glowTex.Size() * 0.5f, 0.09f, SpriteEffects.None, 0f);
            }

            //前摇期：向目标方向的三道红色警示短线（可读性阀）
            if (telegraphTimer > 0) {
                for (int i = -1; i <= 1; i++) {
                    Vector2 rayDir = new Vector2(NPC.direction, 0f).RotatedBy(i * 0.22f);
                    float rayLen = 30f * telegraphGlow;
                    spriteBatch.Draw(px, center + rayDir * (16f + rayLen * 0.5f), null,
                        edge * (0.5f * telegraphGlow), rayDir.ToRotation(),
                        origin, Size(rayLen, 1.2f), SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }
}
