using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 血腥砍刀重铸。材质：锈口屠刀。签名行为：①命中附加流血并记刻，同一目标第三刻触发放血
    /// ②放血那一击加伤 40%，爆出血雾并为你回复 2 点生命 ③命中血尘垫底加锈红火星，音色钝沉
    /// </summary>
    internal class GsBloodyMachete : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.BloodyMachete;

        internal override int BoomerProjType => ModContent.ProjectileType<GsBloodyMacheteProj>();

        internal override float DamageMul => 1.10f;   //万圣节掉落的弱势镖，补一成底伤

        protected override string GsDescFallback =>
            "Hits inflict Bleeding and carve a tally; the third tally on one target lets the blood loose\n" +
            "The bloodletting strike deals 40% bonus damage and heals you for 2 life\n" +
            "Right click while it flies: command it to dash toward your cursor";
    }

    /// <summary>屠刀镖体：放血三刻</summary>
    internal class GsBloodyMacheteProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.BloodyMachete;

        protected override Color GlowColor => new(200, 55, 60);

        protected override Color TrailColor => new(150, 35, 45);

        protected override SoundStyle HitSound => SoundID.Tink with { Volume = 0.45f, Pitch = -0.4f };

        /// <summary>目标 whoAmI → 刻数（owner 判定端本地量）</summary>
        private readonly Dictionary<int, int> tally = [];

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            tally.TryGetValue(target.whoAmI, out int cur);
            if (cur + 1 >= 3) {
                modifiers.FinalDamage *= 1.4f;
            }
        }

        protected override void OnHitEffects(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Bleeding, 240);
            tally.TryGetValue(target.whoAmI, out int cur);
            cur++;
            if (cur >= 3) {
                tally[target.whoAmI] = 0;
                //放血：owner 端回 2 点血（自己客户端写自己的生命，逐帧差量自动同步）
                Player owner = Owner;
                if (owner.whoAmI == Main.myPlayer && owner.statLife < owner.statLifeMax2) {
                    owner.Heal(2);
                }
                BloodBurstFX(target);
            }
            else {
                tally[target.whoAmI] = cur;
            }
        }

        private void BloodBurstFX(NPC target) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.5f, Pitch = -0.3f }, target.Center);
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, GlowColor, 0.4f)?.Configure(12, 0.85f);
            for (int i = 0; i < 10; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood,
                    Main.rand.NextVector2Circular(4.5f, 4.5f), 60, default, Main.rand.NextFloat(1.1f, 1.7f));
                d.noGravity = Main.rand.NextBool(3);
            }
        }

        protected override void HitBurstFX(NPC target, NPC.HitInfo hit) {
            //锈红火星 + 原版血尘垫底
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Circular(3.5f, 3.5f), GlowColor,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(10, 16));
            }
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood,
                    Main.rand.NextVector2Circular(3f, 3f), 80, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        protected override void FlightFX(Player owner) {
            base.FlightFX(owner);
            //飞行滴血
            if (PhaseTimer % 6 == 0 && !Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                    new Vector2(0f, Main.rand.NextFloat(0.5f, 1.5f)), 100, default, Main.rand.NextFloat(0.8f, 1.1f));
                d.noGravity = false;
            }
        }
    }
}
