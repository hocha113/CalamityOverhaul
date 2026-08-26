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
    /// 灯蛾标记体：1 血的信号寄生蛾。不打人，附着环绕期周期 ping 宿主噪音
    /// （ping 间隔低于静默阈值 = 噪音永不自然消散，把"等冷却"变成要付费的），
    /// 玩家持械面向即折线规避。拍死 +4 噪：腾手的代价。断视线拉距 5s 可甩。
    /// T1 跃迁入场 ×2，T2+ 巡检维持场上 ≥1。零战斗力、最高战略权重
    /// </summary>
    internal class OldNetTaggerICE : ModNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //ai[0] 状态位
        private const int StateApproach = 0;
        private const int StateAttach = 1;
        private const int StateSkitter = 2;
        private const int StateLeave = 3;

        /// <summary>ai[0]：状态（接近/附着/规避/离场）</summary>
        private ref float State => ref NPC.ai[0];
        /// <summary>ai[1]：状态计时（规避剩余帧）</summary>
        private ref float StateTimer => ref NPC.ai[1];
        /// <summary>ai[2]：ping 倒数</summary>
        private ref float PingTimer => ref NPC.ai[2];
        /// <summary>ai[3]：断附着/重索敌累计</summary>
        private ref float LoseTimer => ref NPC.ai[3];

        //以下为纯表现实例状态（M1 单人语义；MP 化走 SendExtraAI TODO）
        private float orbitAngle;
        private float pingFlash;
        private readonly Vector2[] ghostTrail = new Vector2[4];
        private int ghostHead;
        private int ghostTimer;

        private float Seed => NPC.whoAmI * 1.117f;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NPCBestiaryDrawModifiers hide = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, hide);
            NPCID.Sets.MustAlwaysDraw[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 12;
            NPC.height = 12;
            //零伤害：它的武器是让整张网知道你在哪
            NPC.damage = 0;
            NPC.defense = 0;
            NPC.lifeMax = 1;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.value = 0;
            NPC.npcSlots = 0.1f;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) {
            PingTimer = OldNetMetrics.TaggerPingTicks;
            orbitAngle = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void AI() {
            //旧网门控：绝不泄漏到主世界与其他子世界
            if (!OldNetWorld.Active) {
                NPC.active = false;
                return;
            }
            pingFlash = MathF.Max(0f, pingFlash - 0.05f);
            TickGhostTrail();

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

            switch ((int)State) {
                case StateAttach:
                    UpdateAttach(player);
                    break;
                case StateSkitter:
                    UpdateSkitter();
                    break;
                default:
                    UpdateApproach(player);
                    break;
            }

            //信号蛾微光：暗处也看得见那盏"灯"
            Lighting.AddLight(NPC.Center, 0.14f, 0.12f, 0.08f);
        }

        //──── 接近：从墙侧飞向玩家 ────

        private void UpdateApproach(Player player) {
            Vector2 toPlayer = player.Center - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity,
                toPlayer.SafeNormalize(Vector2.UnitX) * OldNetMetrics.TaggerApproachSpeed, 0.08f);
            NPC.direction = NPC.spriteDirection = NPC.velocity.X >= 0f ? 1 : -1;

            if (toPlayer.Length() < OldNetMetrics.TaggerAttachRange) {
                State = StateAttach;
                LoseTimer = 0f;
                NPC.netUpdate = true;
                //首次被附着的一次性教学（会话内去重，TODO MP: per-player 化）
                if (OldNetICEDirector.TryMarkHintOnce(OldNetICEDirector.HintTaggerAttached)
                    && player.whoAmI == Main.myPlayer) {
                    CombatText.NewText(player.getRect(), new Color(255, 200, 120),
                        OldNetTexts.TaggerAttached.Value, dramatic: true);
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = 0.9f }, NPC.Center);
                }
                return;
            }
            //重索敌失败：飞了半天也贴不上 → 离场
            if (++LoseTimer >= OldNetMetrics.TaggerReseekTicks) {
                BeginLeave();
            }
        }

        //──── 附着：椭圆环绕 + 周期 ping ────

        private void UpdateAttach(Player player) {
            //椭圆轨道：横向宽纵向扁，读作绕着信号打转
            orbitAngle += 0.035f;
            float radius = OldNetMetrics.TaggerOrbitMid
                + MathF.Sin(Main.GlobalTimeWrappedHourly * 0.6f + Seed) * OldNetMetrics.TaggerOrbitSway;
            Vector2 orbitPos = player.Center + new Vector2(
                MathF.Cos(orbitAngle) * radius, MathF.Sin(orbitAngle) * radius * 0.62f);
            Vector2 want = (orbitPos - NPC.Center) * 0.15f;
            if (want.Length() > 8f) {
                want = want.SafeNormalize(Vector2.Zero) * 8f;
            }
            NPC.velocity = Vector2.Lerp(NPC.velocity, want, 0.5f);
            NPC.direction = NPC.spriteDirection = NPC.velocity.X >= 0f ? 1 : -1;

            //周期 ping：宿主噪音 +1（AddNoise 重置静默计时，附着期噪音永不进入衰减）
            //TODO MP: ping 是 per-player 语义，联机化需服务器裁决宿主归属
            if (--PingTimer <= 0f) {
                PingTimer = OldNetMetrics.TaggerPingTicks;
                OldNetPlayer.Get(player).AddNoise(OldNetMetrics.TaggerPingNoise);
                pingFlash = 1f;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.35f, Pitch = 1f }, NPC.Center);
                }
            }

            float dist = Vector2.Distance(player.Center, NPC.Center);

            //持械面向 = 威胁：折线规避（小目标要专门腾手）
            bool aimedAt = player.HeldItem?.damage > 0
                && dist < OldNetMetrics.TaggerThreatRange
                && player.direction == MathF.Sign(NPC.Center.X - player.Center.X);
            if (aimedAt) {
                State = StateSkitter;
                StateTimer = OldNetMetrics.TaggerSkitterTicks;
                NPC.netUpdate = true;
                return;
            }

            //断附着：断视线且拉开距离持续 5s → 重新索敌
            bool losing = dist > OldNetMetrics.TaggerDetachRange
                && !Collision.CanHitLine(NPC.position, NPC.width, NPC.height,
                    player.position, player.width, player.height);
            if (losing) {
                if (++LoseTimer >= OldNetMetrics.TaggerDetachTicks) {
                    State = StateApproach;
                    LoseTimer = 0f;
                    NPC.netUpdate = true;
                }
            }
            else {
                LoseTimer = 0f;
            }
        }

        //──── 规避：随机折线微冲刺 ────

        private void UpdateSkitter() {
            if ((int)StateTimer % 6 == 0) {
                NPC.velocity = Main.rand.NextVector2Unit() * 9f;
            }
            NPC.direction = NPC.spriteDirection = NPC.velocity.X >= 0f ? 1 : -1;
            if (--StateTimer <= 0f) {
                State = StateAttach;
                NPC.netUpdate = true;
            }
        }

        //──── 离场 ────

        private void BeginLeave() {
            State = StateLeave;
            NPC.netUpdate = true;
        }

        private void UpdateLeave() {
            NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(-7f, -1.5f), 0.05f);
            NPC.direction = NPC.spriteDirection = -1;
            NPC.EncourageDespawn(10);
        }

        public override void OnKill() {
            //拍死标记体的代价：击杀者 +4 噪
            int idx = NPC.lastInteraction;
            Player killer = idx >= 0 && idx < Main.maxPlayers ? Main.player[idx] : null;
            if (killer?.active != true) {
                killer = Main.LocalPlayer;
            }
            if (killer?.active == true) {
                OldNetPlayer.Get(killer).AddNoise(OldNetMetrics.NoiseTaggerKill);
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ || NPC.life > 0) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                    DustID.Electric, Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-2.5f, 2.5f));
                dust.noGravity = true;
                dust.scale = Main.rand.NextFloat(0.4f, 0.8f);
            }
        }

        //残影轨迹：每 3t 记一位（Skitter 期读作三帧残影）
        private void TickGhostTrail() {
            if (++ghostTimer < 3) {
                return;
            }
            ghostTimer = 0;
            ghostTrail[ghostHead] = NPC.Center;
            ghostHead = (ghostHead + 1) % ghostTrail.Length;
        }

        //──── 程序化绘制：白芯灯点 + 高频双翼 + 标记虚线 ────
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

            Color wing = new(255, 205, 130);
            Color mark = new(220, 60, 50);

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //渐隐尾迹：4 段残影 dash（Skitter 期即读作残影）
            for (int i = 0; i < ghostTrail.Length; i++) {
                int slot = (ghostHead - 1 - i + ghostTrail.Length * 2) % ghostTrail.Length;
                Vector2 gpos = ghostTrail[slot];
                if (gpos == Vector2.Zero) {
                    continue;
                }
                float fade = (0.28f - i * 0.06f) * (state == StateSkitter ? 1.6f : 1f);
                spriteBatch.Draw(px, gpos - screenPos, null, wing * fade, 0f,
                    origin, Size(3f, 1.2f), SpriteEffects.None, 0f);
            }

            //附着期：蛾到宿主的 8 段采样虚线（"被标记"的可读化）
            if (state == StateAttach) {
                Player host = Main.player[NPC.target];
                if (host?.active == true && !host.dead) {
                    Vector2 hostPos = host.Center - screenPos;
                    for (int i = 1; i <= 8; i++) {
                        Vector2 dot = Vector2.Lerp(center, hostPos, i / 9f);
                        spriteBatch.Draw(px, dot, null, mark * (0.1f + pingFlash * 0.12f),
                            0f, origin, Size(3f, 1f), SpriteEffects.None, 0f);
                    }
                }
            }

            //双翼：3t 换相的高频抖动短 quad
            float flap = (int)(t * 20f) % 2 == 0 ? 0.55f : -0.25f;
            for (int s = -1; s <= 1; s += 2) {
                float ang = s * (0.9f - flap * 0.5f) + MathF.Sin(t * 5f + Seed) * 0.06f;
                Vector2 dir = new Vector2(s, 0f).RotatedBy(-ang * s);
                spriteBatch.Draw(px, center + dir * 3.5f, null, wing * 0.75f,
                    dir.ToRotation(), origin, Size(7f, 1.8f), SpriteEffects.None, 0f);
            }

            //白芯灯点（A=0 加色亮层）：ping 瞬间脉冲放大
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex != null) {
                Color coreGlow = Color.White * (0.55f + pingFlash * 0.45f);
                coreGlow.A = 0;
                spriteBatch.Draw(glowTex, center, null, coreGlow, 0f,
                    glowTex.Size() * 0.5f, 0.1f + pingFlash * 0.05f, SpriteEffects.None, 0f);
                Color halo = wing * (0.3f + pingFlash * 0.3f);
                halo.A = 0;
                spriteBatch.Draw(glowTex, center, null, halo, 0f,
                    glowTex.Size() * 0.5f, 0.18f, SpriteEffects.None, 0f);
            }
            //芯点实体
            spriteBatch.Draw(px, center, null, Color.White * 0.9f, MathHelper.PiOver4 + t * 2f,
                origin, Size(2.5f, 2.5f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
