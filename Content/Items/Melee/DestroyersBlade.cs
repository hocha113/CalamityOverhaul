using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.GameSystem;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// 毁灭者之刃，三段连击，DestroyerSlash.fx
    internal class DestroyersBlade : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item_Melee + "DestroyersBlade";
        [VaultLoaden(CWRConstant.Item_Melee + "DestroyersBladeGlow")]
        public static Asset<Texture2D> Glow = null;

        /// 三段连击计数
        private static int comboCounter;

        public override void SetDefaults() {
            Item.width = Item.height = 120;
            Item.damage = 190;
            Item.knockBack = 6;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.UseSound = null;
            Item.useTime = Item.useAnimation = 22;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Purple;
            Item.value = Item.buyPrice(0, 1, 60, 5);
            Item.shoot = ModContent.ProjectileType<DestroyersBladeHeld>();
            Item.shootSpeed = 15;
            Item.CWR().DeathModeItem = true;
            //noMelee 武器需要手动允许近战词缀
            ItemOverride.ItemMeleePrefixDic[Type] = true;
        }

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int combo = comboCounter % 3;
            float swingDir = comboCounter % 2 == 0 ? 1f : -1f;
            comboCounter++;
            Projectile.NewProjectile(source, player.Center, velocity, type
                , damage, knockback, player.whoAmI, combo, swingDir);
            return false;
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition, null, Color.White
                , rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }
    }

    /// 毁灭者之刃 EX，终结五连光束扇
    internal class DestroyersBladeEX : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "DestroyersBladeEX";
        [VaultLoaden(CWRConstant.Item_Melee + "DestroyersBladeEXGlow")]
        public static Asset<Texture2D> Glow = null;

        /// 三段连击计数
        private static int comboCounter;

        public override void SetDefaults() {
            Item.height = 132;
            Item.width = 134;
            Item.damage = 1090;
            Item.knockBack = 8;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.UseSound = null;
            Item.useTime = Item.useAnimation = 18;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(0, 8, 60, 5);
            Item.shoot = ModContent.ProjectileType<DestroyersBladeEXHeld>();
            Item.shootSpeed = 15;
            //noMelee 武器需要手动允许近战词缀
            ItemOverride.ItemMeleePrefixDic[Type] = true;
        }

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int combo = comboCounter % 3;
            float swingDir = comboCounter % 2 == 0 ? 1f : -1f;
            comboCounter++;
            Projectile.NewProjectile(source, player.Center, velocity, type
                , damage, knockback, player.whoAmI, combo, swingDir);
            return false;
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
           , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition, null, Color.White
                , rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<DestroyersBlade>().
                AddIngredient<SoulofMightEX>().
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }

    /// 毁灭者手持基类，三段+DestroyerSlash+Beam
    internal abstract class DestroyersBladeHeldBase : BaseHeldProj, IPrimitiveDrawable
    {
        /// 对应物品ID
        protected abstract int TargetItemID { get; }
        /// 刀身辉光贴图
        protected abstract Texture2D GlowTex { get; }
        /// EX形态(更快更大)
        protected virtual bool IsEX => false;

        /// 连击索引 0正 1反 2终结
        private ref float ComboIndex => ref Projectile.ai[0];
        /// 挥砍方向 ±1
        private ref float SwingDirAi => ref Projectile.ai[1];

        protected bool IsFinisher => ComboIndex >= 2f;

        //阶段时长(逻辑帧，攻速缩放)
        private float WindupTime => (IsFinisher ? 8f : 5f) - (IsEX ? 1f : 0f);
        private float SlashTime => (IsFinisher ? 14f : 11f) - (IsEX ? 2f : 0f);
        private float RecoverTime => (IsFinisher ? 10f : 8f) - (IsEX ? 1f : 0f);
        private float TotalTime => WindupTime + SlashTime + RecoverTime;
        //挥砍弧度
        private float SwingArc => IsFinisher ? 5.5f : 3.4f;
        //刀尖距离持握点的长度
        private float BladeReach => (IsEX ? 168f : 150f) * (IsFinisher ? 1.08f : 1f);
        //光束伤害系数
        private float BeamDamageMul => 1f;
        //光束数量
        private int BeamCount => IsFinisher ? (IsEX ? 5 : 3) : 1;

        private float elapsed;
        private float speedMul = 1f;
        private int lockedDirection = 1;
        private int swingSign = 1;
        private float startAngle;
        private float endAngle;
        private float currentRotation;
        private float lastRotation;
        private bool slashSoundPlayed;
        private bool beamsFired;
        private float trailFade;

        //刀光轨迹缓存
        private const int TrailMax = 64;
        private const int TrailSubdiv = 4;
        private readonly float[] trailRot = new float[TrailMax];
        private int trailCount;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 64;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 90;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => elapsed >= WindupTime && elapsed <= WindupTime + SlashTime + 1f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CanDamage() != true) {
                return false;
            }
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            Vector2 tip = hand + currentRotation.ToRotationVector2() * BladeReach * Projectile.scale;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , hand, tip, 54f, ref collisionPoint);
        }

        public override void Initialize() {
            swingSign = Math.Sign(SwingDirAi);
            if (swingSign == 0) {
                swingSign = 1;
            }

            lockedDirection = Math.Sign(ToMouse.X);
            if (lockedDirection == 0) {
                lockedDirection = Owner.direction;
            }
            Owner.direction = lockedDirection;

            speedMul = Owner.GetWeaponAttackSpeed(Item);
            if (speedMul <= 0f) {
                speedMul = 1f;
            }

            float baseAngle = Projectile.velocity.ToRotation();
            startAngle = baseAngle - swingSign * SwingArc * 0.5f;
            endAngle = baseAngle + swingSign * SwingArc * 0.5f;
            currentRotation = lastRotation = startAngle;

            if (IsFinisher) {
                Projectile.damage = (int)(Projectile.damage * 1.35f);
                Projectile.scale = IsEX ? 1.2f : 1.12f;
                if (!VaultUtils.isServer) {
                    //终结斩起手的机械蓄能声
                    SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.6f, Pitch = -0.4f, MaxInstances = 3 }, Owner.Center);
                }
            }
            else if (IsEX) {
                Projectile.scale = 1.06f;
            }
        }

        public override void AI() {
            if (Item.type != TargetItemID) {
                Projectile.Kill();
                return;
            }
            if (elapsed >= TotalTime) {
                Projectile.Kill();
                return;
            }

            lastRotation = currentRotation;
            float slashEnd = WindupTime + SlashTime;

            if (elapsed < WindupTime) {
                //蓄力回拉
                float t = elapsed / WindupTime;
                currentRotation = startAngle - swingSign * 0.25f * MathF.Sin(t * MathHelper.PiOver2);
                trailFade = 0f;
            }
            else if (elapsed < slashEnd) {
                //液压驱动般的 ease-out 重斩
                float t = (elapsed - WindupTime) / SlashTime;
                float eased = 1f - MathF.Pow(1f - t, IsFinisher ? 4.4f : 3.5f);
                currentRotation = MathHelper.Lerp(startAngle, endAngle, eased);
                trailFade = 1f;

                if (!slashSoundPlayed) {
                    slashSoundPlayed = true;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.5f }, Owner.Center);
                        if (IsFinisher) {
                            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f, Volume = 0.95f }, Owner.Center);
                        }
                    }
                }

                PushTrailSamples();

                if (!beamsFired && t >= 0.36f) {
                    beamsFired = true;
                    FireBeams();
                }

                //刀刃熔渣火花
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    Vector2 along = Owner.GetPlayerStabilityCenter()
                        + currentRotation.ToRotationVector2() * Main.rand.NextFloat(BladeReach * 0.5f, BladeReach);
                    Vector2 tangent = currentRotation.ToRotationVector2().RotatedBy(swingSign * MathHelper.PiOver2);
                    PRTLoader.NewParticle<PRT_Spark>(along, tangent * Main.rand.NextFloat(3f, 6f)
                        , Color.Lerp(Color.Red, Color.OrangeRed, Main.rand.NextFloat())
                        , Main.rand.NextFloat(0.6f, 1f)).Configure(false, 9);
                }
            }
            else {
                //收势
                float t = (elapsed - slashEnd) / RecoverTime;
                currentRotation = endAngle;
                trailFade = 1f - t;
                PushTrailSamples();
            }

            UpdatePlayerPose();
            Lighting.AddLight(Owner.GetPlayerStabilityCenter() + currentRotation.ToRotationVector2() * BladeReach * 0.7f
                , new Vector3(1f, 0.2f, 0.1f) * 0.7f);
            elapsed += speedMul;
        }

        private void PushTrailSamples() {
            for (int s = TrailSubdiv - 1; s >= 0; s--) {
                float rot = MathHelper.Lerp(currentRotation, lastRotation, s / (float)TrailSubdiv);
                for (int i = Math.Min(trailCount, TrailMax - 1); i > 0; i--) {
                    trailRot[i] = trailRot[i - 1];
                }
                trailRot[0] = rot;
                if (trailCount < TrailMax) {
                    trailCount++;
                }
            }
        }

        private void FireBeams() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            int count = BeamCount;
            float spread = count > 1 ? 0.46f : 0f;
            Vector2 spawnPos = Owner.GetPlayerStabilityCenter() + currentRotation.ToRotationVector2() * BladeReach * 0.5f;
            for (int i = 0; i < count; i++) {
                float offset = count > 1 ? MathHelper.Lerp(-spread, spread, i / (float)(count - 1)) : 0f;
                Vector2 velocity = UnitToMouseV * Item.shootSpeed;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), spawnPos, velocity
                    , ModContent.ProjectileType<DestroyersBeam>(), (int)(Projectile.damage * BeamDamageMul)
                    , Projectile.knockBack / 2, Projectile.owner, ai1: IsEX ? 1f : 0f);
            }
        }

        private void UpdatePlayerPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = lockedDirection;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = currentRotation;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, currentRotation - MathHelper.PiOver2);
            Projectile.Center = Owner.GetPlayerStabilityCenter() + currentRotation.ToRotationVector2() * BladeReach * 0.55f;
            Projectile.timeLeft = 90;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                //金属撞击的火花飞溅
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 3 }, target.Center);
                for (int i = 0; i < (IsFinisher ? 8 : 4); i++) {
                    PRTLoader.NewParticle<PRT_SparkAlpha>(target.Center, Main.rand.NextVector2Circular(6f, 6f)
                        , Main.rand.NextBool() ? Color.Red : Color.OrangeRed
                        , Main.rand.NextFloat(1f, 2f)).Configure(false, Main.rand.Next(10, 18));
                }
                if (IsFinisher) {
                    Color warm = new Color(255, 90, 40);
                    PRTLoader.NewParticle<PRT_MechExplosion>(target.Center, Main.rand.NextVector2Circular(1.5f, 1.5f)
                        , warm, IsEX ? 0.9f : 0.6f).Configure(Main.rand.Next(18, 28), warm);
                }
            }

            if (IsFinisher && CWRServerConfig.Instance.ScreenVibration) {
                var modifier = new PunchCameraModifier(target.Center
                    , currentRotation.ToRotationVector2(), IsEX ? 5f : 4f, 5f, 9, 800f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureValue;
            Vector2 origin = tex.Size() / 2f;
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            float dist = BladeReach * 0.5f * Projectile.scale;

            SpriteEffects effect = lockedDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;
            //贴图刀尖指向右上(-PiOver4)，垂直翻转后指向右下(+PiOver4)
            float rotOffset = lockedDirection == -1 ? -MathHelper.PiOver4 : MathHelper.PiOver4;

            //挥砍残影
            if (elapsed >= WindupTime && elapsed <= WindupTime + SlashTime + 1f) {
                for (int i = 1; i <= 3; i++) {
                    float rot = MathHelper.Lerp(currentRotation, lastRotation, i / 4f);
                    Vector2 pos = hand + rot.ToRotationVector2() * dist - Main.screenPosition;
                    Color trailColor = new Color(255, 60, 30) * (0.32f * (1f - i / 4f));
                    trailColor.A = 0;
                    Main.EntitySpriteDraw(tex, pos, null, trailColor, rot + rotOffset, origin
                        , Projectile.scale, effect, 0);
                }
            }

            //刀身本体 + 辉光层
            Vector2 drawPos = hand + currentRotation.ToRotationVector2() * dist - Main.screenPosition;
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor, currentRotation + rotOffset, origin
                , Projectile.scale, effect, 0);
            Main.EntitySpriteDraw(GlowTex, drawPos, null, Color.White, currentRotation + rotOffset, GlowTex.Size() / 2f
                , Projectile.scale, effect, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (trailCount < 3 || trailFade <= 0.02f) {
                return;
            }
            Effect effect = EffectLoader.DestroyerSlash?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            var bars = new VertexPositionColorTexture[trailCount * 2];
            Vector2 center = Owner.GetPlayerStabilityCenter();
            float outer = (BladeReach + 12f) * Projectile.scale;
            float inner = BladeReach * 0.26f;
            for (int i = 0; i < trailCount; i++) {
                float factor = 1f - i / (float)trailCount;
                Vector2 dir = trailRot[i].ToRotationVector2();
                bars[i * 2] = new VertexPositionColorTexture((center + dir * outer).ToVector3()
                    , Color.White, new Vector2(factor, 0f));
                bars[i * 2 + 1] = new VertexPositionColorTexture((center + dir * inner).ToVector3()
                    , Color.White, new Vector2(factor, 1f));
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(trailFade);
            effect.Parameters["heatBoost"]?.SetValue(IsFinisher ? 1f : (IsEX ? 0.45f : 0.25f));
            effect.Parameters["exMode"]?.SetValue(IsEX ? 1f : 0f);
            effect.Parameters["segCount"]?.SetValue(MathF.Max(5f, SwingArc * (IsEX ? 2.6f : 2.2f)));
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars, 0, bars.Length - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }

    /// 毁灭者之刃手持挥砍
    internal class DestroyersBladeHeld : DestroyersBladeHeldBase
    {
        public override string Texture => CWRConstant.Item_Melee + "DestroyersBlade";
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<DestroyersBlade>()).DisplayName;
        protected override int TargetItemID => ModContent.ItemType<DestroyersBlade>();
        protected override Texture2D GlowTex => DestroyersBlade.Glow.Value;
    }

    /// 毁灭者之刃 EX 手持挥砍
    internal class DestroyersBladeEXHeld : DestroyersBladeHeldBase
    {
        public override string Texture => CWRConstant.Item_Melee + "DestroyersBladeEX";
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<DestroyersBladeEX>()).DisplayName;
        protected override int TargetItemID => ModContent.ItemType<DestroyersBladeEX>();
        protected override Texture2D GlowTex => DestroyersBladeEX.Glow.Value;
        protected override bool IsEX => true;
    }

    /// 毁灭者光束，DestroyerBeam.fx
    /// ai[1] 0普通 1EX
    internal class DestroyersBeam : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private bool IsEX => Projectile.ai[1] > 0f;
        private ref float Init => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 26;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.timeLeft = 300;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (Init == 0) {
                Init = 1;
                if (IsEX) {
                    Projectile.penetrate = 3;
                    Projectile.scale = 1.15f;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.4f, Pitch = 0.15f, MaxInstances = 5 }, Projectile.position);
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            //沿途散落的电火花
            if (!VaultUtils.isServer && Main.rand.NextBool(9)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f)
                    , DustID.RedTorch, -Projectile.velocity * 0.1f, 100, default, 1.1f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.2f, 0.1f) * 1.2f * Main.essScale);
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode(IsEX ? 140 : 110, SoundID.Item14 with { Volume = 0.5f, Pitch = 0.1f, MaxInstances = 5 });
            if (Main.dedServ) {
                return;
            }
            Color warm = new Color(255, 90, 40);
            PRTLoader.NewParticle<PRT_MechExplosion>(Projectile.Center, Main.rand.NextVector2Circular(1f, 1f)
                , warm, IsEX ? 0.7f : 0.45f).Configure(Main.rand.Next(16, 26), warm);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(Projectile.Center, Main.rand.NextVector2Circular(7f, 7f)
                    , Main.rand.NextBool() ? Color.Red : Color.OrangeRed
                    , Main.rand.NextFloat(1f, 2f)).Configure(false, Main.rand.Next(10, 16));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //弹头柔光与十字耀斑
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color coreColor = new Color(255, 70, 35);
            coreColor.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, coreColor, 0f, glow.Size() / 2f
                , (IsEX ? 1.1f : 0.8f) * Projectile.scale, SpriteEffects.None, 0);

            Texture2D star = CWRAsset.StarTexture.Value;
            Color starColor = new Color(255, 160, 110);
            starColor.A = 0;
            Main.EntitySpriteDraw(star, drawPos, null, starColor * 0.8f, Projectile.rotation
                , star.Size() / 2f, 0.16f * Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect effect = EffectLoader.DestroyerBeam?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null || Projectile.oldPos == null) {
                return;
            }

            //收集轨迹点，oldPos[0]最新
            int valid = 0;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    break;
                }
                valid++;
            }
            if (valid < 3) {
                return;
            }

            float halfWidth = (IsEX ? 24f : 17f) * Projectile.scale;
            var bars = new VertexPositionColorTexture[valid * 2];
            for (int i = 0; i < valid; i++) {
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f;
                Vector2 next = i == 0
                    ? Projectile.Center + Projectile.velocity
                    : Projectile.oldPos[i - 1] + Projectile.Size / 2f;
                Vector2 dir = (next - pos).SafeNormalize(Projectile.rotation.ToRotationVector2());
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);

                float factor = 1f - i / (float)valid; //1=弹头 0=尾部
                float width = halfWidth * (0.35f + 0.65f * factor);
                bars[i * 2] = new VertexPositionColorTexture((pos + perp * width).ToVector3()
                    , Color.White, new Vector2(factor, 0f));
                bars[i * 2 + 1] = new VertexPositionColorTexture((pos - perp * width).ToVector3()
                    , Color.White, new Vector2(factor, 1f));
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(1f);
            effect.Parameters["exMode"]?.SetValue(IsEX ? 1f : 0f);
            effect.Parameters["seed"]?.SetValue(Projectile.whoAmI * 0.137f % 1f);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars, 0, bars.Length - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        public override void PostDraw(Color lightColor) => Lighting.AddLight(Projectile.Center, Color.Red.ToVector3() * 1.75f * Main.essScale);
    }
}
