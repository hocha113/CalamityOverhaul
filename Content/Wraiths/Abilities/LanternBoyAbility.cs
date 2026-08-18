using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Marks;
using CalamityOverhaul.Content.Wraiths.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Abilities
{
    internal sealed class LanternBoyAbility : WraithPassiveAbility
    {
        internal const string Key = "LanternBoy";

        /// <summary>「照见」灯斩：攥住的靶子跑不掉，灯斩落得实，加重一成六。</summary>
        internal static readonly WraithSynergyRule GripSlash = new() {
            Id = "LanternBoy.GripSlash",
            Trigger = WraithMark.Gripped,
            Channel = WraithSynergyChannel.DamageAmp,
            Magnitude = _ => 1.6f,
            Name = () => WraithCovenText.LanternHandName,
            UiPriority = 19,
        };

        public override void Update(in WraithAbilityContext context)
            => EnsureController(in context);

        public override void OnComboBeat(in WraithAbilityContext context,
            in WraithComboBeatEvent beat) {
            LanternBoyProj controller = EnsureController(in context);
            controller?.PublishComboBeat(in beat);
        }

        private static LanternBoyProj EnsureController(in WraithAbilityContext context) {
            Player player = context.Player;
            if (player == null || player.whoAmI != Main.myPlayer || !player.active || player.dead) {
                return null;
            }

            LanternBoyProj existing = LanternBoyProj.Find(player.whoAmI);
            if (existing != null) {
                return existing;
            }

            int projectileType = ModContent.ProjectileType<LanternBoyProj>();
            int whoAmI = Projectile.NewProjectile(
                player.GetSource_Misc("CWRWraith_LanternBoyAbility"),
                player.MountedCenter,
                Vector2.Zero,
                projectileType,
                0,
                0f,
                player.whoAmI,
                0f,
                0f,
                context.Revival);
            LanternBoyProj created = whoAmI >= 0 && whoAmI < Main.maxProjectiles
                ? Main.projectile[whoAmI].ModProjectile as LanternBoyProj
                : null;
            created?.BindVessel(context.VesselItem);
            return created;
        }
    }
}
