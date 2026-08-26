using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Disciples
{
    /// <summary>
    /// 奋锐党西门·狂热(席位10)：狂热圣火。
    /// 敌人贴身时点燃狂热：主人得攻速与移速，周围敌人染上圣焰
    /// </summary>
    internal class Zealot : BaseDisciple
    {
        public override int Seat => 10;

        private const float IgniteRange = 260f;

        protected override bool TryCast() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy(Projectile)
                    && Vector2.Distance(npc.Center, Projectile.Center) < IgniteRange) {
                    return true;
                }
            }
            return false;
        }

        protected override void ExecuteAbility() {
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.55f, Pitch = 0.5f }, Projectile.Center);

            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.AddBuff(ModContent.BuffType<ZealotFervorBuff>(), 360);
                //圣焰烧向贴近的敌人(客户端AddBuff会自动上报服务器)
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (npc.active && npc.CanBeChasedBy(Projectile)
                        && Vector2.Distance(npc.Center, Projectile.Center) < IgniteRange) {
                        npc.AddBuff(BuffID.Daybreak, 180);
                    }
                }
            }

            //燃焰迸发
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 9; i++) {
                float angle = MathHelper.TwoPi * i / 9f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 5f) - new Vector2(0f, 1.5f);
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, vel, Def.BodyColor, Main.rand.NextFloat(0.25f, 0.4f))
                    ?.Configure(Main.rand.Next(14, 24), 0.9f);
            }
        }

        protected override void PassiveTick() {
            //狂热者的圣焰余烬常燃
            if (!Main.dedServ && Main.rand.NextBool(9)) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(16f, 22f)
                    , new Vector2(0f, -Main.rand.NextFloat(0.8f, 1.8f)), Def.AccentColor, 0.2f)?.Configure(16, 0.7f);
            }
        }
    }
}
