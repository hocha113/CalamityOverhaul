using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.UIs.SupertableUIs;
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

namespace CalamityOverhaul.Content.Items.Ranged.NeutronBows
{
    internal class NeutronBow : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "NeutronBow";
        public static LocalizedText Lang1;
        public static LocalizedText Lang2;
        public static LocalizedText Lang3;
        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 7));
            Lang1 = this.GetLocalization(nameof(Lang1), () => "Trapping gravity");
            Lang2 = this.GetLocalization(nameof(Lang2), () => "Is making gravity yield");
            Lang3 = this.GetLocalization(nameof(Lang3), () => "Finished!!");
        }

        public override void SetDefaults() {
            Item.width = Item.height = 54;
            Item.damage = 152;
            Item.useAnimation = Item.useTime = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.autoReuse = true;
            Item.knockBack = 2.5f;
            Item.shootSpeed = 16;
            Item.useAmmo = AmmoID.Arrow;
            Item.rare = ItemRarityID.Red;
            Item.DamageType = DamageClass.Ranged;
            Item.value = Item.buyPrice(13, 33, 75, 0);
            Item.crit = 20;
            Item.shoot = ModContent.ProjectileType<NeutronBowHeld>();
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_NeutronBow;
        }

        //右键蓄力
        public override bool AltFunctionUse(Player player) => true;

        //放箭时由手持弹幕拾取弹药
        public override bool CanConsumeAmmo(Item ammo, Player player) => NeutronBowHeld.AmmoConsumeContext;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            //生成手持弹幕接管左右键，全松键后自毁
            int heldType = ModContent.ProjectileType<NeutronBowHeld>();
            if (player.CountProjectilesOfID(heldType) <= 0) {
                Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero, heldType, 0, 0, player.whoAmI);
            }
            return false;
        }
    }

    /// 洛希之弦手持弹幕：<see cref="NeutronBow.Shoot"/> 生成，开火/蓄力期存活
    /// 左键重力箭(强制 <see cref="NeutronArrow"/>)，右键三级蓄力松手射三发引力箭
    internal class NeutronBowHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Ranged + "NeutronBow";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<NeutronBow>();

        /// 弹药门控(仅放箭时消耗)
        internal static bool AmmoConsumeContext { get; private set; }

        private const int MaxFrame = 7;
        private const float MaxCharge = 80f;
        //能量弦锚点(单帧纹理 74×114)
        private static readonly Vector2 StringTopTex = new(16f, 26f);
        private static readonly Vector2 StringBottomTex = new(16f, 90f);

        /// 左键张弓计时
        private float drawTimer;
        /// 右键蓄力 0~MaxCharge
        private float charge;
        /// 蓄力完成
        private bool fullCharged;
        /// 上帧右键(检松开)
        private bool oldDownRight;
        private bool level1 = true;
        private bool level2 = true;
        private bool level3 = true;
        private int uiframe;
        /// 弹药预览(不消耗)
        private ShootState ammoState;

        /// 仍手持洛希之弦
        private bool ItemValid => Item != null && !Item.IsAir && Item.type == ModContent.ItemType<NeutronBow>();
        private bool MouseSafe => !Owner.mouseInterface;
        private bool LeftFiring => DownLeft && MouseSafe;
        private bool RightCharging => DownRight && !DownLeft && MouseSafe;
        public override bool CanFire => DownLeft || DownRight;
        /// 蓄力等级搭箭数
        private int ArrowDrawNum => charge > 60 ? 3 : charge > 30 ? 2 : 1;
        private float HoldDistance => 28;
        private float DrawProgress => LeftFiring ? MathHelper.Clamp(drawTimer / Item.useTime, 0f, 1f) : MathHelper.Clamp(charge / MaxCharge, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 54;
            Projectile.height = 54;
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

            if (CanFire && ammoState.HasAmmo) {
                VaultUtils.ClockFrame(ref Projectile.frame, 2, MaxFrame - 1);
                VaultUtils.ClockFrame(ref uiframe, 5, MaxFrame - 1);
            }
            else {
                Projectile.frame = 0;
                uiframe = 0;
            }

            UpdateLeftFire();
            UpdateRightCharge();

            oldDownRight = DownRight;
        }

        #region 行为逻辑
        /// 左键张弓射重力箭
        private void UpdateLeftFire() {
            if (!LeftFiring || !ammoState.HasAmmo) {
                if (!RightCharging) {
                    drawTimer = 0;
                }
                return;
            }

            drawTimer += Owner.GetWeaponAttackSpeed(Item);
            if (drawTimer >= Item.useTime) {
                FireNeutronArrow();
                drawTimer = 0;
            }
        }

        /// 右键三级蓄力，满蓄松手射引力箭
        private void UpdateRightCharge() {
            if (RightCharging && ammoState.HasAmmo) {
                if (charge < MaxCharge) {
                    charge += 0.5f;

                    if (charge > 8 && level1) {
                        NewText(NeutronBow.Lang1.Value, 0);
                        SoundEngine.PlaySound(CWRSound.LoadTheRounds with { Pitch = -0.3f, Volume = 0.6f }, Projectile.Center);
                        level1 = false;
                    }
                    if (charge > 30 && level2) {
                        NewText(NeutronBow.Lang2.Value, 60);
                        SoundEngine.PlaySound(CWRSound.LoadTheRounds with { Pitch = -0.2f, Volume = 0.7f }, Projectile.Center);
                        level2 = false;
                    }
                    if (charge > 60 && level3) {
                        NewText(NeutronBow.Lang3.Value, 120);
                        SoundEngine.PlaySound(CWRSound.LoadTheRounds with { Pitch = -0.1f, Volume = 0.8f }, Projectile.Center);
                        level3 = false;
                        charge = MaxCharge;
                    }
                }

                if (charge >= MaxCharge && !fullCharged) {
                    fullCharged = true;
                    Vector2 aimVel = Projectile.rotation.ToRotationVector2() * Item.shootSpeed;
                    for (int i = 0; i < 3; i++) {
                        PRTLoader.NewParticle<PRT_LonginusWave>(Projectile.Center, aimVel, Color.BlueViolet, 0.62f)
                            .Configure(new Vector2(1.5f, 3f) * (0.8f - i * 0.1f), aimVel.ToRotation(), 0.12f, 60, Projectile);
                    }
                }
                return;
            }

            //右键松开且满蓄则发射
            if (oldDownRight && !DownRight && fullCharged) {
                FireGravityArrows();
            }

            if (!RightCharging) {
                charge = 0;
                fullCharged = false;
                level1 = level2 = level3 = true;
            }
        }

        private void NewText(string key, int offsetY = 0) {
            Rectangle rectext = Owner.Hitbox;
            rectext.Y -= offsetY;
            CombatText.NewText(rectext, new Color(155, 200, 100 + offsetY), key, true);
        }

        /// 拾取消耗一发箭，输出伤害与击退
        private bool ConsumeAmmo(out int damage, out float knockback, out int usedAmmoItemId) {
            bool dontConsume = Owner.IsRangedAmmoFreeThisShot(new Item(ammoState.UseAmmoItemType));
            AmmoConsumeContext = true;
            bool hasAmmo = Owner.PickAmmo(Item, out _, out _, out damage, out knockback, out usedAmmoItemId, dontConsume);
            AmmoConsumeContext = false;
            return hasAmmo;
        }

        /// 左键射一发重力箭
        private void FireNeutronArrow() {
            SoundEngine.PlaySound(SoundID.Item5, Projectile.Center);

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            if (!ConsumeAmmo(out int damage, out float knockback, out int usedAmmoItemId)) {
                return;
            }

            Vector2 velocity = Projectile.rotation.ToRotationVector2() * ammoState.ShootSpeed;
            EntitySource_ItemUse_WithAmmo source = new(Owner, Item, usedAmmoItemId, "CWRBow");
            int proj = Projectile.NewProjectile(source, Projectile.Center, velocity
                , ModContent.ProjectileType<NeutronArrow>(), damage, knockback, Owner.whoAmI);
            Main.projectile[proj].SetArrowRot();

            NetUpdate();
        }

        /// 右键满蓄射三发引力箭
        private void FireGravityArrows() {
            SoundEngine.PlaySound(CWRSound.Gun_Magnum_Shoot with { Pitch = 0.7f, Volume = 0.6f }, Projectile.Center);
            Owner.CWR().GetScreenShake(6.2f);

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            if (!ConsumeAmmo(out int damage, out float knockback, out int usedAmmoItemId)) {
                return;
            }

            Vector2 velocity = Projectile.rotation.ToRotationVector2() * ammoState.ShootSpeed;
            EntitySource_ItemUse_WithAmmo source = new(Owner, Item, usedAmmoItemId, "CWRBow");
            for (int i = 0; i < 3; i++) {
                int proj = Projectile.NewProjectile(source, Projectile.Center, velocity.RotatedBy((-1 + i) * 0.25f)
                    , ModContent.ProjectileType<EXNeutronArrow>(), damage * (i == 1 ? 5 : 3), knockback, Owner.whoAmI);
                Main.projectile[proj].SetArrowRot();
            }

            NetUpdate();
        }
        #endregion

        #region 姿态
        /// 更新弓位旋转贴玩家指向鼠标
        private void UpdatePose() {
            Projectile.rotation = ToMouseA;
            Owner.ChangeDir(ToMouse.X >= 0 ? 1 : -1);
            Projectile.Center = Owner.GetPlayerStabilityCenter() + Projectile.rotation.ToRotationVector2() * HoldDistance;
        }

        /// 后手持弓瞄准，前手随张弓/蓄力拉弦
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
        /// 纹理像素坐标转世界坐标
        private Vector2 TexPosToWorld(Vector2 texPos) {
            Vector2 frameSize = new(TextureValue.Width, TextureValue.Height / (float)MaxFrame);
            Vector2 offset = texPos - frameSize / 2f;
            if (DirSign < 0) {
                offset.Y = -offset.Y;
            }
            return Projectile.Center + offset.RotatedBy(Projectile.rotation) * Projectile.scale;
        }

        /// 搭箭点世界坐标
        private Vector2 GetNockWorldPos() {
            Vector2 stringMid = TexPosToWorld((StringTopTex + StringBottomTex) / 2f);
            return stringMid - Projectile.rotation.ToRotationVector2() * DrawProgress * 12f * Projectile.scale;
        }

        public override bool PreDraw(ref Color lightColor) {
            //蓄力条
            if (ItemValid) {
                NeutronGlaiveHeldAlt.DrawBar(Owner, charge / 60f * MaxCharge, uiframe);
            }

            //蓄力时的引力井（绘制在弓体之下）
            DrawGravityWell();

            //能量弓弦
            DrawEnergyString();

            //弓体
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(TextureValue, drawPos, TextureValue.GetRectangle(Projectile.frame, MaxFrame)
                , Color.White, Projectile.rotation, TextureValue.GetOrig(MaxFrame)
                , Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);

            //搭在弦上的重力箭矢
            DrawNockedArrows();
            return false;
        }

        private static void DrawLine(Vector2 start, Vector2 end, Color color, float thickness) {
            Vector2 toEnd = end - start;
            float length = toEnd.Length();
            if (length < 1f) {
                return;
            }
            Main.EntitySpriteDraw(TextureAssets.MagicPixel.Value, start - Main.screenPosition, new Rectangle(0, 0, 1, 1)
                , color, toEnd.ToRotation(), new Vector2(0, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0);
        }

        /// 绘制蓝紫能量弓弦
        private void DrawEnergyString() {
            Vector2 top = TexPosToWorld(StringTopTex);
            Vector2 bottom = TexPosToWorld(StringBottomTex);
            Vector2 nock = GetNockWorldPos();

            //AlphaBlend 下 A=0 呈加色
            Color outer = new Color(110, 70, 255, 0) * 0.8f;
            Color inner = new Color(220, 210, 255, 0) * 0.9f;
            DrawLine(top, nock, outer, 3f);
            DrawLine(nock, bottom, outer, 3f);
            DrawLine(top, nock, inner, 1f);
            DrawLine(nock, bottom, inner, 1f);

            //搭箭点的能量节点
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f);
            Main.EntitySpriteDraw(glow, nock - Main.screenPosition, null, new Color(140, 100, 255, 0) * 0.7f
                , 0f, glow.Size() / 2f, (0.32f + DrawProgress * 0.2f) * pulse, SpriteEffects.None);
        }

        /// 绘制搭弦重力箭(数量随蓄力)
        private void DrawNockedArrows() {
            bool leftDrawing = LeftFiring && drawTimer > 3;
            bool rightDrawing = RightCharging && charge > 1;
            if ((!leftDrawing && !rightDrawing) || !ammoState.HasAmmo) {
                return;
            }

            int arrowProjType = ModContent.ProjectileType<NeutronArrow>();
            Main.instance.LoadProjectile(arrowProjType);
            Texture2D arrowTex = TextureAssets.Projectile[arrowProjType].Value;

            Vector2 nock = GetNockWorldPos();
            Vector2 normal = Projectile.rotation.ToRotationVector2().GetNormalVector();
            float fan = 1f - DrawProgress * 0.4f;

            void drawArrow(float offsetRot, Vector2 offsetPos) => Main.EntitySpriteDraw(arrowTex
                , nock + offsetPos - Main.screenPosition, null, Color.White
                , Projectile.rotation + MathHelper.PiOver2 + MathHelper.Pi + offsetRot
                , new Vector2(arrowTex.Width / 2f, 0), Projectile.scale, SpriteEffects.FlipVertically);

            int num = rightDrawing ? ArrowDrawNum : 1;
            switch (num) {
                case 2:
                    drawArrow(0.3f * fan, normal * -1f);
                    drawArrow(-0.3f * fan, normal * 1f);
                    break;
                case 3:
                    drawArrow(0.45f * fan, normal * -1.5f);
                    drawArrow(0f, Vector2.Zero);
                    drawArrow(-0.45f * fan, normal * 1.5f);
                    break;
                default:
                    drawArrow(0f, Vector2.Zero);
                    break;
            }
        }

        /// 搭箭点引力井着色器
        private void DrawGravityWell() {
            float intensity = MathHelper.Clamp(charge / MaxCharge, 0f, 1f);
            if (!RightCharging || intensity < 0.05f) {
                return;
            }

            Effect shader = EffectLoader.NeutronGravityWell?.Value;
            Texture2D canvas = CWRAsset.Placeholder_White.Value;
            Texture2D noise = CWRAsset.PerlinNoise.Value;
            if (shader == null || canvas == null || noise == null) {
                return;
            }

            float drawSize = 90f + intensity * 110f;
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.02f);
            shader.Parameters["intensity"]?.SetValue(intensity);
            shader.Parameters["fadeAlpha"]?.SetValue(1f);
            shader.Parameters["coreColor"]?.SetValue(new Vector3(0.78f, 0.82f, 1f));
            shader.Parameters["diskColor"]?.SetValue(new Vector3(0.42f, 0.26f, 0.95f));
            shader.Parameters["edgeColor"]?.SetValue(new Vector3(0.12f, 0.1f, 0.5f));
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(canvas, GetNockWorldPos() - Main.screenPosition, null, Color.White,
                0f, canvas.Size() * 0.5f, new Vector2(drawSize, drawSize), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
        }
        #endregion
    }
}
