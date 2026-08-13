using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles
{
    /// <summary>
    /// 御晶吊灯(投技触发体)：单盏放大金晶吊灯，物化→跟随玩家→白闪锁定→坠落。
    /// 自身无伤害，压中判定由投技状态在服务端执行；ai[0]=皇后whoAmI ai[1]=1表示被收编 ai[2]=色相种子。
    /// </summary>
    internal class QueenRoyalChandelierProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const int MaterializeTime = 30;
        private const int FollowTime = 44;
        private const int FlashTime = 14;
        private const float BodyRadius = 78f;
        private const float FollowSpeed = 7f;

        private ref float Timer => ref Projectile.localAI[0];
        private int QueenIndex => (int)Projectile.ai[0];
        private float HueSeed => Projectile.ai[2];

        /// <summary>0物化 1跟随 2锁闪 3坠落</summary>
        private int Stage {
            get {
                if (Timer <= MaterializeTime) {
                    return 0;
                }
                if (Timer <= MaterializeTime + FollowTime) {
                    return 1;
                }
                if (Timer <= MaterializeTime + FollowTime + FlashTime) {
                    return 2;
                }
                return 3;
            }
        }

        /// <summary>坠落段(投技状态据此做压中判定)</summary>
        internal bool IsFalling => Stage == 3;

        private float MaterializeP => MathHelper.Clamp(Timer / MaterializeTime, 0f, 1f);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 420;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 84;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 480;
        }

        public override void AI() {
            Timer++;

            //属主皇后失效即安静消散
            NPC queen = QueenIndex >= 0 && QueenIndex < Main.maxNPCs ? Main.npc[QueenIndex] : null;
            if (queen == null || !queen.active || queen.type != NPCID.QueenSlimeBoss) {
                Projectile.Kill();
                return;
            }

            switch (Stage) {
                case 0://物化
                    Projectile.velocity = Vector2.Zero;
                    if (Timer == 2) {
                        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.8f, Pitch = 0.05f }, Projectile.Center);
                    }
                    break;
                case 1: {//悬灯跟随目标横位
                    Player target = queen.target >= 0 && queen.target < Main.maxPlayers ? Main.player[queen.target] : null;
                    if (target.Alives()) {
                        float dx = target.Center.X - Projectile.Center.X;
                        Projectile.velocity = new Vector2(MathHelper.Clamp(dx * 0.06f, -FollowSpeed, FollowSpeed), 0f);
                    }
                    else {
                        Projectile.velocity *= 0.9f;
                    }
                    //服务端周期校正跟随轨迹
                    if (VaultUtils.isServer && (int)Timer % 12 == 0) {
                        Projectile.netUpdate = true;
                    }
                    break;
                }
                case 2://锁定预闪，位置承诺
                    Projectile.velocity = Vector2.Zero;
                    if (Timer == MaterializeTime + FollowTime + 1) {
                        SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.85f, Pitch = 0.8f }, Projectile.Center);
                    }
                    break;
                case 3://坠落
                    Projectile.velocity.X = 0f;
                    Projectile.velocity.Y += 1.05f;
                    if (Projectile.velocity.Y > 26f) {
                        Projectile.velocity.Y = 26f;
                    }
                    Projectile.tileCollide = true;
                    if (Timer == MaterializeTime + FollowTime + FlashTime + 1) {
                        SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);
                    }
                    if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                        Dust d = Dust.NewDustPerfect(Projectile.Top + Main.rand.NextVector2Circular(26f, 10f),
                            DustID.TintableDust, -Projectile.velocity * 0.1f, 120, QueenMotion.GetQueenDustColor(), 1.5f);
                        d.noGravity = true;
                    }
                    break;
            }

            Lighting.AddLight(Projectile.Center, QueenMotion.PrismHue(HueSeed).ToVector3() * (0.45f + 0.45f * MaterializeP));
        }

        public override bool OnTileCollide(Vector2 oldVelocity) => true;

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            if (Projectile.ai[1] == 1f) {
                //被收编成茧：向心收拢闪光，不播落地碎裂
                QueenMotion.ChargeGatherFX(Projectile.Center, 1f, 90f, HueSeed);
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.7f, Pitch = 0.6f }, Projectile.Center);
                return;
            }
            //落空：碎光收势，纯演出无弹幕(投技落空不该反而放弹)
            QueenMotion.CrystalShatterBurst(Projectile.Center, 1.5f, HueSeed);
            QueenMotion.Shake(Projectile.Center, 4f, 12, "QueenRoyalChandelier");
            SoundEngine.PlaySound(SoundID.Item167 with { Volume = 0.7f, Pitch = 0.2f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>吊灯晶簇本体(倒锥quad，金色大盏)</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            Effect effect = EffectLoader.QueenPrismCrystal?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            float half = BodyRadius * 2.1f;
            Vector2 c = Projectile.Center;
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture(new Vector3(c.X - half, c.Y - half, 0f), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture(new Vector3(c.X + half, c.Y - half, 0f), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture(new Vector3(c.X - half, c.Y + half, 0f), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture(new Vector3(c.X + half, c.Y + half, 0f), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            float charge = Stage switch {
                0 => 0f,
                1 => MathHelper.Clamp((Timer - MaterializeTime) / FollowTime, 0f, 1f) * 0.6f,
                2 => 0.6f + 0.4f * ((Timer - MaterializeTime - FollowTime) / FlashTime),
                _ => 1f,
            };

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uMode"]?.SetValue(2f);
            effect.Parameters["uGrow"]?.SetValue(MaterializeP);
            effect.Parameters["uShatter"]?.SetValue(0f);
            effect.Parameters["uCharge"]?.SetValue(charge);
            effect.Parameters["uHueSeed"]?.SetValue(HueSeed);
            effect.Parameters["seed"]?.SetValue(Projectile.whoAmI * 0.211f % 1f);
            //噪声显式绑 s1(shader 内 register(s1))
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>辉光/锁定预闪/落点标记环(真加色批，染色必须带A)</summary>
        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Texture2D ring = CWRAsset.DiffusionCircle.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color hue = QueenMotion.PrismHue(HueSeed);
            Color gold = QueenMotion.HolyGold;

            //常亮金辉底光
            spriteBatch.Draw(glow, drawPos, null, gold * (0.5f * MaterializeP), 0f, glow.Size() / 2f, 2f, SpriteEffects.None, 0f);

            if (Stage >= 1 && Stage <= 2) {
                //落点标记环：地面处呼吸环+竖直警示光柱
                Vector2 ground = QueenMotion.FindGroundBelow(Projectile.Center);
                Vector2 groundPos = ground - Main.screenPosition;
                float pulse = 0.65f + 0.35f * (float)Math.Sin(Timer * (Stage == 2 ? 0.9f : 0.28f));
                float ringStrength = Stage == 2 ? 0.95f : 0.5f;
                spriteBatch.Draw(ring, groundPos, null, hue * (ringStrength * pulse), 0f,
                    ring.Size() / 2f, new Vector2(1.15f, 0.42f), SpriteEffects.None, 0f);
                //光柱：吊灯到地面的竖直提示
                float columnLen = MathHelper.Clamp(ground.Y - Projectile.Center.Y, 0f, 1400f);
                if (columnLen > 8f) {
                    spriteBatch.Draw(glow, drawPos + new Vector2(0f, columnLen * 0.5f), null,
                        hue * (0.16f * pulse * ringStrength), 0f, glow.Size() / 2f,
                        new Vector2(0.5f, columnLen / glow.Height), SpriteEffects.None, 0f);
                }
            }

            if (Stage == 2) {
                //白热锁定快闪
                float flashP = (Timer - MaterializeTime - FollowTime) / FlashTime;
                float blink = 0.5f + 0.5f * (float)Math.Sin(flashP * MathHelper.Pi * 7f);
                spriteBatch.Draw(glow, drawPos, null, Color.White * (0.85f * blink), 0f, glow.Size() / 2f, 2.3f, SpriteEffects.None, 0f);
                spriteBatch.Draw(star, drawPos, null, Color.White * (0.9f * blink), flashP * 2.4f, star.Size() / 2f, 0.7f, SpriteEffects.None, 0f);
            }

            if (Stage == 3) {
                //坠落速度线
                float speed = Projectile.velocity.Length();
                float stretch = MathHelper.Clamp(speed * 0.055f, 0f, 1.6f);
                spriteBatch.Draw(glow, drawPos - Projectile.velocity * 0.8f, null, gold * 0.6f,
                    Projectile.velocity.ToRotation() - MathHelper.PiOver2, glow.Size() / 2f,
                    new Vector2(0.6f, 0.8f + stretch), SpriteEffects.None, 0f);
            }
        }
    }
}
