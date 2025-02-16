using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.TileModules;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.CompressorUIs
{
    internal class CompressorUI : UIHandle, ICWRLoader
    {
        public static CompressorUI Instance => UIHandleLoader.GetUIHandleOfType<CompressorUI>();
        private static bool _active;
        public override bool Active {
            get => false;// _active;//没做完，先藏起来
            set {
                _active = value;
                if (compressorEntity.compressorUIInstance != null) {
                    conversionList = compressorEntity.compressorUIInstance.conversionList;
                }
                else {
                    CompressorUI newCompressor = new CompressorUI();
                    newCompressor.LoadenConversionList();
                    compressorEntity.compressorUIInstance = newCompressor;
                }
            }
        }
        public CompressorTP compressorEntity;
        public List<ItemConversion> conversionList = [];
        public bool onDrag;
        public Vector2 dragOffsetPos;
        public static int Weith => 400;
        public static int Height => 640;
        void ICWRLoader.SetupData() => Instance.LoadenConversionList();
        public void LoadenConversionList() {
            conversionList = [];

            ItemConversion itemConversion = new ItemConversion();
            itemConversion.TargetItem = new Item(ModContent.ItemType<DecayParticles>());
            itemConversion.Level = 0;
            conversionList.Add(itemConversion);

            itemConversion = new ItemConversion();
            itemConversion.TargetItem = new Item(ModContent.ItemType<DecaySubstance>());
            itemConversion.Level = 1;
            conversionList.Add(itemConversion);

            itemConversion = new ItemConversion();
            itemConversion.TargetItem = new Item(ModContent.ItemType<DissipationSubstance>());
            itemConversion.Level = 2;
            conversionList.Add(itemConversion);

            itemConversion = new ItemConversion();
            itemConversion.TargetItem = new Item(ModContent.ItemType<SpectralMatter>());
            itemConversion.Level = 3;
            conversionList.Add(itemConversion);

            itemConversion = new ItemConversion();
            itemConversion.TargetItem = new Item(ModContent.ItemType<InfinityCatalyst>());
            itemConversion.Level = 4;
            conversionList.Add(itemConversion);

            LinkConversion();
        }

        public void LinkConversion() {
            for (int i = 0; i < conversionList.Count - 1; i++) {
                conversionList[i].NextConversion = conversionList[i + 1];
            }
        }

        public CompressorUI Clone() {
            CompressorUI reset = new CompressorUI();
            reset.conversionList = [];
            foreach (var conversion in conversionList) {
                reset.conversionList.Add(conversion.Clone());
            }
            return reset;
        }

        public override void Update() {
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, Weith, Height);
            Rectangle mouseHit = new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1);
            hoverInMainPage = UIHitBox.Intersects(mouseHit);
            if (hoverInMainPage) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Held && !onDrag) {
                    if (!onDrag) {
                        dragOffsetPos = DrawPosition - MousePosition;
                    }
                    onDrag = true;
                }
            }

            if (onDrag) {
                player.mouseInterface = true;
                DrawPosition = MousePosition + dragOffsetPos;
                if (keyLeftPressState == KeyPressState.Released) {
                    onDrag = false;
                }
            }

            for (int i = 0; i < conversionList.Count; i++) {
                ItemConversion itemConversion = conversionList[i];
                itemConversion.DrawPosition = DrawPosition + new Vector2(22, i * (ItemConversion.Height * 2) + 22);
                itemConversion.Update();
            }
        }

        public void TPUpdate() {
            for (int i = 0; i < conversionList.Count; i++) {
                ItemConversion itemConversion = conversionList[i];
                itemConversion.TPUpdate();
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, DrawPosition, Weith, Height
                    , Color.AliceBlue, Color.Azure * 0.8f, 1);
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.Placeholder_White.Value, 4, DrawPosition, Weith, Height
                    , Color.AliceBlue * 0, Color.AliceBlue * 0.8f, 1);
            for (int i = 0; i < conversionList.Count; i++) {
                ItemConversion itemConversion = conversionList[i];
                itemConversion.Draw(spriteBatch);
            }
        }
    }
}
