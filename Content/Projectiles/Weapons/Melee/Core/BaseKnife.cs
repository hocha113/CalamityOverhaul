using CalamityMod;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core
{
    internal class BaseKnife : BaseSwing
    {
        public override string Texture => CWRConstant.Placeholder3;
        public Item Item => Owner.ActiveItem();
        public ref int SwingIndex => ref Item.CWR().SwingIndex;
        public virtual int TargetID => ItemID.None;
        public override Texture2D TextureValue => TargetID == ItemID.None ? CWRUtils.GetT2DValue(Texture) : TextureAssets.Item[TargetID].Value;
        public SwingDataStruct SwingData = new SwingDataStruct();
        public SwingAITypeEnum SwingAIType;
        public enum SwingAITypeEnum {
            None = 0,
            UpAndDown,
        }
        public sealed override void SetSwingProperty() {
            ownerOrientationLock = true;
            Projectile.extraUpdates = 4;
            SetKnifeProperty();
            CWRUtils.SafeLoadItem(TargetID);
        }

        public virtual void SetKnifeProperty() { }

        public override void Initialize() {
            maxSwingTime = Item.useTime;
            SwingData.maxSwingTime = maxSwingTime;
            toProjCoreMode = Projectile.width / 2f;
            if (++SwingIndex > 1) {
                SwingIndex = 0;
            }
        }

        public override void SwingAI() {
            switch (SwingAIType) {
                case SwingAITypeEnum.None:
                    SwingBehavior(SwingData);
                    break;
                case SwingAITypeEnum.UpAndDown:
                    SwingDataStruct swingData = SwingData;
                    if (SwingIndex == 0) {
                        SwingBehavior(swingData);
                    }
                    else {
                        swingData.starArg += 120;
                        swingData.baseSwingSpeed *= -1;
                        SwingBehavior(swingData);
                    }
                    break;
            }
        }
    }
}
