using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Flails
{
    /// <summary>
    /// 【连枷·花之力】粉樱花冠链锤：樱粉花瓣叶绿点缀。签名行为：①甩转每 33 帧蓄一片花瓣（至多 5 片）
    /// ②掷出命中瞬间花瓣全数化作追踪花刃齐射目标 ③收链回手花瓣保留不散
    /// </summary>
    internal class GsFlowerPow : GsFlailScheme
    {
        public override int TargetItemID => ItemID.FlowerPow;

        protected override int FlailProjType => ModContent.ProjectileType<GsFlowerPowHead>();

        protected override string GsDescFallback =>
            "Reforged: petals bloom around the head while spinning, up to five\nLanding a throw sends every petal homing at the target as razor blades";
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;
    }

    /// <summary>
    /// 花之力锤头。花瓣不是弹幕：计数存锤头字段；
    /// 花瓣消耗事件经 ai[2]=1 + netUpdate 过线，远端同步收瓣
    /// </summary>
    internal class GsFlowerPowHead : GsFlailHeadProj
    {
        public override int SourceItemID => ItemID.FlowerPow;
        public override int VanillaProjID => ProjectileID.FlowerPow;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain19;

        /// <summary>花瓣上限</summary>
        private const int PetalCap = 5;
        /// <summary>绽放间隔帧</summary>
        private const int BloomInterval = 33;
        /// <summary>花刃伤害系数</summary>
        private const float PetalDamageMul = 0.45f;

        /// <summary>当前蓄存花瓣数；各端由各自 OnSpinTick 长出，节奏确定性一致</summary>
        private int petalCount;

        protected override void OnSpinTick(float charge) {
            //甩转每 33 帧绽一片，最多 5 片
            if (petalCount >= PetalCap || spinTimer % BloomInterval != 0) {
                return;
            }
            petalCount++;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f, Pitch = 0.55f }, Projectile.Center);
            }
        }

        protected override void PostStateAI() {
            //owner 消耗花瓣后把 ai[2] 写成 1 并 netUpdate，远端在此同步收瓣
            if (Projectile.ai[2] >= 1f) {
                petalCount = 0;
            }
        }

        protected override void OnHeadHit(NPC target, NPC.HitInfo hit, int damageDone, bool headHit) {
            if (!headHit || Owner.whoAmI != Main.myPlayer || petalCount <= 0) {
                return;
            }
            //花瓣全数化作追踪花刃齐射目标；出射角按序号确定性散开
            Vector2 baseDir = Projectile.Center.To(target.Center).SafeNormalize(Vector2.UnitX);
            int volley = petalCount;
            for (int i = 0; i < volley; i++) {
                Vector2 dir = baseDir.RotatedBy((i - (volley - 1) * 0.5f) * 0.42f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir * 13f,
                    ModContent.ProjectileType<GsFlowerPowPetalProj>(),
                    Math.Max(1, (int)(Projectile.damage * PetalDamageMul)), 1.5f, Projectile.owner, target.whoAmI);
            }
            petalCount = 0;
            Projectile.ai[2] = 1f;
            Projectile.netUpdate = true;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.7f, Pitch = 0.2f }, target.Center);
            }
        }
    }

    /// <summary>
    /// 追踪花刃：ai[0]=目标 whoAmI；初段快出、中段转向追踪、尾段收速淡出（减速曲线），全程自旋；
    /// 原版花之力花瓣贴图默认绘制，淡入淡出走 Opacity
    /// </summary>
    internal class GsFlowerPowPetalProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlowerPowPetal;

        private const int LifeFrames = 80;
        private const int FadeInFrames = 4;
        private const int FadeOutFrames = 14;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = LifeFrames;
        }

        private float LifeT => 1f - Projectile.timeLeft / (float)LifeFrames;

        private float Opacity {
            get {
                if (Projectile.timeLeft > LifeFrames - FadeInFrames) {
                    return (LifeFrames - Projectile.timeLeft) / (float)FadeInFrames;
                }
                if (Projectile.timeLeft < FadeOutFrames) {
                    return Projectile.timeLeft / (float)FadeOutFrames;
                }
                return 1f;
            }
        }

        public override void AI() {
            //减速曲线：速度上限从 15 收到 7，绝不匀速直飞
            float speedCap = MathHelper.Lerp(15f, 7f, LifeT * LifeT);
            int targetId = (int)Projectile.ai[0];
            NPC target = targetId >= 0 && targetId < Main.maxNPCs ? Main.npc[targetId] : null;
            if (target != null && target.active && target.CanBeChasedBy(Projectile)) {
                //中段追踪：朝目标缓转向
                Vector2 desired = Projectile.Center.To(target.Center).SafeNormalize(Vector2.UnitX) * speedCap;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.085f);
            }
            else {
                //丢失目标：顺势滑行减速淡出
                Projectile.velocity *= 0.94f;
            }
            if (Projectile.velocity.Length() > speedCap) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * speedCap;
            }
            //自旋方向按 identity 定死，两端一致；淡入淡出交给默认绘制的 alpha
            Projectile.rotation += 0.27f * (Projectile.identity % 2 == 0 ? 1f : -1f);
            Projectile.Opacity = Opacity;
        }

        public override bool? CanDamage() => Opacity > 0.5f ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.35f, Pitch = 0.45f }, Projectile.Center);
        }
    }
}
