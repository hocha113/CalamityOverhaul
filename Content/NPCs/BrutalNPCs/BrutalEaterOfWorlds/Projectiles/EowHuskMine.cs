using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Projectiles
{
    /// <summary>蜕落的节段自爆壳；ai[0]=引信帧数 ai[1]=链序(视觉相位)；引信尽头酸爆</summary>
    internal class EowHuskMine : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>爆炸判定持续帧</summary>
        private const int BlastWindow = 7;
        /// <summary>爆炸判定半径(直径Resize用)</summary>
        private const int BlastSize = 190;

        private int Fuse => (int)Projectile.ai[0];
        private float OrderSeed => Projectile.ai[1];
        /// <summary>已存活帧</summary>
        private int Age => (int)Projectile.localAI[0];
        private bool Detonated => Projectile.localAI[1] >= 1f;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 420;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Projectile.localAI[0]++;

            //蜕落缓冲：继承体节甩出的初速，快速衰减悬停
            if (!Detonated) {
                Projectile.velocity *= 0.9f;
                //悬停微沉浮
                Projectile.velocity.Y += (float)Math.Sin((Age + OrderSeed * 13f) * 0.07f) * 0.012f;
            }
            else {
                Projectile.velocity = Vector2.Zero;
            }

            //引信到点起爆(各端本地按同步ai推进，帧差由生成包对齐)
            if (!Detonated && Age >= Fuse) {
                Detonate();
            }

            //爆窗结束回收
            if (Detonated && Age >= Fuse + BlastWindow) {
                Projectile.Kill();
                return;
            }

            //引信加速脉冲光
            float fuseT = MathHelper.Clamp(Age / (float)Math.Max(Fuse, 1), 0f, 1f);
            Lighting.AddLight(Projectile.Center, EowMotionFX.AcidGreen.ToVector3() * (0.25f + fuseT * 0.55f));

            //临爆滴酸
            if (!VaultUtils.isServer && !Detonated && fuseT > 0.5f && Main.rand.NextBool(9)) {
                PRTLoader.NewParticle<PRT_AcidSplash>(
                    Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    new Vector2(0f, Main.rand.NextFloat(0.5f, 1.5f)), Color.White,
                    Main.rand.NextFloat(0.3f, 0.5f)).Configure(Main.rand.Next(16, 26));
            }
        }

        /// <summary>起爆：判定窗开启+演出</summary>
        private void Detonate() {
            Projectile.localAI[1] = 1f;
            Projectile.hostile = true;
            Vector2 center = Projectile.Center;
            Projectile.Resize(BlastSize, BlastSize);
            Projectile.Center = center;

            if (VaultUtils.isServer) {
                return;
            }
            //酸爆环
            PRTLoader.NewParticle<PRT_StarPulseRing>(center, Vector2.Zero,
                EowMotionFX.AcidGreen, 0.1f).Configure(0.1f, 1.05f, 22);
            EowMotionFX.SpawnAcidBurst(center, 1.5f);
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 9f);
                PRTLoader.NewParticle<PRT_AcidSplash>(center, vel, Color.White,
                    Main.rand.NextFloat(0.5f, 0.95f)).Configure(Main.rand.Next(20, 36));
            }
            PRTLoader.NewParticle<PRT_ToxicMist>(center, Main.rand.NextVector2Circular(1f, 1f),
                Color.White, Main.rand.NextFloat(1.0f, 1.4f)).Configure(Main.rand.Next(40, 62), 0.65f);

            EowMotionFX.CameraPunch(center, 3.2f, 10, "EowHuskMine");
            SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.9f, Pitch = -0.15f, MaxInstances = 6 }, center);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.42f, Pitch = 0.5f, MaxInstances = 6 }, center);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Detonated) {
                return false;
            }

            //蜕壳本体：借体节贴图画一节死壳
            Main.instance.LoadNPC(NPCID.EaterofWorldsBody);
            Texture2D tex = TextureAssets.Npc[NPCID.EaterofWorldsBody].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle frame = tex.Bounds;
            Vector2 origin = frame.Size() / 2f;

            float fuseT = MathHelper.Clamp(Age / (float)Math.Max(Fuse, 1), 0f, 1f);
            //脉冲频率随引信加速：手调延迟曲线
            float pulseRate = MathHelper.Lerp(4.5f, 16f, fuseT * fuseT);
            float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * pulseRate + OrderSeed * 1.7f);

            //死壳(灰紫)
            Color husk = Color.Lerp(EowMotionFX.FleshShadow, Color.Gray, 0.4f);
            Main.EntitySpriteDraw(tex, drawPos, frame, husk * 0.95f, Projectile.rotation,
                origin, Projectile.scale, SpriteEffects.None, 0);

            //内酸涨光(加法)
            Color glow = EowMotionFX.AcidGreen with { A = 0 };
            Main.EntitySpriteDraw(tex, drawPos, frame, glow * (0.25f + 0.6f * pulse * fuseT), Projectile.rotation,
                origin, Projectile.scale * (1f + 0.06f * pulse), SpriteEffects.None, 0);

            //底光
            Texture2D soft = CWRAsset.SoftGlow.Value;
            Main.EntitySpriteDraw(soft, drawPos, null, glow * (0.2f + 0.25f * pulse * fuseT), 0f,
                soft.Size() / 2f, 0.5f + fuseT * 0.3f, SpriteEffects.None, 0);

            return false;
        }
    }
}
