using CalamityOverhaul.Content.Industrials.MaterialFlow;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers
{
    internal class ElectricMinRocket : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/ElectricMinRocket";
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 1;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 1, 0, 0);
            Item.rare = ItemRarityID.Quest;
            Item.createTile = ModContent.TileType<ElectricMinRocketTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 600;
        }
    }

    internal class ElectricMinRocketHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/ElectricMinRocket";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
        }

        public override void AI() {
            Owner.Center = Projectile.Center;
            Owner.CWR().RideElectricMinRocket = true;
            Projectile.velocity = new Vector2(Owner.velocity.X / 6, -6);
            Projectile.rotation = Projectile.velocity.ToRotation();
            Vector2 spanPos = Projectile.position - new Vector2(Owner.direction * 22, -20);
            //Dust.NewDust(spanPos, Projectile.width, Projectile.height, DustID.Smoke, 0, 6);
            spanPos = Projectile.Bottom - new Vector2(Owner.direction * 20, 60);
            PRT_LavaFire lavaFire = new PRT_LavaFire {
                Velocity = Projectile.velocity * -10,
                Position = spanPos,
                Scale = Main.rand.NextFloat(0.8f, 1.2f),
                maxLifeTime = 30,
                minLifeTime = 18,
                Color = Color.Gold
            };
            PRTLoader.AddParticle(lavaFire);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            return base.OnTileCollide(oldVelocity);
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode();
            if (Projectile.IsOwnedByLocalPlayer()) {
                Item item = new Item(ModContent.ItemType<ElectricMinRocket>());
                item.CWR().UEValue = Projectile.ai[0] - 200;
                Owner.QuickSpawnItem(Owner.FromObjectGetParent(), item);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            return false;
        }
    }

    internal class ElectricMinRocketTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/ElectricMinRocketTile";
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;

            AddMapEntry(new Color(67, 72, 81), CWRUtils.SafeGetItemName<ElectricMinRocket>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2xX);
            TileObjectData.newTile.Width = 2;
            TileObjectData.newTile.Height = 3;
            TileObjectData.newTile.Origin = new Point16(1, 2);
            TileObjectData.newTile.AnchorBottom = new AnchorData(
                AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide
                , TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16];
            TileObjectData.newTile.LavaDeath = false;

            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            if (TileProcessorLoader.AutoPositionGetTP<ElectricMinRocketTP>(i, j, out var tp) && tp.InDrop) {
                Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            }
            return false;
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool RightClick(int i, int j) {
            Player player = CWRUtils.TileFindPlayer(i, j);
            if (player != null) {
                if (TileProcessorLoader.AutoPositionGetTP<ElectricMinRocketTP>(i, j, out var tp)) {
                    if (tp.MachineData.UEvalue < 200) {
                        CombatText.NewText(tp.HitBox, Color.DimGray, "能量不足");
                        return false;
                    }
                    Projectile.NewProjectile(player.FromObjectGetParent(), player.Center, Vector2.Zero
                    , ModContent.ProjectileType<ElectricMinRocketHeld>(), 0, 0, player.whoAmI, tp.MachineData.UEvalue);
                    tp.InDrop = false;
                    WorldGen.KillTile(i, j);
                }
            }
            return base.RightClick(i, j);
        }
    }

    internal class ElectricMinRocketTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<ElectricMinRocketTile>();
        public override int TargetItem => ModContent.ItemType<ElectricMinRocket>();
        public bool InDrop = true;
        public override bool CanDrop => InDrop;
        public override float MaxUEValue => 600;
        public override void SetBattery() {
            InDrop = true;
            if (TrackItem == null) {
                MachineData.UEvalue = MaxUEValue;
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }
    }
}
