using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit.Projectiles
{
    /// <summary>
    /// 泡沫风暴大泡（泡泡枪泄压）。缓漂水膜泡，敌触即破。
    /// ai[0] = 破膜标志（owner 写 + netUpdate，远端同拍看到破膜）。
    /// 绘制复用 FishronBubble.fx 水膜合同（Duke 血统同源），着色器缺失时回退辉光双层
    /// </summary>
    internal class GsBubbleVentProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>盘径契约：可见膜半径 = 画布半宽 × 0.42（同 FishronBubbleRender）</summary>
        private const float DiskFrac = 0.42f;
        private const int PopTicks = 5;

        private bool Popping => Projectile.ai[0] >= 1f;
        private ref float PopTimer => ref Projectile.localAI[0];
        private ref float LifeTimer => ref Projectile.localAI[1];

        /// <summary>可见膜半径，判定同源</summary>
        private float MembraneRadius => Projectile.width * 0.62f * Projectile.scale;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 54;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            LifeTimer++;
            if (Popping) {
                //破膜演出各端本地推进；owner 在演出完 Kill（同步兜底）
                if (PopTimer == 0f && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.75f, Pitch = 0.2f, MaxInstances = 5 }, Projectile.Center);
                    for (int i = 0; i < 7; i++) {
                        PRTLoader.NewParticle<PRT_CampfireBubble>(
                            Projectile.Center + Main.rand.NextVector2Circular(MembraneRadius * 0.6f, MembraneRadius * 0.6f),
                            Main.rand.NextVector2Circular(2f, 1.5f) - new Vector2(0f, 1f),
                            GsConduitVFX.SeaBright, Main.rand.NextFloat(0.4f, 0.8f));
                    }
                }
                PopTimer++;
                Projectile.velocity *= 0.8f;
                if (PopTimer >= PopTicks && Projectile.owner == Main.myPlayer) {
                    Projectile.Kill();
                }
                return;
            }

            //缓漂：阻尼 + 轻上浮 + 正弦横摆（identity 定相，各端一致）
            Projectile.velocity *= 0.985f;
            Projectile.velocity.Y -= 0.012f;
            Projectile.velocity.X += MathF.Sin(LifeTimer * 0.055f + Projectile.identity * 1.7f) * 0.02f;
            Projectile.rotation = Projectile.velocity.X * 0.02f;
            Lighting.AddLight(Projectile.Center, GsConduitVFX.SeaMain.ToVector3() * 0.25f);

            //临终自然破膜
            if (Projectile.timeLeft <= PopTicks + 2 && Projectile.owner == Main.myPlayer) {
                BeginPop();
            }
        }

        private void BeginPop() {
            if (!Popping) {
                Projectile.ai[0] = 1f;
                Projectile.netUpdate = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //撞墙破膜：位置同步下各端几乎同帧检出，owner 写标志统一
            if (Projectile.owner == Main.myPlayer) {
                BeginPop();
            }
            Projectile.velocity = Vector2.Zero;
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => BeginPop();

        public override bool? CanDamage() => Popping ? false : null;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => GsConduitVFX.CircleVsRect(Projectile.Center, MembraneRadius, targetHitbox);

        public override bool PreDraw(ref Color lightColor) {
            Effect fx = EffectLoader.FishronBubble?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            float fade = MathHelper.Clamp(LifeTimer / 12f, 0f, 1f);
            if (fx == null || noise == null) {
                //着色器缺失回退：双层软辉光膜
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Vector2 pos = Projectile.Center - Main.screenPosition;
                float rScale = MembraneRadius * 2.6f / glow.Width;
                Main.EntitySpriteDraw(glow, pos, null, GsConduitVFX.SeaMain with { A = 0 } * (0.5f * fade),
                    0f, glow.Size() / 2f, rScale, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, pos, null, GsConduitVFX.SeaBright with { A = 0 } * (0.35f * fade),
                    0f, glow.Size() / 2f, rScale * 0.6f, SpriteEffects.None, 0);
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            fx.Parameters["uTint"]?.SetValue(GsConduitVFX.SeaMain.ToVector3() * 1.05f);
            fx.Parameters["uDeepColor"]?.SetValue(GsConduitVFX.SeaDeep.ToVector3());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + Projectile.identity * 0.61f);
            fx.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.173f);
            fx.Parameters["uWobble"]?.SetValue(MathHelper.Clamp(0.45f + Projectile.velocity.Length() * 0.08f, 0.45f, 1f));
            fx.Parameters["uArm"]?.SetValue(0f);
            fx.Parameters["uBurst"]?.SetValue(Popping ? MathHelper.Clamp(PopTimer / 3.2f, 0f, 1f) : 0f);
            fx.Parameters["uFade"]?.SetValue(fade);

            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            float quad = MembraneRadius / DiskFrac * 2f;
            sb.Draw(pixel, Projectile.Center - Main.screenPosition, null, Color.White,
                0f, pixel.Size() / 2f, new Vector2(quad / pixel.Width, quad / pixel.Height), SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }

    /// <summary>
    /// 风暴眼锚点（刀刃台风泄压）。光标处驻留 2s 的向心风暴：
    /// 己方在场风刃被路由逐帧导向此锚（原版 AI 先跑、汇聚覆写后写），
    /// 锚自带旋涡 tick 判定（与可见环同源），寿命尽 owner 端散爆环
    /// </summary>
    internal class GsTyphoonEyeProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const float EyeRadius = 130f;
        private ref float Timer => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            Projectile.timeLeft = 120;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Timer == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item84 with { Volume = 0.9f, Pitch = -0.2f }, Projectile.Center);
            }
            Timer++;
            Lighting.AddLight(Projectile.Center, new Vector3(0.2f, 0.35f, 0.7f));
            if (VaultUtils.isServer || Main.GameUpdateCount % 2 != 0) {
                return;
            }
            //切向流云（预算 ≤2/帧）
            float ang = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 at = Projectile.Center + ang.ToRotationVector2() * Main.rand.NextFloat(EyeRadius * 0.5f, EyeRadius);
            Vector2 tangent = (ang + MathHelper.PiOver2).ToRotationVector2() * Main.rand.NextFloat(3f, 6f);
            PRTLoader.NewParticle<PRT_SvcCloud>(at, tangent - ang.ToRotationVector2() * 0.8f,
                Color.Lerp(GsConduitVFX.SeaMain, new Color(90, 120, 230), Main.rand.NextFloat()), Main.rand.NextFloat(0.35f, 0.6f));
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => GsConduitVFX.CircleVsRect(Projectile.Center, EyeRadius, targetHitbox);

        public override void OnKill(int timeLeft) {
            //散爆：owner 端结算（OnKill 各端都跑，生成守门）
            if (Projectile.owner == Main.myPlayer) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsConduitNovaProj>(),
                    (int)(Projectile.damage * 1.6f), 6f, Projectile.owner, 170f + 3 * 1024f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float grow = VaultUtils.EaseOutCubic(MathHelper.Clamp(Timer / 10f, 0f, 1f));
            float fade = MathHelper.Clamp(Projectile.timeLeft / 18f, 0f, 1f);
            Color deepBlue = new(60, 90, 210);
            //外环 = 判定半径同源；内环反向差速旋
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, EyeRadius * grow, 20f,
                GsConduitVFX.SeaBright, GsConduitVFX.SeaMain, deepBlue, 0.8f * fade,
                innerGlow: 0.25f, timeSeed: Projectile.identity * 0.29f);
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, EyeRadius * 0.45f * grow, 12f,
                Color.White, deepBlue, GsConduitVFX.SeaDeep, 0.55f * fade,
                timeSeed: Projectile.identity * 0.29f + 40f);
            return false;
        }
    }

    /// <summary>
    /// 弹幕清膛主控（激光机枪泄压）。1.2s 扇形 30 连速射：
    /// owner 端按确定性扇摆角批量生成原版机枪激光（源 Misc 不承签，伤害已烘焙 ×0.5）
    /// </summary>
    internal class GsMachinegunVentProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int FireTicks = 60;
        private ref float Timer => ref Projectile.localAI[0];
        private Player Owner => Main.player[Projectile.owner];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = FireTicks + 14;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }
            Vector2 aim = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
            Projectile.Center = Owner.MountedCenter + aim * 26f;

            //持械姿态锁定
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.ChangeDir(aim.X >= 0f ? 1 : -1);
            Owner.itemRotation = (aim * Owner.direction).ToRotation();
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, aim.ToRotation() - MathHelper.PiOver2);

            if (Timer < FireTicks) {
                //扇摆是 Timer 的确定函数，各端一致；生成只在 owner 端
                float sway = MathF.Sin(Timer * 0.35f) * 0.24f;
                Vector2 dir = aim.RotatedBy(sway);
                if (Timer % 2 == 0 && Projectile.owner == Main.myPlayer) {
                    Projectile.NewProjectile(Owner.GetSource_Misc("GsConduitVent"), Projectile.Center,
                        dir * 14f, ProjectileID.LaserMachinegunLaser, Projectile.damage, 1.6f, Projectile.owner);
                }
                if (Timer % 4 == 0 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 6 }, Projectile.Center);
                    PRTLoader.NewParticle<PRT_TwinsSpark>(Projectile.Center + dir * 8f, dir * 2f,
                        GsConduitVFX.ForgeBright, 0.4f);
                }
            }
            Timer++;
        }

        public override bool PreDraw(ref Color lightColor) {
            //枪口过热辉光
            if (Timer >= FireTicks) {
                return false;
            }
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float flick = 0.7f + 0.3f * MathF.Sin(Timer * 0.9f);
            Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null,
                GsConduitVFX.ForgeMain with { A = 0 } * (0.6f * flick), 0f, glow.Size() / 2f, 0.4f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 群魂夜行主控（幽灵法杖泄压）。ai[0] = 魂数（1~5）。
    /// 绕体 1s 自绘魂影（不生成实体），随后逐只放出原版亡魂扑向最近敌
    /// （owner 端生成，源 Misc 不承签，伤害已烘焙 ×0.9）
    /// </summary>
    internal class GsSpecterVentProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int OrbitTicks = 60;
        private const int ReleaseGap = 10;

        private int SoulCount => Math.Clamp((int)Projectile.ai[0], 1, 5);
        private ref float Timer => ref Projectile.localAI[0];
        private ref float Released => ref Projectile.localAI[1];
        private Player Owner => Main.player[Projectile.owner];

        private static readonly Color SoulGlow = new(150, 255, 210);
        private static readonly Color SoulDeep = new(40, 120, 110);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = OrbitTicks + ReleaseGap * 5 + 20;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = Owner.MountedCenter;
            if (Timer == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.8f, Pitch = -0.4f }, Projectile.Center);
            }
            Timer++;
            Lighting.AddLight(Projectile.Center, SoulGlow.ToVector3() * 0.3f);

            if (!VaultUtils.isServer && Main.GameUpdateCount % 3 == 0) {
                int idx = Main.rand.Next(SoulCount);
                PRTLoader.NewParticle<PRT_SoulLight>(SoulPos(idx), Main.rand.NextVector2Circular(0.5f, 0.5f),
                    SoulGlow, Main.rand.NextFloat(0.3f, 0.5f));
            }

            //绕体结束后逐只放魂
            if (Timer >= OrbitTicks && Released < SoulCount && (Timer - OrbitTicks) % ReleaseGap == 0) {
                if (Projectile.owner == Main.myPlayer) {
                    ReleaseSoul((int)Released);
                }
                Released++;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.55f, Pitch = 0.2f, MaxInstances = 4 }, Projectile.Center);
                }
            }
            if (Released >= SoulCount && Timer >= OrbitTicks + ReleaseGap * SoulCount + 10) {
                Projectile.Kill();
            }
        }

        private Vector2 SoulPos(int i) {
            float phase = Timer * 0.09f + MathHelper.TwoPi * i / SoulCount;
            float radius = 56f + 8f * MathF.Sin(Timer * 0.05f + i * 1.3f);
            return Owner.MountedCenter + phase.ToRotationVector2() * radius;
        }

        private void ReleaseSoul(int index) {
            //最近敌扫描：owner 端离散决策，生成即随包同步
            NPC best = null;
            float bestDist = 700f * 700f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float d = npc.DistanceSQ(Owner.MountedCenter);
                if (d < bestDist) {
                    bestDist = d;
                    best = npc;
                }
            }
            Vector2 from = SoulPos(index);
            Vector2 dir = best != null
                ? (best.Center - from).SafeNormalize(-Vector2.UnitY)
                : (Main.MouseWorld - from).SafeNormalize(-Vector2.UnitY);
            Projectile.NewProjectile(Owner.GetSource_Misc("GsConduitVent"), from, dir * 9f,
                ProjectileID.LostSoulFriendly, Projectile.damage, 2f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            float fade = Released >= SoulCount ? MathHelper.Clamp(Projectile.timeLeft / 15f, 0f, 1f) : 1f;
            for (int i = 0; i < SoulCount; i++) {
                if (i < (int)Released) {
                    continue;
                }
                Vector2 pos = SoulPos(i) - Main.screenPosition;
                float pulse = 0.75f + 0.25f * MathF.Sin(Timer * 0.2f + i * 2.1f);
                Main.EntitySpriteDraw(glow, pos, null, SoulDeep with { A = 0 } * (0.7f * fade * pulse),
                    0f, glow.Size() / 2f, 0.52f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, pos, null, SoulGlow with { A = 0 } * (0.55f * fade * pulse),
                    0f, glow.Size() / 2f, 0.3f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(star, pos, null, SoulGlow with { A = 0 } * (0.4f * fade * pulse),
                    Timer * 0.06f + i, star.Size() / 2f, 0.1f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
