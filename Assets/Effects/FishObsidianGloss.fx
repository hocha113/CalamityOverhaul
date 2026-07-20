// ============================================================================
//FishObsidianGloss.fx 黑曜石鱼火山玻璃单趟着色（SpriteBatch Immediate 直绘鱼贴图）
//身份：深黑近剪影本体 + 窄镜面高光沿轮廓随 uLightDir 扫动 + 紫黑偏光带 + 余温矿脉
//轮廓高光 = 沿光向偏移采 alpha 的边缘差分，全笛卡尔坐标，无极角接缝问题
//输出预乘 alpha，配 BlendState.AlphaBlend
//ps_3_0
// ============================================================================

sampler uImage0 : register(s0);

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

float2 uLightDir;    //贴图空间单位光向，指向被照亮的轮廓侧
float2 uTexel;       //1/贴图尺寸
float uSpec;         //镜面强度 0..~3，glint 脉冲时抬高
float uSheenPhase;   //偏光带相位，随公转/时间滑移
float uCrack;        //余温矿脉强度 0..1，冻结碎裂时拉满
float uFlash;        //爆裂过曝 0..1，只允许 <=2 帧
float uFade;         //整体不透明度
float uSeed;         //每条鱼的噪声偏移
float uDepthDim;     //0 远 .. 1 近，伪 3D 层次压暗
float3 uLightColor;  //世界光照色

//深黑紫玻璃底 / 稍亮的紫黑 / 偏光紫 / 镜面淡紫白 / 余温橙
static const float3 ColBase = float3(0.045, 0.028, 0.075);
static const float3 ColLift = float3(0.16, 0.11, 0.24);
static const float3 ColSheen = float3(0.17, 0.10, 0.26);
static const float3 ColSpec = float3(0.90, 0.84, 1.05);
static const float3 ColEmber = float3(1.00, 0.42, 0.14);

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 vColor : COLOR0) : COLOR0
{
    float4 src = tex2D(uImage0, uv);
    if (src.a < 0.01)
        return float4(0, 0, 0, 0);

    //剪影本体：保留原贴图明暗起伏但压进黑紫域，远景更暗
    float lum = dot(src.rgb, float3(0.30, 0.55, 0.15));
    float3 body = lerp(ColBase, ColLift, lum);
    float lightAmt = dot(uLightColor, float3(0.33, 0.34, 0.33));
    body *= (0.45 + 0.55 * lightAmt) * lerp(0.62, 1.0, uDepthDim);

    //紫黑偏光：垂直光向的窄带随相位滑过身体，噪声打碎成微泽而非色带
    float band = dot(uv - 0.5, float2(uLightDir.y, -uLightDir.x));
    float sheen = 0.5 + 0.5 * sin(band * 10.0 + uSheenPhase);
    float sheenN = tex2D(noiseSamp, uv * 1.6 + uSeed * 3.1).r;
    body += ColSheen * sheen * sheen * (0.25 + 0.75 * sheenN) * 0.42;

    //背光侧轮廓压更黑，给薄片一个体积暗缘
    float aBack = tex2D(uImage0, uv - uLightDir * uTexel * 2.2).a;
    float rimBack = saturate(src.a - aBack);
    body *= 1.0 - rimBack * 0.5;

    //余温矿脉：噪声等值细线，平时几乎不可见，受击/碎裂前才烧起来
    float vn = tex2D(noiseSamp, uv * 2.4 + uSeed * 7.7).r;
    float vein = 1.0 - smoothstep(0.012, 0.05, abs(vn - 0.5));
    float3 ember = ColEmber * vein * uCrack * (0.55 + 0.45 * sin(uSheenPhase * 2.3));

    //迎光轮廓窄镜面：双距离差分收窄成一条高光线，角度随光向持续移动
    float aL1 = tex2D(uImage0, uv + uLightDir * uTexel * 1.6).a;
    float aL2 = tex2D(uImage0, uv + uLightDir * uTexel * 3.2).a;
    float rim = saturate(src.a - min(aL1, aL2));
    float spec = pow(rim, 2.4) * uSpec;

    float alpha = src.a * uFade * vColor.a;
    float3 col = (body + ember * 0.85) * alpha + ColSpec * spec * alpha;
    //爆裂前的过曝白闪，调用侧保证 <=2 帧
    col += float3(0.85, 0.78, 1.0) * uFlash * alpha;
    return float4(col, alpha);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
