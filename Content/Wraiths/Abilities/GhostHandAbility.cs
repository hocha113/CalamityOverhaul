using CalamityOverhaul.Content.Wraiths.Abilities.GhostRains;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Marks;
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
        /// <summary>手位数组容量，取雨中上限</summary>
        internal const int MaxHands = 5;
        /// <summary>常态同时探出的枯手上限</summary>
        internal const int BaseHands = 3;

        //====灵异叠加声明：规则绑定状态语义，谁发的湿/照都认====

        /// <summary>「雨里伸手」可达：淋湿的猎物改吃雨域半径，枯手自它头顶的雨线垂下。</summary>
        internal static readonly WraithSynergyRule RainReach = new() {
            Id = "GhostHand.RainReach",
            Trigger = WraithMark.Soaked,
            Channel = WraithSynergyChannel.ReachRule,
            Name = () => WraithCovenText.RainHandName,
            Note = () => WraithCovenText.RainHandNote,
            UiPriority = 30,
        };

        /// <summary>「雨里伸手」碾轧：湿手好使力，随湿印强度加重。</summary>
        internal static readonly WraithSynergyRule RainCrush = new() {
            Id = "GhostHand.RainCrush",
            Trigger = WraithMark.Soaked,
            Channel = WraithSynergyChannel.DamageAmp,
            Magnitude = power => MathHelper.Lerp(1.15f, 1.45f, power),
            Name = () => WraithCovenText.RainHandName,
            UiPriority = 29,
        };

        /// <summary>「雨里伸手」手位：盘上有鬼发湿印时，同时探出的手放宽到五只。</summary>
        internal static readonly WraithSynergyRule RainHandCap = new() {
            Id = "GhostHand.RainHandCap",
            Trigger = WraithMark.Soaked,
            Channel = WraithSynergyChannel.ReachRule,
            BoardScope = true,
            Name = () => WraithCovenText.RainHandName,
            UiPriority = 28,
        };

        /// <summary>「照见」索敌：灯照过的猎物在手看来近得多，隔着黑也摸得着。</summary>
        internal static readonly WraithSynergyRule LitSeek = new() {
            Id = "GhostHand.LitSeek",
            Trigger = WraithMark.Lit,
            Channel = WraithSynergyChannel.TargetBias,
            Magnitude = _ => 0.25f,
            Name = () => WraithCovenText.LanternHandName,
            Note = () => WraithCovenText.LanternHandNote,
            UiPriority = 20,
        };

        /// <summary>「雨里伸手」：盘上有鬼发湿印时手位放宽到 <see cref="MaxHands"/></summary>
        internal static int HandCap(WraithMark boardEmits)
            => (boardEmits & RainHandCap.Trigger) != 0 ? MaxHands : BaseHands;

        /// <summary>
        /// 可抓判定：boss 亦可被攥住；按目标体型放宽中心距，巨物不因判定尺寸免疫。<br/>
        /// 淋着雨的目标改吃雨域半径——雨落到哪，手就能从哪伸出来
        /// </summary>
        internal static bool CanGrab(NPC npc, Vector2 center, int owner = -1) {
            if (!npc.CanBeChasedBy() || npc.HasBuff<Buffs.GhostGripDebuff>()) {
                return false;
            }
            return Vector2.DistanceSquared(npc.Center, center) < GrabRangeSq(npc, owner);
        }

        private static float GrabRangeSq(NPC npc, int owner) {
            float baseRange = owner >= 0 && WraithSynergy.TriggersOn(RainReach, npc, owner)
                ? GhostRainStorm.Radius : GrabRange;
            float range = baseRange + MathF.Min(npc.width, npc.height) * 0.5f;
            return range * range;
        }

        /// <summary>范围内可猎目标数（含已被攥住者，防手数抖动），封顶 <paramref name="cap"/></summary>
        internal static int CountPrey(Vector2 center, int owner, int cap) {
            int count = 0;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }
                if (Vector2.DistanceSquared(npc.Center, center) < GrabRangeSq(npc, owner)
                    && ++count >= cap) {
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
            int cap = HandCap(context.BoardEmits);

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

            int desired = Math.Clamp(CountPrey(player.Center, player.whoAmI, cap), 1, cap);
            if (handCount >= desired) {
                return;
            }

            for (int slot = 0; slot < cap && handCount < desired; slot++) {
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
                    context.Revival);
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
