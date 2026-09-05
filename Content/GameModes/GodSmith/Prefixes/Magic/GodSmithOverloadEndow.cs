using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Prefixes.Magic
{
    /// <summary>
    /// 【魔力系·过载】奥能过载：覆盖耗魔上浮的法系词缀群（暴怒/强烈/禁忌/无知/笨拙 Inept），
    /// 魔力充盈时命中引爆紫电奥能，替高昂的法力开销讨回利息
    /// </summary>
    internal class GodSmithOverloadEndow : GodSmithEndow
    {
        /// <summary>触发所需魔力比例</summary>
        internal const float ManaGate = 0.7f;

        /// <summary>过载伤害占触发伤害比（顶级档）</summary>
        internal const float BaseDamageRatio = 0.30f;

        /// <summary>触发冷却（帧）</summary>
        internal const int CooldownFrames = 45;

        public override int[] CoveredPrefixes => [
            PrefixID.Furious, PrefixID.Intense, PrefixID.Taboo, PrefixID.Ignorant, PrefixID.Inept,
        ];

        public override float TierScaleFor(int prefixId) => prefixId switch {
            PrefixID.Furious => 1f,
            PrefixID.Intense => 0.8f,
            PrefixID.Taboo => 0.7f,
            PrefixID.Ignorant => 0.55f,
            _ => 0.45f,
        };

        protected override string EndowNameFallback => "Arcane Overload";

        protected override string EndowDescFallback =>
            "While above {0}% mana, hits overload with violet arcs dealing {1}% of that hit";

        public override object[] DescFormatArgs(Item item)
            => [(int)(ManaGate * 100f), (BaseDamageRatio * 100f * TierScaleFor(item.prefix)).ToString("0.#")];

        public override void OnHitNPC(Player player, Item sourceItem, Projectile sourceProj, NPC target,
            in NPC.HitInfo hit, int damageDone, float tierScale) {
            if (target.friendly || target.type == NPCID.TargetDummy) {
                return;
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            //魔力闸：魔力见底时过载熄火
            if (player.statMana < player.statManaMax2 * ManaGate) {
                return;
            }
            if (!player.GetModPlayer<GodSmithPlayer>().TryUseCooldown(
                -ModContent.ProjectileType<GodSmithOverloadArc>(), CooldownFrames)) {
                return;
            }
            int damage = Math.Clamp((int)(damageDone * BaseDamageRatio * tierScale), 6, 500);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithOverloadEndow"), target.Center,
                Vector2.Zero, ModContent.ProjectileType<GodSmithOverloadArc>(), damage, 1f, player.whoAmI);
        }
    }

    /// <summary>紫电奥能弧：在目标身上炸开的短命奥能判定</summary>
    internal class GodSmithOverloadArc : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.LightBeam;

        public override void SetDefaults() {
            Projectile.width = 56;
            Projectile.height = 56;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 18;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
            Projectile.aiStyle = 0;
        }

        public override void AI() {
            if (Projectile.timeLeft == 17 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.5f, Pitch = -0.1f }, Projectile.Center);
            }
        }
    }
}
