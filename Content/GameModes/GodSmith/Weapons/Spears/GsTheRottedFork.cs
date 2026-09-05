using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 【长矛】腐叉重铸：腐脓挤压。<br/>
    /// 材质：猩红血肉拧成的三股叉，齿间滴脓。签名行为：①两拍连刺——轻拍快叉、
    /// 重拍慢压更深 ②重拍命中从伤口挤出一枚腐脓弹，抛物线坠地或撞怪后
    /// 摊成小腐蚀池持续灼蚀 ③命中黏腻湿响，血肉气息浓重
    /// </summary>
    internal class GsTheRottedFork : GsSpearScheme
    {
        public override int TargetItemID => ItemID.TheRottedFork;

        protected override string GsDescFallback =>
            "Reforged: two-beat strikes, a quick jab then a heavy squeeze;\nthe heavy beat squeezes a glob of gore from the wound that splats into a corroding pool";
        protected override int HeldProjType => ModContent.ProjectileType<GsTheRottedForkHeld>();

        protected override int ComboBeats => 2;

        //腐脓弹+腐蚀池吃掉大半预算，底伤小补，综合 DPS 落在原版 105%~118%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.10f;
    }

    /// <summary>
    /// 腐叉手持突刺。ai[0]=拍号 0 轻拍快叉 / 1 重拍慢压；
    /// 重拍命中挤出腐脓弹（20% 伤害，落点留腐蚀池）
    /// </summary>
    internal class GsTheRottedForkHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.TheRottedFork;

        private bool IsHeavyBeat => ComboStage >= 1;

        //轻拍快叉，重拍慢压更深；重拍收势特意拉长（对齐原版 31 帧节奏，腐脓弹才有预算）
        protected override float WindupFrames => IsHeavyBeat ? 6f : 5f;
        protected override float ThrustFrames => IsHeavyBeat ? 6f : 5f;
        protected override float DwellFrames => IsHeavyBeat ? 4f : 3f;
        protected override float RecoverFrames => IsHeavyBeat ? 12f : 10f;
        protected override float RestHoldout => 12f;
        protected override float PullbackDist => IsHeavyBeat ? 18f : 12f;
        protected override float StabReach => IsHeavyBeat ? 72f : 56f;
        protected override float BladeLength => 86f;
        protected override float CollisionWidth => 30f;
        protected override float TipGreedRadius => 27f;
        protected override float ThrustEasePower => IsHeavyBeat ? 3.2f : 2.6f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => IsHeavyBeat ? 0.055f : 0.032f;
        protected override int HitboxSize => 52;
        protected override int HitstopFrames => IsHeavyBeat ? 3 : 2;
        protected override float ThrustPitch => IsHeavyBeat ? -0.38f : -0.20f;

        protected override void OnInit() {
            //重拍慢压：伤害上浮补慢拍节奏
            if (IsHeavyBeat) {
                Projectile.damage = (int)(Projectile.damage * 1.20f);
            }
        }

        protected override void OnThrustBurst() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.75f, Pitch = ThrustPitch }, Owner.Center);
            if (IsHeavyBeat) {
                //重拍带一声黏腻湿响
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.4f, Pitch = -0.35f }, Owner.Center);
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            Vector2 from = Vector2.Lerp(TipPos, target.Center, 0.5f);
            //命中反馈：黏腻湿响
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.45f, Pitch = IsHeavyBeat ? -0.5f : -0.2f, MaxInstances = 3 }, from);
            }
            //腐脓挤压：重拍首个命中从伤口挤出一枚腐脓弹（owner 端生成，随生成包过线）
            if (!IsHeavyBeat || !firstOnTarget || Projectile.numHits > 1 || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Vector2 vel = stabUnit * 5f;
            vel.Y -= 3.5f;//挤出后小抛物线
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), from, vel,
                ModContent.ProjectileType<GsRottedForkGlobProj>(),
                (int)(BaseDamage * 0.20f), Projectile.knockBack * 0.3f, Owner.whoAmI);
        }
    }

    /// <summary>
    /// 腐脓弹：重拍从伤口挤出，抛物线受重力，坠地或撞怪即摊成腐蚀池。原版血弹贴图默认绘制
    /// </summary>
    internal class GsRottedForkGlobProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BloodNautilusShot;
        public override LocalizedText DisplayName => Language.GetText("ItemName.TheRottedFork");

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            //抛物线受重力
            Projectile.velocity.Y += 0.32f;
            if (Projectile.velocity.Y > 12f) {
                Projectile.velocity.Y = 12f;
            }
            Projectile.rotation += Projectile.velocity.X * 0.04f;
        }

        public override void OnKill(int timeLeft) {
            //落地或命中：摊成腐蚀池（owner 端生成，随生成包过线）
            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsRottedForkPoolProj>(),
                    (int)(Projectile.damage * 0.6f), 0f, Projectile.owner);
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.5f, Pitch = -0.6f, MaxInstances = 3 }, Projectile.Center);
        }
    }

    /// <summary>
    /// 腐蚀池：腐脓弹落点摊开的小片脓沼，~1.2 秒内持续低伤判定。<br/>
    /// 区域弹：原版血弹贴图按判定箱尺寸拉伸一笔（lightColor 着色）
    /// </summary>
    internal class GsRottedForkPoolProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BloodNautilusShot;
        public override LocalizedText DisplayName => Language.GetText("ItemName.TheRottedFork");

        private const int LifeFrames = 72;

        public override void SetDefaults() {
            Projectile.width = 64;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;//~1.2s 内至多灼蚀 4 跳
            Projectile.knockBack = 0f;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>区域一笔：贴图按判定箱拉伸，提示灼蚀范围</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 scale = new(Projectile.width / (float)tex.Width, Projectile.height / (float)tex.Height);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor, 0f,
                tex.Size() / 2f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
