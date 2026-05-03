using System;
using UnityEditor;
using UnityEngine;

public static class MiniDecoratorHelpers
{
    public enum LayerName
    {
        gray_stone,
        brown_stone,
        turf,
        steppe,
        snow,
        earth,
        desert,
        forest,
        pebbles,
        village,
        path
    }
    public static void InitBaseDecorator(MBTerrainDecorator decoratorModular)
    {
        if (decoratorModular == null || decoratorModular.t == null)
        {
            Debug.LogError("Decorator or terrain is null. Cannot initialize.");
            return;
        }

        Terrain terrain = decoratorModular.t;
        var leyersCount = Enum.GetValues(typeof(LayerName)).Length;

        decoratorModular.layers.Clear();

        for (int i = 0; i < leyersCount; i++)
        {
            var newLayer = new MBTerrainDecorator.Layers
            {
                name = ((LayerName)i).ToString(),
                active = true,
                layerIndex = i,
                layerType = MBTerrainDecorator.LayerType.texture,
                probability = 0.5f,
                maximumTreeCount = 100,
                width = 1.0f,
                height = 1.0f,
                randomPosition = 0.1f,
                randomRotation = 0.1f,
                randomSize = 0.1f,
                randomHealth = 0.1f,
                offset = 0.1f
            };

            decoratorModular.layers.Add(newLayer);
        }

        Debug.Log($"Initialized {leyersCount} layers with default setup.");
    }

    public static  MBTerrainDecorator.Layers AddPGMRule(MBTerrainDecorator decoratorModular,string layerName,MBTerrainDecorator.BlendType blendType = MBTerrainDecorator.BlendType.add,DefaultAsset pgmAsset = null)
    {
        // if(pgmAsset == null)
        // {
        //     Debug.LogError("PGM asset is null. Cannot add PGM rule.");
        //     return null;
        // }
        //
        var layer = decoratorModular.layers.Find(l => l.name == layerName);
        // Add a PGM rule
        layer.rules.Add(new MBTerrainDecorator.Rules
        {
            active = true,
            filter = MBTerrainDecorator.FilterType.pgm,
            min = 0f,
            max = 1f,
            blend = blendType,
            intensity = 1f,
            contrast = 0f,
            pgmAsset = pgmAsset, // Assign a PGM asset here if needed
            imageChannel = MBTerrainDecorator.ImageChannel.g
        });
        
        return layer;
    }
    
    public static  MBTerrainDecorator.Layers AddGeneratorRule(MBTerrainDecorator decoratorModular,string layerName,MBTerrainDecorator.BlendType blendType = MBTerrainDecorator.BlendType.add,bool active = true,TextAsset generatorAsset = null)
    {
        MBTerrainDecorator.Layers layer = decoratorModular.layers.Find(l => l.name == layerName);
        // Add a Generator rule
        layer.rules.Add(new MBTerrainDecorator.Rules
        {
            active = active,
            filter = MBTerrainDecorator.FilterType.generator,
            min = 0f,
            max = 1f,
            blend = blendType,
            intensity = 1f,
            contrast = 0f,
            generatorAsset = generatorAsset, // Assign a generator asset here if needed
            imageChannel = MBTerrainDecorator.ImageChannel.g
        });
        
        return layer;
    }
}
