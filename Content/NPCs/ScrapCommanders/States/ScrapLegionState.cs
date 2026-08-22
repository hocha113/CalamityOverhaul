using CalamityOverhaul.Content.NPCs.ScrapCommanders.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.States
{
    /// <summary>
    /// 拼装军团：吊臂探向地面，把仆从从废钢堆/地里拉起来。
    /// 优先用场上废钢堆的位置出兵，迫击留下的堆是它的兵营
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)ScrapStateIndex.Legion, typeof(ScrapStateContext))]
    internal class ScrapLegionState : ScrapStateBase
    {
        public override string StateName => "Legion";
        public override ScrapStateIndex StateIndex => ScrapStateIndex.Legion;

        private const int PoseFrames = 20;
        private static readonly int[] SpawnBeats = { 20, 38, 56 };
        private const int StateEnd = 78;

        /// <summary>已出兵的最高拍号（单调闩）</summary>
        private int lastSpawned = -1;
        /// <summary>吊装余帧：出兵后吊臂真探向新兵</summary>
        private int dipFrames;
        private int dipArm = ScrapCommander.ArmSaw;

        public override IScrapState OnUpdate(ScrapStateContext ctx) {
            NPC npc = ctx.Npc;
            ScrapCommander owner = ctx.Owner;
            int t = (int)Timer;

            npc.velocity *= 0.93f;
            LeanByVelocity(npc, 0.08f);
            ctx.MagnetGlow = MathHelper.Clamp(t / (float)PoseFrames, 0f, 1f);

            //锯/钳双臂探地（吊装姿态）
            Vector2 head = npc.Center + npc.velocity;
            ctx.Arms[ScrapCommander.ArmSaw] = ArmDirective.HoldAt(head + new Vector2(-92f, 172f), 0.16f, 0.8f);
            ctx.Arms[ScrapCommander.ArmVice] = ArmDirective.HoldAt(head + new Vector2(92f, 172f), 0.16f, 0.8f);

            //吊装：出兵后吊臂真探向正在出土的新兵（钩住再拉起的读法）
            if (dipFrames > 0) {
                dipFrames--;
                NPC young = FindRisingProbe(npc);
                if (young != null) {
                    ctx.Arms[dipArm] = new ArmDirective {
                        Mode = ArmMode.Hold,
                        Target = young.Center + new Vector2(0f, -44f),
                        Spring = 0.22f,
                        Damping = 0.76f,
                        UseRot = true,
                        WantRot = (young.Center - owner.GetArmPos(dipArm)).ToRotation() - MathHelper.PiOver2,
                        RotRate = 0.3f,
                    };
                    //吊线：臂到新兵的一根短实线
                    Vector2 from = owner.GetArmPos(dipArm);
                    Vector2 dir = (young.Center - from).SafeNormalize(Vector2.UnitY);
                    ctx.AddSolidBeam(from, dir, Vector2.Distance(from, young.Center), 0.5f, 0.5f);
                }
            }

            if (t == 2) {
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 0.45f, Pitch = -0.1f, MaxInstances = 1 }, npc.Center);
                SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.4f, Pitch = -0.4f, MaxInstances = 2 }, npc.Center);
            }

            //出兵拍：兵位上限 3，超编不再拉
            int maxProbes = ctx.MasterMode || ctx.Phase >= 3 ? 3 : 2;
            for (int i = 0; i < SpawnBeats.Length; i++) {
                if (t == SpawnBeats[i] && lastSpawned < i) {
                    lastSpawned = i;
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = -0.2f + i * 0.12f, MaxInstances = 3 }, npc.Center);
                    //拉兵的吊臂顿一下并转入吊装
                    dipArm = i % 2 == 0 ? ScrapCommander.ArmSaw : ScrapCommander.ArmVice;
                    dipFrames = 18;
                    owner.ImpulseArm(dipArm, new Vector2(0f, -4f));
                    if (!VaultUtils.isClient && ScrapLegionProbe.CountFor(npc) < maxProbes) {
                        SpawnProbe(npc, i);
                    }
                }
            }

            Timer++;
            if (t >= StateEnd) {
                return EndAttack(ctx, 55);
            }
            return null;
        }

        /// <summary>找一台还在出土上升段的己方仆从（吊装演出的钩点）</summary>
        private static NPC FindRisingProbe(NPC boss) {
            int type = ModContent.NPCType<ScrapLegionProbe>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == type && (int)npc.ai[0] == boss.whoAmI
                    && npc.velocity.Y < -1f) {
                    return npc;
                }
            }
            return null;
        }

        /// <summary>出兵（服务端）：优先从场上废钢堆里拉，没堆就从统帅脚下的地里拉</summary>
        private static void SpawnProbe(NPC boss, int slot) {
            Vector2 spawnAt = Vector2.Zero;
            bool fromPile = false;
            int pileType = ModContent.ProjectileType<Projectiles.ScrapJunkPile>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == pileType) {
                    spawnAt = p.Center;
                    p.Kill();
                    fromPile = true;
                    break;
                }
            }
            if (!fromPile) {
                float x = boss.Center.X + (slot - 1) * 150f + Main.rand.NextFloat(-40f, 40f);
                float y = FindGroundY(new Vector2(x, boss.Center.Y));
                spawnAt = new Vector2(x, y - 10f);
            }

            int index = NPC.NewNPC(boss.GetSource_FromAI(), (int)spawnAt.X, (int)spawnAt.Y,
                ModContent.NPCType<ScrapLegionProbe>(), 0, boss.whoAmI, slot);
            if (index < Main.maxNPCs && VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, index);
            }
        }
    }
}
