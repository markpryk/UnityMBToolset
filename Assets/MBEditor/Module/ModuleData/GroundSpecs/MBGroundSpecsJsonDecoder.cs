using System;
using System.Collections.Generic;
using UnityEngine;

namespace MountAndBlade.Data
{
    public static class MBGroundSpecsJsonDecoder
    {
        public static DecodedGroundSpecsData DecodeGroundSpecs(GroundSpecJsonData[] jsonArray)
        {
            var data = new DecodedGroundSpecsData();
            data.groundSpecs = new List<DecodedGroundSpec>();

            if (jsonArray == null || jsonArray.Length == 0)
                return data;

            foreach (var json in jsonArray)
            {
                var spec = new DecodedGroundSpec
                {
                    id = json.id,
                    index = json.index,
                    groundConstant = json.ground_constant,
                    flags = json.flags?.raw_value ?? 0,
                    material = json.material,
                    uvScale = json.uv_scale,
                    multitexMaterial = json.multitex_material,
                    hasColor = false,
                    color = Color.white
                };

                if (json.color != null)
                {
                    spec.hasColor = true;
                    spec.color = new Color(json.color.r, json.color.g, json.color.b, 1f);
                }

                data.groundSpecs.Add(spec);
            }

            return data;
        }

        [Serializable]
        public class GroundSpecJsonData
        {
            public string id;
            public int index;
            public string ground_constant;
            public FlagsJsonData flags;
            public string material;
            public float uv_scale;
            public string multitex_material;
            public ColorJsonData color;
        }

        [Serializable]
        public class FlagsJsonData
        {
            public int raw_value;
            public string hex;
            public string symbolic;
        }

        [Serializable]
        public class ColorJsonData
        {
            public float r;
            public float g;
            public float b;
            public string html_clamped;
        }

        [Serializable]
        public class DecodedGroundSpec
        {
            public string id;
            public int index;
            public string groundConstant;
            public int flags;
            public string material;
            public float uvScale;
            public string multitexMaterial;
            public bool hasColor;
            public Color color;
        }

        [Serializable]
        public class DecodedGroundSpecsData
        {
            public List<DecodedGroundSpec> groundSpecs;
        }
    }
}
