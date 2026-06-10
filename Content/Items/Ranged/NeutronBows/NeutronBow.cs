using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Neutrons;
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

        //右键用于蓄力
        public override bool AltFunctionUse(Player player) => true;

        //物品使用本身不消耗箭矢，由手持弹幕在实际放箭时自行拾取
        public override bool CanConsumeAmmo(Item ammo, Player player) => NeutronBowHeld.AmmoConsumeContext;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            //使用瞬间生成手持弹幕，它会自己接管左键开火与右键蓄力，按键全部松开后自动销毁
            int heldType = ModContent.ProjectileType<NeutronBowHeld>();
            if (player.CountProjectilesOfID(heldType) <= 0) {
                Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero, heldType, 0, 0, player.whoAmI);
            }
            return false;
        }
    }

    /// <summary>
    /// 洛希之弦的手持弹幕，由 <see cref="NeutronBow.Shoot"/> 在使用瞬间生成，只在开火与蓄力期间存活
    /// <para>左键发射重力箭矢（强制将箭矢转化为<see cref="NeutronArrow"/>）</para>
    /// <para>右键进行三级蓄力，蓄力期间搭箭点凝聚引力井，蓄满后松开右键发射三发引力箭矢</para>
    /// </summary>
    internal class NeutronBowHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Ranged + "NeutronBow";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<NeutronBow>();

        /// <summary>
        /// 弹药消耗上下文开关：物品使用本身不消耗箭矢（<see cref="NeutronBow.CanConsumeAmmo"/> 返回该值），
        /// 只有手持弹幕实际放箭时才放行消耗
        /// </summary>
        internal static bool AmmoConsumeContext { get; private set; }

        private const int MaxFrame = 7;
        private const float MaxCharge = 80f;
        //能量弦在单帧纹理（74x114）上的锚点
        private static readonly Vector2 StringTopTex = new(16f, 26f);
        private static readonly Vector2 StringBottomTex = new(16f, 90f);

        /// <summary>左键张弓计时器</summary>
        private float drawTimer;
        /// <summary>右键蓄力值 0~<see cref="MaxCharge"/></summary>
        private float charge;
        /// <summary>是否已完成蓄力</summary>
        private bool fullCharged;
        /// <summary>上一帧右键是否按下，用于检测松开瞬间</summary>
        private bool oldDownRight;
        private bool level1 = true;
        private bool level2 = true;
        private bool level3 = true;
        private int uiframe;
        /// <summary>当前帧的弹药状态预览（不消耗）</summary>
        private ShootState ammoState;

        /// <summary>当前是否仍手持着洛希之弦</summary>
        private bool ItemValid => Item != null && !Item.IsAir && Item.type == ModContent.ItemType<NeutronBow>();
        private bool MouseSafe => !Owner.CWR().UIMouseInterface && !Owner.mouseInterface;
        private bool LeftFiring => DownLeft && MouseSafe;
        private bool RightCharging => DownRight && !DownLeft && MouseSafe;
        public override bool CanFire => DownLeft || DownRight;
        /// <summary>蓄力等级对应的搭箭数量</summary>
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
            //按键全部松开且没有待结算的蓄力（蓄满松开的发射在本帧完成）时销毁，
            //让玩家回归普通的持物状态
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
        /// <summary>
        /// 左键：张弓并发射重力箭矢
        /// </summary>
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

        /// <summary>
        /// 右键：三级蓄力，蓄满后松开发射引力箭矢
        /// </summary>
        private void UpdateRightCharge() {
            if (RightCharging && ammoState.HasAmmo) {
                if (charge < MaxCharge) {
                    charge += 0.5f;

                    if (charge > 8 && level1) {
                        NewText(NeutronBow.Lang1.Value, 0);
                        SoundEngine.PlaySound(CWRSound.loadTheRounds with { Pitch = -0.3f, Volume = 0.6f }, Projectile.Center);
                        level1 = false;
                    }
                    if (charge > 30 && level2) {
                        NewText(NeutronBow.Lang2.Value, 60);
                        SoundEngine.PlaySound(CWRSound.loadTheRounds with { Pitch = -0.2f, Volume = 0.7f }, Projectile.Center);
                        level2 = false;
                    }
                    if (charge > 60 && level3) {
                        NewText(NeutronBow.Lang3.Value, 120);
                        SoundEngine.PlaySound(CWRSound.loadTheRounds with { Pitch = -0.1f, Volume = 0.8f }, Projectile.Center);
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

            //松开右键的瞬间：蓄满则发射
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

        /// <summary>
        /// 拾取并消耗一发箭矢弹药，返回是否成功以及合成后的伤害与击退
        /// </summary>
        private bool ConsumeAmmo(out int damage, out float knockback, out int usedAmmoItemId) {
            bool dontConsume = Owner.IsRangedAmmoFreeThisShot(new Item(ammoState.UseAmmoItemType));
            AmmoConsumeContext = true;
            bool hasAmmo = Owner.PickAmmo(Item, out _, out _, out damage, out knockback, out usedAmmoItemId, dontConsume);
            AmmoConsumeContext = false;
            return hasAmmo;
        }

        /// <summary>
        /// 发射一发重力箭矢（左键）
        /// </summary>
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

        /// <summary>
        /// 发射三发引力箭矢（右键蓄满）
        /// </summary>
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
        /// <summary>
        /// 更新弓的位置与旋转，使其紧贴玩家中心、指向鼠标
        /// </summary>
        private void UpdatePose() {
            Projectile.rotation = ToMouseA;
            Owner.ChangeDir(ToMouse.X >= 0 ? 1 : -1);
            Projectile.Center = Owner.GetPlayerStabilityCenter() + Projectile.rotation.ToRotationVector2() * HoldDistance;
        }

        /// <summary>
        /// 设置玩家手臂：后手持弓指向瞄准方向，前手随张弓/蓄力进度向后拉弦
        /// </summary>
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
        /// <summary>
        /// 把单帧纹理像素坐标转换为世界坐标（考虑旋转、缩放与垂直翻转）
        /// </summary>
        private Vector2 TexPosToWorld(Vector2 texPos) {
            Vector2 frameSize = new(TextureValue.Width, TextureValue.Height / (float)MaxFrame);
            Vector2 offset = texPos - frameSize / 2f;
            if (DirSign < 0) {
                offset.Y = -offset.Y;
            }
            return Projectile.Center + offset.RotatedBy(Projectile.rotation) * Projectile.scale;
        }

        /// <summary>
        /// 获取搭箭点（能量弦中点被拉开后的位置）的世界坐标
        /// </summary>
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

        /// <summary>
        /// 绘制能量弓弦：蓝紫色光线连接上下弓臂与搭箭点
        /// </summary>
        private void DrawEnergyString() {
            Vector2 top = TexPosToWorld(StringTopTex);
            Vector2 bottom = TexPosToWorld(StringBottomTex);
            Vector2 nock = GetNockWorldPos();

            //A为0的颜色在AlphaBlend下表现为加色，适合能量体
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

        /// <summary>
        /// 绘制搭在弦上的重力箭矢，数量随蓄力等级增加
        /// </summary>
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

        /// <summary>
        /// 蓄力时在搭箭点绘制引力井着色器特效
        /// </summary>
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
