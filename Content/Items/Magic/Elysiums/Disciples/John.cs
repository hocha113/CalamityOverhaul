using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Disciples
{
    /// <summary>
    /// 约翰·启示(席位3)：启示之眼。
    /// 向至多三个敌人送出注视之眼，中目者受到的一切伤害提高。
    /// 约翰不殉道，是启示录的钥匙
    /// </summary>
    internal class John : BaseDisciple
    {
        public override int Seat => 3;

        private const float CastRange = 600f;
        private const int MaxMarks = 3;

        protected override bool TryCast() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy(Projectile)
                    && !npc.HasBuff<RevelationMarkDebuff>()
                    && Vector2.Distance(npc.Center, Projectile.Center) < CastRange) {
                    return true;
                }
            }
            return false;
        }

        protected override void ExecuteAbility() {
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.65f, Pitch = 0.5f }, Projectile.Center);
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            int sent = 0;
            int damage = (int)(ElysiumPlayer.GetElysiumDamage(Owner) * 0.15f);
            for (int i = 0; i < Main.maxNPCs && sent < MaxMarks; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || !npc.CanBeChasedBy(Projectile)
                    || npc.HasBuff<RevelationMarkDebuff>()
                    || Vector2.Distance(npc.Center, Projectile.Center) >= CastRange) {
                    continue;
                }
                Vector2 vel = (npc.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 7f;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                    ModContent.ProjectileType<DiscipleSigilBolt>(), damage, 1f, Projectile.owner, 0, i);
                sent++;
            }
        }
    }
}
