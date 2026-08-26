using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 入场演出:静默祷姿显形→浑天仪三环逐一显形(节奏渐急)→黄道环划界+首星穿门降临<br/>
    /// 威压来自静止;前 118 帧免伤防抢戏,全程无伤输出
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Intro, typeof(CultistStateContext))]
    internal class CultistIntroState : CultistStateBase
    {
        public override string StateName => "CultistIntro";
        public override CultistStateIndex StateIndex => CultistStateIndex.Intro;

        private const int Duration = 236;
        /// <summary>三环显形拍(渐急:44→32→22 间隔)</summary>
        private static readonly int[] RingBeats = [42, 86, 118];
        private const int CommitBeat = 142;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            npc.alpha = 255;
            npc.dontTakeDamage = true;
            npc.velocity = Vector2.Zero;
            context.OrreryReveal = 0f;
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            npc.velocity *= 0.9f;
            SetPose(npc, 13);
            FaceTarget(npc, context.Target.Center);

            //真身显形:20~70 帧渐显,先站定再动作
            npc.alpha = (int)MathHelper.Clamp(255f - (Timer - 20) * 5.2f, 0f, 255f);
            CultistScreenFX.SetVeil(0.3f, npc.Center, CultistMotion.PhaseCore(0), 560f);

            //三环显形:每环一拍,节奏渐急,链音爬调
            for (int i = 0; i < RingBeats.Length; i++) {
                if (Timer == RingBeats[i]) {
                    context.PushAura(0.6f + i * 0.15f, CultistMotion.PhaseCore(0));
                    CultistMotion.RuneBurst(npc.Center, CultistMotion.RuneGold, 8 + i * 4, 5f);
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item101 with { Volume = 0.7f, Pitch = -0.2f + i * 0.28f }, npc.Center);
                    }
                }
            }
            //环显形进度:各环 26 帧张开
            float reveal = 0f;
            for (int i = 0; i < RingBeats.Length; i++) {
                reveal += MathHelper.Clamp((Timer - RingBeats[i]) / 26f, 0f, 1f);
            }
            context.OrreryReveal = reveal;

            //定形迸发:黄道环划界+首星降临,他划下的天穹成为这场仪式的边界
            if (Timer == CommitBeat) {
                context.PushAura(1f, CultistMotion.PhaseCore(0));
                context.OrreryGlow = 1f;
                CultistMotion.SigilCommitFX(npc.Center, CultistMotion.PhaseCore(0), 1.4f);
                CultistMotion.RuneBurst(npc.Center, CultistMotion.RuneGold, 16, 7f);
                CultistMotion.Shake(npc.Center, 5f, 12);
                CultistScreenFX.PushFlash(0.35f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 0.9f, Pitch = 0.1f }, npc.Center);
                }
            }

            //黄道环+首颗星球(权威端):场心取本体与目标中点,定桩后全场不动
            if (Timer == CommitBeat && !VaultUtils.isClient && !context.ArenaSpawned) {
                context.ArenaCenter = Vector2.Lerp(npc.Center, context.Target.Center, 0.5f);
                context.ArenaSpawned = true;
                Projectile.NewProjectile(npc.GetSource_FromAI(), context.ArenaCenter, Vector2.Zero,
                    ModContent.ProjectileType<CultistZodiacRing>(), 0, 0f, Main.myPlayer, npc.whoAmI);
                //星旋在他头顶穿门而来,裹挟风暴,随后自行漂回场心游走
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + new Vector2(0f, -430f), Vector2.Zero,
                    ModContent.ProjectileType<CultistPlanetProj>(), 60, 0f, Main.myPlayer,
                    CultistPlanetProj.KindVortex, npc.whoAmI, 0f);
                npc.netUpdate = true;
            }

            if (Timer >= CommitBeat) {
                npc.dontTakeDamage = false;
            }

            if (VaultUtils.isClient) {
                return null;
            }
            if (Timer >= Duration) {
                return new CultistCoilState();
            }
            return null;
        }

        public override void OnExit(CultistStateContext context) {
            context.Npc.alpha = 0;
            context.Npc.dontTakeDamage = false;
            context.OrreryReveal = 3f;
        }
    }
}
