Shader "Noble Muffins/Doublesided (Two-Tex, Unlit)" {
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" { }
        _InsideTex ("Insides (RGB)", 2D) = "white" { }
    }

    Category
    {
	    BindChannels {
		   Bind "Vertex", vertex
		   Bind "texcoord", texcoord0
	   }
        ZWrite On
        SubShader
        {
            Lighting Off
            Pass
            {
        		Cull Back
                SetTexture [_MainTex]
                {
                    Combine texture
                } 
            }
            Pass
            {
        		Cull Front
                SetTexture [_InsideTex]
                {
                    Combine texture
                } 
            }
        } 
    }
}
