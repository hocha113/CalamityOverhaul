using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 死亡演出：仪式反噬，法阵碎裂声起，符文从身上剥落回天，帷幕褪色，最后一声与本体一同散场<br/>
    /// 演出毕由权威端补刀走 vanilla 死亡（掉落与进度旗照常）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Death, typeof(CultistStateContext))]
    internal class CultistDeathState : CultistStateBase
    {
        public override string StateName => "CultistDeath";
        public override CultistStateIndex StateIndex => CultistStateIndex.Death;

        private const int Duration = 150;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            npc.dontTakeDamage = true;
            npc.velocity = Vector2.Zero;
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            float t = Timer / (float)Duration;
            SetPose(npc, Timer < 100 ? 13 : 0);
            npc.velocity = new Vector2(0f, -0.3f + t * 0.8f);

            Color core = CultistMotion.ElementCore(context.Element);
            CultistScreenFX.SetVeil(0.85f * (1f - t * 0.5f), npc.Center, core, 540f);
            CultistScreenFX.BreakDesat = MathHelper.Clamp(t * 1.3f, 0f, 0.85f);

            //清场：他召的东西随他而散（权威端）
            if (Timer == 10 && !VaultUtils.isClient) {
                CultistBossAI.ClearMinionsAndProjectiles(npc);
            }

            //法阵碎裂顿点
            if (Timer == 42) {
                context.SigilCommit = 1f;
                CultistMotion.Shake(npc.Center, 6f, 14);
                CultistScreenFX.PushFlash(0.4f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Shatter with { Volume = 1f, Pitch = -0.3f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 0.9f, Pitch = -0.7f }, npc.Center);
                }
            }

            //符文剥落回天
            if (Timer > 42 && Timer % 5 == 0) {
                CultistMotion.RuneBurst(npc.Center + Main.rand.NextVector2Circular(22f, 34f),
                    Timer % 10 == 0 ? CultistMotion.RuneGold : core, 2, 3.5f);
                context.ScalePulse = 1.05f;
            }

            //终散
            if (Timer == Duration - 8) {
                CultistScreenFX.PushFlash(0.7f);
                CultistMotion.RuneBurst(npc.Center, CultistMotion.RuneGold, 26, 9f);
                CultistMotion.Shake(npc.Center, 8f, 16);
            }

            if (VaultUtils.isClient) {
                return null;
            }
            if (Timer >= Duration && !context.DeathPerformanceFinished) {
                //放行 CheckDead 并补刀：走 vanilla 死亡结算（掉落与进度旗照常）
                context.DeathPerformanceFinished = true;
                npc.dontTakeDamage = false;
                npc.life = 0;
                npc.HitEffect();
                npc.checkDead();
                npc.netUpdate = true;
            }
            return null;
        }
    }
}
