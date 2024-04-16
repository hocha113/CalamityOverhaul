using CalamityOverhaul.Content.UIs.Core;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Common
{
    internal class ILMainMenuModification
    {
        internal static List<BaseMainMenuOverUI> MainMenuOverUIInstances;

        public static void HanderLoadMenuOverUI() {
            MainMenuOverUIInstances = new List<BaseMainMenuOverUI>();
            List<Type> rItemIndsTypes = CWRUtils.GetSubclasses(typeof(BaseMainMenuOverUI));
            foreach (Type type in rItemIndsTypes) {
                if (type != typeof(BaseMainMenuOverUI)) {
                    object obj = Activator.CreateInstance(type);
                    if (obj is BaseMainMenuOverUI inds) {
                        inds.Load();
                        MainMenuOverUIInstances.Add(inds);
                    }
                }
            }
        }

        public static void ILMenuSaveDrawFunc() {
            IL_Main.DrawMenu += context => {
                var potlevel = new ILCursor(context);

                if (!potlevel.TryGotoNext(
                    i => i.MatchLdsfld(typeof(Main), nameof(Main.spriteBatch)),
                    i => i.Match(OpCodes.Ldc_I4_0),
                    i => i.MatchLdsfld(typeof(BlendState), nameof(BlendState.AlphaBlend)),
                    i => i.MatchLdsfld(typeof(Main), nameof(Main.SamplerStateForCursor)),
                    i => i.MatchLdsfld(typeof(DepthStencilState), nameof(DepthStencilState.None)),
                    i => i.MatchLdsfld(typeof(RasterizerState), nameof(RasterizerState.CullCounterClockwise)),
                    i => i.Match(OpCodes.Ldnull),
                    i => i.MatchCall(typeof(Main), $"get_{nameof(Main.UIScaleMatrix)}")
                )) {
                    throw new Exception($"{nameof(ILMainMenuModification)}: IL 挂载失败，是否是目标流已经更改或者移除框架?");
                }

                potlevel.EmitDelegate(() => Draw(Main.spriteBatch));
            };
        }

        /// <summary>
        /// 该类是客户端内容，因此，不要在服务器上调用这个加载函数
        /// </summary>
        public static void Load() {
            HanderLoadMenuOverUI();
            ILMenuSaveDrawFunc();
        }

        public static void Unload() => MainMenuOverUIInstances = null;

        private static void Draw(SpriteBatch sb) {
            if (Main.gameMenu && MainMenuOverUIInstances != null && MainMenuOverUIInstances.Count > 0) {
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
                foreach (BaseMainMenuOverUI baseMainMenuOverUI in MainMenuOverUIInstances) {
                    baseMainMenuOverUI.Update(Main.gameTimeCache);
                    baseMainMenuOverUI.Draw(sb);
                }
                sb.End();
            }
        }
    }
}
