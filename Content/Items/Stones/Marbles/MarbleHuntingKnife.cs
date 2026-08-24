using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.GameSystem;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Stones.Marbles
{
    /// <summary>大理石猎刀，交替连斩，每第三击终结斩突进碎石</summary>
    internal class MarbleHuntingKnife : ModItem
    {
        public override string Texture => GraniteMarbleVFX.MarbleTex + "MarbleHuntingKnife";

        public override void SetDefaults() {
            Item.width = Item.height = 40;
            Item.damage = 13;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 16;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTurn = false;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 3f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<MarbleHuntingKnifeHeld>();
            Item.shootSpeed = 8f;
            Item.value = Item.sellPrice(0, 0, 50, 0);
            Item.rare = ItemRarityID.Green;
            //noMelee 须手动允许近战词缀
            ItemOverride.ItemMeleePrefixDic[Type] = true;
        }

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            //ai[0]=连击计数，%3终结、%2交替
            MarbleSwingPlayer mp = player.GetModPlayer<MarbleSwingPlayer>();
            int step = mp.ComboStep;
            mp.ComboStep = (step + 1) % 6;
            mp.ComboTimer = 45;

            Projectile.NewProjectile(source, player.Center, velocity, type, damage, knockback
                , player.whoAmI, step);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Marble, 14)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 6)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    /// <summary>猎刀 Held，交替快斩；终结斩更大弧+前突+碎石</summary>
    internal class MarbleHuntingKnifeHeld : BaseHeldProj, IPrimitiveDrawable
    {
        public override string Texture => GraniteMarbleVFX.MarbleTex + "MarbleHuntingKnife";

        /// 连击 0~5，%3==2 终结，%2 上下交替
        private ref float ComboIndex => ref Projectile.ai[0];
        private bool IsFinisher => (int)ComboIndex % 3 == 2;

        //阶段时长(逻辑帧，吃攻速)
        private float WindupTime => IsFinisher ? 5f : 3.5f;
        private float SlashTime => IsFinisher ? 10f : 8f;
        private float RecoverTime => IsFinisher ? 7.5f : 5f;
        private float TotalTime => WindupTime + SlashTime + RecoverTime;
        private float SwingArc => IsFinisher ? 3.6f : 2.3f;
        //刀尖距枢轴30px，终结斩×1.06
        private const float BladeLength = 30f;
        private const float HoldDistance = 24f;
        private const float SwingDistance = 30f;
        //贴图刀刃约 -57°
        private const float TextureBladeAngle = -0.996f;

        private float elapsed;
        private float speedMul = 1f;
        private int lockedDirection = 1;
        private int swingSign = 1;
        private float baseAngle;
        private float startAngle;
        private float endAngle;
        private float currentRotation;
        private float lastRotation;
        private float currentDistance = HoldDistance;
        private float trailFade;
        private float hitstopTimer;
        private float lungeTimer;
        private bool slashSoundPlayed;
        private bool shardsSpawned;
        private Vector2 pivot;

        //刀光轨迹缓存
        private const int TrailMax = 32;
        private const int TrailSubdiv = 4;
        private readonly float[] trailRot = new float[TrailMax];
        private int trailCount;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 96;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 60;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => elapsed >= WindupTime && elapsed <= WindupTime + SlashTime + 1f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CanDamage() != true) {
                return false;
            }
            Vector2 tip = pivot + currentRotation.ToRotationVector2() * BladeLength * Projectile.scale;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , pivot, tip, 22f, ref collisionPoint);
        }

        public override void Initialize() {
            swingSign = (int)ComboIndex % 2 == 0 ? 1 : -1;

            lockedDirection = Math.Sign(ToMouse.X);
            if (lockedDirection == 0) {
                lockedDirection = Owner.direction;
            }
            Owner.direction = lockedDirection;

            speedMul = Owner.GetWeaponAttackSpeed(Item);
            if (speedMul <= 0f) {
                speedMul = 1f;
            }

            //弧线中心=瞄准，swingSign 定上下
            baseAngle = Projectile.velocity.ToRotation();
            startAngle = baseAngle - swingSign * SwingArc * 0.5f;
            endAngle = baseAngle + swingSign * SwingArc * 0.5f;
            currentRotation = lastRotation = startAngle;
            pivot = GetHandPos() + startAngle.ToRotationVector2() * HoldDistance;

            if (IsFinisher) {
                Projectile.damage = (int)(Projectile.damage * 1.25f);
                Projectile.scale = 1.06f;
                if (!VaultUtils.isServer) {
                    //收刀刮擦音
                    SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = 0.7f, MaxInstances = 3 }, Owner.Center);
                }
            }
        }

        public override void AI() {
            if (Item.type != ModContent.ItemType<MarbleHuntingKnife>()) {
                Projectile.Kill();
                return;
            }
            if (elapsed >= TotalTime) {
                Projectile.Kill();
                return;
            }

            //终结斩命中顿帧
            if (hitstopTimer > 0f) {
                hitstopTimer--;
                UpdatePlayerPose();
                return;
            }

            lastRotation = currentRotation;
            float slashEnd = WindupTime + SlashTime;

            if (elapsed < WindupTime) {
                //收刀蓄势
                float t = elapsed / WindupTime;
                currentRotation = startAngle - swingSign * (IsFinisher ? 0.5f : 0.35f) * MathF.Sin(t * MathHelper.PiOver2);
                currentDistance = MathHelper.Lerp(HoldDistance, HoldDistance * 0.8f, t);
                trailFade = 0f;
            }
            else if (elapsed < slashEnd) {
                //ease-out 快斩
                float t = (elapsed - WindupTime) / SlashTime;
                float eased = 1f - MathF.Pow(1f - t, IsFinisher ? 4.2f : 3.4f);
                currentRotation = MathHelper.Lerp(startAngle, endAngle, eased);
                currentDistance = MathHelper.Lerp(HoldDistance * 0.8f, SwingDistance, eased);
                trailFade = 1f;

                if (!slashSoundPlayed) {
                    slashSoundPlayed = true;
                    PlaySlashSound();
                    if (IsFinisher) {
                        lungeTimer = 3f;
                    }
                }

                //终结前突 3帧×4px，不压已有更快前进
                if (lungeTimer > 0f) {
                    lungeTimer--;
                    if (Owner.velocity.X * lockedDirection < 4f) {
                        Owner.velocity.X = lockedDirection * 4f;
                    }
                }

                PushTrailSamples();

                //终结鎏金闪
                if (IsFinisher && !VaultUtils.isServer && Main.rand.NextBool(2)) {
                    Vector2 along = GetHandPos() + currentRotation.ToRotationVector2()
                        * Main.rand.NextFloat(BladeLength * 0.5f, BladeLength + SwingDistance);
                    PRTLoader.NewParticle<PRT_Sparkle>(along, Vector2.Zero, GraniteMarbleVFX.MarbleGold, 0.5f)
                        .Configure(GraniteMarbleVFX.MarbleGold, 12, 0.2f, 0.5f);
                }
            }
            else {
                //收势
                float t = (elapsed - slashEnd) / RecoverTime;
                currentRotation = endAngle;
                currentDistance = SwingDistance;
                trailFade = 1f - t;
                PushTrailSamples();
            }

            pivot = GetHandPos() + currentRotation.ToRotationVector2() * currentDistance;

            //终结斩收尾碎石(不依赖命中)
            if (IsFinisher && !shardsSpawned && elapsed >= slashEnd - 1f) {
                shardsSpawned = true;
                SpawnFinisherShards();
            }

            UpdatePlayerPose();
            Lighting.AddLight(pivot, GraniteMarbleVFX.MarbleCore.ToVector3() * (IsFinisher ? 0.5f : 0.35f));
            elapsed += speedMul;
        }

        private void PlaySlashSound() {
            if (VaultUtils.isServer) {
                return;
            }
            if (IsFinisher) {
                //终结双层破空
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.05f, Volume = 0.85f, MaxInstances = 3 }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.05f, Volume = 0.65f, MaxInstances = 3 }, Owner.Center);
            }
            else {
                //普斩破空
                float pitch = swingSign > 0 ? 0.3f : 0.45f;
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = pitch, Volume = 0.7f, MaxInstances = 3 }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.6f, Volume = 0.22f, MaxInstances = 3 }, Owner.Center);
            }
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

        private void SpawnFinisherShards() {
            Vector2 tip = GetHandPos() + currentRotation.ToRotationVector2() * (currentDistance + BladeLength);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.2f, Volume = 0.8f }, tip);
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_MarbleChip>(tip
                        , baseAngle.ToRotationVector2().RotatedByRandom(0.7f) * Main.rand.NextFloat(2.5f, 6f)
                        , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.45f, 0.75f))
                        .Configure(Main.rand.Next(20, 30));
                }
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_Smoke>(tip, baseAngle.ToRotationVector2() * Main.rand.NextFloat(1f, 2.5f)
                        , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.35f, 0.55f)).Configure(22, 0.6f, 0.05f);
                }
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                Vector2 dir = baseAngle.ToRotationVector2();
                for (int i = 0; i < 3; i++) {
                    Vector2 v = dir.RotatedBy(MathHelper.Lerp(-0.5f, 0.5f, i / 2f)) * Main.rand.NextFloat(7f, 10f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), tip, v
                        , ModContent.ProjectileType<MarbleShard>(), (int)(Projectile.damage * 0.5f)
                        , Projectile.knockBack * 0.5f, Projectile.owner);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                //命中石凿音
                SoundEngine.PlaySound(SoundID.Dig with {
                    Pitch = IsFinisher ? 0.1f : 0.4f,
                    Volume = IsFinisher ? 0.7f : 0.5f,
                    MaxInstances = 3
                }, target.Center);
                if (IsFinisher) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.15f, Volume = 0.45f, MaxInstances = 3 }, target.Center);
                }

                //石屑+石尘
                int chips = IsFinisher ? 4 : Main.rand.Next(2, 4);
                for (int i = 0; i < chips; i++) {
                    PRTLoader.NewParticle<PRT_MarbleChip>(target.Center
                        , currentRotation.ToRotationVector2().RotatedByRandom(0.6f) * Main.rand.NextFloat(2f, 5f)
                            - Vector2.UnitY * Main.rand.NextFloat(1f, 2.5f)
                        , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.4f, 0.7f))
                        .Configure(Main.rand.Next(18, 28));
                }
                PRTLoader.NewParticle<PRT_Smoke>(target.Center, Main.rand.NextVector2Circular(1.5f, 1.5f)
                    , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.3f, 0.45f)).Configure(20, 0.6f, 0.04f);
            }

            if (IsFinisher) {
                //终结命中 2帧顿+轻震
                hitstopTimer = 2f;
                if (CWRClientConfig.Instance.ScreenVibration) {
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(target.Center
                        , currentRotation.ToRotationVector2(), 3f, 4f, 8, 700f, FullName));
                }
            }
        }

        private void UpdatePlayerPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = lockedDirection;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = currentRotation;

            float armAngle = currentRotation - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armAngle);
            //后臂微收
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Quarter, armAngle + 0.3f * lockedDirection);

            Projectile.Center = pivot;
            Projectile.timeLeft = 60;
        }

        private Vector2 GetHandPos() {
            Vector2 p = Owner.GetPlayerStabilityCenter();
            p.Y -= 6f * Owner.gravDir;
            return p;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureValue;
            Vector2 origin = tex.Size() / 2f;
            Vector2 hand = GetHandPos();
            SpriteEffects effect = lockedDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;
            float drawRot = lockedDirection == -1 ? currentRotation + TextureBladeAngle : currentRotation - TextureBladeAngle;

            //斩击轻残影
            if (CanDamage() == true) {
                Color tint = IsFinisher ? GraniteMarbleVFX.MarbleGold : GraniteMarbleVFX.MarbleCore;
                for (int i = 1; i <= 2; i++) {
                    float rot = MathHelper.Lerp(currentRotation, lastRotation, i / 3f);
                    Vector2 pos = hand + rot.ToRotationVector2() * currentDistance - Main.screenPosition;
                    float ghostRot = lockedDirection == -1 ? rot + TextureBladeAngle : rot - TextureBladeAngle;
                    Color ghostColor = tint * (0.3f * (1f - i / 3f));
                    ghostColor.A = 0;
                    Main.EntitySpriteDraw(tex, pos, null, ghostColor, ghostRot, origin, Projectile.scale, effect, 0);
                }
            }

            Main.EntitySpriteDraw(tex, pivot - Main.screenPosition, null, lightColor, drawRot, origin
                , Projectile.scale, effect, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (trailCount < 3 || trailFade <= 0.02f) {
                return;
            }
            Effect effect = EffectLoader.MarbleSlash?.Value;
            if (effect == null) {
                return;
            }

            //扇面 x=1 最新缘，外径+3px 毛边
            var bars = new VertexPositionColorTexture[trailCount * 2];
            Vector2 center = GetHandPos();
            float reach = (SwingDistance + BladeLength) * Projectile.scale;
            float outer = reach + 3f;
            float inner = reach * 0.45f;
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

            //普斩淡，终结全亮
            GraniteMarbleVFX.ApplyMarbleSlash(effect, trailFade * (IsFinisher ? 1f : 0.7f), IsFinisher ? 1f : 0.2f);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars, 0, bars.Length - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }

    /// <summary>猎刀连击计数</summary>
    internal class MarbleSwingPlayer : ModPlayer
    {
        public int ComboStep;
        public int ComboTimer;

        public override void ResetEffects() {
            if (ComboTimer > 0) {
                ComboTimer--;
            }
            else {
                ComboStep = 0;
            }
        }
    }
}
