// ============================================================================
//GaolWraithChain.fx 鬼链束缚场（横贯拉锁 / 囚笼链栏的底层灵质带）
//铁链 sprite 是结构载体画在上层；本件负责链底的怨魂束缚场：
//沿线行波灵流 + 噪声撕边 + 两端锚结球根收口（端点物理答案：钉进墙的凝结）
//+ uSnap 绷直白闪行波（自中点向两端各跑一发）+ uDecay 锈解自两端向心蚀散。
//TechBind：vs+ps 条带，uv.x 0=A端 1=B端。预乘输出进 AlphaBlend。
//无动态分支，无极角。s1=PerlinNoise（实测值域 0.227~0.776，过 nrm）
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uSeed;
float uTaut;        //0=预告垂链（场弱而晃） 1=绷紧（场满而稳）
float uSnap;        //绷直白闪强度包络 0~1（绷直拍后几帧）
float uSnapT;       //白闪行波位置 0~1（自中点向两端）
float uDecay;       //锈解 0~1：自两端向心蚀散
float3 uColBody;    //束缚场主体（冷雾青灰）
float3 uColGlow;    //缚力辉（冷粉）
float3 uColHot;     //绷直白闪

sampler noiseSamp : register(s1);

//PerlinNoise.r 实测值域 0.227~0.776
float nrm(float x) {
    return saturate((x - 0.227) / 0.549);
}

float noiseTex(float2 uv) {
    return nrm(tex2D(noiseSamp, uv).r);
}

struct VSInput {
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput {
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VSBind(VSInput v) {
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float4 PSBind(PSInput input) : COLOR0 {
    float2 uv = input.TexCoords;
    float along = uv.x;
    float cross_ = (uv.y - 0.5) * 2.0;

    //行波灵流：两层反向沿线滚动，预告期慢、绷紧期快
    float speed = lerp(0.35, 0.95, uTaut);
    float nA = noiseTex(float2(along * 2.6 - uTime * speed + uSeed * 7.0, 0.30 + uSeed));
    float nB = noiseTex(float2(along * 5.1 + uTime * speed * 0.6 + uSeed * 3.0, 0.72));
    float flow = nA * 0.62 + nB * 0.38;

    //横截面：噪声咬边的软带；预告期更散更飘
    float halfW = lerp(0.55, 0.80, uTaut) + (flow - 0.5) * lerp(0.62, 0.34, uTaut);
    float body = saturate(1.0 - abs(cross_) / max(halfW, 0.05));
    body = pow(body, lerp(1.2, 1.9, uTaut));

    //两端锚结：球根状凝结收口（不平切），锚点自身微亮
    float knotA = exp(-pow(along / 0.055, 2.0));
    float knotB = exp(-pow((1.0 - along) / 0.055, 2.0));
    float knot = knotA + knotB;
    body += knot * 0.85;
    //端外羽化保险：内容在画布端缘自然归零
    body *= smoothstep(0.0, 0.022, along) * smoothstep(1.0, 0.978, along);

    //锈解：自两端向心蚀散，蚀口缘挂锈芒
    float er = min(along, 1.0 - along) * 2.0;
    float nE = noiseTex(float2(along * 3.4 + uSeed * 5.0, 0.5 + uSeed * 2.0));
    float eatFront = uDecay * 1.12;
    float decayKeep = smoothstep(eatFront - 0.10, eatFront + 0.06, er + (nE - 0.5) * 0.34);
    float decayEdge = exp(-abs(er + (nE - 0.5) * 0.34 - eatFront) * 15.0) * step(0.001, uDecay);
    body *= decayKeep;

    //绷直白闪行波：自中点向两端各跑一发的过曝脉冲
    float wavePos = abs(along - 0.5) * 2.0;
    float flash = exp(-pow((wavePos - uSnapT) / 0.13, 2.0)) * uSnap;

    //三层合成：主体冷雾 + 缚力辉沿中脊 + 白闪
    float spine = saturate(1.0 - abs(cross_) / 0.24) * body;
    float3 col = uColBody * body * (0.42 + flow * 0.5)
        + uColGlow * spine * (0.35 + 0.4 * uTaut)
        + uColGlow * knot * 0.55 * decayKeep
        + uColHot * flash * (body + knot * 0.5 * decayKeep)
        + uColGlow * decayEdge * 0.55;

    //预告态整场压低；锈解期随蚀散降势
    float presence = lerp(0.42, 1.0, uTaut) * (1.0 - uDecay * 0.4);
    float alpha = saturate(body) * presence * input.Color.a;
    return float4(col * presence * input.Color.a, alpha);
}

technique TechBind {
    pass P0 {
        VertexShader = compile vs_3_0 VSBind();
        PixelShader = compile ps_3_0 PSBind();
    }
}
