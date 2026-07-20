// ============================================================================
//FishMudMound.fx 泥鱼哨兵根部泥堆：破土隆起/常驻泥丘/塌陷回吸/地下泥柱四相共用一张quad
//uv.y=0.40处为地面线，上40%画隆起穹顶，下60%画埋入地形的泥柱与裙摆
//泥柱在破土与下潜时升起，盖住鱼体在地面以下的部分，出入场永远读作泥浆而非贴图穿地
//全笛卡尔坐标，无atan2无极角，噪声输入均为平面uv，无缝隙风险
//哑光湿泥：无加色无纯白，唯一亮点是表面窄水光条，随uSink蒸发
//预乘alpha，配BlendState.AlphaBlend
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;   //秒
float uSeed;   //实例随机相位
float uEmerge; //0..1泥丘隆起量
float uBurst;  //0..1破土爆发瞬时量
float uSink;   //0..1塌陷回吸相
float uPlug;   //0..1地下泥柱强度，出入场遮蔽鱼体地下段
float uFade;   //整体不透明度
float3 uLight; //世界光照调制，泥面哑光必须吃环境光

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

//湿泥色程：最深→暗→主体→湿亮→水光
static const float3 ColMurk = float3(0.157, 0.118, 0.094);
static const float3 ColDeep = float3(0.235, 0.173, 0.129);
static const float3 ColBase = float3(0.369, 0.275, 0.192);
static const float3 ColWet = float3(0.502, 0.392, 0.267);
static const float3 ColSheen = float3(0.627, 0.643, 0.580);

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
    //p.x横向-1..1，p.y地面线为0、空中为负、地下为正（quad高130px：地上52地下78）
    float2 p = float2((uv.x - 0.5) * 2.0, (uv.y - 0.40) * 2.5);

    //表面缓慢翻涌：横向噪声起伏，破土与塌陷时涌动加速
    float churn = tex2D(noiseSamp, float2(p.x * 0.55 + uSeed * 7.0, uTime * (0.10 + uBurst * 0.4 + uSink * 0.3) + uSeed)).r;

    //穹顶轮廓：中央高两肩低，破土时肩部外扩
    float shoulder = saturate(1.0 - p.x * p.x * (1.35 - uBurst * 0.25));
    float h = uEmerge * pow(shoulder, 1.35) * (0.72 + churn * 0.34);
    //破土掀泥：中央区域瞬时抬升翻边
    h += uBurst * 0.40 * smoothstep(0.55, 0.15, abs(p.x)) * (0.5 + churn * 0.6);
    //塌陷压平
    h *= 1.0 - uSink * 0.82;

    float above = -p.y;
    //穹顶体：上边缘羽化，横向两端收口
    float dome = smoothstep(-0.07, 0.02, h - above) * smoothstep(1.0, 0.84, abs(p.x));
    //只在地面以上与浅表层生效，深部交给泥柱
    dome *= smoothstep(0.55, 0.30, p.y);

    //地下泥柱：出入场时盖住鱼体地下段，边缘带噪声抖动
    float plugEdge = 0.44 + (tex2D(noiseSamp, float2(p.y * 0.8 + uSeed * 3.0, uTime * 0.15 + uSeed)).r - 0.5) * 0.14;
    float plug = uPlug * smoothstep(plugEdge, plugEdge - 0.24, abs(p.x)) * step(0.0, p.y);
    //柱底淡出
    plug *= smoothstep(1.5, 1.05, p.y);

    float body = saturate(max(dome, plug));
    if (body < 0.004)
        return float4(0, 0, 0, 0);

    //离表面深度：0为表面，向下渐深
    float surfDist = saturate(h - above);
    float3 col = lerp(ColWet, ColBase, saturate(surfDist * 2.4));
    col = lerp(col, ColDeep, saturate(surfDist * 1.4 - 0.42));
    //泥柱区域整体偏深色搅动
    col = lerp(col, lerp(ColMurk, ColDeep, churn), saturate(plug - dome * 0.5));

    //湿泥颗粒斑驳
    float grain = tex2D(noiseSamp, uv * float2(3.1, 2.6) + uSeed * 5.0).r;
    col *= 0.88 + grain * 0.24;

    //表面窄水光：湿面高光，塌陷时随水分回吸消失
    float sheen = smoothstep(0.10, 0.0, abs(surfDist - 0.03)) * dome * (1.0 - uSink) * (0.30 + 0.30 * churn);
    col = lerp(col, ColSheen, sheen * 0.42);

    //破土的新翻湿泥更亮些
    col = lerp(col, ColWet, uBurst * 0.35 * smoothstep(0.6, 0.1, abs(p.x)));

    //塌陷向心回流暗纹+整体沉暗
    float flow = tex2D(noiseSamp, float2(abs(p.x) * 1.3 - uTime * (0.2 + uSink * 0.5), uSeed * 3.0)).r;
    col = lerp(col, ColMurk, uSink * (0.35 + flow * 0.35));

    float alpha = body * uFade;
    return float4(col * uLight * alpha, alpha);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
