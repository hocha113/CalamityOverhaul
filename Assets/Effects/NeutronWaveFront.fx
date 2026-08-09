// ============================================================================
//NeutronWaveFront.fx 黑域斩切·引力波剑气
//材质是"时空曲率"，不是发光：本体暗，亮度只给焦散线与被拖曳的星点
//三股异质条带共用本 effect，靠顶点色 B 通道分股
//UV.x 沿月牙 0=一侧翼尖 1=另一侧翼尖（按累计弧长归一）
//UV.y 跨波带 0=波前锋面 1=尾迹末端
//顶点色 R=z(0远~1近) G=振幅 B=股序(0焦暗衬/0.5主波/1焦散芯) A=不透明度
//预乘输出走 AlphaBlend；直线算术、无动态分支、只用 tex2D
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uFade;     //整体存活度 0~1
float uPower;    //威力档 0轻拍~1终结
float uLife;     //生命进度 0~1，驱动振幅衰减与展开
float uBirth;    //出生压缩闪 0~1，仅头几帧

texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;      //沿月牙
    float across = uv.y;     //0=波前 1=尾
    float z = input.Color.r;
    float amp = input.Color.g;
    float strand = input.Color.b;
    float opacity = input.Color.a;

    //股掩码，用平滑窗代替分支
    float mBack = 1.0 - smoothstep(0.0, 0.30, strand);
    float mMain = 1.0 - smoothstep(0.0, 0.34, abs(strand - 0.5));
    float mCore = smoothstep(0.72, 1.0, strand);

    //两翼收尖，波峰在中段
    float wing = pow(saturate(sin(3.14159 * along)), 0.62);
    //边界保险：跨带末端与两翼必须自然归零，否则平滑边被几何切断
    float guard = wing * (1.0 - smoothstep(0.86, 1.0, across));

    //---- 涟漪列：引力波不是一道，是一列振幅衰减的波峰 ----
    //chirp：靠近波前排得密，向尾部拉疏
    float q = pow(across, 0.70);
    float phase = q * (11.0 + 5.0 * uPower) - uTime * 2.6;
    float crest = 0.5 + 0.5 * cos(phase);
    crest = pow(crest, 2.3);
    //振幅随离开波前的距离指数衰减，也随寿命整体走低
    float decay = exp(-across * 3.0) * (1.0 - 0.45 * uLife);
    float ripple = crest * decay;

    //---- 被拖曳的星场：背景星点沿弧被拉成丝，显示空间在被拖动 ----
    float2 starUV = float2(along * 3.4 - uTime * 0.06, across * 0.4 + uTime * 0.015);
    float s1 = tex2D(noiseSamp, starUV).r;
    float s2 = tex2D(noiseSamp, starUV + float2(0.05, 0.0)).r;
    float s3 = tex2D(noiseSamp, starUV + float2(0.10, 0.0)).r;
    float stars = smoothstep(0.87, 0.985, s1);
    stars = max(stars, smoothstep(0.90, 0.995, s2) * 0.62);
    stars = max(stars, smoothstep(0.92, 0.999, s3) * 0.34);
    stars *= decay;

    //---- 焦散锋线：透镜焦散，极小面积、最高对比，白是结构不是增益 ----
    float causticD = abs(across - 0.045);
    float caustic = saturate(1.0 - causticD / 0.05);
    caustic = caustic * caustic * caustic;
    //出生几帧的压缩过曝，由消费端时间包络给，不在这里常驻
    caustic *= 0.55 + 0.45 * uPower + uBirth * 1.6;

    //噪声只咬边，不啃穿波面
    float grain = tex2D(noiseSamp, float2(along * 2.1 + uTime * 0.11, across * 1.7 - uTime * 0.23)).r;
    float bite = 1.0 - 0.34 * smoothstep(0.58, 0.14, grain);

    //---- 相对论色移：波前蓝移、尾迹红移，给波带一根不对称的色轴 ----
    float3 cBlue = float3(0.72, 0.90, 1.00);
    float3 cMid = float3(0.40, 0.23, 0.95);
    float3 cRed = float3(0.36, 0.045, 0.15);
    float3 spectrum = lerp(cBlue, cMid, smoothstep(0.0, 0.30, across));
    spectrum = lerp(spectrum, cRed, smoothstep(0.34, 1.0, across));

    //焦暗衬底：近黑紫，给整道波一个能在亮天空下站住的暗身
    float3 cDark = float3(0.045, 0.014, 0.125);
    float3 cHot = float3(0.90, 0.95, 1.00);

    //---- 三股各自的强度 ----
    //衬带：宽而暗，只负责剪影，几乎不发光
    float aBack = guard * (0.30 + 0.34 * decay) * bite;
    //主波：涟漪列 + 星丝
    float aMain = guard * (ripple * 0.88 + 0.12 * decay) * bite;
    //焦散芯：只有锋线
    float aCore = guard * caustic;

    float alpha = (aBack * mBack + aMain * mMain + aCore * mCore) * amp * opacity;

    //远半侧压暗，与母刀光同一个 z 通道约定
    float depthDim = 0.55 + 0.45 * z;

    float3 color = cDark * (aBack * mBack) * 1.6;
    color += spectrum * (aMain * mMain) * 0.85;
    color += cHot * stars * mMain * (0.55 + 0.45 * uPower) * guard;
    color += cHot * (aCore * mCore) * 1.35;
    color *= depthDim;

    alpha = saturate(alpha) * uFade;

    return float4(color * alpha, alpha);
}

technique Technique1
{
    pass NeutronWaveFrontPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
