using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Disciples
{
    /// <summary>
    /// 达太·奇迹(席位9)：奇迹显现。
    /// 长冷却抽取一种奇迹降下：治愈之雨/迅捷之风/守护之环/圣雷显现，
    /// 抽取由主人端决定，演出色各异
    /// </summary>
    internal class Thaddaeus : BaseDisciple
    {
        public override int Seat => 9;

        private enum Miracle
        {
            HealingRain,   //治愈之雨
            SwiftWind,     //迅捷之风
            GuardingRing,  //守护之环
            HolyThunder,   //圣雷显现
        }

        protected override bool TryCast() {
            //主人满血且无敌人时不浪费奇迹
            if (Owner.statLife < Owner.statLifeMax2) {
                return true;
            }
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy(Projectile)
                    && Vector2.Distance(npc.Center, Owner.Center) < 700f) {
                    return true;
                }
            }
            return false;
        }

        protected override void ExecuteAbility() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            Miracle miracle = (Miracle)Main.rand.Next(4);
            //圣雷需要目标，没有就换成守护
            int thunderTarget = -1;
            if (miracle == Miracle.HolyThunder) {
                thunderTarget = FindNearestEnemy();
                if (thunderTarget < 0) {
                    miracle = Miracle.GuardingRing;
                }
            }

            switch (miracle) {
                case Miracle.HealingRain:
                    Owner.statLife += 25;
                    if (Owner.statLife > Owner.statLifeMax2) {
                        Owner.statLife = Owner.statLifeMax2;
                    }
                    Owner.HealEffect(25, true);
                    Owner.AddBuff(BuffID.Regeneration, 480);
                    SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.8f, Pitch = 0.3f }, Owner.Center);
                    MiracleBurst(new Color(180, 255, 200));
                    break;
                case Miracle.SwiftWind:
                    Owner.AddBuff(BuffID.Swiftness, 480);
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.8f, Pitch = 0.6f }, Owner.Center);
                    MiracleBurst(new Color(190, 235, 255));
                    break;
                case Miracle.GuardingRing:
                    Owner.AddBuff(BuffID.Ironskin, 480);
                    Owner.AddBuff(BuffID.Endurance, 480);
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.8f, Pitch = 0.2f }, Owner.Center);
                    MiracleBurst(new Color(230, 220, 170));
                    break;
                case Miracle.HolyThunder:
                    int damage = (int)(ElysiumPlayer.GetElysiumDamage(Owner) * 1.2f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                        ModContent.ProjectileType<JamesChainStrike>(), damage, 4f, Projectile.owner, thunderTarget);
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.9f, Pitch = 0.1f }, Projectile.Center);
                    MiracleBurst(new Color(238, 220, 255));
                    break;
            }
        }

        /// <summary>奇迹显现的通用迸发：达太身上绽开对应色的星环</summary>
        private void MiracleBurst(Color color) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center
                    , angle.ToRotationVector2() * Main.rand.NextFloat(2.5f, 5.5f)
                    , color, Main.rand.NextFloat(0.6f, 1f))?.Configure(false, Main.rand.Next(14, 22));
            }
        }

        private int FindNearestEnemy() {
            int found = -1;
            float closest = 620f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || !npc.CanBeChasedBy(Projectile)) {
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
