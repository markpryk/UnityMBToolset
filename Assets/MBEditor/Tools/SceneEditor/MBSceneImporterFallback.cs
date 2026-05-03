using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Fallback utility for importing scene data from problematic JSON formats
/// </summary>
public static class MBSceneImporterFallback
{
    /// <summary>
    /// Tries to load scene data using a more robust approach for non-standard JSON formats
    /// </summary>
    /// <param name="scenesDataPath">Path to the scenes JSON file</param>
    /// <param name="sceneId">The scene ID to load</param>
    /// <returns>The loaded scene data or null if not found</returns>
    // public static MBJsonData.MBJsonDataModuleScene TryLoad(string scenesDataPath, string sceneId)
    // {
    //     try
    //     {
    //         Debug.Log($"Attempting fallback load for scene: {sceneId}");
    //         
    //         if (!File.Exists(scenesDataPath))
    //         {
    //             Debug.LogError($"Scene data file not found: {scenesDataPath}");
    //             return null;
    //         }
    //
    //         string rawJson = File.ReadAllText(scenesDataPath);
    //         
    //         // Try to extract the specific scene directly
    //         JObject rootObj = JObject.Parse(rawJson);
    //         JArray scenesArray = null;
    //         
    //         // Try different possible scene arrays
    //         foreach (var propName in new[] { "scenes", "Scenes" })
    //         {
    //             if (rootObj[propName] != null && rootObj[propName].Type == JTokenType.Array)
    //             {
    //                 scenesArray = (JArray)rootObj[propName];
    //                 break;
    //             }
    //         }
    //         
    //         if (scenesArray == null)
    //         {
    //             Debug.LogError("Failed to find scenes array in JSON");
    //             return null;
    //         }
    //         
    //         // Custom deserialization approach for problematic JSON
    //         foreach (JObject sceneObj in scenesArray)
    //         {
    //             // Try different ID field names
    //             string id = null;
    //             foreach (var idField in new[] { "id", "Id", "ID" })
    //             {
    //                 if (sceneObj[idField] != null)
    //                 {
    //                     id = sceneObj[idField].ToString();
    //                     break;
    //                 }
    //             }
    //             
    //             if (id == sceneId)
    //             {
    //                 Debug.Log($"Found scene in fallback: {id}");
    //                 
    //                 // Create a new scene object with defensive parsing
    //                 var scene = new MBJsonData.MBJsonDataModuleScene
    //                 {
    //                     Id = id,
    //                     Flags = SafeGetInt(sceneObj, new[] { "flags", "Flags" }),
    //                     MeshName = SafeGetString(sceneObj, new[] { "mesh", "Mesh", "MeshName" }),
    //                     BodyName = SafeGetString(sceneObj, new[] { "body", "Body", "BodyName" }),
    //                     WaterLevel = SafeGetFloat(sceneObj, new[] { "water_level", "WaterLevel" }),
    //                     TerrainCode = SafeGetString(sceneObj, new[] { "terrain_code", "TerrainCode" })
    //                 };
    //                 
    //                 // Handle passages (could be string or array)
    //                 var passageToken = SafeGetToken(sceneObj, new[] { "passages", "Passages", "name", "Name" });
    //                 if (passageToken != null)
    //                 {
    //                     scene.Passages = TokenToStringList(passageToken);
    //                 }
    //                 
    //                 // Handle chests (could be string or array)
    //                 var chestsToken = SafeGetToken(sceneObj, new[] { "chests", "Chests" });
    //                 if (chestsToken != null)
    //                 {
    //                     scene.Chests = TokenToStringList(chestsToken);
    //                 }
    //                 
    //                 // Handle bounds
    //                 HandleBounds(sceneObj, scene);
    //                 
    //                 return scene;
    //             }
    //         }
    //         
    //         Debug.LogError($"Scene with ID '{sceneId}' not found in fallback JSON parsing");
    //         return null;
    //     }
    //     catch (Exception ex)
    //     {
    //         Debug.LogError($"Error in fallback scene loading: {ex.Message}");
    //         Debug.LogException(ex);
    //         return null;
    //     }
    // }
    
