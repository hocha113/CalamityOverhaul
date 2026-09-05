using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Prefixes.Ranged
{
    /// <summary>
    /// 【远程系·跳弹】流转跳弹：与穿云曳光同池竞争，覆盖同一组远程词缀。
    /// 弹药命中后迸出一粒灼铜色跳弹，铛的一声跃向另一名敌人
    /// </summary>
    internal class GodSmithRicochetEndow : GodSmithEndow
    {
        /// <summary>跳弹伤害占触发伤害比（顶级档）</summary>
        internal const float BaseDamageRatio = 0.40f;

        /// <summary>触发冷却（帧）</summary>
        internal const int CooldownFrames = 48;

        /// <summary>跳弹索敌半径</summary>
        internal const float BounceRange = 400f;

        //与穿云曳光同池，权重略低
        public override float RollWeight => 0.8f;

        public override int[] CoveredPrefixes => [
            PrefixID.Unreal, PrefixID.Deadly, PrefixID.Sighted, PrefixID.Powerful,
            PrefixID.Staunch, PrefixID.Rapid, PrefixID.Hasty, PrefixID.Intimidating,
        ];

        public override float TierScaleFor(int prefixId) => prefixId switch {
            PrefixID.Unreal => 1f,
            PrefixID.Deadly => 0.85f,
            PrefixID.Sighted => 0.75f,
            PrefixID.Powerful => 0.75f,
            PrefixID.Staunch => 0.7f,
            PrefixID.Rapid => 0.65f,
            PrefixID.Hasty => 0.65f,
            _ => 0.55f,
        };

        protected override string EndowNameFallback => "Wild Ricochet";

        protected override string EndowDescFallback =>
            "Projectile hits ricochet a copper spark into another foe for {0}% of that hit";

        public override object[] DescFormatArgs(Item item)
            => [(BaseDamageRatio * 100f * TierScaleFor(item.prefix)).ToString("0.#")];

        public override void OnHitNPC(Player player, Item sourceItem, Projectile sourceProj, NPC target,
            in NPC.HitInfo hit, int damageDone, float tierScale) {
            //只走弹幕命中路径（远程武器的本分）
            if (sourceProj == null || target.friendly || target.type == NPCID.TargetDummy) {
                return;
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            //负键冷却：避开重铸饰品效果的正键约定
            if (!player.GetModPlayer<GodSmithPlayer>().TryUseCooldown(
                -ModContent.ProjectileType<GodSmithRicochetBolt>(), CooldownFrames)) {
                return;
            }
            NPC next = FindNext(target);
            if (next == null) {
                return;
            }
            int damage = Math.Clamp((int)(damageDone * BaseDamageRatio * tierScale), 6, 600);
            Vector2 dir = (next.Center - target.Center).SafeNormalize(Vector2.UnitX);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithRicochetEndow"), target.Center,
                dir * 7f, ModContent.ProjectileType<GodSmithRicochetBolt>(), damage, 1f,
                player.whoAmI, next.whoAmI);
        }

        private static NPC FindNext(NPC hitTarget) {
            NPC best = null;
            float bestDist = BounceRange;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.whoAmI == hitTarget.whoAmI || !npc.CanBeChasedBy()) {
                    continue;
                }
                float dist = npc.Distance(hitTarget.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }
    }

    /// <summary>灼铜跳弹：带着金属铛声弹开，先飘后咬加速命中</summary>
    internal class GodSmithRicochetBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bullet;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 50;
            Projectile.tileCollide = false;
            Projectile.aiStyle = 0;
        }

        public override void AI() {
            if (Projectile.timeLeft == 49 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item52 with { Volume = 0.5f, Pitch = 0.35f }, Projectile.Center);
            }
            NPC target = Main.npc[(int)Projectile.ai[0]];
            if (target.active && target.CanBeChasedBy()) {
                Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                float speed = MathHelper.Lerp(7f, 17f, 1f - Projectile.timeLeft / 50f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want * speed, 0.2f);
            }
            else {
                Projectile.velocity *= 0.95f;
                Projectile.alpha = Math.Min(255, Projectile.alpha + 20);
            }
            //原版子弹贴图竖向朝上，旋转补四分之一圈
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.4f, Pitch = 0.5f }, Projectile.Center);
        }
    }
}
