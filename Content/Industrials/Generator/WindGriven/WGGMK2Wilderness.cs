﻿using CalamityMod.Items.Materials;
using CalamityOverhaul.Common;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.Generator.WindGriven
{
    internal class WGGMK2Wilderness : ModItem
    {
        public override string Texture => CWRConstant.Asset + "Generator/WGGMK2WildernessItem";
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 1, 20, 0);
            Item.rare = ItemRarityID.Green;
            Item.createTile = ModContent.TileType<WGGMK2WildernessTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 800;
        }
    }

    internal class WGGMK2WildernessTile : BaseGeneratorTile
    {
        public override string Texture => CWRConstant.Asset + "Generator/WGGMK2WildernessTile";
        public override int GeneratorTP => TileProcessorLoader.GetModuleID<WGGMK2WildernessTP>();
        public override int GeneratorUI => 0;
        public override int TargetItem => ModContent.ItemType<WGGMK2Wilderness>();
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;
            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<WGGMK2Wilderness>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 18;
            TileObjectData.newTile.Origin = new Point16(1, 16);
            TileObjectData.newTile.CoordinateHeights = [
                16, 16, 16, 16, 16, 16, 16, 16, 16
                , 16, 16, 16, 16, 16, 16, 16, 16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }
    }

    internal class WGGMK2WildernessTP : BaseWindGrivenTP
    {
        public override int TargetTileID => ModContent.TileType<WGGMK2WildernessTile>();
        public override float MaxUEValue => 800f;
        public override bool CanDrop => false;
        public override int TargetItem => ModContent.ItemType<WGGMK2Wilderness>();
        [VaultLoaden(CWRConstant.Asset + "Generator/MK2BladeWilderness")]
        internal static Asset<Texture2D> MK2BladeWilderness { get; private set; }
        public override void SetWindGriven() {
            baseRotSpeed = 0.012f;
            energyConversion = 0.12f;
            baseSoundPith = 0.5f;
            baseVolume = 0.85f;
        }

        public override void GeneratorKill() {
            if (!VaultUtils.isServer) {
                Player player = VaultUtils.FindClosestPlayer(CenterInWorld, 1200);
                if (player != null) {
                    player.CWR().UnderstandWindGrivenMK2 = true;
                    int text = CombatText.NewText(HitBox, new Color(111, 247, 200), CWRLocText.Instance.WindGriven_Text2.Value, false);
                    Main.combatText[text].lifeTime = 300;
                }
            }

            if (VaultUtils.isClient) {
                return;
            }

            int dropNum = Main.rand.Next(8, 12);
            for (int i = 0; i < dropNum; i++) {
                DropItem(ModContent.ItemType<DubiousPlating>());
            }
            dropNum = Main.rand.Next(10, 15);
            for (int i = 0; i < dropNum; i++) {
                DropItem(ModContent.ItemType<MysteriousCircuitry>());
            }
            if (Main.rand.NextBool(5)) {
                DropItem(ModContent.ItemType<SuspiciousScrap>());
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            Texture2D blade = MK2BladeWilderness.Value;
            Vector2 drawPos = PosInWorld - Main.screenPosition + new Vector2(24, 28);
            Vector2 drawOrig = new Vector2(blade.Width / 2, blade.Height);
            for (int i = 0; i < 3; i++) {
                float drawRot = (MathHelper.TwoPi) / 3f * i + rotition;
                Color color = Lighting.GetColor(Position.ToPoint() + drawRot.ToRotationVector2().ToPoint());
                spriteBatch.Draw(blade, drawPos, null, color, drawRot, drawOrig, 1, SpriteEffects.None, 0);
            }
        }
    }
}
