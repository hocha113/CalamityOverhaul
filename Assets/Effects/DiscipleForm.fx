// ============================================================================
//DiscipleForm.fx 天国极乐·门徒圣像幽灵
//材质：彩窗玻璃圣像 — 兜帽圣袍剪影(近实体暗底)，体内竖向透光，
//袍摆被噪声撕成金屑拖曳，头顶光环呼吸，殉道时通体燃金后蚀散
//一支承十二门徒：身份差异全走 uniform(身份色/种子/体型由C#画布控制)
//采样 s0 白像素画布 + s1 PerlinNoise(实测值域G 0.22~0.78,阈值前过nrm)
//预乘输出，进 AlphaBlend；ps_3_0
// ============================================================================

float uTime;
float fadeAlpha;         //整体透明度
float uSeed;             //门徒相位种子(错开呼吸/噪声)
float3 bodyColor;        //身份色(袍体)
float3 accentColor;      //亮饰色(缘光/光环/内光)
float uHaloFlare;        //光环燃亮 0~1(施放能力瞬间)
float2 uMotion;          //移动拖曳(UV空间,指向运动反方向)
float uDissolve;         //殉道侵蚀 0~1(通体燃金→蚀散成光屑)
float uEmerge;           //出场成形 0~1(0=未成形,自光屑聚拢)

texture uNoiseTex;
sampler noiseSamp : register(s1) = sampler_state
{
    texture   = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU  = wrap;
    AddressV  = wrap;
};

sampler baseSamp : register(s0);

#define PI  3.14159265
#define TAU 6.28318530

//绑定噪声实测值域归一
float nrm(float raw)
{
    return saturate((raw - 0.22) / 0.56);
}

struct PSInput
{
    float4 Position  : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color     : COLOR0;
};

float4 DiscipleFormPS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float3 col = 0;
    float alpha = 0;

    //=
    //1. 身形蒙版：兜帽 + 圣袍(上窄下阔的钟形)，袍摆随呼吸与移动轻摆
    //=
    float2 headC = float2(0.5, 0.315);

    //袍摆摆动：呼吸性侧摆 + 移动拖曳(下摆吃满,肩部几乎不动)
    float hemT = smoothstep(0.40, 0.80, uv.y);
    float sway = sin(uTime * 1.25 + uSeed) * 0.014 * hemT;
    float dragX = uMotion.x * hemT * 0.55;
    float cx = uv.x - 0.5 - sway - dragX;

    //袍身半宽：肩窄摆阔
    float halfw = lerp(0.078, 0.178, pow(smoothstep(0.355, 0.80, uv.y), 1.2));
    float robeBand = smoothstep(0.355, 0.40, uv.y) * (1.0 - smoothstep(0.815, 0.85, uv.y));
    float robeMask = robeBand * smoothstep(0.014, -0.010, abs(cx) - halfw);

    //兜帽(头罩外轮廓)
    float hoodDist = length((uv - headC) * float2(1.0, 1.08));
    float hoodMask = smoothstep(0.006, -0.008, hoodDist - 0.098);

    float body = max(robeMask, hoodMask);

    //=
    //2. 成形/殉道侵蚀：噪声阈值切碎，殉道自上而下燃金蚀散，
    //   出场反放(自光屑聚拢成形)；下摆常态微碎
    //=
    float2 nUv = uv * 2.3 + uSeed * 0.37 + float2(uMotion.x * 0.3, uTime * 0.035);
    float nE = nrm(tex2D(noiseSamp, nUv).g);
    float hemErode = smoothstep(0.60, 0.86, uv.y) * 0.55;
    float dissolveBias = uDissolve * (1.6 - uv.y);   //殉道自头顶先蚀
    float erosion = saturate(hemErode + dissolveBias * 1.3 + (1.0 - uEmerge) * 1.7);
    float keep = smoothstep(erosion - 0.16, erosion + 0.06, nE);
    float bodyKept = body * keep;

    //蚀缘光屑带：紧贴侵蚀前沿的窄带亮成金屑
    float edgeBand = smoothstep(erosion - 0.30, erosion - 0.16, nE)
                   * (1.0 - smoothstep(erosion - 0.16, erosion - 0.02, nE));
    float motes = body * edgeBand;

    //=
    //3. 袍体着色：暗琉璃底 + 彩窗竖向透光 + 缘光
    //=
    float3 robeBase = bodyColor * 0.30;

    //彩窗透光：竖向光带缓慢流动
    float bands = nrm(tex2D(noiseSamp, float2(uv.x * 3.4 + uSeed, uv.y * 0.85 - uTime * 0.045)).g);
    float bandLight = smoothstep(0.35, 0.85, bands) * smoothstep(0.38, 0.55, uv.y);
    col += robeBase;
    col += bodyColor * bandLight * 0.38;

    //胸口徽位底光(圣徽由CPU线稿叠画)
    float chest = exp(-dot(uv - float2(0.5, 0.485), uv - float2(0.5, 0.485)) * 240.0);
    col += accentColor * chest * 0.32;

    //缘光：身形边缘一线受光，头肩处最亮
    float edgeIn = saturate((halfw - abs(cx)) / 0.035);
    float rim = robeBand * (1.0 - edgeIn);
    float hoodRim = smoothstep(-0.024, -0.004, hoodDist - 0.098) * hoodMask;
    float rimLight = max(rim * 0.7, hoodRim) * (0.75 + 0.25 * sin(uTime * 1.8 + uSeed));
    col += accentColor * rimLight * (0.78 - uv.y * 0.35);

    //=
    //4. 兜帽下的面庞暗空
    //=
    float faceDist = length((uv - float2(0.5, 0.332)) * float2(1.0, 1.15));
    float faceMask = smoothstep(0.004, -0.006, faceDist - 0.056);
    col = lerp(col, bodyColor * 0.10, faceMask);

    //=
    //5. 殉道燃金：蚀散前通体先燃向亮金
    //=
    col = lerp(col, accentColor * 1.25, saturate(uDissolve * 0.7));

    col *= bodyKept;
    alpha = bodyKept * 0.9;

    //光屑带(加在身体之外)
    col += accentColor * motes * 1.1;
    alpha += motes * 0.7;

    //=
    //6. 头顶光环：细环呼吸，施放/殉道时燃亮
    //=
    float2 haloC = float2(0.5, 0.175);
    float haloDelta = length(uv - haloC) - 0.105;
    float haloFlare = saturate(uHaloFlare + uDissolve * 0.8);
    float halo = exp(-haloDelta * haloDelta * (7000.0 - haloFlare * 3200.0));
    float haloPulse = 0.66 + 0.34 * sin(uTime * 2.1 + uSeed * 1.4);
    float haloStrength = halo * (haloPulse * 0.8 + haloFlare * 1.0) * uEmerge;
    col += accentColor * haloStrength;
    alpha += haloStrength * 0.35;

    //=
    //整体衰减 + 画布边界保险
    //=
    float2 cFromCenter = uv - 0.5;
    float guard = 1.0 - smoothstep(0.44, 0.5, length(cFromCenter));
    col *= fadeAlpha * guard;
    alpha = saturate(alpha) * fadeAlpha * guard;

    return float4(col, alpha);
}

technique DiscipleForm
{
    pass P0
    {
        PixelShader = compile ps_3_0 DiscipleFormPS();
    }
};
