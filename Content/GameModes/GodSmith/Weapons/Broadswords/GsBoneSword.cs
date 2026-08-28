using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【骨剑】材质：干燥髓骨磨成的大骨刃。签名：①终结拍收势时甩出一根旋骨，
    /// 抛物线飞行且自旋越转越快 ②旋骨与刀击命中都迸溅骨屑 ③终结劈砍带前压与骨白辉光
    /// </summary>
    internal class GsBoneSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.BoneSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsBoneSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: the third strike hurls a spinning bone that arcs through the air, " +
            "splintering on whatever it strikes";

        //干骨色板
        internal static readonly Color BoneBright = new(246, 240, 224); //骨白
        internal static readonly Color BoneMain = new(216, 198, 160);   //米黄骨身
        internal static readonly Color BoneHot = new(255, 216, 138);    //髓芯暖黄
        internal static readonly Color BoneDeep = new(52, 42, 30);      //枯骨暗棕

        //底伤 -5% 摊账：每三拍 = 1 + 1 + 1.3(终结) + 0.45(掷骨) ≈ 单拍均值 1.25x，
        //乘 0.95 后综合 DPS 约为原版 112%~119%，落在包络内
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 0.95f;
    }

    /// <summary>
    /// 骨剑手持：三拍。0/1 交替劈骨，2 终结重劈+前压；终结拍进收势的首帧
    /// 向瞄准向甩出旋骨（约 45% 底伤）。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsBoneSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.BoneSword;
        protected override Color EdgeBright => GsBoneSword.BoneBright;
        protected override Color BodyMain => GsBoneSword.BoneMain;
        protected override Color HotAccent => GsBoneSword.BoneHot;
        protected override Color DeepShadow => GsBoneSword.BoneDeep;

        private bool boneTossed;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //甩骨终结：大后摆重劈，收势稍长给掷骨动作留戏
                return new GsBroadBeat {
                    Raise = 8, Hold = 3, Slash = 5, Recover = 13,
                    RaiseBack = 2.3f, Follow = 1.2f, ReachScale = 1.12f, LeanAmp = 0.085f,
                    DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 3f, SwingPitch = -0.22f,
                };
            }
            GsBroadBeat b = GsBroadBeat.Standard;
            b.SwingPitch = stage == 0 ? 0.06f : -0.04f;//干骨劈砍偏干偏脆
            return b;
        }

        protected override void HandlePhaseEvents(int phase) {
            base.HandlePhaseEvents(phase);
            //终结拍进收势的首帧甩骨：顺挥砍余势掷出，带上抬弧线
            if (IsFinisher && !boneTossed && phase == PhaseRecover) {
                boneTossed = true;
                Vector2 tossVel = baseAngle.ToRotationVector2() * 11.5f + new Vector2(0f, -2.6f);
                //底伤 45%：先除回终结拍乘数再摊
                int tossDamage = Math.Max(1, (int)(Projectile.damage * 0.45f / Beat.DamageMult));
                SpawnOwnedProj(ModContent.ProjectileType<GsBoneSwordTossProj>(), Hand, tossVel,
                    tossDamage, Projectile.knockBack * 0.6f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.6f, Pitch = 0.32f }, Owner.Center);
                }
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            //斩切期抖落干燥骨粉
            if (phase == PhaseSlash && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 1f)),
                    DustID.Bone, Vector2.Zero, 80, default, Main.rand.NextFloat(0.7f, 1.1f));
                d.velocity = (mainAngle + swingDir * MathHelper.PiOver2).ToRotationVector2() * 1.4f;
                d.noGravity = false;
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //骨屑迸溅
            int shards = IsFinisher ? 8 : 5;
            for (int i = 0; i < shards; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Bone,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5.5f), 60, default,
                    Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = Main.rand.NextBool(3);
            }
        }
    }

    /// <summary>
    /// 掷骨：抛物线飞行的旋骨。自旋带角加速度（越飞越快转），
    /// 自绘两层旋转残影（加色 A=0），命中或落地骨屑迸溅
    /// </summary>
    internal class GsBoneSwordTossProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>当前自旋角速度（本地演出量，各端由同一初速确定性推进）</summary>
        private float spinSpeed = 0.16f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 150;
        }

        public override void AI() {
            //抛物线：重力与轻微空气阻尼
            Projectile.velocity.Y += 0.34f;
            Projectile.velocity.X *= 0.995f;

            //角加速度：自旋逐帧加快，不匀速
            float dir = Projectile.velocity.X >= 0f ? 1f : -1f;
            spinSpeed = Math.Min(spinSpeed + 0.014f, 0.52f);
            Projectile.rotation += spinSpeed * dir;

            Lighting.AddLight(Projectile.Center, GsBoneSword.BoneMain.ToVector3() * 0.22f);

            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                //飞行途中撒落骨粉
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Bone,
                    -Projectile.velocity * 0.1f, 120, default, Main.rand.NextFloat(0.6f, 0.9f));
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SplinterBurst(target.Center, 8);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.7f, Pitch = 0.4f }, Projectile.Center);
            SplinterBurst(Projectile.Center, 10);
        }

        /// <summary>骨屑迸溅（已守非服务器端）</summary>
        private void SplinterBurst(Vector2 pos, int count) {
            for (int i = 0; i < count; i++) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Bone,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f), 40, default,
                    Main.rand.NextFloat(0.9f, 1.5f));
                d.noGravity = Main.rand.NextBool(3);
            }
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, GsBoneSword.BoneBright, 0.2f)
                ?.Configure(10, 0.75f);
        }

        public override bool PreDraw(ref Color lightColor) {
            //原版骨剑贴图当骨体，残影层全自绘
            Main.instance.LoadItem(ItemID.BoneSword);
            Texture2D tex = TextureAssets.Item[ItemID.BoneSword].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float dir = Projectile.velocity.X >= 0f ? 1f : -1f;

            //两层旋转残影：拖在自旋后方，转得越快拖得越开
            for (int g = 2; g >= 1; g--) {
                float ghostRot = Projectile.rotation - dir * spinSpeed * g * 2.2f;
                Color ghost = GsBoneSword.BoneBright * (g == 1 ? 0.32f : 0.15f);
                ghost.A = 0;
                Main.EntitySpriteDraw(tex, drawPos, null, ghost, ghostRot, origin, 0.9f, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, drawPos, null, lightColor, Projectile.rotation, origin, 0.9f, SpriteEffects.None, 0);
            return false;
        }
    }
}
