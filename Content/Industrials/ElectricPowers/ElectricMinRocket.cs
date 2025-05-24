using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.Audio;
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
            Item.value = Item.buyPrice(0, 1, 10, 0);
            Item.rare = ItemRarityID.Orange;
            Item.createTile = ModContent.TileType<ElectricMinRocketTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 600;
        }
    }

    internal class ElectricMinRocketHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/ElectricMinRocket";
        public override void SetDefaults() => Projectile.width = Projectile.height = 32;
        public override void AI() {
            Owner.Center = Projectile.Center;
            Owner.CWR().RideElectricMinRocket = true;
            Projectile.velocity = new Vector2(Owner.velocity.X / 6, -6);
            Projectile.rotation = Projectile.velocity.ToRotation();
            Owner.fullRotation = Projectile.velocity.X / 2f;
            Owner.fullRotationOrigin = Owner.Size / 2;
            //卸载掉玩家的所有钩爪
            Owner.RemoveAllGrapplingHooks();
            //卸载掉玩家的所有坐骑
            Owner.mount.Dismount(Owner);

            if (++Projectile.localAI[0] > 12) {
                SoundEngine.PlaySound(SoundID.Item24 with { Pitch = -0.2f }, Projectile.Center);
                Projectile.localAI[0] = 0;
            }

            if (Projectile.position.Y < 800) {
                Projectile.Kill();
            }

            if (!VaultUtils.isServer) {
                Vector2 spanPos = Owner.Center - new Vector2(Owner.direction * 20, 0).RotatedBy(Owner.fullRotation);
                PRT_LavaFire lavaFire = new PRT_LavaFire {
                    Velocity = Projectile.velocity * -6,
                    Position = spanPos,
                    Scale = Main.rand.NextFloat(0.8f, 1.2f),
                    maxLifeTime = 18,
                    minLifeTime = 12,
                    Color = Color.Gold
                };
                PRTLoader.AddParticle(lavaFire);
            }
        }

        public override void OnKill(int timeLeft) {
            Owner.fullRotation = 0;
            Projectile.Explode();
            SpawnDust();
            if (Projectile.IsOwnedByLocalPlayer()) {
                Item item = new Item(ModContent.ItemType<ElectricMinRocket>());
                item.CWR().UEValue = Projectile.ai[0] - 200;
                if (item.CWR().UEValue < 0) {
                    item.CWR().UEValue = 0;
                }
                Owner.QuickSpawnItem(Owner.FromObjectGetParent(), item);
            }
        }

        private void SpawnDust() {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 20; i++) {
                int idx = Dust.NewDust(Projectile.Center, Projectile.width, Projectile.height, DustID.Smoke, 0f, 0f, 100, default, 2f);
                Main.dust[idx].velocity *= 3f;
                if (Main.rand.NextBool()) {
                    Main.dust[idx].scale = 0.5f;
                    Main.dust[idx].fadeIn = 1f + Main.rand.NextFloat(1f);
                }
            }
            for (int i = 0; i < 40; i++) {
                int idx = Dust.NewDust(Projectile.Center, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, default, 3f);
                Main.dust[idx].noGravity = true;
                Main.dust[idx].velocity *= 5f;

                idx = Dust.NewDust(Projectile.Center, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, default, 2f);
                Main.dust[idx].velocity *= 2f;
            }
            Vector2 source = Projectile.Center - new Vector2(24f, 24f);
            int goreAmt = 3;
            for (int goreIndex = 0; goreIndex < goreAmt; goreIndex++) {
                float velocityMult = (goreIndex < goreAmt / 3) ? 0.66f : (goreIndex >= 2 * goreAmt / 3 ? 1f : 0.33f);
                SpawnGore(source, velocityMult, new Vector2(1f, 1f));
                SpawnGore(source, velocityMult, new Vector2(-1f, 1f));
                SpawnGore(source, velocityMult, new Vector2(1f, -1f));
                SpawnGore(source, velocityMult, new Vector2(-1f, -1f));
            }
        }

        private void SpawnGore(Vector2 position, float velocityMultiplier, Vector2 velocityOffset) {
            int type = Main.rand.Next(61, 64);
            int goreIndex = Gore.NewGore(Projectile.GetSource_Death(), position, default, type, 1f);
            Gore gore = Main.gore[goreIndex];
            gore.velocity *= velocityMultiplier;
            gore.velocity += velocityOffset;
        }

        public override bool PreDraw(ref Color lightColor) => false;
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

            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<ElectricMinRocket>());

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

        public override void MouseOver(int i, int j) => Main.LocalPlayer.SetMouseOverByTile(ModContent.ItemType<ElectricMinRocket>());

        public override bool RightClick(int i, int j) {
            Player player = CWRUtils.TileFindPlayer(i, j);
            if (player == null) {
                return false;
            }

            if (!TileProcessorLoader.AutoPositionGetTP<ElectricMinRocketTP>(i, j, out var tp)) {
                return false;
            }

            if (tp.MachineData.UEvalue < 200) {
                CombatText.NewText(tp.HitBox, Color.DimGray, CWRLocText.Instance.EnergyShortage.Value);
                SoundEngine.PlaySound(SoundID.MenuClose);
                return false;
            }

            Projectile.NewProjectile(player.FromObjectGetParent(), player.Center, Vector2.Zero
            , ModContent.ProjectileType<ElectricMinRocketHeld>(), 0, 0, player.whoAmI, tp.MachineData.UEvalue);

            tp.InDrop = false;
            tp.SendData();

            WorldGen.KillTile(i, j);
            if (!VaultUtils.isSinglePlayer) {
                NetMessage.SendTileSquare(player.whoAmI, i, j);
            }

            return base.RightClick(i, j);
        }
    }

    internal class ElectricMinRocketTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<ElectricMinRocketTile>();
        public override int TargetItem => ModContent.ItemType<ElectricMinRocket>();
        public override bool ReceivedEnergy => true;
        public bool InDrop = true;
        public override bool CanDrop => InDrop;
        public override float MaxUEValue => 600;
        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(InDrop);
        }
        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            InDrop = reader.ReadBoolean();
        }

        public override void SetBattery() {
            InDrop = true;
            if (TrackItem == null) {
                Tile tile = Framing.GetTileSafely(Position);
                //为什么是165？因为这是自然生成时会使用的背景墙，用这个小区别来防止玩家放置的火箭被影响
                if (tile.WallType == 165) {
                    MachineData.UEvalue = MaxUEValue;
                }
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }
}
