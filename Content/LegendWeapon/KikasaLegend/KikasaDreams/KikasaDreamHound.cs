using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams
{
    /// <summary>
    /// 鬼梦恶犬：左键自梦里唤出的猎手。原版狼贴图 + <c>KikasaHound.fx</c> 实体模式
    /// （体成而实、双目常燃）。自玩家脚下黑水跃出，落地追猎最近的敌人，
    /// 近身扑咬，寿命尽头化雾散回梦里。梦境绑定——离开 Dreaming 即溶解。
    /// 各端同推确定性规则，弹幕仅 owner 端生成，伤害在 owner 端结算
    /// </summary>
    internal class KikasaDreamHound : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>撕咬基伤（召唤加成前），验收再调</summary>
        internal const int BiteDamage = 260;

        /// <summary>在场寿命（帧），尽头化雾</summary>
        internal const int LifeFrames = 300;

        private const int DissolveFrames = 26;

        //==================== 状态 ====================

        private const int StateLeap = 0;
        private const int StateRun = 1;
        private const int StateLunge = 2;
        private const int StateDissolve = 3;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>扑咬冷却（帧），Run 状态里递减</summary>
        private ref float LungeCooldown => ref Projectile.ai[2];

        //==================== 运动参数 ====================

        private const float Gravity = 0.32f;
        private const float MaxFall = 11f;
        private const float RunAccel = 0.30f;
        private const float RunMaxSpeed = 9.6f;
        private const float LungeSpeed = 13.5f;
        private const float ChaseRange = 1150f;

        //==================== 本地表现量 ====================

        private int frameIndex;
        private float frameCounter;
        private bool spawnFxDone;

        private Player Owner => Main.player[Projectile.owner];

        private float Seed => Projectile.identity * 0.7391f;

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
        }

        public override void SetDefaults() {
            Projectile.width = 66;
            Projectile.height = 38;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minion = true;
            Projectile.minionSlots = 0f;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
            Projectile.timeLeft = LifeFrames;
        }

        public override bool MinionContactDamage() => true;

        /// <summary>化雾中没有牙</summary>
        public override bool? CanDamage() => State == StateDissolve ? false : null;

        public override bool? CanCutTiles() => false;

        /// <summary>撞墙不死：横向撞停走小跳逻辑，落地竖速归零</summary>
        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.velocity.X != oldVelocity.X) {
                Projectile.velocity.X = 0f;
            }
            if (Projectile.velocity.Y != oldVelocity.Y) {
                Projectile.velocity.Y = 0f;
            }
            return false;
        }

        /// <summary>提前化雾（超编遣散/离开梦境）。owner 端受理并盖章</summary>
        internal void BeginDissolve() {
            if (State == StateDissolve) {
                return;
            }
            State = StateDissolve;
            StateTimer = 0f;
            //化雾要走完整个包络，别被寿命先掐掉
            if (Projectile.timeLeft < DissolveFrames + 4) {
                Projectile.timeLeft = DissolveFrames + 4;
            }
            if (Main.myPlayer == Projectile.owner) {
                Projectile.netUpdate = true;
            }
        }

        //==================== AI ====================

        public override void AI() {
            Player owner = Owner;
            if (owner == null || !owner.active) {
                Projectile.Kill();
                return;
            }

            //伤害随召唤加成逐帧刷新
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(BiteDamage);

            //梦境绑定：owner 端判定离梦即散，其余端跟同步包
            bool authority = Main.myPlayer == Projectile.owner;
            KikasaDomainPlayer domain = owner.GetModPlayer<KikasaDomainPlayer>();
            if (authority && State != StateDissolve
                && (owner.dead || domain.Phase != KikasaDomainPhase.Dreaming)) {
                BeginDissolve();
            }

            //寿命进入化雾窗
            if (State != StateDissolve && Projectile.timeLeft <= DissolveFrames) {
                State = StateDissolve;
                StateTimer = 0f;
            }

            SpawnBurstFx();

            //接地性必须在施加重力前采样：原版碰撞在 AI 之后才把竖速归零，
            //先加重力再看 velocity.Y 会永远读到下坠——犬会卡在跃出态不索敌、帧停在坠落
            bool grounded = Projectile.velocity.Y == 0f;
            float vyIn = Projectile.velocity.Y;

            float gravity = Gravity;
            switch (State) {
                case StateLeap: gravity = UpdateLeap(grounded); break;
                case StateRun: gravity = UpdateRun(grounded); break;
                case StateLunge: gravity = UpdateLunge(); break;
                case StateDissolve: gravity = UpdateDissolve(); break;
            }
            ApplyGravity(gravity);

            if (MathF.Abs(Projectile.velocity.X) > 0.2f) {
                Projectile.spriteDirection = Projectile.velocity.X > 0f ? 1 : -1;
            }
            UpdateFrame(grounded, vyIn);
        }

        //出场那一口黑水，各端首帧自播

        private void SpawnBurstFx() {
            if (spawnFxDone || Main.dedServ) {
                return;
            }
            spawnFxDone = true;
            SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.55f, Volume = 0.5f, MaxInstances = 3 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.78f, Volume = 0.3f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 7; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(-2.4f, -0.6f));
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    Projectile.Center + Main.rand.NextVector2Circular(16f, 10f), vel,
                    new Color(30, 10, 13) * 0.9f, Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(40, 70));
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_KikasaDreamAsh>(
                    Projectile.Center, new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2f, -0.8f)),
                    new Color(214, 84, 34), Main.rand.NextFloat(0.10f, 0.16f))
                    ?.Configure(Main.rand.Next(40, 80), true);
            }
        }

        //各状态返回本帧应施加的重力，统一在 AI 尾部结算

        private float UpdateLeap(bool grounded) {
            StateTimer++;
            //落地即入追猎
            if (StateTimer > 6f && grounded) {
                State = StateRun;
                StateTimer = 0f;
            }
            return Gravity;
        }

        private float UpdateRun(bool grounded) {
            StateTimer++;
            if (LungeCooldown > 0f) {
                LungeCooldown--;
            }

            NPC target = FindTarget();
            if (target == null) {
                //没有猎物：缓步收住，站定等
                Projectile.velocity.X *= 0.92f;
                return Gravity;
            }

            float dx = target.Center.X - Projectile.Center.X;
            float dy = target.Center.Y - Projectile.Center.Y;
            int dir = dx > 0f ? 1 : -1;

            //地面追击
            Projectile.velocity.X = MathHelper.Clamp(
                Projectile.velocity.X + RunAccel * dir, -RunMaxSpeed, RunMaxSpeed);

            //撞墙小跳；猎物在头顶也蹬地跃起
            if (grounded) {
                bool blocked = MathF.Abs(Projectile.velocity.X) < 0.6f && MathF.Abs(dx) > 40f;
                bool preyAbove = dy < -70f && MathF.Abs(dx) < 110f;
                if (blocked) {
                    Projectile.velocity.Y = -7.4f;
                }
                else if (preyAbove) {
                    Projectile.velocity.Y = -9f;
                }
            }

            //近身起扑：一口咬向猎物中心
            if (LungeCooldown <= 0f && MathF.Abs(dx) < 150f && MathF.Abs(dy) < 110f) {
                State = StateLunge;
                StateTimer = 0f;
                LungeCooldown = 48f;
                Vector2 aim = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                Projectile.velocity = aim * LungeSpeed + new Vector2(0f, -1.2f);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 0.34f, MaxInstances = 3 }, Projectile.Center);
                }
                if (Main.myPlayer == Projectile.owner) {
                    Projectile.netUpdate = true;
                }
            }
            return Gravity;
        }

        private float UpdateLunge() {
            StateTimer++;
            if (StateTimer > 26f) {
                State = StateRun;
                StateTimer = 0f;
            }
            //扑击前段低重力咬直线，后段自然坠回
            return StateTimer <= 12f ? 0.10f : Gravity;
        }

        private float UpdateDissolve() {
            StateTimer++;
            Projectile.velocity.X *= 0.9f;

            //化雾：黑红潮气一路散
            if (!Main.dedServ && StateTimer % 3 == 0) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    Projectile.Center + Main.rand.NextVector2Circular(20f, 12f),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-1.4f, -0.5f)),
                    new Color(28, 10, 12) * 0.85f, Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(Main.rand.Next(36, 60));
            }
            if (StateTimer >= DissolveFrames) {
                Projectile.Kill();
            }
            return 0.08f;
        }

        private void ApplyGravity(float gravity) {
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + gravity, MaxFall);
        }

        private NPC FindTarget() {
            NPC best = null;
            float bestDist = ChaseRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit7 with { Pitch = -0.35f, Volume = 0.6f, MaxInstances = 3 }, target.Center);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_KikasaDreamAsh>(
                    target.Center + Main.rand.NextVector2Circular(14f, 10f),
                    new Vector2(Main.rand.NextFloat(-1.8f, 1.8f), Main.rand.NextFloat(-2.6f, -0.4f)),
                    Main.rand.NextBool(3) ? new Color(214, 84, 34) : new Color(40, 12, 14),
                    Main.rand.NextFloat(0.10f, 0.18f))
                    ?.Configure(Main.rand.Next(30, 60), Main.rand.NextBool(3));
            }
        }

        //==================== 帧与绘制 ====================

        //帧逻辑与倒影同源（原版狼 FindFrame）：跃 10、坠 11、立 0、落地 12、跑 3-9。
        //接地性与入帧竖速由 AI 在施加重力前采样喂入，别在这里读加过重力的 velocity

        private void UpdateFrame(bool grounded, float vyIn) {
            float vx = Projectile.velocity.X;

            if (!grounded) {
                frameIndex = vyIn < 0f ? 10 : 11;
                frameCounter = 0f;
            }
            else if (MathF.Abs(vx) < 0.2f) {
                frameIndex = 0;
                frameCounter = 0f;
            }
            else {
                frameCounter += MathF.Abs(vx) * 0.4f;
                if (frameIndex == 10 || frameIndex == 11) {
                    frameIndex = 12;
                    frameCounter = 0f;
                }
                else if (frameCounter > 8f) {
                    frameCounter -= 8f;
                    frameIndex++;
                    if (frameIndex > 9 || frameIndex < 3) {
                        frameIndex = 3;
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.Wolf);
            Texture2D tex = TextureAssets.Npc[NPCID.Wolf]?.Value;
            if (tex == null) {
                return false;
            }

            int frameH = tex.Height / Main.npcFrameCount[NPCID.Wolf];
            //源矩形上下各内缩 1px，配 shader 帧界钳制双通道防渗色
            Rectangle frame = new(0, frameIndex * frameH + 1, tex.Width, frameH - 2);
            float dissolve = State == StateDissolve
                ? MathHelper.Clamp(StateTimer / DissolveFrames, 0f, 1f) : 0f;
            float alpha = 1f - dissolve * 0.4f;
            SpriteBatch sb = Main.spriteBatch;
            SpriteEffects effects = Projectile.spriteDirection > 0
                ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 origin = frame.Size() * 0.5f;

            //扑击残影：高速时拖出几帧墨影
            float speed = Projectile.velocity.Length();
            if (speed > 11f && State != StateDissolve) {
                for (int k = Projectile.oldPos.Length - 1; k >= 1; k--) {
                    Vector2 oldCenter = Projectile.oldPos[k] + Projectile.Size * 0.5f;
                    if (oldCenter == Projectile.Size * 0.5f) {
                        continue;
                    }
                    float fall = 1f - k / (float)Projectile.oldPos.Length;
                    sb.Draw(tex, oldCenter - Main.screenPosition, frame,
                        new Color(22, 8, 11) * (0.4f * fall), 0f, origin, 1f, effects, 0f);
                }
            }

            DrawBody(sb, tex, frame, alpha, dissolve, effects, origin);
            return false;
        }

        private void DrawBody(SpriteBatch sb, Texture2D tex, Rectangle frame,
            float alpha, float dissolve, SpriteEffects effects, Vector2 origin) {

            Effect hound = EffectLoader.KikasaHound?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool shaderOk = hound != null && noise != null;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Color color;
            if (shaderOk) {
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                hound.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                hound.Parameters["uSeed"]?.SetValue(Seed);
                hound.Parameters["uUvRect"]?.SetValue(new Vector4(
                    0f, frame.Y / (float)tex.Height, 1f, frame.Height / (float)tex.Height));
                hound.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                hound.Parameters["uAspect"]?.SetValue(tex.Width / (float)frame.Height);
                hound.Parameters["uFlipH"]?.SetValue(Projectile.spriteDirection > 0 ? 1f : 0f);
                hound.Parameters["uFlipV"]?.SetValue(0f);
                //实体模式：无水线裁剪，体成而实
                hound.Parameters["uMode"]?.SetValue(1f);
                hound.Parameters["uSeamGate"]?.SetValue(0f);
                hound.Parameters["uWobble"]?.SetValue(0.006f);
                hound.Parameters["uEyeGlow"]?.SetValue(1f);
                hound.Parameters["uEyeAnchor"]?.SetValue(KikasaHoundReflection.EyeAnchor);
                hound.Parameters["uDissolve"]?.SetValue(dissolve);
                hound.Parameters["uEdgeTint"]?.SetValue(new Vector3(0.66f, 0.17f, 0.10f));
                hound.CurrentTechnique = hound.Techniques["TechHound"];
                hound.CurrentTechnique.Passes[0].Apply();
                color = new Color(255, 255, 255, (byte)(alpha * 255f));
            }
            else {
                //无着色器回退：近黑剪影
                color = new Color(16, 7, 10) * alpha;
            }

            sb.Draw(tex, Projectile.Center - Main.screenPosition, frame, color,
                0f, origin, 1f, effects, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
