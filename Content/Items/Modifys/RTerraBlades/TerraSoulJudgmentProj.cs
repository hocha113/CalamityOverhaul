using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Modifys.RTerraBlades
{
    /// <summary>
    /// 真圣剑之魂：泰拉之光的光魂命中后，一柄光化的真圣剑在目标上空显形，凝聚数帧后自天而降贯穿目标<br/>
    /// 显形期零伤害只做预告（剑影渐显 + 一线细光落到目标），俯冲期一击贯穿，穿过后继续下坠散成光屑<br/>
    /// ai[0]=目标 NPC 序号 ai[1]=生成时目标中心 Y（俯冲终点参考）
    /// </summary>
    internal class TerraSoulJudgmentProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>占泰拉之光伤害比例</summary>
        internal const float DamageMul = 0.6f;
        private const int MaterializeFrames = 8;
        private const float DiveSpeed = 38f;
        private const float SpawnHeight = 280f;
        private const int MaxLife = 60;

        private int TargetIndex => (int)Projectile.ai[0];
        private float TargetY => Projectile.ai[1];
        private ref float Timer => ref Projectile.localAI[0];
        private bool Diving => Timer > MaterializeFrames;
        private float Materialize01 => MathHelper.Clamp(Timer / MaterializeFrames, 0f, 1f);

        /// <summary>目标正上方的显形点，贴天顶时压回世界内</summary>
        internal static Vector2 SpawnPointAbove(NPC target) {
            Vector2 p = target.Center - new Vector2(0f, SpawnHeight);
            p.Y = MathF.Max(p.Y, 48f);
            return p;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = MaxLife;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override bool? CanDamage() => Diving ? null : false;

        public override void AI() {
            Timer++;
            if (Timer <= MaterializeFrames) {
                //显形：剑尖朝下悬停，光屑向剑身汇聚
                Projectile.velocity = Vector2.Zero;
                Projectile.rotation = MathHelper.PiOver2 + MathHelper.PiOver4;
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    Vector2 offset = Main.rand.NextVector2CircularEdge(60f, 60f);
                    PRTLoader.NewParticle<PRT_Light>(Projectile.Center + offset, -offset * 0.12f
                        , Main.rand.NextBool(3) ? RTerraBlade.LightCream : RTerraBlade.LightGold
                        , Main.rand.NextFloat(0.12f, 0.2f))?.Configure(10, 0.9f);
                }
                Lighting.AddLight(Projectile.Center, RTerraBlade.LightGold.ToVector3() * (0.4f + 0.6f * Materialize01));
                return;
            }

            if (Timer == MaterializeFrames + 1) {
                //起跳：朝目标当前位置俯冲，目标已消失则垂直落下
                Vector2 dir = Vector2.UnitY;
                if (TargetIndex >= 0 && TargetIndex < Main.maxNPCs && Main.npc[TargetIndex].active) {
                    dir = (Main.npc[TargetIndex].Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                    //俯冲角不超过竖直 35 度，始终读作天降
                    float ang = MathHelper.Clamp(MathHelper.WrapAngle(dir.ToRotation() - MathHelper.PiOver2), -0.6f, 0.6f);
                    dir = (MathHelper.PiOver2 + ang).ToRotationVector2();
                }
                Projectile.velocity = dir * DiveSpeed;
                Projectile.netUpdate = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = 0.3f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.5f, Pitch = -0.1f }, Projectile.Center);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, RTerraBlade.LightCream, 0f)
                        ?.Configure(0.04f, 0.4f, 10);
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            Lighting.AddLight(Projectile.Center, RTerraBlade.LightGold.ToVector3() * 1.2f);

            //穿过目标高度后再坠一段即散
            if (Projectile.Center.Y > TargetY + 260f) {
                Projectile.Kill();
                return;
            }
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f)
                    , -Projectile.velocity * 0.05f, RTerraBlade.LightGold, Main.rand.NextFloat(0.12f, 0.18f))?.Configure(12, 0.9f);
            }
        }

        /// <summary>俯冲一帧跨 38px，用本帧扫过的线段判定，快落不漏</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Diving) {
                return false;
            }
            float point = 0f;
            Vector2 from = Projectile.Center - Projectile.velocity;
            Vector2 to = Projectile.Center + Projectile.velocity * 0.5f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), from, to, 34f, ref point);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //天降没有横向速度，击退按目标相对位置向外
            int dir = Math.Sign(target.Center.X - Projectile.Center.X);
            modifiers.HitDirectionOverride = dir == 0 ? 1 : dir;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = -0.15f }, target.Center);
            PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero, RTerraBlade.LightCream, 0f)
                ?.Configure(0.05f, 0.6f, 12);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center, Main.rand.NextVector2Circular(6f, 4f) - Vector2.UnitY * 3f
                    , Main.rand.NextBool(3) ? RTerraBlade.LightCream : RTerraBlade.LightGold
                    , Main.rand.NextFloat(0.8f, 1.4f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(14f, 30f)
                    , Main.rand.NextVector2Circular(1f, 1f) - Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.5f)
                    , RTerraBlade.LightGold, Main.rand.NextFloat(0.12f, 0.2f))?.Configure(Main.rand.Next(12, 20), 0.9f);
            }
        }

        /// <summary>光化真圣剑：显形期渐显 + 细光预告线，俯冲期拖光柱与三道速度残影</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TerraBladeFX.ItemTex(ItemID.TrueExcalibur);
            Texture2D shot = TerraBladeFX.LightShot;
            Texture2D star = TerraBladeFX.StarBlack;
            Vector2 origin = tex.Size() / 2f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float appear = Diving ? 1f : MathF.Pow(Materialize01, 0.7f);
            const float scale = 1.5f;

            if (!Diving) {
                //预告：一线细光自剑尖落到目标高度，越近起跳越亮
                if (shot != null) {
                    float len = MathF.Max(TargetY - Projectile.Center.Y, 40f);
                    Vector2 mid = pos + new Vector2(0f, len * 0.5f);
                    Color line = RTerraBlade.LightGold * (0.35f * appear);
                    line.A = 0;
                    Main.EntitySpriteDraw(shot, mid, null, line, MathHelper.PiOver2, shot.Size() / 2f
                        , new Vector2(len / shot.Width, 4f / shot.Height), SpriteEffects.None, 0);
                }
                if (star != null) {
                    Color sc = RTerraBlade.LightCream * (0.8f * appear);
                    sc.A = 0;
                    Main.EntitySpriteDraw(star, pos, null, sc, Timer * 0.15f, star.Size() / 2f, 0.12f + 0.12f * appear, SpriteEffects.None, 0);
                }
            }
            else if (shot != null) {
                //光柱：沿反速度向拉伸的金白光带，读作天降
                Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Vector2 mid = pos + back * 110f;
                Color pillar = RTerraBlade.LightGold * 0.55f;
                pillar.A = 0;
                Main.EntitySpriteDraw(shot, mid, null, pillar, Projectile.velocity.ToRotation(), shot.Size() / 2f
                    , new Vector2(240f / shot.Width, 26f / shot.Height), SpriteEffects.None, 0);
                Color core = RTerraBlade.LightCream * 0.7f;
                core.A = 0;
                Main.EntitySpriteDraw(shot, mid, null, core, Projectile.velocity.ToRotation(), shot.Size() / 2f
                    , new Vector2(200f / shot.Width, 9f / shot.Height), SpriteEffects.None, 0);
                for (int g = 3; g >= 1; g--) {
                    Color gc = RTerraBlade.LightGold * (0.4f - 0.1f * g);
                    gc.A = 0;
                    Main.EntitySpriteDraw(tex, pos + back * (16f * g), null, gc, Projectile.rotation, origin, scale, SpriteEffects.None, 0);
                }
            }

            //剑身：白金自发光 + 金色镀层
            Color body = Color.Lerp(Color.White, RTerraBlade.LightCream, 0.5f) * appear;
            Main.EntitySpriteDraw(tex, pos, null, body, Projectile.rotation, origin, scale * appear, SpriteEffects.None, 0);
            Color sheath = RTerraBlade.LightGold * (0.5f * appear);
            sheath.A = 0;
            Main.EntitySpriteDraw(tex, pos, null, sheath, Projectile.rotation, origin, scale * appear * 1.08f, SpriteEffects.None, 0);
            return false;
        }
    }
}
