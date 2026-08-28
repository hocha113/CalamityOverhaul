using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 追星矢(全相恒在,池内三倍权重;掷星/蚀祭/星图收势后固定跟手):司祭举手,头顶凝成一排奥术星,按槽位错拍逐颗锁向掷出<br/>
    /// 公平阀:每矢出手前有预瞄线且末段冻结(预告即承诺),掷出后纯直线;<br/>
    /// 错拍常量声明于 CultistSeekerStar(FirstBeat/BeatGap),节奏可学
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.SeekerStars, typeof(CultistStateContext))]
    internal class CultistSeekerStarsState : CultistStateBase
    {
        public override string StateName => "CultistSeekerStars";
        public override CultistStateIndex StateIndex => CultistStateIndex.SeekerStars;

        /// <summary>凝星拍:一次性铸出全排</summary>
        private const int CastBeat = 10;
        private const int Timeout = 240;

        private static int StarCount(CultistStateContext context) =>
            context.Phase >= 4 || context.IsAsuraMode ? 6 : 5;

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            SetPose(npc, 11);
            FaceTarget(npc, player.Center);
            context.PushAura(0.75f, CultistMotion.PhaseCore(context.Phase));

            //中距压场:星冠在头顶亮着,一颗颗飞出去
            float side = npc.Center.X < player.Center.X ? -1f : 1f;
            Vector2 hover = player.Center + new Vector2(side * 460f, -260f)
                + CultistMotion.BreathingOffset(seed: 19.3f, 10f);
            CultistMotion.SpringHover(npc, hover, 0.012f, 0.09f, 16f);

            //凝星拍:全排一次铸出,出手拍由星自身错拍推进
            if (Timer == CastBeat) {
                CultistMotion.CastFlash(npc.Center + new Vector2(0f, -80f),
                    CultistMotion.PhaseCore(context.Phase), 1.2f);
                context.ScalePulse = 1.10f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.9f, Pitch = 0.3f }, npc.Center);
                }
                if (!VaultUtils.isClient) {
                    int count = StarCount(context);
                    for (int slot = 0; slot < count; slot++) {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + new Vector2(0f, -60f),
                            Vector2.Zero, ModContent.ProjectileType<CultistSeekerStar>(), 40, 0f,
                            Main.myPlayer, npc.whoAmI, slot);
                    }
                    npc.netUpdate = true;
                }
            }

            //凝聚期祷文微涌
            if (Timer > CastBeat && Timer % 10 == 0) {
                CultistMotion.RuneBurst(npc.Center + new Vector2(0f, -40f),
                    CultistMotion.PhaseCore(context.Phase), 1, 3f);
            }

            if (VaultUtils.isClient) {
                return null;
            }

            //双出口:全排掷完即收(飞出去的矢自巡自灭,不占司祭的手),或超时兜底
            int launchedAll = CastBeat + CultistSeekerStar.FirstBeat
                + StarCount(context) * CultistSeekerStar.BeatGap + 14;
            if (Timer >= launchedAll) {
                return new CultistCoilState();
            }
            if (Timer >= Timeout) {
                return new CultistCoilState();
            }
            return null;
        }
    }
}
