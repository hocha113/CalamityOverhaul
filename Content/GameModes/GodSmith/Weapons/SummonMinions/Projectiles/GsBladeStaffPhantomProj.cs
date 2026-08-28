using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 刀刃法杖「八卦剑影」：破防斩第 6 击落地时在目标身上交叉闪过的双匕幻影。
    /// 真弹幕结算一段 40% 召唤伤害，owner 生成全端可见。
    /// 自绘：双把附魔匕首贴图交叉外扫（金色加色缘 + 半透明本体）+ 十字闪
    /// </summary>
    internal class GsBladeStaffPhantomProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonMinionsA";

        private static readonly Color BladeGold = new(255, 206, 110);

        private const int LifeFrames = 18;
        private const int DamageFrames = 5;

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 90;
            Projectile.height = 90;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => Elapsed < DamageFrames ? null : false;

        public override void AI() {
            if (Elapsed == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.5f, Pitch = 0.5f }, Projectile.Center);
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                        BladeGold, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, Main.rand.Next(12, 20));
                }
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                    BladeGold, 0.15f)?.Configure(8, 0.8f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //双匕交叉外扫：原版附魔匕首贴图垫底本体，金色加色缘压上
            Main.instance.LoadProjectile(ProjectileID.Smolstar);
            Texture2D blade = TextureAssets.Projectile[ProjectileID.Smolstar].Value;
            float t = Elapsed / (float)LifeFrames;
            float sweep = MathHelper.Lerp(0.35f, 1.15f, 1f - (1f - t) * (1f - t));
            float alpha = 1f - t;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = blade.Size() / 2f;
            Color edge = BladeGold * (0.7f * alpha);
            edge.A = 0;
            //identity 定相：两把剑影的基角随实例错开，多次触发不重影
            float baseRot = Projectile.identity * 0.9f;
            for (int i = 0; i < 2; i++) {
                float rot = baseRot + i * MathHelper.PiOver2 + sweep * (i == 0 ? 1f : -1f);
                Main.EntitySpriteDraw(blade, pos, null, Color.White * (0.5f * alpha), rot,
                    origin, 1.1f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(blade, pos, null, edge, rot,
                    origin, 1.22f, SpriteEffects.None, 0);
            }
            //十字闪：爆帧后快速衰减
            Texture2D cross = CWRUtils.GetT2DAsset(CWRConstant.Masking + "StarFlare01")?.Value;
            if (cross != null && t < 0.5f) {
                float f = 1f - t * 2f;
                Color c = BladeGold * (0.8f * f);
                c.A = 0;
                Main.EntitySpriteDraw(cross, pos, null, c, baseRot,
                    cross.Size() / 2f, 0.42f * f + 0.1f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
