using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.EvilBiome.Projectiles
{
    /// <summary>
    /// 扑咬蓄势预兆:接触系三型扑咬的前摇可见信号,贴身跟随施法者,
    /// 在所有端渲染压身尘与渐涨蓄势辉(客户端可见状态来自本同步实体,不读服务端私产)。
    /// 自然到点=出手瞬间,死亡帧各端播出手爆点与音效;被提前击杀(中止路径)则静默。
    /// 来源校验镜像沙锥:施法者死亡/槽位复用即自灭,不留无主预告。
    /// ai[0]=来源打包(槽位+1|类型&lt;&lt;8) ai[1]=运动型 ai[2]=出生档位;永不造成伤害
    /// </summary>
    internal class SiphonPounceOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>出手对齐尾帧:预兆比 NPC 前摇多活两帧,保证 NPC 提交帧回读仍在位</summary>
        private const int LaunchTail = 2;

        private int SrcPacked => (int)Projectile.ai[0];
        private int Style => (int)Projectile.ai[1];
        private int Flavor => EvilBiomeMobsNPC.SiphonFlavor(SrcPacked >> 8);
        private int Total => EvilBiomeMobsNPC.PounceWindupFrames(Style) + LaunchTail;
        private int Elapsed => Total - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 40;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体,永不造成伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        /// <summary>来源回读:槽位+类型双重校验(镜像沙锥),失效返回 null</summary>
        private NPC ResolveSource() {
            int src = (SrcPacked & 255) - 1;
            if (src < 0 || src >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[src];
            if (!npc.active || npc.type != SrcPacked >> 8) {
                return null;
            }
            return npc;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //各端本地按运动型对齐寿命(确定性推导,不依赖生成包之外的字段)
                Projectile.timeLeft = Total;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.3f, Pitch = -0.6f, MaxInstances = 4 }, Projectile.Center);
                }
            }

            NPC src = ResolveSource();
            if (src == null) {
                //施法者死亡/槽位复用:静默自灭(提前击杀路径不播出手爆点)
                Projectile.Kill();
                return;
            }
            Projectile.Center = src.Center;

            //各型前摇尘信号(客户端,≤2 粒/帧)
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                int dustType = EvilBiomeFX.DustFor(Flavor);
                switch (Style) {
                    case EvilBiomeMobsNPC.StyleGround: {
                        //蹲身:足底外踢的低平尘
                        Vector2 pos = src.Bottom + new Vector2(Main.rand.NextFloat(-0.5f, 0.5f) * src.width, -2f);
                        Dust crouch = Dust.NewDustPerfect(pos, dustType,
                            new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(0.2f, 0.8f)), 130, default, 0.9f);
                        crouch.noGravity = true;
                        break;
                    }
                    case EvilBiomeMobsNPC.StyleWall: {
                        //贴面:外缘向体心收拢的凝聚尘
                        Vector2 offset = Main.rand.NextVector2CircularEdge(src.width * 0.8f, src.height * 0.8f);
                        Dust cling = Dust.NewDustPerfect(src.Center + offset, dustType, -offset * 0.07f, 130, default, 1f);
                        cling.noGravity = true;
                        break;
                    }
                    default: {
                        //弓身:背脊上方的弧形拱尘
                        float arc = Main.rand.NextFloat(-1.1f, 1.1f);
                        Vector2 pos = src.Center + new Vector2(MathF.Sin(arc) * src.width * 0.7f,
                            -MathF.Cos(arc) * src.height * 0.65f);
                        Dust bow = Dust.NewDustPerfect(pos, dustType, (src.Center - pos) * 0.04f, 130, default, 0.95f);
                        bow.noGravity = true;
                        break;
                    }
                }
            }
            Lighting.AddLight(Projectile.Center, EvilBiomeFX.Bright(Flavor).ToVector3() * 0.2f);
        }

        public override void OnKill(int timeLeft) {
            //只有临近自然到点(=出手瞬间)才播爆点;中止路径的提前击杀静默。
            //留小容差:客户端生成包可能迟到 1~2 帧,服务端击杀包抵达时本地 timeLeft 未必恰为 0
            if (timeLeft > LaunchTail + 2 || VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, EvilBiomeFX.DustFor(Flavor),
                    Main.rand.NextVector2Circular(3f, 3f), 110, default, 1.2f);
                dust.noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.45f, Pitch = 0.15f, MaxInstances = 4 }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC src = ResolveSource();
            if (src == null) {
                return false;
            }
            Texture2D tex = CWRAsset.Extra_98.Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = src.Center - Main.screenPosition;
            float progress = MathHelper.Clamp(Elapsed / (float)Total, 0f, 1f);
            //蓄势辉:暗层(A>0)+亮芯(加色),随临出手渐涨渐急
            float pulse = 1f + 0.15f * MathF.Sin(Elapsed * (0.35f + 0.4f * progress));
            float scale = (src.width + 30f) / tex.Width * (0.5f + 0.5f * progress) * pulse;
            Main.EntitySpriteDraw(tex, pos, null, EvilBiomeFX.Deep(Flavor) * (0.5f * progress),
                0f, origin, scale * 1.18f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, EvilBiomeFX.Bright(Flavor) with { A = 0 } * (0.45f * progress),
                0f, origin, scale * 0.7f, SpriteEffects.None, 0);
            return false;
        }
    }
}
