using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 古咒唤影：召 3 枚远古厄运（P2+ 加 2 道远古光辉）后本体退至高位持咒，不再出手，带作业的呼吸拍<br/>
    /// 公平阀：厄运离玩家保底 300px，其爆环有 vanilla 自带脉冲预告；本体全程可打（拆作业还是打本体的取舍窗）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.AncientRite, typeof(CultistStateContext))]
    internal class CultistAncientRiteState : CultistStateBase
    {
        public override string StateName => "CultistAncientRite";
        public override CultistStateIndex StateIndex => CultistStateIndex.AncientRite;

        private const int Duration = 230;

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            SetPose(npc, 12);
            FaceTarget(npc, player.Center);

            //高位持咒驻停
            float side = npc.Center.X < player.Center.X ? -1f : 1f;
            Vector2 hover = player.Center + new Vector2(side * 420f, -340f)
                + CultistMotion.BreathingOffset(seed: 3.3f, 10f);
            CultistMotion.SpringHover(npc, hover, 0.01f, 0.09f, 14f);

            context.PushAura(0.5f, CultistMotion.RuneGold);

            //起咒
            if (Timer == 24) {
                CultistMotion.CastFlash(npc.Center, CultistMotion.RuneGold, 1.2f);
                CultistMotion.Shake(npc.Center, 3f, 8);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.8f, Pitch = -0.2f }, npc.Center);
                }
            }

            //召唤（权威端）：厄运绕玩家弧列，光辉从两翼扇入
            if (Timer == 30 && !VaultUtils.isClient) {
                for (int i = -1; i <= 1; i++) {
                    //弧列在玩家背向本体的一侧，围而不堵（保底 400px，vanilla 厄运自带膨爆预告）
                    Vector2 away = (player.Center - npc.Center).SafeNormalize(Vector2.UnitX);
                    Vector2 pos = player.Center + away.RotatedBy(i * 0.72f) * 400f;
                    //vanilla aiStyle101 合同：ai[0]=本体索引（膨速随本体血量联动）
                    int doom = NPC.NewNPC(npc.GetSource_FromAI(), (int)pos.X, (int)pos.Y,
                        NPCID.AncientDoom, 0, npc.whoAmI);
                    SyncMinion(doom);
                }
                if (context.Phase >= 1) {
                    for (int i = 0; i < 2; i++) {
                        //vanilla 星辉合同：ai[1]=自旋漂移，出生带速度
                        Vector2 pos = player.Center + new Vector2(i == 0 ? -620f : 620f, -260f);
                        Vector2 vel = (player.Center - pos).SafeNormalize(Vector2.UnitY).RotatedBy(
                            Main.rand.NextFloat(-0.4f, 0.4f)) * 8f;
                        float drift = (Main.rand.NextFloat() - 0.5f) * 0.3f * MathHelper.TwoPi / 60f;
                        int light = NPC.NewNPC(npc.GetSource_FromAI(), (int)pos.X, (int)pos.Y,
                            NPCID.AncientLight, 0, 0f, drift, vel.X, vel.Y);
                        if (light < Main.maxNPCs) {
                            Main.npc[light].velocity = vel;
                        }
                        SyncMinion(light);
                    }
                }
            }

            //持咒符文缓涌
            if (Timer > 30 && Timer % 12 == 0) {
                CultistMotion.RuneBurst(npc.Center + Main.rand.NextVector2Circular(26f, 34f),
                    CultistMotion.RuneGold, 1, 2f);
            }

            if (VaultUtils.isClient) {
                return null;
            }
            if (Timer >= Duration) {
                return new CultistWeaveState();
            }
            return null;
        }

        /// <summary>随从出生广播（权威端）</summary>
        internal static void SyncMinion(int index) {
            if (index < Main.maxNPCs && Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, index);
            }
        }
    }
}
