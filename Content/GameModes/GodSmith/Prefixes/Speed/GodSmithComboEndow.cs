using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Prefixes.Speed
{
    /// <summary>
    /// 【攻速系·连击】连击迸发：覆盖攻速词缀群（灵巧/迅速/急促/急速/轻快/狂乱/敏捷），
    /// 连续命中累积连势，第五击在目标处炸出琥珀色迸发环。
    /// 连势是攻击方端本地量（私有 ModPlayer），换武器或手停即散
    /// </summary>
    internal class GodSmithComboEndow : GodSmithEndow
    {
        /// <summary>引爆所需连击数</summary>
        internal const int FullCombo = 5;

        /// <summary>迸发伤害占触发伤害比（顶级档）</summary>
        internal const float BaseDamageRatio = 0.75f;

        public override int[] CoveredPrefixes => [
            PrefixID.Agile, PrefixID.Quick, PrefixID.Hasty, PrefixID.Rapid,
            PrefixID.Light, PrefixID.Frenzying, PrefixID.Nimble,
        ];

        public override float TierScaleFor(int prefixId) => prefixId switch {
            PrefixID.Agile => 1f,
            PrefixID.Quick => 0.9f,
            PrefixID.Hasty => 0.85f,
            PrefixID.Rapid => 0.8f,
            PrefixID.Light => 0.8f,
            PrefixID.Frenzying => 0.7f,
            _ => 0.55f,
        };

        protected override string EndowNameFallback => "Combo Surge";

        protected override string EndowDescFallback =>
            "Consecutive hits build momentum; the {0}th strike erupts for {1}% of that hit in an area";

        public override object[] DescFormatArgs(Item item)
            => [FullCombo, (BaseDamageRatio * 100f * TierScaleFor(item.prefix)).ToString("0.#")];

        public override void OnHitNPC(Player player, Item sourceItem, Projectile sourceProj, NPC target,
            in NPC.HitInfo hit, int damageDone, float tierScale) {
            if (target.friendly || target.type == NPCID.TargetDummy) {
                return;
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            //迸发环自身命中不计连击，防自循环
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GodSmithComboBurstRing>()) {
                return;
            }
            int stacks = player.GetModPlayer<GodSmithComboEndowPlayer>().AddHit(player.HeldItem?.type ?? 0);
            if (!VaultUtils.isServer && stacks > 1) {
                //连势读数：层数越高火花越多，只攻击方本端可见
                for (int i = 0; i < stacks; i++) {
                    Dust dust = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(14f, 14f),
                        DustID.AmberBolt, -Vector2.UnitY * 1.2f, 80, default, 0.8f);
                    dust.noGravity = true;
                }
            }
            if (stacks < FullCombo) {
                return;
            }
            player.GetModPlayer<GodSmithComboEndowPlayer>().ResetCombo();
            int damage = Math.Clamp((int)(damageDone * BaseDamageRatio * tierScale), 8, 700);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithComboEndow"), target.Center,
                Vector2.Zero, ModContent.ProjectileType<GodSmithComboBurstRing>(), damage, 3f, player.whoAmI);
        }
    }

    /// <summary>连势记账：连击数带时限窗口，换武器立刻清零</summary>
    internal class GodSmithComboEndowPlayer : ModPlayer
    {
        /// <summary>连击窗口（帧）</summary>
        internal const int ComboWindow = 180;

        private int stacks;
        private int weaponType;
        private uint expire;

        /// <summary>登记一次命中并返回当前连击数</summary>
        internal int AddHit(int heldType) {
            if (heldType != weaponType || Main.GameUpdateCount >= expire) {
                stacks = 0;
                weaponType = heldType;
            }
            stacks++;
            expire = Main.GameUpdateCount + ComboWindow;
            return stacks;
        }

        internal void ResetCombo() => stacks = 0;
    }

    /// <summary>连击迸发环：琥珀色气浪一鼓而散，火星贴着环沿蹦跳</summary>
    internal class GodSmithComboBurstRing : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>扩张终末半径（像素）</summary>
        internal const float MaxRadius = 120f;

        private float LifeRatio => 1f - Projectile.timeLeft / 18f;

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
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
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.55f, Pitch = 0.4f }, Projectile.Center);
                for (int i = 0; i < 14; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.AmberBolt,
                        Main.rand.NextVector2Circular(6f, 6f), 60, default, 1.2f);
                    dust.noGravity = true;
                }
            }
            float radius = MaxRadius * (float)Math.Sqrt(LifeRatio);
            int size = (int)(radius * 2f);
            if (size > Projectile.width) {
                Projectile.Resize(size, size);
            }
            Lighting.AddLight(Projectile.Center, 0.5f, 0.35f, 0.1f);
        }

        public override Color? GetAlpha(Color lightColor) => new Color(255, 190, 90, 0) * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float radius = MaxRadius * (float)Math.Sqrt(LifeRatio);
            float fade = 1f - LifeRatio;
            float scale = radius * 2.4f / tex.Width;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(180, 90, 20, 0) * (0.55f * fade), 0f, origin, scale, 0);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(255, 220, 130, 0) * (0.4f * fade), 0f, origin, scale * 0.7f, 0);
            return false;
        }
    }
}
