using CalamityMod;
using CalamityMod.NPCs;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime
{
    internal class SetPosingStarm : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private float modeings = 5000;
        private float sengs;
        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.scale = 1f;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 380;
            Projectile.alpha = 0;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
            sengs = 0;
        }

        public override void AI() {
            if (Projectile.ai[0] == 0) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center + new Vector2(-1200, 0)
                    , new Vector2(53, 0), ModContent.ProjectileType<Mechanicalworm>(), Projectile.damage, 2, -1);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center + new Vector2(0, 1200)
                    , new Vector2(0, -53), ModContent.ProjectileType<Mechanicalworm>(), Projectile.damage, 2, -1);
            }
            if (Projectile.timeLeft <= 30) {
                modeings = 1200 * (Projectile.timeLeft / 30f);
            }
            else {
                if (modeings > 1200) {
                    modeings -= 15;
                }
            }
            foreach (var p in Main.player) {
                if (!p.active) {
                    continue;
                }
                if (p.Distance(Projectile.Center) > modeings) {
                    p.AddBuff(ModContent.BuffType<HellfireExplosion>(), 2);
                    p.HealEffect(-1);
                }
                
            }

            if (Main.LocalPlayer.Distance(Projectile.Center) > modeings && Projectile.timeLeft > 30) {
                if (sengs < 1f) {
                    sengs += 0.02f;
                }
            }
            else {
                if (sengs > 0f) {
                    sengs -= 0.02f;
                }
            }

            if (Projectile.ai[0] % 15 == 0) {
                Vector2 pos = Projectile.Center;
                float rand = Main.rand.NextFloat(MathHelper.TwoPi);
                for (int i = 0; i < 4; i++) {
                    float rot1 = MathHelper.PiOver2 * i + rand;
                    Vector2 vr = rot1.ToRotationVector2();
                    for (int j = 0; j < 33; j++) {
                        CWRParticle spark = new HeavenfallStarParticle(pos, vr * (0.1f + j * 0.34f), false, 13, Main.rand.NextFloat(5.2f, 6.3f), Color.Red);
                        CWRParticleHandler.AddParticle(spark);
                    }
                }
            }
            Projectile.ai[0]++;
        }

        public override void OnKill(int timeLeft) {
            NPC boss = Main.npc[(int)Projectile.ai[1]];
            if (boss.type == NPCID.SkeletronPrime && boss.active) {
                boss.Center = Projectile.Center;
                if (CalamityGlobalNPC.primeCannon != -1) {
                    if (Main.npc[CalamityGlobalNPC.primeCannon].active)
                        Main.npc[CalamityGlobalNPC.primeCannon].Center = Projectile.Center;
                }
                if (CalamityGlobalNPC.primeVice != -1) {
                    if (Main.npc[CalamityGlobalNPC.primeVice].active)
                        Main.npc[CalamityGlobalNPC.primeVice].Center = Projectile.Center;
                }
                if (CalamityGlobalNPC.primeSaw != -1) {
                    if (Main.npc[CalamityGlobalNPC.primeSaw].active)
                        Main.npc[CalamityGlobalNPC.primeSaw].Center = Projectile.Center;
                }
                if (CalamityGlobalNPC.primeLaser != -1) {
                    if (Main.npc[CalamityGlobalNPC.primeLaser].active)
                        Main.npc[CalamityGlobalNPC.primeLaser].Center = Projectile.Center;
                }
                for (int i = 0; i < 33; i++) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI()
                            , Projectile.Center, (MathHelper.TwoPi / 33f * i).ToRotationVector2() * 3
                            , ModContent.ProjectileType<PrimeCannonOnSpan>(), Projectile.damage, 0f
                            , Main.myPlayer, -1, -1, 0);
                }
            }
        }

        public override void PostDraw(Color lightColor) {
            Texture2D mainValue = TextureAssets.Npc[NPCID.Probe].Value;
            Main.instance.LoadNPC(NPCID.Probe);
            Vector2 orig = Projectile.Center - Main.screenPosition;
            for (int i = 0; i < 33; i++) {
                float rot = MathHelper.TwoPi / 33f * i + Projectile.ai[0] * 0.03f;
                Vector2 drawPos = orig + rot.ToRotationVector2() * modeings;
                Main.EntitySpriteDraw(mainValue, drawPos, null, Color.White
                , rot + MathHelper.Pi, mainValue.Size() / 2, 3, SpriteEffects.FlipHorizontally, 0);
            }

            var blackTile = TextureAssets.MagicPixel;
            var diagonalNoise = ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/GreyscaleGradients/HarshNoise");
            var maxOpacity = 1f;
            var shader = CWRMod.Instance.Assets.Request<Effect>(CWRConstant.noEffects + "PrimeHaloShader").Value;
            shader.Parameters["colorMult"].SetValue(11);
            shader.Parameters["time"].SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["radius"].SetValue(modeings + 50);
            shader.Parameters["anchorPoint"].SetValue(Projectile.Center);
            shader.Parameters["screenPosition"].SetValue(Main.screenPosition);
            shader.Parameters["screenSize"].SetValue(Main.ScreenSize.ToVector2());
            shader.Parameters["burnIntensity"].SetValue(1);
            shader.Parameters["playerPosition"].SetValue(Main.LocalPlayer.Center);
            shader.Parameters["maxOpacity"].SetValue(maxOpacity);
            shader.Parameters["isVmos"].SetValue(Projectile.timeLeft <= 30);
            shader.Parameters["projTime"].SetValue(Projectile.timeLeft);
            Main.spriteBatch.GraphicsDevice.Textures[1] = diagonalNoise.Value;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap
                , DepthStencilState.None, Main.Rasterizer, shader, Main.GameViewMatrix.TransformationMatrix);
            Rectangle rekt = new(Main.screenWidth / 2, Main.screenHeight / 2, Main.screenWidth, Main.screenHeight);
            //Rectangle rekt = new(Main.screenWidth / -2, Main.screenHeight / -2, Main.screenWidth * 2, Main.screenHeight * 2);
            Main.spriteBatch.Draw(blackTile.Value, rekt, null, default, 0f, blackTile.Value.Size() / 2, 0, 0f);
            //Main.spriteBatch.Draw(blackTile.Value, Vector2.Zero, new Rectangle(0, 0, 2000, 2000), default, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            Main.spriteBatch.ExitShaderRegion();

            Main.spriteBatch.Draw(CWRUtils.GetT2DValue(CWRConstant.Placeholder2)
                , new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.Red * sengs);
        }
    }
}
