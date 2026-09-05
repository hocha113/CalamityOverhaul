using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【钛影残身】材质：银灰钛钢冷锻的闪避金属（呼应钛金套的影护）。
    /// 签名：①每一斩在原地留下一道钛影残身，凝滞半拍后重演同一道斩击
    /// ②第二拍是「短斩」：收着打的短促快斩，把重量留给终结拍
    /// ③残身重演时嘶鸣
    /// </summary>
    internal class GsTitaniumSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.TitaniumSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsTitaniumSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: every slash leaves a titanium shade hanging in the air that replays the same cut half a beat later";
        internal static readonly Color TiBright = new(222, 230, 244); //钛亮银
        internal static readonly Color TiMain = new(148, 164, 196);   //钛冷灰
        internal static readonly Color TiHot = new(176, 208, 255);    //冷光泛蓝

        //预算账：拍均 (0.95+0.9+1.25)/3≈1.03；每斩钛影 0.22x 半拍后同弧重演
        //（单体重取 ~0.8 → +0.18/拍）；连段总帧 (19+18+25)=62 对原版 60 (+3%)
        //→ 综合单体 DPS ≈ (1.03+0.18)×0.97 ≈ 原版 103%~117%，底伤不再加成
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 钛影残身手持：三拍利落连击（0 正斩 / 1 短斩：收着打 / 2 追影终结：大弧重斩叠上残身）。
    /// 每拍斩切爆发都在原地留钛影残身，玩家本体不位移。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsTitaniumSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.TitaniumSword;
        protected override Color EdgeBright => GsTitaniumSword.TiBright;
        protected override Color BodyMain => GsTitaniumSword.TiMain;
        protected override Color HotAccent => GsTitaniumSword.TiHot;

        private bool shadeFired;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 正斩：干脆利落
            0 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 3, Recover = 9,
                RaiseBack = 1.75f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.05f,
            },
            //拍1 短斩：短促收着打
            1 => new GsBroadBeat {
                Raise = 4, Hold = 2, Slash = 3, Recover = 9,
                RaiseBack = 1.6f, Follow = 0.9f, ReachScale = 0.96f, LeanAmp = 0.04f,
                DamageMult = 0.9f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.08f,
            },
            //拍2 追影终结：大弧重斩，叠上残身
            _ => new GsBroadBeat {
                Raise = 7, Hold = 3, Slash = 4, Recover = 11,
                RaiseBack = 2.1f, Follow = 1.3f, ReachScale = 1.12f, LeanAmp = 0.08f,
                DamageMult = 1.25f, Hitstop = 2, LungeSpeed = 0f, SwingPitch = -0.22f,
            },
        };

        /// <summary>每拍留影（owner 端生成，随生成包过线）</summary>
        protected override void OnSlashBegin() {
            if (!shadeFired) {
                shadeFired = true;
                float startAng = ArcStart - (swingDir * 0.08f);
                int shadeDamage = Math.Max(1, (int)(Projectile.damage * 0.22f));
                SpawnOwnedProj(ModContent.ProjectileType<GsTitaniumSwordShadeProj>(), Hand, Vector2.Zero,
                    shadeDamage, Projectile.knockBack * 0.3f, startAng, ArcEnd, FullReach);
            }
        }

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.32f, Pitch = -0.35f }, Owner.Center);
            }
        }

        /// <summary>命中：冷冽钛音</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.35f, Pitch = 0.3f, MaxInstances = 3 }, target.Center);
            }
        }
    }

    /// <summary>
    /// 钛影残身：挥砍留在原地的半透明刀影。凝滞 9 帧悬在起手角，
    /// 随后 5 帧沿同弧重演斩击（0.22x），再留 8 帧判定尾巴后消亡。
    /// 本体只用原版剑贴图半透明一笔。ai[0]=弧起角 ai[1]=弧止角 ai[2]=触及半径
    /// </summary>
    internal class GsTitaniumSwordShadeProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.TitaniumSword");

        private const int DormantFrames = 9;
        private const int SweepFrames = 5;
        private const int TotalFrames = 22;

        private float ArcFrom => Projectile.ai[0];
        private float ArcTo => Projectile.ai[1];
        private float Reach => Projectile.ai[2];
        private ref float Life => ref Projectile.localAI[0];

        /// <summary>重演行程 0~1</summary>
        private float SweepP(float life) =>
            MathHelper.Clamp((life - DormantFrames) / SweepFrames, 0f, 1f);

        /// <summary>行程角：与真刃同源的爆发缓动（快出缓收）</summary>
        private float AngleAt(float p) {
            float eased = 1f - MathF.Pow(1f - p, 3f);
            return MathHelper.Lerp(ArcFrom, ArcTo, eased);
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = TotalFrames;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            if (Life == DormantFrames + 1 && !VaultUtils.isServer) {
                //残身出刀：金属嘶鸣 + 更薄的挥音
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.35f, Pitch = 0.35f, MaxInstances = 3 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.4f, Pitch = 0.3f }, Projectile.Center);
            }
        }

        //只在重演期结算伤害
        public override bool? CanDamage() =>
            Life > DormantFrames && Life <= DormantFrames + SweepFrames + 1 ? null : false;

        /// <summary>本帧扫过的角度区间逐段采样（半径 35%~102% 的线段）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float pPrev = SweepP(Life - 1f);
            float pCur = SweepP(Life);
            Vector2 center = Projectile.Center;
            float collisionPoint = 0f;
            const int steps = 4;
            for (int i = 0; i <= steps; i++) {
                float ang = AngleAt(MathHelper.Lerp(pPrev, pCur, i / (float)steps));
                Vector2 dir = ang.ToRotationVector2();
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    center + dir * (Reach * 0.35f), center + dir * (Reach * 1.02f), 28f, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.25f, Pitch = 0.5f, MaxInstances = 3 }, target.Center);
        }

        /// <summary>幽灵刀身朝向：与真刃同一套刃口镜像规则（由弧向与面向反推）</summary>
        private void GetGhostOrientation(out SpriteEffects effect, out float rotOffset) {
            float sweepSign = MathF.Sign(ArcTo - ArcFrom);
            int facing = MathF.Cos(MathHelper.Lerp(ArcFrom, ArcTo, 0.5f)) >= 0f ? 1 : -1;
            bool flipVertically = (facing < 0) != (sweepSign < 0);
            effect = flipVertically ? SpriteEffects.FlipVertically : SpriteEffects.None;
            rotOffset = flipVertically ? -MathHelper.PiOver4 : MathHelper.PiOver4;
        }

        /// <summary>本体一笔：原版剑贴图按当前弧角摆在留影位，半透明标识残身；重演结束后不再绘制</summary>
        public override bool PreDraw(ref Color lightColor) {
            if (Life > DormantFrames + SweepFrames) {
                return false;
            }
            Main.instance.LoadItem(ItemID.TitaniumSword);
            Texture2D blade = TextureAssets.Item[ItemID.TitaniumSword].Value;
            GetGhostOrientation(out SpriteEffects effect, out float rotOffset);
            //刀形贴图缩放与真刃同一换算（BladePark 0.46 / BladeTipFill 1.02）
            float scale = Reach * (1.02f - 0.46f) * 2f / MathF.Max(new Vector2(blade.Width, blade.Height).Length(), 1f);
            float ang = AngleAt(SweepP(Life));
            Vector2 at = Projectile.Center + ang.ToRotationVector2() * (Reach * 0.46f) - Main.screenPosition;
            Main.EntitySpriteDraw(blade, at, null, lightColor * 0.5f, ang + rotOffset, blade.Size() * 0.5f, scale, effect, 0);
            return false;
        }
    }
}
