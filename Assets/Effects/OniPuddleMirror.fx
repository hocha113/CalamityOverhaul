// ============================================================================
//OniPuddleMirror.fx 鬼雨立伞水洼倒影(屏幕空间局部镜像)
//TechPuddle:水洼椭圆内绕水面线垂直镜像采样拷屏(伞/玩家/雨一并入镜),
//          纵向压缩的伪透视(扁水洼里映出整把伞),噪声涟漪扰动+镜像染墨
//          +入水深度衰减+水线一线青沫;椭圆外原样输出
//坐标全笛卡尔;直线算术+普通 tex2D,FNA3D 安全;不透明回写(输出即主屏)
//s0=拷屏(批主纹理) s1=PerlinNoise(实测值域 0.22~0.776,只作对称扰动不作阈值)
//消费入口 Scenarios/OniRainWorlds/OniUmbrellaPuddleRender.cs
// ============================================================================

sampler uImage0 : register(s0);   //拷屏,批主纹理
sampler uImage1 : register(s1);

float uTime;
float2 uPuddleCenter;   //水洼中心(屏幕 uv);水面线即中心线
float2 uPuddleHalf;     //水洼半宽/半高(屏幕 uv)
float2 uScreenTexel;    //一屏幕像素的 uv,涟漪位移按像素度量
float uReflScale;       //伪透视压缩:洼内每深入 1px,镜像源向上走这么多 px
float uWobble;          //涟漪扰动强度
float uAlpha;           //整体强度
float3 uTint;           //墨底色
float3 uSheen;          //青灰湿亮

float4 PSPuddle(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    float4 src = tex2D(uImage0, uv);

    //洼内局部坐标:各轴按半径归一,<1 在椭圆内
    float2 d = (uv - uPuddleCenter) / max(uPuddleHalf, 0.00001);
    float ell = dot(d, d);
    float inside = 1.0 - smoothstep(0.55, 1.0, ell);
    //倒影只认水面线以下(uv.y 向下增长)
    float below = smoothstep(0.0, 0.35, d.y);
    float mask = inside * below * uAlpha;

    //入水深度 0~1:越深倒影越暗越晃
    float depth01 = saturate(d.y);

    //涟漪:洼内局部坐标喂平铺噪声,两频对称扰动(中心值 ~0.5,不作阈值)
    float n1 = tex2D(uImage1, float2(d.x * 0.33 + uTime * 0.05, d.y * 0.18 - uTime * 0.11)).r - 0.5;
    float n2 = tex2D(uImage1, float2(d.x * 0.71 - uTime * 0.08, d.y * 0.34 + uTime * 0.07)).r - 0.5;
    float ripple = (n1 + n2 * 0.6) * (0.4 + 0.6 * depth01) * uWobble;

    //绕水面线垂直镜像:纵向压缩的伪透视,涟漪推横向为主、纵向少许
    float2 muv;
    muv.x = uv.x + ripple * uScreenTexel.x * 3.5;
    muv.y = uPuddleCenter.y - (uv.y - uPuddleCenter.y) * uReflScale
          + ripple * uScreenTexel.y * 1.5;
    muv = clamp(muv, 0.002, 0.998);
    float3 refl = tex2D(uImage0, muv).rgb;

    //倒影染墨:压暗向墨色,亮部保辨识
    refl = lerp(refl, uTint, 0.40 + 0.30 * depth01);

    //水体先加深一层,再按反射率叠倒影(反射率随深度衰减)
    float reflK = (1.0 - depth01 * 0.68) * 0.62;
    float3 outCol = lerp(src.rgb, uTint, mask * 0.45);
    outCol = lerp(outCol, refl, mask * reflK);

    //水面线一线青沫:随涟漪明灭
    float shore = exp2(-d.y * d.y * 22.0) * inside * uAlpha;
    outCol += uSheen * shore * (0.10 + 0.10 * (n1 + 0.5));

    return float4(outCol, src.a);
}

technique TechPuddle {
    pass PuddlePass {
        PixelShader = compile ps_3_0 PSPuddle();
    }
}
