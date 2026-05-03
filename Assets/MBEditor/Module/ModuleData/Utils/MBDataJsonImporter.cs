using UnityEngine;
using UnityEditor;
using System.Collections.Generic;using System.IO;
using System.Diagnostics;
using System.Linq;
using UnityEngine.UIElements;

namespace MountAndBlade.ModdingToolkit
{
    /// <summary>
    /// Unity Editor tool to execute Mount & Blade Python converters
    /// </summary>
    public class MBDataJsonImporter
    {
        #region Converter Configuration
        
        public static List<ConverterConfig> ConverterConfigs = new List<ConverterConfig>
        {
            new ConverterConfig("convert_animations.py", "module_animations.py", "animations_full.json"),
            new ConverterConfig("convert_factions.py", "module_factions.py", "factions_full.json"),
            new ConverterConfig("convert_flora.py", "Flora_kinds.py", "flora_full.json"),
            new ConverterConfig("convert_ground_specs.py", "Ground_specs.py", "ground_specs_full.json"),
            new ConverterConfig("convert_items.py", "module_items.py", "items_full.json"),
            new ConverterConfig("convert_map_icons.py", "module_map_icons.py", "map_icons_full.json"),
            new ConverterConfig("convert_meshes.py", "module_meshes.py", "meshes_full.json"),
            new ConverterConfig("convert_music.py", "module_music.py", "music_full.json"),
            new ConverterConfig("convert_particle_systems.py", "module_particle_systems.py", "particle_systems_full.json"),
            new ConverterConfig("convert_parties.py", "module_parties.py", "parties_full.json"),
            new ConverterConfig("convert_party_templates.py", "module_party_templates.py", "party_templates_full.json"),
            new ConverterConfig("convert_postfx.py", "module_postfx.py", "postfx_full.json"),
            new ConverterConfig("convert_scene_props.py", "module_scene_props.py", "scene_props_full.json"),
            new ConverterConfig("convert_scenes.py", "module_scenes.py", "scenes_full.json"),
            new ConverterConfig("convert_skills.py", "module_skills.py", "skills_full.json"),
            new ConverterConfig("convert_skins.py", "module_skins.py", "skins_full.json"),
            new ConverterConfig("convert_skyboxes.py", "Skyboxes.py", "skyboxes_full.json"),
            new ConverterConfig("convert_sounds.py", "module_sounds.py", "sounds_full.json"),
            new ConverterConfig("convert_strings.py", "module_strings.py", "strings_full.json"),
            new ConverterConfig("convert_troops.py", "module_troops.py", "troops_full.json")
        };
        
        public class ConverterConfig
        {
            public string ConverterFileName;
            public string RequiredModuleFile;
            public string OutputJsonName;
            public bool IsFound = false;
            public string ManualPath = "";
            
            public ConverterConfig(string converter, string module, string output)
            {
                ConverterFileName = converter;
                RequiredModuleFile = module;
                OutputJsonName = output;
            }
        }
        
        #endregion
    }
}