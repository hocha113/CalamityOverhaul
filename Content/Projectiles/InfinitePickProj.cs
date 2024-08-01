using CalamityMod.Particles;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.CWRDamageTypes;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Projectiles
{
    internal class InfinitePickProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public List<int> dorpTypes = [];
        public override bool IsLoadingEnabled(Mod mod) {
            return !CWRServerConfig.Instance.AddExtrasContent ? false : base.IsLoadingEnabled(mod);
        }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.DamageType = EndlessDamageClass.Instance;
            Projectile.MaxUpdates = 13;
            Projectile.timeLeft = 30;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.hostile = false;
        }



        public override void AI() {
            Lighting.AddLight(Projectile.Center, Main.DiscoColor.ToVector3() * (Projectile.ai[0] == 1 ? 1.2f : 10));
            if (Projectile.ai[0] == 1 && !CWRUtils.isServer) {
                for (int i = 0; i < 8; i++) {
                    PRK_HeavenfallStar spark = new PRK_HeavenfallStar(Projectile.Center + CWRUtils.randVr(13), Projectile.velocity, false, 13, 1, CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(), HeavenfallLongbow.rainbowColors));
                    DRKLoader.AddParticle(spark);
                }
                Player own = Main.player[Projectile.owner];
                for (int x = 0; x < Projectile.width; x++) {
                    Vector2 pos = Projectile.position;
                    for (int y = 0; y < Projectile.height; y++) {
                        Vector2 tilePos = CWRUtils.WEPosToTilePos(pos + new Vector2(x, y));
                        Tile tile = CWRUtils.GetTile(tilePos);
                        if (tile.HasTile && tile.TileType != TileID.Cactus) {
                            int stye = TileObjectData.GetTileStyle(tile);
                            if (stye == -1)
                                stye = 0;

                            int dorptype = TileLoader.GetItemDropFromTypeAndStyle(tile.TileType, stye);
                            if (dorptype != 0)
                                dorpTypes.Add(dorptype);

                            if (tile.WallType != 0) {
                                if (CWRLoad.WallToItem.TryGetValue(tile.WallType, out int value))
                                    if (value != 0)
                                        dorpTypes.Add(value);
                            }

                            tile.LiquidAmount = 0;
                            tile.HasTile = false;
                            tile.WallType = 0;

                            CWRUtils.SafeSquareTileFrame(tilePos, tile);
                            if (Main.netMode != NetmodeID.SinglePlayer) {
                                NetMessage.SendTileSquare(own.whoAmI, x, y);
                            }
                        }
                    }
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Projectile.parent(), target.position + new Vector2(Main.rand.Next(-160, 160), -420), new Vector2(0, 13), ModContent.ProjectileType<InfiniteEnmgs>(), Projectile.damage / 2, 0, Projectile.owner);
            }
            for (int i = 0; i < 36; i++) {
                Color outerSparkColor = CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(), HeavenfallLongbow.rainbowColors);
                Vector2 vector = Main.rand.NextVector2Unit() * Main.rand.Next(77);
                float slp = Main.rand.NextFloat(0.5f, 0.9f);
                GeneralParticleHandler.SpawnParticle(new FlareShine(Projectile.Center + Main.rand.NextVector2Unit() * 13, vector, Color.White, outerSparkColor
                    , 0f, new Vector2(0.6f, 1f) * slp, new Vector2(1.5f, 2.7f) * slp, 20 + Main.rand.Next(6), 0f, 3f, 0f, Main.rand.Next(7) * 2));

                float scaleBoost = MathHelper.Clamp(Projectile.ai[1] * 0.005f, 0f, 2f);
                float outerSparkScale = 3.2f + scaleBoost;
                PRK_HeavenfallStar spark = new PRK_HeavenfallStar(Projectile.Center, vector, false, 7, outerSparkScale, outerSparkColor);
                DRKLoader.AddParticle(spark);

                Color innerSparkColor = CWRUtils.MultiStepColorLerp(Projectile.ai[1] % 30 / 30f, HeavenfallLongbow.rainbowColors);
                float innerSparkScale = 0.6f + scaleBoost;
                PRK_HeavenfallStar spark2 = new PRK_HeavenfallStar(Projectile.Center, vector, false, 7, innerSparkScale, innerSparkColor);
                DRKLoader.AddParticle(spark2);
            }
        }

        public override void OnKill(int timeLeft) {
            Player own = Main.player[Projectile.owner];
            if (Main.myPlayer == own.whoAmI) {
                Item ball = new Item(ModContent.ItemType<DarkMatterBall>());
                DarkMatterBall darkMatterBall = (DarkMatterBall)ball.ModItem;
                if (dorpTypes.Count > 0) {
                    darkMatterBall.dorpTypes = dorpTypes;
                    own.QuickSpawnItem(own.parent(), darkMatterBall.Item, 1);
                }
            }
        }
    }
}
