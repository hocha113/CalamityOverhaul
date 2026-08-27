using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 死亡演出:主星裂解内爆(全场唯一的大震拍)→浑天仪三环逆序崩碎(节奏渐急)→帷幕褪色终散<br/>
    /// 演出毕由权威端补刀走 vanilla 死亡(掉落与进度旗照常)
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Death, typeof(CultistStateContext))]
    internal class CultistDeathState : CultistStateBase
    {
        public override string StateName => "CultistDeath";
        public override CultistStateIndex StateIndex => CultistStateIndex.Death;

        private const int Duration = 300;
        /// <summary>三环崩碎拍(渐急);主星引爆(清场令 10+裂纹 40+坍缩 14=64 拍)后接手节奏</summary>
        private static readonly int[] BreakBeats = [128, 168, 198];

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
            SetPose(npc, Timer < 200 ? 13 : 0);
            npc.velocity = new Vector2(0f, -0.3f + t * 0.9f);

            Color core = CultistMotion.PhaseCore(context.Phase);
            CultistScreenFX.SetVeil(0.85f * (1f - t * 0.5f), npc.Center, core, 540f);
            CultistScreenFX.BreakDesat = MathHelper.Clamp(t * 1.3f, 0f, 0.85f);
            context.StaggerWobble = MathHelper.Max(context.StaggerWobble, 0.5f * t);

            //清场+主星裂解:他召的一切随他而散(权威端)
            if (Timer == 10 && !VaultUtils.isClient) {
                CultistBossAI.ClearHostileKit(npc);
                CultistPlanetProj.CommandExplode(npc.whoAmI);
                CultistZodiacRing.BeginCollapse(npc.whoAmI);
            }

            //主星引爆拍由星球自身的 DetonationBurst 演出(64 拍),这里只补一记司祭的哀鸣呼应
            if (Timer == 70 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 1.2f, Pitch = -0.8f }, npc.Center);
            }

            //三环逆序崩碎:法器随主人死去
            for (int i = 0; i < BreakBeats.Length; i++) {
                if (Timer == BreakBeats[i]) {
                    context.OrreryReveal = 2f - i;
                    CultistMotion.RuneBurst(npc.Center, CultistMotion.RuneGold, 14 + i * 5, 8f);
                    CultistMotion.Shake(npc.Center, 5f + i * 1.5f, 12);
                    CultistScreenFX.PushFlash(0.25f + i * 0.1f);
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.9f, Pitch = -0.4f + i * 0.25f }, npc.Center);
                    }
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
                //放行 CheckDead 并补刀:走 vanilla 死亡结算(掉落与进度旗照常)
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
