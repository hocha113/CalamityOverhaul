using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 蚀祭:暗影盘滑向主星(食相自身即 90 帧预告),全食期冕矛自星面辐射;<br/>
    /// 本影楔从玩家所在角起步(先给安全区),慢漂移可步行跟随;司祭跪祷不出手
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Eclipse, typeof(CultistStateContext))]
    internal class CultistEclipseState : CultistStateBase
    {
        public override string StateName => "CultistEclipse";
        public override CultistStateIndex StateIndex => CultistStateIndex.Eclipse;

        private const int Timeout = 420;

        /// <summary>没抓到常驻主星时直接放弃(权威端置位)</summary>
        private bool aborted;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            aborted = false;
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            SetPose(npc, 13);
            FaceTarget(npc, player.Center);
            context.PushAura(0.85f, CultistMotion.PhaseCore(context.Phase));
            context.BodyHot = MathHelper.Max(context.BodyHot, MathHelper.Clamp((Timer - 90f) / 80f, 0f, 0.7f));

            Vector2 hover = context.ArenaCenter + new Vector2(0f, -440f)
                + CultistMotion.BreathingOffset(seed: 9.2f, 8f);
            CultistMotion.SpringHover(npc, hover, 0.012f, 0.09f, 16f);

            //起蚀(权威端):本影基角=当下玩家方位(先给安全区),漂移方向随机签名
            if (Timer == 12 && !VaultUtils.isClient) {
                Projectile planet = FindPlanet(npc.whoAmI);
                if (planet == null) {
                    aborted = true;
                }
                else {
                    float umbraBase = (player.Center - planet.Center).ToRotation();
                    float drift = (Main.rand.NextBool() ? 1f : -1f) * 0.0045f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), planet.Center, Vector2.Zero,
                        ModContent.ProjectileType<CultistUmbraShade>(), 0, 0f, Main.myPlayer,
                        npc.whoAmI, umbraBase, drift);
                    npc.netUpdate = true;
                }
            }
            if (Timer == 12 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.9f, Pitch = -0.5f }, npc.Center);
            }

            if (VaultUtils.isClient) {
                return null;
            }

            if (aborted) {
                return new CultistCoilState();
            }
            if (Timer > 60 && !AnyShadeAlive(npc.whoAmI)) {
                return new CultistCoilState(30);
            }
            if (Timer >= Timeout) {
                return new CultistCoilState(30);
            }
            return null;
        }

        /// <summary>找常驻非幻象主星</summary>
        internal static Projectile FindPlanet(int ownerWho) {
            int type = ModContent.ProjectileType<CultistPlanetProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[1] == ownerWho
                    && (int)proj.ai[2] % 10 == 1 && (int)proj.ai[2] / 10 == 0) {
                    return proj;
                }
            }
            return null;
        }

        private static bool AnyShadeAlive(int ownerWho) {
            int type = ModContent.ProjectileType<CultistUmbraShade>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[0] == ownerWho) {
                    return true;
                }
            }
            return false;
        }
    }
}
