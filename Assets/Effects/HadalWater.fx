// ============================================================================
//HadalWater.fx 深渊海沟水体合成滤镜(单 technique 单 pass,Filters.Scene)
//一趟收敛四件事:浑浊纱(随深度渐变的水色收敛)+丁达尔光束(日光带)
//+黑暗呼吸(CPU 折进浑浊度)+超深渊暗角。拷屏改写,压暗合法(非加色批)
//直线算术、无分支、无 atan2,噪声全走绑定贴图(FNA3D 翻译纪律)
//uniform 是设备全局状态:调用点每帧全参数重设(HadalAmbience.ApplyFilterUniforms)
// ============================================================================

sampler uScreen : register(s0);   //滤镜拷屏
sampler uNoise : register(s1);    //Masking/PerlinNoise 512²,LinearWrap;G 通道实测域 0.22~0.776

float2 uScreenSize;    //目标像素尺寸
float2 uWorldScale;    //目标px→世界px 仿射(EndCapture 已预翻重力,只逆 ZoomMatrix)
float2 uWorldOffset;
float uSeaLevelPx;     //海面行世界 y(px),线上不施水效
float3 uVeilTop;       //屏顶浑浊纱色/浑浊度(CPU 按屏顶世界深度采样)
float uTurbTop;
float3 uVeilBottom;    //屏底同上
float uTurbBottom;
float3 uRayColor;      //光束色(色值即强度,拷屏改写无混合语义)
float uRayStrength;    //光束强度(相机深度采样,暮光带中部归零)
float uRayFadeInv;     //光束深度衰减:1/(海面→暮光中部的像素跨度)
float uVignette;       //暗角强度(午夜带起)
float uPresence;       //全局淡入淡出
float uTime;

//PerlinNoise G 通道域校准:实测 0.22~0.776 → 0..1(禁高分位死阈值)
float nrm(float g) {
    return saturate((g - 0.30) * 2.6);
}

float4 PSHadalFilter(float2 uv : TEXCOORD0) : COLOR0 {
    float4 src = tex2D(uScreen, uv);
    float2 world = uWorldOffset + uv * uScreenSize * uWorldScale;

    //水线:世界锚定微摆,线下 26px 羽化入水(线上原样保留海面画面)
    float wob = (tex2D(uNoise, float2(world.x * 0.00042, uTime * 0.013)).g - 0.5) * 22.0;
    float under = saturate((world.y - (uSeaLevelPx + wob)) / 26.0);

    //浑浊纱:屏顶/屏底两采样点按 uv.y 线性插值(屏幕纵跨占世界高度 ~1.5%,线性即光滑)
    float3 veil = lerp(uVeilTop, uVeilBottom, uv.y);
    float turb = lerp(uTurbTop, uTurbBottom, uv.y);

    //水团翻涌:世界锚定双倍频缓滚,浑浊度 ±18% 起伏(水是活的)
    float2 wuv = world * 0.00058;
    float m1 = tex2D(uNoise, wuv + float2(uTime * 0.0045, uTime * 0.009)).g;
    float m2 = tex2D(uNoise, wuv * 2.31 + float2(-uTime * 0.007, uTime * 0.014)).g;
    float murk = nrm(m1 * 0.62 + m2 * 0.38);
    turb = saturate(turb * (0.82 + 0.36 * murk));

    float3 col = lerp(src.rgb, veil, turb * under * uPresence);

    //丁达尔光束:斜向世界锚定条带,主频取形+副频破匀,随深度衰减
    //稀疏度靠高次幂(pow 4.2),禁阈值裁剪(域校准后仍留连续尾)
    float slant = 0.11 + 0.05 * sin(uTime * 0.21);
    float rayCoord = (world.x + world.y * slant) * 0.0030;
    float b1 = nrm(tex2D(uNoise, float2(rayCoord * 0.17, 0.23 + uTime * 0.0035)).g);
    float b2 = nrm(tex2D(uNoise, float2(rayCoord * 0.41 + uTime * 0.006, 0.67)).g);
    float beam = pow(b1, 4.2) * (0.45 + 0.55 * b2) * 1.2;
    float depthFade = pow(saturate(1.0 - (world.y - uSeaLevelPx) * uRayFadeInv), 1.6);
    col += uRayColor * (beam * depthFade * under * uRayStrength * uPresence);

    //暗角:屏缘按等比距离压暗(超深渊带的视野收缩)
    float2 c = (uv - 0.5) * float2(uScreenSize.x / uScreenSize.y, 1.0);
    float vig = smoothstep(0.52, 1.05, length(c) * 1.35) * uVignette * uPresence;
    col *= 1.0 - vig;

    return float4(col, src.a);
}

technique TechHadal {
    pass HadalFilter {
        PixelShader = compile ps_3_0 PSHadalFilter();
    }
}
