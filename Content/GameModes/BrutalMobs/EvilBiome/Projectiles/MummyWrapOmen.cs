using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.EvilBiome.Projectiles
{
    /// <summary>
    /// 裹布缠掷预告:木乃伊抬臂期间的直线标记,掷向出手即锁定(预告即承诺,不再重瞄),
    /// 标记线长=裹布实际射程,所见即所掷;提交帧由本体自持掷出 <see cref="MummyWrapProj"/>。
    /// 来源校验与取消语义镜像 WastesSandConeTelegraph:预告期施法者死亡/槽位复用即取消发射,
    /// 击杀施法者是有效反制。
    /// ai[0]=锁定掷角 ai[1]=出生档位 ai[2]=来源打包(槽位+1|类型&lt;&lt;8);damage 携带裹布伤害(本体永不敌对)
    /// </summary>
    internal class MummyWrapOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //====== 具名数值块 ======
        /// <summary>预告帧数(契约 ≥30,档位一律不缩短)</summary>
        public const int TelegraphFrames = 34;
        private const int FadeFrames = 8;
        /// <summary>标记刻段数</summary>
        private const int MarkSegments = 9;

        private float LockAngle => Projectile.ai[0];
        private int Tier => Math.Clamp((int)Projectile.ai[1], 1, 3);
        private int SrcPacked => (int)Projectile.ai[2];
        private int Flavor => EvilBiomeMobsNPC.SiphonFlavor(SrcPacked >> 8);
        private int TotalLife => TelegraphFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体,永不造成伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;

            //来源检查(镜像沙锥):施法者死亡则取消提交(玩家击杀=有效反制);
            //类型比对防槽位复用,各端读同步的 npc.active,结论一致
            if (!Cancelled && elapsed < TelegraphFrames) {
                int src = (SrcPacked & 255) - 1;
                if (src < 0 || src >= Main.maxNPCs || !Main.npc[src].active
                    || Main.npc[src].type != SrcPacked >> 8) {
                    Cancelled = true;
                }
            }

            //抬臂凝布尘:手位上方汇聚(客户端,≤2 粒/帧)
            if (!Cancelled && elapsed < TelegraphFrames && !VaultUtils.isServer && Main.rand.NextBool(2)) {
                Vector2 hand = Projectile.Center + new Vector2(0f, -18f);
                Dust dust = Dust.NewDustPerfect(hand + Main.rand.NextVector2Circular(10f, 10f),
                    EvilBiomeFX.DustFor(Flavor), new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.2f)), 130, default, 0.9f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, EvilBiomeFX.Bright(Flavor).ToVector3() * 0.2f);

            if (elapsed == TelegraphFrames && !Cancelled) {
                if (!VaultUtils.isClient) {
                    Emit();
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.6f, Pitch = -0.2f, MaxInstances = 4 }, Projectile.Center);
                }
            }
        }

        /// <summary>提交帧掷出裹布卷:沿锁定角直线,速度按档位(只调强度)</summary>
        private void Emit() {
            Vector2 vel = LockAngle.ToRotationVector2() * MummyWrapProj.SpeedFor(Tier);
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                ModContent.ProjectileType<MummyWrapProj>(), Projectile.damage, 0f, Main.myPlayer,
                Flavor, Tier);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float fade;
            if (Cancelled) {
                fade = 0.35f * MathHelper.Clamp(1f - elapsed / (float)TelegraphFrames, 0f, 1f);
            }
            else if (elapsed >= TelegraphFrames) {
                fade = MathHelper.Clamp(1f - (elapsed - TelegraphFrames) / (float)FadeFrames, 0f, 1f);
            }
            else {
                fade = MathHelper.Clamp(elapsed / 8f, 0f, 1f);
            }
            if (fade <= 0.01f) {
                return false;
            }

            Texture2D tex = CWRAsset.Extra_98.Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 dir = LockAngle.ToRotationVector2();
            float progress = MathHelper.Clamp(elapsed / (float)TelegraphFrames, 0f, 1f);
            float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 16f + Projectile.identity);
            float range = MummyWrapProj.SpeedFor(Tier) * MummyWrapProj.FlightFrames;

            //直线标记:沿掷向排布渐亮刻段,线长即裹布射程(所见即所掷)
            for (int i = 0; i < MarkSegments; i++) {
                float t = (i + 1) / (float)MarkSegments;
                Vector2 pos = Projectile.Center + dir * (range * t) - Main.screenPosition;
                Color mark = MummyWrapProj.LinenBright with { A = 0 }
                    * ((0.16f + 0.4f * progress) * fade * pulse * (1f - t * 0.45f));
                Main.EntitySpriteDraw(tex, pos, null, mark, LockAngle + MathHelper.PiOver2,
                    origin, new Vector2(0.09f, 0.3f), SpriteEffects.None, 0);
            }

            //手中成形的裹布卷:暗层+亮芯+风味点睛,随进度胀起旋紧
            Vector2 roll = Projectile.Center + new Vector2(0f, -14f * progress) - Main.screenPosition;
            float rollScale = (0.14f + 0.2f * progress) * pulse;
            float spin = elapsed * 0.25f;
            Main.EntitySpriteDraw(tex, roll, null, MummyWrapProj.LinenDeep * (0.85f * fade * progress),
                spin, origin, rollScale * 1.18f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, roll, null, MummyWrapProj.LinenBright with { A = 0 } * (0.7f * fade * progress),
                spin, origin, rollScale * 0.7f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, roll, null, EvilBiomeFX.Bright(Flavor) with { A = 0 } * (0.35f * fade * progress),
                spin, origin, rollScale * 0.4f, SpriteEffects.None, 0);
            return false;
        }
    }
}
