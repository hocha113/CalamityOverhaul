﻿using CalamityMod;
using CalamityMod.CalPlayer;
using CalamityMod.Items.Accessories;
using CalamityMod.NPCs.DevourerofGods;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.NeutronBowProjs;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    /// <summary>
    /// 惧亡者之证
    /// </summary>
    internal class EmblemOfDread : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item_Accessorie + "EmblemOfDread";
        internal FieldInfo TotalDefenseDamageInfo { get; private set; }
        void ICWRLoader.LoadData() => TotalDefenseDamageInfo = typeof(CalamityPlayer)
            .GetField("totalDefenseDamage", BindingFlags.Instance | BindingFlags.NonPublic);
        void ICWRLoader.UnLoadData() => TotalDefenseDamageInfo = null;

        public override void SetStaticDefaults() {
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(5, 3));
            ItemID.Sets.AnimatesAsSoul[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.accessory = true;
            Item.value = Item.buyPrice(180, 22, 15, 0);
            Item.rare = ModContent.RarityType<Turquoise>();
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems_EmblemOfDread;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.GetModPlayer<EmblemOfDreadPlayer>().Alive = true;
            player.dashType = 0;
            player.Calamity().DashID = string.Empty;
            TotalDefenseDamageInfo?.SetValue(player.Calamity(), 0);
            player.GetDamage<MeleeDamageClass>() += 1f;
            player.GetAttackSpeed<MeleeDamageClass>() += 1f;
            player.GetCritChance<MeleeDamageClass>() += 100f;
            player.aggro += 9999;
            player.statDefense += 100;
            if (player.ownedProjectileCounts[ModContent.ProjectileType<TheGravityShield>()] == 0) {
                Projectile.NewProjectile(player.FromObjectGetParent(), player.Center, Vector2.Zero
                    , ModContent.ProjectileType<TheGravityShield>(), 0, 0, player.whoAmI);
            }
            else {
                foreach (var proj in Main.ActiveProjectiles) {
                    if (proj.owner != player.whoAmI) {
                        continue;
                    }
                    if (proj.type != ModContent.ProjectileType<TheGravityShield>()) {
                        continue;
                    }
                    proj.localAI[2] = hideVisual ? 0 : 1;
                }
            }
        }

        public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player) {
            return true;
        }

        public override void AddRecipes() {
            _ = CreateRecipe()
                .AddIngredient<ElementalGauntlet>()
                .AddIngredient<Affliction>()
                .AddIngredient<DarkSunRing>()
                .AddIngredient<DraedonsHeart>()
                .AddIngredient<AsgardianAegis>()
                .AddIngredient<Radiance>()
                .AddIngredient<YharimsGift>()
                .AddIngredient<TheSponge>()
                .AddIngredient<TheAmalgam>()
                .AddIngredient<WarbanneroftheSun>()
                .AddIngredient<ReaperToothNecklace>()
                .AddIngredient<OccultSkullCrown>()
                .AddIngredient<ChaliceOfTheBloodGod>()
                .AddIngredient<NeutronStarIngot>(12)
                .AddBlockingSynthesisEvent()
                .AddTile(ModContent.TileType<TransmutationOfMatter>())
                .Register();
        }
    }

    public class WarpingPoint : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.Masking + "StarTexture_White";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 100;
            Projectile.timeLeft = 120;
            Projectile.aiStyle = -1;
            Projectile.localNPCHitCooldown = 6;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
        }

        public bool CanDrawCustom() => false;

        public override void AI() {
            if (Projectile.ai[2] % 20 == 0) {
                for (int i = 0; i < 4; i++) {
                    float rot1 = MathHelper.PiOver2 * i;
                    Vector2 vr = rot1.ToRotationVector2();
                    for (int j = 0; j < 23; j++) {
                        BasePRT spark = new PRT_HeavenfallStar(Projectile.Center
                            , vr * (0.24f), false, 30, Main.rand.NextFloat(1.2f, 2f), Color.CadetBlue);
                        PRTLoader.AddParticle(spark);
                    }
                }
            }
            Projectile.ai[2]++;
            Projectile.ai[0] += 0.15f;
            if (Projectile.timeLeft > 110) {
                Projectile.localAI[0] += 0.06f;
                Projectile.ai[1] += 0.1f;
            }

            if (Projectile.timeLeft <= 10) {
                Projectile.localAI[0] -= 0.13f;
                Projectile.ai[1] -= 0.066f;
            }

            Projectile.localAI[1] += 0.07f;
            Projectile.ai[1] = Math.Clamp(Projectile.ai[1], 0f, 1f);

            Lighting.AddLight(Projectile.Center, new Vector3(1, 1, 1));
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        public void Warp() {
            Texture2D warpTex = TextureAssets.Projectile[Type].Value;
            Color warpColor = new Color(45, 45, 45) * Projectile.ai[1];
            for (int i = 0; i < 3; i++) {
                Main.spriteBatch.Draw(warpTex, Projectile.Center - Main.screenPosition
                    , null, warpColor, 0, warpTex.Size() / 2, Projectile.localAI[0], SpriteEffects.None, 0f);
            }
        }

        public void DrawCustom(SpriteBatch spriteBatch) { }
    }

    public class TheGravityShield : BaseHeldProj, IWarpDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private int Time;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 80;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            Projectile.timeLeft = 2;
            Projectile.Center = Owner.Center;
            Owner.statDefense += 60;
            Projectile.localAI[0] = MathF.Abs(MathF.Sin(Time * 0.02f)) * 0.4f + 0.2f;

            if (Projectile.IsOwnedByLocalPlayer() && Owner.velocity.Length() > 12 && Time % 15 == 0) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.Center
                        , Vector2.Zero, ModContent.ProjectileType<WarpingPoint>(), 800, 0);
            }

            if (!Owner.Alives() || !Owner.GetModPlayer<EmblemOfDreadPlayer>().Alive) {
                Projectile.localAI[1] -= 0.1f;
                if (Projectile.localAI[1] <= 0) {
                    Projectile.Kill();
                }
            }
            else if (Projectile.localAI[1] < 1f) {
                Projectile.localAI[1] += 0.1f;
            }
            Time++;
        }

        public override bool PreDraw(ref Color lightColor) {
            return false;
        }

        bool IWarpDrawable.CanDrawCustom() => true;

        void IWarpDrawable.DrawCustom(SpriteBatch spriteBatch) {
            if (Projectile.localAI[2] == 0) {//是0就表示隐藏
                return;
            }

            Texture2D texture = TextureAssets.Item[ModContent.ItemType<EmblemOfDread>()].Value;

            Vector2 center = Owner.Center - Main.screenPosition;

            // 分别控制 X 和 Y 轴的运动范围，制造椭圆轨迹
            float baseRadiusX = 140 + Projectile.localAI[0] * 50; // X轴半径
            float baseRadiusY = 80 + Projectile.localAI[0] * 30;  // Y轴半径（通常小于X轴）

            // Z 轴深度模拟
            float zAmplitude = 40;  // Z轴振幅，控制前后摆动幅度
            float zScaleFactor = 120f; // 透视缩放参数，影响Z轴远近缩放

            float timeOffset = Time * 0.01f;

            // 存储盾牌信息用于排序
            List<(Vector2 pos, float scale, Rectangle rect)> shieldData = new();

            for (int i = 0; i < 3; i++) {
                // 计算旋转角度
                float angle = timeOffset + MathHelper.TwoPi / 3f * i;

                // 计算椭圆轨迹位置
                float x = MathF.Cos(angle) * baseRadiusX;
                float y = MathF.Sin(angle) * baseRadiusY;
                Vector2 orbitPos = new Vector2(x, y) * Projectile.localAI[1];

                // 计算 Z 轴偏移，制造 3D 立体环绕感
                float zOffset = MathF.Sin(angle) * zAmplitude;
                float scale = 1f + (zOffset / zScaleFactor);

                // 计算最终绘制位置
                Vector2 drawPos = center + orbitPos + new Vector2(0, -zOffset); // 向上偏移zOffset

                shieldData.Add((drawPos, scale, CWRUtils.GetRec(texture, i, 3)));
            }

            // 根据Y值（纵深）进行排序，确保远处的先绘制
            shieldData = [.. shieldData.OrderBy(s => s.pos.Y)];

            foreach (var (pos, scale, rect) in shieldData) {
                spriteBatch.Draw(texture, pos, rect, Color.White * Projectile.localAI[1]
                    , 0, rect.Size() / 2, scale, SpriteEffects.None, 0);
            }
        }

        void IWarpDrawable.Warp() {
            if (Owner.GetModPlayer<EmblemOfDreadPlayer>().TheGravityShieldTime <= 0) {
                return;
            }

            Texture2D warpTex = CWRUtils.GetT2DValue(CWRConstant.Masking + "DiffusionCircle");
            Color warpColor = new Color(45, 45, 45) * 0.6f * Projectile.localAI[1];
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 drawOrig = warpTex.Size() / 2;
            for (int i = 0; i < 133; i++) {
                Main.spriteBatch.Draw(warpTex, drawPos, null, warpColor, Projectile.ai[0] + i * 115f
                    , drawOrig, Projectile.localAI[0] + i * 0.005f, SpriteEffects.None, 0f);
            }
        }
    }

    public class EmblemOfDreadProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 330;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.Center = Owner.Center;
            Owner.GivePlayerImmuneState(6);

            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Owner.GetSource_FromThis(), Owner.Center
                , Owner.velocity.GetNormalVector() * 22
                , ModContent.ProjectileType<NeutronLaser>(), 800, 0);
                Projectile.NewProjectile(Owner.GetSource_FromThis(), Owner.Center
                , Owner.velocity.GetNormalVector() * -22
                , ModContent.ProjectileType<NeutronLaser>(), 800, 0);
            }

            if (!Owner.GetModPlayer<EmblemOfDreadPlayer>().Alive 
                || Owner.GetModPlayer<EmblemOfDreadPlayer>().DashTimer <= 0) {
                Projectile.Kill();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<VoidErosion>(), 1300);
        }
    }

    internal class EmblemOfDreadPlayer : ModPlayer
    {
        public bool Alive = false;
        public const int DashDown = 0;
        public const int DashUp = 1;
        public const int DashRight = 2;
        public const int DashLeft = 3;
        public const float DashVelocity = 100f;
        public const int KilllineByLife = 250000;
        public const int DashCooldown = 50;
        public const int DashDuration = 35;
        public int DashDir = -1;
        public int DashDelay = 0;
        public int DashTimer = 0;
        public int TheGravityShieldTime;
        public SlotId SlotId;
        public override void Initialize() => Alive = false;
        public override void ResetEffects() {
            Alive = false;
            if (TheGravityShieldTime > 0) {
                TheGravityShieldTime--;
            }

            if (Player.controlDown && Player.releaseDown && Player.doubleTapCardinalTimer[DashDown] < 15) {
                DashDir = DashDown;
            }
            else if (Player.controlUp && Player.releaseUp && Player.doubleTapCardinalTimer[DashUp] < 15) {
                DashDir = DashUp;
            }
            else if (Player.controlRight && Player.releaseRight && Player.doubleTapCardinalTimer[DashRight] < 15) {
                DashDir = DashRight;
            }
            else if (Player.controlLeft && Player.releaseLeft && Player.doubleTapCardinalTimer[DashLeft] < 15) {
                DashDir = DashLeft;
            }
            else {
                DashDir = -1;
            }
        }

        public override bool PreKill(double damage, int hitDirection, bool pvp
            , ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource) {
            if (Alive && TheGravityShieldTime <= 0) {
                TheGravityShieldTime = 3600;
                Player.Heal(Player.statLifeMax2);
                return false;
            }
            TheGravityShieldTime = 0;
            return base.PreKill(damage, hitDirection, pvp, ref playSound, ref genDust, ref damageSource);
        }

        public override void OnHitByNPC(NPC npc, Player.HurtInfo hurtInfo) {
            if (!Alive) {
                return;
            }

            npc.AddBuff(ModContent.BuffType<VoidErosion>(), 1300);

            if (npc == null 
                || hurtInfo.DamageSource == null 
                || hurtInfo.DamageSource.SourceItem == null) {
                return;
            }

            if (npc.life < KilllineByLife
                && hurtInfo.DamageSource.SourceItem.DamageType 
                == ModContent.GetInstance<TrueMeleeDamageClass>()) {
                npc.SimpleStrikeNPC(npc.lifeMax, hurtInfo.HitDirection);
            }
        }

        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) {
            if (!Alive) {
                return;
            }

            npc.SimpleStrikeNPC(npc.defDamage * 100, Math.Sign(Player.Center.To(npc.Center).X), true, 8);
            modifiers.FinalDamage *= 0.25f;
        }

        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers) {
            if (!Alive) {
                return;
            }

            if (proj.CWR().Source != null 
                && proj.CWR().Source is EntitySource_Parent entitySource
                && entitySource.Entity is NPC npc && npc.Alives()) {
                npc.SimpleStrikeNPC(proj.damage * 100, Math.Sign(Player.Center.To(npc.Center).X), true, 8);
            }

            modifiers.FinalDamage *= 0.25f;
        }

        public override void PreUpdateMovement() {
            if (!Alive) {
                return;
            }

            if (!Player.mount.Active && !Player.setSolar 
                && Player.dashType == DashID.None 
                && DashDir != -1 && DashDelay == 0) {
                Vector2 newVelocity = Player.velocity;

                switch (DashDir) {
                    case DashUp when Player.velocity.Y > -DashVelocity:
                    case DashDown when Player.velocity.Y < DashVelocity: {
                        float dashDirection = DashDir == DashDown ? 1 : -1.3f;
                        newVelocity.Y = dashDirection * DashVelocity;
                        break;
                    }
                    case DashLeft when Player.velocity.X > -DashVelocity:
                    case DashRight when Player.velocity.X < DashVelocity: {
                        float dashDirection = DashDir == DashRight ? 1 : -1;
                        newVelocity.X = dashDirection * DashVelocity;
                        break;
                    }
                    default:
                        return;
                }

                DashDelay = DashCooldown;
                DashTimer = DashDuration;
                Player.velocity = newVelocity;
                Player.maxFallSpeed = 80f;
                SlotId = SoundEngine.PlaySound(DevourerofGodsHead.DeathAnimationSound with { Pitch = -0.2f }, Player.Center);
                Projectile.NewProjectile(Player.FromObjectGetParent(), Player.Center, Vector2.Zero
                    , ModContent.ProjectileType<EmblemOfDreadProj>(), 8000, 8, Player.whoAmI);
            }

            if (DashDelay > 0) {
                DashDelay--;
            }

            if (DashTimer > 0) {
                if (SoundEngine.TryGetActiveSound(SlotId, out var sound)) {
                    sound.Position = Player.Center;
                }
                if (CWRServerConfig.Instance.LensEasing) {
                    Main.SetCameraLerp(0.1f, 60);
                }

                for (int j = 0; j < 53; j++) {
                    BasePRT spark = new PRT_HeavenfallStar(Player.Center
                        , Player.velocity.UnitVector() * (0.1f + j * 0.34f), false, 20, Main.rand.NextFloat(0.6f, 1.3f), Color.BlueViolet);
                    PRTLoader.AddParticle(spark);
                }
                for (int j = 0; j < 53; j++) {
                    BasePRT spark = new PRT_HeavenfallStar(Player.Center
                        , Player.velocity.UnitVector() * -(0.1f + j * 0.34f), false, 20, Main.rand.NextFloat(0.6f, 1.3f), Color.BlueViolet);
                    PRTLoader.AddParticle(spark);
                }

                Player.maxFallSpeed = 80f;
                Player.eocDash = DashTimer;
                Player.armorEffectDrawShadowEOCShield = true;
                DashTimer--;
            }
        }
    }
}
