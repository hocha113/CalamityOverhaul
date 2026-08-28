using Microsoft.Xna.Framework.Graphics;
using System;
using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Prefixes.Accessory
{
    /// <summary>
    /// 【饰品·近战速】狂性迸发：覆盖饰品近战速度词缀链（暴力/勇猛/鲁莽/狂野），
    /// 近战命中累积狂性，叠满八层迸出血色刃环。刃环命中同帧压制，防自喂
    /// </summary>
    internal class GodSmithViolentSurgeEndow : GodSmithEndow
    {
        /// <summary>迸发所需狂性层数</summary>
        internal const int FullStacks = 8;

        /// <summary>刃环伤害占触发伤害比（顶级档）</summary>
        internal const float BaseDamageRatio = 0.55f;

        public override int[] CoveredPrefixes => [
            PrefixID.Violent, PrefixID.Intrepid, PrefixID.Rash, PrefixID.Wild,
        ];

        public override float TierScaleFor(int prefixId) => prefixId switch {
            PrefixID.Violent => 1f,
            PrefixID.Intrepid => 0.75f,
            PrefixID.Rash => 0.5f,
            _ => 0.25f,
        };

        protected override string EndowNameFallback => "Violent Surge";

        protected override string EndowDescFallback =>
            "Melee hits build fury; at {0} stacks it erupts as a crimson blade ring dealing {1}% of that hit";

        public override object[] DescFormatArgs(Item item)
            => [FullStacks, (BaseDamageRatio * 100f * TierScaleFor(item.prefix)).ToString("0.#")];

        public override void OnWearerHitNPC(Item accessory, Player player, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile, float tierScale) {
            //只认近战类伤害，狂性属于抡刀人
            if (target.friendly || target.type == NPCID.TargetDummy
                || !hit.DamageType.CountsAsClass(DamageClass.Melee)) {
                return;
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            GodSmithViolentSurgeEndowPlayer fury = player.GetModPlayer<GodSmithViolentSurgeEndowPlayer>();
            //刃环自己的命中不叠狂性
            if (fury.SuppressedThisFrame) {
                return;
            }
            int stacks = fury.AddStack();
            if (!VaultUtils.isServer && stacks > FullStacks / 2) {
                //过半后见红：狂性读数只攻击方本端可见
                Dust dust = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(16f, 20f),
                    DustID.CrimsonTorch, -Vector2.UnitY * 1.5f, 100, default, 0.9f);
                dust.noGravity = true;
            }
            if (stacks < FullStacks) {
                return;
            }
            fury.ResetStacks();
            int damage = Math.Clamp((int)(damageDone * BaseDamageRatio * tierScale), 6, 500);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithViolentSurgeEndow"), target.Center,
                Vector2.Zero, ModContent.ProjectileType<GodSmithViolentArc>(), damage, 4f, player.whoAmI);
        }
    }

    /// <summary>狂性记账：层数带时限窗口 + 刃环同帧压制</summary>
    internal class GodSmithViolentSurgeEndowPlayer : ModPlayer
    {
        /// <summary>狂性窗口（帧），手停即散</summary>
        internal const int StackWindow = 240;

        private int stacks;
        private uint expire;
        private uint suppressFrame;

        internal bool SuppressedThisFrame => suppressFrame == Main.GameUpdateCount;

        internal void SuppressNow() => suppressFrame = Main.GameUpdateCount;

        internal int AddStack() {
            if (Main.GameUpdateCount >= expire) {
                stacks = 0;
            }
            expire = Main.GameUpdateCount + StackWindow;
            return ++stacks;
        }

        internal void ResetStacks() => stacks = 0;
    }

    /// <summary>血色刃环：狂性炸成一圈血刃气浪，环沿溅血珠</summary>
    internal class GodSmithViolentArc : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>扩张终末半径（像素）</summary>
        internal const float MaxRadius = 110f;

        private float LifeRatio => 1f - Projectile.timeLeft / 16f;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 16;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 16;
            Projectile.aiStyle = 0;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => Main.player[Projectile.owner].GetModPlayer<GodSmithViolentSurgeEndowPlayer>().SuppressNow();

        public override void AI() {
            if (Projectile.timeLeft == 15 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);
            }
            float radius = MaxRadius * (float)Math.Sqrt(LifeRatio);
            int size = (int)(radius * 2f);
            if (size > Projectile.width) {
                Projectile.Resize(size, size);
            }
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 5; i++) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * radius,
                        DustID.Blood, ang.ToRotationVector2() * 2.5f, 60, default, 1.3f);
                    dust.noGravity = false;
                }
            }
            Lighting.AddLight(Projectile.Center, 0.45f, 0.08f, 0.1f);
        }

        public override Color? GetAlpha(Color lightColor) => new Color(255, 60, 70, 0) * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float radius = MaxRadius * (float)Math.Sqrt(LifeRatio);
            float fade = 1f - LifeRatio;
            float scale = radius * 2.4f / tex.Width;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(120, 10, 20, 0) * (0.6f * fade), 0f, origin, scale, 0);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(255, 90, 100, 0) * (0.4f * fade), 0f, origin, scale * 0.7f, 0);
            return false;
        }
    }
}
