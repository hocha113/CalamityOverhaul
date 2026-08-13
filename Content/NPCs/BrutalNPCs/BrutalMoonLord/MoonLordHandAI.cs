using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord
{
    /// <summary>
    /// 月总之手：核心状态的忠实提线木偶。编队/姿态各端确定性推导，
    /// 掌击等直控动作由服务端状态写速度、客户端积分跟随。
    /// 接触伤害按速度门控（各端一致），破坏后转蠕动残口
    /// </summary>
    internal class MoonLordHandAI : CWRNPCOverride
    {
        public override int TargetID => NPCID.MoonLordHand;

        private MLordEyePose pose;
        private float gripFrame;
        private float wriggleTimer;
        private Player targetPlayer;
        /// <summary>掌击预警线强度（蓄势后半段亮起，各端本地推导）</summary>
        private float slamTelegraph;

        public override bool? CanCWROverride() {
            return null;
        }

        public override void SetProperty() {
            npc.aiStyle = -1;
            npc.knockBackResist = 0f;
            int newMaxLife = (int)(npc.lifeMax * MLordDirector.HandLifeFactor);
            npc.life = npc.lifeMax = newMaxLife;
        }

        public override bool AI() {
            npc.aiStyle = -1;
            npc.netOffset = Vector2.Zero;
            npc.knockBackResist = 0f;

            NPC core = MLordFacts.GetCore(npc);
            //核心失效：坠毁（服务端裁定）
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
            MLordStateIndex coreState = MLordFacts.GetCoreState(core);
            int stateTimer = coreAI?.StateTimer ?? 0;
            bool hold = coreAI?.Context?.HoldAllParts ?? false;
            bool broken = npc.ai[MLordAiSlots.PartBroken] == MLordAiSlots.BrokenMark;

            //速度门控接触伤害（各端同式）
            npc.damage = !broken && npc.velocity.Length() > 24f ? MLordDirector.PalmContactDamage : 0;

            if (broken) {
                UpdateBroken(core);
            }
            else {
                UpdateAlive(core, coreAI, coreState, stateTimer, hold);
            }

            //服务端隔帧广播（部件多，错相减半流量）
            if (!VaultUtils.isClient && (Main.GameUpdateCount + (uint)npc.whoAmI) % 2 == 0) {
                npc.netUpdate = true;
            }
            return false;
        }

        #region 存活行为

        private void UpdateAlive(NPC core, MoonLordCoreAI coreAI, MLordStateIndex coreState, int stateTimer, bool hold) {
            bool eyeOpen = ComputeEyeOpen(coreState, stateTimer);
            npc.dontTakeDamage = !eyeOpen;

            //掌击拍：执行者由状态直控（服务端写速度，客户端积分），非执行者走编队
            bool claimedByState = false;
            if (coreState == MLordStateIndex.TidalPalms && coreAI?.Context != null
                && MLordTidalPalmsState.TryGetBeat(coreAI.Context, stateTimer, out int slamIndex, out int sub)) {
                NPC performer = MLordTidalPalmsState.ResolvePerformer(coreAI.Context, slamIndex);
                if (performer != null && performer.whoAmI == npc.whoAmI) {
                    claimedByState = true;
                    UpdateSlamPose(sub);
                }
            }

            if (!claimedByState) {
                slamTelegraph = 0f;
                //编队弹簧
                Vector2 goal = ComputeFormationGoal(core, coreState, hold);
                Vector2 want = (goal - npc.Center) * 0.06f;
                if (want.Length() > 14f) {
                    want = want.SafeNormalize(Vector2.Zero) * 14f;
                }
                npc.velocity = Vector2.Lerp(npc.velocity, want, 0.16f);

                //常态姿态
                float targetGrip = coreState == MLordStateIndex.MoonBite ? 2.6f : 0.6f;
                gripFrame = MathHelper.Lerp(gripFrame, targetGrip, 0.1f);
                pose.PupilAngle = pose.PupilAngle.AngleLerp((targetPlayer.Center - npc.Center).ToRotation(), 0.25f);
                pose.PupilOut = MathHelper.Lerp(pose.PupilOut, eyeOpen ? 0.75f : 0.3f, 0.08f);
                pose.Glow = MathHelper.Lerp(pose.Glow, eyeOpen ? 0.65f : 0.1f, 0.1f);
            }

            pose.Broken = false;
            Lighting.AddLight(npc.Center, MLordDirector.Phantasmal.ToVector3() * (0.25f + pose.Glow * 0.4f));
        }

        /// <summary>掌击子相位姿态（预警张掌/冲线握拳/硬直摊开）</summary>
        private void UpdateSlamPose(int sub) {
            if (MLordTidalPalmsState.InWindup(sub)) {
                gripFrame = MathHelper.Lerp(gripFrame, 0f, 0.3f);
                pose.PupilAngle = pose.PupilAngle.AngleLerp((targetPlayer.Center - npc.Center).ToRotation(), 0.4f);
                pose.PupilOut = MathHelper.Lerp(pose.PupilOut, 1f, 0.2f);
                pose.Glow = MathHelper.Lerp(pose.Glow, 1f, 0.2f);
                //蓄势后半段亮起冲线预警
                float windupT = (sub - MLordTidalPalmsState.BlinkLen) / (float)MLordTidalPalmsState.WindupLen;
                slamTelegraph = MathHelper.Clamp((windupT - 0.45f) / 0.4f, 0f, 1f);
            }
            else if (MLordTidalPalmsState.InDash(sub)) {
                gripFrame = MathHelper.Lerp(gripFrame, 3f, 0.5f);
                pose.PupilAngle = npc.velocity.ToRotation();
                pose.PupilOut = 1f;
                pose.Glow = 1f;
                slamTelegraph = 0f;
                //冲线星尘剥落
                if (!VaultUtils.isServer && npc.velocity.Length() > 20f) {
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(
                        npc.Center + Main.rand.NextVector2Circular(40f, 40f),
                        -npc.velocity * Main.rand.NextFloat(0.08f, 0.2f),
                        MLordDirector.Phantasmal, Main.rand.NextFloat(0.5f, 1f))?.Configure(false, Main.rand.Next(12, 20));
                }
            }
            else if (MLordTidalPalmsState.InRecover(sub)) {
                gripFrame = MathHelper.Lerp(gripFrame, 1f, 0.15f);
                pose.PupilOut = MathHelper.Lerp(pose.PupilOut, 0.6f, 0.1f);
                pose.Glow = MathHelper.Lerp(pose.Glow, 0.8f, 0.1f);
                slamTelegraph = 0f;
            }
        }

        /// <summary>硬直期（掌击收势）眼开可打；其余按状态表</summary>
        private bool ComputeEyeOpen(MLordStateIndex coreState, int stateTimer) {
            switch (coreState) {
                case MLordStateIndex.Concerto:
                case MLordStateIndex.CrescentClose:
                case MLordStateIndex.GravityCollapse:
                case MLordStateIndex.MoonBite:
                    return true;
                case MLordStateIndex.TidalPalms: {
                    int sub = stateTimer % MLordTidalPalmsState.CycleLen;
                    return MLordTidalPalmsState.InRecover(sub)
                        || stateTimer >= MLordTidalPalmsState.SlamCount * MLordTidalPalmsState.CycleLen;
                }
                default:
                    return false;
            }
        }

        /// <summary>编队目标点：按核心状态取阵位</summary>
        private Vector2 ComputeFormationGoal(NPC core, MLordStateIndex coreState, bool hold) {
            float dir = (int)npc.ai[MLordAiSlots.HandSide] == 0 ? -1f : 1f;
            float clock = MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvFormationClock);
            //常态巢位 + 呼吸浮动
            Vector2 home = core.Center + new Vector2(MLordDirector.HandHomeOffset.X * dir, MLordDirector.HandHomeOffset.Y);
            Vector2 breath = new((float)Math.Sin(clock * 0.021f + dir * 1.7f) * 26f,
                (float)Math.Cos(clock * 0.017f + dir) * 22f);

            //目标失效或全体僵直：一律回巢
            if (hold || !targetPlayer.Alives()) {
                return home + breath * 0.3f;
            }

            switch (coreState) {
                case MLordStateIndex.CrescentClose:
                    //高位侧翼支点
                    return core.Center + new Vector2(430f * dir, -300f) + breath * 0.4f;
                case MLordStateIndex.GravityCollapse:
                    //贴核心投掷位
                    return core.Center + new Vector2(260f * dir, -30f) + breath * 0.5f;
                case MLordStateIndex.MoonBite: {
                    //以玩家为轴的慢压合围（正弦相位相对，往复推挤）
                    float sweep = (float)Math.Sin(clock * 0.024f) * 260f;
                    return targetPlayer.Center + new Vector2(dir * (480f - sweep * dir), -60f);
                }
                case MLordStateIndex.DeathrayScan:
                    //扫描期外扩留出视野
                    return core.Center + new Vector2(470f * dir, -150f) + breath;
                default:
                    return home + breath;
            }
        }

        #endregion

        #region 破坏残口行为

        private void UpdateBroken(NPC core) {
            npc.dontTakeDamage = true;
            npc.damage = 0;
            slamTelegraph = 0f;
            wriggleTimer++;

            //残口挂回巢位漂浮
            float dir = (int)npc.ai[MLordAiSlots.HandSide] == 0 ? -1f : 1f;
            float clock = MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvFormationClock);
            Vector2 home = core.Center + new Vector2(MLordDirector.HandHomeOffset.X * dir, MLordDirector.HandHomeOffset.Y)
                + new Vector2((float)Math.Sin(clock * 0.013f + dir * 2f) * 18f, (float)Math.Cos(clock * 0.011f) * 14f);
            Vector2 want = (home - npc.Center) * 0.05f;
            if (want.Length() > 9f) {
                want = want.SafeNormalize(Vector2.Zero) * 9f;
            }
            npc.velocity = Vector2.Lerp(npc.velocity, want, 0.12f);

            pose.Broken = true;
            pose.WriggleTimer = wriggleTimer;
            gripFrame = MathHelper.Lerp(gripFrame, 2f, 0.05f);
        }

        #endregion

        #region 死亡与绘制

        /// <summary>
        /// 防御性兜底：原版 checkDead 的 397 特判先于本钩子执行并自行转破（写 -2 + 生成真眼），
        /// 常规路径走不到这里。若钩子先行（顺序变动），这里镜像原版完成同一件事
        /// </summary>
        public override bool? CheckDead() {
            if (npc.ai[MLordAiSlots.PartBroken] != MLordAiSlots.BrokenMark) {
                npc.ai[MLordAiSlots.PartBroken] = MLordAiSlots.BrokenMark;
                npc.life = npc.lifeMax;
                npc.dontTakeDamage = true;
                npc.netUpdate = true;
                if (!VaultUtils.isClient) {
                    int eye = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y,
                        NPCID.MoonLordFreeEye);
                    if (eye < Main.maxNPCs) {
                        Main.npc[eye].ai[MLordAiSlots.PartCoreIndex] = npc.ai[MLordAiSlots.PartCoreIndex];
                        Main.npc[eye].netUpdate = true;
                    }
                }
            }
            return false;
        }

        public override bool CheckActive() => false;

        public override bool FindFrame(int frameHeight) => false;

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //掌击冲线预警（沿瞳孔锁向的细光线，各端近似一致）
            if (slamTelegraph > 0.02f) {
                MLordRayRender.DrawGuideLine(npc.Center, pose.PupilAngle, 1100f, slamTelegraph);
            }
            MLordDrawHelper.DrawHandAssembly(spriteBatch, npc, screenPos, in pose, (int)Math.Round(gripFrame));
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }

        #endregion
    }
}