    // Helper method to safely convert a token to a List<string>
    private static List<string> TokenToStringList(JToken token)
    {
        if (token == null)
            return null;
            
        if (token.Type == JTokenType.Array)
        {
            var result = new List<string>();
            foreach (var item in token)
            {
                result.Add(item.ToString());
            }
            return result;
        }
        else if (token.Type == JTokenType.String)
        {
            return new List<string> { token.ToString() };
        }
        
        return null;
    }
    
    // Helper method to safely get a token by multiple possible names
    private static JToken SafeGetToken(JObject obj, string[] names)
    {
        foreach (var name in names)
        {
            if (obj[name] != null)
                return obj[name];
        }
        return null;
    }
    
    // Helper method to safely get a string by multiple possible names
    private static string SafeGetString(JObject obj, string[] names)
    {
        var token = SafeGetToken(obj, names);
        return token?.ToString();
    }
    
    // Helper method to safely get an int by multiple possible names
    private static int SafeGetInt(JObject obj, string[] names, int defaultValue = 0)
    {
        var token = SafeGetToken(obj, names);
        if (token == null)
            return defaultValue;
            
        if (token.Type == JTokenType.Integer)
            return (int)token;
            
        if (int.TryParse(token.ToString(), out int result))
            return result;
            
        return defaultValue;
    }
    
    // Helper method to safely get a float by multiple possible names
    private static float SafeGetFloat(JObject obj, string[] names, float defaultValue = 0f)
    {
        var token = SafeGetToken(obj, names);
        if (token == null)
            return defaultValue;
            
        if (token.Type == JTokenType.Float)
            return (float)token;
            
        if (float.TryParse(token.ToString(), out float result))
            return result;
            
        return defaultValue;
    }
    
    // Helper method to handle bounds data
    // private static void HandleBounds(JObject sceneObj, MBJsonData.MBJsonDataModuleScene scene)
    // {
    //     var boundsToken = SafeGetToken(sceneObj, new[] { "bounds", "Bounds" });
    //     
    //     if (boundsToken == null || boundsToken.Type != JTokenType.Object)
    //         return;
    //         
    //     JObject boundsObj = (JObject)boundsToken;
    //     scene.Bounds = new MBJsonData.BoundsData();
    //     
    //     // Handle min bounds
    //     var minToken = SafeGetToken(boundsObj, new[] { "min", "Min" });
    //     if (minToken != null)
    //     {
    //         if (minToken.Type == JTokenType.Array)
    //         {
    //             try {
    //                 scene.Bounds.Min = minToken.ToObject<float[]>();
    //             }
    //             catch {
    //                 scene.Bounds.Min = new float[3] { 0, 0, 0 };
    //             }
    //         }
    //         else if (minToken.Type == JTokenType.Object)
    //         {
    //             // Handle Vector3 style object with x,y,z properties
    //             scene.Bounds.Min = new float[]
    //             {
    //                 SafeGetFloat(minToken as JObject, new[] { "x", "X" }),
    //                 SafeGetFloat(minToken as JObject, new[] { "y", "Y" }),
    //                 SafeGetFloat(minToken as JObject, new[] { "z", "Z" })
    //             };
    //         }
    //     }
    //     
    //     // Handle max bounds
    //     var maxToken = SafeGetToken(boundsObj, new[] { "max", "Max" });
    //     if (maxToken != null)
    //     {
    //         if (maxToken.Type == JTokenType.Array)
    //         {
    //             try {
    //                 scene.Bounds.Max = maxToken.ToObject<float[]>();
    //             }
    //             catch {
    //                 scene.Bounds.Max = new float[3] { 0, 0, 0 };
    //             }
    //         }
    //         else if (maxToken.Type == JTokenType.Object)
    //         {
    //             // Handle Vector3 style object with x,y,z properties
    //             scene.Bounds.Max = new float[]
    //             {
    //                 SafeGetFloat(maxToken as JObject, new[] { "x", "X" }),
    //                 SafeGetFloat(maxToken as JObject, new[] { "y", "Y" }),
    //                 SafeGetFloat(maxToken as JObject, new[] { "z", "Z" })
    //             };
    //         }
    //     }
    // }
}