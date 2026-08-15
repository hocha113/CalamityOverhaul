using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 远古强袭（P2）：嘶吼引三波远古光扇+弧列远古厄运布阵；npc.ai[3]=布阵种子
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.AncientAssault, typeof(CultistStateContext))]
    internal class CultistAncientAssaultState : CultistStateBase
    {
        public override string StateName => "AncientAssault";
        public override CultistStateIndex StateIndex => CultistStateIndex.AncientAssault;

        private const int Duration = 236;
        private static readonly int[] LightWaves = [34, 58, 82];
        private const int DoomMoment = 126;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isClient) {
                context.Npc.ai[3] = Main.rand.Next(1000);
                context.Npc.netUpdate = true;
            }
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            FaceTarget(context);
            context.ElementAura = 1f;
            CultistScreenFX.DeclareVeil(npc.Center, 0.22f, context.Element);

            if (player.Alives()) {
                SetHover(context, player.Center + new Vector2(0f, -380f));
            }

            //起手嘶吼
            if (Timer <= 30) {
                context.CastPose = CultistPose.Scream;
                context.CastGlow = Timer / 30f;
                if ((int)Timer == 8 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 1f, Pitch = 0.1f }, npc.Center);
                }
                return null;
            }

            //远古光三波扇
            foreach (int wave in LightWaves) {
                if ((int)Timer == wave) {
                    context.CastPose = CultistPose.CastForward;
                    context.CastGlow = 1f;
                    Vector2 hand = HandPos(npc);
                    if (!VaultUtils.isServer) {
                        CultistRenderHelper.CastBurst(hand, AimWithLead(npc, player, 10f), context.Element, 1.4f);
                        SoundEngine.PlaySound(SoundID.Item124 with { Volume = 0.8f, MaxInstances = 4 }, hand);
                    }
                    if (!VaultUtils.isClient && player.Alives()) {
                        //镜像原版远古光生成：扇形5连
                        Vector2 aim = AimWithLead(npc, player, 20f);
                        float fanStep = MathHelper.TwoPi / 25f;
                        for (int i = 0; i < 5; i++) {
                            Vector2 vel = (aim * 8f).RotatedBy(fanStep * i - (MathHelper.TwoPi / 5f - fanStep) / 2f);
                            float drift = (Main.rand.NextFloat() - 0.5f) * 0.3f * MathHelper.TwoPi / 60f;
                            int idx = NPC.NewNPC(npc.GetSource_FromAI(), (int)hand.X, (int)hand.Y + 7,
                                NPCID.AncientLight, 0, 0f, drift, vel.X, vel.Y);
                            if (idx < Main.maxNPCs) {
                                Main.npc[idx].velocity = vel;
                                Main.npc[idx].netUpdate = true;
                            }
                        }
                    }
                }
            }

            //远古厄运弧列布阵
            if ((int)Timer == DoomMoment) {
                context.CastPose = CultistPose.CastUp;
                context.CastGlow = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.9f, Pitch = -0.3f }, npc.Center);
                }
                if (!VaultUtils.isClient && player.Alives()) {
                    int count = context.IsDeathMode ? 7 : 6;
                    float baseAngle = npc.ai[3] * 0.13f;
                    for (int i = 0; i < count; i++) {
                        //环弧包夹，避实心墙：留出可穿缝
                        float angle = baseAngle + MathHelper.TwoPi * i / count;
                        Vector2 candidate = player.Center + angle.ToRotationVector2() * 380f;
                        if (!TryFindOpenSpot(ref candidate)) {
                            continue;
                        }
                        NPC.NewNPC(npc.GetSource_FromAI(),
                            (int)candidate.X, (int)candidate.Y, NPCID.AncientDoom, 0, npc.whoAmI);
                    }
                }
            }

            //布阵后的吟唱压场
            if (Timer > DoomMoment && Timer < Duration - 30) {
                context.CastPose = CultistPose.CastUp;
                context.CastGlow = 0.6f;
            }

            if (Timer >= Duration) {
                return new CultistWeaveState();
            }
            return null;
        }

        /// <summary>就近找无实体块落点，微扰最多12次</summary>
        private static bool TryFindOpenSpot(ref Vector2 pos) {
            for (int attempt = 0; attempt < 12; attempt++) {
                Vector2 candidate = pos + (attempt == 0 ? Vector2.Zero : Main.rand.NextVector2Circular(60f, 60f));
                Point tile = candidate.ToTileCoordinates();
                if (!Collision.SolidTiles(tile.X - 2, tile.X + 2, tile.Y - 2, tile.Y + 2)) {
                    pos = candidate;
                    return true;
                }
            }
            return false;
        }
    }
}
