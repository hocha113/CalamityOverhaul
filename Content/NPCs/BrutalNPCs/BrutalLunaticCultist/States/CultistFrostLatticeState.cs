using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 冰·霜晶阵列：印记定形后放射 3 条晶枪列占场，驻场约 2 秒再沿列崩解<br/>
    /// 公平阀：列间保底 50° 走廊；晶枪 20 帧无害生长；碎片沿已声明列向飞，不折向玩家
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.FrostLattice, typeof(CultistStateContext))]
    internal class CultistFrostLatticeState : CultistStateBase
    {
        public override string StateName => "CultistFrostLattice";
        public override CultistStateIndex StateIndex => CultistStateIndex.FrostLattice;

        private const int SigilCharge = 56;

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            SetPose(npc, 12);
            FaceTarget(npc, player.Center);

            //高位后撤：把场地留给晶阵
            float side = npc.Center.X < player.Center.X ? -1f : 1f;
            Vector2 hover = player.Center + new Vector2(side * 460f, -300f)
                + CultistMotion.BreathingOffset(seed: 2.7f, 10f);
            CultistMotion.SpringHover(npc, hover, 0.01f, 0.085f, 15f);

            //落印（权威端）：印心取本体与玩家中点，保底离玩家 260px
            bool second = context.Phase >= 1;
            if (!VaultUtils.isClient && (Timer == 18 || (second && Timer == 18 + 62))) {
                Vector2 mid = Vector2.Lerp(npc.Center, player.Center, 0.55f);
                if (mid.Distance(player.Center) < 260f) {
                    mid = player.Center + (mid - player.Center).SafeNormalize(Vector2.UnitX) * 260f;
                }
                Projectile.NewProjectile(npc.GetSource_FromAI(), mid, Vector2.Zero,
                    ModContent.ProjectileType<CultistSigilProj>(), 0, 0f, Main.myPlayer,
                    1f, 2f, SigilCharge);
            }

            if (Timer == 18 || (second && Timer == 80)) {
                context.PushAura(0.9f, CultistMotion.FrostCore);
                context.ScalePulse = 1.06f;
            }

            if (VaultUtils.isClient) {
                return null;
            }
            //收尾：最后一张印记的晶列生长完 + 驻场观察拍
            int total = (second ? 80 : 18) + SigilCharge + 96;
            if (Timer >= total) {
                return new CultistWeaveState();
            }
            return null;
        }
    }
}
