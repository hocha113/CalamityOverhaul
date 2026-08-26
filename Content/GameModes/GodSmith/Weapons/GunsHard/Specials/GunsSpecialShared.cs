using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsHard.Specials
{
    /// <summary>
    /// 枪·困难特种切片（GunsSpecial）的共享工具：手写持枪姿态帮手 + 喷射器手持基类 + 燃烧流体焰弹 + 残焰补丁。<br/>
    /// 本切片手持接管一律 <see cref="BaseHeldProj"/> 手写姿态，禁止继承 BaseHeldGun
    /// （其 TargetID 扫描会把原版物品永久写进 ItemIsGun 表，违反模式关闭零足迹）
    /// </summary>
    internal static class GsGunPose
    {
        /// <summary>
        /// 手写双手持枪姿态：朝向鼠标、双臂角、枪心锚定、动画锁。
        /// 公式镜像 BaseHeldGun 的姿态数学但完全自持。返回本帧瞄准角
        /// </summary>
        /// <param name="held">手持弹幕</param>
        /// <param name="handDistX">枪心沿瞄准向的距离</param>
        /// <param name="handDistY">枪心垂直落差</param>
        /// <param name="recoilPitch">后坐上抬角（弧度）</param>
        /// <param name="recoilBack">后坐制退位移（沿瞄准向后退 px）</param>
        /// <param name="backArmLift">后手向枪口侧的托举偏角</param>
        public static float Update(BaseHeldProj held, float handDistX, float handDistY,
            float recoilPitch, float recoilBack, float backArmLift = 0.32f) {
            Player owner = held.Owner;
            Projectile proj = held.Projectile;

            owner.ChangeDir(held.ToMouse.X >= 0f ? 1 : -1);
            int safeGrav = held.SafeGravDir;
            int dirSign = owner.direction * safeGrav;

            float aimRot = held.ToMouseA - recoilPitch * dirSign;
            proj.rotation = aimRot;
            Vector2 aimUnit = aimRot.ToRotationVector2();
            proj.Center = owner.GetPlayerStabilityCenter()
                + aimUnit * (handDistX - recoilBack)
                + new Vector2(0f, handDistY * safeGrav);

            //手臂角公式与 BaseHeldGun 同构：正重力下等价于 aimRot - PiOver2
            float armRot = (MathHelper.PiOver2 * safeGrav - aimRot) * dirSign * safeGrav;
            owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRot * -dirSign);
            owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters,
                armRot * -dirSign + owner.direction * backArmLift);

            owner.heldProj = proj.whoAmI;
            owner.itemTime = owner.itemAnimation = 2;
            owner.itemRotation = (aimUnit * owner.direction).ToRotation();
            return aimRot;
        }

        /// <summary>取枪口世界坐标：沿枪身向前 forward，再沿法向偏 normal</summary>
        public static Vector2 MuzzlePos(Projectile proj, int dirSign, float forward, float normal) {
            Vector2 forwardUnit = proj.rotation.ToRotationVector2();
            Vector2 normalUnit = (proj.rotation + (dirSign > 0 ? MathHelper.PiOver2 : -MathHelper.PiOver2)).ToRotationVector2();
            return proj.Center + forwardUnit * forward + normalUnit * normal;
        }

        /// <summary>
        /// 用原版物品贴图画枪体，面左翻转；glow 非空时叠一层同形加色辉光（A 自动清零）
        /// </summary>
        public static void DrawGunBody(int itemId, Vector2 center, float rotation, int dirSign,
            Color lightColor, float scale = 1f, Color? glow = null, float glowScaleBoost = 1.03f) {
            Main.instance.LoadItem(itemId);
            Texture2D tex = TextureAssets.Item[itemId].Value;
            SpriteEffects fx = dirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            Vector2 drawPos = center - Main.screenPosition;
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor, rotation, tex.Size() / 2f, scale, fx, 0);
            if (glow.HasValue && glow.Value.R + glow.Value.G + glow.Value.B > 0) {
                Color g = glow.Value;
                g.A = 0;
                Main.EntitySpriteDraw(tex, drawPos, null, g, rotation, tex.Size() / 2f, scale * glowScaleBoost, fx, 0);
            }
        }

        /// <summary>
        /// 模式切换的本地反馈：漂字 + 机械咔嗒。只在本地玩家路径调用
        /// </summary>
        public static void ModeSwitchFeedback(Player player, string modeName) {
            if (VaultUtils.isServer) {
                return;
            }
            CombatText.NewText(player.getRect(), GameModeTheme.GodSmithAccent, modeName);
            SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.6f, Pitch = 0.25f }, player.Center);
        }
    }

    /// <summary>
    /// 喷射器手持基类（Flamethrower / Elf Melter 共用）。<br/>
    /// 双档喷射：档 0 宽锥扇焰短程，档 1 窄矛长程；
    /// 「气压」持续压喷 3 秒渐满，焰程随气压缩至 60%，松手回压，形成呼吸节奏。<br/>
    /// 凝胶消耗走 <see cref="Player.PickAmmo"/> 原版路径（每个射击节拍一发，1:1）。<br/>
    /// ai[0]=当前档位，ai[2]=干仓旗标（owner 写 + netUpdate）；
    /// 热量/气压由同步的 DownLeft 输入流在各端确定性积分，不走网络包
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
        /// <summary>焰弹色板：0 火橙 / 1 圣诞红绿</summary>
        protected virtual int JetPalette => 0;
        /// <summary>枪口辉光主色</summary>
        protected abstract Color MuzzleColor { get; }
        /// <summary>档 0（宽扇）伤害系数，命中节奏对齐原版后的对账系数</summary>
        protected virtual float Mode0DamageFactor => 1.05f;
        /// <summary>档 1（窄矛）伤害系数，弹著率低于宽扇故补偿更高</summary>
        protected virtual float Mode1DamageFactor => 1.25f;

        /// <summary>气压满值（3 秒）</summary>
        protected const int PressureMax = 180;
        /// <summary>基础射击节拍（tick/发），对齐原版喷火器耗弹率</summary>
        protected const int BaseFireInterval = 4;
        /// <summary>停火多少帧后收枪</summary>
        protected const int IdleKillDelay = 45;

        protected int fireTimer;
        protected int idleTimer;
        protected int soundTimer;
        protected int switchCd;
        protected int dryTimer;
        /// <summary>气压积分（各端各自按 DownLeft 推进，近似一致）</summary>
        protected float pressure;
        /// <summary>枪口后坐动画量 0..1</summary>
        protected float recoilAnim;
        private bool oldDownRight;

        protected int Mode => (int)Projectile.ai[0];
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
            HandleModeSwitch();

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
            HandleAmbientEffects(wantFire);
        }

        /// <summary>每帧姿态：喷射中枪身持续微推 + 低幅震颤</summary>
        protected virtual void UpdatePose() {
            float shake = recoilAnim > 0.05f
                ? MathF.Sin(Main.GameUpdateCount * 1.7f + Projectile.identity) * 0.012f
                : 0f;
            GsGunPose.Update(this, 20f, -4f, recoilAnim * 0.03f + shake, recoilAnim * 2.2f);
        }

        /// <summary>右键循环档位：owner 边沿检测，写 ai[0] 并 netUpdate，全端演出一致</summary>
        private void HandleModeSwitch() {
            if (switchCd > 0) {
                switchCd--;
            }
            if (Projectile.IsOwnedByLocalPlayer()) {
                if (DownRight && !oldDownRight && switchCd <= 0) {
                    switchCd = 12;
                    Projectile.ai[0] = Mode == 0 ? 1f : 0f;
                    NetUpdate();
                    OnModeSwitched(Mode);
                }
                oldDownRight = DownRight;
            }
        }

        /// <summary>档位切换后的反馈（owner 端），子类给漂字文本</summary>
        protected abstract void OnModeSwitched(int newMode);

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
            bool wideMode = Mode == 0;
            float spread = wideMode ? 0.227f : 0.052f;
            float speed = (wideMode ? 11.5f : 14.5f) * pressFactor;
            int life = (int)((wideMode ? 26 : 40) * MathHelper.Lerp(1f, 0.78f, Pressure01));
            Vector2 muzzle = GsGunPose.MuzzlePos(Projectile, DirSign, 30f, -2f);
            Vector2 vel = (ToMouseA + Main.rand.NextFloat(-spread, spread)).ToRotationVector2()
                * speed * Main.rand.NextFloat(0.9f, 1.08f);
            int dmg = (int)(damage * (wideMode ? Mode0DamageFactor : Mode1DamageFactor));

            Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), muzzle, vel,
                ModContent.ProjectileType<GsFlameJetProj>(), dmg, knockback, Owner.whoAmI,
                JetPalette, wideMode ? 0f : 1f, life);

            OnFireExtra(muzzle, damage, knockback);
        }

        /// <summary>owner 端每发之后的追加钩子（Elf Melter 礼物投射用）</summary>
        protected virtual void OnFireExtra(Vector2 muzzle, int ammoDamage, float knockback) { }

        /// <summary>干仓旗标写 ai[2] 过线，远端跟着停焰停积压</summary>
        protected void SetDry(bool value) {
            if (Dry != value) {
                Projectile.ai[2] = value ? 1f : 0f;
                NetUpdate();
            }
        }

        /// <summary>常驻演出：枪口点火种与稀疏烬粒，预算每帧不超 1 粒</summary>
        protected virtual void HandleAmbientEffects(bool firing) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 muzzle = GsGunPose.MuzzlePos(Projectile, DirSign, 30f, -2f);
            Lighting.AddLight(muzzle, MuzzleColor.ToVector3() * (firing ? 0.8f : 0.25f));
            if (firing && Main.GameUpdateCount % 6 == 0) {
                var flame = PRTLoader.NewParticle<PRT_HellFlame>(muzzle,
                    Projectile.rotation.ToRotationVector2() * 2f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    Color.White, 0.4f);
                if (flame != null) {
                    flame.ai[0] = 0;
                    flame.ai[2] = 10;
                    flame.ai[3] = 16;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //红热/枪口亮度随喷射衰减，identity 定相不掷随机
            float flicker = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 11f + Projectile.identity * 0.83f);
            float heat = recoilAnim * flicker;
            GsGunPose.DrawGunBody(HeldTargetItemID, Projectile.Center, Projectile.rotation, DirSign,
                lightColor, 1f, MuzzleColor * (0.12f + heat * 0.30f));

            //枪口火种：常亮一粒小加色光核，喷射时放大
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Vector2 muzzle = GsGunPose.MuzzlePos(Projectile, DirSign, 26f, -2f) - Main.screenPosition;
                Color c = MuzzleColor;
                c.A = 0;
                float s = 0.06f + heat * 0.10f;
                Main.EntitySpriteDraw(glow, muzzle, null, c * (0.5f + heat * 0.5f), 0f,
                    glow.Size() / 2f, s, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 燃烧流体焰弹：喷射器的火舌单元。速度衰减 + 热浮力 + 湍流摆动 + 膨胀 + 色温冷却，
    /// 绝非恒速直线；同类共享 ID 静态免疫对齐原版火焰的命中节奏。<br/>
    /// ai[0]=色板（0 火橙 / 1 圣诞红绿），ai[1]=形态（0 扇焰可留残焰补丁 / 1 窄矛），ai[2]=寿命
    /// </summary>
    internal class GsFlameJetProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //火橙色板
        private static readonly Color FireBright = new(255, 232, 150);
        private static readonly Color FireMain = new(255, 128, 40);
        private static readonly Color FireDeep = new(168, 40, 22);
        //圣诞色板（红绿交替由 identity 决定）
        private static readonly Color ElfGreen = new(96, 232, 110);
        private static readonly Color ElfRed = new(255, 92, 82);
        private static readonly Color ElfDeep = new(70, 130, 90);

        private int maxLife = 30;
        private bool tileHit;

        private int Palette => (int)Projectile.ai[0];
        private bool WideMode => Projectile.ai[1] < 0.5f;
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

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) {
            maxLife = Math.Clamp((int)Projectile.ai[2], 8, 90);
            Projectile.timeLeft = maxLife;
        }

        public override void AI() {
            float t = Life01;

            //流体运动：粘性衰减、热浮力、identity 相位的横向湍流
            Projectile.velocity *= WideMode ? 0.925f : 0.955f;
            Projectile.velocity.Y -= 0.045f;
            Vector2 side = new(-Projectile.velocity.Y, Projectile.velocity.X);
            side = side.SafeNormalize(Vector2.Zero);
            Projectile.velocity += side * MathF.Sin(Main.GameUpdateCount * 0.31f + Seed * MathHelper.TwoPi) * 0.06f;

            //先胀后滞的火团呼吸
            Projectile.scale = MathHelper.Lerp(0.55f, 1.7f, MathF.Sqrt(t));
            Projectile.rotation += (Seed - 0.5f) * 0.09f;

            //入水熄灭成汽
            if (Projectile.wet) {
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 2; i++) {
                        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke,
                            new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1f, 2f)),
                            120, default, 1.1f);
                        d.noGravity = true;
                    }
                    SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.3f, MaxInstances = 2 }, Projectile.Center);
                }
                Projectile.Kill();
                return;
            }

            Color lightC = PaletteBody(0.3f);
            Lighting.AddLight(Projectile.Center, lightC.ToVector3() * (0.65f * (1f - t * 0.7f)));

            //稀疏补粒：每颗焰弹一生只出 3~5 粒，预算安全
            if (!VaultUtils.isServer && Projectile.timeLeft % 7 == 0) {
                var flame = PRTLoader.NewParticle<PRT_HellFlame>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    Projectile.velocity * 0.25f - Vector2.UnitY * 0.6f, Color.White,
                    0.3f + 0.3f * t);
                if (flame != null) {
                    flame.ai[0] = 0;
                    flame.ai[2] = 12;
                    flame.ai[3] = 20;
                }
                if (Palette == 1 && Main.rand.NextBool(3)) {
                    //圣诞焰混一缕冰融水汽
                    PRTLoader.NewParticle<PRT_DefFrostGlint>(Projectile.Center,
                        -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.2f),
                        new Color(190, 240, 255), 0.5f)?.Configure(Main.rand.Next(12, 20));
                }
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            tileHit = true;
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, Main.rand.Next(180, 300));
            if (!VaultUtils.isServer) {
                //命中溅焰，个人反馈层
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center,
                        (-Projectile.velocity).SafeNormalize(Vector2.UnitY).RotatedByRandom(0.8) * Main.rand.NextFloat(2f, 5f),
                        PaletteBright(), Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(12, 18));
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                //余烬回落，火比弹活得久
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_DefEmber>(Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                        new Vector2(Main.rand.NextFloat(-0.7f, 0.7f), -Main.rand.NextFloat(0.3f, 1f)),
                        Main.rand.NextBool() ? PaletteBright() : PaletteBody(0.5f),
                        Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(16, 26), 0.06f);
                }
                if (tileHit) {
                    PRTLoader.NewParticle<PRT_DefScorch>(Projectile.Center, Vector2.Zero,
                        new Color(60, 42, 34), Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(50, 80));
                }
            }

            //扇焰贴地时按配额铺残焰补丁（owner 权威，identity 稀释密度）
            if (tileHit && WideMode && Projectile.owner == Main.myPlayer
                && Projectile.identity % 3 == 0
                && Main.player[Projectile.owner].ownedProjectileCounts[ModContent.ProjectileType<GsFlamePatchProj>()] < 6) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                    Projectile.Center - Vector2.UnitY * 4f, Vector2.Zero,
                    ModContent.ProjectileType<GsFlamePatchProj>(),
                    Math.Max(1, Projectile.damage / 2), 0f, Projectile.owner, Palette);
            }
        }

        private Color PaletteBright() => Palette == 1
            ? (Seed > 0.5f ? ElfGreen : ElfRed)
            : FireBright;

        private Color PaletteBody(float t) => Palette == 1
            ? Color.Lerp(Seed > 0.5f ? ElfGreen : ElfRed, ElfDeep, t)
            : Color.Lerp(FireMain, FireDeep, t);

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            Texture2D coreTex = CWRAsset.Extra_98?.Value;
            Texture2D fogTex = CWRAsset.Fog?.Value;
            if (glowTex == null || coreTex == null) {
                return false;
            }

            float t = Life01;
            float fadeIn = MathHelper.Clamp((maxLife - Projectile.timeLeft) / 4f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / (maxLife * 0.35f), 0f, 1f);
            float alpha = fadeIn * fadeOut;
            if (alpha <= 0.02f) {
                return false;
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float wob = MathF.Sin(Main.GameUpdateCount * 0.42f + Seed * 9f) * 0.14f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.05f, 0f, 0.65f);
            float rot = Projectile.velocity.ToRotation();

            //色温冷却：亮芯白黄 → 主体橙 → 边缘暗红，尾段浮烟
            Color bright = Color.Lerp(PaletteBright(), PaletteBody(0.4f), t * 0.7f);
            Color body = PaletteBody(t * 0.85f);

            //边缘火膜（黑底贴图，A=0 加色）
            Color edge = body * (0.55f * alpha);
            edge.A = 0;
            Main.EntitySpriteDraw(glowTex, drawPos, null, edge, rot,
                glowTex.Size() / 2f, new Vector2(1f + stretch, 1f - wob * 0.5f) * (Projectile.scale * 0.40f),
                SpriteEffects.None, 0);

            //火体主团（真 alpha 液团贴图，带速度拉伸与表面失稳）
            Color bodyC = body * (0.85f * alpha);
            bodyC.A = 0;
            Main.EntitySpriteDraw(coreTex, drawPos, null, bodyC, rot + MathHelper.PiOver2,
                coreTex.Size() / 2f, new Vector2(0.30f + wob * 0.1f, 0.34f + stretch * 0.5f) * Projectile.scale,
                SpriteEffects.None, 0);

            //亮芯只活在前半生
            if (t < 0.6f) {
                Color coreC = bright * ((0.9f - t) * alpha);
                coreC.A = 0;
                Main.EntitySpriteDraw(coreTex, drawPos, null, coreC, rot + MathHelper.PiOver2,
                    coreTex.Size() / 2f, new Vector2(0.13f, 0.17f + stretch * 0.3f) * Projectile.scale,
                    SpriteEffects.None, 0);
            }

            //熄灭尾段浮烟（Fog 真 alpha，可压暗）
            if (fogTex != null && t > 0.55f) {
                float smoke = (t - 0.55f) / 0.45f;
                Color smokeC = new Color(52, 44, 44) * (smoke * 0.5f * fadeOut);
                Main.EntitySpriteDraw(fogTex, drawPos, null, smokeC, Seed * MathHelper.TwoPi + t * 1.5f,
                    fogTex.Size() / 2f, Projectile.scale * 0.30f * (1f + smoke),
                    Seed > 0.5f ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 残焰补丁：扇焰贴地后留下的燃烧地灾，2 秒踩踏 dot。ai[0]=色板
    /// </summary>
    internal class GsFlamePatchProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private static readonly Color PatchFire = new(255, 120, 40);
        private static readonly Color PatchElf = new(150, 230, 110);

        private int Palette => (int)Projectile.ai[0];
        private float Seed => Projectile.identity * 0.377f % 1f;
        private Color BodyColor => Palette == 1 ? PatchElf : PatchFire;

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

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Lighting.AddLight(Projectile.Center, BodyColor.ToVector3() * 0.4f);
            if (!VaultUtils.isServer && Main.GameUpdateCount % 9 == Projectile.identity % 9) {
                //地面火舌稀疏上蹿
                PRTLoader.NewParticle<PRT_DefFlameTongue>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-22f, 22f), 4f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.4f),
                    BodyColor, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(14, 24));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire, 120);

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            float fade = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f)
                * MathHelper.Clamp((120 - Projectile.timeLeft) / 8f, 0f, 1f);
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Seed * 12f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //压扁的火毯双层，摇曳靠相位错拍
            Color baseC = BodyColor * (0.55f * fade * pulse);
            baseC.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, baseC, 0f, glow.Size() / 2f,
                new Vector2(0.62f, 0.16f), SpriteEffects.None, 0);
            Color hotC = Color.Lerp(BodyColor, Color.White, 0.35f) * (0.4f * fade * (1.6f - pulse));
            hotC.A = 0;
            Main.EntitySpriteDraw(glow, drawPos + new Vector2(MathF.Sin(Seed * 20f) * 6f, -3f), null, hotC, 0f,
                glow.Size() / 2f, new Vector2(0.34f, 0.10f), SpriteEffects.None, 0);
            return false;
        }
    }
}
