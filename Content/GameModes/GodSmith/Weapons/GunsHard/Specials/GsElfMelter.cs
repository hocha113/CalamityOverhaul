using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsHard.Specials
{
    /// <summary>
    /// 融雪精灵喷火器重铸（L3 手持接管）：喷射器框架换圣诞皮。<br/>
    /// [融雪扇焰] 红绿焰混冰融水汽的宽锥；[礼物投射] 窄矛喷焰，且每 48 tick 抛出一只
    /// 燃烧礼物盒（抛物线，落地裂 3 股火舌扇，按 8 发凝胶价 PickAmmo 补扣，不足 8 只喷焰不抛盒）
    /// </summary>
    internal class GsElfMelter : GodSmithScheme
    {
        public override int TargetItemID => ItemID.ElfMelter;

        public override string GsFamily => "GunsSpecial";

        protected override string GsDescFallback =>
            "Reforged: festive fire stream with two modes. Melt Fan sprays wide red-green flame laced with thaw mist; Gift Toss focuses a lance jet and lobs a burning present every 0.8s for 8 gel"
            + "\nRight click to switch modes. Pressure rules still apply: long sprays shorten the flame";

        /// <summary>模式名（[0]=融雪扇焰 [1]=礼物投射）</summary>
        internal static LocalizedText[] ModeNames;

        /// <summary>下次举枪沿用的档位；只在本地玩家路径读写</summary>
        internal int preferredMode;
        private int switchCd;

        public override void GsSetStaticDefaults() {
            ModeNames = [
                this.GetLocalization("Mode0", () => "Melt Fan"),
                this.GetLocalization("Mode1", () => "Gift Toss"),
            ];
        }

        public override bool? GsAltFunctionUse(Item item, Player player) => true;

        public override bool? GsCanUseItem(Item item, Player player) {
            if (HeldAlive<GsElfMelterHeld>(player)) {
                return false;
            }
            if (player.altFunctionUse == 2) {
                if (player.whoAmI == Main.myPlayer && switchCd <= 0) {
                    switchCd = 12;
                    preferredMode = preferredMode == 0 ? 1 : 0;
                    GsGunPose.ModeSwitchFeedback(player, ModeNames[preferredMode].Value);
                }
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    ModContent.ProjectileType<GsElfMelterHeld>(),
                    player.GetWeaponDamage(item), item.knockBack, player.whoAmI, preferredMode);
            }
            return false;
        }

        public override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (switchCd > 0) {
                switchCd--;
            }
        }
    }

    /// <summary>
    /// 融雪精灵喷火器手持弹幕：圣诞色板焰流 + 礼物投射节拍。ai[0]=档位
    /// </summary>
    internal class GsElfMelterHeld : GsFlamerHeldBase
    {
        protected override int HeldTargetItemID => ItemID.ElfMelter;

        protected override int JetPalette => 1;

        protected override Color MuzzleColor => new(120, 235, 130);

        /// <summary>礼物投射间隔（tick）</summary>
        private const int GiftInterval = 48;
        /// <summary>每只礼物的凝胶价</summary>
        private const int GiftGelCost = 8;

        /// <summary>礼物节拍计时，owner 端推进</summary>
        private int giftTimer;

        protected override void OnModeSwitched(int newMode) {
            if (GodSmithScheme.TryGetScheme(ItemID.ElfMelter, out GodSmithScheme scheme)
                && scheme is GsElfMelter melter) {
                melter.preferredMode = newMode;
            }
            GsGunPose.ModeSwitchFeedback(Owner, GsElfMelter.ModeNames[newMode].Value);
        }

        protected override void OnFireExtra(Vector2 muzzle, int ammoDamage, float knockback) {
            //礼物投射档：喷焰之余按节拍抛燃烧礼物盒（owner 路径，基类已保证）
            if (Mode != 1) {
                giftTimer = 0;
                return;
            }
            giftTimer += Math.Max(1, (int)MathF.Round(BaseFireInterval / MathF.Max(0.01f, Owner.GetWeaponAttackSpeed(Item))));
            if (giftTimer < GiftInterval) {
                return;
            }
            //凝胶余量不足 8 时只喷焰，不动礼物节拍以外的任何账
            if (Owner.CountItem(ItemID.Gel) < GiftGelCost) {
                giftTimer = GiftInterval;//余额恢复后立刻补抛
                return;
            }
            giftTimer = 0;
            //礼物价：首发参数已由基类耗掉 1 凝胶，这里再补扣 7 发
            for (int i = 0; i < GiftGelCost - 1; i++) {
                Owner.PickAmmo(Item, out _, out _, out _, out _, out _, false);
            }
            Vector2 lob = UnitToMouseV * 9.5f + new Vector2(0f, -4.5f);
            Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), muzzle, lob,
                ModContent.ProjectileType<GsPresentBombProj>(),
                (int)(ammoDamage * 3.5f), knockback * 1.5f, Owner.whoAmI);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item108 with { Volume = 0.5f, Pitch = 0.3f }, muzzle);
            }
        }
    }

    /// <summary>
    /// 燃烧礼物盒：抛物线飞行，落地或命中裂成 3 股向上外溅的火舌扇。
    /// 贴图复用原版 Present 物品贴图，加色叠层读作着火
    /// </summary>
    internal class GsPresentBombProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private static readonly Color BurnGlow = new(255, 150, 60);
        private static readonly Color RibbonGreen = new(110, 230, 120);

        private float Seed => Projectile.identity * 0.709f % 1f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            //抛物线：重力 + 轻微空气阻力，翻滚随速度
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.30f, 16f);
            Projectile.velocity.X *= 0.995f;
            Projectile.rotation += Projectile.velocity.X * 0.05f + 0.03f;

            Lighting.AddLight(Projectile.Center, BurnGlow.ToVector3() * 0.45f);
            if (!VaultUtils.isServer && Projectile.timeLeft % 5 == 0) {
                //盒身着火拖焰
                var flame = PRTLoader.NewParticle<PRT_HellFlame>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    -Projectile.velocity * 0.1f - Vector2.UnitY * 0.8f, Color.White, 0.35f);
                if (flame != null) {
                    flame.ai[0] = 0;
                    flame.ai[2] = 10;
                    flame.ai[3] = 16;
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.55f, Pitch = 0.25f }, Projectile.Center);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center,
                        Main.rand.NextVector2Circular(4f, 3f) - Vector2.UnitY * 2f,
                        Main.rand.NextBool() ? RibbonGreen : new Color(255, 100, 90),
                        Main.rand.NextFloat(0.5f, 0.9f))?.Configure(
                            Main.rand.NextBool() ? RibbonGreen : BurnGlow, Main.rand.Next(18, 30), 0.1f);
                }
            }
            //裂 3 股火舌扇：owner 权威生成，向上外 30 度扇开
            if (Projectile.owner != Main.myPlayer) {
                return;
            }
            int jetDamage = Math.Max(1, (int)(Projectile.damage * 0.45f));
            for (int i = -1; i <= 1; i++) {
                Vector2 vel = (-Vector2.UnitY).RotatedBy(i * MathHelper.ToRadians(30f)) * 7.5f;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                    Projectile.Center - Vector2.UnitY * 6f, vel,
                    ModContent.ProjectileType<GsFlameJetProj>(), jetDamage, 1.5f, Projectile.owner,
                    1f, 0f, 30f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire, 240);

        public override bool OnTileCollide(Vector2 oldVelocity) => true;

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadItem(ItemID.Present);
            Texture2D tex = TextureAssets.Item[ItemID.Present].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;

            Main.EntitySpriteDraw(tex, drawPos, null, lightColor, Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
            //着火的加色呼吸层
            float pulse = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + Seed * 9f);
            Color glow = BurnGlow * (0.45f * pulse);
            glow.A = 0;
            Main.EntitySpriteDraw(tex, drawPos, null, glow, Projectile.rotation, origin, 1.08f, SpriteEffects.None, 0);
            return false;
        }
    }
}
