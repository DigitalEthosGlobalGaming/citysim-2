HEADER
{
    Description = "Unlit printed owner ink";
}
MODES
{
    Forward();
    Depth( S_MODE_DEPTH );
}
COMMON
{
    #include "common/shared.hlsl"
}
struct VertexInput
{
    #include "common/vertexinput.hlsl"
    float4 vColor : COLOR0 < Semantic( Color ); >;
};
struct PixelInput
{
    #include "common/pixelinput.hlsl"
};
VS
{
    #include "common/vertex.hlsl"
    PixelInput MainVs( VertexInput i )
    {
        PixelInput o = ProcessVertex( i );
        o.vVertexColor = float4( SrgbGammaToLinear( i.vColor.rgb ), i.vColor.a );
        return FinalizeVertex( o );
    }
}
PS
{
    #include "common/pixel.hlsl"
    float4 MainPs( PixelInput i ) : SV_Target0 { return i.vVertexColor; }
}
