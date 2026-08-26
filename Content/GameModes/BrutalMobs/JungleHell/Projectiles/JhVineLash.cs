using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.JungleHell.Projectiles
{
    /// <summary>
    /// 缠棘藤鞭：食人花族的预告延伸鞭打。基点/方向/长度在生成瞬间全部锁定（预告即承诺），
    /// 预告期沿承诺线画虚影并在落点亮标记，之后藤鞭快速伸出；判定窗口=伸出可见窗口。<br/>
    /// ai[0]=档位*4+毒素模式(0中毒/1毒液) ai[1]=鞭长 ai[2]=母株NPC索引（仅视觉根部跟随）
    /// </summary>
    internal class JhVineLash : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        /// <summary>预告帧数（≥30 公平契约）</summary>
        private const int TelegraphFrames = 36;
        private const int ExtendFrames = 10;
        private const int HoldFrames = 8;
        private const int RetractFrames = 12;
        private const int TotalLife = TelegraphFrames + ExtendFrames + HoldFrames + RetractFrames;
        /// <summary>鞭身判定半宽（=可见藤蔓半宽，逃生阀门：横向跨出即安全）</summary>
        private const float WhipHalfWidth = 13f;
        private const int PoisonTicksBase = 180;
        private const int PoisonTicksPerTier = 60;
        private const int VenomTicksBase = 60;
        private const int VenomTicksPerTier = 30;

        private int Tier => Math.Max(1, (int)Projectile.ai[0] / 4);
        /// <summary>0=中毒（食人花/双足草魔） 1=毒液（愤怒捕手）</summary>
        private int BuffMode => (int)Projectile.ai[0] % 4;
        private float Reach => Math.Max(Projectile.ai[1], 60f);
        private int AnchorNpc => (int)Projectile.ai[2];
        private int Elapsed => TotalLife - Projectile.timeLeft;

        /// <summary>鞭梢伸出进度 0~1（快出）</summary>
        private float TipProgress {
            get {
                int t = Elapsed - TelegraphFrames;
                if (t <= 0) {
                    return 0f;
                }
                if (t >= ExtendFrames) {
                    return 1f;
                }
                float x = t / (float)ExtendFrames;
                return 1f - (float)Math.Pow(1f - x, 4);
            }
        }

        /// <summary>收回系数 1→0</summary>
        private float RetractFactor {
            get {
                int t = Elapsed - TelegraphFrames - ExtendFrames - HoldFrames;
                if (t <= 0) {
                    return 1f;
                }
                return MathHelper.Clamp(1f - t / (float)RetractFrames, 0f, 1f);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 520;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TotalLife;
                Projectile.rotation = Projectile.velocity.ToRotation();
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.7f, Pitch = -0.3f, MaxInstances = 4 }, Projectile.Center);
                }
            }

            int elapsed = Elapsed;
            //判定窗口=伸出+持鞭的可见窗口
            Projectile.hostile = elapsed >= TelegraphFrames && elapsed < TelegraphFrames + ExtendFrames + HoldFrames;

            Vector2 dir = Projectile.rotation.ToRotationVector2();

            //出鞭瞬间：破空声+沿线叶屑
            if (elapsed == TelegraphFrames && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.75f, Pitch = -0.4f, MaxInstances = 4 }, Projectile.Center);
                for (int i = 0; i < 6; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(0f, Reach),
                        DustID.JungleGrass, dir.RotatedByRandom(0.6f) * Main.rand.NextFloat(1f, 3f), 90, default, Main.rand.NextFloat(0.9f, 1.4f));
                    dust.noGravity = true;
                }
            }

            //预告期：承诺线上稀疏渗尘（≤2/帧）
            if (elapsed < TelegraphFrames && !Main.dedServ && Main.rand.NextBool(2)) {
                Dust seep = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(0f, Reach),
                    DustID.JungleGrass, Vector2.Zero, 160, default, 0.8f);
                seep.noGravity = true;
                seep.velocity = dir * 0.4f;
            }

            Lighting.AddLight(Projectile.Center + dir * Reach * TipProgress, 0.08f, 0.16f, 0.04f);
        }

        /// <summary>沿锁定鞭线连续判定（线段 vs 玩家盒），判定长度与可见伸出严格一致</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            float visible = TipProgress * RetractFactor;
            if (visible < 0.15f) {
                return false;
            }
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            float hitPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + dir * Reach * visible, WhipHalfWidth * 2f, ref hitPoint);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (BuffMode == 1) {
                target.AddBuff(BuffID.Venom, VenomTicksBase + VenomTicksPerTier * Tier);
            }
            else {
                target.AddBuff(BuffID.Poisoned, PoisonTicksBase + PoisonTicksPerTier * Tier);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(0f, Reach * 0.5f),
                    DustID.JungleGrass, Main.rand.NextVector2Circular(1.5f, 1.5f), 120, default, 0.9f);
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D chain = TextureAssets.Chain26.Value;
            Texture2D petal = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;

            int elapsed = Elapsed;
            bool telegraph = elapsed < TelegraphFrames;
            float pulse = 0.65f + 0.35f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f + Projectile.identity * 0.8f);
            float fadeIn = MathHelper.Clamp(elapsed / 8f, 0f, 1f);

            Vector2 dir = Projectile.rotation.ToRotationVector2();
            //视觉根部跟随母株（仅视觉；判定线锁定在生成基点）
            Vector2 root = AnchorNpc.TryGetNPC(out NPC anchor) && anchor.Alives() ? anchor.Center : Projectile.Center;
            Vector2 lockedTip = Projectile.Center + dir * Reach;

            if (telegraph) {
                //承诺线虚影：整条待击线+落点标记，看见的线就是要挨打的线
                DrawChainLine(chain, root, lockedTip, new Color(120, 190, 80, 90) * (0.4f * fadeIn * pulse), 0.8f);
                Main.EntitySpriteDraw(glow, lockedTip - Main.screenPosition, null,
                    new Color(150, 230, 90, 0) * (0.55f * fadeIn * pulse), 0f, glow.Size() / 2f,
                    0.5f + 0.15f * pulse, SpriteEffects.None, 0);
                return false;
            }

            float visible = TipProgress * RetractFactor;
            if (visible <= 0.02f) {
                return false;
            }
            Vector2 tip = Projectile.Center + dir * Reach * visible;

            //鞭身：原版藤链贴图铺设
            DrawChainLine(chain, root, tip, Color.White, 1f);

            //梢头爪叶：三瓣张开扣咬
            float openAngle = Projectile.hostile ? 0.5f : 0.25f;
            Color leaf = new Color(110, 180, 70) * MathHelper.Clamp(visible * 1.5f, 0f, 1f);
            for (int i = -1; i <= 1; i++) {
                Main.EntitySpriteDraw(petal, tip - Main.screenPosition, null, leaf,
                    Projectile.rotation + i * openAngle + MathHelper.PiOver2,
                    new Vector2(petal.Width / 2f, petal.Height * 0.9f),
                    new Vector2(0.12f, 0.26f), SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(glow, tip - Main.screenPosition, null,
                new Color(160, 255, 100, 0) * (0.5f * visible), 0f, glow.Size() / 2f, 0.32f, SpriteEffects.None, 0);
            return false;
        }

        /// <summary>沿两点铺原版链贴图（直线鞭身，光照取样）</summary>
        private static void DrawChainLine(Texture2D chain, Vector2 start, Vector2 end, Color tint, float scale) {
            float dist = Vector2.Distance(start, end);
            if (dist < 8f) {
                return;
            }
            Vector2 dir = (end - start) / dist;
            float rot = dir.ToRotation() - MathHelper.PiOver2;
            int links = Math.Max((int)(dist / 16f), 2);
            for (int i = 0; i < links; i++) {
                Vector2 pos = start + dir * i * 16f;
                Color light = Lighting.GetColor((int)(pos.X / 16f), (int)(pos.Y / 16f));
                Color color = new Color(light.R * tint.R / 255, light.G * tint.G / 255, light.B * tint.B / 255, tint.A);
                Main.spriteBatch.Draw(chain, pos - Main.screenPosition, null, color, rot,
                    new Vector2(chain.Width * 0.5f, 0f), scale, SpriteEffects.None, 0f);
            }
        }
    }
}
