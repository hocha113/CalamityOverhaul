using CalamityOverhaul.Content.NPCs.BloomsandSerpents;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Summon.BloomCallers
{
    /// <summary>
    /// 荒花幼蕾。ai0 状态 ai1 计时 ai2 目标 whoAmI（-1 无）。
    /// 悬浮跟随，前倾投掷垂刺（<see cref="BloomCallerThorn"/>），
    /// 每第三次攻击改为冲身撞击，撞后自旋绽放一圈花瓣。
    /// 单帧贴图，生命感靠正弦浮动、蓄势压缩与冲刺残影补足
    /// </summary>
    internal class BloomCallerSprout : BssModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Summon + "BloomCallerSprout";

        private const int StIdle = 0;
        private const int StWind = 1;
        private const int StDash = 2;
        private const int StNova = 3;
        private const int StRecover = 4;

        private const int WindTime = 14;
        private const int DashTime = 20;
        private const int NovaTime = 12;
        private const int RecoverTime = 14;

        private const float RestRadius = 88f;
        private const float IdleLeash = 300f;
        private const float DashLeash = 640f;
        private const float DetectRange = 520f;
        /// <summary>攒满多少次投掷后换一次冲身绽放</summary>
        private const int ThrowsPerDash = 2;

        private float State { get => Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private float Timer { get => Projectile.ai[1]; set => Projectile.ai[1] = value; }
        private float TargetIndex { get => Projectile.ai[2]; set => Projectile.ai[2] = value; }

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>已攒的投掷数，owner 侧决策，无需入网</summary>
        private int throws;
        /// <summary>朝向符号（1 右 -1 左），绘制侧平滑</summary>
        private float facingSign = 1f;
        /// <summary>身体倾角，随速度与蓄势变化</summary>
        private float lean;
        /// <summary>绽放自旋角，仅演出</summary>
        private float spinRot;
        /// <summary>冲刺热度，驱动残影</summary>
        private float dashHeat;
        private float appear;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.MinionSacrificable[Type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Type] = true;
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 40;
            Projectile.minion = true;
            Projectile.minionSlots = 1f;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.netImportant = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 18000;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 16;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.ai[2] = -1f;
        }

        public override bool? CanDamage() => State == StDash;

        public override bool MinionContactDamage() => State == StDash;

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }
            Owner.AddBuff(ModContent.BuffType<BloomCallerBuff>(), 2);
            Projectile.timeLeft = 2;

            if (appear < 1f) {
                appear = MathHelper.Clamp(appear + 0.07f, 0f, 1f);
            }

            if (Projectile.Distance(Owner.Center) > 2400f) {
                Projectile.Center = RestPosition();
                Projectile.velocity *= 0.1f;
                Enter(StIdle);
            }

            switch ((int)State) {
                case StWind:
                    WindAI();
                    break;
                case StDash:
                    DashAI();
                    break;
                case StNova:
                    NovaAI();
                    break;
                case StRecover:
                    RecoverAI();
                    break;
                default:
                    IdleAI();
                    break;
            }

            float leash = State is StDash or StNova ? DashLeash : IdleLeash;
            ClampLeash(leash);
            UpdateVisual();
            Lighting.AddLight(Projectile.Center, 0.14f, 0.2f, 0.05f);
        }

        #region 状态
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
            float t = Main.GameUpdateCount * 0.012f + Projectile.identity * 0.9f;
            float ang = MathHelper.TwoPi * slot / total + t;
            float rad = RestRadius + (Projectile.identity % 3) * 10f + MathF.Sin(t * 1.4f) * 10f;
            Vector2 orbit = ang.ToRotationVector2() * rad;
            //压扁成头顶弧带，浮在玩家上方
            orbit.Y = orbit.Y * 0.4f - 52f * Owner.gravDir;
            return Owner.Center + orbit;
        }

        private void IdleAI() {
            Vector2 rest = RestPosition();
            float wt = Main.GameUpdateCount * 0.03f + Projectile.identity * 0.7f;
            Vector2 bob = new(MathF.Sin(wt) * 6f, MathF.Sin(wt * 1.6f) * 8f);

            Vector2 want = rest + bob;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, (want - Projectile.Center) * 0.08f, 0.15f);
            Projectile.velocity *= 0.96f;

            if (Projectile.owner == Main.myPlayer && Main.GameUpdateCount % 8 == (uint)(Projectile.identity % 8)) {
                NPC target = PickTarget();
                if (target != null) {
                    TargetIndex = target.whoAmI;
                    Enter(StWind);
                    Projectile.netUpdate = true;
                }
            }
        }

        /// <summary>蓄势：压身盯准，到点投掷垂刺或转入冲身</summary>
        private void WindAI() {
            Timer++;
            NPC target = ResolveTarget();
            if (target == null) {
                Enter(StRecover);
                return;
            }
            Projectile.velocity *= 0.85f;

            if (Timer < WindTime) {
                return;
            }

            //攒满投掷数换冲身，否则甩出垂刺
            if (throws >= ThrowsPerDash) {
                Vector2 aim = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                Projectile.velocity = aim * 15f;
                dashHeat = 1f;
                Enter(StDash);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Grass with { Pitch = -0.3f, Volume = 0.7f }, Projectile.Center);
                }
            }
            else {
                if (Projectile.IsOwnedByLocalPlayer()) {
                    //垂刺走抛物线：直指提前量再抬一点仰角
                    Vector2 aim = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    Vector2 vel = aim * 11.5f + new Vector2(0f, -2.2f);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                        ModContent.ProjectileType<BloomCallerThorn>(), Projectile.damage, Projectile.knockBack,
                        Projectile.owner);
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item17 with { Pitch = 0.2f, Volume = 0.6f }, Projectile.Center);
                }
                throws++;
                Enter(StRecover);
            }
            if (Projectile.owner == Main.myPlayer) {
                Projectile.netUpdate = true;
            }
        }

        private void DashAI() {
            Timer++;
            NPC target = ResolveTarget();
            Vector2 aim = target != null ? target.Center : Projectile.Center + Projectile.velocity;
            Vector2 accel = (aim - Projectile.Center).SafeNormalize(Vector2.UnitX) * 2f;
            float gain = Timer < 4f ? 1.25f : 0.98f;
            Projectile.velocity = (Projectile.velocity + accel) * gain;
            if (Projectile.velocity.Length() > 21f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 21f;
            }
            dashHeat = MathHelper.Clamp(dashHeat + 0.15f, 0f, 1f);

            if (!Main.dedServ && Timer % 2 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center - Projectile.velocity * 0.4f,
                    DustID.JunglePlants, -Projectile.velocity * 0.08f, 130, default, 0.9f);
                d.noGravity = true;
            }

            if (Timer >= DashTime || target == null) {
                BeginNova();
            }
        }

        private void BeginNova() {
            throws = 0;
            Projectile.velocity *= 0.4f;
            Enter(StNova);
            if (Projectile.owner == Main.myPlayer) {
                Projectile.netUpdate = true;
            }
        }

        /// <summary>自旋绽放：转个圈把花瓣抖出去</summary>
        private void NovaAI() {
            Timer++;
            Projectile.velocity *= 0.86f;
            spinRot += 0.55f * (facingSign >= 0f ? 1f : -1f);

            if (Timer == NovaTime / 2) {
                SoundEngine.PlaySound(SoundID.Grass with { Pitch = 0.3f, Volume = 0.85f }, Projectile.Center);
                BloomArsenal.PetalRing(Projectile, Projectile.Center, 6,
                    (int)(Projectile.damage * 0.9f), 2f, 5f);
                if (!Main.dedServ) {
                    for (int i = 0; i < 5; i++) {
                        BssVfx.PetalDrift(Projectile.Center, Main.rand.NextVector2Circular(1.4f, 1f), 0.7f);
                    }
                }
            }

            if (Timer >= NovaTime) {
                spinRot = 0f;
                Enter(StRecover);
            }
        }

        private void RecoverAI() {
            Timer++;
            Vector2 rest = RestPosition();
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, (rest - Projectile.Center) * 0.09f, 0.16f);
            Projectile.velocity *= 0.93f;
            if (Timer >= RecoverTime) {
                Enter(StIdle);
            }
        }

        private void ClampLeash(float maxLen) {
            Vector2 delta = Projectile.Center - Owner.Center;
            float dist = delta.Length();
            if (dist > maxLen) {
                Projectile.Center = Owner.Center + delta.SafeNormalize(Vector2.Zero) * maxLen;
                Vector2 inward = (Owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                if (Vector2.Dot(Projectile.velocity, inward) < 0f) {
                    Projectile.velocity = Vector2.Reflect(Projectile.velocity, inward) * 0.35f + inward * 4f;
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
                if (tagged.CanBeChasedBy(Projectile) && tagged.Distance(Owner.Center) < DetectRange + 200f) {
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
        #endregion

        #region 命中与视觉
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //撞上就地绽放
            if (State == StDash) {
                BeginNova();
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (State == StDash) {
                modifiers.Knockback += 1f;
                modifiers.HitDirectionOverride = Math.Sign(Projectile.velocity.X);
            }
        }

        private void UpdateVisual() {
            //朝向：盯目标时朝目标，其余朝速度
            NPC target = State is StWind or StDash ? ResolveTarget() : null;
            float dx = target != null ? target.Center.X - Projectile.Center.X : Projectile.velocity.X;
            if (MathF.Abs(dx) > 0.4f) {
                facingSign = MathHelper.Lerp(facingSign, MathF.Sign(dx), 0.2f);
            }

            //倾角：蓄势前倾，飞行随速度带一点
            float want = State switch {
                StWind => 0.24f * MathF.Sign(facingSign),
                StDash => MathHelper.Clamp(Projectile.velocity.X * 0.03f, -0.5f, 0.5f),
                _ => MathHelper.Clamp(Projectile.velocity.X * 0.02f, -0.3f, 0.3f),
            };
            lean = MathHelper.Lerp(lean, want, 0.2f);

            dashHeat *= State is StDash ? 1f : 0.88f;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Color col = lightColor * appear;
            float drawRot = State == StNova ? spinRot : lean;
            SpriteEffects flip = facingSign >= 0f ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            //蓄势压身，绽放鼓开
            Vector2 scale = new Vector2(1f, 1f) * Projectile.scale * MathHelper.Lerp(0.5f, 1f, appear);
            if (State == StWind) {
                float w = MathHelper.Clamp(Timer / WindTime, 0f, 1f);
                scale = new Vector2(1f + w * 0.1f, 1f - w * 0.14f) * Projectile.scale;
            }
            else if (State == StNova) {
                float n = MathF.Sin(MathHelper.Clamp(Timer / NovaTime, 0f, 1f) * MathHelper.Pi);
                scale *= 1f + n * 0.12f;
            }

            //冲刺残影
            if (dashHeat > 0.1f) {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float t = 1f - i / (float)Projectile.oldPos.Length;
                    Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Main.EntitySpriteDraw(tex, pos, null, col * (0.3f * t * dashHeat), drawRot,
                        tex.Size() * 0.5f, scale, flip, 0);
                }
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, col,
                drawRot, tex.Size() * 0.5f, scale, flip, 0);
            return false;
        }
        #endregion
    }

    /// <summary>
    /// 荒花垂刺：幼蕾甩出的下坠棘刺，抛物线坠向目标。
    /// 贴图叶顶在上尖端朝下，绘制时按速度方向对齐
    /// </summary>
    internal class BloomCallerThorn : BssModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Summon + "BloomCallerThorn";

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.DamageType = DamageClass.Summon;
        }

        public override void AI() {
            Projectile.velocity.Y = MathHelper.Clamp(Projectile.velocity.Y + 0.2f, -20f, 13f);
            //贴图尖端朝 +Y（下），速度向即尖向
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;

            if (!Main.dedServ && Main.rand.NextBool(7)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JunglePlants,
                    -Projectile.velocity * 0.04f, 140, default, 0.7f);
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JunglePlants,
                    Main.rand.NextVector2Circular(1.5f, 1.5f), 120, default, Main.rand.NextFloat(0.6f, 0.9f));
                d.noGravity = true;
            }
        }
    }
}
