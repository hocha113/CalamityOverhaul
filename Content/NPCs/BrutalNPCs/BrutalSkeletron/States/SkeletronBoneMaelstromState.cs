using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States
{
    /// <summary>万骨临渊（低血大招）：定坛蓄势→八臂轮转斩→骨环迸射→双臂对冲终结</summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronStateIndex.BoneMaelstrom, typeof(SkeletronStateContext))]
    internal class SkeletronBoneMaelstromState : SkeletronStateBase
    {
        public override string StateName => "BoneMaelstrom";
        public override SkeletronStateIndex StateIndex => SkeletronStateIndex.BoneMaelstrom;

        internal const int ArmWaveA = 58;
        internal const int SpiralStart = 76;
        internal const int ArmWaveB = 300;
        internal const int SpiralEnd = 452;
        internal const int PincerFrame = 470;
        internal const int NovaFrame = 508;
        internal const int Duration = 560;

        private int spiralClock;

        public override void OnEnter(SkeletronStateContext context) {
            base.OnEnter(context);
            spiralClock = 0;
        }

        public override ISkeletronState OnUpdate(SkeletronStateContext context) {
            NPC npc = context.Npc;
            npc.damage = npc.defDamage;

            //定坛：缓慢跟随玩家上空，臂阵与螺旋随头同动
            Vector2 altar = context.Target.Center + new Vector2(0f, -250f);
            npc.velocity = (altar - npc.Center) * 0.018f;
            SettleRotation(npc, 0.1f);

            //领域压场（中档，保持弹幕可读）
            float domain = MathHelper.Clamp(Timer / 50f, 0f, 0.55f);
            if (Timer > NovaFrame) {
                domain = MathHelper.Lerp(0.55f, 0f, (Timer - NovaFrame) / (float)(Duration - NovaFrame));
            }
            SkeletronScreenEffects.RequestDomain(domain);

            //蓄势涡流（向心）
            if (Timer < ArmWaveA) {
                float t = Timer / (float)ArmWaveA;
                context.SpinVortex = t;
                context.VortexConverge = 1f;
                context.EyeFlame = 1f + t * 0.6f;
            }
            else {
                context.SpinVortex = MathHelper.Lerp(context.SpinVortex, 0.55f, 0.04f);
                context.VortexConverge = MathHelper.Lerp(context.VortexConverge, 0.4f, 0.04f);
            }

            UpdateBeats(context, npc);

            Timer++;
            if (Timer >= Duration && !VaultUtils.isClient) {
                return new SkeletronHubState();
            }
            return null;
        }

        private void UpdateBeats(SkeletronStateContext context, NPC npc) {
            //起手钟鸣与怒吼
            if (Timer == 4 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 1.1f, Pitch = -0.95f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.1f, Pitch = -0.6f }, npc.Center);
                SkeletronScreenEffects.PushShake(npc.Center, 7f);
            }

            //第一轮八臂布阵
            if (Timer == ArmWaveA && !VaultUtils.isClient) {
                SpawnSlamRing(context, npc, 66, 24);
            }
            //第二轮八臂（错位22.5度）
            if (Timer == ArmWaveB && !VaultUtils.isClient) {
                SpawnSlamRing(context, npc, 26, 20, MathHelper.Pi / 8f);
            }

            //双臂颅火螺旋（纯弹幕，无追踪）
            if (Timer >= SpiralStart && Timer < SpiralEnd && !VaultUtils.isClient) {
                int interval = context.AsuraMode ? 7 : 9;
                if ((Timer - SpiralStart) % interval == 0) {
                    int damage = SkullDamage(context);
                    float angle = spiralClock * 0.618f * MathHelper.TwoPi;
                    for (int k = 0; k < 2; k++) {
                        Vector2 vel = (angle + k * MathHelper.Pi).ToRotationVector2() * 3.1f;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                            ModContent.ProjectileType<SkeletronCursedSkull>(), damage, 0f, Main.myPlayer, 0f, 0f);
                    }
                    spiralClock++;
                    npc.netUpdate = true;
                }
            }

            //终结双臂对冲（钳住玩家当前位）
            if (Timer == PincerFrame && !VaultUtils.isClient) {
                int damage = SkullDamage(context);
                float ang = (context.Target.Center - npc.Center).ToRotation() + MathHelper.PiOver2;
                for (int k = 0; k < 2; k++) {
                    float a = ang + k * MathHelper.Pi;
                    Vector2 pos = context.Target.Center + a.ToRotationVector2() * SkeletronGhostArmProj.LungeRingRadius;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                        ModContent.ProjectileType<SkeletronGhostArmProj>(), damage, 0f, Main.myPlayer,
                        (float)SkeletronGhostArmProj.ArmMode.CircleLunge, a, 24f);
                }
                npc.netUpdate = true;
            }

            //崩渊终爆：留缺口的十六向骨环
            if (Timer == NovaFrame) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCDeath2 with { Volume = 1.1f, Pitch = -0.7f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Item35 with { Volume = 1f, Pitch = -0.5f }, npc.Center);
                    SkeletronScreenEffects.PushShockRing(npc.Center, 1.2f, 900f, 32);
                    SkeletronScreenEffects.PushShake(npc.Center, 11f);
                    for (int i = 0; i < 46; i++) {
                        PRTLoader.NewParticle<PRT_SkeleGhostFlame>(npc.Center + Main.rand.NextVector2Circular(60f, 60f),
                            Main.rand.NextVector2CircularEdge(9f, 9f),
                            SkeletronRenderHelper.GhostCyan, Main.rand.NextFloat(1.6f, 2.8f))?.Configure(Main.rand.Next(26, 46));
                    }
                }
                if (!VaultUtils.isClient) {
                    int damage = SkullDamage(context);
                    float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                    for (int i = 0; i < 16; i++) {
                        if (i == 4 || i == 9 || i == 14) {
                            continue;
                        }
                        Vector2 vel = (baseAngle + MathHelper.TwoPi * i / 16f).ToRotationVector2() * 5.2f;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                            ModContent.ProjectileType<SkeletronBoneShard>(), damage, 0f, Main.myPlayer, 0f, 0f);
                    }
                    npc.netUpdate = true;
                }
            }
        }

        /// <summary>绕头八臂轮转斩布阵</summary>
        private void SpawnSlamRing(SkeletronStateContext context, NPC npc, int firstDelay, int delayStep, float angleOffset = 0f) {
            int damage = SkullDamage(context);
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f + angleOffset;
                Vector2 pos = npc.Center + angle.ToRotationVector2() * SkeletronGhostArmProj.SlamRingRadius;
                //轮转次序：顺时针依次起斩
                int delay = firstDelay + i * delayStep;
                Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                    ModContent.ProjectileType<SkeletronGhostArmProj>(), damage, 0f, Main.myPlayer,
                    (float)SkeletronGhostArmProj.ArmMode.MaelstromSlam, angle, delay);
            }
            npc.netUpdate = true;
        }
    }
}
