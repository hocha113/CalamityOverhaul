using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 分赃金币臼弹：海盗团在集结旗点抛洒的鎏金炮弹。抛物线翻飞（金币绕短轴翻面），
    /// 落地或砸中敌人即炸开金屑并附点金指；飞行拖金屑微光，命中金币脆响。
    /// 材质：鎏金铸币，翻面时以横轴压扁模拟侧棱反光
    /// </summary>
    internal class GsPiratePlunderProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        private static readonly Color CoinGold = new(255, 210, 96);
        private static readonly Color CoinDeep = new(178, 122, 32);
        private static readonly Color CoinShine = new(255, 246, 214);

        private ref float Life => ref Projectile.localAI[0];

        private float Seed => Projectile.identity * 0.8923f % MathHelper.TwoPi;

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.35f, Pitch = 0.5f },
                    Projectile.Center);
            }
            //抛物线：40 帧后开始下坠，横速微阻
            if (Life > 12f) {
                Projectile.velocity.Y += 0.32f;
                if (Projectile.velocity.Y > 14f) {
                    Projectile.velocity.Y = 14f;
                }
            }
            Projectile.velocity.X *= 0.995f;
            Projectile.rotation += 0.2f * Math.Sign(Projectile.velocity.X == 0f
                ? 1f : Projectile.velocity.X);

            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center, CoinGold.ToVector3() * 0.14f);
            //金屑微光尾
            if (Life % 5f == 0f) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center,
                    Main.rand.NextVector2Circular(0.4f, 0.4f), CoinGold, 0.08f)?.Configure(10, 0.7f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //点金指：被砸中的敌人掉更多钱（海盗的职业素养）
            target.AddBuff(BuffID.Midas, 240);
            Pop();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Pop();
            return true;
        }

        /// <summary>炸金屑（命中与落地共用的收尾反馈）</summary>
        private void Pop() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Coins with { Volume = 0.5f }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    (-Vector2.UnitY).RotatedBy(Main.rand.NextFloat(-1.1f, 1.1f))
                        * Main.rand.NextFloat(1.5f, 4f),
                    Main.rand.NextBool() ? CoinGold : CoinShine,
                    Main.rand.NextFloat(0.2f, 0.34f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D soft = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D glint = CWRAsset.StarGlow01?.Value;
            if (soft == null || glow == null || glint == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //绕短轴翻面：横向压扁往复，identity 去同相
            float flip = (float)Math.Cos(Life * 0.32f + Seed);
            float faceW = Math.Max(Math.Abs(flip), 0.14f);

            //币身：暗金垫底 + 金面（真 alpha 层叠出厚度感）
            Main.EntitySpriteDraw(soft, pos, null, CoinDeep * 0.9f, Projectile.rotation,
                soft.Size() / 2f, new Vector2(13f * faceW / soft.Width, 13f / soft.Height),
                SpriteEffects.None, 0);
            Main.EntitySpriteDraw(soft, pos, null, CoinGold * 0.85f, Projectile.rotation,
                soft.Size() / 2f, new Vector2(10f * faceW / soft.Width, 10f / soft.Height),
                SpriteEffects.None, 0);
            //侧棱反光：翻至侧面时一道亮线（加色）
            if (faceW < 0.4f) {
                Main.EntitySpriteDraw(soft, pos, null, (CoinShine with { A = 0 }) * 0.8f,
                    Projectile.rotation, soft.Size() / 2f,
                    new Vector2(3f / soft.Width, 12f / soft.Height), SpriteEffects.None, 0);
            }
            //正面星芒闪（翻到正面的瞬间最亮，加色）
            float glintPow = MathHelper.Clamp((Math.Abs(flip) - 0.7f) / 0.3f, 0f, 1f);
            if (glintPow > 0f) {
                Main.EntitySpriteDraw(glint, pos, null, (CoinShine with { A = 0 }) * glintPow,
                    Seed + Life * 0.05f, glint.Size() / 2f, 0.24f * glintPow, SpriteEffects.None, 0);
            }
            //金辉底光
            Main.EntitySpriteDraw(glow, pos, null, (CoinGold with { A = 0 }) * 0.35f, 0f,
                glow.Size() / 2f, 0.3f, SpriteEffects.None, 0);
            return false;
        }
    }
}
