using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord
{
    /// <summary>
    /// 脱出真眼：部件破坏后的独立威胁集群。共享核心编队时钟轮流出手
    /// （波弹连射/星球短扇/预兆冲撞三技轮转），核心指令可召其锚定或退避。
    /// 保持原版不可伤身份，随核心死亡消散
    /// </summary>
    internal class MoonLordFreeEyeAI : CWRNPCOverride
    {
        public override int TargetID => NPCID.MoonLordFreeEye;

        /// <summary>出手轮换周期帧</summary>
        internal const int StrikePeriod = 150;
        internal const int RamTelegraph = 30;
        internal const int RamDash = 9;

        private MLordEyePose pose;
        private int bodyFrameTick;
        private int bodyFrame;
        private float scalePulse = 1f;
        private Player targetPlayer;
        /// <summary>冲撞锁定方向（出手窗内各端确定性推导）</summary>
        private Vector2 ramDir;

        public override bool? CanCWROverride() {
            return null;
        }

        public override void SetProperty() {
            npc.aiStyle = -1;
            npc.knockBackResist = 0f;
            npc.dontTakeDamage = true;
        }

        public override bool AI() {
            npc.aiStyle = -1;
            npc.netOffset = Vector2.Zero;
            npc.dontTakeDamage = true;

            NPC core = MLordFacts.GetCore(npc);
            if (core == null) {
                if (!VaultUtils.isClient) {
                    npc.life = 0;
                    npc.HitEffect();
                    npc.active = false;
                    npc.netUpdate = true;
                }
                return false;
            }

            for (int i = 0; i < npc.buffImmune.Length; i++) {
                npc.buffImmune[i] = true;
            }

            targetPlayer = Main.player[Math.Clamp(core.target, 0, Main.maxPlayers - 1)];
            //槽位复用等异常取不到覆写时保持null，下游已有空值回退（精确索引缺键会抛出）
            core.TryGetOverride(out MoonLordCoreAI coreAI);
            bool hold = coreAI?.Context?.HoldAllParts ?? false;
            int command = (int)MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvEyeCommand);
            float clock = MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvFormationClock);

            //出生演出
            if (npc.ai[MLordAiSlots.EyeBirthTimer] < 40f) {
                npc.ai[MLordAiSlots.EyeBirthTimer]++;
                UpdateBirth(clock);
                UpdatePresentation(hold: true);
                return false;
            }

            npc.damage = command == MLordEyeCommand.Retreat || hold ? 0 : 60;

            if (hold || command == MLordEyeCommand.Retreat) {
                //退避收拢：贴核心哀鸣漂浮
                Vector2 tuck = core.Center + new Vector2(
                    (float)Math.Sin(clock * 0.03f + npc.whoAmI) * 130f,
                    -220f + (float)Math.Cos(clock * 0.025f + npc.whoAmI) * 40f);
                SpringTo(tuck, 0.07f, 16f);
            }
            else if (command == MLordEyeCommand.Anchor) {
                //锚定阵位：为核心攻击站桩（弦月第三弧等），按扫描席位横向散开防叠站
                int[] anchorEyes = new int[3];
                int anchorCount = MLordFacts.ScanFreeEyes(core, anchorEyes);
                int anchorOrdinal = 0;
                for (int i = 0; i < anchorCount; i++) {
                    if (anchorEyes[i] == npc.whoAmI) {
                        anchorOrdinal = i;
                        break;
                    }
                }
                Vector2 anchorBase = targetPlayer.Alives() ? targetPlayer.Center : core.Center;
                Vector2 anchorPos = anchorBase + new Vector2((anchorOrdinal - (anchorCount - 1) * 0.5f) * 240f, -430f);
                SpringTo(anchorPos, 0.08f, 18f);
                pose.PupilAngle = pose.PupilAngle.AngleLerp((anchorBase - npc.Center).ToRotation(), 0.25f);
            }
            else {
                UpdateSoloCycle(core, clock);
            }

            UpdatePresentation(hold);

            if (!VaultUtils.isClient && (Main.GameUpdateCount + (uint)npc.whoAmI) % 2 == 0) {
                npc.netUpdate = true;
            }
            return false;
        }

        #region 集群自主循环

        /// <summary>编队环绕 + 轮流出手</summary>
        private void UpdateSoloCycle(NPC core, float clock) {
            //目标失效退避贴核心
            if (!targetPlayer.Alives()) {
                Vector2 tuck = core.Center + new Vector2((float)Math.Sin(clock * 0.03f + npc.whoAmI) * 130f, -220f);
                SpringTo(tuck, 0.07f, 16f);
                return;
            }
            int[] eyes = new int[3];
            int eyeCount = MLordFacts.ScanFreeEyes(core, eyes);
            if (eyeCount <= 0) {
                return;
            }
            //本眼在扫描序中的席位（whoAmI 升序，各端一致）
            int ordinal = 0;
            for (int i = 0; i < eyeCount && i < eyes.Length; i++) {
                if (eyes[i] == npc.whoAmI) {
                    ordinal = i;
                    break;
                }
            }

            int strikeRound = (int)(clock / StrikePeriod);
            int strikerOrdinal = strikeRound % eyeCount;
            int strikePhase = (int)(clock % StrikePeriod);

            //非出手位：绕玩家编队环绕（相位按席位均分）
            if (strikerOrdinal != ordinal || strikePhase < 24) {
                float angle = clock * 0.014f + MathHelper.TwoPi / Math.Max(eyeCount, 1) * ordinal;
                Vector2 orbit = targetPlayer.Center + angle.ToRotationVector2() * new Vector2(430f, 330f);
                SpringTo(orbit, 0.06f, 15f);
                pose.PupilAngle = pose.PupilAngle.AngleLerp((targetPlayer.Center - npc.Center).ToRotation(), 0.2f);
                return;
            }

            //出手窗：技型 =（轮次+席位）% 3 轮转
            int strikeKind = (strikeRound + ordinal) % 3;
            switch (strikeKind) {
                case 0:
                    StrikeBoltBurst(strikePhase);
                    break;
                case 1:
                    StrikeOrbFan(strikePhase);
                    break;
                default:
                    StrikeRam(strikePhase);
                    break;
            }
        }

        /// <summary>技一：三连波弹（36f 节拍，预摆后连射）</summary>
        private void StrikeBoltBurst(int strikePhase) {
            npc.velocity *= 0.93f;
            pose.PupilAngle = pose.PupilAngle.AngleLerp((targetPlayer.Center - npc.Center).ToRotation(), 0.4f);
            pose.Glow = 1f;
            if (!VaultUtils.isClient && (strikePhase == 52 || strikePhase == 72 || strikePhase == 92)) {
                Vector2 aim = (targetPlayer.Center + targetPlayer.velocity * 14f - npc.Center).SafeNormalize(Vector2.UnitY);
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + aim * 26f, aim * 8.2f,
                    ProjectileID.PhantasmalBolt, MLordDirector.BoltDamage, 0f, Main.myPlayer);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item125 with { Volume = 0.5f, Pitch = 0.4f, MaxInstances = 5 }, npc.Center);
                }
            }
        }

        /// <summary>技二：星球短扇（一次持握三球）</summary>
        private void StrikeOrbFan(int strikePhase) {
            npc.velocity *= 0.93f;
            pose.Glow = 1f;
            if (!VaultUtils.isClient && strikePhase == 60) {
                Vector2 aim = (targetPlayer.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                for (int i = -1; i <= 1; i++) {
                    Vector2 offset = aim.RotatedBy(i * 0.4f) * 56f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + offset, Vector2.Zero,
                        ModContent.ProjectileType<MLordOrbProj>(), MLordDirector.OrbDamage, 0f, Main.myPlayer,
                        npc.whoAmI, 0f, 34 + (i + 1) * 4);
                }
            }
        }

        /// <summary>技三：预兆冲撞（锁向抖动预警→直线贯穿→硬刹）</summary>
        private void StrikeRam(int strikePhase) {
            int ramStart = 60;
            if (strikePhase < ramStart) {
                //预警：锁向 + 高频颤抖
                npc.velocity *= 0.9f;
                if (strikePhase > ramStart - RamTelegraph) {
                    ramDir = (targetPlayer.Center + targetPlayer.velocity * 10f - npc.Center).SafeNormalize(Vector2.UnitY);
                    npc.position += Main.rand.NextVector2Circular(1.6f, 1.6f);
                    pose.PupilAngle = ramDir.ToRotation();
                    pose.PupilOut = 1f;
                    pose.Glow = 1f;
                    if (strikePhase == ramStart - RamTelegraph + 2 && !VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Zombie102 with { Volume = 0.8f, Pitch = 0.2f, MaxInstances = 4 }, npc.Center);
                    }
                }
            }
            else if (strikePhase == ramStart) {
                npc.velocity = ramDir * 30f;
                if (!VaultUtils.isServer) {
                    MLordScreenFX.Punch(npc.Center, 4f, 8, ramDir);
                }
            }
            else if (strikePhase < ramStart + RamDash) {
                npc.velocity *= 1.015f;
            }
            else {
                npc.velocity *= 0.88f;
            }
        }

        #endregion

        #region 出生与表现

        /// <summary>出生：残口升起 + 甩落星屑</summary>
        private void UpdateBirth(float clock) {
            npc.velocity = Vector2.Lerp(npc.velocity, new Vector2(0f, -3.2f), 0.1f);
            npc.position += Main.rand.NextVector2Circular(1.2f, 1.2f);
            scalePulse = MathHelper.Lerp(scalePulse, 1.15f, 0.1f);
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                MLordScreenFX.StarBurst(npc.Center + Main.rand.NextVector2Circular(30f, 30f), 0.3f, 2);
            }
            _ = clock;
        }

        private void UpdatePresentation(bool hold) {
            //本体帧循环
            if (++bodyFrameTick >= 5) {
                bodyFrameTick = 0;
                if (++bodyFrame > 3) {
                    bodyFrame = 0;
                }
            }
            scalePulse = MathHelper.Lerp(scalePulse, 1f + 0.12f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f + npc.whoAmI), 0.2f);
            pose.PupilOut = MathHelper.Clamp(MathHelper.Lerp(pose.PupilOut, hold ? 0.3f : 0.7f, 0.06f), 0f, 1f);
            pose.Glow = MathHelper.Lerp(pose.Glow, hold ? 0.1f : 0.4f, 0.08f);
            pose.Broken = false;
            Lighting.AddLight(npc.Center, MLordDirector.Phantasmal.ToVector3() * (0.3f + pose.Glow * 0.4f));
        }

        /// <summary>朝目标点弹簧进给</summary>
        private void SpringTo(Vector2 goal, float gain, float maxSpeed) {
            Vector2 want = (goal - npc.Center) * gain;
            if (want.Length() > maxSpeed) {
                want = want.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            npc.velocity = Vector2.Lerp(npc.velocity, want, 0.14f);
        }

        #endregion

        #region 死亡与绘制

        public override bool CheckActive() => false;

        public override bool FindFrame(int frameHeight) => false;

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            MLordDrawHelper.DrawFreeEyeAssembly(spriteBatch, npc, screenPos, in pose, bodyFrame, scalePulse);
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }

        #endregion
    }
}
