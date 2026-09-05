using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Launchers
{
    /// <summary>
    /// 杰克南瓜灯发射器重铸：区域压制。滚地南瓜原版保留；本武器一切爆点
    /// 都留下 2.5 秒烛火场（踩踏持续伤害）
    /// </summary>
    internal class GsJackOLanternLauncher : GsLauncherScheme
    {
        public override int TargetItemID => ItemID.JackOLanternLauncher;

        protected override string GsDescFallback =>
            "Reforged: every blast leaves a candle field that scorches whoever stands in it";
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            LaunchRecoil(player, velocity, 1.3f);
            return null;
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.JackOLantern || !proj.IsOwnedByLocalPlayer()) {
                return;
            }
            //区域压制：一切爆点都点起烛火场
            Projectile.NewProjectile(proj.GetSource_FromThis(), proj.Center - new Vector2(0f, 8f),
                Vector2.Zero, ModContent.ProjectileType<GsCandleFieldProj>(),
                Math.Max(1, (int)(proj.damage * 0.25f)), 0f, proj.owner);
        }
    }

    /// <summary>
    /// 烛火场：爆点余烬燃成的一片低矮火毯，静止 2.5 秒，踩进来的敌人被持续灼烧。
    /// 判定用本地免疫（每目标约每半秒一跳）；借原版燃烧瓶火滩贴图按判定框拉伸一笔
    /// </summary>
    internal class GsCandleFieldProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.MolotovFire;

        public override void SetDefaults() {
            Projectile.width = 96;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire, 120);

        /// <summary>区域一笔：火滩贴图取首帧，按判定框拉伸画在中心，收尾 30 帧随 alpha 淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            int frames = Math.Max(1, Main.projFrames[ProjectileID.MolotovFire]);
            Rectangle src = new(0, 0, tex.Width, tex.Height / frames);
            Vector2 scale = new(Projectile.width / (float)src.Width, Projectile.height / (float)src.Height);
            float fade = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src, lightColor * fade,
                0f, src.Size() / 2f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
