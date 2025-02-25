using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.CompressorUIs
{
    internal class ItemConversion : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        public static int Weith => 64;
        public static int Height => 64;
        public static int barWeith => 220;
        public static int barHeight => 40;
        public int MaxCharge => 40 * (Level + 1);
        public int Level { get; set; }
        public float Charge;
        public Item TargetItem { get; set; }
        internal ItemAccept ContainerLeft = new ItemAccept();
        internal ItemAccept ContainerRight = new ItemAccept();
        internal ArrowLock ArrowLock = new ArrowLock();
        internal ItemConversion NextConversion;
        public ItemConversion Clone() {
            ItemConversion reset = new ItemConversion();
            reset.Level = Level;
            reset.Charge = Charge;
            reset.TargetItem = TargetItem.Clone();
            reset.ContainerLeft.Item = ContainerLeft.Item.Clone();
            reset.ContainerRight.Item = ContainerRight.Item.Clone();
            return reset;
        }
        public override void Update() {
            ContainerLeft.DrawPosition = DrawPosition;
            ContainerRight.DrawPosition = DrawPosition + new Vector2(barWeith + Weith, 0);
            ContainerLeft.FaterConversion = this;
            ContainerRight.FaterConversion = this;
            ContainerLeft.IsRight = false;
            ContainerRight.IsRight = true;

            ContainerLeft.Update();
            if (ContainerLeft.Item.type > ItemID.None) {
                if (Charge < MaxCharge) {
                    Charge++;
                }
                else {
                    SoundEngine.PlaySound(SoundID.Grab);
                    if (--ContainerLeft.Item.stack <= 0) {
                        ContainerLeft.Item.TurnToAir();
                    }
                    if (ContainerRight.Item.type != TargetItem.type) {
                        ContainerRight.Item = TargetItem.Clone();
                    }
                    else {
                        ContainerRight.Item.stack++;
                    }

                    if (NextConversion != null && !ArrowLock.IsLock) {
                        if (NextConversion.ContainerLeft.Item.type != ContainerRight.Item.type && ContainerRight.Item.type > ItemID.None) {
                            NextConversion.ContainerLeft.Item = ContainerRight.Item.Clone();
                        }
                        else {
                            NextConversion.ContainerLeft.Item.stack += ContainerRight.Item.stack;
                        }

                        ContainerRight.Item.TurnToAir();
                    }
                    Charge = 0;
                }
            }
            else {
                Charge = MathHelper.Lerp(Charge, 0, 0.2f);
            }
            Charge = MathHelper.Clamp(Charge, 0.0001f, MaxCharge);
            ContainerRight.Update();

            if (NextConversion != null) {
                ArrowLock.DrawPosition = DrawPosition + new Vector2(Weith + barWeith / 2 - Weith / 2, Height);
                ArrowLock.Update();
            }
        }

        public void TPUpdate() {
            if (ContainerLeft.Item.type > ItemID.None) {
                if (Charge < MaxCharge) {
                    Charge++;
                }
                else {
                    if (--ContainerLeft.Item.stack <= 0) {
                        ContainerLeft.Item.TurnToAir();
                    }
                    if (ContainerRight.Item.type != TargetItem.type) {
                        ContainerRight.Item = TargetItem.Clone();
                    }
                    else {
                        ContainerRight.Item.stack++;
                    }

                    if (NextConversion != null && !ArrowLock.IsLock) {
                        if (NextConversion.ContainerLeft.Item.type != ContainerRight.Item.type) {
                            NextConversion.ContainerLeft.Item = ContainerRight.Item.Clone();
                        }
                        else {
                            NextConversion.ContainerLeft.Item.stack += ContainerRight.Item.stack;
                        }

                        ContainerRight.Item.TurnToAir();
                    }
                    Charge = 0;
                }
            }
            else {
                Charge = MathHelper.Lerp(Charge, 0, 0.2f);
            }
            Charge = MathHelper.Clamp(Charge, 0.0001f, MaxCharge);
            ContainerRight.Update();
        }

        public override void Draw(SpriteBatch spriteBatch) {
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, DrawPosition + new Vector2(Weith, (Height - barHeight) / 2)
                , barWeith, barHeight, Color.AliceBlue * 0.8f, Color.Azure * 0.2f, 1);
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, DrawPosition + new Vector2(Weith, (Height - barHeight) / 2)
                , (int)(barWeith * (Charge / MaxCharge)), barHeight, Color.AliceBlue * 0, Color.Azure * 0.8f, 1);

            ContainerLeft.Draw(spriteBatch);
            ContainerRight.Draw(spriteBatch);

            if (NextConversion != null) {
                ArrowLock.Draw(spriteBatch);
            }
        }
    }
}
