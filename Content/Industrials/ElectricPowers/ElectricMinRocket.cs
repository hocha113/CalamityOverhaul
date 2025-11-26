using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers
{
    internal class ElectricMinRocket : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/ElectricMinRocket";
        [VaultLoaden(CWRConstant.Asset + "ElectricPowers/ElectricMinRocketGlow")]
        public static Asset<Texture2D> Glow = null;
        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 40;
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

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 0) {
                Item.createTile = ModContent.TileType<ElectricMinRocketTile>();
            }
            else {
                Item.createTile = -1;
            }
            return player.CountProjectilesOfID<ElectricMinRocketHeld>() == 0;
        }

        public override bool ConsumeItem(Player player) => player.altFunctionUse == 0;

        public override bool? UseItem(Player player) {
            if (player.altFunctionUse == 2) {
                if (Item.CWR().UEValue <= 0) {
                    CombatText.NewText(player.Hitbox, Color.DimGray, CWRLocText.Instance.EnergyShortage.Value);
                    SoundEngine.PlaySound(SoundID.MenuClose);
                    Item.createTile = ModContent.TileType<ElectricMinRocketTile>();
                    return true;
                }

                Projectile.NewProjectile(player.FromObjectGetParent(), player.Center, Vector2.Zero
                , ModContent.ProjectileType<ElectricMinRocketHeld>(), 0, 0, player.whoAmI, Item.CWR().UEValue);
                Item.TurnToAir();
                return false;
            }
            return null;
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition
                , null, Color.White, rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }
    }

    internal class ElectricMinRocketHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/ElectricMinRocket";
        private ref float UEValue => ref Projectile.ai[0];
        private bool ControlDown {
            get => Projectile.ai[1] == 1;
            set => Projectile.ai[1] = value ? 1 : 0;
        }
        public override void SetDefaults() => Projectile.width = Projectile.height = 32;
        public override void AI() {
            if (Projectile.IsOwnedByLocalPlayer()) {
                if (ControlDown != Owner.controlDown) {
                    Projectile.netUpdate = true;
                }

                ControlDown = Owner.controlDown;

                if (Main.keyState[Keys.Space] == KeyState.Down) {
                    Projectile.ai[2] = 1f;
                    Projectile.netUpdate = true;
                    Projectile.Kill();
                    return;
                }
            }

            if (ControlDown) {
                Projectile.ai[2] = 1f;
            }
            else {
                Projectile.ai[2] = 0f;
            }

            //玩家飞行控制
            Owner.noFallDmg = true;
            Owner.gravity = 0f;
            Owner.maxFallSpeed = 100f;

            Projectile.timeLeft = 2;
            Owner.Center = Projectile.Center;
            Owner.CWR().RideElectricMinRocket = true;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, new Vector2(Owner.velocity.X / 3, ControlDown ? 2 : -6), 0.1f);
            Projectile.rotation = Projectile.velocity.ToRotation();
            Owner.fullRotation = Projectile.velocity.X / 4f;
            Owner.fullRotationOrigin = Owner.Size / 2;
            //卸载掉玩家的所有钩爪
            Owner.RemoveAllGrapplingHooks();
            //卸载掉玩家的所有坐骑
            Owner.mount.Dismount(Owner);

            if (++Projectile.localAI[0] > 12) {
                SoundEngine.PlaySound(SoundID.Item24 with { Pitch = -0.2f }, Projectile.Center);
                Projectile.localAI[0] = 0;
            }

            if (Projectile.position.Y < 800 || UEValue <= 0) {
                Projectile.Kill();
            }

            if (!VaultUtils.isServer) {
                Vector2 spanPos = Owner.Center - new Vector2(Owner.direction * 20, 0).RotatedBy(Owner.fullRotation);
                PRT_LavaFire lavaFire = new PRT_LavaFire {
                    Velocity = new Vector2(Projectile.velocity.X * -6, MathHelper.Max(6f, Projectile.velocity.Y * -6)),
                    Position = spanPos,
                    Scale = Main.rand.NextFloat(0.8f, 1.2f),
                    maxLifeTime = 18,
                    minLifeTime = 12,
                    Color = Color.Gold
                };
                PRTLoader.AddParticle(lavaFire);
            }

            UEValue -= 0.02f;

            UEValue = MathHelper.Clamp(UEValue, 0, 600);
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.ai[2] == 0) {
                Projectile.Explode();
                SpawnDust();
            }

            Owner.CWR().RideElectricMinRocketRecoverStateTime = 30;

            if (Projectile.IsOwnedByLocalPlayer()) {
                Item item = new Item(ModContent.ItemType<ElectricMinRocket>());
                item.CWR().UEValue = Projectile.ai[0];
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

        public override bool PreDraw(ref Color lightColor) {
            Vector2 drawPos = Owner.GetPlayerStabilityCenter() + new Vector2(0, -60) - Main.screenPosition;
            int uiBarByWidthSengs = (int)(CWRAsset.BarFull.Value.Width * (UEValue / 600f));
            //绘制温度相关的图像
            Rectangle fullRec = new Rectangle(0, 0, uiBarByWidthSengs, CWRAsset.BarFull.Value.Height);
            Main.spriteBatch.Draw(CWRAsset.BarTop.Value, drawPos, null, Color.White, 0, CWRAsset.BarTop.Size() / 2, 1, SpriteEffects.None, 0);
            Main.spriteBatch.Draw(CWRAsset.BarFull.Value, drawPos + new Vector2(10, 0), fullRec, Color.White, 0, CWRAsset.BarTop.Size() / 2, 1, SpriteEffects.None, 0);

            if (Main.keyState.PressingShift()) {
                string textContent = (((int)UEValue) + "/" + ((int)600f) + "UE").ToString();
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(textContent);
                Utils.DrawBorderStringFourWay(Main.spriteBatch, FontAssets.MouseText.Value, textContent
                            , drawPos.X - textSize.X / 2 + 18, drawPos.Y, Color.White, Color.Black, new Vector2(0.3f), 0.6f);
            }
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
            Player player = Main.LocalPlayer;
            if (player == null) {
                return false;
            }

            if (!TileProcessorLoader.AutoPositionGetTP<ElectricMinRocketTP>(i, j, out var tp)) {
                return false;
            }

            if (tp.MachineData.UEvalue <= 0) {
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
                if (tile.WallType == WallID.SapphireGemspark) {
                    MachineData.UEvalue = MaxUEValue;
                }
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }
}
