// ============================================================================
//KikasaUmbrellaTrail.fx 悬伞墨痕拖尾(世界空间 TriangleStrip,vs+ps)
//被气流从伞面剥离的湿墨:中实边蚀、尾端阈值抬升撕成丝缕、
//轴心一线血芯尾部先断、新段窄湿光
//
//为什么独立成文件:带 VertexShader 的 technique 一旦在某个 Effect 实例上 Apply 过,
//MojoShader 会把它残留在 effect->current_vert_raw 里永不清除;此后同一实例任何
//ps-only pass 的 CommitChanges 都会按残留顶点着色器的符号表,把 transformMatrix
//写进"当前绑定的顶点着色器"的常量寄存器——SpriteBatch 批下即精灵着色器的投影
//矩阵(c0..c3)被世界矩阵顶掉,整批精灵瞬移出屏。故本效果只许被顶点图元消费,
//绝不进 SpriteBatch;伞体的 ps-only technique 留在 KikasaUmbrella.fx
//
//顶点契约(KikasaRainRender.DrawOneInkTrail 同源):
//uv.x=沿带弧长(32px 一单位,世界锚定——点定下后纹理不追着带跑,只留极慢晕开漂移)
//uv.y=0..1 横跨条带;顶点色 R=剩余寿命 G=速度强度 A=头部整体透明度
//坐标全笛卡尔;直线算术+普通 tex2D,FNA3D 安全;预乘输出进 AlphaBlend 批
//s1=PerlinNoise(实测值域 0.227~0.776,阈值前归一);s0 不采样
//消费入口 KikasaRains/KikasaRainRender.cs DrawInkTrails
// ============================================================================

sampler uImage1 : register(s1);

float4x4 transformMatrix;   //世界→屏幕,世界坐标顶点直入

float uTime;
float uSeed;        //个体相位(伞的 identity)
float3 uColInk;     //墨体近黑
float3 uColDeep;    //暗血缘
float3 uColCore;    //血芯
float3 uColSheen;   //湿反光

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

struct VSInput
{
    float4 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 TexCoords: TEXCOORD0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 TexCoords: TEXCOORD0;
};

PSInput VSTrail(VSInput v) {
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float4 PSTrail(PSInput input) : COLOR0 {
    float u = input.TexCoords.x;
    float cx = input.TexCoords.y * 2.0 - 1.0;   //-1..1 横向,0=轴心
    float lifeT = input.Color.r;
    float strength = input.Color.g;
    float headA = input.Color.a;

    //两张错频噪声:n1 蚀边揉形,n2 尾端撕丝;漂移压到极慢=墨在空气里晕开
    float n1 = noiseTex(float2(u * 0.61 + uSeed, cx * 0.35 + uSeed * 1.7 + uTime * 0.10));
    float n2 = noiseTex(float2(u * 1.35 - uSeed * 2.3, cx * 0.22 - uTime * 0.06));
    n1 = saturate((n1 - 0.227) / 0.549);
    n2 = saturate((n2 - 0.227) / 0.549);

    //墨体:中实边蚀;老段与弱段蚀得更碎
    float erode = 0.30 + 0.45 * (1.0 - lifeT) + 0.20 * (1.0 - strength);
    float edge = 1.0 - smoothstep(0.30, 1.0, abs(cx) + (n1 - 0.5) * erode);

    //尾端撕丝:寿命走低阈值抬升,墨被撕成断续丝缕而非整体淡出
    float tearTh = 0.12 + (1.0 - lifeT) * 0.60;
    float tear = smoothstep(tearTh, tearTh + 0.22, n2 * 0.72 + n1 * 0.28);
    float body = edge * tear;

    //浓度:速度强度直接当墨量,出生端满、老段沉
    float density = body * strength * (0.30 + 0.70 * lifeT);

    //色:墨体近黑,轴心沉、缘处透一点暗血
    float3 col = lerp(uColDeep, uColInk, saturate(0.55 + 0.45 * (1.0 - abs(cx)) - n1 * 0.25));

    //血芯:轴心一线暗红,尾部先断(同一撕丝噪声门控,芯不悬空)
    float core = exp2(-cx * cx * 26.0) * smoothstep(0.30, 0.85, lifeT) * tear;

    //新段窄湿光:剥离瞬间的水光,几段内退去
    float dSheen = cx - 0.25;
    float sheen = exp2(-dSheen * dSheen * 34.0) * smoothstep(0.72, 1.0, lifeT) * strength;

    float a = saturate(density * 0.66) * headA;
    float3 outCol = col * a
        + uColCore * core * 0.34 * strength * headA
        + uColSheen * sheen * 0.16 * headA;
    return float4(outCol, a);
}

technique TechTrail {
    pass TrailPass {
        VertexShader = compile vs_3_0 VSTrail();
        PixelShader = compile ps_3_0 PSTrail();
    }
}
