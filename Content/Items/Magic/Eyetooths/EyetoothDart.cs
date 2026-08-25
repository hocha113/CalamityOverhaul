using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Summon.EyekiteStaffs;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Eyetooths
{
    /// <summary>
    /// 泣血瞳牙牙镖。飞行段前几帧猛加速；首咬入肉后崩上目标头顶，
    /// 短暂聚焦锁定头部，再以更高速度俯身二咬<br/>
    /// ai0 状态 Flight/Pop/Aim/Slam，ai1 目标 whoAmI（-1 无），ai2 状态计时（按更新帧）
    /// </summary>
    internal class EyetoothDart : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Projectile_Magic + "EyetoothDart";

        private const int StFlight = 0;
        private const int StPop = 1;
        private const int StAim = 2;
        private const int StSlam = 3;

        /// <summary>崩起时长（更新帧，extraUpdates=1 下折半成游戏帧）</summary>
        private const int PopTime = 16;
        /// <summary>聚焦悬停时长</summary>
        private const int AimTime = 10;
        /// <summary>俯咬失的后转坠落的时限</summary>
        private const int SlamTimeout = 70;
        /// <summary>飞行段速度上限（每更新帧）</summary>
        private const float FlightCap = 15f;
        /// <summary>俯咬速度（每更新帧）</summary>
        private const float SlamSpeed = 13f;
        /// <summary>目标死亡后的补咬索敌半径</summary>
        private const float RetargetRange = 260f;

        private float State { get => Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private float TargetIndex { get => Projectile.ai[1]; set => Projectile.ai[1] = value; }
        private float Timer { get => Projectile.ai[2]; set => Projectile.ai[2] = value; }

        private Trail bloodTrail;
        private readonly Vector2[] trailPoints = new Vector2[EyetoothVFX.TrailPoints];
        /// <summary>拖尾热度，随速度攒随缓飞散</summary>
        private float heat;
        /// <summary>聚焦白芒强度</summary>
        private float gleam;
        /// <summary>崩起翻转角速度</summary>
        private float spin;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = EyetoothVFX.TrailPoints;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 200;
            Projectile.extraUpdates = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.ai[1] = -1f;
        }

        public override bool? CanDamage() => (int)State is StFlight or StSlam ? null : false;

        /// <summary>牙镖快，逐段线扫防穿隧</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if ((int)State is StPop or StAim) {
                return false;
            }
            Rectangle box = targetHitbox;
            box.Inflate(4, 4);
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(box.TopLeft(), box.Size()
                , Projectile.Center - Projectile.velocity, Projectile.Center, 12f, ref _);
        }

        public override void AI() {
            Timer++;
            switch ((int)State) {
                case StPop:
                    PopAI();
                    break;
                case StAim:
                    AimAI();
                    break;
                case StSlam:
                    SlamAI();
                    break;
                default:
                    FlightAI();
                    break;
            }
            Lighting.AddLight(Projectile.Center, 0.2f * heat + 0.04f, 0.04f, 0.05f);
        }

        private void FlightAI() {
            //出膛猛加速，尾段轻微泄劲，不给匀速平飞
            if (Timer < 10f) {
                Projectile.velocity *= 1.07f;
            }
            else {
                Projectile.velocity *= 0.998f;
            }
            if (Projectile.velocity.Length() > FlightCap) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * FlightCap;
            }

            //镖颤：出手抖动随行程收束，牙镖回稳
            float wobbleDecay = MathHelper.Clamp(1f - Timer / 26f, 0f, 1f);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2
                + MathF.Sin(Timer * 0.55f) * 0.16f * wobbleDecay;

            heat = MathHelper.Clamp(heat + 0.06f, 0f, 1f) * MathHelper.Clamp(Projectile.velocity.Length() / FlightCap + 0.4f, 0f, 1f);

            if (Timer % 7 == 0 && Projectile.velocity.Length() > 5f) {
                EyetoothVFX.FlightDrip(Projectile.Center, Projectile.velocity);
            }
        }

        private void PopAI() {
            Projectile.tileCollide = false;
            NPC target = ResolveTarget();
            if (target != null) {
                Vector2 toApex = ApexFor(target) - Projectile.Center;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toApex * 0.14f, 0.3f);
                if (Projectile.velocity.Length() > 13f) {
                    Projectile.velocity = Projectile.velocity.SafeNormalize(-Vector2.UnitY) * 13f;
                }
                if (toApex.Length() < 12f || Timer >= PopTime) {
                    Enter(StAim);
                }
            }
            else {
                TryRetargetOrDrop();
            }

            //拔出后的空翻，角速度渐停
            Projectile.rotation += spin;
            spin *= 0.93f;
            heat *= 0.94f;
        }

        private void AimAI() {
            Projectile.tileCollide = false;
            Projectile.velocity *= 0.8f;
            Projectile.velocity.Y -= 0.02f;

            NPC target = ResolveTarget();
            if (target == null) {
                TryRetargetOrDrop();
                return;
            }

            //牙尖咬向头顶，聚焦收束
            Vector2 aimDir = (HeadPoint(target) - Projectile.Center).SafeNormalize(Vector2.UnitY);
            Projectile.rotation = Projectile.rotation.AngleLerp(aimDir.ToRotation() + MathHelper.PiOver2, 0.38f);
            gleam = MathHelper.Clamp(Timer / AimTime, 0f, 1f);

            if (Timer == 3f && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.32f, Pitch = 0.4f, MaxInstances = 3 }, Projectile.Center);
            }

            if (Timer >= AimTime) {
                BeginSlam(aimDir);
            }
        }

        private void SlamAI() {
            Projectile.tileCollide = true;
            NPC target = ResolveTarget();
            if (target != null && Timer <= SlamTimeout) {
                Vector2 desired = (HeadPoint(target) - Projectile.Center).SafeNormalize(
                    Projectile.velocity.SafeNormalize(Vector2.UnitY));
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired * SlamSpeed, 0.4f);
            }
            else {
                //失的转坠，吃重力砸向地面
                Projectile.velocity.X *= 0.99f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.3f, 16f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            heat = 1f;
            gleam *= 0.88f;
        }

        /// <summary>崩起顶点，横向偏移按 identity 定死，全端一致</summary>
        private Vector2 ApexFor(NPC target) {
            float side = (EyekiteVFX.Hash(Projectile.identity, 11) - 0.5f) * 2f;
            float rise = 34f + MathF.Min(target.height, 180f) * 0.16f;
            return target.Top + new Vector2(side * (10f + target.width * 0.12f), -rise);
        }

        private static Vector2 HeadPoint(NPC target)
            => target.Top + new Vector2(0f, MathF.Min(10f, target.height * 0.15f));

        private NPC ResolveTarget() {
            int idx = (int)TargetIndex;
            if (idx >= 0 && idx < Main.maxNPCs) {
                NPC npc = Main.npc[idx];
                if (npc.active && npc.CanBeChasedBy(Projectile)) {
                    return npc;
                }
            }
            return null;
        }

        /// <summary>目标没了就近补咬，找不到便直坠落地</summary>
        private void TryRetargetOrDrop() {
            if (Projectile.owner == Main.myPlayer) {
                NPC next = Projectile.Center.FindClosestNPC(RetargetRange);
                if (next != null) {
                    TargetIndex = next.whoAmI;
                    Projectile.netUpdate = true;
                    return;
                }
            }
            if ((int)State != StSlam) {
                BeginSlam(Vector2.UnitY);
            }
        }

        private void Enter(int state) {
            State = state;
            Timer = 0f;
        }

        private void BeginSlam(Vector2 aimDir) {
            Enter(StSlam);
            Projectile.velocity = aimDir * SlamSpeed;
            heat = 1f;
            int idx = (int)TargetIndex;
            if (idx >= 0 && idx < Main.maxNPCs) {
                //保证二咬能吃到同一目标
                Projectile.localNPCImmunity[idx] = 0;
            }
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.5f, Pitch = 0.5f, MaxInstances = 3 }, Projectile.Center);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if ((int)State == StSlam) {
                //俯咬吃 150% 伤害
                modifiers.FinalDamage *= 1.5f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if ((int)State == StFlight) {
                target.AddBuff(ModContent.BuffType<EyetoothWound>(), 240);
                EyetoothVFX.BiteSplat(Projectile.Center, Projectile.velocity);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.38f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.3f, Pitch = 0.1f, MaxInstances = 3 }, Projectile.Center);
                }

                TargetIndex = target.whoAmI;
                Enter(StPop);
                Projectile.timeLeft = 300;
                Vector2 popDir = (ApexFor(target) - Projectile.Center).SafeNormalize(-Vector2.UnitY);
                Projectile.velocity = popDir * 7f;
                spin = 0.4f * (hit.HitDirection >= 0 ? 1f : -1f);
                EyetoothVFX.RipOut(Projectile.Center, popDir);
                if (Projectile.owner == Main.myPlayer) {
                    Projectile.netUpdate = true;
                }
                return;
            }

            if ((int)State == StSlam) {
                target.AddBuff(ModContent.BuffType<EyetoothWound>(), 480);
                EyetoothVFX.SlamBurst(Projectile.Center, Projectile.velocity);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.5f, Pitch = -0.05f, MaxInstances = 3 }, Projectile.Center);
                }
                if (Projectile.owner == Main.myPlayer) {
                    Player owner = Main.player[Projectile.owner];
                    owner.CWR().ScreenShakeValue = Math.Max(owner.CWR().ScreenShakeValue, 2f);
                }
                Projectile.Kill();
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            EyetoothVFX.TileShatter(Projectile.Center, oldVelocity);
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.45f, Pitch = 0.3f, MaxInstances = 3 }, Projectile.Center);
            }
            return true;
        }

        public override void OnKill(int timeLeft) => EyetoothVFX.Residue(Projectile);

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            int state = (int)State;

            //俯咬余影，骨白拖影读出速度
            if (state == StSlam && Projectile.oldPos != null) {
                for (int g = 1; g <= 2; g++) {
                    int i = g * 3 - 1;
                    if (i >= Projectile.oldPos.Length || Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    Color ghost = EyetoothVFX.Bone * (g == 1 ? 0.32f : 0.14f);
                    ghost.A = 0;
                    Vector2 gPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                    Main.EntitySpriteDraw(tex, gPos, null, ghost, Projectile.oldRot[i], origin, Projectile.scale, SpriteEffects.None, 0);
                }
            }

            //本体，沿行进轴速度拉伸
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.04f, 0f, 0.45f);
            Vector2 scale = new Vector2(1f - stretch * 0.3f, 1f + stretch) * Projectile.scale;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor, Projectile.rotation, origin, scale, SpriteEffects.None, 0);

            if (gleam > 0.05f) {
                DrawFocus(drawPos);
            }
            return false;
        }

        /// <summary>聚焦演出：牙尖白芒加一线指向头顶的血色瞄准线</summary>
        private void DrawFocus(Vector2 drawPos) {
            Vector2 tipDir = (Projectile.rotation - MathHelper.PiOver2).ToRotationVector2();
            Vector2 tipPos = Projectile.Center + tipDir * 11f;
            float flicker = 0.72f + 0.28f * MathF.Sin(Main.GlobalTimeWrappedHourly * 34f + Projectile.identity);

            NPC target = ResolveTarget();
            if (target != null && VaultAsset.placeholder2?.Value is Texture2D px) {
                Vector2 to = HeadPoint(target) - tipPos;
                float len = to.Length();
                if (len > 8f) {
                    Color line = EyetoothVFX.Arterial * (0.38f * gleam * flicker);
                    line.A = 0;
                    Main.spriteBatch.Draw(px, tipPos - Main.screenPosition, null, line
                        , to.ToRotation(), new Vector2(0f, px.Height * 0.5f)
                        , new Vector2(len / px.Width, 1.6f / px.Height), SpriteEffects.None, 0f);
                }
            }

            if (CWRAsset.StarFlare01?.Value is Texture2D flare) {
                Color glow = EyetoothVFX.Bone * (0.75f * gleam * flicker);
                glow.A = 0;
                Main.EntitySpriteDraw(flare, tipPos - Main.screenPosition, null, glow
                    , Main.GlobalTimeWrappedHourly * 2.6f, flare.Size() * 0.5f
                    , 0.14f * gleam, SpriteEffects.None, 0);
            }
        }

        private float TrailWidth(float f) => 9f * heat * (0.15f + f * 0.85f);

        private Color TrailColor(Vector2 tex)
            => Color.Lerp(EyetoothVFX.BloodDeep, EyetoothVFX.Arterial, tex.X) * (heat * (0.22f + tex.X * 0.78f));

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !Projectile.active || heat <= 0.05f) {
                return;
            }
            EyetoothVFX.DrawBloodTrail(Projectile, ref bloodTrail, trailPoints, TrailWidth, TrailColor, heat);
        }
    }
}
