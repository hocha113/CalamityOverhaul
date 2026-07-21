using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.GameSystem;
using InnoVault.PRT;
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

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 叛逆之刃
    /// </summary>
    internal class RebelBlade : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "RebelBlade";

        /// <summary>三段连击计数，决定下一次挥砍的招式</summary>
        private int comboCounter;
        /// <summary>连击重置计时器，过久未挥砍则回到第一段</summary>
        private int comboResetTimer;

        public override void SetDefaults() {
            Item.width = Item.height = 54;
            Item.shootSpeed = 9;
            Item.crit = 8;
            Item.damage = 286;
            Item.useTime = 30;
            Item.useAnimation = 15;
            Item.knockBack = 6;
            Item.value = Item.buyPrice(0, 83, 55, 0);
            Item.rare = ItemRarityID.Lime;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.UseSound = null;
            Item.DamageType = CWRRef.GetTrueMeleeDamageClass();
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<RebelBladeHeld>();
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.CWR().isHeldItem = true;
            //noMelee 武器需要手动允许近战词缀
            ItemOverride.ItemMeleePrefixDic[Type] = true;
        }

        public override void UseStyle(Player player, Rectangle heldItemFrame) => player.itemLocation = player.GetPlayerStabilityCenter();

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<RebelBladeFlyAttcke>()] == 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<RebelBladeHeld>()] == 0;
        }

        public override void HoldItem(Player player) {
            if (comboResetTimer > 0 && --comboResetTimer == 0) {
                comboCounter = 0;
            }

            if (Main.myPlayer != player.whoAmI || player.PressKey()) {
                return;
            }

            bool spwan = true;

            int rebelBladeBack = ModContent.ProjectileType<RebelBladeBack>();
            int rebelBladeFlyAttcke = ModContent.ProjectileType<RebelBladeFlyAttcke>();
            int rebelBladeHeld = ModContent.ProjectileType<RebelBladeHeld>();

            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.owner != player.whoAmI) {
                    continue;
                }
                if (proj.type == rebelBladeBack || proj.type == rebelBladeFlyAttcke || proj.type == rebelBladeHeld) {
                    spwan = false;
                    break;
                }
            }

            if (spwan) {
                Projectile.NewProjectileDirect(player.GetSource_FromThis(), player.Center, Vector2.Zero, rebelBladeBack, 0, 0, player.whoAmI);
            }
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                SoundEngine.PlaySound(SoundID.Item1, position);
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<RebelBladeFlyAttcke>(), (int)(damage * 0.6f), knockback, player.whoAmI);
                comboCounter = 0;//飞刃攻击重置连击
                return false;
            }

            int combo = comboCounter % 3;
            float swingDir = comboCounter % 2 == 0 ? 1f : -1f;
            comboCounter++;
            comboResetTimer = 75;
            Projectile.NewProjectile(source, player.Center, velocity, type
                , damage, knockback, player.whoAmI, combo, swingDir);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.LunarBar, 10)
                .AddIngredient(ItemID.SoulofMight, 15)
                .AddIngredient(ItemID.SoulofLight, 15)
                .AddIngredient(ItemID.SoulofNight, 15)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 叛逆之刃手持弹幕
    /// <br/>三段连击: 正手斩 → 反手斩 → 终结回旋斩，命中目标析出叛逆能量球
    /// <br/>刀光由 RebelSlashTrail.fx 渲染，终结回旋斩时撕裂与星火增强
    /// </summary>
    internal class RebelBladeHeld : BaseHeldProj, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Item_Melee + "RebelBlade";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<RebelBlade>();

        /// <summary>连击索引: 0=正手斩 1=反手斩 2=终结回旋斩</summary>
        private ref float ComboIndex => ref Projectile.ai[0];
        /// <summary>挥砍方向符号 ±1</summary>
        private ref float SwingDirAi => ref Projectile.ai[1];

        private bool IsFinisher => ComboIndex >= 2f;

        //阶段时长（逻辑帧，受攻速缩放）
        private float WindupTime => IsFinisher ? 9f : 6f;
        private float SlashTime => IsFinisher ? 17f : 12f;
        private float RecoverTime => 8f;
        private float TotalTime => WindupTime + SlashTime + RecoverTime;
        //挥砍弧度，终结技近整圈
        private float SwingArc => IsFinisher ? 5.7f : 3.5f;
        //刀尖距离持握点的长度
        private float BladeReach => IsFinisher ? 215f : 195f;

        private static readonly Color RebelBlue = new(80, 140, 255);
        private static readonly Color RebelCyan = new(150, 220, 255);

        private float elapsed;
        private float speedMul = 1f;
        private int lockedDirection = 1;
        private int swingSign = 1;
        private float startAngle;
        private float endAngle;
        private float currentRotation;
        private float lastRotation;
        private bool slashSoundPlayed;
        private float trailFade;
        private readonly HashSet<int> hitNPCs = [];

        //刀光轨迹缓存，每逻辑帧细分采样
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
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
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
                , hand, tip, 56f, ref collisionPoint);
        }

        public override void Initialize() {
            //真近战伤害类型继承自物品
            Projectile.DamageType = Item.DamageType;

            swingSign = Math.Sign(SwingDirAi);
            if (swingSign == 0) {
                swingSign = 1;
            }

            lockedDirection = Math.Sign(Projectile.velocity.X);
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
                Projectile.damage = (int)(Projectile.damage * 1.3f);
                Projectile.scale = 1.1f;
            }
        }

        public override void AI() {
            if (Item.type != ModContent.ItemType<RebelBlade>()) {
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
                currentRotation = startAngle - swingSign * 0.24f * MathF.Sin(t * MathHelper.PiOver2);
                trailFade = 0f;
            }
            else if (elapsed < slashEnd) {
                //ease-out 重斩
                float t = (elapsed - WindupTime) / SlashTime;
                float eased = 1f - MathF.Pow(1f - t, IsFinisher ? 4.4f : 3.4f);
                currentRotation = MathHelper.Lerp(startAngle, endAngle, eased);
                trailFade = 1f;
                PushTrailSamples();

                if (!slashSoundPlayed) {
                    slashSoundPlayed = true;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item71 with {
                            Pitch = -0.6f + ComboIndex * 0.15f
                        }, Owner.Center);
                        if (IsFinisher) {
                            SoundEngine.PlaySound(SoundID.Item60 with { Volume = 0.5f, Pitch = -0.2f }, Owner.Center);
                        }
                    }
                }

                //刀刃蓝色能量尘
                if (!VaultUtils.isServer) {
                    Vector2 along = Owner.GetPlayerStabilityCenter()
                        + currentRotation.ToRotationVector2() * Main.rand.NextFloat(BladeReach * 0.45f, BladeReach);
                    Dust dust = Dust.NewDustPerfect(along, DustID.FireworkFountain_Blue
                        , currentRotation.ToRotationVector2().RotatedBy(swingSign * MathHelper.PiOver2) * Main.rand.NextFloat(1f, 4f), 55);
                    dust.noGravity = true;
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
                , RebelBlue.ToVector3() * 0.6f);
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

        private void UpdatePlayerPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = lockedDirection;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = currentRotation;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, currentRotation - MathHelper.PiOver2);
            Projectile.Center = Owner.GetPlayerStabilityCenter() + currentRotation.ToRotationVector2() * BladeReach * 0.5f;
            Projectile.timeLeft = 90;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = currentRotation.ToRotationVector2().X > 0 ? 1 : -1;
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.85f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //转发物品命中钩子，维持装备与饰品的真近战联动
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }

            if (!VaultUtils.isServer) {
                for (int i = 0; i < (IsFinisher ? 6 : 3); i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center, Main.rand.NextVector2Circular(6f, 6f)
                        , Color.Lerp(RebelBlue, RebelCyan, Main.rand.NextFloat()), Main.rand.NextFloat(0.8f, 1.3f)).Configure(false, 12);
                }
            }

            SpawnOrbs(target);
        }

        private void SpawnOrbs(NPC target) {
            if (!Projectile.IsOwnedByLocalPlayer() || target.FromWormBodysRandomSet(5)) {
                return;
            }

            int type = ModContent.ProjectileType<RebelBladeOrb>();
            if (Owner.ownedProjectileCounts[type] > 33) {
                return;
            }

            int count = IsFinisher ? 4 : 3;
            for (int i = 0; i < count; i++) {
                Vector2 spwanPos = target.position + new Vector2(target.width * Main.rand.NextFloat(), target.height * Main.rand.NextFloat());
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), spwanPos, Vector2.Zero
                    , type, Item.damage / 5, 0, Owner.whoAmI);
                Owner.ownedProjectileCounts[type]++;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            for (int i = 0; i < 3; i++) {
                Vector2 spwanPos = target.position + new Vector2(target.width * Main.rand.NextFloat(), target.height * Main.rand.NextFloat());
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), spwanPos, Vector2.Zero
                    , ModContent.ProjectileType<RebelBladeOrb>(), Item.damage / 5, 0, Owner.whoAmI);
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
            if (CanDamage() == true) {
                for (int i = 1; i <= 3; i++) {
                    float rot = MathHelper.Lerp(currentRotation, lastRotation, i / 4f);
                    Vector2 pos = hand + rot.ToRotationVector2() * dist - Main.screenPosition;
                    Color trailColor = RebelBlue * (0.35f * (1f - i / 4f));
                    trailColor.A = 0;
                    Main.EntitySpriteDraw(tex, pos, null, trailColor, rot + rotOffset, origin
                        , Projectile.scale, effect, 0);
                }
            }

            //刀身本体
            Vector2 drawPos = hand + currentRotation.ToRotationVector2() * dist - Main.screenPosition;
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor, currentRotation + rotOffset, origin
                , Projectile.scale, effect, 0);

            //终结回旋斩的能量辉光层
            if (IsFinisher && CanDamage() == true) {
                Color glow = RebelCyan * 0.4f;
                glow.A = 0;
                Main.EntitySpriteDraw(tex, drawPos, null, glow, currentRotation + rotOffset, origin
                    , Projectile.scale * 1.05f, effect, 0);
            }
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (trailCount < 3 || trailFade <= 0.02f) {
                return;
            }
            Effect effect = EffectLoader.RebelSlashTrail?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            var bars = new VertexPositionColorTexture[trailCount * 2];
            Vector2 center = Owner.GetPlayerStabilityCenter();
            //贴图较大(196x200)，视觉刀尖在 reach*0.5 + 半对角 ≈ reach + 40，刀光外缘取中间值贴合刃口
            float outer = (BladeReach + 28f) * Projectile.scale;
            float inner = BladeReach * 0.30f;
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
            effect.Parameters["uFade"]?.SetValue(trailFade);
            effect.Parameters["uHeat"]?.SetValue(IsFinisher ? 1f : 0.35f);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars, 0, bars.Length - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }

    internal class RebelBladeBack : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "RebelBlade";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 45;
            Projectile.timeLeft = 200;
            Projectile.knockBack = 2;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            if (Owner.GetItem().type != ModContent.ItemType<RebelBlade>()
                || Owner.ownedProjectileCounts[ModContent.ProjectileType<RebelBladeFlyAttcke>()] > 0
                || DownLeft || DownRight
                ) {
                Projectile.Kill();
            }
            Projectile.timeLeft = 2;
            Projectile.Center = Owner.GetPlayerStabilityCenter();
            float rot = 120;
            Projectile.rotation = Owner.direction > 0 ? MathHelper.ToRadians(rot) : MathHelper.ToRadians(180 - rot);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition + Owner.CWR().SpecialDrawPositionOffset;
            Main.EntitySpriteDraw(value, drawPos, null, lightColor, Projectile.rotation + MathHelper.PiOver4, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs
            , List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            behindNPCsAndTiles.Add(index);
        }
    }

    internal class RebelBladeFlyAttcke : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "RebelBlade";

        private Color tillColor = Color.White;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Projectile.type] = 3;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
        }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 45;
            Projectile.timeLeft = 200;
            Projectile.knockBack = 2;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.SetProjtimesPierced(0);
            if (Projectile.localAI[1] <= 0) {
                Projectile.rotation = Projectile.velocity.ToRotation();
            }

            if (Projectile.localAI[0] > 0 || Projectile.localAI[1] > 0) {
                tillColor = Color.Red;
            }

            if (!DownRight) {
                Projectile.tileCollide = false;
                tillColor = Color.CadetBlue;
                Projectile.ChasingBehavior(Owner.Center, 23);
                if (Projectile.Distance(Owner.Center) < 80) {
                    Projectile.Kill();
                }
            }
            else if (Projectile.localAI[1] <= 0) {
                tillColor = Color.Yellow;
                Projectile.tileCollide = true;
                Projectile.timeLeft = 200;
                Owner.itemRotation = (Projectile.velocity * Projectile.direction).ToRotation();
                Vector2 mousePos = ToMouse + Owner.GetPlayerStabilityCenter();
                Vector2 ver = Projectile.Center.To(mousePos);
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.ai[0] += Main.rand.Next(1, 3);
                    Projectile.netUpdate = true;//肮脏的手段——HoCha113, 2024-06-02 02:37
                }
                if (Projectile.ai[0] > 30) {
                    SoundEngine.PlaySound(SoundID.Item7, Projectile.Center);
                    Projectile.velocity = ver.UnitVector() * 45;
                    Projectile.ai[0] = 0;
                }
                Projectile.velocity *= 0.98f;
                if (ver.Length() < 16) {
                    Projectile.velocity = Projectile.velocity.RotatedByRandom(0.9f);
                }
            }

            if (Projectile.localAI[0] > 0) {
                Projectile.localAI[0]--;
            }
            if (Projectile.localAI[1] > 0) {
                Projectile.localAI[1]--;
            }

            float rot = (MathHelper.PiOver2 * SafeGravDir - Owner.Center.To(Projectile.Center).ToRotation()) * DirSign * SafeGravDir;
            float rot2 = (MathHelper.PiOver2 * SafeGravDir - MathHelper.ToRadians(DirSign > 0 ? -20 : 200)) * DirSign * SafeGravDir;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rot * -DirSign);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, rot2 * -DirSign);
            Owner.direction = Owner.Center.To(Projectile.Center).X > 0 ? 1 : -1;

            Lighting.AddLight(Projectile.Center, tillColor.ToVector3() * 2.2f);
        }

        private void HitEffet(Vector2 returnVer) {
            if (Projectile.localAI[0] <= 0) {
                Projectile.localAI[0] = 12;
                Projectile.localAI[1] = 12;
                Projectile.rotation = (-Projectile.velocity).ToRotation();
                Vector2 splatterDirection = returnVer.SafeNormalize(Vector2.UnitY);
                for (int j = 0; j < 3; j++) {
                    float sparkScale = Main.rand.NextFloat(1.2f, 2.33f);
                    int sparkLifetime = Main.rand.Next(22, 36);
                    Color sparkColor = Color.Lerp(Color.Silver, Color.Gold, Main.rand.NextFloat(0.7f));
                    Vector2 sparkVelocity = splatterDirection.RotatedByRandom(0.9f) * Main.rand.NextFloat(19f, 34.5f);
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, sparkVelocity, sparkColor, sparkScale).Configure(true, sparkLifetime);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            HitEffet(Projectile.velocity);
            if (Projectile.damage < Projectile.originalDamage * 5) {
                Projectile.damage += 15;
            }
            Projectile.velocity = Projectile.velocity.RotatedByRandom(0.6f);
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.timeLeft = 30;
            Projectile.velocity = -oldVelocity;
            Projectile.DigByTile(CWRSound.HitTheSteel with { MaxInstances = 3, Volume = 0.5f });
            HitEffet(Projectile.velocity);
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle rectangle = texture.GetRectangle();
            Vector2 drawOrigin = rectangle.Size() / 2;

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2;
                Color color = lightColor * (float)((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length / 2);
                Main.EntitySpriteDraw(texture, drawPos, rectangle, color, Projectile.oldRot[k] + MathHelper.PiOver4, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation + MathHelper.PiOver4, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    public class RebelBladeOrb : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.penetrate = 6;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width
                , Projectile.height, DustID.FireworkFountain_Blue, 0, 0, 55, Main.DiscoColor);
            dust.noGravity = true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 30);
            target.AddBuff(BuffID.OnFire3, 30);

            if (target.IsWormBody()) {
                Projectile.timeLeft = 1;
            }
            else {
                target.AddBuff(ModContent.BuffType<HellburnBuff>(), 30);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage /= 10;
            }
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode(66, SoundID.Item60 with { Pitch = 0.6f });
        }
    }
}
