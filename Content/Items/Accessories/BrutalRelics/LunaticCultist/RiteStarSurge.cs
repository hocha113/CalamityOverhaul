using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.LunaticCultist
{
    /// <summary>
    /// 星辰仪式的星核：头顶结晶天体成形（CultistPlanet TechStardust），
    /// 行星环坍缩前摇后崩解，唤落 16 颗坠星；Projectile.damage 即坠星伤害
    /// </summary>
    internal class RiteStarCore : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Timer => ref Projectile.localAI[0];

        private const int FormFrames = 16;
        private const int CommitFrame = 48;
        private const int LifeFrames = 58;
        /// <summary>星核可见半径(px)</summary>
        private const float VisRadius = 64f;
        /// <summary>坠星总数</summary>
        private const int ShardCount = 16;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames + 4;
            Projectile.netImportant = true;
        }

        /// <summary>坍缩系数：成形后自 1 收到 0.62，定形拍后骤缩归零（坍缩读得出的前摇）</summary>
        private float CollapseScale {
            get {
                if (Timer <= FormFrames) {
                    return 1f;
                }
                if (Timer <= CommitFrame) {
                    float t = (Timer - FormFrames) / (CommitFrame - FormFrames);
                    return MathHelper.Lerp(1f, 0.62f, t * t);
                }
                return MathHelper.Lerp(0.62f, 0.05f, MathHelper.Clamp((Timer - CommitFrame) / (LifeFrames - CommitFrame), 0f, 1f));
            }
        }

        private float FormIn => MathHelper.Clamp(Timer / FormFrames, 0f, 1f);

        public override void AI() {
            Timer++;
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            Vector2 anchor = owner.Center + new Vector2(0f, -168f);
            Projectile.Center = Vector2.Lerp(Projectile.Center, anchor, 0.1f);
            Projectile.velocity = Vector2.Zero;

            bool onScreen = CultistMotion.OnScreen(Projectile.Center, 360f);
            if (!VaultUtils.isServer && onScreen) {
                //仪式帷幕随坍缩收拢
                float veil = 0.22f + 0.14f * (1f - CollapseScale);
                CultistScreenFX.SetVeil(veil, Projectile.Center, CultistMotion.StardustCore, 480f);
            }

            if ((int)Timer == 4 && !VaultUtils.isServer && onScreen) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = -0.1f }, Projectile.Center);
            }

            //坍缩期：向心晶尘+爬音（要来了）
            if (Timer > FormFrames && Timer < CommitFrame && !VaultUtils.isServer && onScreen) {
                if (Main.rand.NextBool(2)) {
                    Vector2 offset = Main.rand.NextVector2Unit() * Main.rand.NextFloat(90f, 150f);
                    InnoVault.PRT.PRTLoader.NewParticle<PRT_CultistFrostMote>(Projectile.Center + offset,
                        -offset * 0.055f,
                        Color.Lerp(CultistMotion.StardustCore, CultistMotion.StardustEdge, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.6f, 1.0f))?.Configure(Main.rand.Next(16, 26));
                }
                if ((int)(Timer - FormFrames) % 8 == 0) {
                    float riser = (Timer - FormFrames) / (CommitFrame - FormFrames);
                    SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.3f, Pitch = -0.4f + riser * 0.9f },
                        Projectile.Center);
                }
            }

            //定形拍：星核崩解，坠星唤落（owner 端出弹）
            if ((int)Timer == CommitFrame) {
                if (!VaultUtils.isServer && onScreen) {
                    SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.9f, Pitch = -0.2f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.6f, Pitch = 0.2f }, Projectile.Center);
                    CultistScreenFX.PushFlash(0.16f);
                    CultistMotion.SigilCommitFX(Projectile.Center, CultistMotion.StardustCore, 1.2f);
                    CultistMotion.RuneBurst(Projectile.Center, CultistMotion.StardustCore, 14, 8f);
                    CultistMotion.Shake(Projectile.Center, 4f, 10);
                }
                if (Projectile.owner == Main.myPlayer) {
                    SummonShards(owner);
                }
            }

            Lighting.AddLight(Projectile.Center, CultistMotion.StardustCore.ToVector3() * 0.7f * FormIn);
        }

        /// <summary>owner 端：按敌人分布唤落坠星，无敌则在头顶扇面铺开</summary>
        private void SummonShards(Player owner) {
            List<NPC> targets = new(3);
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || owner.Distance(npc.Center) > 1400f) {
                    continue;
                }
                targets.Add(npc);
                if (targets.Count >= 3) {
                    break;
                }
            }

            for (int i = 0; i < ShardCount; i++) {
                Vector2 spawnPos;
                float fallAngle;
                if (targets.Count > 0) {
                    NPC target = targets[i % targets.Count];
                    spawnPos = target.Center + new Vector2(Main.rand.NextFloat(-110f, 110f),
                        -560f - Main.rand.NextFloat(120f));
                    //带一点目标速度预判
                    Vector2 lead = target.Center + target.velocity * 12f;
                    fallAngle = (lead - spawnPos).ToRotation() + Main.rand.NextFloat(-0.05f, 0.05f);
                }
                else {
                    spawnPos = owner.Center + new Vector2((i / (float)(ShardCount - 1) - 0.5f) * 520f,
                        -560f - Main.rand.NextFloat(120f));
                    fallAngle = MathHelper.PiOver2 + Main.rand.NextFloat(-0.12f, 0.12f);
                }
                if (spawnPos.Y < 640f) {
                    spawnPos.Y = 640f;
                }
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), spawnPos, Vector2.Zero,
                    ModContent.ProjectileType<RiteStarShard>(), Projectile.damage, Projectile.knockBack,
                    Projectile.owner, fallAngle, i * 2);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect effect = EffectLoader.CultistPlanet?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            SpriteBatch sb = Main.spriteBatch;
            float scale = FormIn * CollapseScale;
            if (scale <= 0.03f) {
                return false;
            }
            if (effect == null || canvas == null || noise == null) {
                //着色器缺席回退：软光球
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    sb.Draw(glow, Projectile.Center - Main.screenPosition, null,
                        (CultistMotion.StardustCore with { A = 0 }) * (0.8f * scale), 0f,
                        glow.Size() * 0.5f, VisRadius * 2f / glow.Width * scale, SpriteEffects.None, 0f);
                }
                return false;
            }

            //uniform 全参数重设（共享 shader 的设备全局残留陷阱）
            effect.CurrentTechnique = effect.Techniques["TechStardust"];
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + Projectile.identity * 0.37f);
            effect.Parameters["uAlpha"]?.SetValue(FormIn);
            effect.Parameters["uSpin"]?.SetValue(0.05f);
            effect.Parameters["uShear"]?.SetValue(0f);
            effect.Parameters["uTilt"]?.SetValue(-0.35f);
            effect.Parameters["uLightDir"]?.SetValue(new Vector3(-0.45f, -0.55f, 0.70f));
            effect.Parameters["uColDeep"]?.SetValue(new Vector3(0.02f, 0.05f, 0.10f));
            effect.Parameters["uColMid"]?.SetValue(new Vector3(0.16f, 0.38f, 0.48f));
            effect.Parameters["uColBright"]?.SetValue(new Vector3(0.62f, 0.90f, 0.95f));
            effect.Parameters["uColStorm"]?.SetValue(new Vector3(0.95f, 1.0f, 1.0f));
            effect.Parameters["uSolidity"]?.SetValue(0.62f);
            effect.Parameters["uPupil"]?.SetValue(0f);

            //TechStardust 环系延伸到 ~2.2 倍球径，画布折算同一契约、坍缩走 quad 缩放
            float quadSize = VisRadius / 0.42f * 2f * scale;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(canvas, Projectile.Center - Main.screenPosition, null, Color.White, 0f,
                canvas.Size() * 0.5f, quadSize / canvas.Width, SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }

    /// <summary>
    /// 坠星：原版坠落之星精灵晶青染色，沿声明角加速下坠，同材质残影拖尾<br/>
    /// ai[0]=下落角 ai[1]=起落延迟；穿透 2，逐星单次命中
    /// </summary>
    internal class RiteStarShard : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FallingStar;

        private ref float Timer => ref Projectile.localAI[0];
        private float FallAngle => Projectile.ai[0];
        private int Delay => (int)Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 240;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Timer++;
            if (Timer < Delay) {
                Projectile.velocity = Vector2.Zero;
                return;
            }
            if ((int)Timer == Delay && !VaultUtils.isServer && CultistMotion.OnScreen(Projectile.Center, 300f)) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.35f, Pitch = 0.4f }, Projectile.Center);
            }

            //沿声明角加速下坠
            float speed = MathHelper.Clamp((Timer - Delay) * 0.6f, 4f, 21f);
            Projectile.velocity = FallAngle.ToRotationVector2() * speed;
            Projectile.tileCollide = Timer > Delay + 10;
            Projectile.rotation += 0.24f;

            if (!VaultUtils.isServer && Main.rand.NextBool(3) && CultistMotion.OnScreen(Projectile.Center, 200f)) {
                InnoVault.PRT.PRTLoader.NewParticle<PRT_CultistFrostMote>(Projectile.Center,
                    -Projectile.velocity * 0.08f,
                    Color.Lerp(CultistMotion.StardustCore, CultistMotion.StardustEdge, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.5f, 1.0f))?.Configure(Main.rand.Next(14, 24));
            }
            Lighting.AddLight(Projectile.Center, CultistMotion.StardustCore.ToVector3() * 0.4f);
        }

        public override void OnKill(int timeLeft) {
            //撞击收尾：16 星齐落时按序号节流音效，防连响糊团
            CultistMotion.ImpactBurst(Projectile.Center, 1, 0.9f, playSound: Projectile.identity % 3 == 0);
            if (Projectile.identity % 4 == 0) {
                CultistMotion.Shake(Projectile.Center, 1.8f, 5);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Timer < Delay) {
                return false;
            }
            Main.instance.LoadProjectile(ProjectileID.FallingStar);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.FallingStar].Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            //同材质残影拖尾
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, oldPos, null,
                    CultistMotion.StardustCore with { A = 0 } * (0.32f * t),
                    Projectile.rotation - i * 0.24f, origin, 0.7f + 0.2f * t, SpriteEffects.None, 0);
            }

            if (glow != null) {
                Main.EntitySpriteDraw(glow, pos, null, CultistMotion.StardustCore with { A = 0 } * 0.5f,
                    0f, glow.Size() * 0.5f, 0.6f, SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(tex, pos, null, Color.Lerp(Color.White, CultistMotion.StardustCore, 0.35f),
                Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
            return false;
        }
    }
}
