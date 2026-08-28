using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Prefixes.Magic
{
    /// <summary>
    /// 【魔力系·回流】魔力回流：覆盖省魔词缀群（神话/巧匠/秘术/天界/熟练/狂躁），
    /// 命中时有几率从敌人身上剥出一缕蓝魔力，蜿蜒飞回持有者补魔。
    /// 回流缕是跨端可见实体，补魔结算只在 owner 端
    /// </summary>
    internal class GodSmithManaRefluxEndow : GodSmithEndow
    {
        /// <summary>触发几率（百分比）</summary>
        internal const int ProcChance = 25;

        /// <summary>顶级档单次返还魔力</summary>
        internal const int BaseManaBack = 12;

        /// <summary>触发冷却（帧）</summary>
        internal const int CooldownFrames = 30;

        public override int[] CoveredPrefixes => [
            PrefixID.Mythical, PrefixID.Masterful, PrefixID.Mystic,
            PrefixID.Celestial, PrefixID.Adept, PrefixID.Manic,
        ];

        public override float TierScaleFor(int prefixId) => prefixId switch {
            PrefixID.Mythical => 1f,
            PrefixID.Masterful => 0.9f,
            PrefixID.Mystic => 0.75f,
            PrefixID.Celestial => 0.7f,
            PrefixID.Adept => 0.6f,
            _ => 0.55f,
        };

        protected override string EndowNameFallback => "Mana Reflux";

        protected override string EndowDescFallback =>
            "Hits have a {0}% chance to pull a wisp of mana back to you, restoring {1} mana";

        public override object[] DescFormatArgs(Item item)
            => [ProcChance, Math.Max(1, (int)(BaseManaBack * TierScaleFor(item.prefix)))];

        public override void OnHitNPC(Player player, Item sourceItem, Projectile sourceProj, NPC target,
            in NPC.HitInfo hit, int damageDone, float tierScale) {
            if (target.friendly || target.type == NPCID.TargetDummy) {
                return;
            }
            //权威 roll 只在 owner 端，结果随回流缕实体同步
            if (player.whoAmI != Main.myPlayer || Main.rand.Next(100) >= ProcChance) {
                return;
            }
            if (!player.GetModPlayer<GodSmithPlayer>().TryUseCooldown(
                -ModContent.ProjectileType<GodSmithManaRefluxWisp>(), CooldownFrames)) {
                return;
            }
            int manaBack = Math.Max(1, (int)(BaseManaBack * tierScale));
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithManaRefluxEndow"), target.Center,
                Main.rand.NextVector2Circular(3f, 3f), ModContent.ProjectileType<GodSmithManaRefluxWisp>(),
                0, 0f, player.whoAmI, manaBack);
        }
    }

    /// <summary>魔力回流缕：一缕蓝白魔力先飘散再折返，加速咬回持有者掌心；
    /// ai[0] = 返还魔力量，补魔只在 owner 端结算</summary>
    internal class GodSmithManaRefluxWisp : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.aiStyle = 0;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            //先飘散 12 帧，再折返加速飞向持有者
            if (Projectile.timeLeft < 108) {
                Vector2 want = (owner.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                float speed = MathHelper.Lerp(4f, 16f, 1f - Projectile.timeLeft / 108f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want * speed, 0.16f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, 0.1f, 0.25f, 0.5f);
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.BlueCrystalShard,
                    -Projectile.velocity * 0.1f, 120, default, 0.9f);
                dust.noGravity = true;
            }
            //触及持有者：补魔（owner 端结算）并散作蓝尘
            if (Projectile.Hitbox.Intersects(owner.Hitbox)) {
                if (Projectile.owner == Main.myPlayer) {
                    int manaBack = Math.Max(1, (int)Projectile.ai[0]);
                    owner.statMana = Math.Min(owner.statManaMax2, owner.statMana + manaBack);
                    owner.ManaEffect(manaBack);
                }
                Projectile.Kill();
            }
        }

        public override Color? GetAlpha(Color lightColor) => new Color(120, 190, 255, 0) * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float stretch = 0.16f + Projectile.velocity.Length() * 0.03f;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(30, 70, 160, 0) * Projectile.Opacity, Projectile.rotation, origin,
                new Vector2(stretch * 1.6f, 0.26f), 0);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(170, 220, 255, 0) * Projectile.Opacity, Projectile.rotation, origin,
                new Vector2(stretch, 0.13f), 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item28 with { Volume = 0.4f, Pitch = 0.4f }, Projectile.Center);
            for (int i = 0; i < 7; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.BlueCrystalShard,
                    Main.rand.NextVector2Circular(2.5f, 2.5f), 100, default, 1f);
                dust.noGravity = true;
            }
        }
    }
}
