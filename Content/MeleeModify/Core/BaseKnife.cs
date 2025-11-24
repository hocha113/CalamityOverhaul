using CalamityMod;
using CalamityOverhaul.Content.RemakeItems;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.MeleeModify.Core
{
    internal abstract class BaseKnife : BaseSwing
    {
        public override int TargetID => CWRItemOverride.GetCalItemID(Name[..^4]);
        public override string Texture => CWRConstant.Placeholder3;
        public override Texture2D TextureValue => TargetID == ItemID.None ? TextureAssets.Projectile[Type].Value : TextureAssets.Item[TargetID].Value;
        public SwingDataStruct SwingData = new SwingDataStruct();
        public SwingAITypeEnum SwingAIType;
        protected bool autoSetShoot;
        protected bool onSound;
        protected Dictionary<int, NPC> onHitNPCs = [];
        public enum SwingAITypeEnum
        {
            None = 0,
            UpAndDown,
            Down,
            Sceptre,
        }
        public sealed override void SetSwingProperty() {
            Projectile.extraUpdates = 4;
            ownerOrientationLock = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14 * UpdateRate;
            Projectile.DamageType = CWRRef.GetTrueMeleeDamageClass();
            SetKnifeProperty();
            VaultUtils.SafeLoadItem(TargetID);
        }

        protected void updaTrailTexture() => SwingSystem.trailTextures[Type] = CWRUtils.GetT2DAsset(trailTexturePath);
        protected void updaGradientTexture() => SwingSystem.gradientTextures[Type] = CWRUtils.GetT2DAsset(gradientTexturePath);

        public virtual void SetKnifeProperty() { }

        public sealed override void Initialize() {
            base.Initialize();
            Projectile.DamageType = Item.DamageType;
            maxSwingTime = Item.useTime;
            SwingData.maxSwingTime = maxSwingTime;
            toProjCoreMode = (IgnoreImpactBoxSize ? 22 : Projectile.width) / 2f;
            if (autoSetShoot) {
                ShootSpeed = Item.shootSpeed;
            }
            if (++SwingIndex > 1) {
                SwingIndex = 0;
            }
            KnifeInitialize();
        }

        public virtual void KnifeInitialize() {

        }

        public virtual void WaveUADBehavior() {

        }

        public virtual void SceptreBehavior() {

        }

        public virtual void MeleeEffect() {

        }

        public virtual bool PreSwingAI() {
            return true;
        }

        public sealed override void SwingAI() {
            if (!PreSwingAI()) {
                return;
            }
            switch (SwingAIType) {
                case SwingAITypeEnum.None:
                    SwingBehavior(SwingData);
                    break;
                case SwingAITypeEnum.UpAndDown:
                    SwingDataStruct swingData = SwingData;
                    if (SwingIndex == 1) {
                        inDrawFlipdiagonally = true;
                        swingData.starArg += 120;
                        swingData.baseSwingSpeed *= -1;
                    }
                    WaveUADBehavior();
                    SwingBehavior(swingData);
                    break;
                case SwingAITypeEnum.Down:
                    inDrawFlipdiagonally = true;
                    SwingData.starArg += 120;
                    SwingData.baseSwingSpeed *= -1;
                    SwingBehavior(SwingData);
                    break;
                case SwingAITypeEnum.Sceptre:
                    shootSengs = 0.95f;
                    maxSwingTime = 70;
                    canDrawSlashTrail = false;
                    SwingData.starArg = 13;
                    SwingData.baseSwingSpeed = 2;
                    SwingData.ler1_UpLengthSengs = 0.1f;
                    SwingData.ler1_UpSpeedSengs = 0.1f;
                    SwingData.ler1_UpSizeSengs = 0.062f;
                    SwingData.ler2_DownLengthSengs = 0.01f;
                    SwingData.ler2_DownSpeedSengs = 0.14f;
                    SwingData.ler2_DownSizeSengs = 0;
                    SwingData.minClampLength = 160;
                    SwingData.maxClampLength = 200;
                    SwingData.ler1Time = 8;
                    SwingData.maxSwingTime = 60;
                    SceptreBehavior();
                    SwingBehavior(SwingData);
                    break;
            }
        }

        public sealed override void NoServUpdate() {
            if (Time % UpdateRate == 0) {
                MeleeEffect();
            }
        }

        public sealed override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (onHitNPCs.TryAdd(target.whoAmI, target)) {
                KnifeHitNPC(target, hit, damageDone);
                Owner.wingTime += Owner.wingTimeMax * 0.2f;
                if (Owner.wingTime > Owner.wingTimeMax) {
                    Owner.wingTime = Owner.wingTimeMax;
                }
            }
        }
        /// <summary>
        /// 在刀刃击中NPC时运行
        /// </summary>
        /// <param name="target"></param>
        /// <param name="hit"></param>
        /// <param name="damageDone"></param>
        public virtual void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        }

        /// <summary>
        /// 执行适配的大幅度挥击动作，控制挥击速度、尺寸变化及音效播放
        /// </summary>
        /// <param name="initialMeleeSize">初始的近战武器大小倍率</param>
        /// <param name="phase1Ratio">第一阶段持续时间占总挥击时间的比例</param>
        /// <param name="phase2Ratio">第二阶段持续时间占总挥击时间的比例</param>
        /// <param name="phase0SwingSpeed">第一阶段的基础挥击速度（负值表示回收动作）</param>
        /// <param name="phase1SwingSpeed">第二阶段的基础挥击速度</param>
        /// <param name="phase2SwingSpeed">第三阶段的基础挥击速度</param>
        /// <param name="phase0MeleeSizeIncrement">第一阶段每帧增加的武器尺寸倍率</param>
        /// <param name="phase2MeleeSizeIncrement">第三阶段每帧减少的武器尺寸倍率</param>
        /// <param name="swingSound">挥击音效的样式，默认为<see cref="SoundID.item1"/></param>
        public void ExecuteAdaptiveSwing(
            float initialMeleeSize = 0.84f,
            float phase1Ratio = 0.5f,
            float phase2Ratio = 0.6f,
            float phase0SwingSpeed = -0.3f,
            float phase1SwingSpeed = 4.2f,
            float phase2SwingSpeed = 9f,
            float phase0MeleeSizeIncrement = 0.002f,
            float phase2MeleeSizeIncrement = -0.002f,
            SoundStyle swingSound = default,
            bool drawSlash = true) {
            //初始化时间为0时设置初始武器大小
            if (Time == 0) {
                OtherMeleeSize = initialMeleeSize;
            }

            if (SwingAIType == SwingAITypeEnum.UpAndDown && SwingIndex == 1) {
                phase0SwingSpeed *= -1;
                phase1SwingSpeed *= -1;
                phase2SwingSpeed *= -1;
            }

            //计算当前挥击速度比例
            float swingSpeedMultiplier = SwingMultiplication;

            //计算各阶段的结束时间
            int phase1EndTime = (int)(maxSwingTime * phase1Ratio * UpdateRate * swingSpeedMultiplier);
            int phase2EndTime = (int)(maxSwingTime * phase2Ratio * UpdateRate * swingSpeedMultiplier);

            //第二阶段逻辑：主挥击阶段
            if (Time > phase1EndTime) {
                if (!onSound) {
                    //播放挥击音效
                    SoundEngine.PlaySound(swingSound == default ? SoundID.Item1 : swingSound, Owner.Center);
                    onSound = true;
                }

                //启用挥击轨迹绘制
                canDrawSlashTrail = drawSlash;

                //设置第二阶段的挥击速度
                SwingData.baseSwingSpeed = phase1SwingSpeed;

                //在进入第二阶段的第一帧计算具体挥击速度
                if (Time == phase1EndTime + 1) {
                    speed = MathHelper.ToRadians(SwingData.baseSwingSpeed) / swingSpeedMultiplier;
                }
            }
            //第一阶段逻辑：准备挥击阶段
            else {
                //增大武器尺寸
                OtherMeleeSize += phase0MeleeSizeIncrement;

                //设置第一阶段的挥击速度
                SwingData.baseSwingSpeed = phase0SwingSpeed;

                //计算挥击速度
                speed = MathHelper.ToRadians(SwingData.baseSwingSpeed) / swingSpeedMultiplier;

                //禁用挥击轨迹绘制
                canDrawSlashTrail = false;
            }

            //第三阶段逻辑：挥击结束阶段
            if (Time > phase2EndTime) {
                //缩小武器尺寸
                OtherMeleeSize += phase2MeleeSizeIncrement;

                //设置第三阶段的挥击速度
                SwingData.baseSwingSpeed = phase2SwingSpeed;

                //在进入第三阶段的第一帧计算具体挥击速度
                if (Time == phase2EndTime + 1) {
                    speed = MathHelper.ToRadians(SwingData.baseSwingSpeed) / swingSpeedMultiplier;
                }
            }
        }
    }
}
