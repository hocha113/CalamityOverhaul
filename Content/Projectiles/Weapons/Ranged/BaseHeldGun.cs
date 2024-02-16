using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal abstract class BaseHeldGun : BaseHeldRanged
    {
        public float offsetRot;
        public Vector2 offsetPos;
        /// <summary>
        /// 枪械是否受到应力缩放
        /// </summary>
        public virtual bool PressureWhetherIncrease => true;
        /// <summary>
        /// 应力缩放系数
        /// </summary>
        public float OwnerPressureIncrease => PressureWhetherIncrease ? Owner.CWR().PressureIncrease : 1;
        /// <summary>
        /// 枪压，决定开火时的上抬力度
        /// </summary>
        public virtual float GunPressure => 0;
        /// <summary>
        /// 控制力度，决定压枪的力度
        /// </summary>
        public virtual float ControlForce => 0.01f;
        /// <summary>
        /// 开火时会制造的后坐力模长
        /// </summary>
        public virtual float Recoil => GunPressure * 5;
        /// <summary>
        /// 应力范围
        /// </summary>
        public virtual float RangeOfStress => 5;
        /// <summary>
        /// 该枪械在开火时的一个转动角，用于快捷获取
        /// </summary>
        public virtual float GunOnFireRot => ToMouseA - offsetRot * DirSign;
        /// <summary>
        /// 更新后座力的作用状态，这个函数只应该由弹幕主人调用
        /// </summary>
        public virtual void UpdateRecoil() {
            offsetRot -= ControlForce;
            if (offsetRot <= 0) {
                offsetRot = 0;
            }
        }
        /// <summary>
        /// 制造后坐力，这个函数只应该由弹幕主人调用，它不会自动调用，需要重写时在合适的代码片段中调用这个函数
        /// ，以确保制造后坐力的时机正确，一般在<see cref="BaseHeldRanged.SpanProj"/>运行时调用
        /// </summary>
        /// <returns>返回制造出的后坐力向量</returns>
        public virtual Vector2 CreateRecoil() {
            offsetRot += GunPressure * OwnerPressureIncrease;
            Vector2 recoilVr = ShootVelocity.UnitVector() * (Recoil * -OwnerPressureIncrease);
            if (Math.Abs(Owner.velocity.X) < RangeOfStress && Math.Abs(Owner.velocity.Y) < RangeOfStress) {
                Owner.velocity += recoilVr;
            }
            return recoilVr;
        }

        public override void AI() {
            InOwner();
            if (Projectile.IsOwnedByLocalPlayer()) {
                UpdateRecoil();
                SpanProj();
            }
            Time++;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, onFire ? Color.White : lightColor
                , Projectile.rotation, value.Size() / 2, Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            return false;
        }
    }
}
