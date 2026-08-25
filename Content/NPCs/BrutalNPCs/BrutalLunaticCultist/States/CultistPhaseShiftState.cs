using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 转阶段演出：清场+旧星球退场 → 仰首嘶吼 → 新星球降临;星云相多出两颗幻象,星尘相唤幻影龙<br/>
    /// 全程免伤但不出手,攻势不跨阶段(公平阀:清弹幕+新阶段起手有缓冲)
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.PhaseShift, typeof(CultistStateContext))]
    internal class CultistPhaseShiftState : CultistStateBase
    {
        public override string StateName => "CultistPhaseShift";
        public override CultistStateIndex StateIndex => CultistStateIndex.PhaseShift;

        private const int Duration = 140;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            context.IsInPhaseTransition = true;
            npc.dontTakeDamage = true;
            npc.velocity = Vector2.Zero;

            if (!VaultUtils.isClient) {
                context.Phase++;
                context.Element = context.Phase;
                npc.ai[0] = context.Phase;
                npc.ai[1] = context.Element;
                context.ClearAttackBag();
                //转阶段后给玩家缓冲，别立刻咏唱
                context.ChantCooldown = System.Math.Max(context.ChantCooldown, 360);
                npc.netUpdate = true;
            }
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            SetPose(npc, 13);
            npc.velocity *= 0.9f;

            Color core = CultistMotion.PhaseCore(context.Phase);
            CultistScreenFX.SetVeil(0.65f, npc.Center, core, 680f);
            context.PushAura(0.9f, core);

            //清场+旧星球退场：旧攻势不跨阶段（权威端）
            if (Timer == 10 && !VaultUtils.isClient) {
                CultistBossAI.ClearMinionsAndProjectiles(npc);
                CultistPlanetProj.BeginDeparture(npc.whoAmI);
            }

            //嘶吼顿点
            if (Timer == 34) {
                context.SigilCommit = 1f;
                CultistScreenFX.PushFlash(0.45f);
                CultistMotion.Shake(npc.Center, 6f, 14);
                CultistMotion.RuneBurst(npc.Center, CultistMotion.RuneGold, 18, 7.5f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 1.2f, Pitch = -0.15f }, npc.Center);
                }
            }

            //新星球降临(权威端):星云相带两颗幻象,遮挡程度即识真线索
            if (Timer == 52 && !VaultUtils.isClient) {
                int kind = System.Math.Clamp(context.Phase, 0, 4);
                Projectile.NewProjectile(npc.GetSource_FromAI(), context.ArenaCenter, Vector2.Zero,
                    ModContent.ProjectileType<CultistPlanetProj>(), 60, 0f, Main.myPlayer,
                    kind, npc.whoAmI, 0f);
                if (kind == CultistPlanetProj.KindNebula) {
                    for (int i = 1; i <= 2; i++) {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), context.ArenaCenter, Vector2.Zero,
                            ModContent.ProjectileType<CultistPlanetProj>(), 60, 0f, Main.myPlayer,
                            kind, npc.whoAmI, i * 10f);
                    }
                }
            }

            //星尘相：唤出幻影龙(召唤柱的龙,可猎杀换充能削减)
            if (Timer == 80 && context.Phase == 2 && !context.DragonSpawned && !VaultUtils.isClient) {
                Vector2 pos = npc.Center + new Vector2(npc.direction * -500f, -320f);
                //ai[0]=0 让龙头自建 30 节体段并共享血池（realLife）
                int head = NPC.NewNPC(npc.GetSource_FromAI(), (int)pos.X, (int)pos.Y, NPCID.CultistDragonHead);
                if (head < Main.maxNPCs) {
                    Main.npc[head].lifeMax = Main.npc[head].life = 3600;
                    CultistBossAI.SyncMinion(head);
                }
                context.DragonSpawned = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie102 with { Volume = 1f }, pos);
                }
            }

            //嘶吼后符文持续涌
            if (Timer > 34 && Timer % 6 == 0) {
                CultistMotion.RuneBurst(npc.Center + Main.rand.NextVector2Circular(24f, 32f), core, 1, 3f);
            }

            if (VaultUtils.isClient) {
                return null;
            }
            if (Timer >= Duration) {
                return new CultistWeaveState();
            }
            return null;
        }

        public override void OnExit(CultistStateContext context) {
            context.IsInPhaseTransition = false;
            context.Npc.dontTakeDamage = false;
        }
    }
}
