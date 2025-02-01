using CalamityMod.Projectiles.Boss;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Rogue
{
    /// <summary>
    /// 沙匕
    /// </summary>
    internal class SandDagger : ModItem
    {
        public override string Texture => CWRConstant.Item + "Rogue/SandDagger";
        public override void SetDefaults() {
            Item.width = 48;
            Item.height = 48;
            Item.damage = 16;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 18;
            Item.knockBack = 3f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(0, 0, 50, 15);
            Item.shoot = ModContent.ProjectileType<SandDaggerThrow>();
            Item.shootSpeed = 10f;
            Item.DamageType = CWRLoad.RogueDamageClass;
        }
    }

    internal class SandDaggerThrow : BaseThrowable
    {
        public override string Texture => CWRConstant.Item + "Rogue/SandDaggerProj";
        private bool onTIle;
        private bool onSend;
        private float tileRot;
        private readonly static int[] SandTileIDs = new int[] {
            TileID.Sand, TileID.Ebonsand, TileID.Pearlsand, TileID.Crimsand
            , TileID.HardenedSand, TileID.CorruptHardenedSand, TileID.CrimsonHardenedSand };
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }
        public override void SetThrowable() {
            Projectile.width = Projectile.height = 8;
            HandOnTwringMode = -20;
        }

        public override void FlyToMovementAI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (++Projectile.ai[2] > 20 && !onTIle) {
                Projectile.velocity.Y += 0.3f;
                Projectile.velocity.X *= 0.99f;
            }
            if (onTIle) {
                Projectile.timeLeft = 2;
                Projectile.rotation = tileRot;
                if (Projectile.ai[2] <= 40) {
                    Projectile.velocity *= 0.6f;
                }

                if (onSend && Projectile.ai[2] > 40) {
                    Projectile.velocity = new Vector2(0, -13);
                    Projectile.rotation = Projectile.velocity.ToRotation();
                }

                if (++Projectile.ai[2] >= 60) {
                    Projectile.Kill();
                }
            }

        }

        public override bool PreThrowOut() {
            SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
            Projectile.velocity = UnitToMouseV * 17.5f;
            Projectile.tileCollide = true;
            Projectile.penetrate = 1;
            if (stealthStrike && Projectile.ai[2] == 0) {
                Projectile.damage *= 2;
                Projectile.ArmorPenetration = 10;
                Projectile.penetrate = 6;
                Projectile.extraUpdates = 3;
                Projectile.scale = 1.5f;
            }
            Projectile.localAI[0] = 1;
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (onSend) {
                Projectile.Explode();
                for (int i = 0; i < 3; i++) {
                    Vector2 velocity = new Vector2(0, -6).RotatedByRandom(0.6f);
                    int proj = Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, velocity
                    , ModContent.ProjectileType<DesertScourgeSpit>(), Projectile.damage / 2, Projectile.knockBack, Projectile.owner);
                    Main.projectile[proj].hostile = false;
                    Main.projectile[proj].friendly = true;
                    NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, proj);
                }
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (!onTIle) {
                Projectile.Center += Projectile.velocity;
                Projectile.velocity = Vector2.Zero;
                tileRot = Projectile.rotation;
                Vector2 tilePos = CWRUtils.WEPosToTilePos(Projectile.Bottom);
                if (SandTileIDs.Contains(CWRUtils.GetTile(tilePos + new Vector2(0, 0)).TileType)
                || SandTileIDs.Contains(CWRUtils.GetTile(tilePos + new Vector2(1, 0)).TileType)
                || SandTileIDs.Contains(CWRUtils.GetTile(tilePos + new Vector2(-1, 0)).TileType)
                || SandTileIDs.Contains(CWRUtils.GetTile(tilePos + new Vector2(0, 1)).TileType)
                || SandTileIDs.Contains(CWRUtils.GetTile(tilePos + new Vector2(0, -1)).TileType)) {
                    onSend = true;
                }
                Collision.HitTiles(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);
                SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
                onTIle = true;
            }
            return false;
        }

        public override void DrawThrowable(Color lightColor) {
            if (Projectile.localAI[0] == 1) {
                for (int k = 0; k < Projectile.oldPos.Length; k++) {
                    Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2;
                    Color color = lightColor * (float)((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length / 2);
                    Main.EntitySpriteDraw(TextureValue, drawPos, null, color
                    , Projectile.rotation + (MathHelper.PiOver2 + OffsetRoting) * (Projectile.velocity.X > 0 ? 1 : -1)
                    , TextureValue.Size() / 2, Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically, 0);
                }
            }

            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation + (MathHelper.PiOver2 + OffsetRoting) * (Projectile.velocity.X > 0 ? 1 : -1)
                , TextureValue.Size() / 2, Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically, 0);
        }
    }
}
