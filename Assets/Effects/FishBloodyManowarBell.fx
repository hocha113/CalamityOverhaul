// ============================================================================
//FishBloodyManowarBell.fx 血腥水母半透明伞膜（批量 quad，一次 DrawUserIndexedPrimitives）
//伞钟椭圆 SDF + 缘部增厚（拟菲涅尔）+ 裙缘噪声撕裂荷叶边 + 纵向水管纹 + 内脏微光。
//收缩相位驱动伞形挤压（窄而长）与伞缘瞬时提亮，读作"收缩-滑行"泳姿的收缩拍。
//顶点色打包：R=收缩量0..1 G=透明度包络 B=每只种子 A=未用。
//quad 局部 uv：x 0..1 横跨伞体，y 0=伞顶(apex) 1=裙底；朝向由 C# 端顶点摆放承载。
//极角审计：无 atan2/theta/phi 消费，全部笛卡尔 uv + 贴图采样，无缝隙风险。
//预乘 alpha 输出，配 BlendState.AlphaBlend：膜体半透明压暗背景，伞缘提亮走 alpha=0 加色分量
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;            //秒
float3 uColMembrane;    //伞膜主体深红
float3 uColDark;        //伞膜暗缘（近黑瘀血）
float3 uColRim;         //收缩提亮的伞缘血色
float3 uColOrgan;       //内脏团微光

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
    float contract = input.Color.r;
    float fade = input.Color.g;
    float seed = input.Color.b * 9.73;

    //局部坐标：x -1..1 横跨，y 0 伞顶 .. 1 裙底
    float2 q = float2(uv.x * 2.0 - 1.0, uv.y);

    //收缩形变：横向挤窄、伞体拉长（喷水推进的收缩拍）
    float rx = 0.80 - 0.20 * contract;
    float stretch = 1.0 + 0.10 * contract;

    //伞钟椭圆场：圆心居中偏上，e=1 为伞缘
    float e = length(float2(q.x / rx, (q.y - 0.52) / (0.58 * stretch)));

    //裙缘荷叶边：正弦扇贝 + 噪声撕裂，收缩时裙摆下探
    float n1 = tex2D(noiseSamp, float2(q.x * 0.61 + seed, seed * 0.37 + uTime * 0.045)).r;
    float skirtY = 0.80 + 0.07 * contract + 0.055 * sin(q.x * 8.0 + seed * 5.0 + uTime * 1.7);
    float skirtEdge = smoothstep(skirtY, skirtY - 0.14 - 0.12 * n1, q.y);

    //伞体遮罩：椭圆软边 × 裙缘撕裂
    float bellMask = smoothstep(1.0, 0.90, e) * skirtEdge;

    //缘部增厚：侧视薄膜的边缘更致密（拟菲涅尔），撑起半透明的"厚度感"
    float rim = smoothstep(0.42, 0.97, e);

    //膜色：体心浅、边缘沉入瘀黑
    float3 col = lerp(uColMembrane, uColDark, rim * 0.82);

    //纵向水管纹：伞膜的放射水管，笛卡尔正弦近似（低幅压暗）
    float canal = 0.5 + 0.5 * sin(q.x * 11.0 + seed * 3.1);
    col = lerp(col, uColDark, canal * (1.0 - rim) * 0.22);

    //内脏团微光：伞顶下方一小团暖沉的血色（结构而非光球，权重克制）
    float2 og = (q - float2(0.0, 0.40)) * float2(2.4, 2.7);
    float organ = saturate(1.0 - dot(og, og));
    col += uColOrgan * organ * organ * 0.30;

    //膜面呼吸噪声：透明度微起伏，半透明活体感
    float n2 = tex2D(noiseSamp, q * 0.55 + seed + uTime * 0.03).r;

    float alpha = bellMask * (0.30 + 0.40 * rim) * (0.82 + 0.30 * n2) * fade;

    //收缩拍伞缘提亮：小面积、随收缩即逝（alpha=0 的纯加色分量）
    float rimLight = smoothstep(0.86, 1.0, e) * skirtEdge * contract;
    float3 addGlow = uColRim * rimLight * 0.55 * fade;

    return float4(col * alpha + addGlow, alpha);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
