using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Rendering;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States
{
    /// <summary>折射牢笼：五芒棱晶环+跑马灯光束网，击碎节点开出永久缺口</summary>
    [InnoVault.StateMachines.VaultState((int)QueenSlimeStateIndex.RefractionCage, typeof(QueenSlimeStateContext))]
    internal class QueenRefractionCageState : QueenSlimeStateBase
    {
        public override string StateName => "RefractionCage";
        public override QueenSlimeStateIndex StateIndex => QueenSlimeStateIndex.RefractionCage;

        private const int NodeCount = 5;
        private const float CageRadius = 470f;
        private const int MaterializeTime = 50;
        private const int WebTime = 320;
        private const int CollapseTime = 34;
        private const int TotalTime = MaterializeTime + WebTime + CollapseTime;

        /// <summary>笼心(玩家开场位置捕获)，各端由节点均值重建</summary>
        private Vector2 cageCenter;
        private bool centerCaptured;

        public QueenRefractionCageState() {
        }

        public override void OnEnter(QueenSlimeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            DisableContactDamage(npc);
            npc.noGravity = true;
            npc.noTileCollide = true;
            centerCaptured = false;
        }

        public override IQueenSlimeState OnUpdate(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;
            DisableContactDamage(npc);

            //笼心：服务端首帧捕获并布节点；客户端由在场节点均值重建
            if (!centerCaptured) {
                if (!VaultUtils.isClient) {
                    cageCenter = player.Center;
                    centerCaptured = true;
                    SpawnCage(context);
                }
                else {
                    List<NPC> found = context.CollectPrismNodes();
                    if (found.Count >= 3) {
                        Vector2 sum = Vector2.Zero;
                        foreach (var n in found) {
                            sum += n.Center;
                        }
                        cageCenter = sum / found.Count;
                        centerCaptured = true;
                    }
                    else {
                        cageCenter = player.Center;
                    }
                }
            }

            //皇后姿态：笼顶上方摇曳凝视
            float sway = (float)Math.Sin(Timer * 0.024f) * 160f;
            Vector2 anchor = cageCenter + new Vector2(sway, -CageRadius - 210f);
            QueenMotion.SpringHover(npc, anchor, 0.012f, 0.09f, 15f);
            QueenMotion.FlightLean(npc);
            context.PoseCommand = 5;
            FaceTarget(npc, player.Center);

            //物化段
            if (Timer <= MaterializeTime) {
                float p = Timer / (float)MaterializeTime;
                context.SetChargeState(1, p);
                if (Timer == 6) {
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.9f, Pitch = -0.1f }, cageCenter);
                }
                return null;
            }

            //织网帧(服务端一次)：馈线+环网
            if (Timer == MaterializeTime + 1 && !VaultUtils.isClient) {
                WeaveWeb(context);
            }

            //织网期间：皇后偶发凝胶单珠(服务端)
            int webT = Timer - MaterializeTime;
            if (webT > 0 && webT < WebTime && webT % 85 == 0 && !VaultUtils.isClient) {
                for (int i = -1; i <= 1; i++) {
                    Vector2 vel = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY).RotatedBy(i * 0.24f) * 8.6f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                        ModContent.ProjectileType<QueenShardProj>(), QueenShardProj.PearlDamage, 0f, Main.myPlayer,
                        (int)QueenShardProj.Mode.Pearl, 0f, i * 0.3f + 0.5f);
                }
                SoundEngine.PlaySound(SoundID.Item155 with { Volume = 0.6f, Pitch = 0.3f }, npc.Center);
            }
            context.PrismShimmer = Math.Max(context.PrismShimmer, 0.45f);

            //收笼：碎环退场
            if (Timer == MaterializeTime + WebTime && !VaultUtils.isClient) {
                ShatterCage(context);
            }

            if (Timer >= TotalTime && !VaultUtils.isClient) {
                return new QueenAerialBalletState();
            }

            return null;
        }

        /// <summary>布置五芒节点(服务端)，先清战场残留节点</summary>
        private void SpawnCage(QueenSlimeStateContext context) {
            foreach (var stale in context.CollectPrismNodes()) {
                QueenMotion.ScriptKill(stale);
            }
            for (int i = 0; i < NodeCount; i++) {
                float angle = MathHelper.TwoPi * i / NodeCount - MathHelper.PiOver2;
                Vector2 pos = cageCenter + angle.ToRotationVector2() * CageRadius;
                NPC node = QueenMotion.SpawnMinion(context.Npc, NPCID.QueenSlimeMinionBlue,
                    QueenMinionRole.PrismNode, i, pos, QueenSlimeMinionAI.PrismNodeLife());
                if (node != null) {
                    node.ai[3] = MaterializeTime + WebTime + 10;
                    node.netUpdate = true;
                }
            }
        }

        /// <summary>织网(服务端)：王冠馈线+节点环网跑马灯</summary>
        private void WeaveWeb(QueenSlimeStateContext context) {
            List<NPC> nodes = context.CollectPrismNodes();
            if (nodes.Count == 0) {
                return;
            }

            //王冠→首节点馈线(常亮低伤)
            Projectile.NewProjectile(context.Npc.GetSource_FromAI(), QueenSlimeRenderHelper.CrownAnchor(context.Npc),
                Vector2.Zero, ModContent.ProjectileType<QueenPrismBeamProj>(), QueenPrismBeamProj.BeamDamage, 0f, Main.myPlayer,
                context.Npc.whoAmI, nodes[0].whoAmI,
                QueenPrismBeamProj.PackMode(QueenPrismBeamProj.BeamMode.Feeder, 0, WebTime));

            //环网：i→i+1 跑马灯
            for (int i = 0; i < nodes.Count; i++) {
                NPC from = nodes[i];
                NPC to = nodes[(i + 1) % nodes.Count];
                Projectile.NewProjectile(context.Npc.GetSource_FromAI(), from.Center, Vector2.Zero,
                    ModContent.ProjectileType<QueenPrismBeamProj>(), QueenPrismBeamProj.BeamDamage, 0f, Main.myPlayer,
                    from.whoAmI, to.whoAmI,
                    QueenPrismBeamProj.PackMode(QueenPrismBeamProj.BeamMode.WebMarquee, i, WebTime));
            }
        }

        /// <summary>碎环(服务端击杀节点，原生死亡链同步各端演出)</summary>
        private void ShatterCage(QueenSlimeStateContext context) {
            foreach (var n in context.CollectPrismNodes()) {
                QueenMotion.ScriptKill(n);
            }
        }

        public override void OnExit(QueenSlimeStateContext context) {
            base.OnExit(context);
            DisableContactDamage(context.Npc);
        }
    }
}
