using Microsoft.Xna.Framework.Graphics;
using System;
using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
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

    /// <summary>紫电奥能弧：三道紫电在目标身上炸开又收束，弧向逐帧抖动（AI 内掷随机，绘制只读）</summary>
    internal class GodSmithOverloadArc : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.LightBeam;

        private float Seed => Projectile.whoAmI * 2.399f;

        private float LifeRatio => 1f - Projectile.timeLeft / 18f;

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
                for (int i = 0; i < 10; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch,
                        Main.rand.NextVector2Circular(5f, 5f), 100, default, 1.3f);
                    dust.noGravity = true;
                }
            }
            //抖动量在 AI 里掷好存 localAI，绘制端只读（绘制禁 Main.rand）
            Projectile.localAI[0] = Main.rand.NextFloat(-0.22f, 0.22f);
            Lighting.AddLight(Projectile.Center, 0.4f, 0.15f, 0.55f);
        }

        public override Color? GetAlpha(Color lightColor) => new Color(200, 120, 255, 0) * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float grow = LifeRatio < 0.3f ? LifeRatio / 0.3f : 1f - (LifeRatio - 0.3f) / 0.7f;
            for (int i = 0; i < 3; i++) {
                float rot = Seed + i * (MathHelper.TwoPi / 3f) + Projectile.localAI[0];
                float len = (1.1f + 0.5f * i % 2) * (0.4f + grow);
                Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                    new Color(90, 20, 160, 0) * (0.8f * grow), rot, origin, new Vector2(1.1f, len * 1.15f), 0);
                Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                    new Color(225, 160, 255, 0) * grow, rot, origin, new Vector2(0.5f, len), 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch,
                    Main.rand.NextVector2Circular(3f, 3f), 100, default, 1.1f);
                dust.noGravity = true;
            }
        }
    }
}
