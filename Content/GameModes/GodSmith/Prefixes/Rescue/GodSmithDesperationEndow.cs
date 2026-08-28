using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Prefixes.Rescue
{
    /// <summary>
    /// 【救济池·爆发】破釜沉舟：覆盖减伤减击退的负词缀群（破损/糟糕/可怕/烦人/迟钝/损坏/劣质/羞耻/错乱/微弱），
    /// 残破的武器在逆境中怒吼，命中有几率炸出黑红怒焰。词缀越破，爆发越猛（档位反向：破损 = 1.0）。
    /// 低权重逆境彩蛋，期望收益补不满词缀亏空，是风味不是白嫖
    /// </summary>
    internal class GodSmithDesperationEndow : GodSmithEndow
    {
        /// <summary>触发几率（百分比）</summary>
        internal const int ProcChance = 18;

        /// <summary>爆发伤害占触发伤害比（最破档）</summary>
        internal const float BaseDamageRatio = 1.10f;

        //救济彩蛋：低权重
        public override float RollWeight => 0.5f;

        public override int[] CoveredPrefixes => [
            PrefixID.Broken, PrefixID.Terrible, PrefixID.Awful, PrefixID.Annoying, PrefixID.Dull,
            PrefixID.Damaged, PrefixID.Shoddy, PrefixID.Shameful, PrefixID.Deranged, PrefixID.Weak,
        ];

        //反向档位：伤害被砍得越狠，怒焰越旺
        public override float TierScaleFor(int prefixId) => prefixId switch {
            PrefixID.Broken => 1f,
            PrefixID.Terrible => 0.9f,
            PrefixID.Awful => 0.85f,
            PrefixID.Annoying => 0.8f,
            PrefixID.Dull => 0.75f,
            PrefixID.Damaged => 0.7f,
            PrefixID.Shoddy => 0.6f,
            PrefixID.Shameful => 0.6f,
            PrefixID.Deranged => 0.55f,
            _ => 0.4f,
        };

        protected override string EndowNameFallback => "Desperate Burst";

        protected override string EndowDescFallback =>
            "This battered weapon rages: hits have a {0}% chance to erupt for {1}% of that hit in an area";

        public override object[] DescFormatArgs(Item item)
            => [ProcChance, (BaseDamageRatio * 100f * TierScaleFor(item.prefix)).ToString("0.#")];

        public override void OnHitNPC(Player player, Item sourceItem, Projectile sourceProj, NPC target,
            in NPC.HitInfo hit, int damageDone, float tierScale) {
            if (target.friendly || target.type == NPCID.TargetDummy) {
                return;
            }
            //怒焰自身命中不再触发，防连环
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GodSmithDesperationBurst>()) {
                return;
            }
            //权威 roll 只在 owner 端
            if (player.whoAmI != Main.myPlayer || Main.rand.Next(100) >= ProcChance) {
                return;
            }
            int damage = Math.Clamp((int)(damageDone * BaseDamageRatio * tierScale), 8, 700);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithDesperationEndow"), target.Center,
                Vector2.Zero, ModContent.ProjectileType<GodSmithDesperationBurst>(), damage, 5f, player.whoAmI);
        }
    }

    /// <summary>黑红怒焰：先黑烟压场再赤焰炸开，余烬带着火星回落</summary>
    internal class GodSmithDesperationBurst : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>扩张终末半径（像素）</summary>
        internal const float MaxRadius = 110f;

        private float LifeRatio => 1f - Projectile.timeLeft / 20f;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 20;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.aiStyle = 0;
        }

        public override void AI() {
            if (Projectile.timeLeft == 19 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.6f, Pitch = -0.3f }, Projectile.Center);
                for (int i = 0; i < 8; i++) {
                    Dust smoke = Dust.NewDustPerfect(Projectile.Center,
                        DustID.Smoke, Main.rand.NextVector2Circular(3f, 3f), 170, Color.Black, 1.6f);
                    smoke.noGravity = true;
                }
            }
            float radius = MaxRadius * (float)Math.Sqrt(LifeRatio);
            int size = (int)(radius * 2f);
            if (size > Projectile.width) {
                Projectile.Resize(size, size);
            }
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * radius,
                        DustID.Torch, ang.ToRotationVector2() * 2.5f - Vector2.UnitY * 1.2f, 80, default, 1.4f);
                    dust.noGravity = true;
                }
            }
            Lighting.AddLight(Projectile.Center, 0.55f, 0.2f, 0.05f);
        }

        public override Color? GetAlpha(Color lightColor) => new Color(255, 90, 40, 0) * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float radius = MaxRadius * (float)Math.Sqrt(LifeRatio);
            float fade = 1f - LifeRatio;
            float scale = radius * 2.4f / tex.Width;
            //赤焰双层：深红外沿 + 橙亮内核
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(140, 20, 10, 0) * (0.6f * fade), 0f, origin, scale, 0);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(255, 140, 60, 0) * (0.45f * fade), 0f, origin, scale * 0.65f, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(30f, 30f),
                    DustID.Torch, -Vector2.UnitY * Main.rand.NextFloat(1f, 3f), 60, default, 1.2f);
                dust.noGravity = false;
            }
        }
    }
}
