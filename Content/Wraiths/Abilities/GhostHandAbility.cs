using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Projectiles;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Abilities
{
    internal sealed class GhostHandAbility : WraithPassiveAbility
    {
        internal const string Key = "GhostHand";
        internal const float GrabRange = 300f;
        /// <summary>同时探出的枯手上限，按范围内猎物数量伸手</summary>
        internal const int MaxHands = 3;

        /// <summary>可抓判定：boss 亦可被攥住；按目标体型放宽中心距，巨物不因判定尺寸免疫</summary>
        internal static bool CanGrab(NPC npc, Vector2 center) {
            if (!npc.CanBeChasedBy() || npc.HasBuff<Buffs.GhostGripDebuff>()) {
                return false;
            }
            float range = GrabRange + MathF.Min(npc.width, npc.height) * 0.5f;
            return Vector2.DistanceSquared(npc.Center, center) < range * range;
        }

        /// <summary>范围内可猎目标数（含已被攥住者，防手数抖动），封顶 <see cref="MaxHands"/></summary>
        internal static int CountPrey(Vector2 center) {
            int count = 0;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }
                float range = GrabRange + MathF.Min(npc.width, npc.height) * 0.5f;
                if (Vector2.DistanceSquared(npc.Center, center) < range * range
                    && ++count >= MaxHands) {
                    break;
                }
            }
            return count;
        }

        public override void Update(in WraithAbilityContext context) {
            Player player = context.Player;
            if (player == null || player.whoAmI != Main.myPlayer || !player.active || player.dead) {
                return;
            }
            int projectileType = ModContent.ProjectileType<GhostHandProj>();

            //盘点已存在的手位，缺哪个补哪个
            Span<bool> slotTaken = stackalloc bool[MaxHands];
            int handCount = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.owner != player.whoAmI || proj.type != projectileType
                    || proj.ModProjectile is not GhostHandProj hand) {
                    continue;
                }
                if (hand.HandSlot >= 0 && hand.HandSlot < MaxHands) {
                    slotTaken[hand.HandSlot] = true;
                }
                handCount++;
            }

            int desired = Math.Clamp(CountPrey(player.Center), 1, MaxHands);
            if (handCount >= desired) {
                return;
            }

            for (int slot = 0; slot < MaxHands && handCount < desired; slot++) {
                if (slotTaken[slot]) {
                    continue;
                }
                int index = Projectile.NewProjectile(
                    player.GetSource_Misc("CWRWraith_GhostHandAbility"),
                    player.Center,
                    Vector2.Zero,
                    projectileType,
                    0,
                    0f,
                    player.whoAmI,
                    0f,
                    0f,
                    context.Mastery);
                if (index >= 0 && index < Main.maxProjectiles
                    && Main.projectile[index].ModProjectile is GhostHandProj spawned) {
                    spawned.AssignHandSlot(slot);
                }
                slotTaken[slot] = true;
                handCount++;
            }
        }
    }
}
