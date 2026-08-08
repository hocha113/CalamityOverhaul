using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>
    /// 雨樋「落雨」：樱流巡航时沿途甩下的墨滴。<br/>
    /// 墨滴不追踪，只往下坠——所以航线本身就是攻击范围，飞哪儿淋哪儿。<br/>
    /// 落地摊成一小片墨洼，踩进去的敌手被滞缚一段。<br/>
    /// ai[0]=坠落初速的横向分量
    /// </summary>
    internal class OniMeiInkRain : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int FallLife = 150;
        /// <summary>摊开成洼后的存续帧数</summary>
        private const int PuddleLife = 90;
        private const float PuddleHalfWidth = 26f;
        private const float PuddleHeight = 10f;

        private static readonly Color InkWet = new(58, 14, 22);
        private static readonly Color InkDark = new(22, 9, 13);

        private bool puddle;
        private int puddleTimer;
        private float trail;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.DamageType = CWRRef.GetTrueMeleeNoSpeedDamageClass();
            Projectile.timeLeft = FallLife;
            Projectile.aiStyle = -1;
        }

        /// <summary>owner 端甩一滴；初速带一点航向惯性，读作"是从刀上甩下去的"</summary>
        internal static void Drip(Player player, Vector2 at, Vector2 flightVelocity, int damage,
            IEntitySource source = null) {
            if (player == null || Main.myPlayer != player.whoAmI) {
                return;
            }
            Vector2 velocity = new(flightVelocity.X * 0.18f, Math.Max(2f, flightVelocity.Y * 0.15f) + 2.5f);
            Projectile.NewProjectile(source ?? player.GetSource_Misc("CWR_OniMeiInkRain"),
                at, velocity, ModContent.ProjectileType<OniMeiInkRain>(),
                Math.Max(1, damage), 0f, player.whoAmI, ai0: velocity.X);
        }

        public override void AI() {
            if (puddle) {
                Projectile.velocity = Vector2.Zero;
                if (++puddleTimer >= PuddleLife) {
                    Projectile.Kill();
                }
                return;
            }

            //坠落：横向阻尼、纵向加速，越坠越快越细长
            Projectile.velocity.X *= 0.985f;
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.42f, 17f);
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;

            if (Main.dedServ) {
                return;
            }
            trail += Projectile.velocity.Length();
            while (trail >= 26f) {
                trail -= 26f;
                PRTLoader.NewParticle<PRT_OniInkDrop>(Projectile.Center,
                    -Projectile.velocity * 0.06f, InkDark, Main.rand.NextFloat(0.08f, 0.14f))
                    ?.Configure(Main.rand.Next(10, 16));
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            BecomePuddle();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Drip with { Pitch = -0.3f, Volume = 0.35f }, target.Center);
            }
            BecomePuddle();
        }

        /// <summary>摊成墨洼：判定箱压扁贴地，此后只滞不砍</summary>
        private void BecomePuddle() {
            if (puddle) {
                return;
            }
            puddle = true;
            puddleTimer = 0;
            Vector2 foot = Projectile.Bottom;
            Projectile.width = (int)(PuddleHalfWidth * 2f);
            Projectile.height = (int)PuddleHeight;
            Projectile.Bottom = foot;
            Projectile.tileCollide = false;
            Projectile.timeLeft = PuddleLife + 2;
            Projectile.netUpdate = true;

            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Drip with { Pitch = 0.15f, Volume = 0.28f }, foot);
                //溅开：贴地横向甩出，不做成向上的喷泉
                for (int i = 0; i < 5; i++) {
                    float side = Main.rand.NextBool() ? 1f : -1f;
                    PRTLoader.NewParticle<PRT_OniInkDrop>(foot,
                        new Vector2(side * Main.rand.NextFloat(1.2f, 3.4f), -Main.rand.NextFloat(0.4f, 1.6f)),
                        InkWet, Main.rand.NextFloat(0.10f, 0.20f))
                        ?.Configure(Main.rand.Next(14, 22));
                }
            }
        }

        /// <summary>洼只滞不砍：伤害只在坠落那一下结算</summary>
        public override bool? CanDamage() => puddle ? false : null;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = target.Center.X >= Projectile.Center.X ? 1 : -1;

        public override void OnKill(int timeLeft) {
            if (!Main.dedServ && puddle) {
                PRTLoader.NewParticle<PRT_OniInkDrop>(Projectile.Bottom, Vector2.Zero,
                    InkDark, 0.10f)?.Configure(12);
            }
        }

        /// <summary>洼在世：踩进来的敌手被墨黏住</summary>
        public override void PostAI() {
            if (!puddle || Main.dedServ && Main.netMode != Terraria.ID.NetmodeID.Server) {
                return;
            }
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Rectangle box = Projectile.Hitbox;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.friendly || !npc.CanBeChasedBy() || !npc.Hitbox.Intersects(box)) {
                    continue;
                }
                NPC root = OniMeiCombat.ResolveEffectRoot(npc);
                root?.AddBuff(ModContent.BuffType<OniBindDebuff>(), OniMeiCombat.InkRainPuddleBindTicks);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ) {
                return false;
            }
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 half = new(0.5f);

            if (puddle) {
                float t = 1f - puddleTimer / (float)PuddleLife;
                //洼：贴地压扁的一摊，边缘比心浅
                Vector2 at = Projectile.Bottom - Main.screenPosition - Vector2.UnitY * 2f;
                float grow = MathHelper.Clamp(puddleTimer / 6f, 0.35f, 1f);
                Main.EntitySpriteDraw(pixel, at, src, InkDark * (t * 0.75f), 0f, half,
                    new Vector2(PuddleHalfWidth * 2f * grow, PuddleHeight * 0.9f), SpriteEffects.None);
                Main.EntitySpriteDraw(pixel, at, src, InkWet * (t * 0.55f), 0f, half,
                    new Vector2(PuddleHalfWidth * 1.3f * grow, PuddleHeight * 0.55f), SpriteEffects.None);
                return false;
            }

            //坠落：沿速度拉长的一枚墨条，越快越长
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() / 8f, 1f, 3.2f);
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(pixel, pos, src, InkDark * 0.9f,
                Projectile.velocity.ToRotation() + MathHelper.PiOver2, half,
                new Vector2(4.5f, 11f * stretch), SpriteEffects.None);
            Main.EntitySpriteDraw(pixel, pos, src, InkWet * 0.6f,
                Projectile.velocity.ToRotation() + MathHelper.PiOver2, half,
                new Vector2(2.2f, 8f * stretch), SpriteEffects.None);
            return false;
        }
    }
}
