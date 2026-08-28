using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.States
{
    /// <summary>
    /// 低血大招·心搏骤停：心跳骤停+黑幕压顶，闪现拍上真身与假体成组贯穿
    /// 每拍前有裂隙微光标记来向；终拍六向合围留一条缺口
    /// 大招结束心脏力竭：8秒受伤加深的惩罚窗口
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BrainStateIndex.HeartAttack, typeof(BrainStateContext))]
    internal class BrainHeartAttackState : BrainStateBase
    {
        public override string StateName => "HeartAttack";
        public override BrainStateIndex StateIndex => BrainStateIndex.HeartAttack;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int AnnounceTime = 50;
        /// <summary>闪现拍相对起始帧（加速逼近），最后一项为终拍</summary>
        private static readonly int[] FlashBeats = [50, 120, 182, 236, 282, 322];
        private const int WarnLead = 22;
        private const int FinalRestTime = 40;
        private const int FalterTime = 110;
        private const float FlashRadius = 470f;
        private const float DashSpeed = 29f;
        internal const int ShardDamage = 12;
        #endregion

        private int nextBeat;
        private int nextWarn;
        private float[] rolledAngles;
        private bool falterStarted;

        public BrainHeartAttackState() {
        }

        public override void OnEnter(BrainStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            npc.damage = 0;
            context.Invulnerable = true;
            nextBeat = 0;
            nextWarn = 0;
            falterStarted = false;
            rolledAngles = new float[FlashBeats.Length];

            if (!VaultUtils.isClient) {
                BrainProjectileUtils.ClearBrainProjectiles();
                //遁入高位黑暗
                BrainMotion.ServerTeleport(npc, context.Target.Center - Vector2.UnitY * 480f, Vector2.Zero);
            }
            //长音抽走：骤停前兆
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath10 with { Volume = 0.6f, Pitch = -0.85f }, npc.Center);
            }
        }

        public override IBrainState OnUpdate(BrainStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            //整场骤停：无自动心跳
            context.BeatSilenced = true;
            context.HideFromMinions = Timer < AnnounceTime + FlashBeats[^1] + FinalRestTime;

            //黑幕包络
            if (Timer < AnnounceTime) {
                context.BlackoutTarget = MathHelper.Lerp(0f, 0.78f, Timer / (float)AnnounceTime);
                context.GhostFade = Math.Min(context.GhostFade, MathHelper.Lerp(1f, 0.25f, Timer / (float)AnnounceTime));
                npc.damage = 0;
                context.Invulnerable = true;
                if (!VaultUtils.isClient) {
                    npc.velocity *= 0.9f;
                }
                return null;
            }

            int local = Timer - AnnounceTime;
            int lastBeatTick = FlashBeats[^1];

            //闪现拍段
            if (local <= lastBeatTick + FinalRestTime) {
                context.BlackoutTarget = 0.78f;

                //速度门控伤害（闪现冲刺时有判定）
                npc.damage = npc.velocity.Length() > 10f ? (int)(npc.defDamage * 1.2f) : 0;

                //预警：拍前22帧在来向布微光裂隙（服务端）
                if (!VaultUtils.isClient && nextWarn < FlashBeats.Length && local == FlashBeats[nextWarn] - WarnLead) {
                    rolledAngles[nextWarn] = Main.rand.NextFloat(MathHelper.TwoPi);
                    PlaceWarnRifts(context, nextWarn);
                    nextWarn++;
                }

                //闪现拍
                if (nextBeat < FlashBeats.Length && local == FlashBeats[nextBeat]) {
                    bool isFinal = nextBeat == FlashBeats.Length - 1;
                    if (!VaultUtils.isClient) {
                        DoFlashDash(context, nextBeat, isFinal);
                    }
                    BrainHeartbeat.Thump(isFinal ? 1.5f : 1.3f, 0.93f);
                    BrainHeartbeat.PlayThumpSound(player.Center, isFinal ? 1.1f : 0.95f, 0.1f * nextBeat);
                    BrainMotion.Shake(player.Center, isFinal ? 9f : 5f, 12);
                    nextBeat++;
                }

                //拍后滑行减速
                if (!VaultUtils.isClient && npc.velocity.Length() > 4f) {
                    npc.velocity *= 0.965f;
                }
                return null;
            }

            //力竭窗口：黑幕撤去，心脏踉跄重启，受伤加深
            if (!falterStarted) {
                falterStarted = true;
                context.FalterTimer = 480;
                BrainHeartbeat.Thump(0.8f, 0.88f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCDeath10 with { Volume = 0.5f, Pitch = 0.2f }, npc.Center);
                }
                if (!VaultUtils.isClient) {
                    KillWarnRifts();
                }
            }

            int falterLocal = local - lastBeatTick - FinalRestTime;
            context.BlackoutTarget = MathHelper.Lerp(0.78f, 0f, MathHelper.Clamp(falterLocal / 30f, 0f, 1f));
            context.BeatSilenced = false;
            context.BeatPeriod = 64;    //踉跄的慢拍
            context.BeatIntensity = 0.5f;
            context.TelegraphGlow = 0.7f * (1f - falterLocal / (float)FalterTime);
            npc.damage = 0;

            if (!VaultUtils.isClient) {
                //力竭漂浮，无力追击
                Vector2 drift = player.Center + new Vector2(Math.Sign(npc.Center.X - player.Center.X) * 360f, -120f);
                BrainMotion.SpringHover(npc, drift, 0.006f, 0.07f, 6f);
            }

            //渗血喘息
            if (!VaultUtils.isServer && falterLocal % 6 == 0 && BrainMotion.OnScreen(npc.Center)) {
                BrainMotion.BloodMistBurst(npc.Center + Main.rand.NextVector2Circular(50f, 40f), 0.4f, 1, 2f);
            }

            if (falterLocal >= FalterTime && !VaultUtils.isClient) {
                return new BrainHoverState();
            }
            return null;
        }

        /// <summary>预警裂隙：三个（终拍五个）来向微光</summary>
        private void PlaceWarnRifts(BrainStateContext context, int beatIndex) {
            NPC npc = context.Npc;
            Player player = context.Target;
            bool isFinal = beatIndex == FlashBeats.Length - 1;
            int dashers = isFinal ? 6 : 3;
            float baseAngle = rolledAngles[beatIndex];

            //终拍留缺口：跳过槽0（缺口方向=baseAngle）
            for (int i = isFinal ? 1 : 0; i < dashers; i++) {
                float angle = baseAngle + MathHelper.TwoPi * i / dashers;
                Vector2 pos = player.Center + angle.ToRotationVector2() * FlashRadius;
                Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                    ModContent.ProjectileType<BrainTeleportRift>(), 0, 0f, Main.myPlayer, 0f);
            }
        }

        /// <summary>闪现拍：真身+假体成组自预警位向心贯穿</summary>
        private void DoFlashDash(BrainStateContext context, int beatIndex, bool isFinal) {
            NPC npc = context.Npc;
            Player player = context.Target;
            float baseAngle = rolledAngles[beatIndex];
            int dashers = isFinal ? 6 : 3;
            int fakeDamage = BrainMirrorStrikeState.MirrorContactDamage + (context.IsAsuraMode ? 3 : 0);

            KillWarnRifts();

            //真身占槽1（终拍缺口在槽0方向）
            int brainSlot = isFinal ? 1 : 0;
            for (int i = isFinal ? 1 : 0; i < dashers; i++) {
                float angle = baseAngle + MathHelper.TwoPi * i / dashers;
                Vector2 pos = player.Center + angle.ToRotationVector2() * FlashRadius;
                Vector2 vel = (player.Center - pos).SafeNormalize(Vector2.UnitY) * DashSpeed;
                if (i == brainSlot) {
                    BrainMotion.ServerTeleport(npc, pos, vel);
                }
                else {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), pos, vel,
                        ModContent.ProjectileType<BrainMirrorImage>(), fakeDamage, 0f, Main.myPlayer,
                        BrainMirrorImage.PackMode(BrainMirrorImage.ModeGuidedDash, i), pos.X, pos.Y);
                }
            }

            //终拍中心散珠
            if (isFinal) {
                int damage = ShardDamage + (context.IsAsuraMode ? 3 : 0);
                for (int i = 0; i < 8; i++) {
                    float angle = baseAngle + MathHelper.TwoPi * i / 8f + 0.2f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), player.Center + angle.ToRotationVector2() * 30f,
                        angle.ToRotationVector2() * 6.8f,
                        ModContent.ProjectileType<BrainBloodShard>(), damage, 0f, Main.myPlayer, 0f);
                }
            }
        }

        private static void KillWarnRifts() {
            int riftType = ModContent.ProjectileType<BrainTeleportRift>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == riftType) {
                    proj.Kill();
                }
            }
        }

        public override void OnExit(BrainStateContext context) {
            base.OnExit(context);
            NPC npc = context.Npc;
            npc.damage = npc.defDamage;
            if (!VaultUtils.isClient) {
                KillWarnRifts();
            }
        }
    }
}
