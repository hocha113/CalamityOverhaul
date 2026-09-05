using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【湮光刃·光束剑】材质：地牢幽蓝晶簇里析出的纯光之刃，掷出的束是一柄光铸的剑影。
    /// 签名：①每一斩发出光束波，光束命中为刃身蓄光
    /// ②蓄满三层后下一道升格为湮光巨束：体格更大、贯穿 +3，出膛自带轰鸣
    /// </summary>
    internal class GsBeamSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.BeamSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsBeamSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: every slash casts a beam wave; beam hits charge the blade, and at three charges the next beam erupts into a vast annihilating lance, wider, brighter, piercing deeper";
        internal static readonly Color IonBright = new(214, 232, 255); //光刃白蓝
        internal static readonly Color IonBlue = new(110, 150, 240);   //幽蓝体色
        internal static readonly Color IonViolet = new(190, 160, 255); //湮光紫白

        /// <summary>蓄光层数（0~3）；跨玩家共享单例，只在 myPlayer 守门路径读写</summary>
        internal int Charge;

        //底伤不加成（原版 52/useAnim20 每挥一发全伤光束）：刀身拍均 1.07x + 光束 0.8x，
        //三层蓄光（光束命中攒）后下一道升格 1.4x 湮光巨束（贯穿 6），升格增益摊进循环；
        //按三拍循环约 53 帧摊算，贴脸（刀+束齐中）约原版 117%、纯刃外约 113%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 湮光刃手持：三拍快剑。0 顺斩 / 1 返斩（音调上扬）/ 2 重斩（小前压）。
    /// 每拍斩切爆发发出光束波；蓄满三层时 owner 侧把下一道升格为湮光巨束
    /// （远端靠生成包看到正确形态）。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsBeamSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.BeamSword;
        protected override Color EdgeBright => GsBeamSword.IonBright;
        protected override Color BodyMain => GsBeamSword.IonBlue;
        protected override Color HotAccent => GsBeamSword.IonViolet;

        private bool beamFired;

        private GsBeamSword Scheme =>
            GodSmithScheme.TryGetScheme(SwordItemID, out GodSmithScheme s) ? s as GsBeamSword : null;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 顺斩
            0 => new GsBroadBeat {
                Raise = 5, Hold = 1, Slash = 3, Recover = 7,
                RaiseBack = 1.75f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.04f,
                DamageMult = 1.0f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.0f,
            },
            //拍1 返斩
            1 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 7,
                RaiseBack = 1.8f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 1.0f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.1f,
            },
            //拍2 重斩：小前压
            _ => new GsBroadBeat {
                Raise = 7, Hold = 2, Slash = 4, Recover = 9,
                RaiseBack = 2.1f, Follow = 1.2f, ReachScale = 1.1f, LeanAmp = 0.07f,
                DamageMult = 1.2f, Hitstop = 2, LungeSpeed = 2.0f, SwingPitch = -0.2f,
            },
        };

        //==================== 湮光演出 ====================

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.75f, Pitch = Beat.SwingPitch }, Owner.Center);
            //光刃嗡鸣
            SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.3f, Pitch = 0.3f }, Owner.Center);
        }

        /// <summary>每拍斩切爆发发光束；蓄满三层则升格湮光巨束（owner 决策，束形态随生成包过线）</summary>
        protected override void OnSlashBegin() {
            if (beamFired) {
                return;
            }
            beamFired = true;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.38f, Pitch = 0.2f + 0.06f * ComboStage }, Owner.Center);
            }
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            GsBeamSword scheme = Scheme;
            bool annihilate = scheme != null && scheme.Charge >= 3;
            if (annihilate) {
                scheme.Charge = 0;
            }
            Vector2 dir = baseAngle.ToRotationVector2();
            int dmg = Math.Max(1, (int)(Projectile.damage * (annihilate ? 1.4f : 0.8f)));
            SpawnOwnedProj(ModContent.ProjectileType<GsBeamSwordBeamProj>(),
                Hand + dir * (FullReach * 0.7f), dir * (annihilate ? 16f : 15f), dmg,
                Projectile.knockBack * 0.5f, swingDir, annihilate ? 1f : 0f);
        }
    }

    /// <summary>
    /// 光束波：用原版光束剑光束贴图。出膛 16 帧减速回稳后滑行；
    /// 命中为刃身蓄光（owner 记账）。ai[0]=挥动符号 ai[1]=湮光旗
    /// （体格更大、贯穿 6、湮光巨束命中不再蓄光、出膛自带轰鸣）
    /// </summary>
    internal class GsBeamSwordBeamProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SwordBeam;

        private bool Annihilate => Projectile.ai[1] > 0.5f;
        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 44;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 22;
            Projectile.timeLeft = 46;
        }

        public override void AI() {
            Life++;
            if (Life == 1f) {
                if (Annihilate) {
                    //湮光巨束：贯穿 +3、体格更大、出膛轰鸣
                    Projectile.penetrate = 6;
                    Projectile.Resize(60, 60);
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.7f, Pitch = -0.3f }, Projectile.Center);
                        SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.5f, Pitch = 0.0f }, Projectile.Center);
                    }
                }
            }

            //出膛减速回稳：前 16 帧 15~16 → 约 11，尾段收速
            if (Life <= 16f) {
                Projectile.velocity *= 0.978f;
            }
            else if (Projectile.timeLeft < 10) {
                Projectile.velocity *= 0.95f;
            }
            //原版光束贴图为斜向刀形，补 45 度
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
        }

        /// <summary>命中蓄光（湮光巨束不再蓄，防永动）；满层时 owner 听到候发提示音</summary>
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!Annihilate && Projectile.owner == Main.myPlayer
                && GodSmithScheme.TryGetScheme(ItemID.BeamSword, out GodSmithScheme s) && s is GsBeamSword scheme) {
                int old = scheme.Charge;
                scheme.Charge = Math.Min(3, scheme.Charge + 1);
                if (old < 3 && scheme.Charge == 3 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.45f, Pitch = 0.4f }, Main.player[Projectile.owner].Center);
                }
            }
        }
    }
}
