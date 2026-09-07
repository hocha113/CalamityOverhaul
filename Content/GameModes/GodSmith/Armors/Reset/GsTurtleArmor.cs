using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 龟甲套 · 棘刺（近战坦）。单件沿用原版。<br/>
    /// 原版旗标清点：15% 减伤 / 2 倍反伤（thorns + turtleThorns）→ 原样补回；无删除项。<br/>
    /// 签名：受到伤害时向周围迸出 8 根棘刺（近战伤害），每根造成防御力 2 倍的伤害，每 3 秒一次
    /// </summary>
    internal class GsTurtleArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.TurtleHelmet];
        public override int BodyID => ItemID.TurtleScaleMail;
        public override int LegsID => ItemID.TurtleLeggings;
        public override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "15% damage reduction and melee attackers take double damage back; taking damage launches 8 spikes that each deal 2x your defense, once every 3 seconds";

        private const int SpikeCount = 8;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.endurance += 0.15f;
            player.thorns = Math.Max(player.thorns, 1f);
            player.turtleThorns = true;
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            if (player.whoAmI != Main.myPlayer || !state.TryUseCooldown(this, 180)) {
                return;
            }
            int defense = player.statDefense;
            int damage = Math.Max(10, defense * 2);
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.7f, Pitch = -0.2f }, player.Center);
            for (int i = 0; i < SpikeCount; i++) {
                Vector2 velocity = (MathHelper.TwoPi * i / SpikeCount + Main.rand.NextFloat(-0.15f, 0.15f)).ToRotationVector2()
                    * Main.rand.NextFloat(8f, 10f);
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithTurtleEndow"), player.Center, velocity,
                    ModContent.ProjectileType<GsTurtleSpikeProj>(), damage, 5f, player.whoAmI);
            }
        }
    }

    /// <summary>龟甲棘刺：借原版尖球贴图，径向迸出后受轻微重力下坠，可穿透一次，触地即碎</summary>
    internal class GsTurtleSpikeProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SpikyBall;

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 70;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.18f, 14f);
            Projectile.rotation += Projectile.velocity.X * 0.1f;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Stone,
                    Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1.5f, 1f));
                dust.noGravity = false;
            }
        }
    }
}
