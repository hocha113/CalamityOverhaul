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
    /// 回旋镖族镖弹基类：三相轨迹状态机。<br/>
    /// 去程持续减速（OutDrag），悬停急停蓄势（自旋攀升），回程向玩家持续加速。<br/>
    /// 跨端契约：ai[0]=相位 ai[1]=相位计时 ai[2]=武器私用槽，全走 netUpdate 过线；
    /// 计时类转相各端确定性推进，命中类转相 owner 权威 + netUpdate 校正
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

        //==================== 三相参数面 ====================

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
        /// <summary>冲刺速度（回弹/折射/折跳等机制借冲刺相位续飞）</summary>
        protected virtual float DashSpeed => 19f;
        /// <summary>冲刺时长帧</summary>
        protected virtual int DashTime => 16;
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

        /// <summary>当前自旋角速度（各端确定性推进）</summary>
        protected float spinSpeed;
        /// <summary>自旋方向，由初速 X 符号决定</summary>
        protected int spinDir = 1;

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

        //==================== 碰撞与命中 ====================

        public sealed override bool OnTileCollide(Vector2 oldVelocity) => HandleTileCollide(oldVelocity);

        /// <summary>撞地默认：叮一声折回；重坠/折射类子类全权覆写</summary>
        protected virtual bool HandleTileCollide(Vector2 oldVelocity) {
            if (!BounceOffTiles) {
                return true;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = 0.3f }, Projectile.Center);
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
            }
            if (HoverOnFirstHit && Phase == PhaseOut) {
                //命中滞空：去程撞上目标就停在它身边打转
                EnterPhase(PhaseHover, Owner);
            }
        }

        /// <summary>命中骑士钩（owner 端；叠层/折射/处决逻辑放这）</summary>
        protected virtual void OnHitEffects(NPC target, NPC.HitInfo hit, int damageDone) { }

        /// <summary>回手：接住瞬间（格挡窗放这），随后消亡</summary>
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
        /// <summary>进相瞬间（各端；音效自守 !VaultUtils.isServer）</summary>
        protected virtual void OnEnterPhase(int phase, Player owner) { }

        //==================== 绘制：原版物品贴图本体一笔 ====================

        public sealed override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Main.instance.LoadItem(SourceItemID);
            Texture2D tex = TextureAssets.Item[SourceItemID].Value;
            Vector2 origin = tex.Size() / 2f;
            float scale = Projectile.scale * BodyScale;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            PreDrawUnder(sb, drawPos, lightColor);
            sb.Draw(tex, drawPos, null, lightColor, Projectile.rotation, origin, scale, SpriteEffects.None, 0);
            return false;
        }

        /// <summary>本体之下的结构层（船锚链条）</summary>
        protected virtual void PreDrawUnder(SpriteBatch sb, Vector2 drawPos, Color lightColor) { }
    }
}
