using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 掷星:他把神器当武器。主星收势退向远平面(56 帧全程可见=预告)→预瞄线追瞄锁死→沿线掷出→自行归位<br/>
    /// 蓄势期本体反向后撤(拉弓的身体语言);预瞄线末 14 帧冻结=预告即承诺;星球只在近平面咬人,撞黄道环反弹
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.PlanetHurl, typeof(CultistStateContext))]
    internal class CultistPlanetHurlState : CultistStateBase
    {
        public override string StateName => "CultistPlanetHurl";
        public override CultistStateIndex StateIndex => CultistStateIndex.PlanetHurl;

        private const int Windup = 56;
        private const int Duration = 156;
        /// <summary>预瞄线生成拍:寿命定值使其归零帧恰为出手帧</summary>
        private const int AimLineBeat = Windup - CultistPlanetAimLine.Lifetime;

        /// <summary>没抓到可掷的星球时直接放弃(权威端置位)</summary>
        private bool aborted;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            aborted = false;
            if (!VaultUtils.isClient) {
                //收势令:主星拉到头顶远平面
                aborted = !CultistPlanetProj.CommandRecede(context.Npc.whoAmI);
            }
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            SetPose(npc, 11);
            FaceTarget(npc, player.Center);
            context.PushAura(0.85f, CultistMotion.PhaseCore(context.Phase));
            context.OrreryGlow = 1f;

            //拉弓身体语言:蓄势期反向后撤(pow2 渐深),掷出瞬反冲
            if (Timer < Windup) {
                float t = Timer / (float)Windup;
                Vector2 away = (npc.Center - player.Center).SafeNormalize(Vector2.UnitX);
                Vector2 hover = player.Center
                    + away * (420f + t * t * 180f)
                    + new Vector2(0f, -260f)
                    + CultistMotion.BreathingOffset(seed: 7.7f, 10f);
                CultistMotion.SpringHover(npc, hover, 0.014f, 0.09f, 17f);
            }
            else {
                npc.velocity *= 0.94f;
            }

            //蓄势语调
            if (Timer == 8 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.9f, Pitch = -0.3f }, npc.Center);
            }
            if (Timer % 10 == 0 && Timer < Windup) {
                CultistMotion.RuneBurst(npc.Center + new Vector2(0f, -30f),
                    CultistMotion.PhaseCore(context.Phase), 2, 3.5f);
                context.ScalePulse = 1.05f;
            }

            //预瞄线上桩(权威端):挂在收势主星上,追瞄期跟人,末 14 帧冻结
            if (Timer == AimLineBeat && !VaultUtils.isClient && !aborted) {
                Projectile planet = FindRecedingPlanet(npc.whoAmI);
                if (planet != null) {
                    Vector2 aim = CultistMotion.PredictTarget(player, planet.Center, 9f, 0.55f);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), planet.Center, Vector2.Zero,
                        ModContent.ProjectileType<CultistPlanetAimLine>(), 0, 0f, Main.myPlayer,
                        planet.whoAmI, aim.X, aim.Y);
                }
            }

            //掷出:沿预瞄线锁定点出手(预告即承诺);预瞄线缺席时回退现算预判
            if (Timer == Windup && !VaultUtils.isClient) {
                Vector2 aim = CultistPlanetAimLine.GetLockedAim(npc.whoAmI)
                    ?? CultistMotion.PredictTarget(player, npc.Center, 9f, 0.55f);
                CultistPlanetProj.CommandLaunch(npc.whoAmI, aim);
                npc.velocity -= (aim - npc.Center).SafeNormalize(Vector2.UnitY) * 7f;
                npc.netUpdate = true;
            }
            if (Timer == Windup) {
                CultistMotion.Shake(npc.Center, 7f, 13);
                CultistScreenFX.PushFlash(0.22f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 1.1f, Pitch = -0.4f }, npc.Center);
                }
            }

            if (VaultUtils.isClient) {
                return null;
            }
            if (aborted || Timer >= Duration) {
                return new CultistCoilState();
            }
            return null;
        }

        /// <summary>找收势待掷段的非幻象主星(预瞄线挂点)</summary>
        private static Projectile FindRecedingPlanet(int ownerWho) {
            int type = ModContent.ProjectileType<CultistPlanetProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[1] == ownerWho
                    && (int)proj.ai[2] % 10 == 3 && (int)proj.ai[2] / 10 == 0) {
                    return proj;
                }
            }
            return null;
        }
    }
}
