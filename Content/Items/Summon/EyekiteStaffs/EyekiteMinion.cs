using CalamityOverhaul.Common;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Summon.EyekiteStaffs
{
    /// <summary>
    /// ai0 状态 Idle/Windup/Charge/Yank/Recover
    /// ai1 状态计时
    /// ai2 目标 whoAmI，-1 无
    /// </summary>
    internal class EyekiteMinion : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Projectile_Summon + "EyekiteMinion";

        private const int StIdle = 0;
        private const int StWindup = 1;
        private const int StCharge = 2;
        private const int StYank = 3;
        private const int StRecover = 4;

        private const int WindupTime = 8;
        private const int ChargeTime = 16;
        private const int YankTime = 14;
        private const int RecoverTime = 22;

        private const float RestRadius = 108f;
        private const float IdleLeash = 236f;
        private const float ChargeLeash = 540f;
        private const float DetectRange = 520f;

        private float State { get => Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private float Timer { get => Projectile.ai[1]; set => Projectile.ai[1] = value; }
        private float TargetIndex { get => Projectile.ai[2]; set => Projectile.ai[2] = value; }

        private Trail cordTrail;
        private Trail dashTrail;
        private readonly Vector2[] cordPoints = new Vector2[EyekiteVFX.CordPoints];
        private readonly Vector2[] dashPoints = new Vector2[EyekiteVFX.TrailPoints];
        private float visualTension;
        private float visualTwang;
        private float visualTwangPos;
        private float dashHeat;
        private float appear;
        /// <summary>朝左时垂直翻转采样，带滞回防正上正下抖</summary>
        private bool faceFlipped;
        private Color cordLight = Color.White;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.MinionSacrificable[Type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Type] = true;
            ProjectileID.Sets.TrailCacheLength[Type] = EyekiteVFX.TrailPoints;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.minion = true;
            Projectile.minionSlots = 1f;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.netImportant = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 18000;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.ai[2] = -1f;
        }

        public override bool? CanDamage() => State is StCharge or StYank;

        public override bool MinionContactDamage() => State is StCharge or StYank;

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }
            //增益在场才续命：取消增益后寿命自然耗尽即解散。
            //禁止在这里反向 AddBuff，否则玩家永远取消不掉召唤
            if (Owner.HasBuff(ModContent.BuffType<EyekiteBuff>())) {
                Projectile.timeLeft = 2;
            }

            if (appear < 1f) {
                appear = MathHelper.Clamp(appear + 0.08f, 0f, 1f);
            }

            Vector2 anchor = CordAnchor();
            if (Projectile.Distance(Owner.Center) > 2400f) {
                Projectile.Center = RestPosition();
                Projectile.velocity *= 0.1f;
                Enter(StIdle);
            }

            switch ((int)State) {
                case StWindup:
                    WindupAI();
                    break;
                case StCharge:
                    ChargeAI();
                    break;
                case StYank:
                    YankAI(anchor);
                    break;
                case StRecover:
                    RecoverAI(anchor);
                    break;
                default:
                    IdleAI(anchor);
                    break;
            }

            float leash = State is StCharge or StYank ? ChargeLeash : IdleLeash;
            ClampLeash(anchor, leash);
            UpdateVisual(anchor);
            Lighting.AddLight(Projectile.Center, 0.28f, 0.06f, 0.07f);
        }

        private Vector2 CordAnchor() {
            //肩侧略偏朝向，风筝线从人身上出去
            float side = Owner.direction;
            return Owner.Center + new Vector2(side * 6f, -12f * Owner.gravDir);
        }

        private void CountFlock(out int slot, out int total) {
            slot = 0;
            total = 0;
            int selfId = Projectile.identity;
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.owner != Projectile.owner || p.type != Type) {
                    continue;
                }
                total++;
                if (p.identity < selfId) {
                    slot++;
                }
            }
            if (total < 1) {
                total = 1;
            }
        }

        private Vector2 RestPosition() {
            CountFlock(out int slot, out int total);
            float t = Main.GameUpdateCount * 0.012f + Projectile.identity * 0.73f;
            float ang = MathHelper.TwoPi * slot / total + t + MathF.Sin(t * 0.65f + slot) * 0.28f;
            float rad = RestRadius + (Projectile.identity % 5) * 7f + MathF.Sin(t * 1.4f) * 10f;
            Vector2 orbit = ang.ToRotationVector2() * rad;
            orbit.Y = orbit.Y * 0.62f - 36f * Owner.gravDir;
            return Owner.Center + orbit;
        }

        private bool InStruggleWindow() {
            int cycle = (int)(Main.GameUpdateCount + Projectile.identity * 19) % 268;
            return cycle >= 208 && cycle < 244;
        }

        private void IdleAI(Vector2 anchor) {
            Vector2 rest = RestPosition();
            Vector2 wind;
            float wt = Main.GameUpdateCount * 0.031f + Projectile.identity * 0.4f;
            wind = new Vector2(MathF.Sin(wt) * 9f, MathF.Cos(wt * 0.73f) * 6f);

            if (InStruggleWindow()) {
                Vector2 away = (Projectile.Center - Owner.Center).SafeNormalize(Vector2.UnitX);
                Projectile.velocity += away * 0.92f;
                if (Timer == 0 && Projectile.soundDelay <= 0) {
                    Timer = 1f;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.35f, Pitch = 0.4f }, Projectile.Center);
                    }
                    Projectile.soundDelay = 30;
                }
            }
            else {
                Timer = 0f;
            }

            Vector2 want = rest + wind;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, (want - Projectile.Center) * 0.085f, 0.18f);
            Projectile.velocity *= 0.96f;

            float dist = Vector2.Distance(Projectile.Center, anchor);
            if (dist > RestRadius * 1.15f) {
                Vector2 pull = (anchor - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity += pull * (dist - RestRadius) * 0.018f;
            }

            if (Projectile.owner == Main.myPlayer && Main.GameUpdateCount % 8 == (uint)(Projectile.identity % 8)) {
                NPC target = PickTarget();
                if (target != null) {
                    TargetIndex = target.whoAmI;
                    Enter(StWindup);
                    Projectile.netUpdate = true;
                }
            }

            if (!Main.dedServ && EyekiteVFX.Hash(Projectile.identity, (int)Main.GameUpdateCount) < 0.012f) {
                Vector2 dripAt = Vector2.Lerp(anchor, Projectile.Center, 0.45f + EyekiteVFX.Hash(Projectile.identity + 3, (int)Main.GameUpdateCount) * 0.3f);
                EyekiteVFX.IdleDrip(dripAt);
            }
        }

        private void WindupAI() {
            Timer++;
            Vector2 pull = (Owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
            Projectile.velocity = Projectile.velocity * 0.78f + pull * 1.4f;
            if (Timer >= WindupTime) {
                NPC target = ResolveTarget();
                Vector2 aim = target != null
                    ? target.Center - Projectile.Center
                    : Projectile.velocity;
                Projectile.velocity = aim.SafeNormalize(Vector2.UnitX) * 7.5f;
                Enter(StCharge);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.55f, Pitch = -0.15f }, Projectile.Center);
                    EyekiteVFX.ChargeSpray(Projectile.Center, Projectile.velocity);
                }
                if (Projectile.owner == Main.myPlayer) {
                    Projectile.netUpdate = true;
                }
            }
        }

        private void ChargeAI() {
            Timer++;
            NPC target = ResolveTarget();
            Vector2 aim = target != null ? target.Center : Projectile.Center + Projectile.velocity;
            Vector2 accel = (aim - Projectile.Center).SafeNormalize(Vector2.UnitX) * 2.15f;
            //前 3 帧猛加速，后半不再匀速爬
            float gain = Timer < 4f ? 1.35f : Timer < 10f ? 1.08f : 0.96f;
            Projectile.velocity = (Projectile.velocity + accel) * gain;
            float cap = MathHelper.Lerp(10f, 21f, MathHelper.Clamp(Timer / 8f, 0f, 1f));
            if (Projectile.velocity.Length() > cap) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * cap;
            }

            dashHeat = MathHelper.Clamp(dashHeat + 0.18f, 0f, 1f);
            if (Timer % 3 == 0 && !Main.dedServ) {
                EyekiteVFX.ChargeSpray(Projectile.Center, Projectile.velocity);
            }

            Vector2 anchor = CordAnchor();
            bool overLeash = Vector2.Distance(Projectile.Center, anchor) > ChargeLeash - 36f;
            if (Timer >= ChargeTime || overLeash || target == null) {
                BeginYank(anchor);
            }
        }

        private void BeginYank(Vector2 anchor) {
            Vector2 toAnchor = (anchor - Projectile.Center).SafeNormalize(Vector2.UnitY);
            Projectile.velocity = toAnchor * 26f;
            Enter(StYank);
            visualTwang = 1f;
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item153 with { Volume = 0.7f, Pitch = 0.15f }, Projectile.Center);
                EyekiteVFX.YankBurst(Projectile.Center, toAnchor);
            }
            if (Projectile.owner == Main.myPlayer) {
                Projectile.netUpdate = true;
            }
        }

        private void YankAI(Vector2 anchor) {
            Timer++;
            Vector2 toAnchor = (anchor - Projectile.Center).SafeNormalize(Vector2.UnitY);
            //爆完立刻减速，别匀速飞回去
            float keep = MathHelper.Lerp(0.92f, 0.78f, Timer / YankTime);
            Projectile.velocity = Projectile.velocity * keep + toAnchor * 1.8f;
            visualTwang = MathHelper.Clamp(1.1f - Timer / YankTime, 0f, 1f);
            dashHeat *= 0.9f;

            if (Timer >= YankTime) {
                Enter(StRecover);
                if (Projectile.owner == Main.myPlayer) {
                    Projectile.netUpdate = true;
                }
            }
        }

        private void RecoverAI(Vector2 anchor) {
            Timer++;
            Vector2 rest = RestPosition();
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, (rest - Projectile.Center) * 0.12f, 0.2f);
            Projectile.velocity *= 0.9f;
            visualTwang *= 0.84f;
            dashHeat *= 0.82f;
            if (Timer >= RecoverTime) {
                Enter(StIdle);
            }
            _ = anchor;
        }

        private void ClampLeash(Vector2 anchor, float maxLen) {
            Vector2 delta = Projectile.Center - anchor;
            float dist = delta.Length();
            if (dist > maxLen) {
                Projectile.Center = anchor + delta.SafeNormalize(Vector2.Zero) * maxLen;
                Vector2 inward = (anchor - Projectile.Center).SafeNormalize(Vector2.Zero);
                if (Vector2.Dot(Projectile.velocity, inward) < 0f) {
                    Projectile.velocity = Vector2.Reflect(Projectile.velocity, inward) * 0.35f + inward * 6f;
                }
            }
        }

        private void Enter(int state) {
            State = state;
            Timer = 0f;
        }

        private NPC PickTarget() {
            if (Owner.HasMinionAttackTargetNPC) {
                NPC tagged = Main.npc[Owner.MinionAttackTargetNPC];
                if (tagged.CanBeChasedBy(Projectile) && tagged.Distance(Owner.Center) < DetectRange + 180f) {
                    return tagged;
                }
            }
            return Projectile.Center.FindClosestNPC(DetectRange);
        }

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

        private void UpdateVisual(Vector2 anchor) {
            float dist = Vector2.Distance(Projectile.Center, anchor);
            float restLen = RestRadius * 1.05f;
            float wantTension = MathHelper.Clamp((dist - restLen) / (IdleLeash - restLen), 0f, 1f);
            if (State is StCharge or StYank) {
                wantTension = MathHelper.Lerp(wantTension, 1f, 0.65f);
            }
            visualTension = MathHelper.Lerp(visualTension, wantTension, 0.22f);
            if (State != StYank) {
                visualTwang *= 0.9f;
            }

            //期望朝向按状态取，再做最短路角度平滑，杜绝参考系硬切的帧间跳变
            Vector2 face;
            float turn;
            switch ((int)State) {
                case StWindup: {
                    NPC target = ResolveTarget();
                    face = target != null ? target.Center - Projectile.Center : Projectile.rotation.ToRotationVector2();
                    turn = 0.3f;
                    break;
                }
                case StCharge:
                    face = Projectile.velocity;
                    turn = 0.4f;
                    break;
                case StYank:
                    //被拽回时仍瞪着扑出去的方向，尾巴先行
                    face = -Projectile.velocity;
                    turn = 0.25f;
                    break;
                default:
                    face = Projectile.velocity.LengthSquared() > 2.2f
                        ? Projectile.velocity
                        : Projectile.Center - Owner.Center;
                    turn = 0.1f;
                    break;
            }

            float desired = face.SafeNormalize(Vector2.UnitX).ToRotation();
            if (State == StIdle && InStruggleWindow()) {
                //挣扎期慢频小幅摆头，不是高频振动
                desired += MathF.Sin(Main.GameUpdateCount * 0.31f + Projectile.identity * 1.7f) * 0.3f;
            }
            Projectile.rotation += MathHelper.WrapAngle(desired - Projectile.rotation) * turn;
        }

        private float CordWidthFunc(float t)
            => EyekiteVFX.CordWidth(t, visualTension, visualTwang, visualTwangPos);

        private Color CordColorFunc(Vector2 _) => cordLight;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (State == StCharge) {
                BeginYank(CordAnchor());
            }
            if (!Main.dedServ) {
                EyekiteVFX.HitSplat(target.Center, Projectile.velocity);
            }
            if (State == StYank && Projectile.owner == Main.myPlayer) {
                Owner.CWR().ScreenShakeValue = Math.Max(Owner.CWR().ScreenShakeValue, 2.4f);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (State == StYank) {
                modifiers.Knockback += 1.8f;
                modifiers.HitDirectionOverride = Math.Sign(Projectile.velocity.X);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (EffectLoader.KiteSinew?.Value == null || CWRAsset.PerlinNoise?.Value == null) {
                Vector2 anchor = CordAnchor();
                Vector2 attach = Projectile.Center - Projectile.rotation.ToRotationVector2() * 10f;
                EyekiteVFX.BuildCord(cordPoints, anchor, attach, visualTension, visualTwang
                    , visualTwangPos, Projectile.identity, Main.GlobalTimeWrappedHourly);
                EyekiteVFX.DrawCordFallback(cordPoints, visualTension, lightColor);
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float speed = Projectile.velocity.Length();
            //常态漂移不压扁，速度过阈才拉伸
            float stretch = MathHelper.Clamp((speed - 4.5f) * 0.042f, 0f, 0.4f);
            Vector2 scale = new Vector2(1f + stretch, 1f - stretch * 0.32f) * Projectile.scale * MathHelper.Lerp(0.4f, 1f, appear);
            Color color = lightColor * appear;
            //贴图朝右：朝左半平面时垂直翻转采样防倒栽，±0.22 滞回防正上正下逐帧闪
            float cosr = MathF.Cos(Projectile.rotation);
            if (cosr < -0.22f) {
                faceFlipped = true;
            }
            else if (cosr > 0.22f) {
                faceFlipped = false;
            }
            SpriteEffects fx = faceFlipped ? SpriteEffects.FlipVertically : SpriteEffects.None;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, color
                , Projectile.rotation, origin, scale, fx, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Owner == null || !Owner.active) {
                return;
            }
            Vector2 anchor = CordAnchor();
            Vector2 attach = Projectile.Center - Projectile.rotation.ToRotationVector2() * 10f;
            visualTwangPos = State == StYank ? MathHelper.Clamp(1f - Timer / YankTime, 0f, 1f) : 0.55f;
            EyekiteVFX.BuildCord(cordPoints, anchor, attach, visualTension, visualTwang
                , visualTwangPos, Projectile.identity, Main.GlobalTimeWrappedHourly);
            cordLight = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
            EyekiteVFX.DrawCord(ref cordTrail, cordPoints, CordWidthFunc, CordColorFunc
                , visualTension, visualTwang, visualTwangPos, Projectile.identity, cordLight);

            if (dashHeat > 0.05f) {
                EyekiteVFX.FillOldPosTrail(Projectile, dashPoints);
                EyekiteVFX.DrawChargeTrail(ref dashTrail, dashPoints, DashWidthFunc, DashColorFunc, dashHeat * appear);
            }
        }

        private float DashWidthFunc(float f) => 22f * dashHeat * appear * (0.18f + f * 0.82f);

        private Color DashColorFunc(Vector2 tex)
            => Color.Lerp(EyekiteVFX.BloodDeep, EyekiteVFX.Arterial, tex.X) * (dashHeat * appear * (0.2f + tex.X * 0.8f));
    }
}
