using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee.StormGoddessSpears;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// 荒芜弓：按住左键自动张弓，箭亡时爆发连锁沙金电流
    internal class BarrenBow : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "BarrenBow";
        public override void SetDefaults() {
            Item.damage = 28;
            Item.width = 32;
            Item.height = 58;
            Item.useTime = 15;
            Item.useAnimation = 15;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.DamageType = DamageClass.Ranged;
            Item.channel = true;
            Item.autoReuse = true;
            Item.shootSpeed = 12f;
            Item.useAmmo = AmmoID.Arrow;
            Item.value = Item.buyPrice(0, 2, 15, 0);
            Item.rare = CWRID.Rarity_PureGreen;
            Item.shoot = ModContent.ProjectileType<BarrenBowHeld>();
        }

        //放箭时由手持弹幕拾取弹药，物品使用不扣箭
        public override bool CanConsumeAmmo(Item ammo, Player player) => BarrenBowHeld.AmmoConsumeContext;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            //生成手持弹幕接管开火，松手无在途箭则自毁
            int heldType = ModContent.ProjectileType<BarrenBowHeld>();
            if (player.CountProjectilesOfID(heldType) <= 0) {
                Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero, heldType, 0, 0, player.whoAmI);
            }
            return false;
        }

        public override void AddRecipes() {
            if (CWRID.Item_LunarianBow > 0) {
                CreateRecipe()
                .AddIngredient(ItemID.LightShard, 2)
                .AddIngredient(ItemID.AntlionMandible, 5)
                .AddIngredient(ItemID.HellwingBow)
                .AddIngredient(CWRID.Item_LunarianBow)
                .AddTile(TileID.Anvils)
                .Register();
            }
            else {
                CreateRecipe()
                .AddIngredient(ItemID.LightShard, 2)
                .AddIngredient(ItemID.AntlionMandible, 5)
                .AddIngredient(ItemID.HellwingBow)
                .AddTile(TileID.Anvils)
                .Register();
            }
        }
    }

    /// 荒芜弓手持弹幕：<see cref="BarrenBow.Shoot"/> 生成，开火期存活
    /// 瞄准张弓、追踪箭矢，箭亡处引爆电流；松键后在途箭未结算时隐形守望
    internal class BarrenBowHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Ranged + "BarrenBow";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<BarrenBow>();

        /// <summary>弹药消耗门控，仅手持放箭时放行(<see cref="BarrenBow.CanConsumeAmmo"/>)</summary>
        internal static bool AmmoConsumeContext { get; private set; }

        //弓弦锚点(纹理 56×78，弦 x=18~20)
        private static readonly Vector2 StringTopTex = new(18.5f, 21f);
        private static readonly Vector2 StringBottomTex = new(18.5f, 60f);
        /// <summary>张弓计时器，达到 <see cref="Item.useTime"/> 时放箭</summary>
        private float drawTimer;
        /// <summary>当前帧的弹药状态预览（不消耗）</summary>
        private ShootState ammoState;
        /// <summary>被追踪的箭矢，消亡时在其位置引爆荒芜电流</summary>
        private readonly List<TrackedArrow> trackedArrows = [];

        private struct TrackedArrow
        {
            public int WhoAmI;
            public int Identity;
            public int BurstDamage;
            public Vector2 LastPos;
        }

        /// <summary>张弓进度 0~1</summary>
        private float DrawProgress => MathHelper.Clamp(drawTimer / Item.useTime, 0f, 1f);
        /// <summary>当前是否仍手持着荒芜弓</summary>
        private bool ItemValid => Item != null && !Item.IsAir && Item.type == ModContent.ItemType<BarrenBow>();
        /// <summary>是否处于开火交互中：按住左键、手持正确物品且鼠标未悬停UI</summary>
        private bool Engaged => ItemValid && DownLeft && !Owner.mouseInterface;
        public override bool CanFire => DownLeft;
        private float HoldDistance => TextureValue.Width / 2f - 8;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 58;
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
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return false;
            }
            //松键/切物后仅有在途箭待结算时守望存活
            if ((!DownLeft || !ItemValid) && trackedArrows.Count == 0) {
                Projectile.Kill();
                return false;
            }
            return true;
        }

        public override void AI() {
            Projectile.timeLeft = 2;

            if (!Engaged) {
                //守望：不接管姿态/绘制，仅追踪箭矢
                drawTimer = 0;
                Projectile.Center = Owner.GetPlayerStabilityCenter();
                UpdateTrackedArrows();
                return;
            }

            SetHeld();
            ammoState = Owner.GetShootState();
            UpdatePose();
            UpdateArms();

            //张弓与放箭
            if (ammoState.HasAmmo) {
                drawTimer += Owner.GetWeaponAttackSpeed(Item);
                SpawnDrawDust();
                if (drawTimer >= Item.useTime) {
                    Fire();
                    drawTimer = 0;
                }
            }
            else {
                drawTimer = 0;
            }

            UpdateTrackedArrows();
        }

        /// 更新弓位旋转，贴玩家中心指向鼠标
        private void UpdatePose() {
            Projectile.rotation = ToMouseA;
            Owner.ChangeDir(ToMouse.X >= 0 ? 1 : -1);
            Projectile.Center = Owner.GetPlayerStabilityCenter() + Projectile.rotation.ToRotationVector2() * HoldDistance;
        }

        /// 后手持弓瞄准，前手随张弓进度拉弦
        private void UpdateArms() {
            float holdArmRot = Projectile.rotation - MathHelper.PiOver2 * SafeGravDir;
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, holdArmRot);

            //拉弦手：张弓越满，手臂越收拢
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

        /// 张弓搭箭点沙金电火花
        private void SpawnDrawDust() {
            if (Main.dedServ || DrawProgress < 0.5f || !Main.rand.NextBool(3)) {
                return;
            }
            Vector2 nock = GetNockWorldPos();
            Dust dust = Dust.NewDustPerfect(nock + Main.rand.NextVector2Circular(6f, 6f), DustID.GoldFlame,
                Main.rand.NextVector2Circular(0.6f, 0.6f), 100, default, 0.8f + DrawProgress * 0.5f);
            dust.noGravity = true;
            Lighting.AddLight(nock, new Vector3(0.45f, 0.36f, 0.12f) * DrawProgress);
        }

        /// 拾取并消耗一发箭
        private bool PickArrow(out int shootType, out float speed, out int damage, out float knockback, out int usedAmmoItemId) {
            bool dontConsume = Owner.IsRangedAmmoFreeThisShot(new Item(ammoState.UseAmmoItemType));
            AmmoConsumeContext = true;
            bool hasAmmo = Owner.PickAmmo(Item, out shootType, out speed, out damage, out knockback, out usedAmmoItemId, dontConsume);
            AmmoConsumeContext = false;
            return hasAmmo;
        }

        /// 放箭并纳入电流追踪
        private void Fire() {
            SoundEngine.PlaySound(SoundID.Item5, Projectile.Center);

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            if (!PickArrow(out int shootType, out float speed, out int damage, out float knockback, out int usedAmmoItemId)) {
                return;
            }

            Vector2 velocity = Projectile.rotation.ToRotationVector2() * speed;
            EntitySource_ItemUse_WithAmmo source = new(Owner, Item, usedAmmoItemId, "CWRBow");
            int proj = Projectile.NewProjectile(source, Projectile.Center, velocity
                , shootType, damage, knockback, Owner.whoAmI);
            Main.projectile[proj].rotation = velocity.ToRotation() + MathHelper.PiOver2;

            trackedArrows.Add(new TrackedArrow {
                WhoAmI = proj,
                Identity = Main.projectile[proj].identity,
                BurstDamage = damage / 2,
                LastPos = Projectile.Center
            });
            //防止极端情况下列表无限膨胀
            if (trackedArrows.Count > 30) {
                trackedArrows.RemoveAt(0);
            }

            NetUpdate();
        }

        /// 追踪箭矢，消亡处爆发荒芜电流
        private void UpdateTrackedArrows() {
            if (!Projectile.IsOwnedByLocalPlayer() || trackedArrows.Count == 0) {
                return;
            }

            for (int i = trackedArrows.Count - 1; i >= 0; i--) {
                TrackedArrow tracked = trackedArrows[i];
                Projectile arrow = Main.projectile[tracked.WhoAmI];
                if (arrow.active && arrow.identity == tracked.Identity) {
                    tracked.LastPos = arrow.Center;
                    trackedArrows[i] = tracked;
                    continue;
                }
                BurstBarrenCurrent(tracked.LastPos, tracked.BurstDamage);
                trackedArrows.RemoveAt(i);
            }
        }

        /// 指定位置爆发荒芜电流
        private void BurstBarrenCurrent(Vector2 pos, int damage) {
            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.5f, Pitch = 0.25f, PitchVariance = 0.2f }, pos);

            Projectile.NewProjectile(Projectile.GetSource_FromThis(), pos, Vector2.Zero
                , ModContent.ProjectileType<BarrenPulseProj>(), 0, 0, Owner.whoAmI);

            for (int i = 0; i < 2; i++) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), pos, VaultUtils.RandVr(6, 9)
                    , ModContent.ProjectileType<BarrenArc>(), damage, 0, Owner.whoAmI);
            }
        }

        /// <summary>
        /// 把纹理像素坐标转换为世界坐标（考虑旋转、缩放与垂直翻转）
        /// </summary>
        private Vector2 TexPosToWorld(Vector2 texPos) {
            Vector2 offset = texPos - TextureValue.Size() / 2f;
            if (DirSign < 0) {
                offset.Y = -offset.Y;
            }
            return Projectile.Center + offset.RotatedBy(Projectile.rotation) * Projectile.scale;
        }

        /// <summary>
        /// 获取搭箭点（弦中点被拉开后的位置）的世界坐标
        /// </summary>
        private Vector2 GetNockWorldPos() {
            Vector2 stringMid = TexPosToWorld((StringTopTex + StringBottomTex) / 2f);
            return stringMid - Projectile.rotation.ToRotationVector2() * DrawProgress * 10f * Projectile.scale;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!Engaged) {
                return false;//守望状态不绘制
            }
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            DrawBowstring(lightColor);
            DrawBowBody(drawPos, lightColor);
            DrawNockedArrow(lightColor);
            DrawChargeGlow();
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
        /// 绘制动态弓弦：上下锚点到搭箭点的两段直线
        /// </summary>
        private void DrawBowstring(Color lightColor) {
            Vector2 top = TexPosToWorld(StringTopTex);
            Vector2 bottom = TexPosToWorld(StringBottomTex);
            Vector2 nock = GetNockWorldPos();
            Color stringColor = Color.Lerp(lightColor, Color.White, 0.3f) * 0.85f;
            DrawLine(top, nock, stringColor, 2f);
            DrawLine(nock, bottom, stringColor, 2f);
        }

        /// <summary>
        /// 绘制弓体：使用扣除着色器裁掉纹理上烘焙的静态弓弦
        /// </summary>
        private void DrawBowBody(Vector2 drawPos, Color lightColor) {
            Effect effect = EffectLoader.DeductDraw.Value;
            effect.Parameters["topLeft"].SetValue(new Vector2(18, 21));
            effect.Parameters["width"].SetValue(2f);
            effect.Parameters["height"].SetValue(40f);
            effect.Parameters["drawColor"].SetValue(lightColor.ToVector4());
            effect.Parameters["textureSize"].SetValue(TextureValue.Size());

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);

            Main.EntitySpriteDraw(TextureValue, drawPos, null, lightColor
                , Projectile.rotation, TextureValue.Size() / 2f, Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>
        /// 绘制搭在弦上的箭矢
        /// </summary>
        private void DrawNockedArrow(Color lightColor) {
            if (drawTimer < 3) {
                return;
            }
            int ammoItemType = ammoState.UseAmmoItemType;
            if (ammoItemType <= ItemID.None || ammoItemType >= TextureAssets.Item.Length) {
                return;
            }

            Main.instance.LoadItem(ammoItemType);
            Texture2D arrowTex = TextureAssets.Item[ammoItemType].Value;
            //无限类弹药（如无尽箭袋）显示其对应的实体箭矢
            Item ammoItem = new(ammoItemType);
            if (!ammoItem.consumable) {
                int showType = ItemID.WoodenArrow;
                if (VaultUtils.ProjectileToSafeAmmoMap.TryGetValue(ammoItem.shoot, out int mapped)) {
                    showType = mapped;
                }
                Main.instance.LoadItem(showType);
                arrowTex = TextureAssets.Item[showType].Value;
            }

            Vector2 nock = GetNockWorldPos();
            Main.EntitySpriteDraw(arrowTex, nock - Main.screenPosition, null, lightColor
                , Projectile.rotation + MathHelper.PiOver2, new Vector2(arrowTex.Width / 2f, arrowTex.Height)
                , Projectile.scale, SpriteEffects.FlipVertically);
        }

        /// <summary>
        /// 满弦时在搭箭点绘制荒芜能量光辉
        /// </summary>
        private void DrawChargeGlow() {
            float progress = DrawProgress;
            if (progress < 0.55f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float strength = (progress - 0.55f) / 0.45f;
            Color glowColor = new Color(255, 205, 110, 0) * (0.35f + strength * 0.4f);
            Vector2 nock = GetNockWorldPos();
            Main.EntitySpriteDraw(glow, nock - Main.screenPosition, null, glowColor
                , 0f, glow.Size() / 2f, 0.5f + strength * 0.3f, SpriteEffects.None);
        }
    }

    /// <summary>
    /// 荒芜电流：沙金色的连锁电弧，继承风暴电弧的连锁行为
    /// </summary>
    internal class BarrenArc : StormArc
    {
        public override void SetDefaults() {
            base.SetDefaults();
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.tileCollide = false;
        }

        public override Color GetLightningColor(float factor) {
            float colorInterpolant = (float)Math.Sin(Projectile.identity / 3f + factor * 20f + Main.GlobalTimeWrappedHourly * 1.1f) * 0.5f + 0.5f;
            return VaultUtils.MultiStepColorLerp(colorInterpolant, new Color(255, 190, 80), new Color(255, 226, 148), Color.White);
        }
    }

    /// <summary>
    /// 荒芜电涌的视觉冲击波，无伤害，使用 <see cref="EffectLoader.BarrenPulse"/> 着色器绘制
    /// </summary>
    internal class BarrenPulseProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private ref float Timer => ref Projectile.ai[0];
        private const int LifeTime = 24;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
        }

        public override void AI() {
            Timer++;
            float progress = Timer / LifeTime;
            Lighting.AddLight(Projectile.Center, new Vector3(0.8f, 0.62f, 0.25f) * (1f - progress));
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.BarrenPulse?.Value;
            Texture2D canvas = CWRAsset.Placeholder_White.Value;
            Texture2D noise = CWRAsset.Extra_193.Value;
            if (shader == null || canvas == null || noise == null) {
                return false;
            }

            float progress = MathHelper.Clamp(Timer / LifeTime, 0f, 1f);
            float drawSize = 90f + progress * 140f;

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.02f);
            shader.Parameters["ringProgress"]?.SetValue(progress);
            shader.Parameters["fadeAlpha"]?.SetValue(1f - progress * progress);
            shader.Parameters["coreColor"]?.SetValue(new Vector3(1f, 0.92f, 0.66f));
            shader.Parameters["midColor"]?.SetValue(new Vector3(1f, 0.72f, 0.3f));
            shader.Parameters["edgeColor"]?.SetValue(new Vector3(0.62f, 0.4f, 0.16f));
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(canvas, Projectile.Center - Main.screenPosition, null, Color.White,
                0f, canvas.Size() * 0.5f, new Vector2(drawSize, drawSize), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
