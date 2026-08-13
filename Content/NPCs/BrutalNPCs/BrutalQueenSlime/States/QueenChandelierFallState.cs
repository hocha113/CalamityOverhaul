using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States
{
    /// <summary>水晶吊灯：空中悬晶错帧蓄能坠落，皇后在吊灯间起舞点射</summary>
    [InnoVault.StateMachines.VaultState((int)QueenSlimeStateIndex.ChandelierFall, typeof(QueenSlimeStateContext))]
    internal class QueenChandelierFallState : QueenSlimeStateBase
    {
        public override string StateName => "ChandelierFall";
        public override QueenSlimeStateIndex StateIndex => QueenSlimeStateIndex.ChandelierFall;

        private const int TotalTime = 350;

        private Vector2 stageCenter;
        private bool anchored;

        public QueenChandelierFallState() {
        }

        public override void OnEnter(QueenSlimeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            DisableContactDamage(npc);
            npc.noGravity = true;
            npc.noTileCollide = true;
            anchored = false;
        }

        public override IQueenSlimeState OnUpdate(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;
            DisableContactDamage(npc);

            if (!anchored) {
                anchored = true;
                stageCenter = player.Center;
                //挂灯(服务端)
                if (!VaultUtils.isClient) {
                    int count = context.IsDeathMode ? 5 : 4;
                    float spacing = 300f;
                    for (int i = 0; i < count; i++) {
                        float x = stageCenter.X + (i - (count - 1) * 0.5f) * spacing;
                        Vector2 pos = new Vector2(x, stageCenter.Y - 440f);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                            ModContent.ProjectileType<QueenChandelierProj>(), QueenChandelierProj.BurstDamage, 0f, Main.myPlayer,
                            i * 26, 0f, i * 0.19f);
                    }
                }
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.85f, Pitch = -0.25f }, npc.Center);
            }

            //皇后在灯间穿舞：正弦滑步于灯层上方
            float t = Timer * 0.021f;
            Vector2 anchor = stageCenter + new Vector2((float)Math.Sin(t) * 480f, -530f + (float)Math.Cos(t * 1.7f) * 46f);
            QueenMotion.SpringHover(npc, anchor, 0.015f, 0.1f, 19f);
            QueenMotion.FlightLean(npc);
            context.PoseCommand = 5;
            FaceTarget(npc, player.Center);

            //间奏点射：单发瞄准珠(服务端)
            if (Timer % 68 == 34 && Timer < TotalTime - 80 && !VaultUtils.isClient) {
                Vector2 vel = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY) * 9.4f;
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                    ModContent.ProjectileType<QueenShardProj>(), QueenShardProj.ShardDamage, 0f, Main.myPlayer,
                    (int)QueenShardProj.Mode.Shard, 0f, Timer * 0.01f % 1f);
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.55f, Pitch = 0.4f }, npc.Center);
            }

            if (Timer >= TotalTime && !VaultUtils.isClient) {
                return new QueenAerialBalletState();
            }

            return null;
        }
    }
}
