using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.DataStructures;

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
        /// 鼠标是否悬停在TP实体之上
        /// </summary>
        public bool HoverTP;
        /// <summary>
        /// 没电提示
        /// </summary>
        public bool BatteryPrompt;
        /// <summary>
        /// 攻击提示
        /// </summary>
        public bool AttackPrompt;
        /// <summary>
        /// 是否友好
        /// </summary>
        public virtual bool Friend => true;
        /// <summary>
        /// 选择角度
        /// </summary>
        public float Rotation;
        /// <summary>
        /// 单次攻击的能量消耗，默认为20
        /// </summary>
        public float SingleEnergyConsumption = 20;
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
        public virtual string BodyPath => "";
        public virtual string BodyGlowPath => "";
        public virtual string BarrelPath => "";
        public virtual string BarrelGlowPath => "";
        public virtual Asset<Texture2D> GetBodyAsset => ModifyTurretLoader.BodyAssetDic[ID];
        public virtual Asset<Texture2D> GetBodyGlowAsset => ModifyTurretLoader.BodyGlowAssetDic[ID];
        public virtual Asset<Texture2D> GetBarrelAsset => ModifyTurretLoader.BarrelAssetDic[ID];
        public virtual Asset<Texture2D> GetBarrelGlowAsset => ModifyTurretLoader.BarrelGlowAssetDic[ID];
        #endregion
        public override void SetBattery() {
            if (TargetCenter == default) {
                TargetCenter = Center + new Vector2(111, 80f);
            }
            if (!Friend) {//敌对炮塔在放置之初需要是满电量的
                MachineData.UEvalue = MaxUEValue;
            }
            SetTurret();
        }

        public virtual void SetTurret() { }

        public override void Update() {
            HoverTP = HitBox.Intersects(Main.MouseWorld.GetRectangle(1));

            PreUpdate();

            CanFire = false;
            if (Friend) {
                TargetByNPC = Center.FindClosestNPC(700, false, true);
                if (TargetByNPC != null) {
                    TargetCenter = TargetByNPC.Center;
                    CanFire = true;
                }
            }
            else {
                TargetByPlayer = CWRUtils.InPosFindPlayer(Center, 700);
                if (TargetByPlayer != null) {
                    TargetCenter = TargetByPlayer.Center;
                    CanFire = true;
                }
            }

            if (MachineData.UEvalue <= SingleEnergyConsumption) {
                CanFire = false;
                if (!BatteryPrompt) {
                    CombatText.NewText(HitBox, new Color(111, 247, 200), CWRLocText.Instance.Turret_Text1.Value, false);
                    BatteryPrompt = true;
                }
            }
            else {
                BatteryPrompt = false;
            }

            if (!CanFire) {
                AttackPrompt = false;
                FireStorage = FireTime;//避免零帧起手
                TargetCenter = Center + new Vector2(Dir * 111, 80f);
                if (!Friend && TargetByPlayer == null) {//敌对炮塔在不攻击的情况下会自行充能
                    MachineData.UEvalue += 0.5f;
                }
            }
            else if (!AttackPrompt) {
                if (!Friend) {
                    CombatText.NewText(HitBox, Color.OrangeRed, CWRLocText.Instance.Turret_Text2.Value, false);
                }
                AttackPrompt = true;
            }

            NewTargetCenter = Vector2.Lerp(NewTargetCenter, TargetCenter, 0.1f);

            UnitToTarget = Center.To(NewTargetCenter).UnitVector();
            Rotation = UnitToTarget.ToRotation();
            Dir = Math.Sign(UnitToTarget.X);

            if (CanFire && FireStorage <= 0) {
                RecoilValue -= Recoil;
                if (PreShoot() && !VaultUtils.isClient) {
                    Projectile.NewProjectile(new EntitySource_WorldEvent()
                        , Center + UnitToTarget * 64, UnitToTarget * 9, ShootID, Damage, Friend ? 4 : 0, -1);
                }
                MachineData.UEvalue -= SingleEnergyConsumption;
                MachineData.UEvalue = MathHelper.Clamp(MachineData.UEvalue, 0, MaxUEValue);
                FireStorage += FireTime;
            }

            if (FireStorage > 0) {
                FireStorage--;
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
            Vector2 drawPos = Center + UnitToTarget * RecoilValue * 0.6f - Main.screenPosition;
            Color drawColor = Lighting.GetColor(Position.X, Position.Y);
            Vector2 drawBarrelPos = drawPos + UnitToTarget * (30 + RecoilValue);

            ModifyDrawData(ref drawPos, ref drawBarrelPos);

            DrawTurret(drawPos, drawBarrelPos, drawColor);

            DrawChargeBar();
        }

        public virtual void DrawChargeBar() {
            if (!HoverTP) {
                return;
            }

            Vector2 drawPos = Center + new Vector2(-30, 40) - Main.screenPosition;
            int uiBarByWidthSengs = (int)(ChargingStationTP.BarFull.Value.Width * (MachineData.UEvalue / MaxUEValue));
            // 绘制温度相关的图像
            Rectangle fullRec = new Rectangle(0, 0, uiBarByWidthSengs, ChargingStationTP.BarFull.Value.Height);
            Main.spriteBatch.Draw(ChargingStationTP.BarTop.Value, drawPos, null, Color.White, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            Main.spriteBatch.Draw(ChargingStationTP.BarFull.Value, drawPos + new Vector2(10, 0), fullRec, Color.White, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
        }

        public virtual void DrawTurret(Vector2 drawPos, Vector2 drawBarrelPos, Color drawColor) {
            Color glowColor = Color.White;
            if (!CanFire) {//在待机时设置颜色偏暗，让玩家知道这个炮塔在待机
                glowColor = drawColor;
                glowColor.R /= 3;
                glowColor.G /= 3;
                glowColor.B /= 3;
                glowColor.A = 255;

                drawColor.R /= 2;
                drawColor.G /= 2;
                drawColor.B /= 2;
                drawColor.A = 255;
            }

            if (GetBarrelAsset != null) {
                Main.EntitySpriteDraw(GetBarrelAsset.Value, drawBarrelPos, null, drawColor
                , Rotation, GetBarrelAsset.Size() / 2, 1, Dir > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
                if (GetBarrelGlowAsset != null) {
                    Main.EntitySpriteDraw(GetBarrelGlowAsset.Value, drawBarrelPos, null, glowColor
                    , Rotation, GetBarrelGlowAsset.Size() / 2, 1, Dir > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
                }
            }

            Main.EntitySpriteDraw(GetBodyAsset.Value, drawPos, null, drawColor
                , Rotation, GetBodyAsset.Size() / 2, 1, Dir > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            if (GetBodyGlowAsset != null) {
                Main.EntitySpriteDraw(GetBodyGlowAsset.Value, drawPos, null, glowColor
                , Rotation, GetBodyGlowAsset.Size() / 2, 1, Dir > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            }
        }
    }
}
