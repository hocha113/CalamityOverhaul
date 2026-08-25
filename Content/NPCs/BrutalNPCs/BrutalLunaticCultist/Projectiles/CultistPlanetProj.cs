using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 教徒召唤的巨型天体（CultistPlanet.fx，一种星球一个 technique）<br/>
    /// ai[0]=星球种类 0星旋 1星云 2星尘 3日耀 4月明 ai[1]=宿主npc ai[2]=阶段包装(个位:0降临 1常驻 2退场;十位:幻象序号)<br/>
    /// 运动学:星旋小幅游走/星云漂移(带幻象)/星尘绕教徒公转/日耀月明钉死场心<br/>
    /// 公平阀:碰撞半径小于可见球体;开火走 PlanetVolleyGate 与本体轮流出手;降临成形前无判定
    /// </summary>
    internal class CultistPlanetProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int KindVortex = 0;
        internal const int KindNebula = 1;
        internal const int KindStardust = 2;
        internal const int KindSolar = 3;
        internal const int KindMoon = 4;

        private const int ArriveFrames = 56;
        private const int DepartFrames = 46;

        private int Kind => (int)Projectile.ai[0];
        private int OwnerWho => (int)Projectile.ai[1];
        private int Stage => (int)Projectile.ai[2] % 10;
        private int PhantomIndex => (int)Projectile.ai[2] / 10;
        private bool IsPhantom => PhantomIndex > 0;

        private ref float Timer => ref Projectile.localAI[0];
        /// <summary>星尘公转角(各端本地积分,权威端位置广播兜底)</summary>
        private ref float OrbitAngle => ref Projectile.localAI[1];

        /// <summary>可见球体半径(px),shader 球盘=画布 0.42,quad 按此折算</summary>
        internal float VisRadius => Kind switch {
            KindNebula => 320f,
            KindStardust => 250f,
            KindSolar => 360f,
            KindMoon => 520f,
            _ => 340f,
        };

        /// <summary>碰撞半径:小于可见体(对玩家宽容);星云是气,判定更松</summary>
        private float CollisionRadius => VisRadius * (Kind == KindNebula ? 0.70f : 0.88f) * Projectile.scale;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 18000;
            Projectile.netImportant = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Timer++;
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            bool ownerAlive = owner != null && owner.active && owner.type == NPCID.CultistBoss;

            //宿主没了:直接进退场
            if (!ownerAlive && Stage != 2) {
                SetStage(2);
            }

            CultistStateContext context = null;
            if (ownerAlive && owner.TryGetOverride(out CultistBossAI overrideAI)) {
                context = overrideAI.Context;
            }

            //生命阶段
            switch (Stage) {
                case 0: {
                    //降临:假纵深从远处逼近,cubed 缓出
                    float t = MathHelper.Clamp(Timer / ArriveFrames, 0f, 1f);
                    float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                    Projectile.scale = 0.08f + 0.92f * ease;
                    if (Timer >= ArriveFrames) {
                        SetStage(1);
                        //落位一击
                        CultistMotion.Shake(Projectile.Center, 7f, 14);
                        CultistMotion.RuneBurst(Projectile.Center, CultistMotion.PhaseCore(Kind), 18, 9f);
                        if (!VaultUtils.isServer) {
                            SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 1.1f, Pitch = -0.6f }, Projectile.Center);
                        }
                    }
                    break;
                }
                case 1:
                    Projectile.scale = MathHelper.Lerp(Projectile.scale, 1f, 0.1f);
                    break;
                default: {
                    //退场:收缩渐隐,散成符文
                    Projectile.scale *= 0.965f;
                    if (Timer % 5 == 0) {
                        CultistMotion.RuneBurst(Projectile.Center, CultistMotion.PhaseCore(Kind), 2, 6f);
                    }
                    if (Projectile.scale < 0.1f) {
                        Projectile.Kill();
                        return;
                    }
                    break;
                }
            }
            if (Projectile.timeLeft < 120 && Stage != 2) {
                Projectile.timeLeft = 120;
            }

            //运动学(权威端写位置,netImportant 广播)
            if (!VaultUtils.isClient && ownerAlive && context != null) {
                Vector2 anchor = ComputeAnchor(context, owner);
                Projectile.velocity = (anchor - Projectile.Center) * 0.045f;
                if (Projectile.velocity.Length() > 15f) {
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 15f;
                }
                if (Main.GameUpdateCount % 45 == 0) {
                    Projectile.netUpdate = true;
                }
            }
            else if (VaultUtils.isClient) {
                //客户端沿广播速度自走,权威端周期兜底
                Projectile.velocity *= 0.995f;
            }

            //星球自身的弹幕:与本体轮流出手(公平阀),幻象不开火
            if (!VaultUtils.isClient && context != null && Stage == 1 && !IsPhantom && context.PlanetVolleyGate) {
                EmitVolley(context, owner);
            }

            //体光
            float glow = Kind == KindSolar ? 1.1f : 0.55f;
            Lighting.AddLight(Projectile.Center, CultistMotion.PhaseCore(Kind).ToVector3() * glow * Projectile.scale);
        }

        private void SetStage(int stage) {
            Projectile.ai[2] = PhantomIndex * 10 + stage;
            Timer = 0;
            Projectile.netUpdate = true;
        }

        /// <summary>各星球的运动学锚点</summary>
        private Vector2 ComputeAnchor(CultistStateContext context, NPC owner) {
            Vector2 center = context.ArenaCenter;
            float t = Main.GlobalTimeWrappedHourly;
            switch (Kind) {
                case KindNebula: {
                    //星云:缓慢漂移;幻象各占相位角
                    float phase = PhantomIndex * MathHelper.TwoPi / 3f;
                    return center + new Vector2(
                        (float)Math.Sin(t * 0.21f + phase) * 260f,
                        (float)Math.Cos(t * 0.16f + phase) * 170f);
                }
                case KindStardust: {
                    //星尘:绕教徒公转,扫过圆环的钟表指针
                    OrbitAngle += 0.011f;
                    return owner.Center + OrbitAngle.ToRotationVector2() * 560f;
                }
                case KindSolar:
                case KindMoon:
                    //日耀/月明:钉死场心炙烤
                    return center;
                default:
                    //星旋:小幅利萨茹游走,环宽有呼吸
                    return center + new Vector2(
                        (float)Math.Sin(t * 0.30f) * 130f,
                        (float)Math.Sin(t * 0.47f + 1.3f) * 90f);
            }
        }

        /// <summary>星球弹幕(权威端):每种天体一种语言,缺口是声明常量</summary>
        private void EmitVolley(CultistStateContext context, NPC owner) {
            Player target = context.Target;
            if (target == null || !target.Alives()) {
                return;
            }
            switch (Kind) {
                case KindVortex: {
                    //星旋:10 槽缓速真言环,朝玩家扇区跳 3 槽(GapSlots=3,公平阀)
                    if (Timer % 96 != 0) {
                        return;
                    }
                    const int Slots = 10;
                    const int GapSlots = 3;
                    float playerAngle = (target.Center - Projectile.Center).ToRotation();
                    int gapCenter = (int)MathF.Round(playerAngle / MathHelper.TwoPi * Slots);
                    for (int i = 0; i < Slots; i++) {
                        int delta = Math.Abs(((i - gapCenter) % Slots + Slots + Slots / 2) % Slots - Slots / 2);
                        if (delta <= GapSlots / 2) {
                            continue;
                        }
                        Vector2 dir = (MathHelper.TwoPi * i / Slots).ToRotationVector2();
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                            Projectile.Center + dir * VisRadius * 0.9f, dir * 4.2f,
                            ModContent.ProjectileType<CultistTrueBolt>(), 38, 0f, Main.myPlayer, context.Phase);
                    }
                    CultistMotion.CastFlash(Projectile.Center, CultistMotion.VortexCore, 1.2f);
                    break;
                }
                case KindStardust: {
                    //星尘:公转切向甩晶弹,轨迹可由公转方向预读
                    if (Timer % 80 != 0) {
                        return;
                    }
                    Vector2 tangent = (OrbitAngle + MathHelper.PiOver2).ToRotationVector2();
                    for (int i = 0; i < 2; i++) {
                        Vector2 vel = tangent.RotatedBy((i - 0.5f) * 0.24f) * 5.6f;
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                            Projectile.Center + vel.SafeNormalize(Vector2.Zero) * VisRadius * 0.9f, vel,
                            ModContent.ProjectileType<CultistTrueBolt>(), 38, 0f, Main.myPlayer, context.Phase);
                    }
                    break;
                }
                case KindSolar: {
                    //日耀:日珥抛焰,沿可见抛物线落地成燃地
                    if (Timer % 92 != 0) {
                        return;
                    }
                    Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                    for (int i = 0; i < 2; i++) {
                        Vector2 vel = dir.RotatedBy((i - 0.5f) * 0.5f) * 7.5f - Vector2.UnitY * 4.5f;
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                            Projectile.Center + vel.SafeNormalize(Vector2.Zero) * VisRadius * 0.95f, vel,
                            ModContent.ProjectileType<CultistFlameBolt>(), 40, 0f, Main.myPlayer, 0f, 1f);
                    }
                    CultistMotion.CastFlash(Projectile.Center + dir * VisRadius, CultistMotion.SolarCore, 1f);
                    break;
                }
                //星云的压力是幻象本身,月明的攻击走激光态,都不开火
            }
        }

        /// <summary>圆形碰撞,可见即危险</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float radius = CollisionRadius;
            Vector2 center = Projectile.Center;
            Vector2 closest = new(
                MathHelper.Clamp(center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(center, closest) < radius * radius;
        }

        /// <summary>伤害窗=可见窗:成形后才咬人;幻象永不咬人(识真线索)</summary>
        public override bool CanHitPlayer(Player target)
            => Stage == 1 && Projectile.scale > 0.95f && !IsPhantom;

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //撞上行星:向外弹开,仁慈方向
            Vector2 push = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
            target.velocity = push * 11f;
            if (Kind == KindSolar) {
                target.AddBuff(BuffID.OnFire3, 180);
            }
        }

        /// <summary>命令某宿主的所有星球退场(权威端)</summary>
        internal static void BeginDeparture(int ownerWho) {
            int type = ModContent.ProjectileType<CultistPlanetProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[1] == ownerWho && (int)proj.ai[2] % 10 != 2) {
                    proj.ai[2] = (int)proj.ai[2] / 10 * 10 + 2;
                    proj.localAI[0] = 0f;
                    proj.netUpdate = true;
                }
            }
        }

        public override void DrawBehind(int index, System.Collections.Generic.List<int> behindNPCsAndTiles,
            System.Collections.Generic.List<int> behindNPCs, System.Collections.Generic.List<int> behindProjectiles,
            System.Collections.Generic.List<int> overPlayers, System.Collections.Generic.List<int> overWiresUI) {
            //压在 NPC 身后:星球是舞台,弹幕和本体都读得清
            behindNPCs.Add(index);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!CultistMotion.OnScreen(Projectile.Center, VisRadius * 2.4f)) {
                return false;
            }
            Effect effect = EffectLoader.CultistPlanet?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            SpriteBatch sb = Main.spriteBatch;
            if (effect == null || canvas == null || noise == null) {
                DrawFallback(sb);
                return false;
            }

            //瞳孔开度:月明专属,各端从本地 context 读
            float pupil = 0f;
            if (Kind == KindMoon && OwnerWho >= 0 && OwnerWho < Main.maxNPCs
                && Main.npc[OwnerWho].active && Main.npc[OwnerWho].TryGetOverride(out CultistBossAI ai)) {
                pupil = ai.Context?.PupilOpen ?? 0f;
            }

            //uniform 全参数重设(共享 shader 的设备全局残留陷阱)
            effect.CurrentTechnique = effect.Techniques[TechniqueName];
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + Projectile.identity * 0.37f);
            effect.Parameters["uAlpha"]?.SetValue(Stage == 2 ? Projectile.scale : 1f);
            effect.Parameters["uSpin"]?.SetValue(SpinOf(Kind));
            effect.Parameters["uShear"]?.SetValue(Kind == KindVortex ? 0.45f : 0f);
            effect.Parameters["uTilt"]?.SetValue(TiltOf(Kind));
            effect.Parameters["uLightDir"]?.SetValue(new Vector3(-0.45f, -0.55f, 0.70f));
            effect.Parameters["uColDeep"]?.SetValue(PaletteDeep(Kind));
            effect.Parameters["uColMid"]?.SetValue(PaletteMid(Kind));
            effect.Parameters["uColBright"]?.SetValue(PaletteBright(Kind));
            effect.Parameters["uColStorm"]?.SetValue(PaletteStorm(Kind));
            effect.Parameters["uSolidity"]?.SetValue(IsPhantom ? 0.22f : 0.62f);
            effect.Parameters["uPupil"]?.SetValue(pupil);

            //球盘=画布半径 0.42,quad 按可见半径折算(与 .fx 头部契约同步)
            float quadSize = VisRadius / 0.42f * 2f * Projectile.scale;

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

        /// <summary>着色器缺席回退:软光球剪影,至少占位可见</summary>
        private void DrawFallback(SpriteBatch sb) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            Color core = CultistMotion.PhaseCore(Kind) with { A = 255 };
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, core * 0.85f, 0f,
                glow.Size() * 0.5f, VisRadius * 2f / glow.Width * Projectile.scale, SpriteEffects.None, 0f);
        }

        private string TechniqueName => Kind switch {
            KindNebula => "TechNebula",
            KindStardust => "TechStardust",
            KindSolar => "TechSolar",
            KindMoon => "TechMoon",
            _ => "TechVortex",
        };

        private static float SpinOf(int kind) => kind switch {
            KindNebula => 0.010f,
            KindStardust => 0.030f,
            KindSolar => 0.020f,
            KindMoon => 0.002f,
            _ => 0.028f,
        };

        private static float TiltOf(int kind) => kind switch {
            KindStardust => -0.35f,
            KindVortex => -0.16f,
            _ => 0f,
        };

        private static Vector3 PaletteDeep(int kind) => kind switch {
            KindNebula => new(0.10f, 0.02f, 0.15f),
            KindStardust => new(0.02f, 0.05f, 0.10f),
            KindSolar => new(0.28f, 0.05f, 0.01f),
            KindMoon => new(0.10f, 0.10f, 0.13f),
            _ => new(0.012f, 0.035f, 0.075f),
        };

        private static Vector3 PaletteMid(int kind) => kind switch {
            KindNebula => new(0.46f, 0.10f, 0.46f),
            KindStardust => new(0.16f, 0.38f, 0.48f),
            KindSolar => new(0.85f, 0.32f, 0.05f),
            KindMoon => new(0.32f, 0.33f, 0.38f),
            _ => new(0.055f, 0.21f, 0.30f),
        };

        private static Vector3 PaletteBright(int kind) => kind switch {
            KindNebula => new(0.95f, 0.52f, 0.85f),
            KindStardust => new(0.62f, 0.90f, 0.95f),
            KindSolar => new(1.0f, 0.72f, 0.25f),
            KindMoon => new(0.62f, 0.64f, 0.70f),
            _ => new(0.40f, 0.78f, 0.86f),
        };

        private static Vector3 PaletteStorm(int kind) => kind switch {
            KindNebula => new(1.0f, 0.86f, 1.0f),
            KindStardust => new(0.95f, 1.0f, 1.0f),
            KindSolar => new(1.0f, 0.95f, 0.80f),
            KindMoon => new(0.55f, 1.0f, 0.85f),
            _ => new(0.72f, 0.94f, 1.0f),
        };
    }
}
