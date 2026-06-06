using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Marbles
{
    /// <summary>
    /// 大理石战盾：+防御 + 抗击退；维护可再生的"石卫"护盾吸收伤害，破碎迸射碎片；
    /// 按饰品技能键短暂举盾完美格挡并反弹弹幕
    /// </summary>
    internal class MarbleShield : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 34;
            Item.accessory = true;
            Item.defense = 5;
            Item.value = Item.sellPrice(0, 0, 90, 0);
            Item.rare = ItemRarityID.Orange;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            MarbleShieldPlayer mp = player.GetModPlayer<MarbleShieldPlayer>();
            mp.Equipped = true;
            mp.HideVisual = hideVisual;
            player.statDefense += 4;
            player.GetKnockback<MeleeDamageClass>() += 0.1f;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.InsertHotkeyBinding(CWRKeySystem.Accessory_Skills, "[KEY]"
                , CWRKeySystem.Notbound.Value + $"[{CWRKeySystem.Accessory_Skills.DisplayName}]");
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.MarbleBlock, 20)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 10)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    internal class MarbleShieldPlayer : ModPlayer
    {
        public bool Equipped;
        public bool HideVisual;
        public int RechargeTimer;
        public int BlockTimer;
        public int BlockCooldown;
        public float RingAlpha;

        public const int RechargeTime = 720;
        public const int BlockWindow = 22;
        public const int BlockCooldownTime = 300;

        public bool BarrierReady => RechargeTimer <= 0;
        public bool Blocking => BlockTimer > 0;

        public override void ResetEffects() {
            Equipped = false;
            HideVisual = false;
            if (RechargeTimer > 0) {
                RechargeTimer--;
            }
            if (BlockTimer > 0) {
                BlockTimer--;
            }
            if (BlockCooldown > 0) {
                BlockCooldown--;
            }
        }

        public override void ProcessTriggers(Terraria.GameInput.TriggersSet triggersSet) {
            if (!Equipped || Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (CWRKeySystem.Accessory_Skills.JustPressed && BlockCooldown <= 0) {
                BlockTimer = BlockWindow;
                BlockCooldown = BlockCooldownTime;
                SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.3f }, Player.Center);
            }
        }

        public override void PostUpdate() {
            if (Equipped && Blocking) {
                ReflectNearbyProjectiles();
            }
            if (Equipped && Player.whoAmI == Main.myPlayer
                && Player.CountProjectilesOfID<MarbleAegisRing>() == 0) {
                Projectile.NewProjectile(Player.FromObjectGetParent(), Player.Center, Vector2.Zero
                    , ModContent.ProjectileType<MarbleAegisRing>(), 0, 0, Player.whoAmI);
            }
        }

        //完美格挡窗口：免伤
        public override bool FreeDodge(Player.HurtInfo info) {
            if (Equipped && Blocking) {
                BurstGuard(40, true);
                return true;
            }
            return false;
        }

        //石卫护盾：再生式吸收一次伤害
        public override bool ConsumableDodge(Player.HurtInfo info) {
            if (Equipped && BarrierReady) {
                RechargeTimer = RechargeTime;
                BurstGuard(24, false);
                return true;
            }
            return false;
        }

        private void BurstGuard(int damage, bool strong) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = strong ? -0.1f : 0.2f, Volume = 1.1f }, Player.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(Player.Center, Vector2.Zero
                    , GraniteMarbleVFX.MarbleGold, 0).Configure(0.15f, strong ? 1.4f : 0.9f, 24);
                for (int i = 0; i < (strong ? 16 : 10); i++) {
                    PRTLoader.NewParticle<PRT_Smoke>(Player.Center, Main.rand.NextVector2Circular(5f, 5f)
                        , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.4f, 0.7f)).Configure(24, 0.7f, 0.05f);
                }
            }

            if (Player.whoAmI == Main.myPlayer) {
                int count = strong ? 8 : 5;
                for (int i = 0; i < count; i++) {
                    Vector2 v = (MathHelper.TwoPi / count * i + Main.rand.NextFloat(0.3f)).ToRotationVector2() * Main.rand.NextFloat(7f, 10f);
                    Projectile.NewProjectile(Player.FromObjectGetParent(), Player.Center, v
                        , ModContent.ProjectileType<MarbleShard>(), damage, 4f, Player.whoAmI);
                }
            }
        }

        private void ReflectNearbyProjectiles() {
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (!proj.hostile || proj.friendly || proj.damage <= 0) {
                    continue;
                }
                if (Player.Center.To(proj.Center).Length() > 160f) {
                    continue;
                }
                proj.hostile = false;
                proj.friendly = true;
                proj.owner = Player.whoAmI;
                proj.velocity = -proj.velocity;
                proj.netUpdate = true;
                if (!VaultUtils.isServer) {
                    PRTLoader.NewParticle<PRT_Sparkle>(proj.Center, Vector2.Zero
                        , GraniteMarbleVFX.MarbleGold, 0.6f).Configure(GraniteMarbleVFX.MarbleGold, 14, 0.2f, 0.7f);
                }
            }
        }
    }

    /// <summary>
    /// 大理石战盾的环绕视觉：环绕玩家旋转的大理石碎环，状态决定亮度
    /// </summary>
    internal class MarbleAegisRing : BaseHeldProj, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private float Time;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 120;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.hide = true;
        }

        public override void AI() {
            Projectile.timeLeft = 2;
            MarbleShieldPlayer mp = Owner.GetModPlayer<MarbleShieldPlayer>();
            if (!Owner.Alives() || !mp.Equipped) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = Owner.GetPlayerStabilityCenter();
            Time += 1f;

            float target = mp.HideVisual ? 0f : (mp.Blocking ? 1.3f : (mp.BarrierReady ? 0.85f : 0.28f));
            mp.RingAlpha = MathHelper.Lerp(mp.RingAlpha, target, 0.12f);

            if (mp.RingAlpha > 0.4f) {
                Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.MarbleGold.ToVector3() * 0.4f * mp.RingAlpha);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            MarbleShieldPlayer mp = Owner.GetModPlayer<MarbleShieldPlayer>();
            float alpha = mp.RingAlpha;
            if (alpha <= 0.02f) {
                return;
            }

            Vector2 center = Owner.GetPlayerStabilityCenter() - Main.screenPosition;
            Texture2D ring = CWRAsset.DiffusionCircle.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;

            Color gold = GraniteMarbleVFX.MarbleGold; gold.A = 0;
            Color core = GraniteMarbleVFX.MarbleCore; core.A = 0;

            float radius = 58f + (mp.Blocking ? 6f : 0f);
            spriteBatch.Draw(ring, center, null, gold * 0.4f * alpha, Time * 0.02f, ring.Size() / 2f
                , (radius * 2f) / ring.Width, SpriteEffects.None, 0f);

            int shards = 6;
            float spin = Time * 0.03f;
            for (int i = 0; i < shards; i++) {
                float a = spin + MathHelper.TwoPi / shards * i;
                Vector2 pos = center + a.ToRotationVector2() * radius;
                spriteBatch.Draw(glow, pos, null, gold * 0.6f * alpha, 0f, glow.Size() / 2f, 0.45f * alpha, SpriteEffects.None, 0f);
                spriteBatch.Draw(star, pos, null, core * 0.7f * alpha, a, star.Size() / 2f, 0.08f * alpha, SpriteEffects.None, 0f);
            }
        }
    }
}
