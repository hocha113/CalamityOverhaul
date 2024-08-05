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
            Sceptre,
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
                case SwingAITypeEnum.Sceptre:
                    shootSengs = 0.95f;
                    maxSwingTime = 70;
                    canDrawSlashTrail = false;
                    SwingBehavior(starArg: 13, baseSwingSpeed: 2, ler1_UpLengthSengs: 0.1f
                        , ler1_UpSpeedSengs: 0.1f, ler1_UpSizeSengs: 0.062f
                    , ler2_DownLengthSengs: 0.01f, ler2_DownSpeedSengs: 0.14f, ler2_DownSizeSengs: 0
                    , minClampLength: 160, maxClampLength: 200, ler1Time: 8, maxSwingTime: 60);
                    break;
            }
        }
    }
}
