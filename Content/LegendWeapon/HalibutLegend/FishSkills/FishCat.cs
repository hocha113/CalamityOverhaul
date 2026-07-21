using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>猫鱼技能，右键抛射跳跃猫鱼</summary>
    internal class FishCat : FishSkill
    {
        public static SoundStyle Sound => CWRSound.Hajm with {
            MaxInstances = 3,
        };
        public override int UnlockFishID => ItemID.Catfish;
        public override int DefaultCooldown => 60 * (10 - HalibutData.GetDomainLayer() / 2);
        public override int ResearchDuration => 60 * 22;
        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override bool? CanUseItem(Item item, Player player) {
            if (player.altFunctionUse != 2) {
                return null;
            }

            //镜像FishBunny:仅主人客户端掷鱼,防远端客户端幽灵弹
            if (player.whoAmI != Main.myPlayer) {
                return false;
            }

            if (Cooldown > 0) {
                return false;
            }

            item.UseSound = null;
            Vector2 velocity = player.To(Main.MouseWorld).UnitVector() * 12f;
            Vector2 position = player.Center;
            ShootState shootState = player.GetShootState();
            var source = shootState.Source;
            int damage = shootState.WeaponDamage;
            float knockback = shootState.WeaponKnockback;

            SetCooldown();

            int catCount = 3 + HalibutData.GetDomainLayer();

            for (int i = 0; i < catCount; i++) {
                float throwAngle = velocity.ToRotation() + Main.rand.NextFloat(-0.5f, 0.5f);
                float throwSpeed = Main.rand.NextFloat(12f, 18f);
                Vector2 throwVelocity = throwAngle.ToRotationVector2() * throwSpeed;
                throwVelocity.Y -= Main.rand.NextFloat(4f, 7f);

                Projectile.NewProjectile(
                    source,
                    position,
                    throwVelocity,
                    ModContent.ProjectileType<CatfishLeaper>(),
                    (int)(damage * (1.2f + HalibutData.GetDomainLayer() * 0.3f)),
                    knockback * 2.2f,
                    player.whoAmI
                );
            }

            SoundEngine.PlaySound(SoundID.Item1 with {
                Volume = 0.7f,
                Pitch = 0.4f
            }, position);

            SoundEngine.PlaySound(SoundID.Meowmere with {
                Volume = 0.5f,
                Pitch = 0.6f
            }, position);

            FishCatVFX.ThrowBurst(position, velocity);

            return false;
        }
    }

    /// <summary>猫鱼跳跃弹幕，跳跃与扑击状态机</summary>
    internal class CatfishLeaper : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Catfish;

        private enum CatState
        {
            Airborne,
            OnGround,
            Hunting,
            Spinning,
            Exploding
        }

        private CatState State {
            get => (CatState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float CatLife => ref Projectile.ai[1];
        private ref float TargetNPCID => ref Projectile.ai[2];

        private int groundTime = 0;
        private const int MinGroundTime = 5;
        private const int MaxGroundTime = 18;
        private const float JumpForce = 14f;
        private const float HuntJumpForce = 17f;

        private const float Gravity = 0.45f;
        private const float GroundFriction = 0.85f;
        private const float AirResistance = 0.985f;
        private const float MaxFallSpeed = 20f;

        private const float DetectionRange = 700f;
        private const float HuntRange = 450f;

        private float bodyRotation = 0f;
        private float spinRotation = 0f;
        private float spinSpeed = 0f;
        private float squashStretch = 1f;
        private int idleAnimTimer = 0;
        private bool isSpinning = false;

        //离水扑腾与之字乱窜的表演状态,全为客户端视觉量
        private float wriggleRot = 0f;
        private float flopPhase = 0f;
        private float flopAmp = 1f;
        private int facing = 1;
        private int lastHopDir = 0;
        private float nextWarnMeow = 0f;
        private bool initialized = false;

        private const int MaxLifeTime = 540;
        private const int ExplosionRadius = 155;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = MaxLifeTime;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage /= 2f;
            }
        }

        public override void AI() {
            if (!initialized) {
                initialized = true;
                flopAmp = Main.rand.NextFloat(0.6f, 1.1f);
                //出手翻滚:被抛出的鱼在空中挣扎打转
                isSpinning = true;
                spinSpeed = Main.rand.NextFloat(0.28f, 0.5f) * (Main.rand.NextBool() ? 1f : -1f);
            }

            CatLife++;

            if (Math.Abs(Projectile.velocity.X) > 0.4f) {
                facing = Projectile.velocity.X < 0 ? -1 : 1;
            }

            switch (State) {
                case CatState.Airborne:
                    AirbornePhaseAI();
                    break;
                case CatState.OnGround:
                    OnGroundPhaseAI();
                    break;
                case CatState.Hunting:
                    HuntingPhaseAI();
                    break;
                case CatState.Spinning:
                    SpinningPhaseAI();
                    break;
                case CatState.Exploding:
                    ExplodingPhaseAI();
                    break;
            }

            //自旋结束后把残余角平滑归位,避免视觉跳变
            if (!isSpinning && spinRotation != 0f) {
                spinRotation = MathHelper.WrapAngle(spinRotation) * 0.8f;
                if (Math.Abs(spinRotation) < 0.02f) {
                    spinRotation = 0f;
                }
            }
            if (State != CatState.Airborne && State != CatState.Spinning) {
                wriggleRot *= 0.75f;
            }

            UpdateCatAnimation();

            //供TrailingMode缓存旋转,残影链使用
            Projectile.rotation = bodyRotation + spinRotation + wriggleRot;

            float lightIntensity = 0.5f;
            Lighting.AddLight(Projectile.Center,
                1.0f * lightIntensity,
                0.8f * lightIntensity,
                0.6f * lightIntensity);

            if (Projectile.timeLeft <= 35 && State != CatState.Exploding) {
                State = CatState.Exploding;
            }
        }

        private void AirbornePhaseAI() {
            Projectile.velocity.Y += Gravity;
            if (Projectile.velocity.Y > MaxFallSpeed) {
                Projectile.velocity.Y = MaxFallSpeed;
            }

            Projectile.velocity.X *= AirResistance;

            if (Projectile.velocity.LengthSquared() > 9f) {
                bodyRotation = MathHelper.Lerp(bodyRotation, Projectile.velocity.Y * 0.04f, 0.25f);
            }

            //离水扑腾:速度越大挣扎越猛,顶点处收敛
            flopPhase += 0.26f + Math.Min(Projectile.velocity.Length() * 0.012f, 0.14f);
            float vigor = MathHelper.Clamp(Projectile.velocity.Length() * 0.06f, 0.35f, 1f);
            wriggleRot = (float)Math.Sin(flopPhase) * 0.13f * flopAmp * vigor;

            if (isSpinning) {
                spinRotation += spinSpeed;
                spinSpeed *= 0.96f;

                if (Math.Abs(spinSpeed) < 0.1f) {
                    isSpinning = false;
                }
            }

            //高速位移时偶发甩落油滴
            if (CatLife % 9 == 0 && Projectile.velocity.Length() > 7f) {
                FishCatVFX.OilDrip(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f)
                    , -Projectile.velocity * 0.1f, 1);
            }
        }

        private void OnGroundPhaseAI() {
            groundTime++;

            Projectile.velocity.X *= GroundFriction;
            Projectile.velocity.Y = 0;

            bodyRotation = MathHelper.Lerp(bodyRotation, 0, 0.35f);

            NPC target = Projectile.Center.FindClosestNPC(DetectionRange);

            if (target != null) {
                TargetNPCID = target.whoAmI;
                State = CatState.Hunting;
                groundTime = 0;
                return;
            }

            int jumpTime = Main.rand.Next(MinGroundTime, MaxGroundTime);
            if (groundTime >= jumpTime) {
                PerformJump(false);
                groundTime = 0;

                if (Main.rand.NextBool(3)) {
                    InitiateSpin();
                }
            }

            //油滑鱼身闲置滴油
            if (CatLife % 26 == 0) {
                FishCatVFX.OilDrip(Projectile.Bottom + new Vector2(Main.rand.NextFloat(-8f, 8f), -6f)
                    , new Vector2(0f, 0.3f), 1);
            }
        }

        private void HuntingPhaseAI() {
            groundTime++;

            Projectile.velocity.X *= GroundFriction;
            Projectile.velocity.Y = 0;

            if (!IsTargetValid()) {
                State = CatState.OnGround;
                groundTime = 0;
                return;
            }

            NPC target = Main.npc[(int)TargetNPCID];

            float distanceToTarget = Vector2.Distance(Projectile.Center, target.Center);

            if (distanceToTarget > HuntRange) {
                State = CatState.OnGround;
                groundTime = 0;
                return;
            }

            Vector2 toTarget = target.Center - Projectile.Center;

            int huntJumpTime = Main.rand.Next(3, 12);
            if (groundTime >= huntJumpTime) {
                float horizontalSpeed = Math.Abs(toTarget.X) < 80f ? 7f : 11f;
                Projectile.velocity.X = Math.Sign(toTarget.X) * horizontalSpeed;
                Projectile.velocity.Y = -HuntJumpForce;

                if (toTarget.Y < -120f) {
                    Projectile.velocity.Y -= 3f;
                }

                State = CatState.Airborne;
                groundTime = 0;
                lastHopDir = Math.Sign(toTarget.X);

                if (Main.rand.NextBool(2)) {
                    InitiateSpin();
                }

                EmitMeow(0.8f, FishCatVFX.MeowCream, 0.45f, 0.8f);
                FishCatVFX.JumpDust(Projectile.Bottom, Math.Sign(toTarget.X), 1f, true);
            }
        }

        private void SpinningPhaseAI() {
            //旋转态=空中态+自旋,共用空中物理(旧版漏调用导致无重力直飞)
            AirbornePhaseAI();
        }

        private void ExplodingPhaseAI() {
            Projectile.velocity *= 0.88f;

            float progress = MathHelper.Clamp(1f - Projectile.timeLeft / 35f, 0f, 1f);

            //临爆膨胀,抖动频率随进度加快
            float freq = 0.9f + progress * 1.6f;
            squashStretch = 1f + progress * 0.12f + (float)Math.Sin(CatLife * freq) * 0.3f;

            //警告喵:间隔渐短音调渐高,声弧转橙
            if (nextWarnMeow <= 0f) {
                nextWarnMeow = CatLife + 2f;
            }
            if (CatLife >= nextWarnMeow) {
                nextWarnMeow = CatLife + MathHelper.Lerp(11f, 4.5f, progress);
                EmitMeow(0.45f + 0.3f * progress, FishCatVFX.MeowWarn, 0.3f, 0.65f + progress * 0.5f);
            }
        }

        private void PerformJump(bool isHunt) {
            float jumpPower = isHunt ? HuntJumpForce : JumpForce;

            //之字乱窜:偏好反向上次跳,偶发同向保混乱
            int dir = lastHopDir == 0
                ? (Main.rand.NextBool() ? 1 : -1)
                : (Main.rand.NextFloat() < 0.72f ? -lastHopDir : lastHopDir);
            lastHopDir = dir;

            float horizontalSpeed = Main.rand.NextFloat(4f, 9f);

            //碎跳变体:低矮急促的贴地弹,读出抽风感
            bool skitter = Main.rand.NextBool(5);
            if (skitter) {
                jumpPower *= 0.5f;
                horizontalSpeed *= 1.5f;
            }

            Projectile.velocity.X = dir * horizontalSpeed;
            Projectile.velocity.Y = -jumpPower;

            State = CatState.Airborne;

            //音高随起跳动量:碎跳尖促,大跳低沉
            float momentum = MathHelper.Clamp(Projectile.velocity.Length() / 19f, 0f, 1f);
            EmitMeow(0.4f + 0.35f * momentum, FishCatVFX.MeowCream
                , 0.32f, skitter ? 0.95f : MathHelper.Lerp(0.35f, 0.75f, momentum));
            FishCatVFX.JumpDust(Projectile.Bottom, dir, jumpPower / JumpForce, false);
        }

        private void InitiateSpin() {
            isSpinning = true;
            spinSpeed = Main.rand.NextFloat(0.4f, 0.8f) * (Main.rand.NextBool() ? 1 : -1);
            State = CatState.Spinning;
        }

        private bool IsTargetValid() {
            int targetID = (int)TargetNPCID;
            if (targetID < 0 || targetID >= Main.maxNPCs) return false;

            NPC target = Main.npc[targetID];
            return target.active && target.CanBeChasedBy();
        }

        /// <summary>喵一声</summary>
        private void EmitMeow(float strength, Color color, float volume, float pitch) {
            float mouthRot = bodyRotation + spinRotation + wriggleRot;
            Vector2 mouthOff = new Vector2(facing * 15f, -2f).RotatedBy(mouthRot);
            FishCatVFX.MeowArc(Projectile.Center + mouthOff, mouthOff.ToRotation(), strength, color);

            SoundEngine.PlaySound(FishCat.Sound with {
                Volume = volume,
                Pitch = pitch
            }, Projectile.Center);
        }

        private void UpdateCatAnimation() {
            idleAnimTimer++;

            if (State == CatState.Airborne || State == CatState.Spinning) {
                float speedRatio = Math.Abs(Projectile.velocity.Y) / MaxFallSpeed;
                float targetSquash = MathHelper.Lerp(1f, 1.4f, speedRatio);
                squashStretch = MathHelper.Lerp(squashStretch, targetSquash, 0.25f);
            }
            else if (State == CatState.OnGround) {
                if (groundTime < 4) {
                    squashStretch = MathHelper.Lerp(squashStretch, 0.65f, 0.35f);
                }
                else {
                    float breathe = (float)Math.Sin(idleAnimTimer * 0.12f) * 0.06f;
                    squashStretch = MathHelper.Lerp(squashStretch, 1f + breathe, 0.12f);
                }
            }
            else if (State == CatState.Hunting) {
                float tension = (float)Math.Sin(idleAnimTimer * 0.2f) * 0.1f;
                squashStretch = MathHelper.Lerp(squashStretch, 1f + tension, 0.18f);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if ((State == CatState.Airborne || State == CatState.Spinning) && Projectile.velocity.Y == 0) {
                bool hardLanding = Math.Abs(oldVelocity.Y) > 9f;
                State = CatState.OnGround;
                groundTime = 0;
                isSpinning = false;

                SoundEngine.PlaySound(SoundID.Dig with {
                    Volume = 0.35f,
                    Pitch = 0.6f
                }, Projectile.Center);

                FishCatVFX.LandDust(Projectile.Bottom, oldVelocity, hardLanding);

                return false;
            }

            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * 0.65f;
                //撞墙弹回甩两滴油
                FishCatVFX.OilDrip(Projectile.Center
                    , new Vector2(-Math.Sign(oldVelocity.X) * 1.5f, -1f), 2);
            }

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.timeLeft = Math.Min(Projectile.timeLeft, 3);
            State = CatState.Exploding;
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode(ExplosionRadius, default, false);
            FishCatVFX.Explode(Projectile.Center, HalibutData.GetDomainLayer(Main.player[Projectile.owner]), facing);

            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = 0.75f,
                Pitch = 0.25f
            }, Projectile.Center);

            SoundEngine.PlaySound(SoundID.Meowmere with {
                Volume = 0.6f,
                Pitch = 0.3f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D catTex = TextureAssets.Item[ItemID.Catfish].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = catTex.Size() / 2f;

            SpriteEffects effects = facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float drawRot = bodyRotation + spinRotation + wriggleRot;

            Vector2 scale = new Vector2(Projectile.scale / squashStretch, Projectile.scale * squashStretch);

            Color drawColor = Projectile.GetAlpha(lightColor);

            //速度残影链:画在一切之下,加色淡影不压暗背景
            if (Projectile.velocity.LengthSquared() > 36f) {
                for (int i = 3; i >= 1; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;//生成头几帧缓存未填,跳过防止残影闪现在世界原点
                    }
                    Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Color ghostColor = new Color(212, 186, 148, 0) * (0.17f - i * 0.045f);
                    sb.Draw(catTex, ghostPos, null, ghostColor, Projectile.oldRot[i], origin, scale * 0.97f, effects, 0);
                }
            }

            //自旋拖影:位置残影表达不了自旋,用旋转错相重影
            if (isSpinning && Math.Abs(spinSpeed) > 0.12f) {
                for (int i = 1; i <= 2; i++) {
                    Color smear = new Color(222, 196, 158, 0) * (0.3f / i);
                    sb.Draw(catTex, drawPos, null, smear, drawRot - spinSpeed * 2.2f * i, origin, scale, effects, 0);
                }
            }

            //叠层落影:体下三层渐暗,廉价立体感骨架保留
            for (int i = 0; i < 3; i++) {
                float shadowOffset = (3 - i) * 2.5f;
                Vector2 shadowPos = drawPos + new Vector2(0, shadowOffset);
                Color shadowColor = new Color(0, 0, 0, 90) * (1f - i * 0.3f);

                sb.Draw(catTex, shadowPos, null, shadowColor, drawRot, origin, scale * 0.96f, effects, 0);
            }

            if (State == CatState.Exploding) {
                float flash = (float)Math.Sin(CatLife * 1.8f) * 0.5f + 0.5f;
                drawColor = Color.Lerp(drawColor, new Color(255, 150, 50), flash * 0.6f);
            }

            sb.Draw(catTex, drawPos, null, drawColor, drawRot, origin, scale, effects, 0);

            DrawOilSheen(sb, catTex, drawPos, scale, effects, drawRot);

            //状态热度皮层:加色小幅度,替代旧的同贴图不透明叠壳
            if (State == CatState.Hunting) {
                float excite = 0.16f + 0.08f * (float)Math.Sin(idleAnimTimer * 0.35f);
                sb.Draw(catTex, drawPos, null, new Color(255, 212, 150, 0) * excite, drawRot, origin, scale * 1.03f, effects, 0);
            }
            else if (State == CatState.Exploding) {
                float progress = MathHelper.Clamp(1f - Projectile.timeLeft / 35f, 0f, 1f);
                float flash = (float)Math.Sin(CatLife * (1.2f + progress * 1.3f)) * 0.5f + 0.5f;
                sb.Draw(catTex, drawPos, null, new Color(255, 138, 58, 0) * (0.2f + 0.4f * flash * progress)
                    , drawRot, origin, scale * 1.04f, effects, 0);
            }

            return false;
        }

        /// <summary>油光缓扫</summary>
        private void DrawOilSheen(SpriteBatch sb, Texture2D tex, Vector2 drawPos, Vector2 scale, SpriteEffects effects, float rot) {
            float sweep = (Main.GlobalTimeWrappedHourly * 0.5f + Projectile.whoAmI * 0.373f) % 1f;
            int bandW = Math.Max(2, tex.Width / 7);
            int maxX = tex.Width - bandW;
            int bandX = (int)(sweep * maxX);
            bool flipped = effects == SpriteEffects.FlipHorizontally;

            for (int i = -1; i <= 1; i++) {
                int x = (int)MathHelper.Clamp(bandX + i * bandW, 0, maxX);
                Rectangle src = new Rectangle(x, 0, bandW, tex.Height);
                //切片原点对回贴图中心;水平翻转时原点在切片内镜像
                float originX = tex.Width * 0.5f - x;
                if (flipped) {
                    originX = bandW - originX;
                }
                float strength = i == 0 ? 0.30f : 0.13f;
                Color sheen = new Color(255, 242, 214, 0) * strength;
                sb.Draw(tex, drawPos, src, sheen, rot, new Vector2(originX, tex.Height * 0.5f), scale, effects, 0);
            }
        }
    }
}
