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
    /// 潮汐掌击（四臂对角版）：每拍两掌自对角翼位同时闪现（左上+右下 / 右上+左下轮换）
    /// →反向蓄势→交叉贯穿突进（冲线在玩家处交叉，安全区为垂直走廊）→硬刹→睁眼硬直。
    /// 每掌预告线独立；接触伤害由部件 AI 按速度门控（各端确定性），无手后由真眼执行冲撞版
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.TidalPalms, typeof(MLordContext))]
    internal class MLordTidalPalmsState : MLordStateBase
    {
        public override string StateName => "TidalPalms";
        public override MLordStateIndex StateIndex => MLordStateIndex.TidalPalms;

        //子相位帧段
        internal const int BlinkLen = 12;
        internal const int WindupLen = 40;
        internal const int DashLen = 10;
        internal const int SkidLen = 16;
        internal const int RecoverLen = 22;
        internal const int CycleLen = BlinkLen + WindupLen + DashLen + SkidLen + RecoverLen;
        internal const int SlamCount = 3;
        internal const int PunishTail = 46;
        internal const float DashSpeed = 47f;
        /// <summary>每拍最多同时出手的掌数（对角双掌）</summary>
        internal const int MaxPerformers = 2;

        /// <summary>本拍各执行者锁定的冲线方向（服务端，按执行序号分槽）</summary>
        private readonly Vector2[] lockedDashDirs = new Vector2[MaxPerformers];

        public override void OnEnter(MLordContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isClient) {
                context.Owner.ai[MLordAiSlots.OvAttackSeed] = Main.rand.Next(1, 100000);
                context.Npc.netUpdate = true;
            }
        }

        public override IMLordState OnUpdate(MLordContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;

            //核心退到后景高位旁观（拉开镜头层次）
            HoverTo(npc, target.Center + new Vector2(0f, -300f), 6f, 0.04f);
            UpdateLean(context);

            int totalLen = SlamCount * CycleLen + PunishTail;
            int slamIndex = Timer / CycleLen;
            int sub = Timer % CycleLen;

            if (slamIndex < SlamCount) {
                Span<int> performers = stackalloc int[MaxPerformers];
                int performerCount = ResolvePerformers(context, slamIndex, performers);
                if (!VaultUtils.isClient) {
                    for (int i = 0; i < performerCount; i++) {
                        DriveSlamServer(context, Main.npc[performers[i]], i, slamIndex, sub);
                    }
                }
                //头部掩护弹：蓄势中拍点一发直射
                if (sub == BlinkLen + WindupLen / 2 && !VaultUtils.isClient) {
                    SpawnCoverBolt(context);
                }
            }

            Timer++;
            if (Timer >= totalLen) {
                return NextAttack(context);
            }
            return null;
        }

        /// <summary>
        /// 本拍执行者集合（各端确定性一致）：对角双掌，拍偶取{上左,下右}、拍奇取{上右,下左}，
        /// 对角缺位以就近存活手补位（不重复征用）；全无手时由真眼冲撞（单执行者）。
        /// 返回执行者数，索引写入 performers
        /// </summary>
        internal static int ResolvePerformers(MLordContext context, int slamIndex, Span<int> performers) {
            MLordPartsStatus parts = context.Parts;
            int count = 0;

            Span<int> wantSlots = stackalloc int[2];
            if (slamIndex % 2 == 0) {
                wantSlots[0] = 0;
                wantSlots[1] = 3;
            }
            else {
                wantSlots[0] = 1;
                wantSlots[1] = 2;
            }
            foreach (int slot in wantSlots) {
                int pick = parts.HandAlive(slot) ? parts.HandIndex(slot) : parts.FirstAliveHand(slot);
                if (pick >= 0 && !AlreadyPicked(performers, count, pick)) {
                    performers[count++] = pick;
                }
            }
            if (count > 0) {
                return count;
            }

            //真眼冲撞版
            int[] eyes = new int[MLordFacts.MaxFreeEyes];
            int eyeCount = MLordFacts.ScanFreeEyes(context.Npc, eyes);
            if (eyeCount > 0) {
                performers[0] = eyes[slamIndex % eyeCount];
                return 1;
            }
            return 0;
        }

        private static bool AlreadyPicked(Span<int> performers, int count, int value) {
            for (int i = 0; i < count; i++) {
                if (performers[i] == value) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>服务端驱动一名执行者的掌击拍：执行序 0 走高翼位、1 走对侧低翼位（对角交叉）</summary>
        private void DriveSlamServer(MLordContext context, NPC performer, int ordinal, int slamIndex, int sub) {
            Player target = context.Target;
            int seed = (int)context.Owner.ai[MLordAiSlots.OvAttackSeed];
            float side = MLordConstellationProj.Hash01(seed, slamIndex) > 0.5f ? 1f : -1f;
            if (ordinal == 1) {
                side = -side;
            }
            //高翼位与低翼位：两掌冲线在玩家处交叉，安全区为与连线垂直的走廊
            float flankY = ordinal == 0 ? -190f : 130f;

            if (sub == 0) {
                //真眼执行者瞬移前掐断其身上的链束：活束随瞬移横甩全屏是不可读判定
                if (performer.type == NPCID.MoonLordFreeEye) {
                    KillLinksTouching(performer.whoAmI);
                }
                //闪现落位：对角翼位
                Vector2 blinkPos = target.Center + new Vector2(side * 560f,
                    flankY + MLordConstellationProj.Hash01(seed, slamIndex + 40 + ordinal * 13) * 120f - 60f);
                performer.Center = blinkPos;
                performer.velocity = Vector2.Zero;
                performer.netUpdate = true;
                if (!VaultUtils.isServer) {
                    MLordScreenFX.StarBurst(blinkPos, 0.8f, 10);
                }
            }
            else if (sub < BlinkLen + WindupLen) {
                //反向蓄势：pow(t,6) 后仰，末端猛然回吸
                float t = (sub - BlinkLen) / (float)WindupLen;
                Vector2 away = (performer.Center - target.Center).SafeNormalize(Vector2.UnitX * side);
                performer.velocity = away * MathF.Pow(t, 6f) * 20f;
                //锁定冲线方向（蓄势后半段收敛，前半段跟踪）
                if (t < 0.55f) {
                    lockedDashDirs[ordinal] = (target.Center + target.velocity * 8f - performer.Center).SafeNormalize(Vector2.UnitX * -side);
                }
                if (sub == BlinkLen + WindupLen - 8 && ordinal == 0 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.8f, Pitch = -0.4f, MaxInstances = 4 }, performer.Center);
                }
            }
            else if (sub == BlinkLen + WindupLen) {
                //一帧点火全速
                performer.velocity = lockedDashDirs[ordinal] * DashSpeed;
                performer.netUpdate = true;
                if (!VaultUtils.isServer) {
                    if (ordinal == 0) {
                        SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 1f, Pitch = -0.2f }, performer.Center);
                    }
                    MLordScreenFX.Punch(performer.Center, 6f, 10, lockedDashDirs[ordinal]);
                }
            }
            else if (sub < BlinkLen + WindupLen + DashLen) {
                //冲线复合加速
                performer.velocity *= 1.02f;
            }
            else if (sub < BlinkLen + WindupLen + DashLen + SkidLen) {
                //硬刹
                performer.velocity *= 0.76f;
            }
            else {
                //硬直悬停
                performer.velocity *= 0.85f;
            }
        }

        /// <summary>灭掉与该真眼相连的链束（服务端调用，Kill 自带跨端广播）</summary>
        private static void KillLinksTouching(int eyeWhoAmI) {
            int linkType = ModContent.ProjectileType<MLordEyeLinkProj>();
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type == linkType
                    && ((int)p.ai[0] == eyeWhoAmI || (int)p.ai[1] == eyeWhoAmI)) {
                    p.Kill();
                }
            }
        }

        /// <summary>头部（或核心）掩护直射弹</summary>
        private void SpawnCoverBolt(MLordContext context) {
            NPC origin = context.Parts.Head >= 0 && context.Parts.HeadAlive
                ? Main.npc[context.Parts.Head] : context.Npc;
            Vector2 aim = (context.Target.Center - origin.Center).SafeNormalize(Vector2.UnitY);
            Projectile.NewProjectile(origin.GetSource_FromAI(), origin.Center + aim * 40f, aim * 7.5f,
                ProjectileID.PhantasmalBolt, ScaleDamage(context, MLordDirector.BoltDamage), 0f, Main.myPlayer);
        }

        #region 部件侧只读节拍查询（部件 AI 姿态/预警共用）

        /// <summary>当前拍执行者索引与子相位；不在掌击态返回 false</summary>
        internal static bool TryGetBeat(MLordContext context, int stateTimer, out int slamIndex, out int sub) {
            slamIndex = stateTimer / CycleLen;
            sub = stateTimer % CycleLen;
            return slamIndex < SlamCount;
        }

        /// <summary>蓄势期（部件绘制预警线用）</summary>
        internal static bool InWindup(int sub) => sub >= BlinkLen && sub < BlinkLen + WindupLen;
        /// <summary>冲线期</summary>
        internal static bool InDash(int sub) => sub >= BlinkLen + WindupLen && sub < BlinkLen + WindupLen + DashLen + SkidLen / 2;
        /// <summary>硬直期（眼睁开）</summary>
        internal static bool InRecover(int sub) => sub >= BlinkLen + WindupLen + DashLen + SkidLen;

        #endregion
    }
}
