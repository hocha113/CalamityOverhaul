using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 钛金三叉戟重铸：钛影分身。<br/>
    /// 材质：冷锻钛钢三叉刃。签名行为：①每次刺出的爆发帧，真刃上下各生成一道
    /// 平行钛影幻矛，直飞约 180 像素后消散（呼应钛金套装的影分身）
    /// ②幻矛半透明拉丝自绘，银灰冷光贯穿飞行 ③三线同刺——中线真刃重、旁线幻影轻，
    /// 命中反馈冷冽金属音，沉稳收势
    /// </summary>
    internal class GsTitaniumTrident : GsSpearScheme
    {
        public override int TargetItemID => ItemID.TitaniumTrident;

        protected override string GsDescFallback =>
            "Reforged: each thrust casts two parallel shade-tridents above and below the true blade," +
            "\nflying a short lane before dissolving";

        protected override int HeldProjType => ModContent.ProjectileType<GsTitaniumTridentHeld>();

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;//双幻矛已是三线输出，底伤只小补，综合 DPS 落在原版 105%~120%
    }

    /// <summary>
    /// 钛金三叉戟手持突刺：刺出爆发帧自持枪手上下各 24 像素生成平行钛影幻矛（各 35% 伤害）
    /// </summary>
    internal class GsTitaniumTridentHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.TitaniumTrident;

        //冷锻钛钢色板
        internal static readonly Color TitanBright = new(214, 222, 234);  //银灰亮
        internal static readonly Color TitanCold = new(150, 172, 204);    //冷钛蓝灰
        internal static readonly Color TitanDeep = new(58, 66, 88);       //深钛影

        //钛沉稳：重量阶梯的沉端
        protected override float WindupFrames => 5f;
        protected override float ThrustFrames => 6f;
        protected override float DwellFrames => 4f;
        protected override float RecoverFrames => 9f;
        protected override float RestHoldout => 11f;
        protected override float PullbackDist => 16f;
        protected override float StabReach => 66f;
        protected override float BladeLength => 96f;
        protected override float CollisionWidth => 33f;
        protected override float TipGreedRadius => 28f;
        protected override float ThrustEasePower => 3f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => 0.05f;
        protected override int HitboxSize => 54;
        protected override int HitstopFrames => 2;
        protected override float ThrustPitch => -0.26f;

        protected override Color EdgeColor => TitanBright;
        protected override Color CoreColor => TitanCold;
        protected override Color ShaftColor => TitanDeep with { A = 235 };

        protected override void OnThrustBurst() {
            //爆发帧上下各放一道平行幻矛（owner 端生成，随生成包过线）
            if (Projectile.IsOwnedByLocalPlayer()) {
                Vector2 side = stabUnit.RotatedBy(MathHelper.PiOver2);
                for (int i = 0; i < 2; i++) {
                    float sign = i == 0 ? 1f : -1f;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        Hand + side * (sign * 24f), stabUnit * 9f,
                        ModContent.ProjectileType<GsTitaniumTridentShadeProj>(),
                        (int)(BaseDamage * 0.35f), Projectile.knockBack * 0.25f, Owner.whoAmI);
                }
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = ThrustPitch }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.25f, Pitch = 0.4f }, Owner.Center);
            for (int i = 0; i < 3; i++) {
                Vector2 at = Vector2.Lerp(Hand, TipPos, Main.rand.NextFloat(0.5f, 1f));
                Color c = Main.rand.NextBool(3) ? TitanBright : TitanCold;
                PRTLoader.NewParticle<PRT_Spark>(at, stabUnit * Main.rand.NextFloat(4f, 8f), c,
                    Main.rand.NextFloat(0.32f, 0.55f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        /// <summary>命中反馈：冷冽钛金属音 + 银灰冷光迸溅（低饱和、无暖色）</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.4f, Pitch = 0.35f, MaxInstances = 3 }, target.Center);
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, TitanBright, 0.18f)?.Configure(9, 0.7f);
            for (int i = 0; i < 6; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(0.5) * Main.rand.NextFloat(3.5f, 8f);
                Color c = Main.rand.NextBool(3) ? TitanBright : TitanCold;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(true, Main.rand.Next(12, 18));
            }
        }
    }

    /// <summary>
    /// 钛影幻矛：真刃刺出时上下平行生成的半透明矛影，直飞约 180 像素（末段骤减）后消散。<br/>
    /// 自绘拉丝矛影：深钛影垫底 + 冷灰主拉丝 + 银亮矛尖，全程半透明、渐隐生命周期
    /// </summary>
    internal class GsTitaniumTridentShadeProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.TitaniumTrident");

        private ref float Life => ref Projectile.localAI[0];

        /// <summary>出生 2 帧淡入、末尾 7 帧淡出（幻影渐隐）</summary>
        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 2f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 7f, 0f, 1f));

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 22;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Life++;
            Projectile.rotation = Projectile.velocity.ToRotation();
            //末段骤减收势，幻影散在减速里
            if (Life > 14f) {
                Projectile.velocity *= 0.85f;
            }
            Lighting.AddLight(Projectile.Center, GsTitaniumTridentHeld.TitanCold.ToVector3() * (0.22f * VisualFade));
            if (VaultUtils.isServer) {
                return;
            }
            if (Life % 3f == 0f) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                    -Projectile.velocity * 0.06f, GsTitaniumTridentHeld.TitanCold,
                    Main.rand.NextFloat(0.25f, 0.4f))?.Configure(Main.rand.Next(7, 11), 0.4f, 1.5f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.25f, Pitch = 0.55f, MaxInstances = 3 }, target.Center);
            for (int i = 0; i < 4; i++) {
                Vector2 vel = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedByRandom(0.5) * Main.rand.NextFloat(2f, 5f);
                PRTLoader.NewParticle<PRT_Spark>(target.Center, vel,
                    Main.rand.NextBool() ? GsTitaniumTridentHeld.TitanBright : GsTitaniumTridentHeld.TitanCold,
                    Main.rand.NextFloat(0.28f, 0.45f))?.Configure(true, Main.rand.Next(9, 14));
            }
        }

        /// <summary>半透明拉丝矛影：深钛影垫底 + 冷灰主拉丝 + 银亮矛尖（加色 A=0，无随机）</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D streak = CWRAsset.LightShot?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (streak == null || glow == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float rot = Projectile.rotation;
            Vector2 origin = streak.Size() / 2f;
            float len = 96f / streak.Size().X;

            //深钛影垫底（宽一档，衬出矛影厚度）
            Main.spriteBatch.Draw(streak, pos, null,
                (GsTitaniumTridentHeld.TitanDeep with { A = 0 }) * (0.55f * fade), rot, origin,
                new Vector2(len, 0.20f), SpriteEffects.None, 0f);
            //冷灰主拉丝
            Main.spriteBatch.Draw(streak, pos, null,
                (GsTitaniumTridentHeld.TitanCold with { A = 0 }) * (0.7f * fade), rot, origin,
                new Vector2(len * 0.94f, 0.12f), SpriteEffects.None, 0f);
            //银亮芯线
            Main.spriteBatch.Draw(streak, pos, null,
                (GsTitaniumTridentHeld.TitanBright with { A = 0 }) * (0.55f * fade), rot, origin,
                new Vector2(len * 0.85f, 0.05f), SpriteEffects.None, 0f);
            //矛尖冷光点
            Vector2 tip = pos + Projectile.velocity.SafeNormalize(Vector2.UnitX) * 44f;
            Main.spriteBatch.Draw(glow, tip, null,
                (GsTitaniumTridentHeld.TitanBright with { A = 0 }) * (0.5f * fade), 0f,
                glow.Size() / 2f, 0.18f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
