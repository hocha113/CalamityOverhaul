// ============================================================================
//HadalSky.fx 深渊海沟远景天幕(单 technique 单 pass,CustomSky 全屏 quad)
//分带渐变深海底色 + 低频水团斑驳 + 浅带远景光柱残余;海面线以上 alpha 归零
//保留原版海天。预乘输出进 AlphaBlend 批。
//直线算术、无分支、无 atan2,噪声全走绑定贴图(FNA3D 翻译纪律)
// ============================================================================

sampler uScreen : register(s0);   //白像素画布(不采样,占位保 s0 语义)
sampler uNoise : register(s1);    //Masking/PerlinNoise 512²,LinearWrap;G 通道实测域 0.22~0.776

float uIntensity;      //天幕淡入淡出
float uTime;
float2 uScreenSize;
float3 uColTop;        //屏顶/屏底背景水色(CPU 按世界深度采样)
float3 uColBottom;
float uSeaLineUv;      //海面线屏幕 uv(线上 alpha 归零;线在屏顶上方时为负=全屏接管)
float2 uNoiseAnchor;   //视差锚(真实相机 × 0.5,远景比玩家水层更远)
float uWorldPerPx;     //屏幕px→视差世界px
float uShaftStrength;  //远景光柱强度(浅带专属,与滤镜光束同源渐灭)
float3 uShaftTint;
float uDeepMottle;     //水团斑驳幅度

//PerlinNoise G 通道域校准:实测 0.22~0.776 → 0..1
float nrm(float g) {
    return saturate((g - 0.30) * 2.6);
}

float4 PSHadalSky(float2 uv : TEXCOORD0) : COLOR0 {
    float2 wpx = uNoiseAnchor + uv * uScreenSize * uWorldPerPx;

    //分带渐变底色
    float3 col = lerp(uColTop, uColBottom, uv.y);

    //深海水团:双倍频低频缓漂明度斑驳(黑不是均匀的黑)
    float n1 = tex2D(uNoise, wpx * 0.00021 + float2(uTime * 0.0021, uTime * 0.0009)).g;
    float n2 = tex2D(uNoise, wpx * 0.00063 + float2(-uTime * 0.0032, uTime * 0.0015)).g;
    float mot = nrm(n1 * 0.6 + n2 * 0.4);
    col *= lerp(1.0 - uDeepMottle, 1.0 + uDeepMottle * 0.6, mot);

    //远景光柱:x 低频竖带缓移(浅带里更远处的丁达尔,比滤镜光束虚)
    float shaft = nrm(tex2D(uNoise, float2(wpx.x * 0.00011 + uTime * 0.0018, 0.37)).g);
    col += uShaftTint * (pow(shaft, 2.4) * uShaftStrength);

    //海面线以上让位原版海天(羽化带 ~6.5% 屏高)
    float a = uIntensity * smoothstep(uSeaLineUv - 0.015, uSeaLineUv + 0.05, uv.y);
    return float4(col * a, a);
}

technique TechHadalSky {
    pass SkyPass {
        PixelShader = compile ps_3_0 PSHadalSky();
    }
}
