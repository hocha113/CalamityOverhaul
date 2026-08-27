using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.States
{
    /// <summary>
    /// 鲨群空袭：升上高空点航道，鲨鱼龙沿斜线俯冲轰炸，
    /// 公爵亲自压轴俯冲收尾（依旧是直线+水迹的家规）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FishronStateIndex.SharkronStrafe, typeof(FishronStateContext))]
    internal class FishronSharkronStrafeState : FishronStateBase
    {
        public override string StateName => "SharkronStrafe";
        public override FishronStateIndex StateIndex => FishronStateIndex.SharkronStrafe;
        public override bool AllowFarSnap => false;

        private const int AscendEnd = 40;
        private const int LaneTelegraphTime = 52;
        private const int SelfDiveStart = 150;
        private const int TotalTime = 192;
        private const float SharkronSpeed = 24f;

        /// <summary>鲨群俯冲起帧：末相航道预警 -30%（48→34 帧）</summary>
        private static int LaneDiveStart(FishronStateContext ctx) => ctx.Phase >= 3 ? 78 : 92;
        /// <summary>压轴预告起帧：末相 -30%（22→15 帧）</summary>
        private static int SelfAimStart(FishronStateContext ctx) => ctx.Phase >= 3 ? 135 : 128;

        //服务端专用航道表
        private Vector2[] laneOrigins;
        private Vector2[] laneDirs;
        private bool selfDiveLaunched;
        //压轴俯冲方向，预告锁定帧冻结
        private Vector2 selfDiveDir;

        public FishronSharkronStrafeState() {
        }

        private static int LaneCount(FishronStateContext ctx) => ctx.Phase >= 2 ? 5 : 3;

        public override void OnEnter(FishronStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            laneOrigins = null;
            laneDirs = null;
            selfDiveLaunched = false;
            selfDiveDir = Vector2.Zero;
        }

        public override IFishronState OnUpdate(FishronStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            //幕一：跃升到玩家上空（屏顶边缘的剪影压场，不失联）
            if (Timer <= AscendEnd) {
                Vector2 goal = player.Center + new Vector2(0, -620f);
                Vector2 desired = (goal - npc.Center).SafeNormalize(-Vector2.UnitY)
                    * MathHelper.Lerp(10f, 26f, Timer / (float)AscendEnd);
                npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.15f);
                AimBodyAlongVelocity(npc);
                context.FrameCommand = 2;

                if (Timer == AscendEnd) {
                    SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 1f, Pitch = 0.2f, MaxInstances = 3 }, npc.Center);
                }
                return null;
            }

            //高空缓横移压场；压轴预告亮起后停横移（锁线不平移），
            //起冲后整段悬停控制让位俯冲段，免得阻尼把冲刺拖死
            if (!selfDiveLaunched) {
                npc.velocity *= 0.9f;
                if (Timer < SelfAimStart(context)) {
                    npc.position.X += MathHelper.Clamp(player.Center.X - npc.Center.X, -3.2f, 3.2f);
                }
                FaceBody(npc, player.Center, 0.08f);
            }

            //幕二：点亮航道（服务端定几何，预告线弹幕随生成同步）
            if (Timer == AscendEnd + 4 && !VaultUtils.isClient) {
                BuildLanes(context);
                for (int i = 0; i < laneOrigins.Length; i++) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), laneOrigins[i], laneDirs[i],
                        ModContent.ProjectileType<FishronTelegraph>(), 0, 0f, Main.myPlayer,
                        -1, -1, FishronTelegraph.PackParams(2, LaneTelegraphTime + (int)(LaneDiveStart(context) - AscendEnd - 4)));
                }
            }

            //幕三：鲨群沿航道俯冲（每道两条错帧；末相翻倍成四条，出膛速度也翻倍）
            if (!VaultUtils.isClient && laneOrigins != null && Timer >= LaneDiveStart(context) && Timer < SelfAimStart(context)) {
                int t = (int)Timer - LaneDiveStart(context);
                bool lastStand = context.Phase >= 3;
                float sharkSpeed = lastStand ? SharkronSpeed * 2f : SharkronSpeed;
                for (int i = 0; i < laneOrigins.Length; i++) {
                    int rel = t - i * 6;
                    bool fire = lastStand ? rel is 0 or 9 or 18 or 27 : rel is 0 or 18;
                    if (fire) {
                        TryLaunchSharkron(npc, laneOrigins[i], laneDirs[i], sharkSpeed);
                    }
                }
            }

            //幕四：公爵压轴俯冲，先亮短预告再走直线
            if (Timer == SelfAimStart(context) && !VaultUtils.isClient) {
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center,
                    (player.Center - npc.Center).SafeNormalize(Vector2.UnitY),
                    ModContent.ProjectileType<FishronTelegraph>(), 0, 0f, Main.myPlayer,
                    npc.whoAmI, player.whoAmI, FishronTelegraph.PackParams(0, SelfDiveStart - SelfAimStart(context)));
            }
            if (Timer >= SelfAimStart(context) && Timer < SelfDiveStart) {
                context.SetChargeState(1, (Timer - SelfAimStart(context)) / (float)(SelfDiveStart - SelfAimStart(context)));
                //预告锁定帧同步冻结俯冲方向
                if (Timer < SelfDiveStart - FishronTelegraph.LockTime || selfDiveDir == Vector2.Zero) {
                    selfDiveDir = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                }
                context.DashDirection = selfDiveDir;
                context.FrameCommand = 1;
            }
            if (Timer == SelfDiveStart) {
                selfDiveLaunched = true;
                Vector2 dir = selfDiveDir == Vector2.Zero
                    ? (player.Center - npc.Center).SafeNormalize(Vector2.UnitY) : selfDiveDir;
                //末相压轴俯冲 +15%：46→53
                npc.velocity = dir * (context.Phase >= 3 ? 53f : 46f);
                npc.netUpdate = true;
                FishronMotionFX.SpawnDashBurst(npc.Center, dir, 1.1f);
                SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 1f, Pitch = 0.3f, MaxInstances = 3 }, npc.Center);
            }
            if (selfDiveLaunched) {
                AimBodyAlongVelocity(npc);
                context.FrameCommand = 2;
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    FishronMotionFX.SpawnSprayCone(npc.Center, -npc.velocity.SafeNormalize(Vector2.UnitY),
                        1, 3f, 8f, 0.5f, 0.9f);
                }
                bool passed = npc.Distance(player.Center) > 620f
                    && Vector2.Dot(npc.velocity.SafeNormalize(Vector2.Zero),
                        (player.Center - npc.Center).SafeNormalize(Vector2.Zero)) < -0.2f;
                if (passed && Timer < TotalTime - 10) {
                    Timer = TotalTime - 10;
                }
                //收尾轻刹
                if (Timer > TotalTime - 10) {
                    npc.velocity *= 0.9f;
                }
            }

            if (Timer >= TotalTime) {
                return new FishronHoverState();
            }
            return null;
        }

        /// <summary>航道几何：从公爵两翼撒开，压向玩家两侧包夹点</summary>
        private void BuildLanes(FishronStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int count = LaneCount(context);
            laneOrigins = new Vector2[count];
            laneDirs = new Vector2[count];
            int mid = count / 2;
            for (int i = 0; i < count; i++) {
                laneOrigins[i] = npc.Center + new Vector2((i - mid) * 130f, -30f);
                //中央道预判玩家，其余道左右包夹
                Vector2 aim = player.Center + new Vector2((i - mid) * 200f, 0f);
                if (i == mid) {
                    aim = player.Center + player.velocity * 22f;
                }
                laneDirs[i] = (aim - laneOrigins[i]).SafeNormalize(Vector2.UnitY);
            }
        }

        /// <summary>全场鲨鱼龙容量：撒鲨的招式频繁，靠这个顶不至于淹屏</summary>
        internal const int SharkronCap = 9;

        /// <summary>全场鲨鱼龙计数</summary>
        internal static int CountSharkrons() {
            int count = 0;
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == NPCID.Sharkron || n.type == NPCID.Sharkron2) {
                    count++;
                }
            }
            return count;
        }

        /// <summary>甩出一条鲨鱼龙（受全场容量约束，服务端调用），撒鲨招式共用；末相容量顶随数量翻倍</summary>
        internal static void TryLaunchSharkron(NPC npc, Vector2 origin, Vector2 dir, float speed) {
            int cap = npc.TryGetOverride(out DukeFishronAI ov) && ov.PhaseThreeLive
                ? SharkronCap * 2 : SharkronCap;
            if (CountSharkrons() >= cap) {
                return;
            }
            int idx = NPC.NewNPC(npc.GetSource_FromAI(), (int)origin.X, (int)origin.Y, NPCID.Sharkron);
            if (idx < 0 || idx >= Main.maxNPCs) {
                return;
            }
            NPC shark = Main.npc[idx];
            shark.ai[0] = 1f;
            shark.ai[1] = 1f;
            shark.velocity = dir * speed;
            shark.rotation = shark.velocity.ToRotation();
            shark.direction = Math.Sign(dir.X) >= 0 ? 1 : -1;
            shark.spriteDirection = shark.direction;
            shark.netUpdate = true;
        }

        public override void OnExit(FishronStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
        }
    }
}
