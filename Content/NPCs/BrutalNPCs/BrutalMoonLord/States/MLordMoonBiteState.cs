using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using CalamityOverhaul.Content.TimeFreezes;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States
{
    /// <summary>
    /// 月蚀噬咬：头部（含破损残口）张口甩出月咬之舌锁疗，被咬期间派出星髓凝滴回航吸血，
    /// 凝滴可拦截。四掌踞于绕玩家缓旋方阵四角收放合围（缺口随阵旋转移动），
    /// 头部全程睁眼（高风险高回报窗口）。
    /// 中段插入合掌抓捕拍：四掌散至对角捕位追踪→锁定停顿→向锁点合拢，
    /// 命中即转入掌中处刑投技（合拢本身接触伤害清零，威胁是抓取判定而非撞击）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.MoonBite, typeof(MLordContext))]
    internal class MLordMoonBiteState : MLordStateBase
    {
        public override string StateName => "MoonBite";
        public override MLordStateIndex StateIndex => MLordStateIndex.MoonBite;

        internal const int MouthOpenEnd = 40;
        internal const int BiteEnd = 380;

        //―――― 合掌抓捕拍（原始帧，不吃死亡模式压缩：与受击无敌节奏耦合）――――
        /// <summary>抓捕拍开始（散至捕位并追踪）</summary>
        internal const int ClapStart = 150;
        /// <summary>追踪帧长（预警前半段）</summary>
        internal const int ClapTrackLen = 40;
        /// <summary>锁定停顿帧长（预警后半段，导线定格）</summary>
        internal const int ClapLockLen = 12;
        /// <summary>合拢冲线帧长（抓取判定窗，与可见动作精确对齐）</summary>
        internal const int ClapLungeLen = 16;
        /// <summary>过冲帧长（扑空交错）</summary>
        internal const int ClapOvershootLen = 6;
        /// <summary>硬刹帧长</summary>
        internal const int ClapBrakeLen = 16;
        /// <summary>抓捕拍整窗结束（此后回归合围编队）</summary>
        internal const int ClapEnd = ClapStart + ClapTrackLen + ClapLockLen + ClapLungeLen + ClapOvershootLen + ClapBrakeLen;
        /// <summary>捕位半径</summary>
        internal const float ClapPostRadius = 520f;
        /// <summary>合拢冲线速度</summary>
        internal const float ClapLungeSpeed = 42f;

        private int stateLength;
        /// <summary>服务端已抓住目标，本帧转入处刑</summary>
        private bool caught;

        public override void OnEnter(MLordContext context) {
            base.OnEnter(context);
            stateLength = Frames(context, BiteEnd + 50);
            caught = false;

            //头部实体彻底不在场（极端边界）则跳招
            if (!VaultUtils.isClient && context.Parts.Head < 0) {
                Timer = stateLength;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie94 with { Volume = 1f, Pitch = -0.45f }, context.Npc.Center);
            }
        }

        public override IMLordState OnUpdate(MLordContext context) {
            Player target = context.Target;

            //四掌全被合围阵征用：手阵拖曳携行本体贴近压迫（舌区施压）
            RequestMove(context, target.Center + MLordDirector.CoreHoverOffset + new Vector2(0f, -40f),
                0.7f, MLordMovePolicy.Tow);
            UpdateLean(context);

            if (!VaultUtils.isClient) {
                RunServer(context);
                RunClapServer(context);
            }
            UpdateClapPresentation(context);

            //抓住即转入掌中处刑（客户端由 ai 槽同步跟进）
            if (caught && !VaultUtils.isClient) {
                return CreateState(MLordStateIndex.PalmExecution);
            }

            Timer++;
            if (Timer >= stateLength) {
                return NextAttack(context);
            }
            return null;
        }

        private void RunServer(MLordContext context) {
            if (context.Parts.Head < 0) {
                return;
            }
            NPC head = Main.npc[context.Parts.Head];

            //甩舌：对 3000 内所有玩家各出一条月咬之舌（原版弹幕，锁疗身份保留）
            if (Timer == MouthOpenEnd) {
                Vector2 mouth = head.Center + new Vector2(0f, 216f);
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player player = Main.player[i];
                    if (!player.active || player.dead || player.Distance(mouth) > 3000f) {
                        continue;
                    }
                    Vector2 aim = (player.Center - mouth).SafeNormalize(Vector2.UnitY);
                    Projectile.NewProjectile(head.GetSource_FromAI(), mouth, aim,
                        ProjectileID.MoonLeech, 0, 0f, Main.myPlayer, head.whoAmI + 1, i);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie101 with { Volume = 1f, Pitch = -0.3f }, head.Center);
                }
            }

            //凝滴回航波：被咬玩家处生成星髓凝滴（可拦截的治疗载体）
            int waveInterval = Frames(context, 80);
            if (Timer > MouthOpenEnd && Timer < BiteEnd && (Timer - MouthOpenEnd) % waveInterval == 0) {
                SpawnLeechBlobs(head);
            }

            //慢压期间偶发直射弹补压：活头自口出弹；头破由真眼轮席代射（残口只管咬舌，不当炮口）
            if (Timer > MouthOpenEnd && Timer % Frames(context, 64) == 30) {
                NPC origin = context.Parts.HeadAlive ? head
                    : MLordFacts.GetFreeEye(context.Npc, Timer % MLordFacts.MaxFreeEyes);
                if (origin != null) {
                    Vector2 aim = (context.Target.Center - origin.Center).SafeNormalize(Vector2.UnitY);
                    Projectile.NewProjectile(origin.GetSource_FromAI(), origin.Center + aim * 44f, aim * 6.8f,
                        ModContent.ProjectileType<MLordBoltProj>(), ScaleDamage(context, MLordDirector.BoltDamage), 0f, Main.myPlayer);
                }
            }
        }

        #region 合掌抓捕（服务端驱动）

        /// <summary>服务端抓捕拍：驱动四掌散位→锁定→合拢，冲线期做抓取判定</summary>
        private void RunClapServer(MLordContext context) {
            if (caught || Timer < ClapStart || Timer >= ClapEnd) {
                return;
            }
            //公平阀（逐帧活判定，与手部姿态征用判据同源）：
            //世界时停/无有效目标/存活手不足两只时本帧不驱动不判定，掌自然回归合围
            if (WorldFreezeSystem.IsActive || !context.Target.Alives()
                || context.Parts.AliveHandCount < 2) {
                return;
            }

            int sub = Timer - ClapStart;
            NPC core = context.Npc;

            //锁定帧：把合拢中心写入攻击锚点槽（客户端导线/各端冲线方向共用）
            if (sub == 0 || sub == ClapTrackLen) {
                context.Owner.ai[MLordAiSlots.OvAnchorX] = context.Target.Center.X;
                context.Owner.ai[MLordAiSlots.OvAnchorY] = context.Target.Center.Y;
                core.netUpdate = true;
            }
            Vector2 anchor = new(context.Owner.ai[MLordAiSlots.OvAnchorX], context.Owner.ai[MLordAiSlots.OvAnchorY]);

            for (int slot = 0; slot < MLordPartsStatus.HandSlots; slot++) {
                if (!context.Parts.HandAlive(slot)) {
                    continue;
                }
                NPC hand = Main.npc[context.Parts.HandIndex(slot)];

                if (sub < ClapTrackLen) {
                    //追踪：散至对角捕位（跟随活目标）；捕位钳进合法区，
                    //玩家拉远时手不追着玩家离体（技能不破坏手部限定范围）
                    Vector2 post = MLordLocomotion.ClampAttackPost(core, hand,
                        context.Target.Center + ClapPostDir(slot) * ClapPostRadius);
                    SpringHand(hand, post, 20f, 0.09f);
                }
                else if (sub < ClapTrackLen + ClapLockLen) {
                    //锁定停顿：捕位定格在锁点周围，蓄势微退（同样钳区）
                    Vector2 post = MLordLocomotion.ClampAttackPost(core, hand,
                        anchor + ClapPostDir(slot) * (ClapPostRadius + (sub - ClapTrackLen) * 4f));
                    SpringHand(hand, post, 14f, 0.1f);
                }
                else if (sub == ClapTrackLen + ClapLockLen) {
                    //一帧点火合拢
                    hand.velocity = (anchor - hand.Center).SafeNormalize(ClapPostDir(slot) * -1f) * ClapLungeSpeed;
                    hand.netUpdate = true;
                }
                else if (sub < ClapTrackLen + ClapLockLen + ClapLungeLen + ClapOvershootLen) {
                    //冲线与过冲：保持全速，判定见下；过伸即失速——
                    //抓捕半径受臂展约束，够不着的猎物在全伸展处扑空
                    if (MLordLocomotion.BeyondReach(core, hand)) {
                        hand.velocity *= 0.62f;
                    }
                }
                else {
                    //硬刹
                    hand.velocity *= 0.72f;
                }
            }

            //抓取判定：仅冲线窗内（与可见合拢动作精确对齐）
            int lungeSub = sub - ClapTrackLen - ClapLockLen;
            if (lungeSub >= 0 && lungeSub < ClapLungeLen) {
                TryCatchPlayer(context);
            }
        }

        /// <summary>冲线窗逐帧判定：任一存活掌命中任一活玩家即抓取</summary>
        private void TryCatchPlayer(MLordContext context) {
            for (int slot = 0; slot < MLordPartsStatus.HandSlots; slot++) {
                if (!context.Parts.HandAlive(slot)) {
                    continue;
                }
                NPC hand = Main.npc[context.Parts.HandIndex(slot)];
                Rectangle grasp = hand.Hitbox;
                grasp.Inflate(24, 24);

                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player player = Main.player[i];
                    if (!player.active || player.dead || player.ghost) {
                        continue;
                    }
                    if (!grasp.Intersects(player.Hitbox)) {
                        continue;
                    }
                    CommitCatch(context, hand, player);
                    return;
                }
            }
        }

        /// <summary>抓取成立：写入投技槽位并清掉本头的月咬之舌（处刑舞台清场）</summary>
        private void CommitCatch(MLordContext context, NPC hand, Player player) {
            caught = true;
            context.Owner.ai[MLordAiSlots.OvGrabTarget] = player.whoAmI + 1;
            context.Owner.ai[MLordAiSlots.OvGrabHand] = hand.whoAmI + 1;
            hand.velocity *= 0.2f;
            hand.netUpdate = true;
            context.Npc.netUpdate = true;

            if (context.Parts.Head >= 0) {
                int headIndex = context.Parts.Head;
                foreach (Projectile p in Main.ActiveProjectiles) {
                    if (p.type == ProjectileID.MoonLeech && (int)p.ai[0] == headIndex + 1) {
                        p.Kill();
                    }
                }
            }
        }

        /// <summary>抓捕拍各端表现：节拍音效与锁定读秒（位置声全客户端可闻）</summary>
        private void UpdateClapPresentation(MLordContext context) {
            if (VaultUtils.isServer || Timer < ClapStart || Timer >= ClapEnd) {
                return;
            }
            //本地眼里抓捕拍是否成立（与手部姿态征用同一判据）
            if (context.Parts.AliveHandCount < 2 || !context.Target.Alives()) {
                return;
            }
            int sub = Timer - ClapStart;
            NPC npc = context.Npc;
            if (sub == 0) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.9f, Pitch = -0.5f }, npc.Center);
            }
            else if (sub == ClapTrackLen) {
                //锁定读秒：高音短鸣
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.85f, Pitch = 0.55f }, npc.Center);
            }
            else if (sub == ClapTrackLen + ClapLockLen) {
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 1f, Pitch = -0.35f }, npc.Center);
                MLordScreenFX.Punch(npc.Center, 5f, 10);
            }
        }

        /// <summary>槽位对角方向（0上左/1上右/2下左/3下右）</summary>
        internal static Vector2 ClapPostDir(int slot) {
            float x = slot % 2 == 0 ? -0.7071f : 0.7071f;
            float y = slot < 2 ? -0.7071f : 0.7071f;
            return new Vector2(x, y);
        }

        /// <summary>服务端弹簧进给一只手（与编队弹簧同族，力度独立）</summary>
        private static void SpringHand(NPC hand, Vector2 goal, float maxSpeed, float gain) {
            Vector2 want = (goal - hand.Center) * gain;
            if (want.Length() > maxSpeed) {
                want = want.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            hand.velocity = Vector2.Lerp(hand.velocity, want, 0.18f);
        }

        #endregion

        #region 部件侧只读节拍查询（手部姿态/预警导线共用）

        /// <summary>处于抓捕拍窗口（含追踪/锁定/冲线/过冲/硬刹）</summary>
        internal static bool InClapWindow(int stateTimer) => stateTimer >= ClapStart && stateTimer < ClapEnd;

        /// <summary>追踪+锁定期（画合拢导线）</summary>
        internal static bool InClapTelegraph(int stateTimer) {
            int sub = stateTimer - ClapStart;
            return sub >= 0 && sub < ClapTrackLen + ClapLockLen;
        }

        /// <summary>锁定停顿期（导线定格满亮）</summary>
        internal static bool InClapLock(int stateTimer) {
            int sub = stateTimer - ClapStart;
            return sub >= ClapTrackLen && sub < ClapTrackLen + ClapLockLen;
        }

        /// <summary>冲线期（含过冲）</summary>
        internal static bool InClapLunge(int stateTimer) {
            int sub = stateTimer - ClapStart;
            return sub >= ClapTrackLen + ClapLockLen
                && sub < ClapTrackLen + ClapLockLen + ClapLungeLen + ClapOvershootLen;
        }

        #endregion

        /// <summary>为每个被月咬命中的玩家生成一枚凝滴，沿舌线回航（原版装配约定）</summary>
        private static void SpawnLeechBlobs(NPC head) {
            for (int projIndex = 0; projIndex < Main.maxProjectiles; projIndex++) {
                Projectile tongue = Main.projectile[projIndex];
                if (!tongue.active || tongue.type != ProjectileID.MoonLeech
                    || (int)tongue.ai[0] != head.whoAmI + 1) {
                    continue;
                }
                int playerIndex = (int)tongue.ai[1];
                if (playerIndex < 0 || playerIndex >= Main.maxPlayers) {
                    continue;
                }
                Player player = Main.player[playerIndex];
                if (!player.active || player.dead || player.FindBuffIndex(BuffID.MoonLeech) == -1) {
                    continue;
                }
                int blob = NPC.NewNPC(head.GetSource_FromAI(), (int)player.Center.X, (int)player.Center.Y,
                    NPCID.MoonLordLeechBlob);
                if (blob < Main.maxNPCs) {
                    Main.npc[blob].ai[0] = head.whoAmI + 1;
                    Main.npc[blob].ai[1] = projIndex;
                    Main.npc[blob].netUpdate = true;
                }
            }
        }
    }
}
