using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Prefixes.Offense
{
    /// <summary>
    /// 【伤害系·溢杀】溢杀连锁：与处决斩痕同池竞争，覆盖同一组通用伤害词缀。
    /// 击杀敌人时，溢出的伤害凝成血色电光跃向附近敌人，杀意在人群里传递
    /// </summary>
    internal class GodSmithOverkillEndow : GodSmithEndow
    {
        /// <summary>溢出伤害携带比例（顶级档）</summary>
        internal const float BaseOverkillRatio = 0.6f;

        /// <summary>附加的该击基础伤害比例（顶级档）</summary>
        internal const float BaseFlatRatio = 0.2f;

        /// <summary>连锁索敌半径</summary>
        internal const float ChainRange = 500f;

        //与处决斩痕同池，权重略低
        public override float RollWeight => 0.8f;

        public override int[] CoveredPrefixes => [
            PrefixID.Godly, PrefixID.Demonic, PrefixID.Ruthless, PrefixID.Superior,
            PrefixID.Sharp, PrefixID.Deadly2, PrefixID.Murderous, PrefixID.Pointy,
            PrefixID.Hurtful, PrefixID.Nasty,
        ];

        public override float TierScaleFor(int prefixId) => prefixId switch {
            PrefixID.Godly => 1f,
            PrefixID.Demonic => 0.95f,
            PrefixID.Ruthless => 0.85f,
            PrefixID.Superior => 0.75f,
            PrefixID.Sharp => 0.7f,
            PrefixID.Deadly2 => 0.7f,
            PrefixID.Murderous => 0.6f,
            PrefixID.Nasty => 0.5f,
            _ => 0.55f,
        };

        protected override string EndowNameFallback => "Overkill Cascade";

        protected override string EndowDescFallback =>
            "Kills send a crimson bolt to a nearby foe, carrying {0}% of the overkill plus {1}% of the hit";

        public override object[] DescFormatArgs(Item item) {
            float tier = TierScaleFor(item.prefix);
            return [(BaseOverkillRatio * 100f * tier).ToString("0.#"), (BaseFlatRatio * 100f * tier).ToString("0.#")];
        }

        public override void OnHitNPC(Player player, Item sourceItem, Projectile sourceProj, NPC target,
            in NPC.HitInfo hit, int damageDone, float tierScale) {
            //只在这一击真正打死目标时结算溢出
            if (target.friendly || target.type == NPCID.TargetDummy || target.life > 0) {
                return;
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            int overkill = Math.Max(0, -target.life);
            int damage = Math.Clamp((int)((overkill * BaseOverkillRatio + damageDone * BaseFlatRatio) * tierScale), 10, 900);
            NPC next = FindNext(target);
            if (next == null) {
                return;
            }
            Vector2 dir = (next.Center - target.Center).SafeNormalize(Vector2.UnitX);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithOverkillEndow"), target.Center,
                dir * 6f, ModContent.ProjectileType<GodSmithOverkillChainBolt>(), damage, 3f,
                player.whoAmI, next.whoAmI);
        }

        private static NPC FindNext(NPC dead) {
            NPC best = null;
            float bestDist = ChainRange;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.whoAmI == dead.whoAmI || !npc.CanBeChasedBy()) {
                    continue;
                }
                float dist = npc.Distance(dead.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }
    }

    /// <summary>血色连锁电光：一粒饱含杀意的血珠拖着电光，先散后咬，加速扑向下一个目标</summary>
    internal class GodSmithOverkillChainBolt : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.aiStyle = 0;
        }

        public override void AI() {
            if (Projectile.timeLeft == 59 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.45f, Pitch = 0.4f }, Projectile.Center);
            }
            //追踪既定目标；目标失效则直行淡出
            NPC target = Main.npc[(int)Projectile.ai[0]];
            if (target.active && target.CanBeChasedBy()) {
                Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                float speed = MathHelper.Lerp(6f, 19f, 1f - Projectile.timeLeft / 60f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want * speed, 0.18f);
            }
            else {
                Projectile.velocity *= 0.96f;
                Projectile.alpha = Math.Min(255, Projectile.alpha + 18);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, 0.45f, 0.05f, 0.08f);
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.CrimsonTorch,
                    -Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(0.6f, 0.6f), 120, default, 1.1f);
                dust.noGravity = true;
            }
        }

        public override Color? GetAlpha(Color lightColor) => new Color(255, 40, 60, 0) * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            //速度拉伸的双层血光弹体：宽暗芯 + 窄亮尾
            float stretch = 0.22f + Projectile.velocity.Length() * 0.045f;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(140, 8, 25, 0) * Projectile.Opacity, Projectile.rotation, origin,
                new Vector2(stretch * 1.6f, 0.3f), 0);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(255, 90, 110, 0) * Projectile.Opacity, Projectile.rotation, origin,
                new Vector2(stretch, 0.14f), 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.5f, Pitch = 0.2f }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.CrimsonTorch,
                    Main.rand.NextVector2Circular(4f, 4f), 100, default, 1.3f);
                dust.noGravity = true;
            }
        }
    }
}
