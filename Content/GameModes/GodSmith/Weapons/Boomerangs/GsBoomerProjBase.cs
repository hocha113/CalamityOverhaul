using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 回旋镖族镖弹基类：三相轨迹状态机 + 旋转残影绘制。<br/>
    /// 去程持续减速（OutDrag），悬停急停蓄势（自旋攀升、辉光渐亮），回程向玩家持续加速；
    /// 悬停/回程期 owner 按右键下达改向冲刺（每掷 RedirectCharges 次）。<br/>
    /// 跨端契约：ai[0]=相位 ai[1]=相位计时 ai[2]=武器私用槽，全走 netUpdate 过线；
    /// 计时类转相各端确定性推进，命中/输入类转相 owner 权威 + netUpdate 校正；
    /// 输入只在 IsOwnedByLocalPlayer() 读，粒子守 !VaultUtils.isServer，绘制不掷 Main.rand
    /// </summary>
    internal abstract class GsBoomerProjBase : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override LocalizedText DisplayName
            => Language.GetText("ItemName." + ItemID.Search.GetName(SourceItemID));

        //==================== 相位常量 ====================

        protected const int PhaseOut = 0;
        protected const int PhaseHover = 1;
        protected const int PhaseReturn = 2;
        protected const int PhaseDash = 3;

        //==================== 子类必填 ====================

        /// <summary>对应的原版物品 ID（贴图与显示名来源）</summary>
        internal abstract int SourceItemID { get; }

        /// <summary>主题辉光色（材质身份的颜色面）</summary>
        protected abstract Color GlowColor { get; }

        //==================== 三相参数面 ====================

        protected virtual Color TrailColor => GlowColor;
        protected virtual Color SmearColor => GlowColor;

        /// <summary>去程帧数</summary>
        protected virtual int OutTime => 26;
        /// <summary>去程每帧速度衰减（&lt;1，禁匀速直飞）</summary>
        protected virtual float OutDrag => 0.965f;
        /// <summary>悬停帧数</summary>
        protected virtual int HoverTime => 18;
        /// <summary>悬停急减速系数</summary>
        protected virtual float HoverDrag => 0.80f;
        /// <summary>回程起始速度</summary>
        protected virtual float ReturnBaseSpeed => 6f;
        /// <summary>回程每帧加速度</summary>
        protected virtual float ReturnAccel => 0.55f;
        /// <summary>回程速度上限</summary>
        protected virtual float ReturnMaxSpeed => 17f;
        /// <summary>指令冲刺速度</summary>
        protected virtual float DashSpeed => 19f;
        /// <summary>指令冲刺时长帧</summary>
        protected virtual int DashTime => 16;
        /// <summary>每掷可用指令次数</summary>
        protected virtual int RedirectCharges => 1;
        /// <summary>去程也允许下达指令</summary>
        protected virtual bool AllowCommandInOut => false;
        /// <summary>去程首次命中即转悬停（命中滞空读法）</summary>
        protected virtual bool HoverOnFirstHit => true;
        /// <summary>撞墙折回（false 交由子类 HandleTileCollide 全权）</summary>
        protected virtual bool BounceOffTiles => true;
        /// <summary>判定箱边长 px</summary>
        protected virtual int HitboxSize => 26;
        /// <summary>同目标再判间隔帧</summary>
        protected virtual int HitCooldown => 14;
        /// <summary>本体贴图缩放</summary>
        protected virtual float BodyScale => 1f;
        /// <summary>自旋倍率</summary>
        protected virtual float SpinRateMul => 1f;
        /// <summary>残影基础透明度</summary>
        protected virtual float GhostBaseAlpha => 0.22f;
        /// <summary>命中音（族默认金属轻鸣，木质武器覆写）</summary>
        protected virtual SoundStyle HitSound => SoundID.Tink with { Volume = 0.5f, Pitch = 0.2f };

        //==================== 状态 ====================

        /// <summary>当前相位（过线）</summary>
        protected int Phase {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }

        /// <summary>相位内计时（过线）</summary>
        protected int PhaseTimer {
            get => (int)Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }

        protected Player Owner => Main.player[Projectile.owner];

        /// <summary>当前自旋角速度（各端确定性推进，绘制涂抹用）</summary>
        protected float spinSpeed;
        /// <summary>自旋方向，由初速 X 符号决定</summary>
        protected int spinDir = 1;
        /// <summary>已用指令次数（owner 权威本地量）</summary>
        protected int redirectsUsed;
        private bool prevRight;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 9;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public sealed override void SetDefaults() {
            Projectile.width = Projectile.height = HitboxSize;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = HitCooldown;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 900;
            SetBoomerDefaults();
        }

        /// <summary>子类追加默认值（尺寸之外的个性项）</summary>
        protected virtual void SetBoomerDefaults() { }

        //==================== 三相状态机 ====================

        public sealed override void AI() {
            Player owner = Owner;
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                spinDir = Projectile.velocity.X >= 0f ? 1 : -1;
                spinSpeed = 0.4f * SpinRateMul;
                OnSpawnInit(owner);
            }

            PhaseTimer++;
            UpdateSpin();
            ReadOwnerCommand(owner);

            switch (Phase) {
                case PhaseOut:
                    Projectile.velocity *= OutDrag;
                    OnOutTick(owner);
                    if (OutFinished(owner)) {
                        EnterPhase(PhaseHover, owner);
                    }
                    break;
                case PhaseHover:
                    Projectile.velocity *= HoverDrag;
                    //轻微浮沉，whoAmI 相位错拍
                    Projectile.velocity.Y += MathF.Sin((PhaseTimer + (Projectile.whoAmI * 7)) * 0.35f) * 0.05f;
                    OnHoverTick(owner);
                    if (PhaseTimer >= HoverTime) {
                        EnterPhase(PhaseAfterHover, owner);
                    }
                    break;
                case PhaseReturn: {
                    float speed = MathF.Min(ReturnMaxSpeed, ReturnBaseSpeed + (PhaseTimer * ReturnAccel));
                    Vector2 toOwner = owner.Center - Projectile.Center;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                        toOwner.SafeNormalize(Vector2.UnitY) * speed, 0.38f);
                    OnReturnTick(owner);
                    if (toOwner.Length() < 30f) {
                        CatchBack(owner);
                        return;
                    }
                    break;
                }
                case PhaseDash:
                    OnDashTick(owner);
                    if (PhaseTimer >= DashTime) {
                        EnterPhase(PhaseReturn, owner);
                    }
                    break;
            }

            if (!VaultUtils.isServer) {
                FlightFX(owner);
            }
            Lighting.AddLight(Projectile.Center, GlowColor.ToVector3() * 0.35f);

            //超距保险：拉得太远直接消散（回程速度上限追不上传送等极端位移）
            if (Projectile.Distance(owner.Center) > 2600f) {
                Projectile.Kill();
            }
        }

        /// <summary>相位切换统一入口；回程关地形碰撞保证接得回</summary>
        protected void EnterPhase(int phase, Player owner) {
            Phase = phase;
            PhaseTimer = 0;
            if (phase == PhaseReturn) {
                Projectile.tileCollide = false;
            }
            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.netUpdate = true;
            }
            OnEnterPhase(phase, owner);
        }

        /// <summary>去程结束判定：时限到或速度衰竭</summary>
        protected virtual bool OutFinished(Player owner)
            => PhaseTimer >= OutTime || Projectile.velocity.Length() < 2.5f;

        /// <summary>悬停期满后的去向（重坠类武器改成 PhaseDash 接坠击）</summary>
        protected virtual int PhaseAfterHover => PhaseReturn;

        private void UpdateSpin() {
            float target = SpinTarget(Phase) * SpinRateMul;
            spinSpeed = MathHelper.Lerp(spinSpeed, target, 0.2f);
            Projectile.rotation += spinSpeed * spinDir;
        }

        /// <summary>各相位自旋目标角速度；悬停期攀升是「蓄势」的第一读法</summary>
        protected virtual float SpinTarget(int phase) => phase switch {
            PhaseHover => 0.42f + (0.5f * MathHelper.Clamp(PhaseTimer / (float)HoverTime, 0f, 1f)),
            PhaseDash => 0.95f,
            PhaseReturn => 0.6f,
            _ => 0.42f,
        };

        //==================== 指令改向 ====================

        private void ReadOwnerCommand(Player owner) {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            bool right = Main.mouseRight && !owner.mouseInterface;
            bool edge = right && !prevRight;
            prevRight = right;
            if (!edge || owner.HeldItem == null || owner.HeldItem.type != SourceItemID) {
                return;
            }
            if (redirectsUsed >= RedirectCharges) {
                return;
            }
            bool phaseOk = Phase == PhaseHover || Phase == PhaseReturn || (AllowCommandInOut && Phase == PhaseOut);
            if (!phaseOk) {
                return;
            }
            redirectsUsed++;
            CommandDash(Main.MouseWorld, owner);
        }

        /// <summary>下达改向冲刺（owner 端调用，netUpdate 带 ai 与速度过线）</summary>
        protected virtual void CommandDash(Vector2 aim, Player owner) {
            Projectile.velocity = (aim - Projectile.Center).SafeNormalize(Vector2.UnitX * spinDir) * DashSpeed;
            Projectile.tileCollide = true;
            EnterPhase(PhaseDash, owner);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.5f, Pitch = 0.25f }, Projectile.Center);
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GlowColor, 0.4f)?.Configure(10, 0.9f);
            }
            OnCommandFX(owner);
        }

        /// <summary>改向瞬间的个性演出（owner 端）</summary>
        protected virtual void OnCommandFX(Player owner) { }

        //==================== 碰撞与命中 ====================

        public sealed override bool OnTileCollide(Vector2 oldVelocity) => HandleTileCollide(oldVelocity);

        /// <summary>撞地默认：叮一声折回；重坠/折射类子类全权覆写</summary>
        protected virtual bool HandleTileCollide(Vector2 oldVelocity) {
            if (!BounceOffTiles) {
                return true;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = 0.3f }, Projectile.Center);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        -oldVelocity.RotatedByRandom(0.6) * Main.rand.NextFloat(0.1f, 0.25f),
                        TrailColor, Main.rand.NextFloat(0.3f, 0.45f))?.Configure(true, Main.rand.Next(10, 16));
                }
            }
            //弹开一点防卡角
            if (Projectile.velocity.X != oldVelocity.X) {
                Projectile.velocity.X = -oldVelocity.X * 0.35f;
            }
            if (Projectile.velocity.Y != oldVelocity.Y) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.35f;
            }
            if (Phase == PhaseOut || Phase == PhaseDash) {
                EnterPhase(PhaseReturn, Owner);
            }
            return false;
        }

        public sealed override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            OnHitEffects(target, hit, damageDone);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(HitSound, target.Center);
                HitBurstFX(target, hit);
            }
            if (HoverOnFirstHit && Phase == PhaseOut) {
                //命中滞空：去程撞上目标就停在它身边打转
                EnterPhase(PhaseHover, Owner);
            }
        }

        /// <summary>命中骑士钩（owner 端；叠层/折射/处决逻辑放这）</summary>
        protected virtual void OnHitEffects(NPC target, NPC.HitInfo hit, int damageDone) { }

        /// <summary>命中粒子默认：主题色火星迸溅 + 点光</summary>
        protected virtual void HitBurstFX(NPC target, NPC.HitInfo hit) {
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, GlowColor, 0.22f)?.Configure(9, 0.8f);
            for (int i = 0; i < 4; i++) {
                Vector2 vel = Projectile.velocity.SafeNormalize(Vector2.UnitX)
                    .RotatedByRandom(0.8) * Main.rand.NextFloat(2.5f, 6f);
                PRTLoader.NewParticle<PRT_Spark>(target.Center, vel, GlowColor,
                    Main.rand.NextFloat(0.35f, 0.55f))?.Configure(true, Main.rand.Next(12, 18));
            }
        }

        /// <summary>回手：接住瞬间（格挡窗/收尾演出放这），随后消亡</summary>
        private void CatchBack(Player owner) {
            OnCatch(owner);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.6f }, owner.Center);
            }
            Projectile.Kill();
        }

        protected virtual void OnCatch(Player owner) { }

        //==================== 个性钩子面 ====================

        /// <summary>出生初始化（各端首帧）</summary>
        protected virtual void OnSpawnInit(Player owner) { }
        /// <summary>去程每帧</summary>
        protected virtual void OnOutTick(Player owner) { }
        /// <summary>悬停每帧</summary>
        protected virtual void OnHoverTick(Player owner) { }
        /// <summary>回程每帧</summary>
        protected virtual void OnReturnTick(Player owner) { }
        /// <summary>冲刺每帧</summary>
        protected virtual void OnDashTick(Player owner) { }
        /// <summary>进相瞬间（各端；音效粒子自守 !VaultUtils.isServer）</summary>
        protected virtual void OnEnterPhase(int phase, Player owner) { }

        /// <summary>飞行粒子默认：低频点缀，悬停期加密（蓄势第二读法）</summary>
        protected virtual void FlightFX(Player owner) {
            int interval = Phase switch { PhaseHover => 3, PhaseDash => 2, PhaseReturn => 4, _ => 5 };
            if (PhaseTimer % interval == 0) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center - (Projectile.velocity * 0.4f),
                    -Projectile.velocity * 0.05f, TrailColor, 0.12f)?.Configure(12, 0.55f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Circular(2.5f, 2.5f), TrailColor,
                    Main.rand.NextFloat(0.3f, 0.45f))?.Configure(true, Main.rand.Next(8, 14));
            }
        }

        //==================== 绘制：残影 + 旋转涂抹 + 本体 + 蓄势辉光 ====================

        public sealed override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Main.instance.LoadItem(SourceItemID);
            Texture2D tex = TextureAssets.Item[SourceItemID].Value;
            Vector2 origin = tex.Size() / 2f;
            float scale = Projectile.scale * BodyScale;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            PreDrawUnder(sb, drawPos, lightColor);

            //位置残影：越旧越淡越小
            int len = ProjectileID.Sets.TrailCacheLength[Type];
            for (int i = len - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float k = 1f - (i / (float)len);
                Color gc = TrailColor * (GhostBaseAlpha * k * k);
                gc.A = 0;
                Vector2 gpos = Projectile.oldPos[i] + (Projectile.Size / 2f) - Main.screenPosition;
                sb.Draw(tex, gpos, null, gc, Projectile.oldRot[i], origin,
                    scale * (0.9f + (0.1f * k)), SpriteEffects.None, 0);
            }

            //旋转涂抹：亮度跟角速度走，双瓣对称
            float spinNorm = MathF.Min(1f, MathF.Abs(spinSpeed) / 0.9f);
            Texture2D smear = CWRAsset.SemiCircularSmear?.Value;
            if (spinNorm > 0.15f && smear != null) {
                float smearScale = tex.Size().Length() * scale / smear.Width * 1.5f;
                Color sc = SmearColor * (0.34f * spinNorm);
                sc.A = 0;
                sb.Draw(smear, drawPos, null, sc, Projectile.rotation,
                    smear.Size() / 2f, smearScale, SpriteEffects.None, 0);
                Color sc2 = sc * 0.55f;
                sc2.A = 0;
                sb.Draw(smear, drawPos, null, sc2, Projectile.rotation + MathHelper.Pi,
                    smear.Size() / 2f, smearScale * 0.92f, SpriteEffects.None, 0);
            }

            //本体（原版物品贴图只当本体垫底，残影辉光全是自绘层）
            sb.Draw(tex, drawPos, null, lightColor, Projectile.rotation, origin, scale, SpriteEffects.None, 0);

            //蓄势辉光：悬停攀升、冲刺满亮、回程余亮
            float charge = Phase switch {
                PhaseHover => MathHelper.Clamp(PhaseTimer / (float)HoverTime, 0f, 1f),
                PhaseDash => 1f,
                PhaseReturn => 0.35f,
                _ => 0.12f,
            };
            Color glow = GlowColor * (0.14f + (0.42f * charge));
            glow.A = 0;
            sb.Draw(tex, drawPos, null, glow, Projectile.rotation, origin, scale * 1.06f, SpriteEffects.None, 0);

            PostDrawLayers(sb, drawPos, lightColor);
            return false;
        }

        /// <summary>本体之下的自绘层（链条/领域等）</summary>
        protected virtual void PreDrawUnder(SpriteBatch sb, Vector2 drawPos, Color lightColor) { }

        /// <summary>本体之上的自绘层（光环/印记等）</summary>
        protected virtual void PostDrawLayers(SpriteBatch sb, Vector2 drawPos, Color lightColor) { }
    }
}
