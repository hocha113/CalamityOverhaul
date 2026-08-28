using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee.Abyssrends;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Undertows
{
    /// <summary>
    /// <see cref="Undertow.Shoot"/> 生成,按键期间存活。
    /// 左键按拉弓节奏射渊棘箭,右键三段蓄力,拉满松手射渊压重箭
    /// </summary>
    internal class UndertowHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Ranged + "Undertow";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<Undertow>();

        /// <summary>弹药门控(仅放箭时消耗)</summary>
        internal static bool AmmoConsumeContext { get; private set; }

        private const float MaxCharge = 78f;
        //弦锚点(贴图 68x156,弦尖像素实测)
        private static readonly Vector2 StringTopTex = new(12f, 5f);
        private static readonly Vector2 StringBottomTex = new(12f, 151f);

        /// <summary>左键张弓计时</summary>
        private float drawTimer;
        /// <summary>右键蓄力 0~MaxCharge</summary>
        private float charge;
        /// <summary>蓄力完成</summary>
        private bool fullCharged;
        /// <summary>上帧右键(检松开)</summary>
        private bool oldDownRight;
        private bool cue1 = true;
        private bool cue2 = true;
        private bool cue3 = true;
        /// <summary>弹药预览(不消耗)</summary>
        private ShootState ammoState;

        private bool ItemValid => Item != null && !Item.IsAir && Item.type == ModContent.ItemType<Undertow>();
        private bool MouseSafe => !Owner.mouseInterface;
        private bool LeftFiring => DownLeft && MouseSafe;
        private bool RightCharging => DownRight && !DownLeft && MouseSafe;
        public override bool CanFire => DownLeft || DownRight;
        private float HoldDistance => 26f;
        private float DrawProgress => LeftFiring
            ? MathHelper.Clamp(drawTimer / Item.useTime, 0f, 1f)
            : MathHelper.Clamp(charge / MaxCharge, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 66;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.timeLeft = 2;
            Projectile.hide = false;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => false;

        public override bool PreUpdate() {
            if (!Owner.active || Owner.dead || !ItemValid) {
                Projectile.Kill();
                return false;
            }
            //全松键且无待结算蓄力时销毁
            if (!DownLeft && !DownRight && charge <= 0) {
                Projectile.Kill();
                return false;
            }
            return true;
        }

        public override void AI() {
            SetHeld();
            Projectile.timeLeft = 2;

            ammoState = Owner.GetShootState();

            UpdatePose();
            UpdateArms();
            UpdateLeftFire();
            UpdateRightCharge();

            oldDownRight = DownRight;
        }

        #region 行为
        /// <summary>左键按拉弓节奏放箭</summary>
        private void UpdateLeftFire() {
            if (!LeftFiring || !ammoState.HasAmmo) {
                if (!RightCharging) {
                    drawTimer = 0;
                }
                return;
            }

            drawTimer += Owner.GetWeaponAttackSpeed(Item);
            if (drawTimer >= Item.useTime) {
                FireSpine();
                drawTimer = 0;
            }
        }

        /// <summary>右键三段蓄力,满蓄松手射渊压重箭</summary>
        private void UpdateRightCharge() {
            if (RightCharging && ammoState.HasAmmo) {
                if (charge < MaxCharge) {
                    charge += 1f;

                    if (charge > 8f && cue1) {
                        SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.2f, Volume = 0.6f }, Projectile.Center);
                        cue1 = false;
                    }
                    if (charge > 40f && cue2) {
                        SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = 0.15f, Volume = 0.7f }, Projectile.Center);
                        cue2 = false;
                    }
                    if (charge >= MaxCharge && cue3) {
                        SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.35f, Volume = 0.9f }, Projectile.Center);
                        cue3 = false;
                    }
                }

                if (charge >= MaxCharge && !fullCharged) {
                    fullCharged = true;
                }

                //蓄力期水团向搭箭点收拢
                if (!Main.dedServ && Main.rand.NextFloat() < 0.25f + DrawProgress * 0.35f) {
                    Vector2 nock = GetNockWorldPos();
                    Vector2 dir = Main.rand.NextVector2Unit();
                    float dist = MathHelper.Lerp(52f, 18f, DrawProgress);
                    PRTLoader.NewParticle<PRT_AbyssGlob>(nock + dir * dist
                        , -dir * MathHelper.Lerp(1.6f, 4.5f, DrawProgress)
                        , Color.Lerp(AbyssrendFX.Body, AbyssrendFX.Cyan, DrawProgress * 0.5f)
                        , Main.rand.NextFloat(0.2f, 0.4f))
                        .Configure(11, 1.3f);
                }
                return;
            }

            //右键松开且满蓄则发射
            if (oldDownRight && !DownRight && fullCharged) {
                FireHeavySpine();
            }

            if (!RightCharging) {
                charge = 0;
                fullCharged = false;
                cue1 = cue2 = cue3 = true;
            }
        }

        /// <summary>拾取消耗一发箭,输出伤害与击退</summary>
        private bool ConsumeAmmo(out int damage, out float knockback, out int usedAmmoItemId) {
            bool dontConsume = Owner.IsRangedAmmoFreeThisShot(new Item(ammoState.UseAmmoItemType));
            AmmoConsumeContext = true;
            bool hasAmmo = Owner.PickAmmo(Item, out _, out _, out damage, out knockback, out usedAmmoItemId, dontConsume);
            AmmoConsumeContext = false;
            return hasAmmo;
        }

        /// <summary>左键射一发渊棘箭</summary>
        private void FireSpine() {
            SoundEngine.PlaySound(SoundID.Item5 with { Volume = 0.9f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = 0.4f, Volume = 0.45f }, Projectile.Center);

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            if (!ConsumeAmmo(out int damage, out float knockback, out int usedAmmoItemId)) {
                return;
            }

            Vector2 velocity = Projectile.rotation.ToRotationVector2() * ammoState.ShootSpeed;
            EntitySource_ItemUse_WithAmmo source = new(Owner, Item, usedAmmoItemId, "CWRBow");
            int proj = Projectile.NewProjectile(source, GetNockWorldPos(), velocity
                , ModContent.ProjectileType<UndertowSpine>(), damage, knockback, Owner.whoAmI);
            Main.projectile[proj].SetArrowRot();

            NetUpdate();
        }

        /// <summary>右键满蓄射渊压重箭</summary>
        private void FireHeavySpine() {
            SoundEngine.PlaySound(SoundID.Item96 with { Pitch = -0.15f, Volume = 0.8f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Splash with { Pitch = -0.3f, Volume = 0.7f }, Projectile.Center);
            Owner.CWR().GetScreenShake(5f);

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            if (!ConsumeAmmo(out int damage, out float knockback, out int usedAmmoItemId)) {
                return;
            }

            Vector2 velocity = Projectile.rotation.ToRotationVector2() * ammoState.ShootSpeed * 1.2f;
            EntitySource_ItemUse_WithAmmo source = new(Owner, Item, usedAmmoItemId, "CWRBow");
            int proj = Projectile.NewProjectile(source, GetNockWorldPos(), velocity
                , ModContent.ProjectileType<UndertowSpine>(), damage * 3, knockback * 1.6f, Owner.whoAmI
                , ai0: 1f);
            Main.projectile[proj].SetArrowRot();

            NetUpdate();
        }
        #endregion

        #region 姿态
        /// <summary>弓位贴玩家,旋转指向鼠标</summary>
        private void UpdatePose() {
            Projectile.rotation = ToMouseA;
            Owner.ChangeDir(ToMouse.X >= 0 ? 1 : -1);
            Projectile.Center = Owner.GetPlayerStabilityCenter() + Projectile.rotation.ToRotationVector2() * HoldDistance;
        }

        /// <summary>后手持弓瞄准,前手随张弓/蓄力拉弦</summary>
        private void UpdateArms() {
            float holdArmRot = Projectile.rotation - MathHelper.PiOver2 * SafeGravDir;
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, holdArmRot);

            float pull = DrawProgress;
            Player.CompositeArmStretchAmount stretch = Player.CompositeArmStretchAmount.Full;
            if (pull > 0.25f)
                stretch = Player.CompositeArmStretchAmount.ThreeQuarters;
            if (pull > 0.5f)
                stretch = Player.CompositeArmStretchAmount.Quarter;
            if (pull > 0.75f)
                stretch = Player.CompositeArmStretchAmount.None;
            Owner.SetCompositeArmFront(true, stretch, holdArmRot);

            Owner.itemRotation = MathHelper.WrapAngle(Projectile.rotation * Owner.direction);
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
        }
        #endregion

        #region 绘制
        /// <summary>纹理像素坐标转世界坐标</summary>
        private Vector2 TexPosToWorld(Vector2 texPos) {
            Vector2 frameSize = new(TextureValue.Width, TextureValue.Height);
            Vector2 offset = texPos - frameSize / 2f;
            if (DirSign < 0) {
                offset.Y = -offset.Y;
            }
            return Projectile.Center + offset.RotatedBy(Projectile.rotation) * Projectile.scale;
        }

        /// <summary>搭箭点世界坐标</summary>
        private Vector2 GetNockWorldPos() {
            Vector2 stringMid = TexPosToWorld((StringTopTex + StringBottomTex) / 2f);
            return stringMid - Projectile.rotation.ToRotationVector2() * DrawProgress * 13f * Projectile.scale;
        }

        public override bool PreDraw(ref Color lightColor) {
            DrawEnergyString();

            //弓体
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(TextureValue, drawPos, null
                , lightColor, Projectile.rotation, TextureValue.Size() / 2f
                , Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);

            DrawNockedArrow();
            return false;
        }

        private static void DrawLine(Vector2 start, Vector2 end, Color color, float thickness) {
            Vector2 toEnd = end - start;
            float length = toEnd.Length();
            if (length < 1f) {
                return;
            }
            Main.EntitySpriteDraw(VaultAsset.placeholder2.Value, start - Main.screenPosition, new Rectangle(0, 0, 1, 1)
                , color, toEnd.ToRotation(), new Vector2(0, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0);
        }

        /// <summary>拉弦时叠一根被拽弯的能量弦,静置时只有贴图上画好的弦</summary>
        private void DrawEnergyString() {
            float pull = DrawProgress;
            if (pull < 0.08f) {
                return;
            }
            Vector2 top = TexPosToWorld(StringTopTex);
            Vector2 bottom = TexPosToWorld(StringBottomTex);
            Vector2 nock = GetNockWorldPos();

            //AlphaBlend 下 A=0 呈加色
            Color outer = new Color(AbyssrendFX.Cyan.R, AbyssrendFX.Cyan.G, AbyssrendFX.Cyan.B, 0) * (0.55f * pull + 0.2f);
            Color inner = new Color(AbyssrendFX.Foam.R, AbyssrendFX.Foam.G, AbyssrendFX.Foam.B, 0) * (0.7f * pull + 0.2f);
            DrawLine(top, nock, outer, 3f);
            DrawLine(nock, bottom, outer, 3f);
            DrawLine(top, nock, inner, 1f);
            DrawLine(nock, bottom, inner, 1f);

            //搭箭点能量节点
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f);
            Main.EntitySpriteDraw(glow, nock - Main.screenPosition, null
                , new Color(AbyssrendFX.Cyan.R, AbyssrendFX.Cyan.G, AbyssrendFX.Cyan.B, 0) * (0.6f * pull)
                , 0f, glow.Size() / 2f, (0.2f + pull * 0.2f) * pulse, SpriteEffects.None);
        }

        /// <summary>绘制搭弦渊棘箭,重箭蓄力时更大更亮</summary>
        private void DrawNockedArrow() {
            bool leftDrawing = LeftFiring && drawTimer > 3;
            bool rightDrawing = RightCharging && charge > 1;
            if ((!leftDrawing && !rightDrawing) || !ammoState.HasAmmo) {
                return;
            }

            int arrowProjType = ModContent.ProjectileType<UndertowSpine>();
            Main.instance.LoadProjectile(arrowProjType);
            Texture2D arrowTex = TextureAssets.Projectile[arrowProjType].Value;

            Vector2 nock = GetNockWorldPos();
            float scale = Projectile.scale * (rightDrawing ? 1f + DrawProgress * 0.2f : 1f);
            Color col = rightDrawing
                ? Color.Lerp(Color.White, AbyssrendFX.Foam, DrawProgress * 0.5f)
                : Color.White;

            //箭贴图朝上,原点压在箭尾附近,箭头沿弓向前
            Main.EntitySpriteDraw(arrowTex, nock - Main.screenPosition, null, col
                , Projectile.rotation + MathHelper.PiOver2, new Vector2(arrowTex.Width / 2f, arrowTex.Height * 0.8f)
                , scale, SpriteEffects.None);
        }
        #endregion
    }
}
