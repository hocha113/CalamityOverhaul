using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Disciples
{
    /// <summary>
    /// 巴多罗买·真言(席位5)：真言揭示。
    /// 掷出剥示之刃，命中者护甲被临时剥离并显形(debuff，不改字段)
    /// </summary>
    internal class Bartholomew : BaseDisciple
    {
        public override int Seat => 5;

        private const float CastRange = 430f;

        protected override bool TryCast() => FindTarget() >= 0;

        protected override void ExecuteAbility() {
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.6f, Pitch = 0.6f }, Projectile.Center);
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            int target = FindTarget();
            if (target < 0) {
                return;
            }
            int damage = (int)(ElysiumPlayer.GetElysiumDamage(Owner) * 0.35f);
            Vector2 vel = (Main.npc[target].Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 9f;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                ModContent.ProjectileType<DiscipleSigilBolt>(), damage, 2f, Projectile.owner, 2, target);
        }

        private int FindTarget() {
            int found = -1;
            float closest = CastRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || !npc.CanBeChasedBy(Projectile) || npc.HasBuff<TruthRevealDebuff>()) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < closest) {
                    closest = dist;
                    found = i;
                }
            }
            return found;
        }
    }
}
