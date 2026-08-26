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
    /// 扫描哨眼：一盏会回头的探照灯。零 DPS，全部威胁都是位置信息：
    /// 锥内通视充能 36 tick → +12 噪 + NotifySpotted 猎队响应 + 3s 追光锁定（持续曝光计费）。
    /// 锥体永远可见（等窗/硬穿/击杀三路的读秒基准）；时停中整机冻结=静止障碍。
    /// 定点 NPC，镜像 TurretICE 形态；布防见 OldNetThreatField.SeedSweepEyes
    /// </summary>
    internal class OldNetSweepEye : ModNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //ai[0]：充能；ai[1]：基准轴向（rad，布防时写入）；ai[2]：状态 0=扫摆 1=锁定；ai[3]：锁定倒数
        private ref float Charge => ref NPC.ai[0];
        private ref float BaseAxis => ref NPC.ai[1];
        private ref float State => ref NPC.ai[2];
        private ref float LockTimer => ref NPC.ai[3];
        //localAI[0]：扫摆时钟（AI 冻结即停，锥随时停凝固）；localAI[1]：当前轴向（表现+裁决共用）
        private ref float SweepClock => ref NPC.localAI[0];
        private ref float CurrentAxis => ref NPC.localAI[1];
        //localAI[2]：初始化旗标
        private ref float Inited => ref NPC.localAI[2];

        private const int StateSweep = 0;
        private const int StateLocked = 1;

        private static readonly Color ColdCyan = new(0, 220, 255);
        private static readonly Color Amber = new(255, 170, 60);
        private static readonly Color WarnRed = new(235, 64, 44);
        private static readonly Color Shell = new(20, 44, 50);

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NPCBestiaryDrawModifiers hide = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, hide);
            NPCID.Sets.MustAlwaysDraw[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 24;
            NPC.height = 24;
            //零接触伤：它不开火，被看见本身就是关卡
            NPC.damage = 0;
            NPC.defense = OldNetMetrics.SweepEyeDefense;
            NPC.lifeMax = OldNetMetrics.SweepEyeLife;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = false;
            NPC.value = 0;
            NPC.npcSlots = 0.3f;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
        }

        //一次性布防的哨戒体：不参与原版远离despawn（布防在开局，玩家远在出生点，
        //不关此闸下一帧就被 activeRange 清场且 seeded 旗标不补种）；门控自杀兜底离场
        public override bool CheckActive() => false;

        public override void AI() {
            if (!OldNetWorld.Active) {
                NPC.active = false;
                return;
            }
            //定点：钉死出生位
            NPC.velocity = Vector2.Zero;

            if (Inited < 1f) {
                Inited = 1f;
                //相位=whoAmI 哈希：同屏多眼错拍，节律可预读
                SweepClock = NPC.whoAmI * 37 % OldNetMetrics.SweepEyePeriodTicks;
                CurrentAxis = BaseAxis;
            }

            NPC.TargetClosest(faceTarget: false);
            Player player = Main.player[NPC.target];
            bool hasTarget = player != null && player.active && !player.dead;

            if ((int)State == StateLocked) {
                TickLocked(player, hasTarget);
            }
            else {
                TickSweep(player, hasTarget);
            }

            float glow = (int)State == StateLocked ? 0.9f
                : 0.35f + Charge / OldNetMetrics.SweepEyeChargeTicks * 0.4f;
            Lighting.AddLight(NPC.Center, 0.05f * glow, 0.20f * glow, 0.24f * glow);
        }

        //──── 扫摆：130° 弧内匀速往返（三角波），锥内通视充能 ────

        private void TickSweep(Player player, bool hasTarget) {
            SweepClock++;
            CurrentAxis = MathHelper.Lerp(CurrentAxis, SweepTarget(), 0.18f);

            bool inCone = hasTarget && PlayerInCone(player);
            if (!inCone) {
                //脱锥快速回落（比炮塔 -2 更宽容，鼓励拉扯）
                Charge = MathF.Max(0f, Charge - OldNetMetrics.SweepEyeDecayPerTick);
                return;
            }

            Charge++;
            if ((int)Charge % 12 == 1 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.4f, Pitch = 0.7f }, NPC.Center);
            }
            if (Charge < OldNetMetrics.SweepEyeChargeTicks) {
                return;
            }

            //目击：点亮玩家 + 猎队响应（复用巡逻目击口），转入追光锁定
            Charge = 0f;
            OldNetPlayer.Get(player).AddNoise(OldNetMetrics.NoiseEyeSpotted);
            OldNetICEDirector.NotifySpotted(player);
            State = StateLocked;
            LockTimer = OldNetMetrics.SweepEyeLockTicks;
            NPC.netUpdate = true;
            if (!Main.dedServ) {
                SoundEngine.PlaySound(CWRSound.FaultOccurred with { Volume = 0.55f, Pitch = 0.35f }, NPC.Center);
            }
        }

        //──── 锁定：追光灯跟随 3s，期间持续曝光计费 ────

        private void TickLocked(Player player, bool hasTarget) {
            if (hasTarget) {
                float toPlayer = (player.Center - NPC.Center).ToRotation();
                CurrentAxis = CurrentAxis.AngleLerp(toPlayer, 0.15f);
                //持续曝光：每秒 +1 噪（追光灯替全网直播你的坐标）
                OldNetPlayer.Get(player).AddNoise(OldNetMetrics.NoiseEyeExposurePerSecond / 60f);
            }
            if (--LockTimer <= 0f || !hasTarget) {
                State = StateSweep;
                Charge = 0f;
                NPC.netUpdate = true;
            }
        }

        //三角波扫摆目标角：匀速往返，节律可背
        private float SweepTarget() {
            float t01 = SweepClock % OldNetMetrics.SweepEyePeriodTicks / OldNetMetrics.SweepEyePeriodTicks;
            float tri = t01 < 0.5f ? t01 * 2f : 2f - t01 * 2f;
            return BaseAxis + (tri * 2f - 1f) * OldNetMetrics.SweepEyeArcHalf;
        }

        private bool PlayerInCone(Player player) {
            Vector2 toPlayer = player.Center - NPC.Center;
            if (toPlayer.Length() > OldNetMetrics.SweepEyeConeLen) {
                return false;
            }
            float angleDiff = MathF.Abs(MathHelper.WrapAngle(toPlayer.ToRotation() - CurrentAxis));
            if (angleDiff > OldNetMetrics.SweepEyeConeHalfAngle) {
                return false;
            }
            return Collision.CanHitLine(NPC.position, NPC.width, NPC.height,
                player.position, player.width, player.height);
        }

        public override void OnKill() {
            //死叫：击毁哨眼有代价（且高处输出姿势本身暴露）
            int idx = NPC.lastInteraction;
            Player killer = idx >= 0 && idx < Main.maxPlayers ? Main.player[idx] : null;
            if (killer?.active != true) {
                killer = Main.LocalPlayer;
            }
            if (killer?.active == true) {
                OldNetPlayer.Get(killer).AddNoise(OldNetMetrics.NoiseEyeKill);
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < (NPC.life <= 0 ? 14 : 3); i++) {
                Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                    DustID.Electric, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f));
                dust.noGravity = true;
                dust.scale = Main.rand.NextFloat(0.5f, 1f);
            }
        }

        //──── 程序化绘制：12 ray 渐隐拼扇 + 斜置方芯壳体 + 单眼白点 ────

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            Vector2 center = NPC.Center - screenPos;
            Vector2 origin = new(px.Width * 0.5f, px.Height * 0.5f);
            float t = Main.GlobalTimeWrappedHourly;

            bool locked = (int)State == StateLocked;
            float chargeFrac = MathHelper.Clamp(Charge / OldNetMetrics.SweepEyeChargeTicks, 0f, 1f);
            //待机冷青 → 充能琥珀 → 锁定警戒红（三段充能色公约）
            Color accent = locked ? WarnRed : Color.Lerp(ColdCyan, Amber, chargeFrac);

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //扫描锥：12 根等角细 ray 渐隐拼扇（PatrolICE 前向线束的放大版）。
            //充能收窄聚焦："它在瞄你"的形状语言；锁定后红扇 + 边缘白描
            float axis = CurrentAxis;
            float visHalf = OldNetMetrics.SweepEyeConeHalfAngle * (1f - chargeFrac * 0.35f);
            const int rayCount = 12;
            float baseAlpha = locked ? 0.15f : 0.09f + chargeFrac * 0.08f;
            for (int k = 0; k < rayCount; k++) {
                float frac = rayCount <= 1 ? 0.5f : k / (float)(rayCount - 1);
                float ang = axis + (frac * 2f - 1f) * visHalf;
                //边缘 ray 略短、更透：扇面有"体"而不糊成色块
                float edge = 1f - MathF.Abs(frac * 2f - 1f) * 0.35f;
                float rayLen = OldNetMetrics.SweepEyeConeLen * (0.94f + 0.06f * MathF.Sin(t * 2f + k));
                Vector2 dir = ang.ToRotationVector2();
                spriteBatch.Draw(px, center + dir * rayLen * 0.5f, null,
                    accent * (baseAlpha * edge), ang, origin,
                    Size(rayLen, 1.1f), SpriteEffects.None, 0f);
            }
            if (locked) {
                //锁定白描边：两根边缘亮线
                for (int s = -1; s <= 1; s += 2) {
                    float ang = axis + s * visHalf;
                    Vector2 dir = ang.ToRotationVector2();
                    spriteBatch.Draw(px, center + dir * OldNetMetrics.SweepEyeConeLen * 0.5f, null,
                        Color.White * 0.30f, ang, origin,
                        Size(OldNetMetrics.SweepEyeConeLen, 1f), SpriteEffects.None, 0f);
                }
            }

            //壳体：斜置方芯 + 短横臂
            spriteBatch.Draw(px, center, null, Shell, MathHelper.PiOver4,
                origin, Size(15f, 15f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, center, null, Shell * 0.9f, 0f,
                origin, Size(19f, 4f), SpriteEffects.None, 0f);
            //单眼白点：充能时眼芯放大
            float eyeSize = 3.5f + chargeFrac * 3f + (locked ? 1.5f : 0f);
            spriteBatch.Draw(px, center, null, accent * 0.9f, MathHelper.PiOver4,
                origin, Size(eyeSize + 2.5f, eyeSize + 2.5f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, center, null, Color.White * (0.75f + chargeFrac * 0.25f),
                MathHelper.PiOver4, origin, Size(eyeSize, eyeSize), SpriteEffects.None, 0f);

            //充能进度条：头顶读秒（被盯上的可读性阀，PatrolICE 同语汇）
            if (chargeFrac > 0.01f && !locked) {
                Vector2 barTl = center + new Vector2(-13f, -24f);
                spriteBatch.Draw(px, barTl, null, new Color(10, 20, 24) * 0.85f, 0f,
                    Vector2.Zero, Size(26f, 3f), SpriteEffects.None, 0f);
                spriteBatch.Draw(px, barTl, null, accent, 0f,
                    Vector2.Zero, Size(26f * chargeFrac, 3f), SpriteEffects.None, 0f);
            }

            //眼芯辉光（A=0 加色亮层）
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex != null) {
                float pulse = locked ? 0.75f + 0.25f * MathF.Sin(t * 9f) : 0.4f + chargeFrac * 0.35f;
                Color glowCol = accent * (0.5f * pulse);
                glowCol.A = 0;
                spriteBatch.Draw(glowTex, center, null, glowCol, 0f,
                    glowTex.Size() * 0.5f, 0.2f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
