using CalamityMod.Projectiles.Turret;
using CalamityOverhaul.Content.Industrials.MaterialFlow;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets
{
    internal abstract class BaseTurretTP : BaseBattery//同样，这个东西也当作一个电池吧
    {
        #region Data
        /// <summary>
        /// 目标玩家实例
        /// </summary>
        public Player TargetByPlayer;
        /// <summary>
        /// 目标NPC
        /// </summary>
        public NPC TargetByNPC;
        /// <summary>
        /// 是否有开火欲望？
        /// </summary>
        public bool CanFire;
        /// <summary>
        /// 是否友好
        /// </summary>
        public virtual bool Friend => true;
        /// <summary>
        /// 选择角度
        /// </summary>
        public float Rotation;
        /// <summary>
        /// 要发射的射弹
        /// </summary>
        public int ShootID;
        /// <summary>
        /// 左右朝向
        /// </summary>
        public int Dir;
        /// <summary>
        /// 开火蓄力
        /// </summary>
        public int FireStorage;
        /// <summary>
        /// 开火时间，默认为60
        /// </summary>
        public int FireTime = 60;
        /// <summary>
        /// 伤害，默认为22
        /// </summary>
        public int Damage = 22;
        /// <summary>
        /// 受力值
        /// </summary>
        public float RecoilValue;
        /// <summary>
        /// 后坐力大小，默认为12
        /// </summary>
        public float Recoil = 12;
        /// <summary>
        /// 这个炮塔将要对准的点
        /// </summary>
        public Vector2 TargetCenter;
        /// <summary>
        /// 这个炮塔将要对准的点，在Ai中时机使用的值，会自动渐变靠拢<see cref="TargetCenter"/>
        /// </summary>
        public Vector2 NewTargetCenter;
        /// <summary>
        /// 朝向的单位向量
        /// </summary>
        public Vector2 UnitToTarget;
        /// <summary>
        /// 炮塔身体的中心坐标
        /// </summary>
        public Vector2 Center => CenterInWorld + Offset;
        /// <summary>
        /// 炮塔身体中心和TP实体中心的矫正值
        /// </summary>
        public virtual Vector2 Offset => new Vector2(0, -24);
        public override float MaxUEValue => 1000;
        public virtual Asset<Texture2D> GetBodyAsset => CWRAsset.Placeholder_ERROR;
        public virtual Asset<Texture2D> GetBarrelAsset => null;
        #endregion
        public override void SetBattery() {
            if (TargetCenter == default) {
                TargetCenter = Center + new Vector2(111, 80f);
            }
            SetTurret();
        }

        public virtual void SetTurret() { }

        public override void Update() {
            PreUpdate();

            CanFire = false;
            if (Friend) {
                TargetByNPC = Center.FindClosestNPC(800, false, true);
                if (TargetByNPC != null) {
                    TargetCenter = TargetByNPC.Center;
                    CanFire = true;
                }
            }
            else {
                TargetByPlayer = CWRUtils.InPosFindPlayer(Center, 800);
                if (TargetByPlayer != null) {
                    TargetCenter = TargetByPlayer.Center;
                    CanFire = true;
                }
            }

            if (!CanFire) {
                TargetCenter = Center + new Vector2(Dir * 111, 80f);
            }

            NewTargetCenter = Vector2.Lerp(NewTargetCenter, TargetCenter, 0.1f);

            UnitToTarget = Center.To(NewTargetCenter).UnitVector();
            Rotation = UnitToTarget.ToRotation();
            Dir = Math.Sign(UnitToTarget.X);

            if (CanFire && ++FireStorage > FireTime) {
                RecoilValue -= Recoil;
                if (PreShoot() && !VaultUtils.isClient) {
                    Projectile.NewProjectile(new EntitySource_WorldEvent()
                        , Center + UnitToTarget * 64, UnitToTarget * 9, ShootID, Damage, Friend ? 4 : 0, -1);
                }
                FireStorage = 0;
            }

            RecoilValue *= 0.8f;
            if (Math.Abs(RecoilValue) < 0.0011f) {
                RecoilValue = 0;
            }

            PostUpdate();
        }

        public virtual void PreUpdate() { }

        public virtual void PostUpdate() { }

        /// <summary>
        /// 运行与默认发射逻辑之前，该函数运行在所有端上，所以编写弹幕生成代码时需要注意是否是客户端
        /// </summary>
        /// <returns></returns>
        public virtual bool PreShoot() => true;

        public virtual void ModifyDrawData(ref Vector2 drawPos, ref Vector2 drawBarrelPos) { }

        public override void Draw(SpriteBatch spriteBatch) {
            Vector2 drawPos = Center - Main.screenPosition + UnitToTarget * RecoilValue * 0.6f;
            Color drawColor = Lighting.GetColor(Position.X, Position.Y);
            Vector2 drawBarrelPos = drawPos + UnitToTarget * (30 + RecoilValue);

            ModifyDrawData(ref drawPos, ref drawBarrelPos);

            DrawTurret(drawPos, drawBarrelPos, drawColor);
        }

        public virtual void DrawTurret(Vector2 drawPos, Vector2 drawBarrelPos, Color drawColor) {
            if (GetBarrelAsset != null) {
                Main.EntitySpriteDraw(GetBarrelAsset.Value, drawBarrelPos, null, drawColor
                , Rotation, GetBarrelAsset.Size() / 2, 1, Dir > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            }

            Main.EntitySpriteDraw(GetBodyAsset.Value, drawPos, null, drawColor
                , Rotation, GetBodyAsset.Size() / 2, 1, Dir > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }
}
