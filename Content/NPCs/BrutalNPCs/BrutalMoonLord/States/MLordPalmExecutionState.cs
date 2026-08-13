using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States
{
    /// <summary>
    /// 掌中处刑（投技演出段）：合掌命中后攥住玩家→顿帧→拖到头颅面前→
    /// 触须两记抽打→额眼贴脸死光横扫（另一束擦颊而过）→引力坍缩蓄压→反手甩落收尾。
    /// 时间轴用原始帧不吃节奏压缩（连段间距与受击无敌帧耦合）。
    /// 位移与锁控全部在被抓玩家自己的客户端（<see cref="MLordGrabPlayer"/>），
    /// 本状态只负责权威节拍、手部驱动与弹幕；全程手/头睁眼可打，
    /// 队友击破抓握之手可提前救人
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.PalmExecution, typeof(MLordContext))]
    internal class MLordPalmExecutionState : MLordStateBase
    {
        public override string StateName => "PalmExecution";
        public override MLordStateIndex StateIndex => MLordStateIndex.PalmExecution;

        //―――― 时间轴（原始帧）――――
        /// <summary>攥握顿帧结束</summary>
        internal const int HitstopEnd = 8;
        /// <summary>拖曳到面前结束</summary>
        internal const int DragEnd = 40;
        /// <summary>触须抽打两拍（间距 ≥48：跨过 Boss 槽受击无敌）</summary>
        internal const int Lash1Tick = 48;
        internal const int Lash2Tick = 96;
        /// <summary>贴脸死光生成帧（自带 44 帧预警，出束于 144）</summary>
        internal const int RaySpawnTick = 100;
        internal const int RayTelegraph = 44;
        /// <summary>擦颊虚惊束生成帧（角度偏出，不打被抓者）</summary>
        internal const int NearMissSpawnTick = 110;
        /// <summary>引力坍缩蓄压开始</summary>
        internal const int CollapseStart = 150;
        /// <summary>甩落释放帧</summary>
        internal const int ReleaseTick = 180;
        /// <summary>恢复拍结束（回到出招表）</summary>
        internal const int RecoverEnd = 240;

        /// <summary>持握点相对头颅中心偏移（额眼正前下方）</summary>
        internal static Vector2 HoldOffset => new(0f, 150f);
        /// <summary>甩落初速</summary>
        internal const float FlingSpeed = 34f;

        public override void OnEnter(MLordContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isClient) {
                //处刑期间真眼退避旁观（也免其火力越过连段伤害预算）
                context.Owner.ai[MLordAiSlots.OvEyeCommand] = MLordEyeCommand.Retreat;
                context.Npc.netUpdate = true;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie97 with { Volume = 1.1f, Pitch = -0.35f }, context.Npc.Center);
            }
        }

        public override void OnExit(MLordContext context) {
            base.OnExit(context);
            if (!VaultUtils.isClient) {
                //异常中断（死亡/月退抢占）也保证松手：清投技槽让被抓端立刻解钉
                context.Owner.ai[MLordAiSlots.OvGrabTarget] = 0f;
                context.Owner.ai[MLordAiSlots.OvGrabHand] = 0f;
                context.Owner.ai[MLordAiSlots.OvEyeCommand] = MLordEyeCommand.Solo;
                context.Npc.netUpdate = true;
            }
        }

        public override IMLordState OnUpdate(MLordContext context) {
            NPC npc = context.Npc;

            //整套阵形定桩：处刑期间不追目标，头颅保持舞台中心
            npc.velocity *= 0.9f;
            UpdateLean(context);
            context.EclipseDrive = 1f;

            if (!VaultUtils.isClient) {
                RunServer(context);
            }
            UpdatePresentation(context);

            Timer++;
            if (Timer >= RecoverEnd) {
                return NextAttack(context);
            }
            return null;
        }

        #region 服务端权威节拍

        private void RunServer(MLordContext context) {
            bool held = TryResolveGrip(context, out NPC hand, out Player victim);

            //持握有效性逐帧核验：手被打断/目标死亡离场/异常远距 → 提前进入释放
            if (Timer < ReleaseTick && !held) {
                ClearGrabSlots(context);
                Timer = ReleaseTick;
                return;
            }

            if (Timer < ReleaseTick) {
                DriveGripHand(context, hand);
                RunComboBeats(context, victim);
            }
            else if (Timer == ReleaseTick) {
                DoFling(context, hand);
            }
            else {
                //恢复拍：抓握手缓慢刹车归位（编队弹簧在状态结束后自然接管）
                if (hand != null) {
                    hand.velocity *= 0.88f;
                }
            }
        }

        /// <summary>解出抓握之手与被抓玩家；任一失效返回 false</summary>
        private static bool TryResolveGrip(MLordContext context, out NPC hand, out Player victim) {
            hand = null;
            victim = null;
            int handIndex = (int)context.Owner.ai[MLordAiSlots.OvGrabHand] - 1;
            int playerIndex = (int)context.Owner.ai[MLordAiSlots.OvGrabTarget] - 1;
            if (handIndex < 0 || handIndex >= Main.maxNPCs
                || playerIndex < 0 || playerIndex >= Main.maxPlayers) {
                return false;
            }
            NPC candidate = Main.npc[handIndex];
            if (!candidate.active || candidate.type != NPCID.MoonLordHand
                || candidate.ai[MLordAiSlots.PartBroken] == MLordAiSlots.BrokenMark) {
                return false;
            }
            Player player = Main.player[playerIndex];
            if (!player.active || player.dead || player.ghost
                || player.Distance(candidate.Center) > 1800f) {
                return false;
            }
            hand = candidate;
            victim = player;
            return true;
        }

        /// <summary>清空投技槽位（松手信号，被抓端读到即解钉）</summary>
        private static void ClearGrabSlots(MLordContext context) {
            context.Owner.ai[MLordAiSlots.OvGrabTarget] = 0f;
            context.Owner.ai[MLordAiSlots.OvGrabHand] = 0f;
            context.Npc.netUpdate = true;
        }

        /// <summary>驱动抓握手：顿帧→拖到面前→面前紧握（坍缩期带压迫震颤）</summary>
        private void DriveGripHand(MLordContext context, NPC hand) {
            Vector2 holdPoint = HoldPoint(context);

            if (Timer < HitstopEnd) {
                //攥握顿帧：定格 8 帧
                hand.velocity = Vector2.Zero;
                return;
            }
            if (Timer < DragEnd) {
                //拖曳：强弹簧进给，前快后缓
                SpringHand(hand, holdPoint, 38f, 0.2f);
                return;
            }
            //面前紧握：坍缩期叠压迫震颤
            Vector2 goal = holdPoint;
            if (Timer >= CollapseStart) {
                float t = (Timer - CollapseStart) / (float)(ReleaseTick - CollapseStart);
                goal += new Vector2((float)Math.Sin(Timer * 0.9f) * 3f * t, (float)Math.Cos(Timer * 1.1f) * 2f * t);
            }
            SpringHand(hand, goal, 18f, 0.25f);
        }

        /// <summary>连段节拍：触须两抽 + 贴脸死光 + 擦颊虚惊束（全部锚定头颅）</summary>
        private void RunComboBeats(MLordContext context, Player victim) {
            if (context.Parts.Head < 0) {
                return;
            }
            NPC head = Main.npc[context.Parts.Head];
            Vector2 holdPoint = HoldPoint(context);

            if (Timer == Lash1Tick || Timer == Lash2Tick) {
                //触须抽打：命中点即持握点，左右交替
                float side = Timer == Lash1Tick ? -1f : 1f;
                Projectile.NewProjectile(head.GetSource_FromAI(),
                    holdPoint + new Vector2(side * 26f, 0f), Vector2.Zero,
                    ModContent.ProjectileType<MLordGrabLashProj>(),
                    ScaleDamage(context, MLordDirector.GrabLashDamage), 0f, Main.myPlayer,
                    head.whoAmI, side);
            }

            if (Timer == RaySpawnTick) {
                //贴脸主束：自额眼贯穿持握点（被抓者必中的一拍）
                float angle = (holdPoint - head.Center).ToRotation();
                Projectile.NewProjectile(head.GetSource_FromAI(), head.Center, Vector2.Zero,
                    ModContent.ProjectileType<MLordScanRayProj>(),
                    ScaleDamage(context, MLordDirector.GrabRayDamage), 0f, Main.myPlayer,
                    head.whoAmI, angle, RayTelegraph);
            }
            if (Timer == NearMissSpawnTick) {
                //擦颊虚惊束：向被抓者背侧偏 0.3 rad，贴着脸掠过（不命中持握点）
                float side = victim != null && victim.Center.X < head.Center.X ? -1f : 1f;
                float angle = (holdPoint - head.Center).ToRotation() + 0.3f * side;
                Projectile.NewProjectile(head.GetSource_FromAI(), head.Center, Vector2.Zero,
                    ModContent.ProjectileType<MLordScanRayProj>(),
                    ScaleDamage(context, MLordDirector.GrabRayDamage), 0f, Main.myPlayer,
                    head.whoAmI, angle, RayTelegraph);
            }
        }

        /// <summary>甩落：清槽松手 + 抓握手反手下劈（被抓端继承手速成抛物）</summary>
        private void DoFling(MLordContext context, NPC hand) {
            ClearGrabSlots(context);
            if (hand == null) {
                return;
            }
            float side = hand.Center.X - context.Npc.Center.X;
            float sign = Math.Abs(side) < 20f ? ((int)hand.ai[MLordAiSlots.HandSide] == 0 ? -1f : 1f) : Math.Sign(side);
            hand.velocity = new Vector2(sign * 0.55f, 0.85f).SafeNormalize(Vector2.UnitY) * FlingSpeed;
            hand.netUpdate = true;
        }

        #endregion

        #region 各端表现

        private void UpdatePresentation(MLordContext context) {
            if (VaultUtils.isServer) {
                return;
            }
            NPC npc = context.Npc;
            Vector2 stagePos = HoldPoint(context);

            if (Timer == 1) {
                //攥握定格：星尘爆 + 方向冲击（旁观端按距离衰减）
                MLordScreenFX.StarBurst(GripHandCenter(context, stagePos), 1.1f, 16);
                MLordScreenFX.Punch(stagePos, 6f, 10);
            }

            if (Timer >= CollapseStart && Timer < ReleaseTick) {
                //引力坍缩蓄压：吸光 + 向心星流（末段自带静默）
                float t = (Timer - CollapseStart) / (float)(ReleaseTick - CollapseStart);
                Vector2 gripPos = GripHandCenter(context, stagePos);
                MLordScreenEffects.PushGravityDim(gripPos, t * 0.7f);
                MLordScreenFX.ConvergeStreak(gripPos, 380f, t);
                context.HeartExposure = MathHelper.Max(context.HeartExposure, t * 0.4f);
                if (Timer == CollapseStart + 8 || Timer == CollapseStart + 20) {
                    float pitch = Timer == CollapseStart + 8 ? -0.2f : 0.15f;
                    SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.85f, Pitch = pitch }, gripPos);
                }
            }

            if (Timer == ReleaseTick) {
                //甩落终击：一场处刑最重的一拍
                Vector2 gripPos = GripHandCenter(context, stagePos);
                MLordScreenEffects.PushStarRing(gripPos, 1f, 720f, 30);
                MLordScreenFX.StarBurst(gripPos, 1.7f, 24);
                MLordScreenFX.Punch(gripPos, 10f, 18, new Vector2(0f, 1f));
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.1f, Pitch = -0.45f }, gripPos);
                SoundEngine.PlaySound(SoundID.Zombie97 with { Volume = 0.9f, Pitch = -0.7f }, npc.Center);
            }
        }

        /// <summary>抓握手当前位置（表现用，手缺位退回舞台点）</summary>
        private static Vector2 GripHandCenter(MLordContext context, Vector2 fallback) {
            int handIndex = (int)context.Owner.ai[MLordAiSlots.OvGrabHand] - 1;
            if (handIndex < 0 || handIndex >= Main.maxNPCs || !Main.npc[handIndex].active) {
                return fallback;
            }
            return Main.npc[handIndex].Center;
        }

        #endregion

        /// <summary>持握点：头颅面前（头缺位退化为核心上方）</summary>
        internal static Vector2 HoldPoint(MLordContext context) {
            if (context.Parts.Head >= 0 && Main.npc[context.Parts.Head].active) {
                return Main.npc[context.Parts.Head].Center + HoldOffset;
            }
            return context.Npc.Center + MLordDirector.HeadWeldOffset + HoldOffset;
        }

        /// <summary>服务端弹簧进给抓握手</summary>
        private static void SpringHand(NPC hand, Vector2 goal, float maxSpeed, float gain) {
            Vector2 want = (goal - hand.Center) * gain;
            if (want.Length() > maxSpeed) {
                want = want.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            hand.velocity = Vector2.Lerp(hand.velocity, want, 0.3f);
        }
    }
}
