using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core
{
    internal class BaseKnife : BaseSwing
    {
        public override string Texture => CWRConstant.Placeholder3;
        public override Texture2D TextureValue => TargetID == ItemID.None ? CWRUtils.GetT2DValue(Texture) : TextureAssets.Item[TargetID].Value;
        public SwingDataStruct SwingData = new SwingDataStruct();
        public SwingAITypeEnum SwingAIType;
        public enum SwingAITypeEnum
        {
            None = 0,
            UpAndDown,
        }
        public sealed override void SetSwingProperty() {
            Projectile.extraUpdates = 4;
            ownerOrientationLock = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10 * updateCount;
            SetKnifeProperty();
            CWRUtils.SafeLoadItem(TargetID);
        }

        protected void updaTrailTexture() => SwingSystem.trailTextures[Type] = CWRUtils.GetT2DAsset(trailTexturePath);
        protected void updaGradientTexture() => SwingSystem.gradientTextures[Type] = CWRUtils.GetT2DAsset(gradientTexturePath);

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
                        inDrawFlipdiagonally = true;
                        swingData.starArg += 120;
                        swingData.baseSwingSpeed *= -1;
                        SwingBehavior(swingData);
                    }
                    break;
            }
        }
    }
}
