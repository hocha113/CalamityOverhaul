using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Flamethrowers
{
    /// <summary>
    /// 喷火器族共享层：喷射器手持基类（Flamethrower / Elf Melter 共用）+ 燃烧流体焰弹 + 残焰补丁。
    /// 持枪姿态帮手 <see cref="GsGunPose"/> 归枪械族（<c>Weapons/Guns/GsGunPoseShared.cs</c>），本族只读引用；
    /// 手持接管一律 <see cref="BaseHeldProj"/> 手写姿态，禁止继承 BaseHeldGun
    /// （其 TargetID 扫描会把原版物品永久写进 ItemIsGun 表，违反模式关闭零足迹）。<br/>
    /// 宽锥扇焰短程喷射；「气压」持续压喷 3 秒渐满，焰程随气压缩至 60%，松手回压，形成呼吸节奏。<br/>
    /// 凝胶消耗走 <see cref="Player.PickAmmo"/> 原版路径（每个射击节拍一发，1:1）。<br/>
    /// ai[2]=干仓旗标（owner 写 + netUpdate）；
    /// 气压由同步的 DownLeft 输入流在各端确定性积分，不走网络包
    /// </summary>
    internal abstract class GsFlamerHeldBase : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override LocalizedText DisplayName =>
            HeldTargetItemID > ItemID.None && HeldTargetItemID < ItemID.Count
                ? Language.GetText("ItemName." + ItemID.Search.GetName(HeldTargetItemID))
                : base.DisplayName;

        /// <summary>接管的原版物品 ID</summary>
        protected abstract int HeldTargetItemID { get; }
        /// <summary>宽扇伤害系数，命中节奏对齐原版后的对账系数</summary>
        protected virtual float DamageFactor => 1.05f;

        /// <summary>气压满值（3 秒）</summary>
        protected const int PressureMax = 180;
        /// <summary>基础射击节拍（tick/发），对齐原版喷火器耗弹率</summary>
        protected const int BaseFireInterval = 4;
        /// <summary>停火多少帧后收枪</summary>
        protected const int IdleKillDelay = 45;

        protected int fireTimer;
        protected int idleTimer;
        protected int soundTimer;
        protected int dryTimer;
        /// <summary>气压积分（各端各自按 DownLeft 推进，近似一致）</summary>
        protected float pressure;
        /// <summary>枪口后坐动画量 0..1</summary>
        protected float recoilAnim;

        protected bool Dry => Projectile.ai[2] > 0f;
        protected float Pressure01 => pressure / PressureMax;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.hide = true;
            Projectile.timeLeft = 60;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            //模式被关立刻收枪，硬性兜底；换武器/死亡同判
            if (!GameModeSystem.GodSmithActive || Item.type != HeldTargetItemID
                || Owner.dead || !Owner.active || Owner.noItems) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 2;

            UpdatePose();

            bool wantFire = DownLeft && !Owner.CCed;
            if (wantFire && !Dry) {
                idleTimer = 0;
                pressure = MathF.Min(pressure + 1f, PressureMax);
                float atkSpeed = Owner.GetWeaponAttackSpeed(Item);
                if (atkSpeed <= 0f) {
                    atkSpeed = 1f;
                }
                int interval = Math.Max(1, (int)MathF.Round(BaseFireInterval / atkSpeed));
                if (++fireTimer >= interval) {
                    fireTimer = 0;
                    FireOnce();
                }
            }
            else {
                idleTimer++;
                pressure = MathF.Max(0f, pressure - 3f);
                fireTimer = 99;//再按立即出焰
                if (Dry && Projectile.IsOwnedByLocalPlayer()) {
                    //松手或断按时复位干仓，让下次扣扳机重新走弹药判定
                    SetDry(false);
                }
                if (idleTimer > IdleKillDelay) {
                    Projectile.Kill();
                    return;
                }
            }

            if (Dry) {
                if (++dryTimer > 30 && Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.Kill();
                    return;
                }
            }
            else {
                dryTimer = 0;
            }

            recoilAnim = MathF.Max(0f, recoilAnim - 0.16f);
        }

        /// <summary>每帧姿态：喷射中枪身持续微推 + 低幅震颤</summary>
        protected virtual void UpdatePose() {
            float shake = recoilAnim > 0.05f
                ? MathF.Sin(Main.GameUpdateCount * 1.7f + Projectile.identity) * 0.012f
                : 0f;
            GsGunPose.Update(this, 20f, -4f, recoilAnim * 0.03f + shake, recoilAnim * 2.2f);
        }

        /// <summary>一个射击节拍：owner 端过原版弹药链并生成焰弹，各端播音与后坐</summary>
        protected virtual void FireOnce() {
            recoilAnim = 1f;
            if (!VaultUtils.isServer && ++soundTimer >= 2) {
                soundTimer = 0;
                //气压跌落时喷口声音发闷，读得出憋气
                SoundEngine.PlaySound(SoundID.Item34 with {
                    Volume = 0.30f,
                    Pitch = -0.2f - 0.35f * Pressure01,
                    MaxInstances = 3
                }, Projectile.Center);
            }

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            if (!Owner.PickAmmo(Item, out _, out _, out int damage, out float knockback, out _, false)) {
                SetDry(true);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item16 with { Volume = 0.4f, Pitch = -0.4f }, Projectile.Center);
                }
                return;
            }

            float pressFactor = 1f - 0.4f * Pressure01;
            const float Spread = 0.227f;
            float speed = 11.5f * pressFactor;
            int life = (int)(26 * MathHelper.Lerp(1f, 0.78f, Pressure01));
            Vector2 muzzle = GsGunPose.MuzzlePos(Projectile, DirSign, 30f, -2f);
            Vector2 vel = (ToMouseA + Main.rand.NextFloat(-Spread, Spread)).ToRotationVector2()
                * speed * Main.rand.NextFloat(0.9f, 1.08f);

            Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), muzzle, vel,
                ModContent.ProjectileType<GsFlameJetProj>(), (int)(damage * DamageFactor), knockback,
                Owner.whoAmI, life);
        }

        /// <summary>干仓旗标写 ai[2] 过线，远端跟着停焰停积压</summary>
        protected void SetDry(bool value) {
            if (Dry != value) {
                Projectile.ai[2] = value ? 1f : 0f;
                NetUpdate();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //手持接管期间原版不绘制枪体，此为必要的一笔
            GsGunPose.DrawGunBody(HeldTargetItemID, Projectile.Center, Projectile.rotation, DirSign, lightColor);
            return false;
        }
    }

    /// <summary>
    /// 燃烧流体焰弹：喷射器的火舌单元。速度衰减 + 热浮力 + 湍流摆动 + 膨胀，
    /// 绝非恒速直线；同类共享 ID 静态免疫对齐原版火焰的命中节奏。<br/>
    /// ai[0]=寿命
    /// </summary>
    internal class GsFlameJetProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BallofFire;

        private int maxLife = 30;
        private bool tileHit;

        /// <summary>0 出生 → 1 熄灭</summary>
        private float Life01 => 1f - Projectile.timeLeft / (float)maxLife;
        private float Seed => Projectile.identity * 0.6180f % 1f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 5;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.timeLeft = 60;
        }

        public override void OnSpawn(IEntitySource source) {
            maxLife = Math.Clamp((int)Projectile.ai[0], 8, 90);
            Projectile.timeLeft = maxLife;
        }

        public override void AI() {
            //流体运动：粘性衰减、热浮力、identity 相位的横向湍流
            Projectile.velocity *= 0.925f;
            Projectile.velocity.Y -= 0.045f;
            Vector2 side = new(-Projectile.velocity.Y, Projectile.velocity.X);
            side = side.SafeNormalize(Vector2.Zero);
            Projectile.velocity += side * MathF.Sin(Main.GameUpdateCount * 0.31f + Seed * MathHelper.TwoPi) * 0.06f;

            //先胀后滞的火团呼吸
            Projectile.scale = MathHelper.Lerp(0.55f, 1.7f, MathF.Sqrt(Life01));
            Projectile.rotation += (Seed - 0.5f) * 0.09f;

            //入水熄灭
            if (Projectile.wet) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.3f, MaxInstances = 2 }, Projectile.Center);
                }
                Projectile.Kill();
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            tileHit = true;
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire, Main.rand.Next(180, 300));

        public override void OnKill(int timeLeft) {
            //扇焰贴地时按配额铺残焰补丁（owner 权威，identity 稀释密度）
            if (tileHit && Projectile.owner == Main.myPlayer
                && Projectile.identity % 3 == 0
                && Main.player[Projectile.owner].ownedProjectileCounts[ModContent.ProjectileType<GsFlamePatchProj>()] < 6) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                    Projectile.Center - Vector2.UnitY * 4f, Vector2.Zero,
                    ModContent.ProjectileType<GsFlamePatchProj>(),
                    Math.Max(1, Projectile.damage / 2), 0f, Projectile.owner);
            }
        }
    }

    /// <summary>
    /// 残焰补丁：扇焰贴地后留下的燃烧地灾，2 秒踩踏 dot
    /// </summary>
    internal class GsFlamePatchProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BallofFire;

        public override void SetDefaults() {
            Projectile.width = 56;
            Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120;
        }

        public override void AI() => Projectile.velocity = Vector2.Zero;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire, 120);

        public override bool PreDraw(ref Color lightColor) {
            //区域一笔：原版贴图按判定箱宽度拉伸画在地面，给出踩踏范围提示
            Main.instance.LoadProjectile(Projectile.type);
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 scale = new(Projectile.width / (float)tex.Width, Projectile.height / (float)tex.Height);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor, 0f,
                tex.Size() / 2f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
