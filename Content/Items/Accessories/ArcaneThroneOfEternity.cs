using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    internal class ArcaneThroneOfEternity : ModItem
    {
        public override string Texture => CWRConstant.Item_Accessorie + "ArcaneThroneOfEternity";

        public override void SetDefaults() {
            Item.width = 40;
            Item.height = 32;
            Item.accessory = true;
            Item.value = Item.buyPrice(180, 22, 15, 0);
            Item.rare = CWRID.Rarity_Turquoise;
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_ArcaneThroneOfEternity;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.GetDamage<MagicDamageClass>() += 1f;
            player.GetCritChance<MagicDamageClass>() += 100f;
            player.GetAttackSpeed<MagicDamageClass>() += 1f;
            player.statManaMax2 += 1000;
            player.manaRegenBonus += 1000;
            player.aggro += 1200;

            ArcaneThronePlayer thronePlayer = player.GetModPlayer<ArcaneThronePlayer>();
            thronePlayer.Alive = true;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.InsertHotkeyBinding(CWRKeySystem.Accessory_Skills, "[KEY]", CWRKeySystem.Notbound.Value + $"[{CWRKeySystem.Accessory_Skills.DisplayName}]");
        }
    }

    /// <summary>
    /// 奥术裂隙，魔法弹幕命中时在现实帷幕上撕开的裂口，持续爆发造成范围伤害
    /// </summary>
    public class ArcaneRiftProj : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        /// <summary> 总持续时间(帧) </summary>
        public const int Lifetime = 150;
        /// <summary> 爆发脉冲周期(帧) </summary>
        public const int PulseCycle = 30;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 240;
            Projectile.timeLeft = Lifetime;
            Projectile.aiStyle = -1;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 9;
            Projectile.DamageType = DamageClass.Magic;
        }

        public bool CanDrawCustom() => false;
        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //首帧初始化随机外观
            if (Projectile.localAI[2] == 0f) {
                Projectile.localAI[2] = 1f;
                Projectile.rotation = Main.rand.NextFloat(MathHelper.TwoPi);
                Projectile.localAI[1] = Main.rand.NextFloat();
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Pitch = 0.4f, Volume = 0.7f }, Projectile.Center);
            }

            Projectile.ai[2]++;

            //开合曲线
            if (Projectile.timeLeft > Lifetime - 18) {
                Projectile.localAI[0] += 1f / 18f;
            }
            else if (Projectile.timeLeft <= 24) {
                Projectile.localAI[0] -= 1f / 24f;
            }
            Projectile.localAI[0] = Math.Clamp(Projectile.localAI[0], 0f, 1f);

            //周期性爆发：脉冲起点喷出粒子
            if (Projectile.ai[2] % PulseCycle == 1 && !VaultUtils.isServer) {
                Vector2 axis = Projectile.rotation.ToRotationVector2();
                for (int i = 0; i < 8; i++) {
                    Vector2 vel = axis.RotatedBy(Main.rand.NextFloat(-0.9f, 0.9f)) * Main.rand.NextFloat(2f, 6f) * (Main.rand.NextBool() ? 1 : -1);
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vel, Color.MediumPurple, Main.rand.NextFloat(0.5f, 1f)).Configure(false, 22);
                }
                for (int i = 0; i < 4; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                    PRTLoader.NewParticle<PRT_SpaceFracture>(Projectile.Center + Main.rand.NextVector2Circular(30f, 30f), vel
                        , Color.Lerp(Color.Gold, Color.BlueViolet, Main.rand.NextFloat()), Main.rand.NextFloat(0.4f, 0.8f)).Configure(Main.rand.Next(15, 26), Main.rand.NextFloat(-0.5f, 0.5f));
                }
            }

            //裂隙内的微光逸散
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                Vector2 axis = Projectile.rotation.ToRotationVector2();
                Vector2 pos = Projectile.Center + axis * Main.rand.NextFloat(-90f, 90f) * Projectile.localAI[0];
                PRTLoader.NewParticle<PRT_Spark>(pos, axis.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-1.5f, 1.5f)
                    , Main.rand.NextBool() ? Color.Cyan : Color.MediumPurple, Main.rand.NextFloat(0.4f, 0.9f)).Configure(false, 14);
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.55f, 0.25f, 0.9f) * Projectile.localAI[0]);
        }

        public void Warp() {
            Texture2D warpTex = TextureAssets.Projectile[Type].Value;
            Color warpColor = new Color(40, 12, 50) * Projectile.localAI[0];
            for (int i = 0; i < 3; i++) {
                Main.spriteBatch.Draw(warpTex, Projectile.Center - Main.screenPosition, null, warpColor
                    , Projectile.rotation + Projectile.ai[2] * 0.02f + i * 2.09f, warpTex.Size() / 2
                    , new Vector2(0.32f, 0.62f) * Projectile.localAI[0], SpriteEffects.None, 0f);
            }
        }

        public void DrawCustom(SpriteBatch spriteBatch) { }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.ArcaneRift?.Value;
            Texture2D canvas = CWRAsset.Placeholder_White?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            float open = Projectile.localAI[0];

            if (shader == null || canvas == null || noise == null) {
                //着色器缺失时的降级绘制
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Color glowColor = new Color(150, 60, 255, 0) * open;
                Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null, glowColor
                    , Projectile.rotation, glow.Size() / 2, new Vector2(1.2f, 3.6f) * open, SpriteEffects.None);
                return false;
            }

            float pulse = Projectile.ai[2] % PulseCycle / PulseCycle;

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + Projectile.localAI[1] * 37f);
            shader.Parameters["riftOpen"]?.SetValue(open);
            shader.Parameters["pulse"]?.SetValue(pulse);
            shader.Parameters["fadeAlpha"]?.SetValue(open);
            shader.Parameters["seed"]?.SetValue(Projectile.localAI[1]);

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float drawSize = 420f;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Main.spriteBatch.Draw(canvas, drawPos, null, Color.White
                , Projectile.rotation + MathHelper.PiOver2, canvas.Size() * 0.5f
                , new Vector2(drawSize, drawSize), SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }

    /// <summary>
    /// 真理跃迁，撕裂空间进入高维领域，期间免疫一切伤害，离开时释放现实震荡
    /// </summary>
    public class TruthLeapProj : BaseHeldProj, IWarpDrawable
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        /// <summary> 领域持续时间(帧) </summary>
        public const int Duration = 180;
        private float seed;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Duration;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;
        public override bool? CanDamage() => false;
        public bool CanDrawCustom() => false;
        public void DrawCustom(SpriteBatch spriteBatch) { }

        public override void Initialize() {
            seed = Main.rand.NextFloat();
            SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Pitch = -0.5f, Volume = 1.4f }, Owner.Center);

            if (!VaultUtils.isServer) {
                //空间向内撕裂的入场特效
                for (int i = 0; i < 26; i++) {
                    Vector2 offset = Main.rand.NextVector2CircularEdge(200f, 200f);
                    Vector2 vel = -offset.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(4f, 9f);
                    PRTLoader.NewParticle<PRT_SpaceFracture>(Owner.Center + offset, vel
                        , Color.Lerp(Color.Cyan, Color.BlueViolet, Main.rand.NextFloat()), Main.rand.NextFloat(0.5f, 1.1f)).Configure(Main.rand.Next(18, 30), Main.rand.NextFloat(-0.6f, 0.6f));
                }
            }
        }

        /// <summary> 领域展开进度 0~1 </summary>
        public float SphereProgress {
            get {
                int age = Duration - Projectile.timeLeft;
                float opening = Math.Clamp(age / 16f, 0f, 1f);
                float closing = Math.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
                return CWRUtils.EaseOutCubic(opening) * closing;
            }
        }

        public override void AI() {
            Projectile.Center = Owner.Center;

            //高维领域内：免疫一切伤害，敌人无法锁定
            Owner.GivePlayerImmuneState(4, false);
            Owner.aggro -= 99999;
            Owner.opacityForAnimation = MathHelper.Lerp(Owner.opacityForAnimation, 0.3f, 0.12f);
            Owner.noFallDmg = true;

            //跃迁过程中按下技能键可提前离开
            if (Projectile.IsOwnedByLocalPlayer() && Projectile.timeLeft < Duration - 15
                && CWRKeySystem.Accessory_Skills.JustPressed) {
                Projectile.Kill();
            }

            //界膜上漂浮的符光
            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                Vector2 offset = Main.rand.NextVector2CircularEdge(150f, 150f) * SphereProgress;
                PRTLoader.NewParticle<PRT_Spark>(Owner.Center + offset, offset.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2) * 2f
                    , Main.rand.NextBool() ? Color.Cyan : Color.Gold, Main.rand.NextFloat(0.4f, 0.8f)).Configure(false, 16);
            }

            Lighting.AddLight(Owner.Center, new Vector3(0.3f, 0.6f, 0.9f) * SphereProgress);
        }

        public override void OnKill(int timeLeft) {
            //离开高维领域：释放毁灭性的现实震荡
            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Owner.FromObjectGetParent(), Owner.Center, Vector2.Zero
                    , ModContent.ProjectileType<RealityShockProj>(), 80000, 12f, Owner.whoAmI);
            }

            SoundEngine.PlaySound(SoundID.DD2_BetsySummon with { Pitch = 0.2f, Volume = 1.6f }, Owner.Center);

            if (!VaultUtils.isServer) {
                for (int i = 0; i < 32; i++) {
                    float rot = MathHelper.TwoPi / 32f * i;
                    Vector2 vr = rot.ToRotationVector2();
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(Owner.Center, vr * Main.rand.NextFloat(3f, 9f)
                        , Main.rand.NextBool() ? Color.Gold : Color.MediumPurple, Main.rand.NextFloat(0.7f, 1.4f)).Configure(false, 30);
                }
            }
        }

        public void Warp() {
            Texture2D warpTex = TextureAssets.Projectile[Type].Value;
            Color warpColor = new Color(30, 35, 60) * SphereProgress;
            Main.spriteBatch.Draw(warpTex, Projectile.Center - Main.screenPosition, null, warpColor
                , Main.GlobalTimeWrappedHourly * 0.7f, warpTex.Size() / 2
                , 0.6f * SphereProgress, SpriteEffects.None, 0f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.ArcaneHighDimension?.Value;
            Texture2D canvas = CWRAsset.Placeholder_White?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            float progress = SphereProgress;

            if (progress <= 0.01f) {
                return false;
            }

            if (shader == null || canvas == null || noise == null) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Color glowColor = new Color(70, 140, 255, 0) * (progress * 0.6f);
                Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null, glowColor
                    , 0f, glow.Size() / 2, 5.5f * progress, SpriteEffects.None);
                return false;
            }

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + seed * 23f);
            shader.Parameters["sphereProgress"]?.SetValue(progress);
            shader.Parameters["fadeAlpha"]?.SetValue(1f);
            shader.Parameters["seed"]?.SetValue(seed);

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float drawSize = 380f;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Main.spriteBatch.Draw(canvas, drawPos, null, Color.White
                , 0f, canvas.Size() * 0.5f, new Vector2(drawSize, drawSize), SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }

    /// <summary>
    /// 现实震荡，离开高维领域或时间回溯时的扩张冲击波
    /// <br/><see cref="Projectile.ai"/>[1]：样式，0为金紫现实震荡，1为青白时间回溯
    /// </summary>
    public class RealityShockProj : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        /// <summary> 总持续时间(帧) </summary>
        public const int Lifetime = 75;
        /// <summary> 冲击波最大半径(像素) </summary>
        public const float MaxRadius = 560f;
        private int Time;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 64;
            Projectile.timeLeft = Lifetime;
            Projectile.aiStyle = -1;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = DamageClass.Magic;
        }

        public bool CanDrawCustom() => false;
        public override bool ShouldUpdatePosition() => false;

        /// <summary> 冲击波扩张进度 0~1 </summary>
        public float Progress => CWRUtils.EaseOutCubic(Time / (float)Lifetime);
        /// <summary> 当前冲击波前沿半径 </summary>
        public float CurrentRadius => MaxRadius * Progress;

        private bool RewindStyle => Projectile.ai[1] == 1f;

        public override void AI() {
            Time++;

            if (Time == 1) {
                if (Projectile.localAI[1] == 0f) {
                    Projectile.localAI[1] = Main.rand.NextFloat(0.1f, 1f);
                }
                if (Main.LocalPlayer.Distance(Projectile.Center) < 1600f) {
                    Main.LocalPlayer.CWR().GetScreenShake(RewindStyle ? 8f : 14f);
                }
                if (CWRServerConfig.Instance.LensEasing) {
                    Main.SetCameraLerp(0.12f, 45);
                }
                SoundEngine.PlaySound(SoundID.DD2_BetsySummon with { Pitch = RewindStyle ? 0.5f : -0.4f, Volume = 1.8f }, Projectile.Center);
            }

            //冲击波前沿粒子
            if (!VaultUtils.isServer && Time % 4 == 0 && Progress < 0.92f) {
                Color edge = RewindStyle ? Color.Cyan : Color.MediumPurple;
                Color core = RewindStyle ? Color.White : Color.Gold;
                for (int i = 0; i < 6; i++) {
                    float rot = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = Projectile.Center + rot.ToRotationVector2() * CurrentRadius;
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(pos, rot.ToRotationVector2() * Main.rand.NextFloat(2f, 5f)
                        , Main.rand.NextBool() ? core : edge, Main.rand.NextFloat(0.6f, 1.2f)).Configure(false, 20);
                }
            }

            Vector3 lightColor = RewindStyle ? new Vector3(0.4f, 0.9f, 1.1f) : new Vector3(1.1f, 0.7f, 1.2f);
            Lighting.AddLight(Projectile.Center, lightColor * (1f - Progress));
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float dist = Vector2.Distance(targetHitbox.Center.ToVector2(), Projectile.Center);
            return dist <= CurrentRadius + 60f;
        }

        public void Warp() {
            Texture2D warpTex = TextureAssets.Projectile[Type].Value;
            float fade = 1f - Progress;
            Color warpColor = new Color(50, 25, 60) * fade;
            Main.spriteBatch.Draw(warpTex, Projectile.Center - Main.screenPosition, null, warpColor
                , Time * 0.05f, warpTex.Size() / 2
                , CurrentRadius / 100f, SpriteEffects.None, 0f);
        }

        public void DrawCustom(SpriteBatch spriteBatch) { }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.ArcaneRealityTremor?.Value;
            Texture2D canvas = CWRAsset.Placeholder_White?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;

            float fadeAlpha = 1f - CWRUtils.EaseInQuad(Time / (float)Lifetime);

            if (shader == null || canvas == null || noise == null) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Color glowColor = (RewindStyle ? new Color(80, 200, 255, 0) : new Color(255, 200, 90, 0)) * fadeAlpha;
                Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null, glowColor
                    , 0f, glow.Size() / 2, CurrentRadius / 24f, SpriteEffects.None);
                return false;
            }

            Vector3 coreColor = RewindStyle ? new Vector3(0.85f, 1f, 1f) : new Vector3(1f, 0.88f, 0.55f);
            Vector3 edgeColor = RewindStyle ? new Vector3(0.2f, 0.7f, 1f) : new Vector3(0.6f, 0.3f, 1f);

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["ringProgress"]?.SetValue(Progress * 0.92f);
            shader.Parameters["ringThickness"]?.SetValue(0.075f + (1f - Progress) * 0.05f);
            shader.Parameters["fadeAlpha"]?.SetValue(fadeAlpha);
            shader.Parameters["seed"]?.SetValue(Projectile.localAI[1]);
            shader.Parameters["coreColor"]?.SetValue(coreColor);
            shader.Parameters["edgeColor"]?.SetValue(edgeColor);

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float drawDiameter = MaxRadius * 2.4f;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Main.spriteBatch.Draw(canvas, drawPos, null, Color.White
                , 0f, canvas.Size() * 0.5f, new Vector2(drawDiameter, drawDiameter), SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }

    /// <summary>
    /// 永恒奥秘之座玩家类，管理所有饰品机制
    /// </summary>
    internal class ArcaneThronePlayer : ModPlayer
    {
        public bool Alive;
        /// <summary> 全知状态（魔力高于90%） </summary>
        public bool Omniscience;
        /// <summary> 根源演算激活 </summary>
        public bool RootCalcActive;
        /// <summary> 根源演算剩余时间(帧) </summary>
        public int RootCalcTimer;
        /// <summary> 根源演算冷却(帧) </summary>
        public int RootCalcCooldown;
        /// <summary> 真理跃迁冷却(帧) </summary>
        public int TruthLeapCooldown;
        /// <summary> 时间回溯冷却(帧) </summary>
        public int RewindCooldown;
        /// <summary> 奥术裂隙内部冷却(帧) </summary>
        public int RiftSpawnCooldown;

        //时间回溯快照环形缓冲区，每30帧采样一次，覆盖过去5秒
        private const int SnapshotInterval = 30;
        private const int SnapshotCount = 10;
        private readonly Vector2[] snapPos = new Vector2[SnapshotCount];
        private readonly int[] snapLife = new int[SnapshotCount];
        private readonly int[] snapMana = new int[SnapshotCount];
        private int snapHead;
        private int snapStored;
        private int snapTimer;

        public override void Initialize() {
            Alive = false;
            Omniscience = false;
            RootCalcActive = false;
        }

        public override void ResetEffects() {
            Alive = false;

            if (RootCalcCooldown > 0) {
                RootCalcCooldown--;
            }
            if (RootCalcTimer > 0) {
                RootCalcTimer--;
                if (RootCalcTimer <= 0) {
                    RootCalcActive = false;
                }
            }
            if (TruthLeapCooldown > 0) {
                TruthLeapCooldown--;
            }
            if (RewindCooldown > 0) {
                RewindCooldown--;
            }
            if (RiftSpawnCooldown > 0) {
                RiftSpawnCooldown--;
            }
        }

        public override void PostUpdateMiscEffects() {
            bool leaping = Player.ownedProjectileCounts[ModContent.ProjectileType<TruthLeapProj>()] > 0;

            if (!Alive) {
                Omniscience = false;
                RootCalcActive = false;
                RootCalcTimer = 0;
                snapStored = 0;
                snapTimer = 0;
                if (!leaping && Player.opacityForAnimation < 1f) {
                    RecoverOpacity();
                }
                return;
            }

            //魔力恢复永不间断
            Player.manaRegenDelay = 0;

            //全知状态判定
            Omniscience = Player.statManaMax2 > 0 && Player.statMana >= (int)(Player.statManaMax2 * 0.9f);

            //全知状态下环绕的金色符星
            if (Omniscience && !VaultUtils.isServer && Main.rand.NextBool(9)) {
                float rot = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Player.Center + rot.ToRotationVector2() * Main.rand.NextFloat(34f, 50f);
                PRTLoader.NewParticle<PRT_HeavenfallStar>(pos, rot.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 1.5f
                    , Main.rand.NextBool(3) ? Color.Gold : Color.MediumPurple, Main.rand.NextFloat(0.3f, 0.6f)).Configure(false, 18);
            }

            //记录时间回溯快照
            if (++snapTimer >= SnapshotInterval) {
                snapTimer = 0;
                snapPos[snapHead] = Player.Center;
                snapLife[snapHead] = Player.statLife;
                snapMana[snapHead] = Player.statMana;
                snapHead = (snapHead + 1) % SnapshotCount;
                if (snapStored < SnapshotCount) {
                    snapStored++;
                }
            }

            //跃迁结束后平滑恢复透明度
            if (!leaping && Player.opacityForAnimation < 1f) {
                RecoverOpacity();
            }
        }

        private void RecoverOpacity() {
            Player.opacityForAnimation = MathHelper.Lerp(Player.opacityForAnimation, 1f, 0.15f);
            if (Player.opacityForAnimation > 0.98f) {
                Player.opacityForAnimation = 1f;
            }
        }

        public override void PostUpdateRunSpeeds() {
            if (!Alive) {
                return;
            }

            //高维领域中：无重力自由移动
            if (Player.ownedProjectileCounts[ModContent.ProjectileType<TruthLeapProj>()] > 0) {
                Player.gravity = 0f;
                Vector2 dir = Vector2.Zero;
                if (Player.controlLeft) {
                    dir.X -= 1f;
                }
                if (Player.controlRight) {
                    dir.X += 1f;
                }
                if (Player.controlUp || Player.controlJump) {
                    dir.Y -= 1f;
                }
                if (Player.controlDown) {
                    dir.Y += 1f;
                }
                if (dir != Vector2.Zero) {
                    Player.velocity = Vector2.Lerp(Player.velocity, dir.SafeNormalize(Vector2.Zero) * 16f, 0.16f);
                }
                else {
                    Player.velocity *= 0.88f;
                }
            }
        }

        public override void PreUpdateMovement() {
            if (!Alive) {
                return;
            }

            //真理跃迁，按下专属按键撕裂空间进入高维领域
            if (CWRKeySystem.Accessory_Skills.JustPressed && TruthLeapCooldown <= 0
                && Player.ownedProjectileCounts[ModContent.ProjectileType<TruthLeapProj>()] == 0
                && Player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(Player.FromObjectGetParent(), Player.Center, Vector2.Zero
                    , ModContent.ProjectileType<TruthLeapProj>(), 0, 0, Player.whoAmI);
                TruthLeapCooldown = 1800;
            }

            //根源演算，手持魔法武器时右键激活
            if (Player.controlUseTile && Player.releaseUseItem
                && !RootCalcActive && RootCalcCooldown <= 0
                && Player.whoAmI == Main.myPlayer
                && Player.HeldItem.DamageType.CountsAsClass<MagicDamageClass>()) {
                RootCalcActive = true;
                RootCalcTimer = 600;
                RootCalcCooldown = 3600;

                SoundEngine.PlaySound(CWRSound.Pecharge with { Pitch = 0.3f, Volume = 1.5f }, Player.Center);

                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 50; i++) {
                        float rot = MathHelper.TwoPi / 50f * i;
                        Vector2 vr = rot.ToRotationVector2();
                        PRTLoader.NewParticle<PRT_Spark>(Player.Center, vr * Main.rand.NextFloat(2f, 5f)
                            , Color.MediumPurple, Main.rand.NextFloat(1f, 2f)).Configure(false, 30);
                    }
                }
            }
        }

        /// <summary>
        /// 魔法弹幕命中时附加随机禁忌诅咒并撕开奥术裂隙，排除衍生弹幕防止级联
        /// </summary>
        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone) {
            if (!Alive) {
                return;
            }

            int riftType = ModContent.ProjectileType<ArcaneRiftProj>();
            int shockType = ModContent.ProjectileType<RealityShockProj>();
            int leapType = ModContent.ProjectileType<TruthLeapProj>();
            if (proj.type == riftType || proj.type == shockType || proj.type == leapType) {
                return;
            }

            if (!hit.DamageType.CountsAsClass<MagicDamageClass>()) {
                return;
            }

            ApplyRandomCurse(target);

            //命中时有概率在现实帷幕上撕开奥术裂隙，限制同时存在数量
            if (RiftSpawnCooldown <= 0 && Main.rand.NextBool(4)
                && Player.ownedProjectileCounts[riftType] < 4
                && Player.whoAmI == Main.myPlayer) {
                RiftSpawnCooldown = 12;
                Projectile.NewProjectile(Player.FromObjectGetParent(), target.Center, Vector2.Zero
                    , riftType, hit.SourceDamage, 0, Player.whoAmI);
            }
        }

        /// <summary>
        /// 附加五种禁忌诅咒之一：虚空侵蚀、时停、灵魂燃烧、引力坍缩、超位崩解
        /// </summary>
        private static void ApplyRandomCurse(NPC target) {
            switch (Main.rand.Next(5)) {
                case 0:
                    target.AddBuff(ModContent.BuffType<VoidErosion>(), 600);
                    break;
                case 1:
                    //时停对Boss与冻结免疫单位无效，退化为灵魂燃烧
                    if (!target.boss && !CWRLoad.NPCValue.ImmuneFrozen[target.type]) {
                        target.AddBuff(ModContent.BuffType<TemporalStasis>(), 75);
                    }
                    else {
                        target.AddBuff(ModContent.BuffType<SoulBurning>(), 300);
                    }
                    break;
                case 2:
                    target.AddBuff(ModContent.BuffType<SoulBurning>(), 300);
                    break;
                case 3:
                    target.AddBuff(ModContent.BuffType<GravitationalCollapse>(), 240);
                    break;
                default:
                    target.AddBuff(ModContent.BuffType<HyperDisintegration>(), 300);
                    break;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (!Alive) {
                return;
            }

            //全知状态下魔法暴击伤害倍率提升至1000%（默认200%加上额外800%）
            if (Omniscience && modifiers.DamageType.CountsAsClass<MagicDamageClass>()) {
                modifiers.CritDamage += 8f;
            }
        }

        public override bool PreKill(double damage, int hitDirection, bool pvp
            , ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource) {
            if (Alive && RewindCooldown <= 0 && snapStored > 0) {
                RewindCooldown = 3600;

                //取出最旧的快照（约5秒之前）
                int idx = snapStored >= SnapshotCount ? snapHead : 0;
                Vector2 rewindPos = snapPos[idx];
                int rewindLife = Math.Clamp(snapLife[idx], (int)(Player.statLifeMax2 * 0.5f), Player.statLifeMax2);
                int rewindMana = Math.Clamp(snapMana[idx], 0, Player.statManaMax2);

                //回溯时刻的青白现实震荡，从致死位置炸开
                if (Player.whoAmI == Main.myPlayer) {
                    Projectile.NewProjectile(Player.FromObjectGetParent(), Player.Center, Vector2.Zero
                        , ModContent.ProjectileType<RealityShockProj>(), 30000, 8f, Player.whoAmI, 0, 1f);
                }

                if (!VaultUtils.isServer) {
                    //致死位置与回溯位置之间的时间残影粒子
                    int steps = (int)(Vector2.Distance(Player.Center, rewindPos) / 20f) + 1;
                    for (int i = 0; i < steps; i++) {
                        Vector2 pos = Vector2.Lerp(Player.Center, rewindPos, i / (float)steps);
                        PRTLoader.NewParticle<PRT_Spark>(pos, Main.rand.NextVector2Circular(1f, 1f)
                            , Color.Cyan, Main.rand.NextFloat(0.4f, 0.8f)).Configure(false, 20);
                    }
                }

                //时间回溯：恢复5秒前的生命、魔力与位置
                Player.statLife = rewindLife;
                Player.statMana = rewindMana;
                Player.Teleport(rewindPos, -1);
                Player.velocity = Vector2.Zero;
                Player.GivePlayerImmuneState(120);

                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.8f, Volume = 1.5f }, rewindPos);
                SoundEngine.PlaySound(CWRSound.Pecharge with { Pitch = 0.6f, Volume = 1.2f }, rewindPos);

                playSound = false;
                genDust = false;
                return false;
            }
            return base.PreKill(damage, hitDirection, pvp, ref playSound, ref genDust, ref damageSource);
        }

        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource) {
            //玩家真正死亡时重置所有冷却和状态
            RootCalcActive = false;
            RootCalcTimer = 0;
            RootCalcCooldown = 0;
            TruthLeapCooldown = 0;
            RewindCooldown = 0;
            RiftSpawnCooldown = 0;
            Omniscience = false;
            snapStored = 0;
            snapHead = 0;
            snapTimer = 0;
        }
    }

    /// <summary>
    /// 永恒奥秘之座全局弹幕，处理魔法弹幕的无限穿透、全知自动锁定与根源演算镜像复制
    /// </summary>
    internal class ArcaneEternityGlobalProj : GlobalProjectile
    {
        public override bool InstancePerEntity => true;
        /// <summary> 是否为根源演算生成的镜像法术 </summary>
        public bool IsMirrorImage;

        //镜像复制递归保护与每帧预算
        private static bool spawningMirrors;
        private static uint mirrorBudgetFrame = uint.MaxValue;
        private static int mirrorEventsThisFrame;

        private static bool IsOwnArtifact(int type) {
            return type == ModContent.ProjectileType<ArcaneRiftProj>()
                || type == ModContent.ProjectileType<RealityShockProj>()
                || type == ModContent.ProjectileType<TruthLeapProj>();
        }

        private static bool IsEligibleMagicProj(Projectile projectile) {
            if (!projectile.friendly || projectile.hostile || projectile.damage <= 0) {
                return false;
            }
            if (!projectile.DamageType.CountsAsClass<MagicDamageClass>()) {
                return false;
            }
            if (projectile.minion || projectile.sentry || Main.projPet[projectile.type]) {
                return false;
            }
            if (IsOwnArtifact(projectile.type)) {
                return false;
            }
            if (projectile.ModProjectile is BaseHeldProj) {
                return false;
            }
            return true;
        }

        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers) {
                return;
            }
            Player player = Main.player[projectile.owner];
            if (player == null || !player.active) {
                return;
            }
            ArcaneThronePlayer throne = player.GetModPlayer<ArcaneThronePlayer>();
            if (!throne.Alive || !IsEligibleMagicProj(projectile)) {
                return;
            }

            //所有魔法弹幕获得无限穿透能力
            if (projectile.penetrate >= 1) {
                projectile.penetrate = -1;
                projectile.maxPenetrate = -1;
                if (!projectile.usesLocalNPCImmunity && !projectile.usesIDStaticNPCImmunity) {
                    projectile.usesLocalNPCImmunity = true;
                    projectile.localNPCHitCooldown = 12;
                }
            }

            //根源演算：复制5道镜像法术
            if (spawningMirrors || IsMirrorImage) {
                return;
            }
            if (!throne.RootCalcActive || projectile.owner != Main.myPlayer) {
                return;
            }

            //每帧镜像事件预算，防止裂变型法术引起弹幕爆炸
            if (Main.GameUpdateCount != mirrorBudgetFrame) {
                mirrorBudgetFrame = Main.GameUpdateCount;
                mirrorEventsThisFrame = 0;
            }
            if (mirrorEventsThisFrame >= 6) {
                return;
            }
            mirrorEventsThisFrame++;

            spawningMirrors = true;
            try {
                int mirrorDamage = Math.Max(1, (int)(projectile.damage * 0.35f));
                for (int i = 0; i < 5; i++) {
                    Vector2 vel = projectile.velocity.RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f));
                    Vector2 pos = projectile.Center + Main.rand.NextVector2Circular(24f, 24f);
                    int idx = Projectile.NewProjectile(player.FromObjectGetParent(), pos, vel
                        , projectile.type, mirrorDamage, projectile.knockBack * 0.5f, projectile.owner
                        , projectile.ai[0], projectile.ai[1], projectile.ai[2]);
                    if (idx >= 0 && idx < Main.maxProjectiles) {
                        Projectile mirror = Main.projectile[idx];
                        mirror.GetGlobalProjectile<ArcaneEternityGlobalProj>().IsMirrorImage = true;
                        mirror.netUpdate = true;
                    }
                }
            }
            finally {
                spawningMirrors = false;
            }
        }

        public override void PostAI(Projectile projectile) {
            if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers) {
                return;
            }
            Player player = Main.player[projectile.owner];
            if (player == null || !player.active) {
                return;
            }
            ArcaneThronePlayer throne = player.GetModPlayer<ArcaneThronePlayer>();
            if (!throne.Alive) {
                return;
            }
            if (!IsEligibleMagicProj(projectile)) {
                return;
            }

            //镜像法术的紫色残光
            if (IsMirrorImage && !VaultUtils.isServer && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Spark>(projectile.Center, projectile.velocity * 0.05f
                    , Color.Violet, Main.rand.NextFloat(0.3f, 0.6f)).Configure(false, 10);
            }

            //全知状态：所有魔法弹幕自动锁定敌人
            if (!throne.Omniscience || projectile.velocity.Length() < 1f) {
                return;
            }
            NPC target = projectile.Center.FindClosestNPC(1000);
            if (target != null) {
                projectile.SmoothHomingBehavior(target.Center, 1f, 0.085f);
            }
        }
    }
}
