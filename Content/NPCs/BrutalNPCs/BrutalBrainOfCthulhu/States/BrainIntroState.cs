using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.States
{
    /// <summary>入场演出：低语预兆→镜像残片旋聚→实体化重拍→凝视静场</summary>
    [InnoVault.StateMachines.VaultState((int)BrainStateIndex.Intro, typeof(BrainStateContext))]
    internal class BrainIntroState : BrainStateBase
    {
        public override string StateName => "Intro";
        public override BrainStateIndex StateIndex => BrainStateIndex.Intro;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int OmenEnd = 42;          //黑暗预兆
        private const int ConvergeEnd = 150;     //镜像残片旋聚+飞眼集结
        private const int MaterializeEnd = 188;  //实体化
        private const int StillEnd = 244;        //凝视静场
        /// <summary>飞眼目标数量</summary>
        internal const int CreeperCount = 10;
        #endregion

        private bool creepersSpawned;
        private bool shardsSpawned;

        public BrainIntroState() {
        }

        public override void OnEnter(BrainStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            context.Invulnerable = true;
            npc.damage = 0;
            context.GhostFade = 0f;
            creepersSpawned = false;
            shardsSpawned = false;
        }

        public override IBrainState OnUpdate(BrainStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            npc.damage = 0;
            context.Invulnerable = Timer < MaterializeEnd;
            npc.velocity *= 0.9f;

            //心跳从远处逼近：周期 88→54，音量渐强
            float approach = MathHelper.Clamp(Timer / (float)MaterializeEnd, 0f, 1f);
            context.BeatPeriod = (int)MathHelper.Lerp(88f, 54f, approach);
            context.BeatIntensity = MathHelper.Lerp(0.2f, 0.5f, approach);

            //幕一：黑暗预兆
            if (Timer <= OmenEnd) {
                context.GhostFade = 0f;
                //即刻收编场上已有飞眼（灾厄 OnSpawn 可能先塞了一批），防其在错误模式下乱跑
                //并把脑定位到玩家上方侧位待命（此时 FindTarget 已保证 Target 有效）
                if (Timer == 1 && !VaultUtils.isClient) {
                    AdoptExistingCreepers();
                    if (player.Alives()) {
                        npc.Center = player.Center + new Vector2(player.direction * 300f, -320f);
                        npc.velocity = Vector2.Zero;
                        npc.netUpdate = true;
                    }
                }
                //聚拢的血雾预兆
                if (!VaultUtils.isServer && Timer % 5 == 0) {
                    Vector2 pos = npc.Center + Main.rand.NextVector2CircularEdge(360f, 360f);
                    BrainMotion.BloodMistBurst(pos, 0.5f, 1, 2f);
                }
                return null;
            }

            //幕二：镜像残片旋聚+飞眼自屏外集结
            if (Timer <= ConvergeEnd) {
                context.GhostFade = MathHelper.Clamp((Timer - OmenEnd) / (float)(ConvergeEnd - OmenEnd), 0f, 0.35f);

                if (!shardsSpawned) {
                    shardsSpawned = true;
                    if (!VaultUtils.isClient) {
                        //三片镜像残影自远处螺旋汇入
                        for (int i = 0; i < 3; i++) {
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                                ModContent.ProjectileType<BrainMirrorImage>(), 0, 0f, Main.myPlayer,
                                BrainMirrorImage.PackMode(BrainMirrorImage.ModeIntroConverge, i),
                                npc.Center.X, npc.Center.Y);
                        }
                    }
                    SoundEngine.PlaySound(SoundID.Zombie102 with { Volume = 0.5f, Pitch = -0.75f, MaxInstances = 2 }, npc.Center);
                }

                if (!creepersSpawned && Timer > OmenEnd + 20) {
                    creepersSpawned = true;
                    if (!VaultUtils.isClient) {
                        SpawnOrAdoptCreepers(context);
                    }
                }

                //血雾持续向体心收束
                if (!VaultUtils.isServer && Timer % 3 == 0) {
                    Vector2 pos = npc.Center + Main.rand.NextVector2CircularEdge(220f, 220f) * (1f - context.GhostFade);
                    Vector2 vel = (npc.Center - pos) * 0.06f;
                    BrainMotion.BloodMistBurst(pos + vel, 0.4f, 0, 0f);
                    if (Timer % 9 == 0) {
                        Lighting.AddLight(pos, BrainMotion.BloodDark.ToVector3() * 0.5f);
                    }
                }
                return null;
            }

            //幕三：实体化，最后20帧血雾骤停（爆前收势）
            if (Timer <= MaterializeEnd) {
                float t = (Timer - ConvergeEnd) / (float)(MaterializeEnd - ConvergeEnd);
                context.GhostFade = MathHelper.Lerp(0.35f, 1f, BrainMotion.SharpOut(t, 5));

                if (Timer == MaterializeEnd - 1) {
                    //实体化重拍：咆哮+重心跳+短震
                    BrainMotion.Roar(npc.Center, 1.1f, -0.35f);
                    BrainHeartbeat.Thump(1.25f, 0.93f);
                    BrainHeartbeat.PlayThumpSound(npc.Center, 1f);
                    BrainMotion.Shake(npc.Center, 7f, 18);
                    BrainMotion.BloodMistBurst(npc.Center, 1.6f, 14, 9f);
                }
                return null;
            }

            //幕四：凝视静场，威压来自静止
            if (Timer <= StillEnd) {
                context.GhostFade = 1f;
                //极缓慢地面向玩家漂移
                Vector2 drift = (player.Center - npc.Center).SafeNormalize(Vector2.Zero) * 0.5f;
                npc.velocity = Vector2.Lerp(npc.velocity, drift, 0.05f);
                return null;
            }

            return new BrainHoverState();
        }

        /// <summary>即刻收编：重排既有飞眼的指挥槽并裁掉超编（服务端）</summary>
        private static int AdoptExistingCreepers() {
            int count = 0;
            foreach (var n in Main.ActiveNPCs) {
                if (n.type != NPCID.Creeper) {
                    continue;
                }
                if (count >= CreeperCount) {
                    //超编静默移除
                    n.active = false;
                    n.netUpdate = true;
                    continue;
                }
                BrainCreeperAI.CommandIdle(n, count);
                count++;
            }
            return count;
        }

        /// <summary>收编场上已有飞眼并补齐编制（服务端）</summary>
        private static void SpawnOrAdoptCreepers(BrainStateContext context) {
            NPC npc = context.Npc;
            int count = AdoptExistingCreepers();

            int need = CreeperCount + (context.IsAsuraMode ? 2 : 0);
            for (int i = count; i < need; i++) {
                //自脑周围环形远点入场
                float angle = MathHelper.TwoPi * i / need;
                Vector2 pos = npc.Center + angle.ToRotationVector2() * 780f;
                int idx = NPC.NewNPC(npc.GetSource_FromAI(), (int)pos.X, (int)pos.Y, NPCID.Creeper);
                if (idx >= 0 && idx < Main.maxNPCs) {
                    BrainCreeperAI.CommandIdle(Main.npc[idx], i);
                    Main.npc[idx].netUpdate = true;
                }
            }
            context.RefreshCreepers();
        }

        public override void OnExit(BrainStateContext context) {
            base.OnExit(context);
            NPC npc = context.Npc;
            npc.damage = npc.defDamage;
            context.GhostFade = 1f;
        }
    }
}
