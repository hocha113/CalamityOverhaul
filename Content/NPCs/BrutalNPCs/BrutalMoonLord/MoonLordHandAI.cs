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

            //速度门控接触伤害（各端同式）；抓捕合掌与掌中处刑期清零，威胁是抓取判定，不叠撞击；
            //掌击入位划线同样免伤：翼位起手是位移不是攻击（伤害窗只在冲线，契约2.3）
            bool graspWindow = coreState == MLordStateIndex.PalmExecution
                || (coreState == MLordStateIndex.MoonBite && MLordMoonBiteState.InClapWindow(stateTimer));
            bool entryDart = coreState == MLordStateIndex.TidalPalms && MLordTidalPalmsState.InBlink(stateTimer);
            npc.damage = !broken && !graspWindow && !entryDart && npc.velocity.Length() > 24f
                ? MLordDirector.PalmContactDamage : 0;

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
                Span<int> performers = stackalloc int[MLordTidalPalmsState.MaxPerformers];
                int performerCount = MLordTidalPalmsState.ResolvePerformers(coreAI.Context, slamIndex, performers);
                for (int i = 0; i < performerCount; i++) {
                    if (performers[i] == npc.whoAmI) {
                        claimedByState = true;
                        UpdateSlamPose(sub);
                        break;
                    }
                }
            }
            //抓捕合掌拍：全体存活掌被征用（判据与服务端一致：窗口内且存活手≥2且目标有效）
            else if (coreState == MLordStateIndex.MoonBite && coreAI?.Context != null
                && MLordMoonBiteState.InClapWindow(stateTimer)
                && coreAI.Context.Parts.AliveHandCount >= 2
                && targetPlayer.Alives()) {
                claimedByState = true;
                UpdateClapPose(core, stateTimer);
            }
            //掌中处刑：抓握手锁死攥握姿态，其余掌回巢旁观
            else if (coreState == MLordStateIndex.PalmExecution
                && (int)MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvGrabHand) - 1 == npc.whoAmI) {
                claimedByState = true;
                UpdateGripPose(core);
            }

            if (!claimedByState) {
                slamTelegraph = 0f;
                //编队弹簧
                Vector2 goal = ComputeFormationGoal(core, coreAI, coreState, stateTimer, hold);
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

                //协奏声部预备：出弹前抬手亮眼张掌（预备动作即弹幕预告，契约2）
                if (coreState == MLordStateIndex.Concerto && coreAI?.Context != null) {
                    int slot = ((int)npc.ai[MLordAiSlots.HandRow] == 1 ? 2 : 0)
                        + ((int)npc.ai[MLordAiSlots.HandSide] == 0 ? 0 : 1);
                    float windup = MLordConcertoState.BeatWindup(coreAI.Context, stateTimer, slot);
                    if (windup > 0f) {
                        pose.Glow = Math.Max(pose.Glow, windup);
                        pose.PupilOut = Math.Max(pose.PupilOut, 0.85f * windup);
                        gripFrame = MathHelper.Lerp(gripFrame, 0f, 0.3f * windup);
                        if (!VaultUtils.isServer) {
                            MLordScreenFX.ConvergeStreak(npc.Center, 130f, windup * 0.6f);
                        }
                    }
                }
            }

            pose.Broken = false;
            Lighting.AddLight(npc.Center, MLordDirector.Phantasmal.ToVector3() * (0.25f + pose.Glow * 0.4f));
        }

        /// <summary>掌击子相位姿态（入位划线/预警张掌/冲线握拳/硬直摊开）</summary>
        private void UpdateSlamPose(int sub) {
            if (sub < MLordTidalPalmsState.BlinkLen) {
                //入位划线：面朝行进向，起步与落位各一记星尘（位移可见，不做廉价闪现）
                gripFrame = MathHelper.Lerp(gripFrame, 1f, 0.2f);
                if (npc.velocity.LengthSquared() > 9f) {
                    pose.PupilAngle = npc.velocity.ToRotation();
                }
                pose.PupilOut = MathHelper.Lerp(pose.PupilOut, 0.8f, 0.15f);
                pose.Glow = MathHelper.Lerp(pose.Glow, 0.7f, 0.15f);
                slamTelegraph = 0f;
                if (!VaultUtils.isServer && (sub == 1 || sub == MLordTidalPalmsState.BlinkLen - 1)) {
                    MLordScreenFX.StarBurst(npc.Center, 0.55f, 6);
                }
            }
            else if (MLordTidalPalmsState.InWindup(sub)) {
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

        /// <summary>抓捕合掌姿态：张掌追踪→锁定导线定格→合拢冲线（速度由状态服务端直控）</summary>
        private void UpdateClapPose(NPC core, int stateTimer) {
            if (MLordMoonBiteState.InClapTelegraph(stateTimer)) {
                //追踪期瞄活目标，锁定期瞄锚点（导线沿瞳孔方向绘制）
                Vector2 aimPoint = targetPlayer.Center;
                if (MLordMoonBiteState.InClapLock(stateTimer)) {
                    aimPoint = new Vector2(
                        MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvAnchorX, aimPoint.X),
                        MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvAnchorY, aimPoint.Y));
                }
                gripFrame = MathHelper.Lerp(gripFrame, 0.2f, 0.25f);
                pose.PupilAngle = pose.PupilAngle.AngleLerp((aimPoint - npc.Center).ToRotation(), 0.4f);
                pose.PupilOut = MathHelper.Lerp(pose.PupilOut, 1f, 0.2f);
                pose.Glow = MathHelper.Lerp(pose.Glow, 1f, 0.16f);
                //导线亮度：追踪期渐亮，锁定期满亮定格
                int sub = stateTimer - MLordMoonBiteState.ClapStart;
                float t = sub / (float)MLordMoonBiteState.ClapTrackLen;
                slamTelegraph = MLordMoonBiteState.InClapLock(stateTimer)
                    ? 1f : MathHelper.Clamp((t - 0.3f) / 0.6f, 0f, 1f);
            }
            else if (MLordMoonBiteState.InClapLunge(stateTimer)) {
                //合拢冲线：全开掌型扑抓，星尘剥落
                gripFrame = MathHelper.Lerp(gripFrame, 0f, 0.5f);
                if (npc.velocity.LengthSquared() > 16f) {
                    pose.PupilAngle = npc.velocity.ToRotation();
                }
                pose.PupilOut = 1f;
                pose.Glow = 1f;
                slamTelegraph = 0f;
                if (!VaultUtils.isServer && npc.velocity.Length() > 20f) {
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(
                        npc.Center + Main.rand.NextVector2Circular(40f, 40f),
                        -npc.velocity * Main.rand.NextFloat(0.08f, 0.2f),
                        MLordDirector.Phantasmal, Main.rand.NextFloat(0.5f, 1f))?.Configure(false, Main.rand.Next(12, 20));
                }
            }
            else {
                //硬刹收势
                gripFrame = MathHelper.Lerp(gripFrame, 1f, 0.12f);
                pose.PupilOut = MathHelper.Lerp(pose.PupilOut, 0.6f, 0.1f);
                pose.Glow = MathHelper.Lerp(pose.Glow, 0.7f, 0.1f);
                slamTelegraph = 0f;
            }
        }

        /// <summary>掌中处刑攥握姿态：紧握拳型，瞳孔盯死被抓者</summary>
        private void UpdateGripPose(NPC core) {
            gripFrame = MathHelper.Lerp(gripFrame, 3f, 0.4f);
            slamTelegraph = 0f;
            int victimIndex = (int)MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvGrabTarget) - 1;
            Vector2 aim = victimIndex >= 0 && victimIndex < Main.maxPlayers && Main.player[victimIndex].active
                ? Main.player[victimIndex].Center : targetPlayer.Center;
            pose.PupilAngle = pose.PupilAngle.AngleLerp((aim - npc.Center).ToRotation(), 0.35f);
            pose.PupilOut = MathHelper.Lerp(pose.PupilOut, 0.9f, 0.15f);
            pose.Glow = MathHelper.Lerp(pose.Glow, 1f, 0.12f);
        }

        /// <summary>硬直期（掌击收势）眼开可打；其余按状态表</summary>
        private bool ComputeEyeOpen(MLordStateIndex coreState, int stateTimer) {
            switch (coreState) {
                case MLordStateIndex.Concerto:
                case MLordStateIndex.CrescentClose:
                case MLordStateIndex.GravityCollapse:
                case MLordStateIndex.MoonBite:
                //掌中处刑全程可打：队友击破抓握之手即提前救人
                case MLordStateIndex.PalmExecution:
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

        /// <summary>编队目标点：按核心状态与行位（上对/下对）取阵位，四臂分声部不同位</summary>
        private Vector2 ComputeFormationGoal(NPC core, MoonLordCoreAI coreAI, MLordStateIndex coreState,
            int stateTimer, bool hold) {
            float dir = (int)npc.ai[MLordAiSlots.HandSide] == 0 ? -1f : 1f;
            int row = (int)npc.ai[MLordAiSlots.HandRow] == 1 ? 1 : 0;
            int slot = row * 2 + ((int)npc.ai[MLordAiSlots.HandSide] == 0 ? 0 : 1);
            float clock = MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvFormationClock);

            //常态巢位（上对高展、下对外张，X 形构图）+ 同相呼吸：
            //四臂共享一个呼吸相位（上→下 0.15rad 级联），X 分量沿外向展开——
            //一个生物的胸腔起伏，而非四只手各漂各的
            Vector2 homeOffset = row == 0 ? MLordDirector.HandHomeOffset : MLordDirector.LowerHandHomeOffset;
            Vector2 home = core.Center + new Vector2(homeOffset.X * dir, homeOffset.Y);
            Vector2 breath = new((float)Math.Sin(clock * 0.021f - row * 0.15f) * 20f * dir,
                (float)Math.Cos(clock * 0.017f - row * 0.15f) * 22f);

            //目标失效或全体僵直：一律回巢
            if (hold || !targetPlayer.Alives()) {
                return home + breath * 0.3f;
            }

            switch (coreState) {
                case MLordStateIndex.TidalPalms:
                    //非执行手收拢护体（分声部：出手的打、其余的架），冲线的手由状态直控不走此处
                    return core.Center + new Vector2((homeOffset.X - 96f) * dir, homeOffset.Y + 26f)
                        + breath * 0.5f;
                case MLordStateIndex.Concerto: {
                    //即将出手的声部预备抬起（预备动作兼弹幕预告）
                    float windup = coreAI?.Context != null
                        ? MLordConcertoState.BeatWindup(coreAI.Context, stateTimer, slot) : 0f;
                    Vector2 lift = new(26f * dir * windup, -48f * windup);
                    return home + breath + lift;
                }
                case MLordStateIndex.CrescentClose:
                    //上对高位支点持弧，下对低位外张支点（放出封底弧后原位持握）
                    return row == 0
                        ? core.Center + new Vector2(430f * dir, -300f) + breath * 0.4f
                        : core.Center + new Vector2(380f * dir, 142f) + breath * 0.4f;
                case MLordStateIndex.GravityCollapse:
                    //贴核心投掷位：上对肩前，下对腋下
                    return row == 0
                        ? core.Center + new Vector2(260f * dir, -30f) + breath * 0.5f
                        : core.Center + new Vector2(320f * dir, 52f) + breath * 0.5f;
                case MLordStateIndex.MoonBite: {
                    //四臂合围：四掌踞于绕玩家缓旋的方阵四角，半径随呼吸收放。
                    //公平声明：环绕弹簧限速 14 低于接触伤门控 24，本阵只是压迫走位、
                    //不构成伤害环（豁免缺口契约）；伤害窗只在预告完整的合掌冲线
                    float ringAngle = clock * 0.012f + slot * MathHelper.PiOver2;
                    float radius = 470f - (float)Math.Sin(clock * 0.024f) * 90f;
                    return targetPlayer.Center + ringAngle.ToRotationVector2() * radius;
                }
                case MLordStateIndex.DeathrayScan:
                    //扫描期四臂持握转向的仪式阵：上对高举、下对低捧，环拱头部
                    return row == 0
                        ? core.Center + new Vector2(470f * dir, -170f) + breath
                        : core.Center + new Vector2(430f * dir, 112f) + breath;
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

            //残口挂回巢位漂浮（上下对各归其位）
            float dir = (int)npc.ai[MLordAiSlots.HandSide] == 0 ? -1f : 1f;
            Vector2 homeOffset = (int)npc.ai[MLordAiSlots.HandRow] == 1
                ? MLordDirector.LowerHandHomeOffset : MLordDirector.HandHomeOffset;
            float clock = MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvFormationClock);
            Vector2 home = core.Center + new Vector2(homeOffset.X * dir, homeOffset.Y)
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
