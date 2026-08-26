using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 月瞳凝视(月明专属):司祭跪祷,月面竖瞳 40 帧睁开(预告)后自星心扫出凝视光束<br/>
    /// 扫速声明恒定,跑在光前即安全;攻击主体是月亮,他只是祈祷
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Gaze, typeof(CultistStateContext))]
    internal class CultistGazeState : CultistStateBase
    {
        public override string StateName => "CultistGaze";
        public override CultistStateIndex StateIndex => CultistStateIndex.Gaze;

        private const int EyeOpenFrames = 40;
        private const int Timeout = 300;

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
            context.PushAura(0.9f, CultistMotion.MoonCore);
            //竖瞳开度:凝视全程顶满(自然回落交给上下文衰减)
            context.PupilOpen = MathHelper.Max(context.PupilOpen,
                MathHelper.Clamp(Timer / (float)EyeOpenFrames, 0f, 1f));

            Vector2 hover = context.ArenaCenter + new Vector2(0f, -460f)
                + CultistMotion.BreathingOffset(seed: 11.3f, 8f);
            CultistMotion.SpringHover(npc, hover, 0.012f, 0.09f, 15f);

            //睁眼起音
            if (Timer == 4 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie102 with { Volume = 0.9f, Pitch = -0.6f }, npc.Center);
            }

            //放束(权威端):起始角=玩家方位后撤半弧,扫过玩家所在扇区
            if (Timer == EyeOpenFrames && !VaultUtils.isClient) {
                Projectile planet = CultistEclipseState.FindPlanet(npc.whoAmI);
                if (planet == null) {
                    aborted = true;
                }
                else {
                    float sweepDir = Main.rand.NextBool() ? 1f : -1f;
                    float playerAngle = (player.Center - planet.Center).ToRotation();
                    float startAngle = playerAngle - sweepDir * 0.85f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), planet.Center, Vector2.Zero,
                        ModContent.ProjectileType<CultistGazeBeam>(), 50, 0f, Main.myPlayer,
                        startAngle, sweepDir * 0.011f, planet.whoAmI);
                    npc.netUpdate = true;
                }
            }

            if (VaultUtils.isClient) {
                return null;
            }

            if (aborted) {
                return new CultistCoilState();
            }
            if (Timer > EyeOpenFrames + 40 && !AnyBeamAlive(npc.whoAmI)) {
                return new CultistCoilState(30);
            }
            if (Timer >= Timeout) {
                return new CultistCoilState(30);
            }
            return null;
        }

        private static bool AnyBeamAlive(int ownerWho) {
            int type = ModContent.ProjectileType<CultistGazeBeam>();
            NPC owner = ownerWho >= 0 && ownerWho < Main.maxNPCs ? Main.npc[ownerWho] : null;
            if (owner == null || !owner.active) {
                return false;
            }
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type) {
                    return true;
                }
            }
            return false;
        }
    }
}
