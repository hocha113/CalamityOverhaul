using CalamityMod;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class AegisBladeGuardian : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "AegisBlade";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 13;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }
        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
        }
        public override bool? CanDamage() => Projectile.ai[1] != 2 ? false : base.CanDamage();
        public override void AI() {
            if (Projectile.ai[1] != 1) {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            }

            if (Projectile.ai[0] == 0) {
                Projectile.velocity = new Vector2(0, -12);
            }
            else if (Projectile.ai[1] == 0) {
                HandleStateZero();
            }
            else if (Projectile.ai[1] == 1) {
                HandleStateOne();
            }
            else if (Projectile.ai[1] == 2) {
                HandleStateTwo();
            }

            //全局计时器递增
            Projectile.ai[0]++;
        }

        //处理状态 0：加速、缩放、跟随玩家
        private void HandleStateZero() {
            Projectile.scale += 0.01f;
            Projectile.velocity *= 0.97f;
            Projectile.position += Main.player[Projectile.owner].velocity;

            if (Projectile.velocity.LengthSquared() < 9) {
                TransitionToState(1, resetVelocity: true);
            }
        }

        //处理状态 1：扩展规模、粒子效果、触发逻辑
        private void HandleStateOne() {
            if (Projectile.scale < 2.5f) {
                Projectile.scale += 0.02f;
                GenerateParticles(6, 133, 140, 17, Color.Gold, 16, 1, 1.5f);
            }
            else {
                GenerateParticles(6, 3, 14, 19, Color.DarkGoldenrod, Main.rand.Next(16, 18), 1, 1.5f);
            }

            if (Projectile.damage < Projectile.originalDamage * 45) {
                Projectile.damage += 35;
            }

            Projectile.velocity = Vector2.Zero;
            Projectile.rotation += 0.2f;
            Projectile.position += Main.player[Projectile.owner].velocity;

            if (Main.player[Projectile.owner].PressKey(false)) {
                Projectile.timeLeft = 300;
                if (Projectile.ai[0] > 55) Projectile.ai[0] = 55;
            }

            if (Projectile.ai[0] > 60 || !Main.player[Projectile.owner].PressKey(false)) {
                TransitionToState(2, resetVelocity: false);
            }
        }

        //处理状态 2：追踪目标或自爆
        private void HandleStateTwo() {
            NPC npc = Projectile.Center.FindClosestNPC(6000, true, true);

            if (npc != null) {
                Projectile.ChasingBehavior(npc.Center, 56);
                Projectile.penetrate = 1;
            }
            else {
                Projectile.Kill();
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
        }

        //生成粒子效果
        private void GenerateParticles(int count, int minRadius, int maxRadius, float speed, Color color, int size, float alpha, float hueShift) {
            if (!VaultUtils.isServer) {
                for (int i = 0; i < count; i++) {
                    Vector2 randVector = Main.rand.NextVector2Unit();
                    Vector2 pos = Projectile.Center + randVector * Main.rand.Next(minRadius, maxRadius) * Projectile.scale;
                    Vector2 velocity = randVector * speed;

                    BasePRT particle = new PRT_Light(pos, velocity, Main.rand.NextFloat(0.1f, 0.6f), color, size, 1, alpha, hueShift, _entity: Projectile);
                    PRTLoader.AddParticle(particle);
                }
            }
        }

        //状态转换逻辑
        private void TransitionToState(int newState, bool resetVelocity) {
            Projectile.ai[1] = newState;
            if (resetVelocity) {
                Projectile.ai[0] = 1;
                Projectile.velocity = Vector2.Zero;
            }
            Projectile.netUpdate = true;
        }

        public override void OnKill(int timeLeft) {
            Projectile.damage = Projectile.originalDamage;
            Projectile.Explode(1200);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 156; i++) {
                    Vector2 pos = Projectile.Center;
                    Vector2 particleSpeed = Main.rand.NextVector2Unit() * Main.rand.Next(13, 34);
                    BasePRT energyLeak = new PRT_Light(pos, particleSpeed
                        , Main.rand.NextFloat(0.5f, 1.3f), Color.Gold, 30, 1, 1.5f, hueShift: 0.0f);
                    PRTLoader.AddParticle(energyLeak);
                }
            }
            if (Projectile.IsOwnedByLocalPlayer() && Projectile.scale > 1.6f) {
                for (int i = 0; i < 12; i++) {
                    Vector2 velocity = CalamityUtils.RandomVelocity(100f, 70f, 100f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + velocity.UnitVector() * 13
                        , velocity, ModContent.ProjectileType<AegisFlame>(), (int)(Projectile.damage * 0.5), 0f, Projectile.owner, 0f, 0f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.White
                , Projectile.rotation, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            if (Projectile.ai[1] == 2) {
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    Main.EntitySpriteDraw(value, Projectile.oldPos[i] - Main.screenPosition + Projectile.Size / 2, null, Color.White * (1 - i * 0.1f)
                    , Projectile.rotation, value.Size() / 2, Projectile.scale - i * 0.1f, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
