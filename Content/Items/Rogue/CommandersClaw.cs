using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Rogue
{
    internal class CommandersClaw : ModItem
    {
        public override string Texture => CWRConstant.Item_Rogue + "CommandersClaw";
        public override void SetDefaults() {
            Item.width = Item.height = 52;
            Item.damage = 132;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useAnimation = Item.useTime = 39;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 7f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<CommandersClawThrow>();
            Item.shootSpeed = 17f;
            Item.DamageType = CWRLoad.RogueDamageClass;
            Item.rare = ItemRarityID.Purple;
            Item.value = Item.buyPrice(0, 1, 65, 0);
            Item.CWR().DeathModeItem = true;
        }
    }

    internal class CommandersClawThrow : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Rogue + "CommandersClawThrow";
        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 6;
            Projectile.timeLeft = 900;
            Projectile.aiStyle = 0;
            Projectile.DamageType = CWRLoad.RogueDamageClass;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 50;
            Projectile.extraUpdates = 1;
            Projectile.CWR().Viscosity = true;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            Projectile.spriteDirection = Projectile.velocity.X > 0 ? 1 : -1;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation + (Projectile.spriteDirection > 0 ? 0 : -MathHelper.PiOver2)
                , tex.Size() / 2, Projectile.scale, Projectile.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) { }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) { }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) { }

        public override void OnKill(int timeLeft) {
            CWRDust.SplashDust(Projectile, 121, DustID.FireworkFountain_Red, DustID.FireworkFountain_Red, 13, Color.White);
            Projectile.Explode();
        }
    }
}