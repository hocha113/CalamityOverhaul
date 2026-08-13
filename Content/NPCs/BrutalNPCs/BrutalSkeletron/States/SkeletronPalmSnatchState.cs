using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Projectiles;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.Cinematics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States
{
    /// <summary>
    /// 合掌拍捉（头侧主状态，投技）：双掌张开对峙→同步对拍；<br/>
    /// 拍空双掌互击进入失衡惩罚窗；拍中夹人举到颅前受诅咒骷髅环轰，双掌携人砸地收尾<br/>
    /// 广播契约：ai[1]=被抓玩家 whoAmI+1（0=无），ai[3]=子相位；受害端读取自锁自解
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronStateIndex.PalmSnatch, typeof(SkeletronStateContext))]
    internal class SkeletronPalmSnatchState : SkeletronStateBase
    {
        public override string StateName => "PalmSnatch";
        public override SkeletronStateIndex StateIndex => SkeletronStateIndex.PalmSnatch;

        #region 子相位契约（写入头 ai[3]，各端读取；抓取路径单调递增）
        internal const int SubFlank = 0;      //双掌就位
        internal const int SubTelegraph = 1;  //对峙预警
        internal const int SubSnap = 2;       //合拍闭合
        internal const int SubClamp = 3;      //夹持顿帧
        internal const int SubLift = 4;       //举至颅前
        internal const int SubBarrage = 5;    //诅咒环轰
        internal const int SubWindup = 6;     //收尾蓄势
        internal const int SubSlam = 7;       //携人下砸
        internal const int SubRecover = 8;    //砸地恢复（终结信号，ai[1] 保留到离场）
        internal const int SubWhiff = 9;      //拍空/中断恢复（ai[1] 已清，受害端静默释放）
        #endregion

        #region 时间线常量
        internal const int FlankMax = 40;     //就位超时
        internal const int SnapMax = 26;      //闭合超时（超时未捕→拍空）
        internal const int ClampLen = 22;     //夹持顿帧
        internal const int LiftLen = 38;      //举升
        internal const int BarrageLen = 114;  //环轰总长
        internal const int WaveInterval = 38; //环轰波间隔
        internal const int WindupLen = 26;    //蓄势
        internal const int SlamMax = 24;      //下砸超时
        internal const int RecoverLen = 54;   //恢复拍
        internal const int WhiffLen = 60;     //拍空惩罚窗
        #endregion

        //服务端权威事实（客户端仅凭 ai[] 广播呈现）
        private int subTimer;
        private int grabIndex = -1;
        private Vector2 clapAnchor;
        private float slamGroundY;
        private int barrageWave;
        private bool caught;
        //各端演出去重锚
        private int subLatch = -1;

        public override void OnEnter(SkeletronStateContext context) {
            base.OnEnter(context);
            subTimer = 0;
            grabIndex = -1;
            barrageWave = 0;
            caught = false;
            subLatch = -1;
            slamGroundY = -1f;
            context.SnatchAnchor = Vector2.Zero;
            context.SnatchAnchorLocked = false;

            if (!VaultUtils.isClient) {
                context.Npc.ai[SkeletronAiSlots.HeadParamA] = 0f;
                context.Npc.ai[SkeletronAiSlots.HeadParamB] = SubFlank;
                //进场即压冷却，拍空离场时再改短
                context.SnatchCooldown = SkeletronDirector.SnatchCooldownTicks;
                context.Npc.netUpdate = true;
            }
        }

        public override void OnExit(SkeletronStateContext context) {
            base.OnExit(context);
            context.SnatchAnchor = Vector2.Zero;
            context.SnatchAnchorLocked = false;
            //出口清广播：ai[1] 清零 + 子相位置为中断码，受害端凭此静默释放
            if (!VaultUtils.isClient) {
                context.Npc.ai[SkeletronAiSlots.HeadParamA] = 0f;
                context.Npc.ai[SkeletronAiSlots.HeadParamB] = SubWhiff;
                context.Npc.netUpdate = true;
            }
        }

        public override ISkeletronState OnUpdate(SkeletronStateContext context) {
            NPC npc = context.Npc;
            //投技全程头无接触伤害（威胁全在双掌与弹幕）
            npc.damage = 0;

            int sub = (int)npc.ai[SkeletronAiSlots.HeadParamB];

            //各端演出去重：子相位前进沿播放一次性反馈
            if (sub != subLatch) {
                bool forward = sub > subLatch;
                subLatch = sub;
                subTimer = 0;
                if (forward) {
                    PlaySubPhaseCue(context, sub);
                }
            }

            UpdateHeadMotion(context, sub);
            UpdateVisualEnvelope(context, sub);

            //权威推进
            if (!VaultUtils.isClient) {
                ISkeletronState next = AuthorityUpdate(context, sub);
                if (next != null) {
                    return next;
                }
            }

            subTimer++;
            Timer++;
            return null;
        }

        #region 权威逻辑

        private ISkeletronState AuthorityUpdate(SkeletronStateContext context, int sub) {
            NPC npc = context.Npc;

            //保底超时：任何异常都不允许滞留
            if (Timer > 700) {
                return new SkeletronHubState();
            }

            //抓取期目标与手部有效性校验
            if (caught && sub >= SubClamp && sub <= SubSlam && !ValidateGrab(context)) {
                AbortGrab(npc);
                return null;
            }

            switch (sub) {
                case SubFlank:
                    //双掌到位或超时即进入对峙
                    if (HandsInFlankPosition(context) || subTimer >= FlankMax) {
                        //拍捉需要双手，缺手直接放弃
                        if (context.HandCount < 2) {
                            AbortGrab(npc);
                            return null;
                        }
                        SetSub(npc, SubTelegraph);
                    }
                    break;

                case SubTelegraph: {
                    int telegraph = SkeletronDirector.SnatchTelegraphFrames;
                    int lockFrame = telegraph - SkeletronDirector.SnatchAnchorLockFrames;
                    //末拍锁定锚点（公平阀：给玩家脱离走廊的读秒窗）
                    if (subTimer == lockFrame) {
                        context.SnatchAnchor = context.Target.Center;
                        context.SnatchAnchorLocked = true;
                        npc.netUpdate = true;
                    }
                    if (subTimer >= telegraph) {
                        clapAnchor = context.SnatchAnchorLocked ? context.SnatchAnchor : context.Target.Center;
                        SetSub(npc, SubSnap);
                    }
                    break;
                }

                case SubSnap: {
                    if (context.HandCount < 2) {
                        AbortGrab(npc);
                        break;
                    }
                    //捕获判定：双掌闭合到位 + 玩家仍在囚笼盒内
                    if (TryCatch(context)) {
                        caught = true;
                        grabIndex = context.Target.whoAmI;
                        npc.ai[SkeletronAiSlots.HeadParamA] = grabIndex + 1;
                        SetSub(npc, SubClamp);
                        break;
                    }
                    if (PalmsArrived(context) || subTimer >= SnapMax) {
                        //拍空：双掌互击，进入失衡惩罚窗，冷却改短
                        context.SnatchCooldown = Math.Min(context.SnatchCooldown, SkeletronDirector.SnatchWhiffCooldownTicks);
                        if (context.LeftHand != null) {
                            context.LeftHand.ai[SkeletronAiSlots.HandFree] += 1f;
                            context.LeftHand.netUpdate = true;
                        }
                        SetSub(npc, SubWhiff);
                    }
                    break;
                }

                case SubClamp:
                    if (subTimer >= ClampLen) {
                        SetSub(npc, SubLift);
                    }
                    break;

                case SubLift:
                    if (subTimer >= LiftLen) {
                        SetSub(npc, SubBarrage);
                    }
                    break;

                case SubBarrage:
                    //三轮诅咒骷髅绕环俯冲
                    if (subTimer % WaveInterval == 0 && barrageWave < 3) {
                        SpawnSkullRing(context, barrageWave);
                        barrageWave++;
                    }
                    if (subTimer >= BarrageLen) {
                        SetSub(npc, SubWindup);
                    }
                    break;

                case SubWindup:
                    if (subTimer >= WindupLen) {
                        slamGroundY = SkeletronFacts.FindGroundY(GetCageCenter(npc), 90);
                        if (slamGroundY < 0f) {
                            slamGroundY = GetCageCenter(npc).Y + 620f;
                        }
                        SetSub(npc, SubSlam);
                    }
                    break;

                case SubSlam: {
                    bool grounded = false;
                    NPC probe = context.LeftHand ?? context.RightHand;
                    if (probe != null) {
                        grounded = probe.Center.Y >= slamGroundY - 46f
                            || Collision.SolidCollision(probe.position, probe.width, probe.height);
                    }
                    if (grounded || subTimer >= SlamMax) {
                        OnSlamImpact(context);
                        SetSub(npc, SubRecover);
                    }
                    break;
                }

                case SubRecover:
                    if (subTimer >= RecoverLen) {
                        return new SkeletronHubState();
                    }
                    break;

                case SubWhiff:
                    if (subTimer >= WhiffLen) {
                        return new SkeletronHubState();
                    }
                    break;
            }
            return null;
        }

        /// <summary>抓取期有效性：玩家在场、活着、没被传走，双手健在</summary>
        private bool ValidateGrab(SkeletronStateContext context) {
            if (grabIndex < 0 || grabIndex >= Main.maxPlayers) {
                return false;
            }
            Player player = Main.player[grabIndex];
            if (player == null || !player.active || player.dead) {
                return false;
            }
            if (context.HandCount < 2) {
                return false;
            }
            //回忆药水等瞬移逃逸的兜底断投
            return player.Center.Distance(GetCageCenter(context.Npc)) < 900f;
        }

        /// <summary>中断抓取：清目标广播，转入失衡恢复窗</summary>
        private void AbortGrab(NPC npc) {
            caught = false;
            grabIndex = -1;
            npc.ai[SkeletronAiSlots.HeadParamA] = 0f;
            SetSub(npc, SubWhiff);
        }

        private void SetSub(NPC npc, int sub) {
            npc.ai[SkeletronAiSlots.HeadParamB] = sub;
            //子相位是一次性事件，立即广播（头部常规是10帧节流）
            npc.netUpdate = true;
        }

        /// <summary>双掌是否到达对峙位</summary>
        private static bool HandsInFlankPosition(SkeletronStateContext context) {
            if (context.LeftHand == null || context.RightHand == null) {
                return false;
            }
            float flank = SkeletronDirector.SnatchFlankDistance;
            Vector2 leftSlot = context.Target.Center + new Vector2(-flank, 0f);
            Vector2 rightSlot = context.Target.Center + new Vector2(flank, 0f);
            return context.LeftHand.Center.Distance(leftSlot) < 130f
                && context.RightHand.Center.Distance(rightSlot) < 130f;
        }

        /// <summary>双掌均已闭合到锚点两侧槽位</summary>
        private bool PalmsArrived(SkeletronStateContext context) {
            float halfGap = SkeletronDirector.SnatchHalfGap;
            return context.LeftHand != null && context.RightHand != null
                && context.LeftHand.Center.Distance(clapAnchor + new Vector2(-halfGap, 0f)) < 34f
                && context.RightHand.Center.Distance(clapAnchor + new Vector2(halfGap, 0f)) < 34f;
        }

        /// <summary>捕获判定：双掌闭合足够近 + 玩家命中盒仍在囚笼盒内</summary>
        private bool TryCatch(SkeletronStateContext context) {
            Player target = context.Target;
            if (!target.Alives()) {
                return false;
            }
            NPC left = context.LeftHand;
            NPC right = context.RightHand;
            if (left == null || right == null) {
                return false;
            }
            float closeSpan = SkeletronDirector.SnatchHalfGap * 2f + 150f;
            if (Math.Abs(left.Center.X - right.Center.X) > closeSpan) {
                return false;
            }
            float halfW = SkeletronDirector.SnatchHalfGap + 40f;
            Rectangle cage = new Rectangle((int)(clapAnchor.X - halfW), (int)(clapAnchor.Y - 80f),
                (int)(halfW * 2f), 160);
            return cage.Intersects(target.Hitbox);
        }

        /// <summary>环轰一轮：8 枚诅咒骷髅绕囚笼成环，错拍向心俯冲</summary>
        private void SpawnSkullRing(SkeletronStateContext context, int wave) {
            NPC npc = context.Npc;
            Vector2 cage = GetCageCenter(npc);
            int damage = SkullDamage(context) + 2;
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f + wave * 0.39f;
                Vector2 pos = cage + angle.ToRotationVector2() * SkeletronOrbitSkull.OrbitRadius;
                //俯冲延迟逐枚错拍（波纹读法），全部数据走 ai[] 随出生包同步
                Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                    ModContent.ProjectileType<SkeletronOrbitSkull>(), damage, 0f, Main.myPlayer,
                    angle, npc.whoAmI, SkeletronOrbitSkull.BaseDiveDelay + i * 2f);
            }
            npc.netUpdate = true;
        }

        /// <summary>砸地终结：双掌冲击反馈 + 骨刺自落点向两侧隆起</summary>
        private void OnSlamImpact(SkeletronStateContext context) {
            NPC npc = context.Npc;
            Vector2 cage = GetCageCenter(npc);

            //冲击广播计数（客户端凭此播落掌反馈）
            if (context.LeftHand != null) {
                context.LeftHand.ai[SkeletronAiSlots.HandFree] += 1f;
                context.LeftHand.netUpdate = true;
            }
            if (context.RightHand != null) {
                context.RightHand.ai[SkeletronAiSlots.HandFree] += 1f;
                context.RightHand.netUpdate = true;
            }

            int damage = SkeletronHeadAI.GetSkullDamage(npc);
            for (int i = -2; i <= 2; i++) {
                float x = cage.X + i * 98f;
                float gy = SkeletronFacts.FindGroundY(new Vector2(x, cage.Y - 120f));
                if (gy <= 0f) {
                    continue;
                }
                Projectile.NewProjectile(npc.GetSource_FromAI(), new Vector2(x, gy), Vector2.Zero,
                    ModContent.ProjectileType<SkeletronBoneSpike>(), damage, 0f, Main.myPlayer,
                    Math.Abs(i) * 4f, 1.2f - Math.Abs(i) * 0.08f);
            }
        }

        #endregion

        #region 运动与演出

        /// <summary>头部动线：对峙居高观刑，抓住后压近凝视，砸地后失力漂浮</summary>
        private void UpdateHeadMotion(SkeletronStateContext context, int sub) {
            NPC npc = context.Npc;
            Vector2 want;
            float pull;

            switch (sub) {
                case SubFlank:
                case SubTelegraph:
                case SubSnap: {
                    Vector2 focus = context.SnatchAnchorLocked ? context.SnatchAnchor : context.Target.Center;
                    want = focus + new Vector2(0f, -430f);
                    pull = 0.035f;
                    break;
                }
                case SubClamp:
                case SubLift:
                case SubBarrage:
                case SubWindup: {
                    //凝视位偏移与手部伺服锚（头下 205px）严格一致才有均衡点，否则头手互追整体无限上漂；
                    //举升相位加一段临时失配：失配×拉力≈3.4px/t 的受控爬升，38t 约升 130px；
                    //蓄势急提由手侧目标跳变完成，头保持均衡
                    Vector2 cage = GetCageCenter(npc);
                    float liftDrive = sub == SubLift ? 68f : 0f;
                    want = new Vector2(cage.X, cage.Y - 205f - liftDrive);
                    pull = sub == SubClamp ? 0.03f : 0.05f;
                    break;
                }
                case SubSlam:
                    //观刑：悬停不动
                    npc.velocity *= 0.85f;
                    SettleRotation(npc, 0.15f);
                    return;
                case SubRecover:
                    npc.velocity *= 0.95f;
                    npc.velocity.Y -= 0.02f;
                    SettleRotation(npc, 0.06f);
                    return;
                default: //SubWhiff
                    npc.velocity *= 0.92f;
                    SettleRotation(npc, 0.08f);
                    return;
            }

            npc.velocity = (want - npc.Center) * pull;
            SettleRotation(npc, 0.12f);
        }

        /// <summary>视觉包络：眼火随相位起伏（各端本地）</summary>
        private void UpdateVisualEnvelope(SkeletronStateContext context, int sub) {
            float eye = sub switch {
                SubTelegraph => 1.2f,
                SubSnap => 1.35f,
                SubClamp => 1.4f,
                SubLift or SubBarrage => 1.5f,
                SubWindup or SubSlam => 1.55f,
                SubRecover => 0.35f,
                SubWhiff => 0.5f,
                _ => 1f,
            };
            context.EyeFlame = MathHelper.Lerp(context.EyeFlame, eye, 0.1f);
        }

        /// <summary>子相位前进沿的一次性声画（各端本地，服务端静默）</summary>
        private void PlaySubPhaseCue(SkeletronStateContext context, int sub) {
            if (VaultUtils.isServer) {
                return;
            }
            NPC npc = context.Npc;
            Vector2 cage = GetCageCenter(npc);

            switch (sub) {
                case SubTelegraph:
                    //对峙咆哮：投技专属预警音
                    SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Volume = 0.9f, Pitch = -0.5f }, npc.Center);
                    break;
                case SubClamp:
                    //夹持顿帧：闷钟 + 重击 + 冲击环
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 1f, Pitch = -0.35f }, cage);
                    SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.95f, Pitch = -0.7f }, cage);
                    SkeletronScreenEffects.PushShockRing(cage, 0.7f, 380f, 20);
                    SkeletronScreenEffects.PushShake(cage, 8f);
                    break;
                case SubWindup:
                    SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Volume = 0.85f, Pitch = -0.25f }, npc.Center);
                    SkeletronScreenEffects.PushShake(cage, 4f);
                    break;
                case SubSlam:
                    SoundEngine.PlaySound(SoundID.DD2_GhastlyGlaivePierce with { Volume = 1f, Pitch = -0.5f }, cage);
                    break;
                case SubRecover:
                    //落地大环（落掌钝响由手部冲击计数广播承担）
                    SkeletronScreenEffects.PushShockRing(cage, 0.95f, 640f, 26);
                    break;
            }
        }

        #endregion

        #region 对外契约

        /// <summary>囚笼中心：双掌中点，缺手回退颅前位</summary>
        internal static Vector2 GetCageCenter(NPC head) {
            int hands = SkeletronFacts.CountHands(head, out NPC left, out NPC right);
            if (hands == 2) {
                return (left.Center + right.Center) * 0.5f;
            }
            if (hands == 1) {
                return (left ?? right).Center;
            }
            return head.Center + new Vector2(0f, 210f);
        }

        /// <summary>投技派发门闸（Hub 调用，仅权威端）：一阶段双手健在、血量达标、冷却完毕、无演出无时停</summary>
        internal static bool CanDispatch(SkeletronStateContext context) {
            NPC npc = context.Npc;
            if ((int)npc.ai[SkeletronAiSlots.HeadPhase] != SkeletronPhase.Bound) {
                return false;
            }
            if (context.HandCount < 2 || context.SnatchCooldown > 0) {
                return false;
            }
            if (npc.life > npc.lifeMax * SkeletronDirector.SnatchLifeGate) {
                return false;
            }
            if (Main.IsItDay() && !context.BossRush) {
                return false;
            }
            if (WorldFreezeSystem.IsActive) {
                return false;
            }
            //单机端演出播放期间不出投技（专用服此值恒空）
            if (CutsceneDirector.CurrentClip != null) {
                return false;
            }
            return context.Target.Alives();
        }

        #endregion
    }
}
