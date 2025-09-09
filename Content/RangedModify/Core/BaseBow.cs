using CalamityMod;
using CalamityMod.Projectiles.Summon;
using CalamityOverhaul.Common;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.RangedModify.Core
{
    public abstract class BaseBow : BaseHeldRanged
    {
        #region Data
        /// <summary>
        /// 右手基本角度值
        /// </summary>
        public int ArmRotSengsFrontBaseValue = 60;
        /// <summary>
        /// 左手基本角度值
        /// </summary>
        public int ArmRotSengsBackBaseValue = 70;
        /// <summary>
        /// 是否在<see cref="InOwner"/>执行后自动更新手臂参数，默认为<see langword="true"/>
        /// </summary>
        public bool SetArmRotBool = true;
        /// <summary>
        /// 一个开火周期中手臂动画开始的时间，默认为0
        /// </summary>
        public float HandRotStartTime = 0;
        /// <summary>
        /// 一个开火周期中手臂动画的播放速度，默认为0.4f
        /// </summary>
        public float HandRotSpeedSengs = 0.4f;
        /// <summary>
        /// 一个开火周期中手臂动画的播放幅度，默认为0.7f
        /// </summary>
        public float HandRotRange = 0.7f;
        /// <summary>
        /// 是否启用开火动画，默认为<see langword="true"/>
        /// </summary>
        public bool CanFireMotion = true;
        /// <summary>
        /// 开火后是否自动执行弹药消耗逻辑，默认为<see langword="true"/>
        /// </summary>
        public bool CanUpdateConsumeAmmoInShootBool = true;
        /// <summary>
        /// 开火时是否默认播放手持物品的使用音效<see cref="Item.UseSound"/>，但如果准备重写<see cref="SpanProj"/>，这个属性将失去作用，默认为<see langword="true"/>
        /// </summary>
        public bool FiringDefaultSound = true;
        /// <summary>
        /// 是否绘制箭矢动画，默认为<see langword="true"/>
        /// </summary>
        public bool BowArrowDrawBool = true;
        /// <summary>
        /// 绘制箭矢的数量，默认值为1
        /// </summary>
        public int BowArrowDrawNum = 1;
        /// <summary>
        /// 箭矢绘制模长偏移值，默认值为-20
        /// </summary>
        protected int DrawArrowMode = -20;
        /// <summary>
        /// 箭矢绘制角度矫正值，默认为0
        /// </summary>
        protected float DrawArrowOffsetRot = 0;
        /// <summary>
        /// 自定义绘制中心点，默认为<see cref="Vector2.Zero"/>，即不启用
        /// </summary>
        protected Vector2 CustomDrawOrig = Vector2.Zero;
        /// <summary>
        /// 开火额外矫正位置，这个值在开火后自动回归默认值<see cref="Vector2.Zero"/>
        /// </summary>
        public Vector2 FireOffsetPos = Vector2.Zero;
        /// <summary>
        /// 开火速度额外矫正，这个值在开火后自动回归默认值<see cref="Vector2.Zero"/>
        /// </summary>
        public Vector2 FireOffsetVector = Vector2.Zero;
        /// <summary>
        /// 是一把弓
        /// </summary>
        public bool IsBow = true;
        /// <summary>
        /// 射弹特殊生成属性，用于决定射弹的特殊行为，默认值为<see cref="SpanTypesEnum.None"/>
        /// </summary>
        public SpanTypesEnum ShootSpanTypeValue = SpanTypesEnum.None;
        /// <summary>
        /// 是否处于开火时间
        /// </summary>
        public override bool CanFire => DownLeft || DownRight && CanRightClick && !onFire && SafeMousetStart;
        /// <summary>
        /// 是否允许手持状态，如果玩家关闭了手持动画设置，这个值将在非开火状态时返回<see langword="false"/>
        /// </summary>
        public override bool OnHandheldDisplayBool => WeaponHandheldDisplay || CanFire;
        /// <summary>
        /// 获取来自物品的生成源
        /// </summary>
        public override EntitySource_ItemUse_WithAmmo Source => new EntitySource_ItemUse_WithAmmo(Owner, Item, UseAmmoItemType, "CWRBow");
        /// <summary>
        /// 弓弦数据
        /// </summary>
        public BowstringDataStruct BowstringData = new BowstringDataStruct();
        public struct BowstringDataStruct
        {
            /// <summary>
            /// 是否裁切弓弦，这个会改变弓的绘制方式
            /// </summary>
            public bool CanDeduct;
            /// <summary>
            /// 是否额外绘制动画弓弦
            /// </summary>
            public bool CanDraw;
            /// <summary>
            /// 如果<see cref="CanDeduct"/>为<see langword="true"/>就需要设置这个矩形，用于决定裁剪的部位
            /// </summary>
            public Rectangle DeductRectangle = default;
            /// <summary>
            /// 设置这个会让整个弓弦位置移动
            /// </summary>
            public Vector2 CoreOffset = default;
            /// <summary>
            /// 上侧的位置矫正
            /// </summary>
            public Vector2 TopBowOffset = default;
            /// <summary>
            /// 下侧的位置矫正
            /// </summary>
            public Vector2 BottomBowOffset = default;
            /// <summary>
            /// 点集，为<see cref="DoEffect"/>所用
            /// </summary>
            public Vector2[] Points = new Vector2[3];
            /// <summary>
            /// 效果实例
            /// </summary>
            public PathEffect DoEffect = null;
            /// <summary>
            /// 是否自动更具<see cref="DeductRectangle"/>的宽度来设置<see cref="thicknessEvaluator"/>，默认为<see langword="true"/>
            /// </summary>
            public bool AutomaticWidthSetting = true;
            /// <summary>
            /// 弓弦宽度
            /// </summary>
            public TrailThicknessCalculator thicknessEvaluator = (_) => 1;
            /// <summary>
            /// 弓弦颜色
            /// </summary>
            public TrailColorEvaluator colorEvaluator = null;
            public BowstringDataStruct() { }
        }
        #endregion

        public void SetArmInFire() {
            if (ShootCoolingValue >= HandRotStartTime && CanFireMotion) {
                float backArmRotation = Projectile.rotation * SafeGravDir + MathHelper.PiOver2 + MathHelper.Pi * DirSign;
                float amountValue = 1 - ShootCoolingValue / (Item.useTime - HandRotStartTime);
                Player.CompositeArmStretchAmount stretch = amountValue.ToStretchAmount();
                Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsBack * -DirSign);
                Owner.SetCompositeArmFront(true, stretch, backArmRotation);
            }
        }

        /// <summary>
        /// 手持武器时距离玩家中心的距离。
        /// 默认会根据武器纹理的宽度自动计算一个合理值。
        /// 对于特殊形状的武器，可以在子类中重写它以进行微调
        /// </summary>
        protected virtual float HandheldDistance {
            get {
                if (VaultUtils.isServer) {//不 要 在 服 务 器 上 访 问 图 片
                    return 0;
                }
                return TextureValue.Width / 2;
            }
        }
        /// <summary>
        /// 从闲置到瞄准姿势的动画过渡进度，范围从 0 (完全闲置) 到 1 (完全瞄准)
        /// </summary>
        private float fireAnimationProgress;
        /// <summary>
        /// 瞄准动画的过渡速度，值越大，从闲置到开火姿势的过渡越快
        /// </summary>
        public float AimingAnimationSpeed = 0.1f;
        /// <summary>
        /// 是否启用“牛仔甩枪”式的旋转瞄准动画，默认为<see langword="true"/>
        /// </summary>
        public bool EnableCowboySpin = true;

        /// <summary>
        /// 统一处理从闲置到开火的所有姿势过渡和动画
        /// </summary>
        private void UpdateAimingAnimation() {
            //计算目标姿势 (闲置和开火)
            int offsetY = 0;
            if (!VaultUtils.isServer) {
                offsetY = TextureValue.Height / 10;
            }
            //-- 目标闲置姿势 --
            Vector2 idlePosition = Owner.GetPlayerStabilityCenter() + new Vector2(Owner.direction * HandheldDistance, offsetY).RotatedBy(Owner.fullRotation);
            int art = 20;
            if (SafeGravDir < 0) {
                art = 340;
            }
            float fullRotation = MathHelper.ToDegrees(Owner.fullRotation) * Owner.direction;
            float value = art + fullRotation;
            float idleRotation = Owner.direction > 0 ? MathHelper.ToRadians(value) : MathHelper.ToRadians(180 - value);
            float idleArmRotFront = ArmRotSengsFrontBaseValue * CWRUtils.atoR * SafeGravDir;
            float idleArmRotBack = ArmRotSengsBackBaseValue * CWRUtils.atoR * SafeGravDir;

            //-- 目标开火姿势 --
            float fireRotation = LazyRotationUpdate ? oldSetRoting : ToMouseA;
            Vector2 firePosition = Owner.GetPlayerStabilityCenter() + fireRotation.ToRotationVector2() * HandheldDistance;
            float fireArmRot = (MathHelper.PiOver2 * SafeGravDir - fireRotation) * DirSign * SafeGravDir;

            if (EnableCowboySpin) {
                float bowTargetRotation = fireRotation;
                bool isAimingUpwards = fireRotation.ToRotationVector2().Y * DirSign * SafeGravDir < 0;
                if (EnableCowboySpin && isAimingUpwards) {
                    //如果是朝右，默认不会旋转，手动给目标角度加上360度（2 * PI），强制Lerp走长路径
                    if (Owner.direction > 0) {
                        bowTargetRotation += MathHelper.TwoPi;
                    }
                }

                Projectile.rotation = MathHelper.Lerp(idleRotation, bowTargetRotation, fireAnimationProgress);
            }
            else {
                Projectile.rotation = idleRotation.AngleLerp(fireRotation, fireAnimationProgress);
            }

            //-- 弓身位置：插值逻辑保持不变 --
            Projectile.Center = Vector2.Lerp(idlePosition, firePosition, fireAnimationProgress);

            //-- 手臂旋转：始终使用“最短路径”插值，保持平滑 --
            ArmRotSengsFront = idleArmRotFront.AngleLerp(fireArmRot, fireAnimationProgress);
            ArmRotSengsBack = idleArmRotBack.AngleLerp(fireArmRot, fireAnimationProgress);

            //当完全进入开火姿势时，才更新玩家朝向
            if (fireAnimationProgress >= 0.9f) {
                Owner.direction = fireRotation.ToRotationVector2().X > 0 ? 1 : -1;
            }
        }

        public override void FiringIncident() {
            //FiringIncident 的逻辑现在只负责更新开火状态，不再直接调用姿势函数
            if (DownLeft) {
                if (HaveAmmo) onFire = true;
            }
            else {
                onFire = false;
            }

            if (DownRight && CanRightClick && !onFire && SafeMousetStart) {
                if (HaveAmmo) {
                    SafeMousetStart2 = true;
                    onFireR = true;
                }
            }
            else {
                onFireR = false;
                SafeMousetStart2 = false;
            }
        }

        public override void PostSetRangedProperty() {
            if (HandheldDistance < 16) {
                EnableCowboySpin = false;
            }

            foreach (var gBow in RangedLoader.GlobalRangeds) {
                gBow.PostModifyBow(this);
            }

            //如果指定了弓弦的扣除矩形（用于纹理剪裁）
            if (BowstringData.DeductRectangle != default) {
                //允许扣除逻辑进行
                BowstringData.CanDeduct = true;
                //如果开启了自动宽度设置，并且扣除矩形的宽度有效（大于0）
                if (BowstringData.AutomaticWidthSetting && BowstringData.DeductRectangle.Width > 0) {
                    //设置弓弦的厚度计算器为固定值，等于扣除矩形的宽度
                    BowstringData.thicknessEvaluator = (_) => BowstringData.DeductRectangle.Width / 2;
                }
                if (BowstringData.TopBowOffset == default && BowstringData.BottomBowOffset == default) {
                    BowstringData.TopBowOffset = BowstringData.BottomBowOffset = new Vector2(BowstringData.DeductRectangle.Left, BowstringData.DeductRectangle.Top - 2);
                }
            }

            //如果任意弓弦的偏移量（顶部、底部、核心）被设置，或者设置了矩形裁切，允许弓弦绘制
            if (BowstringData.TopBowOffset != default || BowstringData.BottomBowOffset != default || BowstringData.CoreOffset != default || BowstringData.CanDeduct) {
                BowstringData.CanDraw = true;
            }
            //如果仅设置了顶部偏移量，但未设置底部偏移量，则将底部偏移量与顶部偏移量保持一致
            if (BowstringData.TopBowOffset != default && BowstringData.BottomBowOffset == default) {
                BowstringData.BottomBowOffset = BowstringData.TopBowOffset;
            }
            //如果仅设置了底部偏移量，但未设置顶部偏移量，则将顶部偏移量与底部偏移量保持一致
            if (BowstringData.TopBowOffset == default && BowstringData.BottomBowOffset != default) {
                BowstringData.TopBowOffset = BowstringData.BottomBowOffset;
            }
        }

        public virtual void PreInOwner() { }

        public override void InOwner() {
            PreInOwner();
            SetHeld();

            Projectile.timeLeft = 2;

            //如果玩家正在按住攻击键，则进度条向 1 (开火姿势) 推进
            //否则，进度条向 0 (闲置姿势) 回退
            fireAnimationProgress += CanFire ? AimingAnimationSpeed : -AimingAnimationSpeed;
            fireAnimationProgress = MathHelper.Clamp(fireAnimationProgress, 0f, 1f); // 确保进度在 0 和 1 之间

            if (!CanFire) {
                if (LazyRotationUpdate) {
                    oldSetRoting = ToMouseA;
                }
            }

            UpdateAimingAnimation();
            
            if (HaveAmmo) {
                ShootCoolingValue += AttackSpeed;
            }
            
            SetArmInFire();

            ShootCoolingValue = MathHelper.Clamp(ShootCoolingValue, 0, Item.useTime);

            if (SafeMouseInterfaceValue) {
                FiringIncident();
            }

            PostInOwner();
        }

        public virtual void PostInOwner() { }

        public virtual void BowShoot() {
            int proj = Projectile.NewProjectile(Source, Projectile.Center + FireOffsetPos, ShootVelocity + FireOffsetVector
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].CWR().SpanTypes = (byte)ShootSpanTypeValue;
            Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
        }

        public virtual void BowShootR() {
            int proj = Projectile.NewProjectile(Source, Projectile.Center + FireOffsetPos, ShootVelocity + FireOffsetVector
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].CWR().SpanTypes = (byte)ShootSpanTypeValue;
            Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
        }

        public virtual void SetShootAttribute() {

        }

        public virtual void PostBowShoot() {

        }

        public override void SpanProj() {
            //ShootCoolingValue >= Item.useTime而不是ShootCoolingValue > Item.useTime，后者会让所有弓的攻击看起来都慢一帧
            if (ShootCoolingValue >= Item.useTime && (onFire || onFireR)) {
                if (LazyRotationUpdate) {
                    Projectile.rotation = oldSetRoting = ToMouseA;
                }

                if (ForcedConversionTargetAmmoFunc.Invoke()) {
                    AmmoTypes = ToTargetAmmo;
                }

                SetShootAttribute();

                if (Projectile.IsOwnedByLocalPlayer()) {
                    if (onFire) {
                        BowShoot();
                    }
                    if (onFireR) {
                        BowShootR();
                    }
                    if (GlobalItemBehavior) {
                        ItemLoaderInFireSetBaver();
                    }
                    UpdateConsumeAmmo();
                }
                PostBowShoot();

                if (FiringDefaultSound) {
                    HanderPlaySound();
                }

                FireOffsetVector = FireOffsetPos = Vector2.Zero;
                ShootCoolingValue = 0;
                onFireR = onFire = false;
            }
        }

        public sealed override bool PreDraw(ref Color lightColor) {
            Vector2 drawPos = Projectile.Center - Main.screenPosition + SpecialDrawPositionOffset;
            if (OnHandheldDisplayBool) {
                if (BowstringData.CanDraw) {
                    DrawBowstring(drawPos);
                }

                if (BowstringData.CanDeduct) {
                    DeductBowDraw(drawPos, ref lightColor);
                }
                else {
                    BowDraw(drawPos, ref lightColor);
                }
            }

            if (CWRServerConfig.Instance.BowArrowDraw && BowArrowDrawBool) {
                ArrowDraw(drawPos, lightColor);
            }

            return false;
        }

        public virtual void BowDraw(Vector2 drawPos, ref Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, drawPos, null, lightColor
                , Projectile.rotation, TextureValue.Size() / 2, Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            if (GlowTexPath != "") {
                Texture2D glowTex = RangedLoader.TypeToGlowAsset[GetType()].Value;
                Main.EntitySpriteDraw(glowTex, drawPos, null, Color.White
                , Projectile.rotation, glowTex.Size() / 2, Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            }
        }

        public virtual void DeductBowDraw(Vector2 drawPos, ref Color lightColor) {
            Effect effect = EffectLoader.DeductDraw.Value;
            effect.CurrentTechnique.Passes[0].Apply();
            effect.Parameters["topLeft"].SetValue(BowstringData.DeductRectangle.TopLeft());
            effect.Parameters["width"].SetValue(BowstringData.DeductRectangle.Width);
            effect.Parameters["height"].SetValue(BowstringData.DeductRectangle.Height);
            effect.Parameters["drawColor"].SetValue(lightColor.ToVector4());
            effect.Parameters["textureSize"].SetValue(TextureValue.Size());
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(default, BlendState.AlphaBlend, Main.DefaultSamplerState
                , default, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);

            Main.EntitySpriteDraw(TextureValue, drawPos, null, lightColor
                , Projectile.rotation, TextureValue.Size() / 2, Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            if (GlowTexPath != "") {
                Texture2D glowTex = RangedLoader.TypeToGlowAsset[GetType()].Value;
                Main.EntitySpriteDraw(glowTex, drawPos, null, Color.White
                , Projectile.rotation, glowTex.Size() / 2, Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        public virtual void HanderBowstringTexturePoss(float t, out Vector2 leftTexCoord, out Vector2 rightTexCoord) {
            // 获取 DeductRectangle 的相关信息
            Rectangle deductRec = BowstringData.DeductRectangle;

            // 计算矩形的起始点和范围在纹理上的比例
            float minU = (float)deductRec.X / TextureValue.Width;  // 左边界的纹理坐标
            float maxU = (float)(deductRec.X + deductRec.Width) / TextureValue.Width; // 右边界的纹理坐标
            float minV = (float)deductRec.Y / TextureValue.Height; // 上边界的纹理坐标
            float maxV = (float)(deductRec.Y + deductRec.Height) / TextureValue.Height; // 下边界的纹理坐标

            // 确保 t 在 [0, 1] 范围内并均匀映射到 DeductRectangle 的 Y 轴范围
            float v = minV + (maxV - minV) * t; // 均匀映射到纵向范围

            // 生成左、右 TexCoord
            leftTexCoord = new Vector2(minU, v);  // 左侧为矩形的左边界
            rightTexCoord = new Vector2(maxU, v); // 右侧为矩形的右边界
        }

        public virtual void DrawBowstring(Vector2 drawPos) {
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            BowstringData.Points = new Vector2[3];

            Vector2 toProjRot = Projectile.rotation.ToRotationVector2();

            Vector2 bowPos = drawPos - toProjRot * (TextureValue.Width / 2 - 1);
            bowPos += toProjRot * BowstringData.CoreOffset.X;
            bowPos += toProjRot.GetNormalVector() * BowstringData.CoreOffset.Y * DirSign;
            Vector2 posTop = bowPos + (Projectile.rotation - MathHelper.PiOver2).ToRotationVector2() * (TextureValue.Height / 2 - BowstringData.TopBowOffset.Y) + toProjRot * BowstringData.TopBowOffset.X;
            Vector2 posBottom = bowPos + (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2() * (TextureValue.Height / 2 - BowstringData.BottomBowOffset.Y) + toProjRot * BowstringData.BottomBowOffset.X;

            float lengsOFstValue = ShootCoolingValue / Item.useTime * 16;
            lengsOFstValue -= (BowstringData.TopBowOffset.X + BowstringData.BottomBowOffset.X) / 2;

            if (DirSign < 0) {
                posTop = bowPos + (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2() * (TextureValue.Height / 2 - BowstringData.TopBowOffset.Y) + toProjRot * BowstringData.TopBowOffset.X;
                posBottom = bowPos + (Projectile.rotation - MathHelper.PiOver2).ToRotationVector2() * (TextureValue.Height / 2 - BowstringData.BottomBowOffset.Y) + toProjRot * BowstringData.BottomBowOffset.X;
            }

            BowstringData.Points[0] = posTop;
            BowstringData.Points[1] = bowPos - toProjRot * lengsOFstValue;
            BowstringData.Points[2] = posBottom;

            if (BowstringData.DoEffect == null) {
                BowstringData.colorEvaluator ??= (_) => Lighting.GetColor((int)(Projectile.Center.X / 16), (int)(Projectile.Center.Y / 16));
                BowstringData.DoEffect = new PathEffect(BowstringData.thicknessEvaluator, BowstringData.colorEvaluator, handlerTexturePoss: HanderBowstringTexturePoss);
            }
            BowstringData.DoEffect.GetPathData(BowstringData.Points, Vector2.Zero, 88);
            BowstringData.DoEffect.Draw(TextureValue);
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void ArrowResourceProcessing(ref Texture2D value, Item arrow) {
            if (!arrow.consumable) {
                int newtype = ItemID.WoodenArrow;
                if (VaultUtils.ProjectileToSafeAmmoMap.TryGetValue(arrow.shoot, out int value2)) {
                    newtype = value2;
                }
                Main.instance.LoadItem(newtype);
                value = TextureAssets.Item[newtype].Value;
            }
        }

        public virtual void CustomArrowRP(ref Texture2D value, Item arrow) { }

        public void ArrowDraw(Vector2 drawPos, Color lightColor) {
            int cooltime = 3;
            if (cooltime > Item.useTime / 3) {
                cooltime = Item.useTime / 3;
            }

            if (ShootCoolingValue <= cooltime) {
                return;
            }

            int useAmmoItemType = UseAmmoItemType;
            if (useAmmoItemType == ItemID.None) {
                return;
            }
            if (useAmmoItemType > 0 && useAmmoItemType < TextureAssets.Item.Length) {
                Main.instance.LoadItem(useAmmoItemType);
            }

            Texture2D arrowValue = TextureAssets.Item[useAmmoItemType].Value;
            Item arrowItemInds = new Item(useAmmoItemType);
            ArrowResourceProcessing(ref arrowValue, arrowItemInds);
            CustomArrowRP(ref arrowValue, arrowItemInds);

            if (ForcedConversionTargetAmmoFunc.Invoke() && ToTargetAmmoInDraw != -1) {
                arrowValue = TextureAssets.Projectile[ToTargetAmmo].Value;
                if (ToTargetAmmoInDraw > 0) {
                    arrowValue = TextureAssets.Projectile[ToTargetAmmoInDraw].Value;
                }
                if (ISForcedConversionDrawAmmoInversion) {
                    CustomDrawOrig = new Vector2(arrowValue.Width / 2, 0);
                    DrawArrowOffsetRot = MathHelper.Pi;
                }
            }
            else {
                CustomDrawOrig = Vector2.Zero;
                DrawArrowOffsetRot = 0;
            }

            float drawRot = Projectile.rotation + MathHelper.PiOver2;
            float chordCoefficient = 1 - ShootCoolingValue / Item.useTime;

            float lengsOFstValue = chordCoefficient * 16 + DrawArrowMode;
            Vector2 inprojRot = Projectile.rotation.ToRotationVector2();
            Vector2 offsetDrawPos = inprojRot * lengsOFstValue;
            Vector2 norlInRotUnit = inprojRot.GetNormalVector();
            Vector2 drawOrig = CustomDrawOrig == Vector2.Zero ? new(arrowValue.Width / 2, arrowValue.Height) : CustomDrawOrig;
            drawPos += offsetDrawPos;

            void drawArrow(float overOffsetRot = 0, Vector2 overOffsetPos = default) => Main.EntitySpriteDraw(arrowValue
                , drawPos + (overOffsetPos == default ? Vector2.Zero : overOffsetPos), null, lightColor
                , drawRot + DrawArrowOffsetRot + overOffsetRot, drawOrig, Projectile.scale, SpriteEffects.FlipVertically);

            switch (BowArrowDrawNum) {
                case 2:
                    drawArrow(0.3f * chordCoefficient);
                    drawArrow(-0.3f * chordCoefficient);
                    break;
                case 3:
                    chordCoefficient += 0.5f;
                    if (chordCoefficient > 1) {
                        chordCoefficient = 1;
                    }
                    drawArrow(0.45f * chordCoefficient, norlInRotUnit * -1f);
                    drawArrow();
                    drawArrow(-0.45f * chordCoefficient, norlInRotUnit * 1f);
                    break;
                case 4:
                    chordCoefficient += 0.3f;
                    if (chordCoefficient > 1) {
                        chordCoefficient = 1;
                    }
                    drawArrow(0.6f * chordCoefficient);
                    drawArrow(-0.6f * chordCoefficient);
                    drawArrow(0.2f * chordCoefficient);
                    drawArrow(-0.2f * chordCoefficient);
                    break;
                case 5:
                    chordCoefficient += 0.3f;
                    if (chordCoefficient > 1) {
                        chordCoefficient = 1;
                    }
                    drawArrow(0.7f * chordCoefficient, norlInRotUnit * 0.3f);
                    drawArrow(-0.7f * chordCoefficient, norlInRotUnit * -0.3f);
                    drawArrow();
                    drawArrow(0.35f * chordCoefficient, norlInRotUnit * 0.2f);
                    drawArrow(-0.35f * chordCoefficient, norlInRotUnit * -0.2f);
                    break;
                case 1:
                default:
                    drawArrow();
                    break;
            }
        }

        public void LimitingAngle(int minrot = 50, int maxrot = 130) {
            float minRot = MathHelper.ToRadians(minrot);
            float maxRot = MathHelper.ToRadians(maxrot);
            Projectile.rotation = MathHelper.Clamp(ToMouseA + MathHelper.Pi, minRot, maxRot) - MathHelper.Pi;
            if (ToMouseA + MathHelper.Pi > MathHelper.ToRadians(270)) {
                Projectile.rotation = minRot - MathHelper.Pi;
            }
            Projectile.Center = Owner.GetPlayerStabilityCenter() + Projectile.rotation.ToRotationVector2() * HandheldDistance;
            ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 - (Projectile.rotation + 0.5f * DirSign)) * DirSign;
            SetCompositeArm();
        }
    }
}
