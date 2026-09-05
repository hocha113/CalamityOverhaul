using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using System;
using Terraria;
using Terraria.Audio;
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

    /// <summary>血色连锁电光：一粒饱含杀意的血珠，先散后咬，加速扑向下一个目标</summary>
    internal class GodSmithOverkillChainBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SharpTears;

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
            //原版血刺贴图竖向朝上，旋转补四分之一圈
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.5f, Pitch = 0.2f }, Projectile.Center);
        }
    }
}
